using System;
using System.Collections.Generic;
using System.Linq;
using OpenMOBA.DataStructures;
using OpenMOBA.Geometry;

namespace OpenMOBA.DevTool.Debugging {
   public static class DebugCanvas3DExtensions {
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

      public static void DrawAxisAlignedBoundingBox(this IDebugCanvas canvas, AxisAlignedBoundingBox box, StrokeStyle strokeStyle) {
         var extents = box.Extents;
         var nbl = box.Center - extents;
         var ftr = box.Center + extents;
         canvas.BatchDraw(() => {
            canvas.DrawLine(new DoubleVector3(nbl.X, nbl.Y, nbl.Z), new DoubleVector3(ftr.X, nbl.Y, nbl.Z), strokeStyle);
            canvas.DrawLine(new DoubleVector3(nbl.X, nbl.Y, nbl.Z), new DoubleVector3(nbl.X, ftr.Y, nbl.Z), strokeStyle);
            canvas.DrawLine(new DoubleVector3(nbl.X, nbl.Y, nbl.Z), new DoubleVector3(nbl.X, nbl.Y, ftr.Z), strokeStyle);

            canvas.DrawLine(new DoubleVector3(nbl.X, ftr.Y, nbl.Z), new DoubleVector3(ftr.X, ftr.Y, nbl.Z), strokeStyle);


            canvas.DrawLine(new DoubleVector3(nbl.X, nbl.Y, ftr.Z), new DoubleVector3(ftr.X, nbl.Y, ftr.Z), strokeStyle);
            canvas.DrawLine(new DoubleVector3(nbl.X, ftr.Y, ftr.Z), new DoubleVector3(ftr.X, ftr.Y, ftr.Z), strokeStyle);

            canvas.DrawLine(new DoubleVector3(nbl.X, nbl.Y, ftr.Z), new DoubleVector3(nbl.X, ftr.Y, ftr.Z), strokeStyle);
            canvas.DrawLine(new DoubleVector3(nbl.X, ftr.Y, nbl.Z), new DoubleVector3(nbl.X, ftr.Y, ftr.Z), strokeStyle);

            canvas.DrawLine(new DoubleVector3(ftr.X, nbl.Y, nbl.Z), new DoubleVector3(ftr.X, ftr.Y, nbl.Z), strokeStyle);
            canvas.DrawLine(new DoubleVector3(ftr.X, nbl.Y, ftr.Z), new DoubleVector3(ftr.X, ftr.Y, ftr.Z), strokeStyle);

            canvas.DrawLine(new DoubleVector3(ftr.X, nbl.Y, nbl.Z), new DoubleVector3(ftr.X, nbl.Y, ftr.Z), strokeStyle);
            canvas.DrawLine(new DoubleVector3(ftr.X, ftr.Y, nbl.Z), new DoubleVector3(ftr.X, ftr.Y, ftr.Z), strokeStyle);
         });
      }
   }
}