using System;
using ClipperLib;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace OpenMOBA.Geometry {
   public static class PolyTreePruningExtensions {
      public static readonly long kMinAreaPrune = 16;

      public static void Prune(this PolyNode polyTree, cDouble actorRadius, long areaPruneThreshold = -1) {
         if (areaPruneThreshold < kMinAreaPrune) areaPruneThreshold = kMinAreaPrune;

         var cleaned = Clipper.CleanPolygon(polyTree.Contour, actorRadius / CDoubleMath.c5 + CDoubleMath.c2);
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

//            cDouble kMinimumChildRelativeArea = CDoubleMath.c1 / (cDouble)1000; // prev 1 / 1000, 1 / 1000000 
            child.Prune(actorRadius, Math.Max(kMinAreaPrune, childArea / 1000));
         }
      }

   }
}
