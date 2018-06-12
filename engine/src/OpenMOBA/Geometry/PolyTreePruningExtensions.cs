using System;
using ClipperLib;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace OpenMOBA.Geometry {
   public static class PolyTreePruningExtensions {
      public static readonly cDouble kMinAreaPrune = (cDouble)16;

      public static void Prune(this PolyNode polyTree, cDouble actorRadius, cDouble areaPruneThreshold = default) {
         if (areaPruneThreshold < kMinAreaPrune) areaPruneThreshold = kMinAreaPrune;

         var cleaned = Clipper.CleanPolygon(polyTree.Contour, actorRadius / CDoubleMath.c5 + CDoubleMath.c2);
         if (cleaned.Count > 0) {
            polyTree.Contour.Clear();
            polyTree.Contour.AddRange(cleaned);
         }

         for (var i = polyTree.Childs.Count - 1; i >= 0; i--) {
            var child = polyTree.Childs[i];
            var childArea = CDoubleMath.Abs(Clipper.Area(child.Contour));
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
