using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dargon.Commons;
using Dargon.Luna.Lang;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace Dargon.Luna.Transpiler {
   public static class Program {
      public async static Task Main(string[] args) {
         var workspace = MSBuildWorkspace.Create();

         var project = await workspace.OpenProjectAsync(@"C:\my-repositories\miyu\derp\engine\src\Dargon.Luna\Dargon.Luna.csproj");
         Console.WriteLine(project.Name);
         Console.WriteLine(typeof(Shader).FullName);

         var compilation = await project.GetCompilationAsync();
         var semanticModelCache = new SemanticModelCache(compilation);

         var shadersFound = TypeSearcher.FindShaders(compilation);
         Console.WriteLine(shadersFound.LangShaderSymbol);
         foreach (var shaderImplementation in shadersFound.ShaderImplementations) {
            var tree = shaderImplementation.DeclaringSyntaxReferences.FirstAndOnly().SyntaxTree;

            var shaderInput = FindMembers(shaderImplementation);
            foreach (var method in shaderInput.Methods) {
               var transpiler = new ShaderMethodTranspiler {
                  Compilation = compilation,
                  SemanticModelCache = semanticModelCache,
               };

               var decls = method.DeclaringSyntaxReferences;
               Console.WriteLine(method.Name + " " + method.ToString() + " " + decls.Length);
               if (decls.Length == 0) continue;
               var decl = method.DeclaringSyntaxReferences.FirstAndOnly();
               var mds = (MethodDeclarationSyntax)decl.GetSyntax();
               if (mds.AttributeLists.Any(a => a.ToString().Contains("LunaIntrinsic"))) {
                  continue;
               }

               transpiler.Visit(mds);

               Console.WriteLine(transpiler.Output.ToString());
            }
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
            VertexShader = vs,
            PixelShader = ps,
            Methods = methods,
         };
      }
   }

   public static class TypeSearcher {
      public static InputSymbols FindShaders(Compilation compilation) {
         var langShaderSymbol = compilation.GetTypeByMetadataName(typeof(Shader).FullName);
         var shaderImplementations =
            compilation.GlobalNamespace
                       .Dfs(n => n.GetNamespaceMembers(), ns => ns.Name != "System" && ns.Name != "Microsoft")
                       .SelectMany(ns => ns.GetTypeMembers())
                       .Where(nts => compilation.ClassifyConversion(nts, langShaderSymbol).IsImplicit &&
                                     !nts.IsAbstract)
                       .ToArray();
         return new InputSymbols {
            LangShaderSymbol = langShaderSymbol,
            ShaderImplementations = shaderImplementations,
         };
      }

      public struct InputSymbols {
         public INamedTypeSymbol LangShaderSymbol;
         public INamedTypeSymbol[] ShaderImplementations;
      }
   }

   public class ShaderTranspilationInput {
      public IMethodSymbol VertexShader;
      public IMethodSymbol PixelShader;
      public List<IMethodSymbol> Methods;
   }

   public class TranspilationEmitter {
      private readonly StringBuilder sb = new StringBuilder();

      private bool requireNewlineBefore;
      private bool requireWhitespaceBefore;
      private bool lastIsIdent;
      private bool isStartOfLine;
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
         Console.WriteLine(node);
         DumpSyntaxTree(node);

         Console.WriteLine("Transpilation thus far:");
         Console.WriteLine(e.ToString());
         throw new NotImplementedException();
      }

      private static void DumpSyntaxTree(SyntaxNode node) {
         foreach (var (i, n) in (i: 0, node).Dfs(x => x.node.ChildNodes().Select(n => (x.i + 1, n))).Reverse()) {
             Console.WriteLine(new string('\t', i) + n.GetType().Name);
         }
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

   public static class RoslynExtensions {
      /// <summary>
      /// From roslyn codebase https://stackoverflow.com/questions/30443616/is-there-any-way-to-get-members-of-a-type-and-all-subsequent-base-types
      /// </summary>
      public static IEnumerable<ITypeSymbol> GetBaseTypesAndThis(this ITypeSymbol type) {
         var current = type;
         while (current != null) {
            yield return current;
            current = current.BaseType;
         }
      }

      public static IEnumerable<ISymbol> GetBaseAndThisMembers(this ITypeSymbol type) 
         => type.GetBaseTypesAndThis().SelectMany(t => t.GetMembers());
   }
}
