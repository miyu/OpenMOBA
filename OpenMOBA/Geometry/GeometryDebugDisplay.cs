using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ClipperLib;
using OpenMOBA.Debugging;

namespace OpenMOBA.Geometry {
   public static class GeometryDebugDisplay {
      public static void DrawPoint(this DebugCanvas canvas, IntVector2 point, Brush brush, float radius = 5.0f) {
         DrawPoints(canvas, new [] { point }, brush, radius);
      }

      public static void DrawPoints(this DebugCanvas canvas, IReadOnlyList<IntVector2> points, Brush brush, float radius = 5.0f) {
         canvas.Draw(g => {
            for (var i = 0; i < points.Count; i ++) {
               g.FillEllipse(brush, points[i].X - radius, points[i].Y - radius, radius * 2, radius * 2);
            }
         });
      }

      public static void DrawLineList(this DebugCanvas canvas, IReadOnlyList<IntVector2> points, Pen pen) {
         if (points.Count % 2 != 0) {
            throw new ArgumentException("Line List points must have even length.");
         }

         canvas.Draw(g => {
            for (var i = 0; i < points.Count; i += 2) {
               var a = points[i];
               var b = points[i + 1];
               g.DrawLine(pen, a.X, a.Y, b.X, b.Y);
            }
         });
      }

      public static void DrawLineList(this DebugCanvas canvas, IReadOnlyList<IntLineSegment2> segments, Pen pen) {
         canvas.DrawLineList(segments.SelectMany(s => s.Points).ToList(), pen);
      }

      public static void DrawPolygons(this DebugCanvas canvas, IReadOnlyList<Polygon> polygons, Color color) {
         using (var pen = new Pen(color)) {
            foreach (var polygon in polygons) {
               canvas.DrawLineStrip(polygon.Points, pen);
            }
         }
      }

      public static void DrawPolyTree(this DebugCanvas canvas, PolyTree polytree) {
         var s = new Stack<PolyNode>();
         s.Push(polytree);
         while (s.Any()) {
            var node = s.Pop();
            node.Childs.ForEach(s.Push);
            if (node.Contour.Any()) {
               canvas.DrawPolygon(
                  new Polygon(node.Contour.ToOpenMobaPoints(), node.IsHole),
                  node.IsHole ? Color.Brown : Color.Orange);
            }
         }
      }

      public static void DrawPolygon(this DebugCanvas canvas, Polygon polygon, Color color) {
         canvas.Draw(g => {
            using (var pen = new Pen(color)) {
               g.DrawPolygon(pen, polygon.Points.Select(p => new Point(p.X, p.Y)).ToArray());
            }
         });
      }

      public static void FillPolygon(this DebugCanvas canvas, Polygon polygon, Brush brush) {
         canvas.Draw(g => {
            g.FillPolygon(brush, polygon.Points.Select(p => new Point(p.X, p.Y)).ToArray());
         });
      }

      public static void DrawLineStrip(this DebugCanvas canvas, IReadOnlyList<IntVector2> points, Pen pen) {
         canvas.Draw(g => {
            for (var i = 0; i < points.Count - 1; i++) {
               var a = points[i];
               var b = points[(i + 1) % points.Count];
               g.DrawLine(pen, a.X, a.Y, b.X, b.Y);
            }
         });
      }
   }
}
