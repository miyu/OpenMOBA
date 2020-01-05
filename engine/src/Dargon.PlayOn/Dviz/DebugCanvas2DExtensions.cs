using System.Collections.Generic;
using System.Numerics;
using Dargon.Commons;
using Dargon.Dviz;
using Dargon.PlayOn.Geometry;
using Poly2Tri;
using Poly2Tri.Triangulation;
using Poly2Tri.Triangulation.Polygon;

#if use_fixed
using cDouble = FixMath.NET.Fix64;
#else
using cDouble = System.Double;
#endif

namespace Dargon.PlayOn.Dviz {
   public static class DebugCanvas2DExtensions {
      private static Vector3 ToV3(IntVector2 p) => new Vector3(p.X, p.Y, 0);
      private static Vector3 ToV3(DoubleVector2 p) => new Vector3((float)p.X, (float)p.Y, 0);
      private static Vector3 ToV3(TriangulationPoint p) => new Vector3((float)p.X, (float)p.Y, 0);
      private static IntVector3 ToIV3(IntVector2 p) => new IntVector3(p);
      private static IntLineSegment3 ToILS3(IntLineSegment2 p) => new IntLineSegment3(ToIV3(p.First), ToIV3(p.Second));

      public static void DrawPoint(this IDebugCanvas canvas, IntVector2 p, StrokeStyle strokeStyle) {
         canvas.DrawPoint(ToV3(p), strokeStyle);
      }

      public static void DrawPoint(this IDebugCanvas canvas, DoubleVector2 p, StrokeStyle strokeStyle) {
         canvas.DrawPoint(ToV3(p), strokeStyle);
      }

      public static void DrawPoints(this IDebugCanvas canvas, IReadOnlyList<IntVector2> p, StrokeStyle strokeStyle) {
         canvas.DrawPoints(p.Map(ToIV3), strokeStyle);
      }

      public static void DrawPoints(this IDebugCanvas canvas, IReadOnlyList<DoubleVector2> p, StrokeStyle strokeStyle) {
         canvas.DrawPoints(p.Map(ToV3), strokeStyle);
      }

      public static void DrawPolygonContour(this IDebugCanvas canvas, IReadOnlyList<IntVector2> poly, StrokeStyle strokeStyle) {
         canvas.DrawLineStrip(poly.Map(ToV3), strokeStyle);
      }

      public static void DrawPolygonContour(this IDebugCanvas canvas, IReadOnlyList<DoubleVector2> poly, StrokeStyle strokeStyle) {
         canvas.DrawLineStrip(poly.Map(ToV3), strokeStyle);
      }

      public static void DrawPolygonContour(this IDebugCanvas canvas, Polygon2 poly, StrokeStyle strokeStyle) {
         canvas.DrawLineStrip(poly.Points.Map(ToV3), strokeStyle);
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
            canvas.DrawTriangle(ToV3(triangle.Points[0]), ToV3(triangle.Points[1]), ToV3(triangle.Points[2]), strokeStyle);
         }
      }

      public static void FillPolygonTriangulation(this IDebugCanvas canvas, Polygon2 poly, FillStyle fillStyle) {
         var cps = new Polygon(poly.Points.Map(p => new PolygonPoint(p.X, p.Y)));
         P2T.Triangulate(cps);

         foreach (var triangle in cps.Triangles) {
            canvas.FillTriangle(ToV3(triangle.Points[0]), ToV3(triangle.Points[1]), ToV3(triangle.Points[2]), fillStyle);
         }
      }

      public static void DrawTriangle(this IDebugCanvas canvas, IntVector2 p1, IntVector2 p2, IntVector2 p3, StrokeStyle strokeStyle) {
         canvas.DrawTriangle(ToV3(p1), ToV3(p2), ToV3(p3), strokeStyle);
      }

      public static void DrawTriangle(this IDebugCanvas canvas, DoubleVector2 p1, DoubleVector2 p2, DoubleVector2 p3, StrokeStyle strokeStyle) {
         canvas.DrawTriangle(ToV3(p1), ToV3(p2), ToV3(p3), strokeStyle);
      }

      public static void FillTriangle(this IDebugCanvas canvas, IntVector2 p1, IntVector2 p2, IntVector2 p3, FillStyle fillStyle) {
         canvas.FillTriangle(ToV3(p1), ToV3(p2), ToV3(p3), fillStyle);
      }

      public static void FillTriangle(this IDebugCanvas canvas, DoubleVector2 p1, DoubleVector2 p2, DoubleVector2 p3, FillStyle fillStyle) {
         canvas.FillTriangle(ToV3(p1), ToV3(p2), ToV3(p3), fillStyle);
      }

      public static void DrawLine(this IDebugCanvas canvas, IntVector2 p1, IntVector2 p2, StrokeStyle strokeStyle) {
         canvas.DrawLine(ToV3(p1), ToV3(p2), strokeStyle);
      }

      public static void DrawLine(this IDebugCanvas canvas, DoubleVector2 p1, DoubleVector2 p2, StrokeStyle strokeStyle) {
         canvas.DrawLine(ToV3(p1), ToV3(p2), strokeStyle);
      }

      public static void DrawLine(this IDebugCanvas canvas, IntLineSegment2 segment, StrokeStyle strokeStyle) {
         canvas.DrawLine(ToV3(segment.First), ToV3(segment.Second), strokeStyle);
      }

      public static void DrawLine(this IDebugCanvas canvas, DoubleLineSegment2 segment, StrokeStyle strokeStyle) {
         canvas.DrawLine(ToV3(segment.First), ToV3(segment.Second), strokeStyle);
      }

      public static void DrawLineList(this IDebugCanvas canvas, IReadOnlyList<IntVector2> points, StrokeStyle strokeStyle) {
         canvas.DrawLineList(points.Map(ToV3), strokeStyle);
      }

      public static void DrawLineList(this IDebugCanvas canvas, IReadOnlyList<DoubleVector2> points, StrokeStyle strokeStyle) {
         canvas.DrawLineList(points.Map(ToV3), strokeStyle);
      }

      public static void DrawLineList(this IDebugCanvas canvas, IReadOnlyList<IntLineSegment2> segments, StrokeStyle strokeStyle) {
         canvas.DrawLineList(segments.Map(ToILS3), strokeStyle);
      }


      public static void DrawLineStrip(this IDebugCanvas canvas, IReadOnlyList<IntVector2> points, StrokeStyle strokeStyle) {
         canvas.DrawLineStrip(points.Map(ToIV3), strokeStyle);
      }

      public static void DrawLineStrip(this IDebugCanvas canvas, IReadOnlyList<DoubleVector2> points, StrokeStyle strokeStyle) {
         canvas.DrawLineStrip(points.Map(ToV3), strokeStyle);
      }

      public static void DrawText(this IDebugCanvas canvas, string text, IntVector2 point) {
         canvas.DrawText(text, ToV3(point));
      }
   }
}