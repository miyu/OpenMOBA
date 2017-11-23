using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using ClipperLib;
using Poly2Tri.Triangulation.Delaunay;

using cInt = System.Int64;
using Clk = OpenMOBA.Geometry.Clockness;

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

      public static cInt Cross(this IntVector2 a, IntVector2 b) => Cross(a.X, a.Y, b.X, b.Y);
      public static cInt Cross(cInt ax, cInt ay, cInt bx, cInt by) => ax * by - ay * bx;

      public static Clockness Clockness(DoubleVector2 a, DoubleVector2 b, DoubleVector2 c) => Clockness(b - a, b - c);
      public static Clockness Clockness(DoubleVector2 ba, DoubleVector2 bc) => Clockness(ba.X, ba.Y, bc.X, bc.Y);
      public static Clockness Clockness(double ax, double ay, double bx, double by, double cx, double cy) => Clockness(bx - ax, by - ay, bx - cx, by - cy);
      public static Clockness Clockness(double bax, double bay, double bcx, double bcy) => (Clockness)Math.Sign(Cross(bax, bay, bcx, bcy));

      public static double Cross(this DoubleVector2 a, DoubleVector2 b) => Cross(a.X, a.Y, b.X, b.Y);
      public static double Cross(double ax, double ay, double bx, double by) => ax * by - ay * bx;

      public static Vector3 ToDotNetVector(this IntVector3 v) => new Vector3(v.X, v.Y, v.Z);
      public static Vector3 ToDotNetVector(this DoubleVector3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
      public static DoubleVector3 ToOpenMobaVector(this Vector3 v) => new DoubleVector3(v.X, v.Y, v.Z);

      public static Vector2 ToDotNetVector(this IntVector2 v) => new Vector2(v.X, v.Y);
      public static Vector2 ToDotNetVector(this DoubleVector2 v) => new Vector2((float)v.X, (float)v.Y);
      public static DoubleVector2 ToOpenMobaVector(this Vector2 v) => new DoubleVector2(v.X, v.Y);

      public static bool IsCollinearWith(this IntLineSegment2 a, IntLineSegment2 b) {
         var a1a2 = a.First.To(a.Second);
         var b1b2 = b.First.To(b.Second);
         var a1b1 = a.First.To(b.First);
         var isParallel = Cross(a1a2, b1b2) == 0;
         var isA1A2CollinearB1 = Cross(a1a2, a1b1) == 0;
         return isParallel && isA1A2CollinearB1;
      }

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

      public static bool Intersects(this IntLineSegment2 a, IntLineSegment2 b) {
         return TryFindSegmentSegmentIntersection(ref a, ref b, out DoubleVector2 _);
      }

      // NOTE: Assumes segments are valid (two distinct endpoints) NOT line-OVERLAPPING
      // that is, segments should not have more than 1 point of intersection.
      // if segments DO have more than 1 point of intersection, this returns no intersection found.
      public static bool TryFindSegmentSegmentIntersectionT(ref IntLineSegment2 a, ref IntLineSegment2 b, out double tForA) {
         // via http://stackoverflow.com/questions/563198/how-do-you-detect-where-two-line-segments-intersect
         var p = a.First;
         var r = a.First.To(a.Second);
         var q = b.First;
         var s = b.First.To(b.Second);

         var rxs = Cross(r, s);
         if (rxs == 0) {
            goto fail;
         }

         var qmp = q - p;
         var t = Cross(qmp, s) / (double)rxs;
         if (t < 0.0 || t > 1.0) {
            goto fail;
         }

         var u = Cross(qmp, r) / (double)rxs;
         if (u < 0.0 || u > 1.0) {
            goto fail;
         }

         tForA = t;
         return true;

         fail:
         tForA = Double.NaN;
         return false;
      }

      // NOTE: Assumes segments are valid (two distinct endpoints) NOT line-OVERLAPPING
      // that is, segments should not have more than 1 point of intersection.
      // if segments DO have more than 1 point of intersection, this returns no intersection found.
      public static bool TryFindSegmentSegmentIntersection(ref IntLineSegment2 a, ref IntLineSegment2 b, out DoubleVector2 result) {
         if (TryFindSegmentSegmentIntersectionT(ref a, ref b, out double t)) {
            var p = a.First;
            var r = a.First.To(a.Second);

            result = new DoubleVector2(p.X + t * r.X, p.Y + t * r.Y);
            return true;
         }
         result = DoubleVector2.Zero;
         return false;
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

      public static bool SegmentIntersectsConvexPolygonInterior(IntLineSegment2 s, IntVector2[] p) {
         if (p.Length == 1) {
            return false;
         } else if (p.Length == 2) {
            return s.Intersects(new IntLineSegment2(p[0], p[1]));
         } else {
            return SegmentIntersectsNonDegenerateConvexPolygonInterior(s, p);
         }
      }

      // assumes p is ccw ordered
      public static bool ConvexPolygonContainsPoint(IntVector2 x, IntVector2[] p) {
#if DEBUG
         if (Clockness(p[0], p[1], p[2]) == Clk.Clockwise) throw new BadInputException("p not ccw");
         if (p.Length < 3) throw new BadInputException("len(p) < 3");
#endif

         for (var i = 0; i < p.Length; i++) {
            var a = p[i == 0 ? p.Length - 1 : i - 1];
            var b = p[i];
            if (Clockness(a, b, x) == Clk.Clockwise) return false;
         }
         return true;
      }

      // assumes p is ccw ordered, edge is counted as interior (neither case)
      public static bool SegmentIntersectsNonDegenerateConvexPolygonInterior(IntLineSegment2 s, IntVector2[] p) {
#if DEBUG
         if (Clockness(p[0], p[1], p[2]) == Clk.Clockwise) throw new BadInputException("p not ccw");
         if (p.Length < 3) throw new BadInputException("len(p) < 3");
#endif
         var (x, y) = s;
         bool xInterior = true, yInterior = true;
         IntVector2 a = p[p.Length - 1], b;
         int i = 0;
         for (; i < p.Length && (xInterior || yInterior); i++, a = b) {
            b = p[i];
            var abx = Clockness(a, b, x);
            var aby = Clockness(a, b, y);
            if (abx == Clk.Clockwise && aby == Clk.Clockwise) return false;
            xInterior &= abx != Clk.Clockwise;
            yInterior &= aby != Clk.Clockwise;
            if (abx == (Clk)(-(int)aby) || abx == Clk.Neither || aby == Clk.Neither) {
               // The below is equivalent to: 
               // // (a, b) places x, y onto opposite half-planes.
               // // Intersect if (x, y) places a, b onto opposite half-planes.
               // var xya = Clockness(x, y, a);
               // var xyb = Clockness(x, y, b);
               // if (xya != xyb || xya == Clk.Neither || xyb == Clk.Neither) return true;
               if (IntLineSegment2.Intersects(a.X, a.Y, b.X, b.Y, x.X, x.Y, y.X, y.Y)) {
                  return true;
               }
            }
         }
         for (; i < p.Length; i++, a = b) {
            b = p[i];
            if (IntLineSegment2.Intersects(a.X, a.Y, b.X, b.Y, x.X, x.Y, y.X, y.Y)) {
               return true;
            }
         }
         return xInterior && yInterior;
      }


      public static bool SegmentIntersectsConvexPolygonInterior(DoubleLineSegment2 s, DoubleVector2[] p) {
         if (p.Length == 1) {
            return false;
         } else if (p.Length == 2) {
            return s.Intersects(new DoubleLineSegment2(p[0], p[1]));
         } else {
            return SegmentIntersectsNonDegenerateConvexPolygonInterior(s, p);
         }
      }

      // assumes p is ccw ordered
      public static bool ConvexPolygonContainsPoint(DoubleVector2 x, DoubleVector2[] p) {
#if DEBUG
         if (Clockness(p[0], p[1], p[2]) == Clk.Clockwise) throw new BadInputException("p not ccw");
         if (p.Length < 3) throw new BadInputException("len(p) < 3");
#endif

         for (var i = 0; i < p.Length; i++) {
            var a = p[i == 0 ? p.Length - 1 : i - 1];
            var b = p[i];
            if (Clockness(a, b, x) == Clk.Clockwise) return false;
         }
         return true;
      }

      // assumes p is ccw ordered, edge is counted as interior (neither case)
      public static bool SegmentIntersectsNonDegenerateConvexPolygonInterior(DoubleLineSegment2 s, DoubleVector2[] p) {
#if DEBUG
         if (Clockness(p[0], p[1], p[2]) == Clk.Clockwise) throw new BadInputException("p not ccw");
         if (p.Length < 3) throw new BadInputException("len(p) < 3");
#endif
         var (x, y) = s;
         bool xInterior = true, yInterior = true;
         DoubleVector2 a = p[p.Length - 1], b;
         int i = 0;
         for (; i < p.Length && (xInterior || yInterior); i++, a = b) {
            b = p[i];
            var abx = Clockness(a, b, x);
            var aby = Clockness(a, b, y);
            if (abx == Clk.Clockwise && aby == Clk.Clockwise) return false;
            xInterior &= abx != Clk.Clockwise;
            yInterior &= aby != Clk.Clockwise;
            if (abx == (Clk)(-(int)aby) || abx == Clk.Neither || aby == Clk.Neither) {
               // The below is equivalent to: 
               // // (a, b) places x, y onto opposite half-planes.
               // // Intersect if (x, y) places a, b onto opposite half-planes.
               // var xya = Clockness(x, y, a);
               // var xyb = Clockness(x, y, b);
               // if (xya != xyb || xya == Clk.Neither || xyb == Clk.Neither) return true;
               if (DoubleLineSegment2.Intersects(a.X, a.Y, b.X, b.Y, x.X, x.Y, y.X, y.Y)) {
                  return true;
               }
            }
         }
         for (; i < p.Length; i++, a = b) {
            b = p[i];
            if (DoubleLineSegment2.Intersects(a.X, a.Y, b.X, b.Y, x.X, x.Y, y.X, y.Y)) {
               return true;
            }
         }
         return xInterior && yInterior;
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
            var va = triangle.Points[i] - origin;
            var vb = triangle.Points[(i + 1) % 3] - origin;
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

      public static ContourNearestPointResult3 FindNearestPointXYZOnContour(List<IntVector3> contour, DoubleVector3 query) {
         var result = new ContourNearestPointResult3 {
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

      public static ContourNearestPointResult2 FindNearestPointOnContour(List<IntVector2> contour, DoubleVector2 query) {
         var result = new ContourNearestPointResult2 {
            Distance = double.PositiveInfinity,
            Query = query
         };
         var pointCount = contour.First().Equals(contour.Last()) ? contour.Count - 1 : contour.Count;
         for (int i = 0; i < pointCount; i++) {
            var p1 = contour[i].ToDoubleVector2();
            var p2 = contour[(i + 1) % pointCount].ToDoubleVector2();
            var nearestPoint = FindNearestPoint(p1, p2, query);
            var distance = (query - nearestPoint).Norm2D();
            if (distance < result.Distance) {
               result.Distance = distance;
               result.SegmentFirstPointContourIndex = i;
               result.NearestPoint = nearestPoint;
            }
         }
         return result;
      }

      public static DoubleVector2 FindNearestPoint(DoubleVector2 p1, DoubleVector2 p2, DoubleVector2 query) {
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
         return FindNearestPoint(p1, p2, query);
      }

      public static DoubleVector2 FindNearestPoint(DoubleLineSegment2 segment, DoubleVector2 query) {
         return FindNearestPoint(segment.First, segment.Second, query);
      }

      public static bool TryErode(this IntLineSegment2 segment, int erosionRadius, out IntLineSegment2 result) {
         var a = segment.First;
         var b = segment.Second;
         var aToB = a.To(b);
         var aToBMagSquared = aToB.SquaredNorm2();

         var erosionDiameter = 2 * erosionRadius;
         if (aToBMagSquared <= erosionDiameter * erosionDiameter) {
            result = default(IntLineSegment2);
            return false;
         }

         var aToBMag = Math.Sqrt(aToBMagSquared);
         var shrink = aToB.LossyScale(erosionRadius / aToBMag);
         result = new IntLineSegment2(a + shrink, b - shrink);
         return true;
      }

      public static IntLineSegment2 Dilate(this IntLineSegment2 segment, int dilationRadius) {
         var a = segment.First;
         var b = segment.Second;
         var aToB = a.To(b);
         var aToBMagSquared = aToB.SquaredNorm2();

         var aToBMag = Math.Sqrt(aToBMagSquared);
         var shrink = aToB.LossyScale(dilationRadius / aToBMag);
         return new IntLineSegment2(a - shrink, b + shrink);
      }

      /// <summary>
      /// Will continue gracefully even if R3 basis can be formed. Beware!
      /// </summary>
      public static bool ComputePlaneBasis(IReadOnlyList<IntVector3> points, out IntVector3 b1, out IntVector3 b2) {
         for (int i = 1; i < points.Count; i++) {
            b1 = points[0].To(points[i]);
            if (b1 != IntVector3.Zero) {
               for (; i < points.Count; i++) {
                  var next = i + 1 == points.Count ? points[0] : points[i + 1];
                  b2 = points[i].To(next);
                  if (b2 != IntVector3.Zero && b2.Cross(b1) != IntVector3.Zero) {
                     return true;
                  }
               }
            }
         }

         b1 = b2 = default(IntVector3);
         return false;
      }

      private static Comparer<IntVector2> xThenYComparer = Comparer<IntVector2>.Create(
         (a, b) => {
            var r = a.X.CompareTo(b.X);
            return r != 0 ? r : a.Y.CompareTo(b.Y);
         });

      /// <summary>
      /// Implementation of Convex Hull using the Monotone Chain Convex Hull Algorithm.
      /// The algorithm was chosen for implementation due to its simplicity. Chan's
      /// Convex Hull algorithm is a tad more efficient when the output point set
      /// is smaller than the input set.
      /// 
      /// based on http://en.wikibooks.org/wiki/Algorithm_Implementation/Geometry/Convex_hull/Monotone_chain
      /// </summary>
      /// <param name="input"></param>
      /// <returns>
      /// The convex hull of the points in counterclockwise order (starting from the
      /// rightmost point)
      /// </returns>
      public static unsafe IntVector2[] ConvexHull(IntVector2[] input) {
         // sort input by x, then y
         Array.Sort(input, xThenYComparer);

         // Compute one side of hull
         var lower = stackalloc IntVector2[input.Length];
         var lowerLength = 0;
         for (var i = 0; i < input.Length; i++) {
            while (lowerLength >= 2 && Clockness(lower[lowerLength - 2], lower[lowerLength - 1], input[i]) != Geometry.Clockness.CounterClockwise) {
               lowerLength--;
            }
            lower[lowerLength] = input[i];
            lowerLength++;
         }
         var x = sizeof(IntVector2);

         // rm last point, which is first of other hull
         lowerLength--;

         // Compute other side of hull
         var upper = stackalloc IntVector2[input.Length];
         var upperLength = 0;
         for (var i = input.Length - 1; i >= 0; i--) {
            while (upperLength >= 2 && Clockness(upper[upperLength - 2], upper[upperLength - 1], input[i]) != Geometry.Clockness.CounterClockwise) {
               upperLength--;
            }
            upper[upperLength] = input[i];
            upperLength++;
         }

         // rm last point, which is first of other hull
         upperLength--;

         var res = new IntVector2[lowerLength + upperLength];
         fixed (IntVector2* pRes = res) {
            var lowerByteCount = lowerLength * IntVector2.Size;
            var upperByteCount = upperLength * IntVector2.Size;
            Buffer.MemoryCopy(lower, pRes, lowerByteCount + upperByteCount, lowerByteCount);
            Buffer.MemoryCopy(upper, (byte*)pRes + lowerByteCount, upperByteCount, upperByteCount);
         }
         return res;
      }

      public static IntVector2[] ConvexHull3(IntVector2 a, IntVector2 b, IntVector2 c) {
         var abc = Clockness(a, b, c);
         if (abc == Clk.Neither) {
            var (s, t) = FindCollinearBounds(a, b, c);
            return s == t ? new[] { s } : new[] { s, t };
         }
         if (abc == Clk.Clockwise) {
            return new[] { c, b, a };
         }
         return new[] { a, b, c };
      }

      public static (IntVector2, IntVector2) FindCollinearBounds(IntVector2 a, IntVector2 b, IntVector2 c) {
         var ab = a.To(b).SquaredNorm2();
         var ac = a.To(c).SquaredNorm2();
         var bc = b.To(c).SquaredNorm2();
         if (ab > ac) {
            return ab > bc ? (a, b) : (b, c);
         } else {
            return ac > bc ? (a, c) : (b, c);
         }
      }

      // See https://stackoverflow.com/questions/2122305/convex-hull-of-4-points
      public static IntVector2[] ConvexHull4(IntVector2 a, IntVector2 b, IntVector2 c, IntVector2 d) {
         var abc = Clockness(a, b, c);

         if (abc == Clk.Neither) {
            var (s, t) = FindCollinearBounds(a, b, c);
            return ConvexHull3(s, t, d);
         }

         // make abc ccw
         if (abc == Clk.Clockwise) (a, c) = (c, a);

         var abd = Clockness(a, b, d);
         var bcd = Clockness(b, c, d);
         var cad = Clockness(c, a, d);

         if (abd == Clk.Neither) {
            var (s, t) = FindCollinearBounds(a, b, d);
            return ConvexHull3(s, t, c);
         }

         if (bcd == Clk.Neither) {
            var (s, t) = FindCollinearBounds(b, c, d);
            return ConvexHull3(s, t, a);
         }

         if (cad == Clk.Neither) {
            var (s, t) = FindCollinearBounds(c, a, d);
            return ConvexHull3(s, t, b);
         }

         if (abd == Clk.CounterClockwise) {
            if (bcd == Clk.CounterClockwise && cad == Clk.CounterClockwise) return new[] { a, b, c };
            if (bcd == Clk.CounterClockwise && cad == Clk.Clockwise) return new[] { a, b, c, d };
            if (bcd == Clk.Clockwise && cad == Clk.CounterClockwise) return new[] { a, b, d, c };
            if (bcd == Clk.Clockwise && cad == Clk.Clockwise) return new[] { a, b, d };
            throw new InvalidStateException();
         } else {
            if (bcd == Clk.CounterClockwise && cad == Clk.CounterClockwise) return new[] { a, d, b, c };
            if (bcd == Clk.CounterClockwise && cad == Clk.Clockwise) return new[] { d, b, c };
            if (bcd == Clk.Clockwise && cad == Clk.CounterClockwise) return new[] { a, d, c };
            // 4th state impossible
            throw new InvalidStateException();
         }
      }


      public static DoubleVector2[] ConvexHull3(DoubleVector2 a, DoubleVector2 b, DoubleVector2 c) {
         var abc = Clockness(a, b, c);
         if (abc == Clk.Neither) {
            var (s, t) = FindCollinearBounds(a, b, c);
            return s == t ? new[] { s } : new[] { s, t };
         }
         if (abc == Clk.Clockwise) {
            return new[] { c, b, a };
         }
         return new[] { a, b, c };
      }

      public static (DoubleVector2, DoubleVector2) FindCollinearBounds(DoubleVector2 a, DoubleVector2 b, DoubleVector2 c) {
         var ab = a.To(b).SquaredNorm2D();
         var ac = a.To(c).SquaredNorm2D();
         var bc = b.To(c).SquaredNorm2D();
         if (ab > ac) {
            return ab > bc ? (a, b) : (b, c);
         } else {
            return ac > bc ? (a, c) : (b, c);
         }
      }

      // See https://stackoverflow.com/questions/2122305/convex-hull-of-4-points
      public static DoubleVector2[] ConvexHull4(DoubleVector2 a, DoubleVector2 b, DoubleVector2 c, DoubleVector2 d) {
         var abc = Clockness(a, b, c);

         if (abc == Clk.Neither) {
            var (s, t) = FindCollinearBounds(a, b, c);
            return ConvexHull3(s, t, d);
         }

         // make abc ccw
         if (abc == Clk.Clockwise) (a, c) = (c, a);

         var abd = Clockness(a, b, d);
         var bcd = Clockness(b, c, d);
         var cad = Clockness(c, a, d);

         if (abd == Clk.Neither) {
            var (s, t) = FindCollinearBounds(a, b, d);
            return ConvexHull3(s, t, c);
         }

         if (bcd == Clk.Neither) {
            var (s, t) = FindCollinearBounds(b, c, d);
            return ConvexHull3(s, t, a);
         }

         if (cad == Clk.Neither) {
            var (s, t) = FindCollinearBounds(c, a, d);
            return ConvexHull3(s, t, b);
         }

         if (abd == Clk.CounterClockwise) {
            if (bcd == Clk.CounterClockwise && cad == Clk.CounterClockwise) return new[] { a, b, c };
            if (bcd == Clk.CounterClockwise && cad == Clk.Clockwise) return new[] { a, b, c, d };
            if (bcd == Clk.Clockwise && cad == Clk.CounterClockwise) return new[] { a, b, d, c };
            if (bcd == Clk.Clockwise && cad == Clk.Clockwise) return new[] { a, b, d };
            throw new InvalidStateException();
         } else {
            if (bcd == Clk.CounterClockwise && cad == Clk.CounterClockwise) return new[] { a, d, b, c };
            if (bcd == Clk.CounterClockwise && cad == Clk.Clockwise) return new[] { d, b, c };
            if (bcd == Clk.Clockwise && cad == Clk.CounterClockwise) return new[] { a, d, c };
            // 4th state impossible
            throw new InvalidStateException();
         }
      }
   }
}
