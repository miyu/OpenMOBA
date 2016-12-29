using System;
using System.Collections.Generic;
using System.Linq;
using ClipperLib;
using Poly2Tri.Triangulation.Delaunay;

namespace OpenMOBA.Geometry {
   public static class GeometryOperations {
      public static Clockness Clockness(IntVector2 a, IntVector2 b, IntVector2 c) => Clockness(b - a, b - c);
      public static Clockness Clockness(IntVector2 ba, IntVector2 bc) => Clockness(ba.X, ba.Y, bc.X, bc.Y);
      public static Clockness Clockness(int ax, int ay, int bx, int by, int cx, int cy) => Clockness(bx - ax, by - ay, bx - cx, by - cy);
      public static Clockness Clockness(int bax, int bay, int bcx, int bcy) => (Clockness)Math.Sign(Cross(bax, bay, bcx, bcy));

      public static int Cross(IntVector2 a, IntVector2 b) => Cross(a.X, a.Y, b.X, b.Y);
      public static int Cross(int ax, int ay, int bx, int by) => ax * by - ay * bx;

      public static double Cross(double bax, double bay, double bcx, double bcy) => bax * bcy - bay * bcx;
      
      public static IntVector2 FindLineLineIntersection(IntLineSegment2 a, IntLineSegment2 b) {
         var p1 = a.First;
         var p2 = a.Second;
         var p3 = b.First;
         var p4 = b.Second;

         var p1xp2 = Cross(p1, p2); // x1y2 - y1x2
         var p3xp4 = Cross(p3, p4); // x3y4 - y3x4
         var v21 = p1 - p2; // (x1 - x2, y1 - y2)
         var v43 = p3 - p4; // (x3 - x4, y3 - y4)

         var denominator = Cross(v21, v43);
         var numeratorX = p1xp2 * v43.X - v21.X * p3xp4;
         var numeratorY = p1xp2 * v43.Y - v21.Y * p3xp4;

         return new IntVector2(numeratorX / denominator, numeratorY / denominator);
      }

      public static bool TryIntersect(double x, double y, IReadOnlyList<ConnectedMesh> meshes, out ConnectedMesh mesh, out DelaunayTriangle triangle) {
         foreach (var m in meshes) {
            if (m.TryIntersect(x, y, out triangle)) {
               mesh = m;
               return true;
            }
         }
         mesh = null;
         triangle = null;
         return false;
      }

      public static bool TryIntersect(this ConnectedMesh mesh, double x, double y, out DelaunayTriangle triangle) {
         if (x < mesh.BoundingBox.MinX || y < mesh.BoundingBox.MinY ||
             x > mesh.BoundingBox.MaxX || y > mesh.BoundingBox.MaxY) {
            triangle = null;
            return false;
         }
         triangle = mesh.Triangles.FirstOrDefault(tri => IsPointInTriangle(x, y, tri));
         return triangle != null;
      }

      public static bool IsPointInTriangle(double px, double py, DelaunayTriangle triangle) {
         // Barycentric coordinates for PIP w/ triangle test http://blackpawn.com/texts/pointinpoly/

         var ax = triangle.Points.Item0.X;
         var ay = triangle.Points.Item0.Y;
         var bx = triangle.Points.Item1.X;
         var by = triangle.Points.Item1.Y;
         var cx = triangle.Points.Item2.X;
         var cy = triangle.Points.Item2.Y;

         var v0x = cx - ax;
         var v0y = cy - ay;
         var v1x = bx - ax;
         var v1y = by - ay;
         var v2x = px - ax;
         var v2y = py - ay;

         var dot00 = v0x * v0x + v0y * v0y;
         var dot01 = v0x * v1x + v0y * v1y;
         var dot02 = v0x * v2x + v0y * v2y;
         var dot11 = v1x * v1x + v1y * v1y;
         var dot12 = v1x * v2x + v1y * v2y;

         var invDenom = 1.0 / (dot00 * dot11 - dot01 * dot01);
         var u = (dot11 * dot02 - dot01 * dot12) * invDenom;
         var v = (dot00 * dot12 - dot01 * dot02) * invDenom;

         return (u >= 0) && (v >= 0) && (u + v < 1);
      }

      public static ContourNearestPointResult FindNearestPoint(List<IntPoint> contour, IntVector2 query) {
         var result = new ContourNearestPointResult {
            Distance = float.PositiveInfinity,
            Query = query
         };
         var pointCount = contour.First().Equals(contour.Last()) ? contour.Count - 1 : contour.Count;
         for (int i = 0; i < pointCount; i++) {
            var p1 = contour[i];
            var p2 = contour[(i + 1) % pointCount];
            var p1p2 = new IntVector2((int)(p2.X - p1.X), (int)(p2.Y - p1.Y));
            var p1Query = new IntVector2((int)(query.X - p1.X), (int)(query.Y - p1.Y));
            var p1QueryProjP1P2Component = p1Query.ProjectOntoComponentD(p1p2);
            IntVector2 nearestPoint;
            if (p1QueryProjP1P2Component <= 0) {
               nearestPoint = p1.ToOpenMobaPoint();
            } else if (p1QueryProjP1P2Component >= 1) {
               nearestPoint = p2.ToOpenMobaPoint();
            } else {
               var p1p2Perp = new IntVector2(p1p2.Y, -p1p2.X);
               var p1QueryProjOntoP1P2Perp = p1Query.LossyProjectOnto(p1p2Perp);
               nearestPoint = query - p1QueryProjOntoP1P2Perp;
            }
            var distance = (query - nearestPoint).Norm2F();
            if (distance < result.Distance) {
               result.Distance = distance;
               result.SegmentFirstPointContourIndex = i;
               result.NearestPoint = nearestPoint;
            }
         }
         return result;
      }
   }
}
