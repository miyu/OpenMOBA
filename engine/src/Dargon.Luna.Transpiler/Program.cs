using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dargon.Commons;
using Dargon.Commons.Collections;
using Dargon.Luna.Lang;
using Dargon.Luna.Transpiler.Deoop;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using MemberAccessException = System.MemberAccessException;

namespace Dargon.Luna.Transpiler {
   public static class Program {
      public async static Task Main(string[] args) {
         Microsoft.Build.Locator.MSBuildLocator.RegisterDefaults();
         // Environment.SetEnvironmentVariable("MSBuildSDKsPath", @"C:\Program Files\dotnet\sdk\3.1.101", EnvironmentVariableTarget.Process);
         // Environment.SetEnvironmentVariable("COREHOST_TRACE", "1", EnvironmentVariableTarget.Process);

         var workspace = MSBuildWorkspace.Create();
         workspace.WorkspaceFailed += (s, e) => { Console.WriteLine(e.Diagnostic); };

         var project = await workspace.OpenProjectAsync(@"V:\my-repositories\miyu\derp\engine\src\Dargon.Luna\Dargon.Luna.csproj");
         Console.WriteLine("LOADED PROJECT: ");
         Console.WriteLine(project.Name);
         Console.WriteLine(typeof(Shader).FullName);

         var compilation = await project.GetCompilationAsync();
         var semanticModelCache = new SemanticModelCache(compilation);

         var shadersFound = TypeSearcher.FindShaders(compilation);
         Console.WriteLine(shadersFound.LangShaderSymbol);
         Console.WriteLine(shadersFound.LunaIntrinsicsSymbol);

         foreach (var shaderImplementation in shadersFound.ShaderImplementations) {
            Console.WriteLine("SHADER: " + shaderImplementation);
            var shaderCds = (ClassDeclarationSyntax)shaderImplementation.DeclaringSyntaxReferences.FirstAndOnly().GetSyntax();
            var shaderInput = FindMembers(shaderImplementation);
            var cgte = new ClassGraphTopologicalEnumerator(semanticModelCache);
            cgte.VisitTypeDeclaration(shaderCds);
            foreach (var c in cgte.TypesTopological) {
               Console.WriteLine("Transpile " + c);
            }
            continue;

            var smil = new ShaderMethodInvocationLowerer {
               Compilation = compilation,
               SemanticModelCache = semanticModelCache,
            };
            var loweredShader = smil.TransformToCStyleInvokes(shaderInput);
            Console.WriteLine(loweredShader);

            // foreach (var method in shaderInput.Methods) {
            // var transpiler = new ShaderMethodTranspiler {
            //    Compilation = compilation,
            //    SemanticModelCache = semanticModelCache,
            // };
            //
            // var decls = method.DeclaringSyntaxReferences;
            // Console.WriteLine(method.Name + " " + method.ToString() + " " + decls.Length);
            // if (decls.Length == 0) continue;
            // var decl = method.DeclaringSyntaxReferences.FirstAndOnly();
            // var mds = (MethodDeclarationSyntax)decl.GetSyntax();
            // if (mds.AttributeLists.Any(a => a.ToString().Contains("LunaIntrinsic"))) {
            //    continue;
            // }
            //
            // transpiler.Visit(mds);
            //
            // Console.WriteLine(transpiler.Output.ToString());
            // }
         }
      }

      private static ShaderTranspilationInput FindMembers(INamedTypeSymbol shaderImplementation) {
         IMethodSymbol vs = null, ps = null;
         var methods = new List<IMethodSymbol>();
         foreach (var m in shaderImplementation.GetBaseAndThisMembers()) {
            if (m is IMethodSymbol ms) {
               methods.Add(ms);
               if (m.Name == "Vert") vs = ms;
               else if (m.Name == "Frag") ps = ms;
            }
         }
         return new ShaderTranspilationInput {
            ShaderClass = shaderImplementation,
            VertexShader = vs,
            PixelShader = ps,
            Methods = methods,
         };
      }
   }

   public static class Diag {
      public static void DumpExpressionAndSyntaxTree(SyntaxNode node) {
         foreach (var (i, n) in DfsSyntaxNodeWithDepth(node)) {
            Console.WriteLine(new string('\t', i) + n.GetType().Name + " " + n.ToString().Replace('\n', ' ').Replace('\r', ' '));
         }
         Console.WriteLine("Node: " + node);
      }

