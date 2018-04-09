using System.Collections.Generic;
using OpenMOBA.Geometry;
using Poly2Tri;
using Poly2Tri.Triangulation;
using Poly2Tri.Triangulation.Polygon;

namespace OpenMOBA.DevTool.Debugging {
   public static class DebugCanvas2DExtensions {
      private static DoubleVector3 ToDV3(IntVector2 p) => new DoubleVector3(p.ToDoubleVector2());
      private static DoubleVector3 ToDV3(DoubleVector2 p) => new DoubleVector3(p);
      private static DoubleVector3 ToDV3(TriangulationPoint p) => new DoubleVector3(p.X, p.Y, 0);
      private static IntVector3 ToIV3(IntVector2 p) => new IntVector3(p);
      private static IntLineSegment3 ToILS3(IntLineSegment2 p) => new IntLineSegment3(ToIV3(p.First), ToIV3(p.Second));

      public static void DrawPoint(this IDebugCanvas canvas, IntVector2 p, StrokeStyle strokeStyle) {
         canvas.DrawPoint(ToDV3(p), strokeStyle);
      }

      public static void DrawPoint(this IDebugCanvas canvas, DoubleVector2 p, StrokeStyle strokeStyle) {
         canvas.DrawPoint(ToDV3(p), strokeStyle);
      }

      public static void DrawPoints(this IDebugCanvas canvas, IReadOnlyList<IntVector2> p, StrokeStyle strokeStyle) {
         canvas.DrawPoints(p.Map(ToIV3), strokeStyle);
      }

      public static void DrawPoints(this IDebugCanvas canvas, IReadOnlyList<DoubleVector2> p, StrokeStyle strokeStyle) {
         canvas.DrawPoints(p.Map(ToDV3), strokeStyle);
      }

      public static void DrawPolygonContour(this IDebugCanvas canvas, Polygon2 poly, StrokeStyle strokeStyle) {
         for (var i = 0; i < poly.Points.Count - 1; i++) {
            canvas.DrawLineStrip(poly.Points.Map(ToDV3), strokeStyle);
         }
      }

      public static void DrawPolygonContours(this IDebugCanvas canvas, IReadOnlyList<Polygon2> polys, StrokeStyle strokeStyle) {
         foreach (var poly in polys) {
            canvas.DrawPolygonContour(poly, strokeStyle);
         }
      }

      public static void DrawPolygonTriangulation(this IDebugCanvas canvas, Polygon2 poly, StrokeStyle strokeStyle) {
         var cps = new Polygon(poly.Points.Map(p => new PolygonPoint(p.X, p.Y)));
         P2T.Triangulate(cps);

         foreach (var triangle in cps.Triangles) {
            canvas.DrawTriangle(ToDV3(triangle.Points[0]), ToDV3(triangle.Points[1]), ToDV3(triangle.Points[2]), strokeStyle);
         }
      }

      public static void FillPolygonTriangulation(this IDebugCanvas canvas, Polygon2 poly, FillStyle fillStyle) {
         var cps = new Polygon(poly.Points.Map(p => new PolygonPoint(p.X, p.Y)));
         P2T.Triangulate(cps);

         foreach (var triangle in cps.Triangles) {
            canvas.FillTriangle(ToDV3(triangle.Points[0]), ToDV3(triangle.Points[1]), ToDV3(triangle.Points[2]), fillStyle);
         }
      }

      public static void DrawTriangle(this IDebugCanvas canvas, IntVector2 p1, IntVector2 p2, IntVector2 p3, StrokeStyle strokeStyle) {
         canvas.DrawTriangle(ToDV3(p1), ToDV3(p2), ToDV3(p3), strokeStyle);
      }

      public static void DrawTriangle(this IDebugCanvas canvas, DoubleVector2 p1, DoubleVector2 p2, DoubleVector2 p3, StrokeStyle strokeStyle) {
         canvas.DrawTriangle(ToDV3(p1), ToDV3(p2), ToDV3(p3), strokeStyle);
      }

      public static void FillTriangle(this IDebugCanvas canvas, IntVector2 p1, IntVector2 p2, IntVector2 p3, FillStyle fillStyle) {
         canvas.FillTriangle(ToDV3(p1), ToDV3(p2), ToDV3(p3), fillStyle);
      }

      public static void FillTriangle(this IDebugCanvas canvas, DoubleVector2 p1, DoubleVector2 p2, DoubleVector2 p3, FillStyle fillStyle) {
         canvas.FillTriangle(ToDV3(p1), ToDV3(p2), ToDV3(p3), fillStyle);
      }
      
      public static void DrawLine(this IDebugCanvas canvas, IntVector2 p1, IntVector2 p2, StrokeStyle strokeStyle) {
         canvas.DrawLine(ToDV3(p1), ToDV3(p2), strokeStyle);
      }

      public static void DrawLine(this IDebugCanvas canvas, DoubleVector2 p1, DoubleVector2 p2, StrokeStyle strokeStyle) {
         canvas.DrawLine(ToDV3(p1), ToDV3(p2), strokeStyle);
      }

      public static void DrawLine(this IDebugCanvas canvas, IntLineSegment2 segment, StrokeStyle strokeStyle) {
         canvas.DrawLine(ToDV3(segment.First), ToDV3(segment.Second), strokeStyle);
      }

      public static void DrawLine(this IDebugCanvas canvas, DoubleLineSegment2 segment, StrokeStyle strokeStyle) {
         canvas.DrawLine(ToDV3(segment.First), ToDV3(segment.Second), strokeStyle);
      }

      public static void DrawLineList(this IDebugCanvas canvas, IReadOnlyList<IntVector2> points, StrokeStyle strokeStyle) {
         canvas.DrawLineList(points.Map(ToDV3), strokeStyle);
      }

      public static void DrawLineList(this IDebugCanvas canvas, IReadOnlyList<DoubleVector2> points, StrokeStyle strokeStyle) {
         canvas.DrawLineList(points.Map(ToDV3), strokeStyle);
      }

      public static void DrawLineList(this IDebugCanvas canvas, IReadOnlyList<IntLineSegment2> segments, StrokeStyle strokeStyle) {
         canvas.DrawLineList(segments.Map(ToILS3), strokeStyle);
      }


      public static void DrawLineStrip(this IDebugCanvas canvas, IReadOnlyList<IntVector2> points, StrokeStyle strokeStyle) {
         canvas.DrawLineStrip(points.Map(ToIV3), strokeStyle);
      }

      public static void DrawLineStrip(this IDebugCanvas canvas, IReadOnlyList<DoubleVector2> points, StrokeStyle strokeStyle) {
         canvas.DrawLineStrip(points.Map(ToDV3), strokeStyle);
      }

      public static void DrawText(this IDebugCanvas canvas, string text, IntVector2 point) {
         canvas.DrawText(text, ToDV3(point));
      }
   }
}