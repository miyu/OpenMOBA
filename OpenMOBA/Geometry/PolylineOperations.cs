using System;
using System.Collections.Generic;
using ClipperLib;

namespace OpenMOBA.Geometry {
   public static class PolylineOperations {
      public static PolyTree ExtrudePolygon(IReadOnlyList<IntVector3> points, int offset) {
         if (points.Count <= 1) {
            throw new NotImplementedException("Not implemented: extrude 0 or 1 points");
         }

         if (offset <= 0) {
            throw new ArgumentOutOfRangeException();
         }

         List<IntVector3> polygonPoints = new List<IntVector3>(points);

         // hack: fidget the last point slightly to enforce having area
         var last = points[points.Count - 1];
         var secondToLast = points[points.Count - 2];
         var secondToLastToLast = secondToLast.To(last);
         if (secondToLastToLast.XY == IntVector2.Zero) {
            polygonPoints.Add(last + new IntVector3(1, 0, 0));
         } else if (secondToLastToLast.X == 0) {
            polygonPoints.Add(last + new IntVector3(1, 0, 0));
         } else {
            polygonPoints.Add(last + new IntVector3(0, 1, 0));
         }

         for (int i = points.Count - 2; i >= 0; i--) {
            polygonPoints.Add(points[i]);
         }

         var inputHairlinePolygon = new Polygon(polygonPoints, false);
         var outputPolygons = PolygonOperations.Offset()
                                               .Include(inputHairlinePolygon)
                                               .Dilate(offset)
                                               .Execute();
         return outputPolygons;
      }
   }
}
