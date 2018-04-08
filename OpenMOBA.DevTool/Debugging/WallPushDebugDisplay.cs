using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenMOBA.Foundation.Terrain;
using OpenMOBA.Foundation.Terrain.CompilationResults.Local;
using OpenMOBA.Geometry;

namespace OpenMOBA.DevTool.Debugging {
   public static class WallPushDebugDisplay {
      private static readonly StrokeStyle InLandStrokeStyle = new StrokeStyle(Color.Gray, 3.0);
      private static readonly StrokeStyle InHoleStrokeStyle = new StrokeStyle(Color.Red, 3.0);
      private static readonly StrokeStyle NearestLandStrokeStyle = new StrokeStyle(Color.Red, 1.0);

      public static void DrawWallPushGrid(this IDebugCanvas canvas, LocalGeometryView lgv, double holeDilationRadius, double xlow = -50, double xhigh = 1100, double xstep = 100, double ylow = -50, double yhigh = 1100, double ystep = 100) {
         for (double x = xlow; x < xhigh; x += xstep) {
            for (double y = ylow; y < yhigh; y += ystep) {
               throw new NotImplementedException();
               //               var query = new DoubleVector2(x, y);
               //               DoubleVector2 nearestLandPoint;
               //               var isInHole = lgv.FindNearestLandPointAndIsInHole(query, out nearestLandPoint);
               //               canvas.DrawPoint(new DoubleVector3(query), isInHole ? InHoleStrokeStyle : InLandStrokeStyle);
               //               if (isInHole) {
               //                  canvas.DrawLine(new DoubleVector3(query), new DoubleVector3(nearestLandPoint), NearestLandStrokeStyle);
               //               }
            }
         }
      }
   }

   public static class LineOfSightDebugDisplay {
      private static readonly FillStyle DefaultFillStyle = new FillStyle(Color.FromArgb(120, Color.Yellow));
      private static readonly StrokeStyle AngleBoundaryStrokeStyle = new StrokeStyle(Color.FromArgb(30, Color.Black), 1.0, new [] { 10f, 10f });
      private static readonly StrokeStyle VisibleWallStrokeStyle = new StrokeStyle(Color.Black, 3.0);

      public static void DrawVisibilityPolygon(this IDebugCanvas debugCanvas, VisibilityPolygon avss, double z = 0.0, FillStyle fillStyle = null) {
         fillStyle = fillStyle ?? DefaultFillStyle;
         var oxy = avss.Origin;
         foreach (var range in avss.Get().Where(range => range.Id != VisibilityPolygon.RANGE_ID_INFINITELY_FAR && range.Id != VisibilityPolygon.RANGE_ID_INFINITESIMALLY_NEAR)) {
            var rstart = DoubleVector2.FromRadiusAngle(100, range.ThetaStart);
            var rend = DoubleVector2.FromRadiusAngle(100, range.ThetaEnd);
      
            var s = range.Segment;
            var s1 = s.First.ToDoubleVector2();
            var s2 = s.Second.ToDoubleVector2();
            DoubleVector2 visibleStart, visibleEnd;
            if (!GeometryOperations.TryFindLineLineIntersection(oxy, oxy + rstart, s1, s2, out visibleStart) ||
                  !GeometryOperations.TryFindLineLineIntersection(oxy, oxy + rend, s1, s2, out visibleEnd)) {
               // wtf?
               continue;
            }

//            debugCanvas.BatchDraw(() => {
            debugCanvas.FillTriangle(oxy, visibleStart, visibleEnd, fillStyle);

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
//            });
         }
      }
   }
}