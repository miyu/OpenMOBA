using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ClipperLib;
using OpenMOBA.DataStructures;
using OpenMOBA.Debugging;
using OpenMOBA.Geometry;

namespace OpenMOBA.DevTool.Debugging {
   public static class GeometryDebugDisplay {
      public static void DrawPolyNode(this IDebugCanvas canvas, PolyNode polytree, StrokeStyle landStroke = null, StrokeStyle holeStroke = null) {
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
                     new Polygon2(node.Contour.Select(p => new IntVector2(p.X, p.Y)).ToList()),
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
