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
         private readonly AddOnlyOrderedHashSet<TypeDeclarationSyntax> typesTopologicalReversed = new AddOnlyOrderedHashSet<TypeDeclarationSyntax>();

         private readonly SemanticModelCache semanticModelCache;

         public TopologicalWalker(SemanticModelCache semanticModelCache) {
            this.semanticModelCache = semanticModelCache;
         }

         public IReadOnlyList<TypeDeclarationSyntax> TypesTopologicalReversed => typesTopologicalReversed;

         public void VisitTypeDeclaration(TypeDeclarationSyntax cds) {
            if (!typesTopologicalReversed.TryAdd(cds, out _)) return;

            var semanticModel = semanticModelCache.Get(cds.SyntaxTree);

            var descendentsByType = cds.DescendantNodes().GroupBy(n => n.GetType()).ToDictionary(g => g.Key, g => g.ToArray());
            IEnumerable<T> EnumerateDescendents<T>() => descendentsByType.GetValueOrDefault(typeof(T))?.OfType<T>() ?? new T[0];

            foreach (var mds in EnumerateDescendents<MethodDeclarationSyntax>()) {
               if (mds.DeclaresAttributeOfType<LunaIntrinsicAttribute>()) {
                  continue;
               }

               foreach (var ies in mds.DescendantNodes().OfType<InvocationExpressionSyntax>()) {
                  var methodDeclaration = ies.GetMethodDeclarationSyntax(semanticModel);
                  VisitTypeDeclaration(methodDeclaration.GetContainingTypeDeclaration());
               }
            }

            foreach (var ts in EnumerateDescendents<TypeSyntax>()) {
               var classDeclaration = ts.GetTypeDeclaringSyntax(semanticModel);
               VisitTypeDeclaration(classDeclaration);
            }
         }
      }
   }
}
