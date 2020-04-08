using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using Dargon.Commons;
using Dargon.Commons.Collections;
using Dargon.Luna.Lang;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Dargon.Luna.Transpiler.Deoop {
   public class ClassGraphTopologicalEnumerator {
      private readonly AddOnlyOrderedHashSet<TypeDeclarationSyntax> topologicalOrdering = new AddOnlyOrderedHashSet<TypeDeclarationSyntax>();
      private readonly SemanticModelCache semanticModelCache;

      public ClassGraphTopologicalEnumerator(SemanticModelCache semanticModelCache) {
         this.semanticModelCache = semanticModelCache;
      }

      public void VisitTypeDeclaration(TypeDeclarationSyntax c) {
         var tw = new TopologicalWalker(semanticModelCache);
         tw.VisitTypeDeclaration(c);

         var classesReverseTopological = tw.TypesTopologicalReversed;
         for (var i = classesReverseTopological.Count - 1; i >= 0; i--) {
            topologicalOrdering.Add(classesReverseTopological[i]);
         }
      }

      public IReadOnlyList<TypeDeclarationSyntax> TypesTopological => topologicalOrdering;

      private class TopologicalWalker {
         private readonly HashSet<TypeDeclarationSyntax> TypesReversed = new HashSet<TypeDeclarationSyntax>();
         private readonly AddOnlyOrderedHashSet<TypeDeclarationSyntax> typesTopologicalReversed = new AddOnlyOrderedHashSet<TypeDeclarationSyntax>();

         private readonly SemanticModelCache semanticModelCache;

         public TopologicalWalker(SemanticModelCache semanticModelCache) {
            this.semanticModelCache = semanticModelCache;
         }

         public IReadOnlyList<TypeDeclarationSyntax> TypesTopologicalReversed => typesTopologicalReversed;

         public void VisitTypeDeclaration(TypeDeclarationSyntax tds) {
            if (!typesTopologicalReversed.TryAdd(tds, out _)) return;

            var typeIdentifier = tds.Identifier.Text;
            Console.WriteLine("Visit Type Declaration: " + typeIdentifier);

            var semanticModel = semanticModelCache.Get(tds.SyntaxTree);

            // var descendentsByType = tds.DescendantNodes().GroupBy(n => n.GetType()).ToDictionary(g => g.Key, g => g.ToArray());
            // IEnumerable<T> EnumerateDescendents<T>() => descendentsByType.GetValueOrDefault(typeof(T))?.OfType<T>() ?? new T[0];

            // Mostly MemberDeclarationSyntax & VariableDeclaratorSyntax
            var memberDeclarators = tds.DescendantNodes().OfType<MemberDeclarationSyntax>().Cast<CSharpSyntaxNode>().ToList();
            for (var i = 0; i < memberDeclarators.Count; i++) {
               var member = memberDeclarators[i];
               if (member is FieldDeclarationSyntax fds) {
                  memberDeclarators.AddRange(fds.Declaration.Variables);
                  continue;
               }

               if (member is MemberDeclarationSyntax mds && mds.DeclaresAttributeOfType<LunaIntrinsicAttribute>()) {
                  continue;
               }

               var memberIdentifier =
                  member is OperatorDeclarationSyntax ods ? ods.OperatorToken.Text :
                  (((dynamic)member).Identifier).ToString();
               Console.WriteLine("- " + typeIdentifier + "." + memberIdentifier);

               foreach (var ins in member.DescendantNodes().OfType<IdentifierNameSyntax>()) {
                  var insds = ins.GetDeclaringSyntaxOrNull<CSharpSyntaxNode>(semanticModel);

                  if (insds == null) {
                     Console.WriteLine("Couldn't find declaration of: " + ins.Identifier.Text + " (OK if it's an intrinsic)");
                     continue;
                  } else if (insds is TypeDeclarationSyntax insdstds) {
                     VisitTypeDeclaration(insdstds);
                  } else if (insds is VariableDeclaratorSyntax vds) {
                     VisitTypeDeclaration(vds.GetContainingTypeDeclaration());
                  } else if (insds is ParameterSyntax ps) {
                     var parameterTdsOrNull = ps.Type.GetTypeDeclaringSyntaxOrNull(semanticModel);
                     if (parameterTdsOrNull != null) {
                        VisitTypeDeclaration(parameterTdsOrNull);
                     }
                  } else if (insds is MemberDeclarationSyntax memds) {
                     VisitTypeDeclaration(memds.GetContainingTypeDeclaration());
                  } else {
                     throw new NotImplementedException("UNKNOWN " + insds.GetType().Name + " " + insds);
                  }
               }
            }
         }
      }
   }
}
