using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenMOBA.Debugging;

namespace OpenMOBA.Geometry {
   public static class GeometryDebugDisplay {
      public static void DrawPoint(this DebugDisplay display, IntVector2 point, Brush brush, float radius = 5.0f) {
         DrawPoints(display, new [] { point }, brush, radius);
      }

      public static void DrawPoints(this DebugDisplay display, IReadOnlyList<IntVector2> points, Brush brush, float radius = 5.0f) {
         display.Draw(g => {
            for (var i = 0; i < points.Count; i ++) {
               g.FillEllipse(brush, points[i].X - radius, points[i].Y - radius, radius * 2, radius * 2);
            }
         });
      }

      public static void DrawLineList(this DebugDisplay display, IReadOnlyList<IntVector2> points, Pen pen) {
         if (points.Count % 2 != 0) {
            throw new ArgumentException("Line List points must have even length.");
         }

         display.Draw(g => {
            for (var i = 0; i < points.Count; i += 2) {
               var a = points[i];
               var b = points[i + 1];
               g.DrawLine(pen, a.X, a.Y, b.X, b.Y);
            }
         });
      }

      public static void DrawLineList(this DebugDisplay display, IReadOnlyList<IntLineSegment2> segments, Pen pen) {
         display.DrawLineList(segments.SelectMany(s => s.Points).ToList(), pen);
      }

      public static void DrawPolygons(this DebugDisplay display, IReadOnlyList<Polygon> polygons, Color color) {
         using (var pen = new Pen(color)) {
            foreach (var polygon in polygons) {
               display.DrawLineStrip(polygon.Points, pen);
            }
         }
      }

      public static void DrawPolygon(this DebugDisplay display, Polygon polygon, Color color) {
         display.Draw(g => {
            using (var pen = new Pen(color)) {
               g.DrawPolygon(pen, polygon.Points.Select(p => new Point(p.X, p.Y)).ToArray());
            }
         });
      }

      public static void FillPolygon(this DebugDisplay display, Polygon polygon, Brush brush) {
         display.Draw(g => {
            g.FillPolygon(brush, polygon.Points.Select(p => new Point(p.X, p.Y)).ToArray());
         });
      }

      public static void DrawLineStrip(this DebugDisplay display, IReadOnlyList<IntVector2> points, Pen pen) {
         display.Draw(g => {
            for (var i = 0; i < points.Count - 1; i++) {
               var a = points[i];
               var b = points[(i + 1) % points.Count];
               g.DrawLine(pen, a.X, a.Y, b.X, b.Y);
            }
         });
      }
   }
}