      public static Exception DumpExpressionAndSyntaxTreeThenReturnThrow(SyntaxNode node) {
         DumpExpressionAndSyntaxTree(node);
         return new InvalidOperationException();
      }

      public static EnumeratorToEnumerableAdapter<(int i, SyntaxNode node), Traverse.TraversalEnumeratorBase<(int i, SyntaxNode node), Stack<(int i, SyntaxNode node)>>> DfsSyntaxNodeWithDepth(SyntaxNode node) =>
         (i: 0, node).Dfs((insert, kvp) => {
            foreach (var child in kvp.node.ChildNodes()) insert((kvp.i + 1, child));
         });
   }

   /// <summary>
   /// Lowers method invocations to C-style invokes, including devirtualization.
   /// </summary>
   public class ShaderMethodInvocationLowerer {
      public static SyntaxTriviaList spaceTrivia = SyntaxFactory.TriviaList(SyntaxFactory.Whitespace(" "));

      public Compilation Compilation { get; set; }
      public SemanticModelCache SemanticModelCache { get; set; }

      private string GetFullMangledName(MethodDeclarationSyntax mds) {
         var s = new Stack<string>();
         s.Push(mds.Identifier.Text);
         foreach (var cds in mds.Ancestors().OfType<ClassDeclarationSyntax>()) {
            s.Push(cds.Identifier.Text);
         }
         return s.Join("_");
      }

      public SyntaxNode TransformToCStyleInvokes(ShaderTranspilationInput input) {

         var shaderMethodInvocationRewriter = new ShaderMethodInvocationRewriter {
            SemanticModelCache = SemanticModelCache,
         };
         var shaderRefFlattener = new ShaderRefFlattener();
         var expressionBodiedMethodsToBlockMethods = new ExpressionBodiedMethodsToBlockMethods();

         var methodsToExplore = new AddOnlyOrderedHashSet<(string, MethodDeclarationSyntax, IMethodSymbol)>();
         var psMds = (MethodDeclarationSyntax)input.PixelShader.DeclaringSyntaxReferences.FirstAndOnly().GetSyntax();
         var vsMds = (MethodDeclarationSyntax)input.VertexShader.DeclaringSyntaxReferences.FirstAndOnly().GetSyntax();

         methodsToExplore.Add((GetFullMangledName(psMds), psMds, input.PixelShader));
         methodsToExplore.Add((GetFullMangledName(vsMds), vsMds, input.VertexShader));

         var rewrittenMethods = new Dictionary<string, (MethodDeclarationSyntax original, MethodDeclarationSyntax rewrite)>();
         for (var i = 0; i < methodsToExplore.Count; i++) {
            var (subjectMangledName, subjectMethodNode, subjectMethodSymbol) = methodsToExplore[i];
            Console.WriteLine("Rewriting method: " + subjectMangledName);

            shaderMethodInvocationRewriter.Reset();
            shaderRefFlattener.Reset();

            if (!subjectMethodSymbol.IsStatic) {
               Diag.DumpExpressionAndSyntaxTree(subjectMethodNode);
               var parameters = new SeparatedSyntaxList<ParameterSyntax>();
               parameters = parameters.Add(SyntaxFactory.Parameter(SyntaxFactory.Identifier("this")).WithType(SyntaxFactory.IdentifierName(subjectMethodSymbol.ContainingType.Name).WithTrailingTrivia(spaceTrivia)));
               parameters = parameters.AddRange(subjectMethodNode.ParameterList.Parameters);
               if (parameters.SeparatorCount > 0) {
                  parameters = parameters.ReplaceSeparator(parameters.GetSeparator(0), parameters.GetSeparator(0).WithTrailingTrivia(spaceTrivia));
               }
               subjectMethodNode = subjectMethodNode.WithParameterList(SyntaxFactory.ParameterList(parameters));
            }

            var rewrite = (MethodDeclarationSyntax)shaderMethodInvocationRewriter.Visit(subjectMethodNode);
            rewrite = rewrite.WithIdentifier(SyntaxFactory.Identifier(subjectMangledName));
            rewrite = (MethodDeclarationSyntax)shaderRefFlattener.Visit(rewrite);
            rewrite = (MethodDeclarationSyntax)expressionBodiedMethodsToBlockMethods.Visit(rewrite);

            rewrittenMethods.Add(subjectMangledName, (subjectMethodNode, rewrite));

            foreach (var (invokeeMangledName, invokeeMethod) in shaderMethodInvocationRewriter.InvokedMethodsByMangledName) {
               var dsrs = invokeeMethod.DeclaringSyntaxReferences;
               if (dsrs.Length == 0) {
                  Diag.DumpExpressionAndSyntaxTree(subjectMethodNode);
                  Console.WriteLine("Mangled Name: " + invokeeMangledName + " => " + invokeeMethod);
                  throw new InvalidOperationException("Invocation has no declaring syntax reference");
               }

               var mds = (MethodDeclarationSyntax)dsrs.FirstAndOnly().GetSyntax();

               // don't explore method (meaning rewrite it) if it's intrinsic. 
               if (mds.AttributeLists.Any(a => a.ToString().Contains("LunaIntrinsic"))) {
                  continue;
               }

               methodsToExplore.Add((invokeeMangledName, mds, invokeeMethod));
            }
         }

         var generatedClass = SyntaxFactory.ClassDeclaration(SyntaxFactory.Identifier(spaceTrivia, "GeneratedClass", spaceTrivia));

         foreach (var (mangledName, (original, rewrite)) in rewrittenMethods) {
            Console.WriteLine("REWROTE " + mangledName);
            Console.WriteLine("FROM: " + original);
            Console.WriteLine("  TO: " + rewrite);
            generatedClass = generatedClass.AddMembers(rewrite);
         }

         Console.WriteLine(generatedClass);
         return null;
      }

