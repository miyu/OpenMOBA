using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dargon.Dviz;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.Geometry;

namespace Dargon.Dviz {
   public static class DebugCanvas3DExtensions {
      private static Vector3 ToV3(IntVector3 p) => new Vector3(p.X, p.Y, p.Z);
      private static Vector3 ToV3(DoubleVector3 p) => new Vector3((float)p.X, (float)p.Y, (float)p.Z);

      public static void DrawPoint(this IDebugCanvas canvas, IntVector3 p, StrokeStyle strokeStyle) {
         canvas.DrawPoint(ToV3(p), strokeStyle);
      }

      public static void DrawPoint(this IDebugCanvas canvas, DoubleVector3 p, StrokeStyle strokeStyle) {
         canvas.DrawPoint(ToV3(p), strokeStyle);
      }

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
               canvas.DrawPoint(ToV3(point), strokeStyle);
            }
         });
      }
      public static void DrawLine(this IDebugCanvas canvas, IntVector3 p1, IntVector3 p2, StrokeStyle strokeStyle) {
         canvas.DrawLine(ToV3(p1), ToV3(p2), strokeStyle);
      }

      public static void DrawLine(this IDebugCanvas canvas, DoubleVector3 p1, DoubleVector3 p2, StrokeStyle strokeStyle) {
         canvas.DrawLine(ToV3(p1), ToV3(p2), strokeStyle);
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
               canvas.DrawLine(ToV3(a), ToV3(b), strokeStyle);
            }
         });
      }

      // public static void DrawLineList(this IDebugCanvas canvas, IReadOnlyList<IntLineSegment3> segments, StrokeStyle strokeStyle) {
      //    canvas.DrawLineList(segments.SelectMany(s => s.Points).Select(p => p.ToDoubleVector3()).ToList(), strokeStyle);
      // }

      public static void DrawLineStrip(this IDebugCanvas canvas, IReadOnlyList<IntVector3> points, StrokeStyle strokeStyle) {
         canvas.DrawLineStrip(ToDoublePoints(points), strokeStyle);
      }

      public static void DrawLineStrip(this IDebugCanvas canvas, IReadOnlyList<DoubleVector3> points, StrokeStyle strokeStyle) {
         canvas.BatchDraw(() => {
            for (var i = 0; i < points.Count - 1; i++) {
               var a = points[i];
               var b = points[i + 1];
               canvas.DrawLine(ToV3(a), ToV3(b), strokeStyle);
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

      public static void DrawAxisAlignedBoundingBox(this IDebugCanvas canvas, AxisAlignedBoundingBox3 box, StrokeStyle strokeStyle) {
         var extents = box.Extents;
         var nbl = box.Center - extents;
         var ftr = box.Center + extents;
         canvas.BatchDraw(() => {
            Vector3 V3(double x, double y, double z) => new Vector3((float)x, (float)y, (float)z);

            canvas.DrawLine(V3(nbl.X, nbl.Y, nbl.Z), V3(ftr.X, nbl.Y, nbl.Z), strokeStyle);
            canvas.DrawLine(V3(nbl.X, nbl.Y, nbl.Z), V3(nbl.X, ftr.Y, nbl.Z), strokeStyle);
            canvas.DrawLine(V3(nbl.X, nbl.Y, nbl.Z), V3(nbl.X, nbl.Y, ftr.Z), strokeStyle);

            canvas.DrawLine(V3(nbl.X, ftr.Y, nbl.Z), V3(ftr.X, ftr.Y, nbl.Z), strokeStyle);

            canvas.DrawLine(V3(nbl.X, nbl.Y, ftr.Z), V3(ftr.X, nbl.Y, ftr.Z), strokeStyle);
            canvas.DrawLine(V3(nbl.X, ftr.Y, ftr.Z), V3(ftr.X, ftr.Y, ftr.Z), strokeStyle);

            canvas.DrawLine(V3(nbl.X, nbl.Y, ftr.Z), V3(nbl.X, ftr.Y, ftr.Z), strokeStyle);
            canvas.DrawLine(V3(nbl.X, ftr.Y, nbl.Z), V3(nbl.X, ftr.Y, ftr.Z), strokeStyle);

            canvas.DrawLine(V3(ftr.X, nbl.Y, nbl.Z), V3(ftr.X, ftr.Y, nbl.Z), strokeStyle);
            canvas.DrawLine(V3(ftr.X, nbl.Y, ftr.Z), V3(ftr.X, ftr.Y, ftr.Z), strokeStyle);

            canvas.DrawLine(V3(ftr.X, nbl.Y, nbl.Z), V3(ftr.X, nbl.Y, ftr.Z), strokeStyle);
            canvas.DrawLine(V3(ftr.X, ftr.Y, nbl.Z), V3(ftr.X, ftr.Y, ftr.Z), strokeStyle);
         });
      }
   }
}