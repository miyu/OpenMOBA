using System.Collections.Generic;
using System.Numerics;
using Dargon.Commons;
using Dargon.Dviz;
using Poly2Tri;
using Poly2Tri.Triangulation;
using Poly2Tri.Triangulation.Polygon;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace Dargon.Dviz {
   public static class DebugCanvas2DExtensions {
      private static Vector3 ToV3(Vector2 p) => new Vector3(p, 0);
      private static Vector3 ToV3(TriangulationPoint p) => new Vector3(p.Xf, p.Yf, 0);

      public static void DrawPoint(this IDebugCanvas canvas, Vector2 p, StrokeStyle strokeStyle) {
         canvas.DrawPoint(ToV3(p), strokeStyle);
      }

      public static void DrawPoints(this IDebugCanvas canvas, IReadOnlyList<Vector2> p, StrokeStyle strokeStyle) {
         canvas.DrawPoints(p.Map(ToV3), strokeStyle);
      }

      public static void DrawPolygonContour(this IDebugCanvas canvas, IReadOnlyList<Vector2> poly, StrokeStyle strokeStyle) {
         canvas.BatchDraw(() => {
            for (var i = 0; i < poly.Count - 1; i++) {
               var a = poly[i];
               var b = poly[i + 1];
               canvas.DrawLine(a, b, strokeStyle);
            }

            if (poly[0] != poly[poly.Count - 1]) {
               canvas.DrawLine(poly[poly.Count - 1], poly[0], strokeStyle);
            }
         });
      }

      public static void DrawTriangle(this IDebugCanvas canvas, Vector2 p1, Vector2 p2, Vector2 p3, StrokeStyle strokeStyle) {
         canvas.DrawTriangle(ToV3(p1), ToV3(p2), ToV3(p3), strokeStyle);
      }

      public static void FillTriangle(this IDebugCanvas canvas, Vector2 p1, Vector2 p2, Vector2 p3, FillStyle fillStyle) {
         canvas.FillTriangle(ToV3(p1), ToV3(p2), ToV3(p3), fillStyle);
      }

      public static void DrawLine(this IDebugCanvas canvas, Vector2 p1, Vector2 p2, StrokeStyle strokeStyle) {
         canvas.DrawLine(ToV3(p1), ToV3(p2), strokeStyle);
      }

      public static void DrawLineList(this IDebugCanvas canvas, IReadOnlyList<Vector2> points, StrokeStyle strokeStyle) {
         canvas.DrawLineList(points.Map(ToV3), strokeStyle);
      }

      public static void DrawLineStrip(this IDebugCanvas canvas, IReadOnlyList<Vector2> points, StrokeStyle strokeStyle) {
         canvas.DrawLineStrip(points.Map(ToV3), strokeStyle);
      }

      public static void DrawText(this IDebugCanvas canvas, string text, Vector2 point) {
         canvas.DrawText(text, ToV3(point));
      }
   }
}