      public class ShaderMethodInvocationRewriter : CSharpSyntaxRewriter {
         public SemanticModelCache SemanticModelCache { get; set; }
         public Dictionary<string, IMethodSymbol> InvokedMethodsByMangledName { get; private set; } = new Dictionary<string, IMethodSymbol>();

         public void Reset() {
            InvokedMethodsByMangledName.Clear();
         }

         public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node) {
            var semanticModel = SemanticModelCache.Get(node.SyntaxTree.GetRoot().SyntaxTree);

            var stack = new Stack<string>();
            var currentExpression = node.Expression;
            var invokedMethodSymbolInfo = (IMethodSymbol)semanticModel.GetSymbolInfo(currentExpression).Symbol;
            stack.Push(invokedMethodSymbolInfo.Name);

            var containingType = invokedMethodSymbolInfo.ContainingType;
            while (containingType != null) {
               stack.Push(containingType.Name);
               containingType = containingType.ContainingType;
            }

            var flattenedName = stack.Join("_");
            Console.WriteLine("VISITING NODE " + node + " HAS NAME " + flattenedName);
            InvokedMethodsByMangledName[flattenedName] = (IMethodSymbol)semanticModel.GetSymbolInfo(node.Expression).Symbol;

            // convert instance invoke to static invoke... left.M(params) => TypeName_M(left, params)
            if (!invokedMethodSymbolInfo.IsStatic) {
               var arguments = new SeparatedSyntaxList<ArgumentSyntax>();
               if (node.Expression is MemberAccessExpressionSyntax maes) {
                  arguments = arguments.Add(SyntaxFactory.Argument(maes.Expression));
               } else if (node.Expression is IdentifierNameSyntax ins) {
                  arguments = arguments.Add(SyntaxFactory.Argument(SyntaxFactory.ThisExpression()));
               } else {
                  throw Diag.DumpExpressionAndSyntaxTreeThenReturnThrow(node);
               }

               arguments = arguments.AddRange(node.ArgumentList.Arguments);
               if (arguments.SeparatorCount > 0) {
                  arguments = arguments.ReplaceSeparator(arguments.GetSeparator(0), arguments.GetSeparator(0).WithTrailingTrivia(spaceTrivia));
               }
               return SyntaxFactory.InvocationExpression(
                  SyntaxFactory.IdentifierName(flattenedName),
                  SyntaxFactory.ArgumentList(arguments));
            }

            return SyntaxFactory.InvocationExpression(
               SyntaxFactory.IdentifierName(flattenedName),
               node.ArgumentList);
         }
      }

