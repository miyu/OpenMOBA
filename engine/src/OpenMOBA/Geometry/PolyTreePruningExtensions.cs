using System;
using ClipperLib;

namespace OpenMOBA.Geometry {
   public static class PolyTreePruningExtensions {
      public const double kMinAreaPrune = 16;

      public static void Prune(this PolyNode polyTree, double actorRadius, double areaPruneThreshold = kMinAreaPrune) {
         var cleaned = Clipper.CleanPolygon(polyTree.Contour, actorRadius / 5 + 2);
         if (cleaned.Count > 0) {
            polyTree.Contour.Clear();
            polyTree.Contour.AddRange(cleaned);
         }

         for (var i = polyTree.Childs.Count - 1; i >= 0; i--) {
            var child = polyTree.Childs[i];
            var childArea = Math.Abs(Clipper.Area(child.Contour));
            if (childArea < areaPruneThreshold) {
               // Console.WriteLine("Prune: " + Clipper.Area(child.Contour) + " " + child.Contour.Count);
               polyTree.Childs.RemoveAt(i);
               continue;
            }

            var kMinimumChildRelativeArea = 1 / 1000.0; // prev 1 / 1000, 1 / 1000000 
            child.Prune(actorRadius, Math.Max(kMinAreaPrune, childArea * kMinimumChildRelativeArea));
         }
      }

   }
}
