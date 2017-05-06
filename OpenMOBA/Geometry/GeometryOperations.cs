using System;
using System.Collections.Generic;
using System.Linq;
using ClipperLib;
using Poly2Tri.Triangulation.Delaunay;

using cInt = System.Int64;

namespace OpenMOBA.Geometry {
   public static class GeometryOperations {
      // C# double.Epsilon is denormal = terrible perf; avoid and use this instead.
      // https://www.johndcook.com/blog/2012/01/05/double-epsilon-dbl_epsilon/
      public const double kEpsilon = 10E-16;

      public static bool IsReal(double v) => !(double.IsNaN(v) || double.IsInfinity(v));
      public static bool IsReal(DoubleVector2 v) => IsReal(v.X) && IsReal(v.Y);
      public static bool IsReal(DoubleVector3 v) => IsReal(v.X) && IsReal(v.Y) && IsReal(v.Z);

      public static Clockness Clockness(IntVector2 a, IntVector2 b, IntVector2 c) => Clockness(b - a, b - c);
      public static Clockness Clockness(IntVector2 ba, IntVector2 bc) => Clockness(ba.X, ba.Y, bc.X, bc.Y);
      public static Clockness Clockness(cInt ax, cInt ay, cInt bx, cInt by, cInt cx, cInt cy) => Clockness(bx - ax, by - ay, bx - cx, by - cy);
      public static Clockness Clockness(cInt bax, cInt bay, cInt bcx, cInt bcy) => (Clockness)Math.Sign(Cross(bax, bay, bcx, bcy));

      public static cInt Cross(IntVector2 a, IntVector2 b) => Cross(a.X, a.Y, b.X, b.Y);
      public static cInt Cross(cInt ax, cInt ay, cInt bx, cInt by) => ax * by - ay * bx;

      public static Clockness Clockness(DoubleVector2 a, DoubleVector2 b, DoubleVector2 c) => Clockness(b - a, b - c);
      public static Clockness Clockness(DoubleVector2 ba, DoubleVector2 bc) => Clockness(ba.X, ba.Y, bc.X, bc.Y);
      public static Clockness Clockness(double ax, double ay, double bx, double by, double cx, double cy) => Clockness(bx - ax, by - ay, bx - cx, by - cy);
      public static Clockness Clockness(double bax, double bay, double bcx, double bcy) => (Clockness)Math.Sign(Cross(bax, bay, bcx, bcy));

      public static double Cross(DoubleVector2 a, DoubleVector2 b) => Cross(a.X, a.Y, b.X, b.Y);
      public static double Cross(double ax, double ay, double bx, double by) => ax * by - ay * bx;

      // todo: this needs love
      public static bool TryFindLineLineIntersection(IntLineSegment2 a, IntLineSegment2 b, out DoubleVector2 result) {
         var p1 = a.First;
         var p2 = a.Second;
         var p3 = b.First;
         var p4 = b.Second;

         var v21 = p1 - p2; // (x1 - x2, y1 - y2)
         var v43 = p3 - p4; // (x3 - x4, y3 - y4)

         var denominator = Cross(v21, v43);
         if (denominator == 0) {
            result = DoubleVector2.Zero;
            return false;
         }

         var p1xp2 = Cross(p1, p2); // x1y2 - y1x2
         var p3xp4 = Cross(p3, p4); // x3y4 - y3x4
         var numeratorX = p1xp2 * v43.X - v21.X * p3xp4;
         var numeratorY = p1xp2 * v43.Y - v21.Y * p3xp4;

         result = new DoubleVector2(numeratorX / (double)denominator, numeratorY / (double)denominator);
         return true;
      }

      // todo: this needs love
      public static bool TryFindLineLineIntersection(DoubleVector2 a1, DoubleVector2 a2, DoubleVector2 b1, DoubleVector2 b2, out DoubleVector2 result) {
         var p1 = a1;
         var p2 = a2;
         var p3 = b1;
         var p4 = b2;

         var v21 = p1 - p2; // (x1 - x2, y1 - y2)
         var v43 = p3 - p4; // (x3 - x4, y3 - y4)

         var denominator = Cross(v21, v43);
         if (denominator == 0.0) {
            result = DoubleVector2.Zero;
            return false;
         }

         var p1xp2 = Cross(p1, p2); // x1y2 - y1x2
         var p3xp4 = Cross(p3, p4); // x3y4 - y3x4
         var numeratorX = p1xp2 * v43.X - v21.X * p3xp4;
         var numeratorY = p1xp2 * v43.Y - v21.Y * p3xp4;

         result = new DoubleVector2(numeratorX / (double)denominator, numeratorY / (double)denominator);
         return true;
      }

      public static bool TryIntersect(this Triangulation triangulation, double x, double y, out TriangulationIsland island, out int triangleIndex) {
         foreach (var candidateIsland in triangulation.Islands) {
            if (candidateIsland.TryIntersect(x, y, out triangleIndex)) {
               island = candidateIsland;
               return true;
            }
         }
         island = null;
         triangleIndex = -1;
         return false;
      }

