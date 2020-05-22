using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dargon.Commons;
using Dargon.Dviz;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Geometry;
using Dargon.Terragami.Dviz;

namespace Dargon.Terragami.Tests {
   public class PointInConvexPolygonTests {
      public static void Exec() {
         var sampleRect = (x: 0, y: 0, w: 400, h: 300);
         var dmch = SceneVisualizerUtils.CreateAndShowFittingCanvasHost(
            AxisAlignedBoundingBox2.BoundingPolygon(
               Polygon2.CreateRect(sampleRect.x, sampleRect.y, sampleRect.w, sampleRect.h)));

         for (var i = 37; i < 100; i++) {
            var r = new Random(i);
            var numPoints = r.Next(3, 20);
            var ps = CreatePoints(numPoints, r, sampleRect);
            var chull = GeometryOperations.ConvexHull(ps).ToList();
            var poly = new Polygon2(chull);

            var canvas = dmch.CreateAndAddCanvas();
            canvas.DrawPoints(ps, StrokeStyle.GrayThick5Solid);
            poly.Visualize(canvas, labelIndices: true, labelCoordinates: true);

            var numQueries = 1000;
            var qs = CreatePoints(numQueries, r, sampleRect);
            var containment = qs.Map(p => Pip(poly, p.ToDoubleVector2()));

            var notContained = qs.LogicalIndex(containment, negateConditions: true);
            canvas.DrawPoints(notContained, StrokeStyle.RedThick5Solid);

            var contained = qs.LogicalIndex(containment);
            canvas.DrawPoints(contained, StrokeStyle.LimeThick5Solid);
         }
      }

      private static IntVector2[] CreatePoints(int numPoints, Random r, (int x, int y, int w, int h) sampleRect) {
         return Arrays.Create(numPoints, () => new DoubleVector2(
            r.NextDouble() * sampleRect.w + sampleRect.x,
            r.NextDouble() * sampleRect.h + sampleRect.y).LossyToIntVector2());
      }

      public static bool Pip(Polygon2 poly, DoubleVector2 query) {
         var ps = poly.Points;

         var lastIndex = ps[0] == ps[^1] ? ps.Count - 2 : ps.Count - 1;

         var v0 = ps[0].ToDoubleVector2();
         var lo = 1; // inclusive
         var hi = lastIndex; // inclusive

         var vlo = ps[lo].ToDoubleVector2();
         var vhi = ps[hi].ToDoubleVector2();

         // ensure q is within the angle formed by vhi-v0-vlo
         if (GeometryOperations.Clockness(vhi, v0, query) == Clockness.CounterClockWise ||
             GeometryOperations.Clockness(v0, vlo, query) == Clockness.CounterClockWise) {
            return false;
         }

         // binsearch for angular range containing query point
         while (lo + 1 != hi) {
            Assert.IsLessThan(lo, hi);

            var mid = lo + (hi - lo) / 2;
            var vmid = ps[mid].ToDoubleVector2();

            // don't have to care about robustness to collinearity,
            // will handle when we find final lo-hi
            var clk = GeometryOperations.Clockness(v0, vmid, query);
            if (clk == Clockness.ClockWise) {
               lo = mid;
            } else if (clk == Clockness.CounterClockWise) {
               hi = mid;
            } else {
               return DoubleVector2.SquaredDistanceNorm2(v0, query) <= DoubleVector2.SquaredDistanceNorm2(v0, vmid);
            }
         }

         // ensure query point is within triangle formed by lo, hi, and v0.
         Assert.Equals(lo + 1, hi);
         vlo = ps[lo].ToDoubleVector2();
         vhi = ps[hi].ToDoubleVector2();
         var finalClockness = GeometryOperations.Clockness(vlo, vhi, query);
         return finalClockness != Clockness.CounterClockWise;
      }

      public static bool Pip(DoubleVector2[] ps, DoubleVector2 query) {
         var lastIndex = ps[0] == ps[^1] ? ps.Length - 2 : ps.Length - 1;

         var v0 = ps[0];
         var lo = 1; // inclusive
         var hi = lastIndex; // inclusive

         var vlo = ps[lo];
         var vhi = ps[hi];

         // ensure q is within the angle formed by vhi-v0-vlo
         if (GeometryOperations.Clockness(vhi, v0, query) == Clockness.CounterClockWise ||
             GeometryOperations.Clockness(v0, vlo, query) == Clockness.CounterClockWise) {
            return false;
         }

         // binsearch for angular range containing query point
         while (lo + 1 != hi) {
            Assert.IsLessThan(lo, hi);

            var mid = lo + (hi - lo) / 2;
            var vmid = ps[mid];

            // don't have to care about robustness to collinearity,
            // will handle when we find final lo-hi
            var clk = GeometryOperations.Clockness(v0, vmid, query);
            if (clk == Clockness.ClockWise) {
               lo = mid;
            } else if (clk == Clockness.CounterClockWise) {
               hi = mid;
            } else {
               return DoubleVector2.SquaredDistanceNorm2(v0, query) <= DoubleVector2.SquaredDistanceNorm2(v0, vmid);
            }
         }

         // ensure query point is within triangle formed by lo, hi, and v0.
         Assert.Equals(lo + 1, hi);
         vlo = ps[lo];
         vhi = ps[hi];
         var finalClockness = GeometryOperations.Clockness(vlo, vhi, query);
         return finalClockness != Clockness.CounterClockWise;
      }
   }
}