      public class ShaderRefFlattener : CSharpSyntaxRewriter {
         private readonly Dictionary<string, SyntaxNode> identifierReplacements = new Dictionary<string, SyntaxNode>();

         public void Reset() {
            identifierReplacements.Clear();
         }

         public override SyntaxNode VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax ldss) {
            var vds = ldss.Declaration;
            if (vds.Type is RefTypeSyntax) {
               foreach (var v in vds.Variables) {
                  var res = (RefExpressionSyntax)v.Initializer.Value;
                  identifierReplacements.Add(v.Identifier.Text, Visit(res.Expression));
               }
               return null;
            }
            return base.VisitLocalDeclarationStatement(ldss);
         }

         public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax ins) {
            var name = ins.Identifier.ValueText;
            if (identifierReplacements.TryGetValue(name, out var replacement)) {
               return replacement;
            } else {
               return base.VisitIdentifierName(ins);
            }
         }
      }

      public class ExpressionBodiedMethodsToBlockMethods : CSharpSyntaxRewriter {
         public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node) {
            if (node.ExpressionBody != null) {
               return base.VisitMethodDeclaration(
                  node.WithBody(
                     SyntaxFactory.Block(
                        SyntaxFactory.ReturnStatement(node.ExpressionBody.Expression.WithLeadingTrivia(spaceTrivia))
                                     .WithLeadingTrivia(spaceTrivia)
                                     .WithTrailingTrivia(spaceTrivia)
                        ))
                      .WithExpressionBody(null)
                      .WithSemicolonToken(default)
                      .WithTrailingTrivia(SyntaxFactory.Whitespace(Environment.NewLine))
                  );
            } else {
               return base.VisitMethodDeclaration(node);
            }
         }
      }
   }

   public static class TypeSearcher {
      public static InputSymbols FindShaders(Compilation compilation) {
         var langShaderSymbol = compilation.GetTypeByMetadataName(typeof(Shader).FullName);
         var lunaIntrinsicsSymbol = compilation.GetTypeByMetadataName(typeof(LunaIntrinsics).FullName);
         var shaderImplementations =
            compilation.GlobalNamespace
                       .Dfs((ins, n) => {
                          foreach (var ns in n.GetNamespaceMembers()) {
                             if (ns.Name != "System" && ns.Name != "Microsoft") {
                                ins(ns);
                             }
                          }
                       })
                       .SelectMany(ns => ns.GetTypeMembers())
                       .Where(nts => compilation.ClassifyConversion(nts, langShaderSymbol).IsImplicit &&
                                     !nts.IsAbstract)
                       .ToArray();

         return new InputSymbols {
            LangShaderSymbol = langShaderSymbol,
            LunaIntrinsicsSymbol = lunaIntrinsicsSymbol,
            ShaderImplementations = shaderImplementations,
         };
      }

      public struct InputSymbols {
         public INamedTypeSymbol LangShaderSymbol;
         public INamedTypeSymbol LunaIntrinsicsSymbol;
         public INamedTypeSymbol[] ShaderImplementations;
      }
   }

   public class ShaderTranspilationInput {
      public INamedTypeSymbol ShaderClass;
      public IMethodSymbol VertexShader;
      public IMethodSymbol PixelShader;
      public List<IMethodSymbol> Methods;
   }

   public class TranspilationEmitter {
      private readonly StringBuilder sb = new StringBuilder();

      private bool requireNewlineBefore;
      private bool requireWhitespaceBefore;
      private bool lastIsIdent;
      private int indent = 0;

      public void EmitIdentifier(string s) => EmitInternal(s, true, false, false, false, false);

      private void EmitInternal(string s, bool isIdent, bool nlBefore, bool nlAfter, bool wsBefore, bool wsAfter) {
         if (nlBefore || requireNewlineBefore) {
            sb.AppendLine();
            sb.Append(' ', indent * 3);
         } else if (requireWhitespaceBefore || wsBefore || (isIdent && lastIsIdent)) {
            sb.Append(' ');
         }
         sb.Append(s);

         requireNewlineBefore = nlAfter;
         requireWhitespaceBefore = wsAfter;
         lastIsIdent = isIdent;
      }

      public void EmitOpenParen() => EmitInternal("(", false, false, false, false, false);
      public void EmitClosedParen() => EmitInternal(")", false, false, false, false, false);

      public void EmitOpenCurly() {
         EmitInternal("{", false, false, true, true, false);
         indent++;
      }

      public void EmitClosedCurly() {
         indent--;
         EmitInternal("}", false, true, true, false, false);
      }

      public void EmitSemicolon() => EmitInternal(";", false, false, true, false, false);

      public void EmitOpenBracket() => EmitInternal("[", false, false, false, false, false);
      public void EmitCloseBracket() => EmitInternal("]", false, false, false, false, false);

      public void EmitBinaryOperator(string s) => EmitInternal(s, false, false, false, true, true);
      public void EmitDot() => EmitInternal(".", false, false, false, false, false);
      public void EmitSeparator(string text) => EmitInternal(text, false, false, false, false, true);

      public override string ToString() => sb.ToString();
   }

   public class SemanticModelCache {
      private readonly Dictionary<SyntaxTree, SemanticModel> store = new Dictionary<SyntaxTree, SemanticModel>();
      private readonly Compilation compilation;

      public SemanticModelCache(Compilation compilation) {
         this.compilation = compilation;
      }

      public SemanticModel Get(SyntaxTree t) {
         if (store.TryGetValue(t, out var res)) return res;
         return store[t] = compilation.GetSemanticModel(t, true);
      }
   }

   public class ShaderMethodTranspiler : CSharpSyntaxVisitor {
      private readonly TranspilationEmitter e = new TranspilationEmitter();
      public Compilation Compilation { get; set; }
      public SemanticModelCache SemanticModelCache { get; set; }
      public TranspilationEmitter Output => e;

      private Dictionary<string, SyntaxNode> identifierReplacements = new Dictionary<string, SyntaxNode>();

      public override void DefaultVisit(SyntaxNode node) {
         Console.WriteLine("Unknown Syntax: " + node.GetType());
         Diag.DumpExpressionAndSyntaxTree(node);

         Console.WriteLine("Transpilation thus far:");
         Console.WriteLine(e.ToString());
         throw new NotImplementedException();
      }

      private TypeInfo GetTypeInfo(TypeSyntax ts) {
         try {
            var sm = SemanticModelCache.Get(ts.SyntaxTree);
            return sm.GetTypeInfo(ts);
         } catch {
            Console.WriteLine("FAILED TO GET TYPE INFO " + ts);
            throw;
         }
      }

      public override void VisitMethodDeclaration(MethodDeclarationSyntax mds) {
         var returnTypeSymbol = GetTypeInfo(mds.ReturnType).Type;
         e.EmitIdentifier(returnTypeSymbol.Name);
         e.EmitIdentifier(mds.Identifier.Text);
         Visit(mds.ParameterList);
         Visit(mds.Body);
         Visit(mds.ExpressionBody);
      }

      public override void VisitArrowExpressionClause(ArrowExpressionClauseSyntax node) {
         e.EmitOpenCurly();
         e.EmitIdentifier("return");
         Visit(node.Expression);
         e.EmitClosedCurly();
      }

      public override void VisitParameterList(ParameterListSyntax pls) {
         e.EmitOpenParen();
         var parameters = pls.Parameters;
         for (var i = 0; i  <parameters.Count; i++) {
            var parameter = parameters[i];
            Assert.Equals(0, parameter.AttributeLists.Count);
            Assert.IsNull(parameter.Default);
            Assert.Equals(0, parameter.Modifiers.Count);

            Visit(parameter.Type);
            e.EmitIdentifier(parameter.Identifier.Text);

            if (i < parameters.SeparatorCount) {
               e.EmitSeparator(parameters.GetSeparator(i).Text);
            }
         }
         e.EmitClosedParen();
      }

      public override void VisitBlock(BlockSyntax node) {
         e.EmitOpenCurly();
         foreach (var s in node.Statements) Visit(s);
         e.EmitClosedCurly();
      }

      public override void VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax ldss) {
         var vds = ldss.Declaration;
         if (vds.Type is RefTypeSyntax) {
            foreach (var v in vds.Variables) {
               var res = (RefExpressionSyntax)v.Initializer.Value;
               identifierReplacements.Add(v.Identifier.Text, res.Expression);
            }
         } else {
            Visit(vds);
            e.EmitSemicolon();
         }
      }

      public override void VisitVariableDeclaration(VariableDeclarationSyntax vds) {
         Assert.IsFalse(vds.Type is RefTypeSyntax);

         Visit(vds.Type);
         var variables = vds.Variables;
         for (var i = 0; i < variables.Count; i++) {
            var vdecl = variables[i];
            Visit(vdecl);
            if (i < variables.SeparatorCount) e.EmitSeparator(variables.GetSeparator(i).Text);
         }
      }

      public override void VisitPredefinedType(PredefinedTypeSyntax pts) {
         var typeSymbol = GetTypeInfo(pts).Type;
         if (typeSymbol.Name == nameof(Single)) e.EmitIdentifier("float");
         else if (typeSymbol.Name == nameof(Double)) e.EmitIdentifier("float");
         else e.EmitIdentifier(typeSymbol.Name);
      }

      public override void VisitIdentifierName(IdentifierNameSyntax ins) {
         var name = ins.Identifier.ValueText;
         if (identifierReplacements.TryGetValue(name, out var replacement)) {
            Visit(replacement);
         } else {
            e.EmitIdentifier(name);
         }
      }

      public override void VisitVariableDeclarator(VariableDeclaratorSyntax vds) {
         if (vds.ArgumentList != null) Visit(vds.ArgumentList);
         e.EmitIdentifier(vds.Identifier.Text);
         if (vds.Initializer != null) VisitEqualsValueClause(vds.Initializer);
      }

      public override void VisitEqualsValueClause(EqualsValueClauseSyntax evcs) {
         e.EmitBinaryOperator("=");
         Visit(evcs.Value);
      }

      public override void VisitDefaultExpression(DefaultExpressionSyntax node) {
         e.EmitIdentifier("0"); // take advantage of compiler cast.
      }

      public override void VisitExpressionStatement(ExpressionStatementSyntax ess) {
         Visit(ess.Expression);
         e.EmitSemicolon();
      }

      public override void VisitAssignmentExpression(AssignmentExpressionSyntax aes) {
         Visit(aes.Left);
         e.EmitBinaryOperator(aes.OperatorToken.Text);
         Visit(aes.Right);
      }

      public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax maes) {
         Visit(maes.Expression);
         e.EmitDot();
         Visit(maes.Name);
      }

      public override void VisitInvocationExpression(InvocationExpressionSyntax ies) {
         Visit(ies.Expression);
         Visit(ies.ArgumentList);
      }

      public override void VisitArgumentList(ArgumentListSyntax als) {
         e.EmitOpenParen();
         var args = als.Arguments;
         for (var i = 0; i < args.Count; i++) {
            var arg = args[i];
            Visit(arg);
            if (i < args.SeparatorCount) e.EmitSeparator(args.GetSeparator(i).Text);
         }
         e.EmitClosedParen();
      }

      public override void VisitReturnStatement(ReturnStatementSyntax node) {
         e.EmitIdentifier(node.ReturnKeyword.Text);
         Visit(node.Expression);
         e.EmitSemicolon();
      }

      public override void VisitBinaryExpression(BinaryExpressionSyntax bes) {
         Visit(bes.Left);
         e.EmitBinaryOperator(bes.OperatorToken.Text);
         Visit(bes.Right);
      }

      public override void VisitParenthesizedExpression(ParenthesizedExpressionSyntax pes) {
         e.EmitOpenParen();
         Visit(pes.Expression);
         e.EmitClosedParen();
      }

      public override void VisitElementAccessExpression(ElementAccessExpressionSyntax eaes) {
         Visit(eaes.Expression);
         e.EmitOpenBracket();
         var args = eaes.ArgumentList.Arguments;
         for (var i = 0 ; i < args.Count; i++) {
            Visit(args[i]);
            if (i < args.SeparatorCount) e.EmitSeparator(args.GetSeparator(i).Text);
         }
         e.EmitCloseBracket();
      }

      public override void VisitArgument(ArgumentSyntax arg) {
         Assert.IsNull(arg.NameColon);
         Visit(arg.Expression);
      }

      public override void VisitLiteralExpression(LiteralExpressionSyntax les) {
         e.EmitIdentifier(les.Token.Text);
      }
   }
}
