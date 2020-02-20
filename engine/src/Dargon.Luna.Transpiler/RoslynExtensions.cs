using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dargon.Commons;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dargon.Luna.Transpiler {
   public static class RoslynExtensions {
      public static MethodDeclarationSyntax GetMethodDeclarationSyntax(this InvocationExpressionSyntax sn, SemanticModel semanticModel)
         => sn.Expression.GetDeclaringSyntax<MethodDeclarationSyntax>(semanticModel);

      public static bool DeclaresAttributeOfType<TAttribute>(this MethodDeclarationSyntax mds) {
         var attributeName = typeof(TAttribute).Name.Replace("Attribute", "");
         return mds.AttributeLists.Any(a => a.ToString().Contains(attributeName));
      }

      public static TypeDeclarationSyntax GetContainingTypeDeclaration(this MethodDeclarationSyntax mds)
         => mds.Ancestors().OfType<TypeDeclarationSyntax>().First();

      public static TypeDeclarationSyntax GetTypeDeclaringSyntax(this TypeSyntax sn, SemanticModel semanticModel)
         => sn.GetDeclaringSyntax<TypeDeclarationSyntax>(semanticModel);

      public static TSyntax GetDeclaringSyntax<TSyntax>(this SyntaxNode sn, SemanticModel semanticModel) {
         var symbolInfo = semanticModel.GetSymbolInfo(sn);
         var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstAndOnly();
         var declarations = symbol.DeclaringSyntaxReferences;
         Assert.Equals(1, declarations.Length);

         var declaration = declarations[0];
         return (TSyntax)(object)declaration.GetSyntax();
      }

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

      /// <summary>
      /// https://stackoverflow.com/questions/21435665/remove-extraneous-semicolons-in-c-sharp-using-roslyn-replace-w-empty-trivia
      /// </summary>
      public static T RemoveSemicolon<T>(this T node,
         SyntaxToken semicolonToken,
         Func<T, SyntaxToken, T> withSemicolonToken) where T : SyntaxNode {
         if (semicolonToken.Kind() != SyntaxKind.None) {
            var leadingTrivia = semicolonToken.LeadingTrivia;
            var trailingTrivia = semicolonToken.TrailingTrivia;

            SyntaxToken newToken = SyntaxFactory.Token(
               leadingTrivia,
               SyntaxKind.None,
               trailingTrivia);

            bool addNewline = semicolonToken.HasTrailingTrivia
                              && trailingTrivia.FirstAndOnly().Kind() == SyntaxKind.EndOfLineTrivia;

            var newNode = withSemicolonToken(node, newToken);

            if (addNewline)
               return newNode.WithTrailingTrivia(SyntaxFactory.Whitespace(Environment.NewLine));
            else
               return newNode;
         }
         return node;
      }
   }
}
