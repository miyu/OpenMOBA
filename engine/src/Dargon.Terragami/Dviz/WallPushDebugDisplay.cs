using System.Drawing;
using System.Linq;
using System.Numerics;
using Dargon.Dviz;
using Dargon.PlayOn.Geometry;
using Dargon.Terragami.Sectors;

namespace Dargon.Terragami.Dviz {
   public static class VisibilityPolygonDebugCanvasExtensions {
      private static readonly FillStyle DefaultFillStyle = new FillStyle(Color.FromArgb(120, Color.Yellow));
      private static readonly StrokeStyle DefaultAngleBoundaryStrokeStyle = new StrokeStyle(Color.FromArgb(30, Color.Black), 1.0, new [] { 10f, 10f });
      private static readonly StrokeStyle DefaultVisibleWallStrokeStyle = new StrokeStyle(Color.Black, 3.0);

      public static void DrawVisibilityPolygon(this IDebugCanvas debugCanvas, SectorVisibilityPolygon avss, double z = 0.0, FillStyle fillStyle = null, StrokeStyle angleBoundaryStrokeStyle = null, StrokeStyle visibleWallStrokeStyle = null) {
         fillStyle = fillStyle ?? DefaultFillStyle;
         var oxy = avss.Origin;
         foreach (var range in avss.Get().Where(range => range.Id != SectorVisibilityPolygon.RANGE_ID_INFINITELY_FAR && range.Id != SectorVisibilityPolygon.RANGE_ID_INFINITESIMALLY_NEAR)) {
            var rstart = DoubleVector2.FromRadiusAngle(PlayOn.CDoubleMath.c100, range.ThetaStart);
            var rend = DoubleVector2.FromRadiusAngle(PlayOn.CDoubleMath.c100, range.ThetaEnd);
      
            var s = range.Segment;
            var s1 = s.First.ToDoubleVector2();
            var s2 = s.Second.ToDoubleVector2();
            DoubleVector2 visibleStart, visibleEnd;
            if (!GeometryOperations.TryFindLineLineIntersection(oxy, oxy + rstart, s1, s2, out visibleStart) ||
                !GeometryOperations.TryFindLineLineIntersection(oxy, oxy + rend, s1, s2, out visibleEnd)) {
               continue;
            }
            
            debugCanvas.FillTriangle(oxy.ToDotNetVector(), visibleStart.ToDotNetVector(), visibleEnd.ToDotNetVector(), fillStyle);

            debugCanvas.DrawLine(
               new Vector3((float)oxy.X, (float)oxy.Y, (float)z),
               new Vector3((float)visibleStart.X, (float)visibleStart.Y, (float)z),
               angleBoundaryStrokeStyle ?? DefaultAngleBoundaryStrokeStyle);

            debugCanvas.DrawLine(
               new Vector3((float)oxy.X, (float)oxy.Y, (float)z),
               new Vector3((float)visibleEnd.X, (float)visibleEnd.Y, (float)z),
               angleBoundaryStrokeStyle ?? DefaultAngleBoundaryStrokeStyle);

            debugCanvas.DrawLine(
               new Vector3((float)visibleStart.X, (float)visibleStart.Y, (float)z),
               new Vector3((float)visibleEnd.X, (float)visibleEnd.Y, (float)z),
               visibleWallStrokeStyle ?? DefaultVisibleWallStrokeStyle);
         }
      }
   }
}