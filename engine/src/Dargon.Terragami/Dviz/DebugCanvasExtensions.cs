using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Dargon.Commons;
using Dargon.Dviz;
using Dargon.PlayOn;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Geometry;
using Poly2Tri.Triangulation;

namespace Dargon.Terragami.Dviz {
   public static class DebugCanvasExtensions {
      private static Vector3 ToV3(IntVector2 p) => new Vector3(p.X, p.Y, 0);
      private static Vector3 ToV3(DoubleVector2 p) => new Vector3((float)p.X, (float)p.Y, 0);
      private static Vector3 ToV3(TriangulationPoint p) => new Vector3((float)p.X, (float)p.Y, 0);

      public static void DrawPoint(this IDebugCanvas canvas, IntVector2 p, StrokeStyle strokeStyle) {
         canvas.DrawPoint(ToV3(p), strokeStyle);
      }

      public static void DrawPoint(this IDebugCanvas canvas, DoubleVector2 p, StrokeStyle strokeStyle) {
         canvas.DrawPoint(ToV3(p), strokeStyle);
      }

      public static void DrawPoints(this IDebugCanvas canvas, IReadOnlyList<IntVector2> p, StrokeStyle strokeStyle) {
         canvas.DrawPoints(p.Map(ToV3), strokeStyle);
      }

      public static void DrawPoints(this IDebugCanvas canvas, IReadOnlyList<DoubleVector2> p, StrokeStyle strokeStyle) {
         canvas.DrawPoints(p.Map(ToV3), strokeStyle);
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
         canvas.DrawLineList(segments.Map(s => ToV3(s.First)), segments.Map(s => ToV3(s.Second)), strokeStyle);
      }

      public static void DrawLineList(this IDebugCanvas canvas, IReadOnlyList<DoubleLineSegment2> segments, StrokeStyle strokeStyle) {
         canvas.DrawLineList(segments.Map(s => ToV3(s.First)), segments.Map(s => ToV3(s.Second)), strokeStyle);
      }

      public static void DrawLineStrip(this IDebugCanvas canvas, IReadOnlyList<IntVector2> points, StrokeStyle strokeStyle) {
         canvas.DrawLineStrip(points.Map(ToV3), strokeStyle);
      }

      public static void DrawLineStrip(this IDebugCanvas canvas, IReadOnlyList<DoubleVector2> points, StrokeStyle strokeStyle) {
         canvas.DrawLineStrip(points.Map(ToV3), strokeStyle);
      }

      public static void DrawPolygon(this IDebugCanvas canvas, IReadOnlyList<IntVector2> poly, StrokeStyle strokeStyle) {
         canvas.DrawPolygon(poly.Map(ToV3), strokeStyle);
      }

      public static void DrawPolygon(this IDebugCanvas canvas, IReadOnlyList<DoubleVector2> poly, StrokeStyle strokeStyle) {
         canvas.DrawPolygon(poly.Map(ToV3), strokeStyle);
      }

      public static void DrawPolygon(this IDebugCanvas canvas, Polygon2 poly, StrokeStyle strokeStyle) {
         canvas.DrawPolygon(poly.Points.Map(ToV3), strokeStyle);
      }

      public static void DrawPolygons(this IDebugCanvas canvas, IReadOnlyList<Polygon2> polys, StrokeStyle strokeStyle) {
         foreach (var poly in polys) {
            canvas.DrawPolygon(poly, strokeStyle);
         }
      }

      public static void FillPolygon(this IDebugCanvas canvas, IReadOnlyList<IntVector2> poly, FillStyle fillStyle) {
         canvas.FillPolygon(poly.Map(ToV3), fillStyle);
      }

      public static void FillPolygon(this IDebugCanvas canvas, IReadOnlyList<DoubleVector2> poly, FillStyle fillStyle) {
         canvas.FillPolygon(poly.Map(ToV3), fillStyle);
      }

      public static void FillPolygon(this IDebugCanvas canvas, Polygon2 poly, FillStyle fillStyle) {
         canvas.FillPolygon(poly.Points.Map(ToV3), fillStyle);
      }

      public static void FillPolygon(this IDebugCanvas canvas, IReadOnlyList<Polygon2> polys, StrokeStyle strokeStyle) {
         foreach (var poly in polys) {
            canvas.DrawPolygon(poly, strokeStyle);
         }
      }