      public static bool TryIntersect(this TriangulationIsland island, double x, double y, out int triangleIndex) {
         if (x < island.IntBounds.Left || y < island.IntBounds.Top ||
             x > island.IntBounds.Right || y > island.IntBounds.Bottom) {
            triangleIndex = -1;
            return false;
         }
         for (var i = 0; i < island.Triangles.Length; i++) {
            if (IsPointInTriangle(x, y, ref island.Triangles[i])) {
               triangleIndex = i;
               return true;
            }
         }
         triangleIndex = -1;
         return false;
      }

      public static bool IsPointInTriangle(double px, double py, ref Triangle3 triangle) {
         // Barycentric coordinates for PIP w/ triangle test http://blackpawn.com/texts/pointinpoly/

         var ax = triangle.Points.A.X;
         var ay = triangle.Points.A.Y;
         var bx = triangle.Points.B.X;
         var by = triangle.Points.B.Y;
         var cx = triangle.Points.C.X;
         var cy = triangle.Points.C.Y;

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

         return (u >= 0) && (v >= 0) && (u + v <= 1);
      }

      public static bool TryIntersectRayWithContainedOriginForVertexIndexOpposingEdge(DoubleVector2 origin, DoubleVector2 direction, ref Triangle3 triangle, out int indexOpposingEdge) {
         // See my explanation on http://math.stackexchange.com/questions/2139740/fast-3d-algorithm-to-find-a-ray-triangle-edge-intersection/2197942#2197942
         // Note: Triangle points (A = p1, B = p2, C = p3) are CCW, origin is p, direction is v.
         // Results are undefined if ray origin is not in triangle (though you can probably math out what it means).
         // If a point is on the edge of the triangle, there will be neither-neither for clockness on the correct edge.
         for (int i = 0; i < 3; i++) {
            var va = triangle.Points[i].XY - origin;
            var vb = triangle.Points[(i + 1) % 3].XY - origin;
            var cvad = Clockness(va, direction);
            var cdvb = Clockness(direction, vb);

            // In-triangle case
            if (cvad == Geometry.Clockness.CounterClockwise &&
                cdvb == Geometry.Clockness.CounterClockwise) {
               indexOpposingEdge = (i + 2) % 3;
               return true;
            }

            // On-edge case
            if (cvad == Geometry.Clockness.Neither &&
                cdvb == Geometry.Clockness.Neither) {
               indexOpposingEdge = (i + 2) % 3;
               return true;
            }
         }
         indexOpposingEdge = -1;
         return false;
         //         throw new ArgumentException("Presumably origin wasn't in triangle (is this case reachable even with malformed input?)");
      }

      public static ContourNearestPointResult FindNearestPointXYZOnContour(List<IntVector3> contour, DoubleVector3 query) {
         var result = new ContourNearestPointResult {
            Distance = double.PositiveInfinity,
            Query = query
         };
         var pointCount = contour.First().Equals(contour.Last()) ? contour.Count - 1 : contour.Count;
         for (int i = 0; i < pointCount; i++) {
            var p1 = contour[i].ToDoubleVector3();
            var p2 = contour[(i + 1) % pointCount].ToDoubleVector3();
            var nearestPoint = FindNearestPointXYZ(p1, p2, query);
            var distance = (query - nearestPoint).Norm2D();
            if (distance < result.Distance) {
               result.Distance = distance;
               result.SegmentFirstPointContourIndex = i;
               result.NearestPoint = nearestPoint;
            }
         }
         return result;
      }

      public static DoubleVector3 FindNearestPointXYZ(DoubleVector3 p1, DoubleVector3 p2, DoubleVector3 query) {
         var p1p2 = p2 - p1;
         var p1Query = query - p1;
         var p1QueryProjP1P2Component = p1Query.ProjectOntoComponentD(p1p2);
         if (p1QueryProjP1P2Component <= 0) {
            return p1;
         } else if (p1QueryProjP1P2Component >= 1) {
            return p2;
         } else {
            return p1 + p1QueryProjP1P2Component * p1p2;
         }
      }

      public static DoubleVector2 FindNearestPoint(IntLineSegment2 segment, DoubleVector2 query) {
         var p1 = segment.First.ToDoubleVector2();
         var p2 = segment.Second.ToDoubleVector2();
         var p1p2 = p2 - p1;
         var p1Query = query - p1;
         var p1QueryProjP1P2Component = p1Query.ProjectOntoComponentD(p1p2);
         if (p1QueryProjP1P2Component <= 0) {
            return p1;
         } else if (p1QueryProjP1P2Component >= 1) {
            return p2;
         } else {
            return p1 + p1QueryProjP1P2Component * p1p2;
         }
      }
   }
}
