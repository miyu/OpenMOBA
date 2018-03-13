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
               var b = points[i + 1];
               canvas.DrawLine(a, b, strokeStyle);
            }
         });
      }

      //      public static void FillPolygon(this IDebugCanvas canvas, Polygon3 polygon, FillStyle fillStyle) {
      //         canvas.FillPolygon(polygon.Points.Select(p => p.ToDoubleVector3()).ToList(), fillStyle);
      //      }
      //
      //      public static void DrawPolygonContour(this IDebugCanvas canvas, Polygon3 polygon, StrokeStyle strokeStyle) {
      //         canvas.DrawPolygonContour(polygon.Points.Select(p => p.ToDoubleVector3()).ToList(), strokeStyle);
      //      }

      //      public static void DrawPolygons(this IDebugCanvas canvas, IReadOnlyList<Polygon3> polygons, StrokeStyle strokeStyle) {
      //         canvas.BatchDraw(() => {
      //            foreach (var polygon in polygons) {
      //               canvas.DrawPolygonContour(polygon, strokeStyle);
      //            }
      //         });
      //      }
      //
      //      public static void DrawPolygons(this IDebugCanvas canvas, IReadOnlyList<IReadOnlyList<DoubleVector3>> contours, StrokeStyle strokeStyle) {
      //         canvas.BatchDraw(() => {
      //            foreach (var contour in contours) {
      //               canvas.DrawPolygonContour(contour, strokeStyle);
      //            }
      //         });
      //      }

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
                  canvas.DrawPolygonContour(
                     new Polygon2(node.Contour.Select(p => new IntVector2(p.X, p.Y)).ToList(), node.IsHole),
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

      public static void FillTriangulation(this IDebugCanvas canvas, Triangulation triangulation, FillStyle fillStyle) {
         canvas.BatchDraw(() => {
            foreach (var island in triangulation.Islands) {
               foreach (var triangle in island.Triangles) {
                  canvas.FillTriangle(triangle, fillStyle);
               }
            }
         });
      }

      public static void DrawTriangle(this IDebugCanvas canvas, Triangle3 triangle, StrokeStyle strokeStyle) {
         canvas.DrawLineStrip(
            triangle.Points.Concat(new[] { triangle.Points.A }).Select(p => new DoubleVector3(p.X, p.Y, 0)).ToList(),
            strokeStyle);
      }


      public static void FillTriangle(this IDebugCanvas canvas, Triangle3 triangle, FillStyle fillStyle) {
         canvas.FillTriangle(
            new DoubleVector3((float)triangle.Points.A.X, (float)triangle.Points.A.Y, 0),
            new DoubleVector3((float)triangle.Points.B.X, (float)triangle.Points.B.Y, 0),
            new DoubleVector3((float)triangle.Points.C.X, (float)triangle.Points.C.Y, 0),
            fillStyle);
      }

      public static void DrawRectangle(this IDebugCanvas canvas, IntRect2 nodeRect, float z, StrokeStyle strokeStyle) {
         canvas.DrawLineStrip(
            new [] {
               new DoubleVector3(nodeRect.Left, nodeRect.Top, z),
               new DoubleVector3(nodeRect.Right, nodeRect.Top, z),
               new DoubleVector3(nodeRect.Right, nodeRect.Bottom, z),
               new DoubleVector3(nodeRect.Left, nodeRect.Bottom, z),
               new DoubleVector3(nodeRect.Left, nodeRect.Top, z)
            }, strokeStyle);
      }
   }
}