      public static void DrawPolygonNode(this IDebugCanvas canvas, PolygonNode polytree, StrokeStyle landStroke = null, StrokeStyle holeStroke = null) {
         landStroke = landStroke ?? new StrokeStyle(Color.Black); // Orange
         holeStroke = holeStroke ?? new StrokeStyle(Color.Red); // Brown

         canvas.BatchDraw(() => {
            var s = new Stack<PolygonNode>();
            s.Push(polytree);
            while (s.Any()) {
               var node = s.Pop();
               node.Children.ForEach(s.Push);
               if (node.Contour != null)
                  canvas.DrawPolygonContour(
                     node.Contour.Map(p => new Vector2(p.X, p.Y)).ToList(),
                     node.IsHole ? holeStroke : landStroke);
            }
         });
      }

      public static void DrawTriangle(this IDebugCanvas canvas, Triangle3 triangle, StrokeStyle strokeStyle) {
         canvas.DrawLineStrip(
            triangle.Points.Concat(new[] { triangle.Points.A }).Select(p => new Vector3((float)p.X, (float)p.Y, 0)).ToList(),
            strokeStyle);
      }


      public static void FillTriangle(this IDebugCanvas canvas, Triangle3 triangle, FillStyle fillStyle) {
         canvas.FillTriangle(
            new Vector3((float)triangle.Points.A.X, (float)triangle.Points.A.Y, 0),
            new Vector3((float)triangle.Points.B.X, (float)triangle.Points.B.Y, 0),
            new Vector3((float)triangle.Points.C.X, (float)triangle.Points.C.Y, 0),
            fillStyle);
      }

      public static void DrawTriangulation(this IDebugCanvas canvas, Triangulation triangulation, StrokeStyle strokeStyle) {
         canvas.BatchDraw(() => {
            foreach (var island in triangulation.Islands) foreach (var triangle in island.Triangles) canvas.DrawTriangle(triangle, strokeStyle);
         });
      }

      public static void FillTriangulation(this IDebugCanvas canvas, Triangulation triangulation, FillStyle fillStyle) {
         canvas.BatchDraw(() => {
            foreach (var island in triangulation.Islands) foreach (var triangle in island.Triangles) canvas.FillTriangle(triangle, fillStyle);
         });
      }

      public static void DrawRectangle(this IDebugCanvas canvas, IntRect2 nodeRect, float z, StrokeStyle strokeStyle) {
         canvas.DrawLineStrip(
            new[] {
               new Vector3(nodeRect.Left, nodeRect.Top, z),
               new Vector3(nodeRect.Right, nodeRect.Top, z),
               new Vector3(nodeRect.Right, nodeRect.Bottom, z),
               new Vector3(nodeRect.Left, nodeRect.Bottom, z),
               new Vector3(nodeRect.Left, nodeRect.Top, z)
            }, strokeStyle);
      }

      private static readonly StrokeStyle StrokeStyle1 = new StrokeStyle(Color.Red, 5, new[] { 3.0f, 1.0f });
      private static readonly StrokeStyle StrokeStyle2 = new StrokeStyle(Color.Lime, 5, new[] { 1.0f, 3.0f });
      private static readonly StrokeStyle StrokeStyle3 = new StrokeStyle(Color.Black, 1, new[] { 1.0f, 3.0f });

      public static void DrawBvh(this IDebugCanvas canvas, BvhILS2 bvh) {
         void Helper(BvhILS2 node, int d) {
            if (d != 0) {
               var s = new StrokeStyle(d % 2 == 0 ? Color.Red : Color.Lime, 10.0f / d, new[] { d % 2 == 0 ? 1.0f : 3.0f, d % 2 == 0 ? 3.0f : 1.0f });
               canvas.DrawRectangle(node.Bounds, 0.0f, s);
            }
            if (node.First != null) {
               Helper(node.First, d + 1);
               Helper(node.Second, d + 1);
            } else {
               for (var i = node.SegmentsStartIndexInclusive; i < node.SegmentsEndIndexExclusive; i++) {
                  canvas.DrawLine(
                     node.Segments[i].First.ToDotNetVector(), node.Segments[i].Second.ToDotNetVector(), StrokeStyle3);
               }
            }
         }
         Helper(bvh, 0);
      }
   }
}
