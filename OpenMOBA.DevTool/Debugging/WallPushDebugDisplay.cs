using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Geometry;

namespace OpenMOBA.DevTool.Debugging {
   public static class WallPushDebugDisplay {
      private static readonly StrokeStyle InLandStrokeStyle = new StrokeStyle(Color.Gray, 3.0);
      private static readonly StrokeStyle InHoleStrokeStyle = new StrokeStyle(Color.Red, 3.0);
      private static readonly StrokeStyle NearestLandStrokeStyle = new StrokeStyle(Color.Red, 1.0);

      public static void DrawWallPushGrid(this IDebugCanvas canvas, TerrainSnapshot terrainSnapshot, double holeDilationRadius, double xlow = -50, double xhigh = 1100, double xstep = 100, double ylow = -50, double yhigh = 1100, double ystep = 100) {
         for (double x = xlow; x < xhigh; x += xstep) {
            for (double y = ylow; y < yhigh; y += ystep) {
               var query = new DoubleVector3(x, y, 0);
               DoubleVector3 nearestLandPoint;
               var isInHole = terrainSnapshot.FindNearestLandPointAndIsInHole(holeDilationRadius, query, out nearestLandPoint);
               canvas.DrawPoint(query, isInHole ? InHoleStrokeStyle : InLandStrokeStyle);
               if (isInHole) {
                  canvas.DrawLine(query, nearestLandPoint, NearestLandStrokeStyle);
               }
            }
         }
      }
   }

   public static class LineOfSightDebugDisplay {
      private static readonly FillStyle LineOfSightFillStyle = new FillStyle(Color.FromArgb(120, Color.Yellow));
      private static readonly StrokeStyle AngleBoundaryStrokeStyle = new StrokeStyle(Color.FromArgb(30, Color.Black), 1.0, new [] { 10f, 10f });
      private static readonly StrokeStyle VisibleWallStrokeStyle = new StrokeStyle(Color.Black, 5.0);

      public static void DrawLineOfSight(this IDebugCanvas debugCanvas, AngularVisibleSegmentStore avss, double z = 0.0) {
         var oxy = avss.Origin;
         foreach (var range in avss.Get().Where(range => range.Id != AngularVisibleSegmentStore.RANGE_ID_NULL)) {
            var rstart = DoubleVector2.FromRadiusAngle(100, range.ThetaStart);
            var rend = DoubleVector2.FromRadiusAngle(100, range.ThetaEnd);
      
            var s = range.Segment;
            var s1 = s.First.XY.ToDoubleVector2();
            var s2 = s.Second.XY.ToDoubleVector2();
            DoubleVector2 visibleStart, visibleEnd;
            if (!GeometryOperations.TryFindLineLineIntersection(oxy, oxy + rstart, s1, s2, out visibleStart) ||
                  !GeometryOperations.TryFindLineLineIntersection(oxy, oxy + rend, s1, s2, out visibleEnd)) {
               // wtf?
               continue;
            }

            debugCanvas.BatchDraw(() => {
               debugCanvas.FillPolygon(
                  new List<DoubleVector3> {
                     new DoubleVector3(oxy.X, oxy.Y, z),
                     new DoubleVector3(visibleStart.X, visibleStart.Y, z),
                     new DoubleVector3(visibleEnd.X, visibleEnd.Y, z)
                  }, LineOfSightFillStyle);

               debugCanvas.DrawLine(
                  new DoubleVector3(oxy.X, oxy.Y, z),
                  new DoubleVector3(visibleStart.X, visibleStart.Y, z),
                  AngleBoundaryStrokeStyle);

               debugCanvas.DrawLine(
                  new DoubleVector3(oxy.X, oxy.Y, z),
                  new DoubleVector3(visibleEnd.X, visibleEnd.Y, z),
                  AngleBoundaryStrokeStyle);

               debugCanvas.DrawLine(
                  new DoubleVector3(visibleStart.X, visibleStart.Y, z),
                  new DoubleVector3(visibleEnd.X, visibleEnd.Y, z),
                  VisibleWallStrokeStyle);
            });
         }
      }
   }
}