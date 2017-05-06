using System;
using System.Collections.Generic;
using ClipperLib;

namespace OpenMOBA.Geometry {
   public static class PolylineOperations {
      public static PolyTree ExtrudePolygon(IReadOnlyList<IntVector3> points, int offset) {
         if (offset <= 0) {
            throw new ArgumentOutOfRangeException();
         }

         List<IntVector3> polygonPoints = new List<IntVector3>(points);
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
