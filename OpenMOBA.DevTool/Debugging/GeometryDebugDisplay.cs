using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ClipperLib;
using OpenMOBA.DataStructures;
using OpenMOBA.Geometry;

namespace OpenMOBA.DevTool.Debugging {
   public static class GeometryDebugDisplay {
      private static List<DoubleVector3> ToDoublePoints(IReadOnlyList<IntVector3> ps) {
         var result = new List<DoubleVector3>(ps.Count);
         for (var i = 0; i < ps.Count; i++) {
            result.Add(ps[i].ToDoubleVector3());
         }
         return result;
      }

      public static void DrawPoints(this IDebugCanvas canvas, IReadOnlyList<IntVector3> points, StrokeStyle strokeStyle) {
         canvas.DrawPoints(ToDoublePoints(points), strokeStyle);
      }

      public static void DrawPoints(this IDebugCanvas canvas, IReadOnlyList<DoubleVector3> points, StrokeStyle strokeStyle) {
         canvas.BatchDraw(() => {
            foreach (var point in points) {
               canvas.DrawPoint(point, strokeStyle);
            }
         });
      }

      public static void DrawLineList(this IDebugCanvas canvas, IReadOnlyList<IntVector3> points, StrokeStyle strokeStyle) {
         canvas.DrawLineList(ToDoublePoints(points), strokeStyle);
      }

      public static void DrawLineList(this IDebugCanvas canvas, IReadOnlyList<DoubleVector3> points, StrokeStyle strokeStyle) {
         if (points.Count % 2 != 0) {
            throw new ArgumentException("Line List points must have even length.");
         }

         canvas.BatchDraw(() => {
            for (var i = 0; i < points.Count; i += 2) {
               var a = points[i];
               var b = points[i + 1];
               canvas.DrawLine(a, b, strokeStyle);
            }
         });
      }

      public static void DrawLineList(this IDebugCanvas canvas, IReadOnlyList<IntLineSegment3> segments, StrokeStyle strokeStyle) {
         canvas.DrawLineList(segments.SelectMany(s => s.Points).Select(p => p.ToDoubleVector3()).ToList(), strokeStyle);
      }

      public static void DrawLineStrip(this IDebugCanvas canvas, IReadOnlyList<IntVector3> points, StrokeStyle strokeStyle) {
         canvas.DrawLineStrip(ToDoublePoints(points), strokeStyle);
      }

      public static void DrawLineStrip(this IDebugCanvas canvas, IReadOnlyList<DoubleVector3> points, StrokeStyle strokeStyle) {
         canvas.BatchDraw(() => {
            for (var i = 0; i < points.Count - 1; i++) {
               var a = points[i];
               var b = points[(i + 1) % points.Count];
               canvas.DrawLine(a, b, strokeStyle);
            }
         });
      }

      public static void FillPolygon(this IDebugCanvas canvas, Polygon polygon, FillStyle fillStyle) {
         canvas.FillPolygon(polygon.Points.Select(p => p.ToDoubleVector3()).ToList(), fillStyle);
      }

      public static void DrawPolygon(this IDebugCanvas canvas, Polygon polygon, StrokeStyle strokeStyle) {
         canvas.DrawPolygon(polygon.Points.Select(p => p.ToDoubleVector3()).ToList(), strokeStyle);
      }

      public static void DrawPolygons(this IDebugCanvas canvas, IReadOnlyList<Polygon> polygons, StrokeStyle strokeStyle) {
         canvas.BatchDraw(() => {
            foreach (var polygon in polygons) {
               canvas.DrawPolygon(polygon, strokeStyle);
            }
         });
      }

      public static void DrawPolyTree(this IDebugCanvas canvas, PolyTree polytree, StrokeStyle landStroke = null, StrokeStyle holeStroke = null) {
         landStroke = landStroke ?? new StrokeStyle(Color.Orange);
         holeStroke = holeStroke ?? new StrokeStyle(Color.Brown);

         canvas.BatchDraw(() => {
            var s = new Stack<PolyNode>();
            s.Push(polytree);
            while (s.Any()) {
               var node = s.Pop();
               node.Childs.ForEach(s.Push);
               if (node.Contour.Any()) {
                  canvas.DrawPolygon(
                     new Polygon(node.Contour, node.IsHole),
                     node.IsHole ? holeStroke : landStroke);
               }
            }
         });
      }

      public static void DrawTriangulation(this IDebugCanvas canvas, Triangulation triangulation, StrokeStyle strokeStyle) {
         canvas.BatchDraw(() => {
            foreach (var island in triangulation.Islands) {
               foreach (var triangle in island.Triangles) {
                  canvas.DrawTriangle(triangle, strokeStyle);
               }
            }
         });
      }

      public static void DrawTriangle(this IDebugCanvas canvas, Triangle3 triangle, StrokeStyle strokeStyle) {
         canvas.DrawLineStrip(
            triangle.Points.Concat(new[] { triangle.Points.A }).ToList(),
            strokeStyle);
      }

      public static void DrawRectangle(this IDebugCanvas canvas, IntRect2 nodeRect, float z, StrokeStyle strokeStyle) {
         canvas.DrawLineStrip(
            new [] {
               new DoubleVector3(nodeRect.Left, nodeRect.Top, z),
               new DoubleVector3(nodeRect.Right, nodeRect.Top, z),
               new DoubleVector3(nodeRect.Right, nodeRect.Bottom, z),
               new DoubleVector3(nodeRect.Left, nodeRect.Bottom, z)
            }, strokeStyle);
      }
   }
}
