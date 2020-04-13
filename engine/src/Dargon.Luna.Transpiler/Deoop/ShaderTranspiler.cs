using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Dargon.Commons;
using Dargon.Commons.Cli;
using Dargon.Commons.Collections;
using Dargon.Luna.Lang;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Dargon.Luna.Transpiler.Deoop.TranspilerInternalStatics;

namespace Dargon.Luna.Transpiler.Deoop {
   public class BaseTranspilationVisitor : CSharpSyntaxVisitor {
      protected readonly TranspilationContext context;

      public BaseTranspilationVisitor(TranspilationContext context) {
         this.context = context;
      }

      public Mangler Mangler => context.Mangler;

      [Flags]
      public enum TokenFlags {
         Trivia = 1 << 0,
         Whitespace = 1 << 1,
      }

      public void Emit(string token, SyntaxNode tag, TokenFlags flags = 0) {
         var (fg, bg) = (flags & TokenFlags.Trivia) != 0 ? (ConsoleColor.DarkGray, null) :
            (flags & TokenFlags.Whitespace) != 0 ? (ConsoleColor.Gray, null) :
            ((ConsoleColor?)ConsoleColor.White, (ConsoleColor?)null);

         using (new ConsoleColorSwitch().To(fg, bg)) {
            Console.Write(token + " ");
         }
      }

      public void EmitSpace(SyntaxNode tag) {
         Emit("", tag, TokenFlags.Whitespace);
      }

      public override void DefaultVisit(SyntaxNode node) {
         base.DefaultVisit(node);

         // Emit($"/*{node.GetType().Name}*/", node, TokenFlags.Trivia);

         var childs = node.ChildNodesAndTokens();
         VisitNodesAndEmitTokens(childs, node, 0, childs.Count);
      }

      protected void VisitNodesAndEmitTokens(ChildSyntaxList childs, SyntaxNode tokenTag, int startIndexInclusive, int endIndexExclusive) {
         for (var index = startIndexInclusive; index < endIndexExclusive; index++) {
            var child = childs[index];
            if (child.IsToken) {
               Emit(child.AsToken().Text, tokenTag);
            } else {
               Visit(child.AsNode());
            }
         }
      }

      public bool TryGetConstantBufferAttribute(ISymbol symbol, out AttributeData attributeData, out ConstantBufferAttribute attribute) 
         => TryGetAttribute(symbol, context.KnownTypes.ConstantBufferAttributeSymbol, out attributeData, out attribute);

      public bool TryGetBakedArrayLengthAttribute(ISymbol symbol, out AttributeData attributeData, out TranspilerBakedArrayLengthAttribute attribute)
         => TryGetAttribute(symbol, context.KnownTypes.TranspilerBakedArrayLengthAttributeSymbol, out attributeData, out attribute);

      public bool TryGetAttribute<TAttribute>(ISymbol symbol, INamedTypeSymbol attributeSymbol, out AttributeData attributeData, out TAttribute attribute) where TAttribute : Attribute {
         symbol.ThrowIfNull(nameof(symbol));
         attributeSymbol.ThrowIfNull(nameof(attributeSymbol));

         var attributes = symbol.GetAttributes();
         foreach (var attr in attributes) {
            if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeSymbol)) {
               continue;
            }

            attributeData = attr;
            attribute = InstantiateAttribute<TAttribute>(attr);
            return true;
         }

