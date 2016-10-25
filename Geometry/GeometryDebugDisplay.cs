using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenMOBA.Debugging;

namespace OpenMOBA.Geometry {
   public static class GeometryDebugDisplay {
      public static void DrawPoints(this DebugDisplay display, IReadOnlyList<IntVector2> points, Pen pen) {
         display.Draw(g => {
            var r = pen.Width / 2.0f;
            for (var i = 0; i < points.Count; i ++) {
               g.DrawEllipse(pen, points[i].X - r, points[i].Y - r, r * 2, r * 2);
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
         using (var pen = new Pen(color)) {
            display.DrawLineStrip(polygon.Points, pen);
         }
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