         attributeData = null;
         attribute = null;
         return false;
      }

      protected T InstantiateAttribute<T>(AttributeData data) where T : Attribute {
         var ctors = typeof(T).GetConstructors();
         var ctor = ctors.FirstAndOnly();
         var inst = (T)ctor.Invoke(data.ConstructorArguments.Map(a => a.Value));
         foreach (var (key, assignment) in data.NamedArguments) {
            typeof(T).GetProperty(key, BindingFlags.Instance).SetValue(inst, assignment.Value);
         }
         return inst;
      }

      protected void QueueAndEmitMethodIdentifier(IMethodSymbol method, SyntaxNode methodNode) {
         Emit(Mangler.MangleFullName(method), methodNode);

         context.QueuedMethods.Add(new MethodTranspilationJob {
            Method = method,
         });
      }

      protected void QueueType(ITypeSymbol type) {
         if (context.KnownTypes.IsFrameworkType(type)) return;
         context.QueuedTypes.Add(TypeTranspilationJob.Create(type));
      }

      protected void QueueType(TypeSyntax node)
         => QueueAndEmitTypeIdentifierInternal(node, false);

      protected void QueueAndEmitTypeIdentifier(TypeSyntax node)
         => QueueAndEmitTypeIdentifierInternal(node, true);

      protected void QueueAndEmitTypeIdentifierInternal(TypeSyntax node, bool emit) {
         var semanticModel = context.SemanticModelCache.Get(node.SyntaxTree);
         var symbol = (ITypeSymbol)semanticModel.GetSymbolInfo(node).Symbol;
         Assert.IsNotNull(symbol);

         if (SymbolEqualityComparer.Default.Equals(symbol, context.KnownTypes.FloatSymbol)) {
            if (emit) Emit("float", node);
         } else {
            Assert.IsFalse(context.KnownTypes.IsFrameworkType(symbol));

            var declarationSyntax = node.GetTypeDeclaringSyntax(semanticModel);
            var mangledTypeName = Mangler.MangleFullName(declarationSyntax);
            if (emit) Emit(mangledTypeName, node);

            context.QueuedTypes.Add(new TypeTranspilationJob {
               TypeSymbol = symbol,
            });
         }
      }

      protected void EmitTypeDeclarationSyntaxMangledIdentifier(BaseTypeDeclarationSyntax declarationSyntax, SyntaxNode tag) {
         var mangledTypeName = Mangler.MangleFullName(declarationSyntax);
         Emit(mangledTypeName, tag);
      }
   }

   public class ShaderTranspiler {
      private readonly SemanticModelCache semanticModelCache;
      private readonly KnownTypes knownTypes;

      public ShaderTranspiler(SemanticModelCache semanticModelCache, KnownTypes knownTypes) {
         this.semanticModelCache = semanticModelCache;
         this.knownTypes = knownTypes;
      }

      public void Transpile(IMethodSymbol method) {
         var context = new TranspilationContext();
         context.KnownTypes = knownTypes;
         context.SemanticModelCache = semanticModelCache;
         context.Mangler = new Mangler(context);
         context.QueuedMethods.Add(new MethodTranspilationJob {
            Method = method,
         });

         var methodVisitor = new MethodTranspilationVisitor(context);
         for (var i = 0; i < context.QueuedMethods.Count; i++) {
            var target = context.QueuedMethods[i];

            Console.WriteLine($"== TRANSPILE {target.Method} ==");
            methodVisitor.Transpile(target);
            Console.WriteLine();
            Console.WriteLine();
         }

         var typeVisitor = new TypeTranspilationVisitor(context);
         for (var i = 0; i < context.QueuedTypes.Count; i++) {
            var target = context.QueuedTypes[i];

            Console.WriteLine($"== TRANSPILE {target} ==");
            typeVisitor.Transpile(target);
            Console.WriteLine();
            Console.WriteLine();
         }
      }

      public class MethodTranspilationVisitor : BaseTranspilationVisitor {
         public MethodTranspilationVisitor(TranspilationContext context) : base(context) { }

         public void Transpile(MethodTranspilationJob job) {
            var declarations = job.Method.DeclaringSyntaxReferences;
            Assert.Equals(1, declarations.Length);

            VisitBaseMethodDeclarationInternal((BaseMethodDeclarationSyntax)declarations[0].GetSyntax());
         }

         public override void VisitMethodDeclaration(MethodDeclarationSyntax node) {
            AbortNotSupported("Reached Method Declaration?", node);
         }

         public void VisitBaseMethodDeclarationInternal(BaseMethodDeclarationSyntax node) {
            var semanticModel = context.SemanticModelCache.Get(node.SyntaxTree);
            var method = (IMethodSymbol)semanticModel.GetDeclaredSymbol(node);
            var containingType = method.ReceiverType;
            QueueType(containingType);

            // emit return type
            ITypeSymbol selfTypeOrNull;
            TypeDeclarationSyntax structCtorTypeOrNull;

            if (node is MethodDeclarationSyntax mds) {
               selfTypeOrNull = method.IsStatic ? null : method.ReceiverType;
               structCtorTypeOrNull = null;

               QueueAndEmitTypeIdentifier(mds.ReturnType);
            } else if (node is ConstructorDeclarationSyntax ctor) {
               selfTypeOrNull = null;
               structCtorTypeOrNull = (TypeDeclarationSyntax)ctor.Parent;

               EmitTypeDeclarationSyntaxMangledIdentifier(structCtorTypeOrNull, ctor);
            } else {
               throw AbortNotSupported("Unknown method-like: ", node);
            }

            var mangledFunctionName = Mangler.MangleFullName(node);
            Emit(mangledFunctionName, node);

            VisitParameterListInternal(selfTypeOrNull, node.ParameterList);

            if (node.ExpressionBody != null) {
               Emit("{", node.ExpressionBody);
               Emit("return", node.ExpressionBody);
               Visit(node.ExpressionBody.Expression);
               Emit(";", node.ExpressionBody);
               Emit("}", node.ExpressionBody);
            } else {
               VisitBlockInternal(node.Body, structCtorTypeOrNull, node);
            }
         }

         public override void VisitParameterList(ParameterListSyntax node) {
            AbortNotSupported("??", node);
         }

         public void VisitParameterListInternal(ITypeSymbol selfTypeOrNull, ParameterListSyntax node) {
            Emit("(", node);

            bool firstParameter = true;

            if (selfTypeOrNull != null) {
               Emit("inout", node);
               Emit(Mangler.MangleFullName(selfTypeOrNull), node);
               Emit(SELF_TOKEN, node);
               firstParameter = false;
            }

            var nodeParameters = node.Parameters;
            for (var i = 0; i < nodeParameters.Count; i++) {
               if (!firstParameter) Emit(",", node);
               VisitParameter(nodeParameters[i]);
               firstParameter = false;
            }

            Emit(")", node);
         }

         public override void VisitParameter(ParameterSyntax node) {
            QueueAndEmitTypeIdentifier(node.Type);
            Emit(node.Identifier.Text, node);

            if (node.AttributeLists.Count != 0) Abort("Attribute List", node);
            if (node.Default != null) Abort("Default", node);
            if (node.Modifiers.Count != 0) Abort("Modifier", node);
         }

         public override void VisitBlock(BlockSyntax node) {
            VisitBlockInternal(node, null, null);
         }

         private void VisitBlockInternal(BlockSyntax node, TypeDeclarationSyntax structCtorSelfOpt, BaseMethodDeclarationSyntax structCtorAutogenCtorTagOpt) {
            var childs = node.ChildNodesAndTokens();
            Assert.Equals("{", childs[0].AsToken().Text);
            Assert.Equals("}", childs[childs.Count - 1].AsToken().Text);

            Emit("{", node);

            if (structCtorSelfOpt != null) {
               EmitTypeDeclarationSyntaxMangledIdentifier(structCtorSelfOpt, structCtorAutogenCtorTagOpt);
               Emit(SELF_TOKEN, structCtorAutogenCtorTagOpt);
               Emit("=", structCtorAutogenCtorTagOpt);
               Emit("{}", structCtorAutogenCtorTagOpt);
               Emit(";", structCtorAutogenCtorTagOpt);
            }

            VisitNodesAndEmitTokens(childs, node, 1, childs.Count - 1);

            if (structCtorSelfOpt != null) {
               Emit("return", structCtorAutogenCtorTagOpt);
               Emit(SELF_TOKEN, structCtorAutogenCtorTagOpt);
               Emit(";", structCtorAutogenCtorTagOpt);
            }

            Emit("}", node);
         }

         public override void VisitVariableDeclaration(VariableDeclarationSyntax node) {
            QueueAndEmitTypeIdentifier(node.Type);

            var variableDeclarators = node.Variables;
            for (var i = 0 ; i < variableDeclarators.Count; i++) {
               if (i != 0) Emit(",", node);
               Visit(variableDeclarators[i]);
            }
         }

         /// Note: this doesn't cover `default` itself. <seealso cref="VisitLiteralExpression"/>
         public override void VisitDefaultExpression(DefaultExpressionSyntax node) {
            Emit("{}", node);
         }

         /// <seealso cref="VisitDefaultExpression"/> for `default(..)`
         public override void VisitLiteralExpression(LiteralExpressionSyntax node) {
            if (node.Token.IsKind(SyntaxKind.DefaultKeyword)) {
               Emit("{}", node);
            } else {
               Emit(node.Token.Text, node);
            }
         }

         public override void VisitInvocationExpression(InvocationExpressionSyntax node) {
            var (methodIdentifierNode, invocationTarget) =
               node.Expression is MemberAccessExpressionSyntax mae
                  ? ((SyntaxNode)mae.Name, mae.Expression)
                  : (node.Expression, null);

            var invocationSemanticModel = context.SemanticModelCache.Get(node.SyntaxTree);
            var methodSymbolInfo = invocationSemanticModel.GetSymbolInfo(methodIdentifierNode);
            var method = (IMethodSymbol)methodSymbolInfo.Symbol;
            Assert.IsNotNull(method);

            QueueAndEmitMethodIdentifier(method, methodIdentifierNode);
            VisitArgumentListInternal(method, invocationTarget, node.ArgumentList);

            if (invocationTarget != null) {
               var invocationTargetTypeInfo = invocationSemanticModel.GetTypeInfo(invocationTarget);
               var invocationTargetType = invocationTargetTypeInfo.Type;
               QueueType(invocationTargetType);
            }
         }

         public void VisitArgumentListInternal(IMethodSymbol method, ExpressionSyntax invocationTarget, ArgumentListSyntax node) {
            Emit("(", node);

            bool firstArgument = true;

            if ((!method.IsStatic && method.MethodKind != MethodKind.Constructor) || (invocationTarget != null)) {
               firstArgument = false;

               if (invocationTarget != null) {
                  Visit(invocationTarget);
               } else {
                  Emit(SELF_TOKEN, node);
               }
            }

            var nodeArguments = node.Arguments;
            for (var i = 0; i < nodeArguments.Count; i++) {
               if (!firstArgument) Emit(",", node);
               VisitArgument(nodeArguments[i]);
               firstArgument = false;
            }

            Emit(")", node);
         }

         public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node) {
            var semanticModel = context.SemanticModelCache.Get(node.SyntaxTree);
            var ctor = (IMethodSymbol)semanticModel.GetSymbolInfo(node).Symbol;

            QueueType(node.Type);

            // Note: an implicit ctor still has a method symbol w/ no declaration.
            if (ctor.Parameters.Length == 0 && ctor.DeclaringSyntaxReferences.Length == 0) {
               // TODO: This is broken if a parameterless ctor's class has field initializers!
               Emit("{}", node);
            } else {
               QueueAndEmitMethodIdentifier(ctor, node);
               VisitArgumentListInternal(ctor, null, node.ArgumentList);
            }
         }

         public override void VisitThisExpression(ThisExpressionSyntax node) {
            Emit(SELF_TOKEN, node);
         }

         public override void VisitIdentifierName(IdentifierNameSyntax node) {
            var isImplicitThisCandidate =
               !(node.Parent is MemberAccessExpressionSyntax mae) || mae.Expression == node;

            if (isImplicitThisCandidate) {
               var semanticModel = context.SemanticModelCache.Get(node.SyntaxTree);
               var symbol = semanticModel.GetSymbolInfo(node).Symbol;
               var declarations = symbol.DeclaringSyntaxReferences;
               Assert.Equals(1, declarations.Length);

               var decl = declarations[0].GetSyntax();

               if ((decl is VariableDeclaratorSyntax declarator &&
                    declarator.Parent is VariableDeclarationSyntax declaration &&
                    declaration.Parent is FieldDeclarationSyntax fieldDeclaration)) {
                  Emit(SELF_TOKEN, node);
                  Emit(".", node);
               }
            }

            Emit(node.Identifier.Text, node);
         }

         public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node) {
            var semanticModel = context.SemanticModelCache.Get(node.SyntaxTree);
            var targetSymbol = semanticModel.GetSymbolInfo(node.Name).Symbol;
            Assert.IsNotNull(targetSymbol);


            var containingType = targetSymbol.ContainingType;
            if (containingType != null && TryGetConstantBufferAttribute(containingType, out _, out var constantBufferAttribute)) {
               var cbName = constantBufferAttribute.Name ?? containingType.Name;
               Emit(cbName + "_" + node.Name.Identifier, node);
            } else {
               base.VisitMemberAccessExpression(node);
            }
         }
      }

      public class TypeTranspilationVisitor : BaseTranspilationVisitor {
         public TypeTranspilationVisitor(TranspilationContext context) : base(context) { }

         public void Transpile(TypeTranspilationJob job) {
            // Supporting partial classes will be annoying - how to canonically order fields?
            // The C# standard sets field ordering for partial classes to undefined. It's probably
            // best don't handle this (hey, maybe it'll make marshalling data to GPU easier!)
            var declarations = job.TypeSymbol.DeclaringSyntaxReferences;
            Assert.Equals(1, declarations.Length);

            var typeDeclarationNode = (BaseTypeDeclarationSyntax)declarations[0].GetSyntax();
            var semanticModel = context.SemanticModelCache.Get(typeDeclarationNode.SyntaxTree);

            Emit("struct", typeDeclarationNode);
            EmitTypeDeclarationSyntaxMangledIdentifier(typeDeclarationNode, typeDeclarationNode);

            Emit("{", typeDeclarationNode);

            if (typeDeclarationNode.BaseList != null) {
               var baseTypes = typeDeclarationNode.BaseList.Types;
               foreach (var baseTypeNodeWrapper in baseTypes) {
                  var baseTypeNode = baseTypeNodeWrapper.Type;

                  var baseSymbol = (ITypeSymbol)semanticModel.GetSymbolInfo(baseTypeNode).Symbol;
                  if (baseSymbol.TypeKind != TypeKind.Class) continue;

                  QueueAndEmitTypeIdentifier(baseTypeNode);
                  QueueAndEmitTypeIdentifier(baseTypeNode);
                  Emit(";", baseTypeNode);
               }
            }

            foreach (var fieldDeclarationNode in typeDeclarationNode.ChildNodes().OfType<FieldDeclarationSyntax>()) {
               VisitFieldDeclaration(fieldDeclarationNode);
            }

            Emit("}", typeDeclarationNode);

            // Visit(typeDeclarationNode);
         }

         public override void VisitFieldDeclaration(FieldDeclarationSyntax node) {
            var semanticModel = context.SemanticModelCache.Get(node.SyntaxTree);

            var variablesDeclaration = node.Declaration;
            var variableTypeNode = variablesDeclaration.Type;
            var variableTypeSymbol = semanticModel.GetSymbolInfo(variableTypeNode).Symbol;

            var declaredVariableSymbols = variablesDeclaration.Variables.Map(v => semanticModel.GetDeclaredSymbol(v));

            var arrayVariableTypeNode = variableTypeNode as ArrayTypeSyntax;
            var isArrayVariableType = arrayVariableTypeNode != null;

            // Don't emit constant buffers, warn if mixing CB & array type.
            var isConstantBufferVariable = false;
            var typeHasConstantBufferAttribute = TryGetConstantBufferAttribute(variableTypeSymbol, out var typeConstantBufferData, out var typeConstantBufferAttribute);
            if (typeHasConstantBufferAttribute) {
               isConstantBufferVariable = true;
            }

            foreach (var declaredVariableSymbol in declaredVariableSymbols) {
               var fieldHasConstantBufferAttribute = TryGetConstantBufferAttribute(declaredVariableSymbol, out var fieldConstantBufferData, out var fieldConstantBufferAttribute);
               if (!fieldHasConstantBufferAttribute) {
                  if (typeHasConstantBufferAttribute) WarningConstantAttributeInTypeButNotField(variableTypeNode, node);
               } else {
                  if (!typeHasConstantBufferAttribute) WarningConstantAttributeInFieldButNotType(node, variableTypeNode);
                  isConstantBufferVariable = true;
               }
            }

            if (isArrayVariableType && isConstantBufferVariable) AbortNotSupported("Constant Buffer Arrays", node);

            if (isConstantBufferVariable) return;


            if (isArrayVariableType) {
               Assert.Equals(1, variablesDeclaration.Variables.Count);
               
               var variableNode = variablesDeclaration.Variables[0];
               var declaredSymbol = semanticModel.GetDeclaredSymbol(variableNode);
               if (!TryGetBakedArrayLengthAttribute(declaredSymbol, out var attributeData, out var bakedArrayLengthAttribute)) {
                  Abort($"Lacked {nameof(TranspilerBakedArrayLengthAttribute)}", node);
               }

               var attributeNode = attributeData.ApplicationSyntaxReference.GetSyntax();

               QueueAndEmitTypeIdentifier(arrayVariableTypeNode.ElementType);
               Emit(variableNode.Identifier.Text, variableNode);
               Emit("[", attributeNode);
               Emit(bakedArrayLengthAttribute.Length.ToString(), attributeNode);
               Emit("]", attributeNode);
               Emit(";", node);
            } else {
               QueueAndEmitTypeIdentifier(variablesDeclaration.Type);
               for (var i = 0; i < variablesDeclaration.Variables.Count; i++) {
                  if (i != 0) Emit(",", variablesDeclaration);

                  var variableDeclarator = variablesDeclaration.Variables[i];
                  Emit(variableDeclarator.Identifier.Text, variableDeclarator);
               }

               Emit(";", node);
            }
         }
      }
   }

   public class Mangler {
      private const string MANGLE_NAMESPACE_DELIMITER = "_";
      private const string MANGLE_CLASS_DELIMITER = "___";
      private const string MANGLE_METHOD_DELIMITER = "69";
      private const string MANGLE_CTOR_PARAM_TYPE_DELIMITER = "420";

      private readonly TranspilationContext context;

      public Mangler(TranspilationContext context) {
         this.context = context;
      }

      // TODO: This is implemented O(N^2). Can be O(N). Consider memoizing.
      public string MangleFullName(MemberDeclarationSyntax mds) {
         if (mds is NamespaceDeclarationSyntax ns) {
            return MangleNamespaceName(ns.Name);
         }

         var name =
            mds is TypeDeclarationSyntax type ? type.Identifier.Text :
            mds is ConstructorDeclarationSyntax ctor ? MangleConstructorName(ctor) :
            mds is MethodDeclarationSyntax method ? MangleMethodName(method) :
            throw Abort("Unhandled Node Type", mds);

         if (mds.Parent is MemberDeclarationSyntax parent) {
            var delimiter =
               parent is NamespaceDeclarationSyntax ? MANGLE_NAMESPACE_DELIMITER :
               parent is TypeDeclarationSyntax ? MANGLE_CLASS_DELIMITER :
               parent is ConstructorDeclarationSyntax ? MANGLE_METHOD_DELIMITER :
               parent is MethodDeclarationSyntax ? MANGLE_METHOD_DELIMITER :
               throw Abort("Unhandled parent type", parent);

            return MangleFullName(parent) + delimiter + name;
         } else {
            return name;
         }
      }

      public string MangleConstructorName(ConstructorDeclarationSyntax ctor) {
         var semanticModel = context.SemanticModelCache.Get(ctor.SyntaxTree);
         var methodSymbol = semanticModel.GetDeclaredSymbol(ctor);
         return MangleMethodName(methodSymbol);
      }

      public string MangleMethodName(MethodDeclarationSyntax method) {
         var semanticModel = context.SemanticModelCache.Get(method.SyntaxTree);
         var methodSymbol = semanticModel.GetDeclaredSymbol(method);
         return MangleMethodName(methodSymbol);
      }

      public string MangleMethodName(IMethodSymbol method) {
         if (method.MethodKind == MethodKind.Constructor) {
            var sb = new StringBuilder();

            sb.Append(method.Name.Replace(".", ""));

            foreach (var p in method.Parameters) {
               sb.Append(MANGLE_CTOR_PARAM_TYPE_DELIMITER);
               sb.Append(MangleFullName(p.Type));
            }

            return sb.ToString();
         }

         return method.Name;
      }

      public string MangleFullName(ISymbol symbol) {
         if (symbol is INamespaceSymbol ns) {
            return MangleNamespaceName(ns);
         }

         var name =
            symbol is ITypeSymbol type ? type.Name :
            symbol is IMethodSymbol method ? MangleMethodName(method) :
            throw new NotImplementedException("Symbol: " + symbol.GetType().Name + " " + symbol);

         if (symbol.ContainingSymbol is ISymbol parent &&
             !(parent is INamespaceSymbol pns && pns.IsGlobalNamespace)) {
            var delimiter =
               parent is INamespaceSymbol ? MANGLE_NAMESPACE_DELIMITER :
               parent is ITypeSymbol ? MANGLE_CLASS_DELIMITER :
               parent is IMethodSymbol ? MANGLE_METHOD_DELIMITER :
               throw new NotImplementedException("Symbol: " + parent.GetType().Name + " " + parent);

            return MangleFullName(parent) + delimiter + name;
         } else {
            return name;
         }
      }

      public string MangleNamespaceName(NameSyntax node) {
         if (node is IdentifierNameSyntax ins) {
            return ins.Identifier.Text;
         } else if (node is QualifiedNameSyntax qns) {
            return MangleNamespaceName(qns.Left) + MANGLE_NAMESPACE_DELIMITER + qns.Right.Identifier.Text;
         } else {
            throw Abort("Unhandled name syntax type", node);
         }
      }

      public string MangleNamespaceName(INamespaceSymbol ns) {
         var parent = ns.ContainingNamespace;
         return parent != null && !parent.IsGlobalNamespace
            ? MangleNamespaceName(parent) + MANGLE_NAMESPACE_DELIMITER + ns.Name
            : ns.Name;
      }
   }

   public enum TranspilationMode { 
      Type,
      Method,
   }

   public class TranspilationContext {
      public KnownTypes KnownTypes;
      public SemanticModelCache SemanticModelCache;
      public Mangler Mangler;

      public AddOnlyOrderedHashSet<MethodTranspilationJob> QueuedMethods { get; } = new AddOnlyOrderedHashSet<MethodTranspilationJob>();
      public AddOnlyOrderedHashSet<TypeTranspilationJob> QueuedTypes { get; } = new AddOnlyOrderedHashSet<TypeTranspilationJob>();
   }

   public struct MethodTranspilationJob : IEquatable<MethodTranspilationJob> {
      public IMethodSymbol Method;

      #region autogenerated equality
      public bool Equals(MethodTranspilationJob other) {
         return Equals(Method, other.Method);
      }

      public override bool Equals(object obj) {
         return obj is MethodTranspilationJob other && Equals(other);
      }

      public override int GetHashCode() {
         return (Method != null ? Method.GetHashCode() : 0);
      }

      public static bool operator ==(MethodTranspilationJob left, MethodTranspilationJob right) {
         return left.Equals(right);
      }

      public static bool operator !=(MethodTranspilationJob left, MethodTranspilationJob right) {
         return !left.Equals(right);
      }
      #endregion
   }

   public struct TypeTranspilationJob : IEquatable<TypeTranspilationJob> {
      public ITypeSymbol TypeSymbol;

      #region autogenerated equality
      public bool Equals(TypeTranspilationJob other) {
         return Equals(TypeSymbol, other.TypeSymbol);
      }

      public override bool Equals(object obj) {
         return obj is TypeTranspilationJob other && Equals(other);
      }

      public override int GetHashCode() {
         return (TypeSymbol != null ? TypeSymbol.GetHashCode() : 0);
      }

      public static bool operator ==(TypeTranspilationJob left, TypeTranspilationJob right) {
         return left.Equals(right);
      }

      public static bool operator !=(TypeTranspilationJob left, TypeTranspilationJob right) {
         return !left.Equals(right);
      }
      #endregion

      public override string ToString()
         => TypeSymbol.ToString();

      public static TypeTranspilationJob Create(ITypeSymbol typeSymbol) {
         return new TypeTranspilationJob {
            TypeSymbol = typeSymbol,
         };
      }
   }

}
