using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Numerics;
using Dargon.PlayOn;
using Dargon.PlayOn.DataStructures;
using Dargon.PlayOn.DevTool.Debugging;
using Dargon.PlayOn.Foundation;
using Dargon.PlayOn.Foundation.Terrain;
using Dargon.PlayOn.Foundation.Terrain.CompilationResults.Local;
using Dargon.PlayOn.Foundation.Terrain.Declarations;
using Dargon.PlayOn.Geometry;

namespace PolyTreeRasterizerTests {
   public class Program {
      private static readonly float renderScale = 1.0f;
      private static readonly Size bounds = new Size(1280, 720);
      private static readonly Random random = new Random(3);
      private static readonly DebugMultiCanvasHost host = DebugMultiCanvasHost.CreateAndShowCanvas(bounds, new Point(50, 50), new OrthographicXYProjector());
      private static int frameCounter = 0;

      public static void Main(string[] args) {
         var dc = (DebugCanvas)host.CreateAndAddCanvas(0);
         var tsm = SectorMetadataPresets.Test2D;
         var lgvm = new LocalGeometryViewManager(new LocalGeometryJob(tsm));
         var lgv = new LocalGeometryView(lgvm, 0, null);
         dc.BatchDraw(g => {
            Rescale(lgv, new Point(16, 16), new Size(256, 256));
         });
      }

      private static unsafe void Rescale(LocalGeometryView lgv, Point padding, Size size) {
         var tsm = lgv.Job.TerrainStaticMetadata;
         var dx = -tsm.LocalBoundary.Left;
         var sx = (double)(size.Width - padding.X * 2) / tsm.LocalBoundary.Width;
         var dy = -tsm.LocalBoundary.Top;
         var sy = (double)(size.Height - padding.Y * 2) / tsm.LocalBoundary.Height;

         sx = sy = Math.Min(sx, sy);

         var contourLists = new[] { lgv.PunchedLand.FlattenToPolygonAndIsHoles().Map(c => c.polygon) };
         var (pq, segments) = ToPQ(contourLists, dx, sx, dy, sy, padding.X, padding.Y);

         var res = new float[size.Height * size.Width];
         for (var i = 0; i < res.Length; i++) res[i] = float.PositiveInfinity;

         fixed (float* buff = res) {
            /* Draw octants to test segment rasterize
            // RasterizeSegmentDistanceField(buff, size.Width, size.Height, 20.3, 20.4, 30.6, 20.4); // x+
            // RasterizeSegmentDistanceField(buff, size.Width, size.Height, 20.3, 20.4, 30.6, 25.2); // o1
            // RasterizeSegmentDistanceField(buff, size.Width, size.Height, 20.3, 20.4, 25.4, 30.6); // o2
            // RasterizeSegmentDistanceField(buff, size.Width, size.Height, 20.3, 20.4, 20.3, 30.6); // y+
            // RasterizeSegmentDistanceField(buff, size.Width, size.Height, 20.3, 20.4, 15, 30.6); // o3
            // RasterizeSegmentDistanceField(buff, size.Width, size.Height, 20.3, 20.4, 10.6, 25.4); // o4
            // RasterizeSegmentDistanceField(buff, size.Width, size.Height, 20.3, 20.4, 10.6, 20.4); // x-
            // RasterizeSegmentDistanceField(buff, size.Width, size.Height, 20.3, 20.4, 10.6, 15.4); // o5
            // RasterizeSegmentDistanceField(buff, size.Width, size.Height, 20.3, 20.4, 15.6, 10.4); // o6
            // RasterizeSegmentDistanceField(buff, size.Width, size.Height, 20.3, 20.4, 20.3, 10.6); // y-
            // RasterizeSegmentDistanceField(buff, size.Width, size.Height, 20.3, 20.4, 25.3, 10.6); // o7
            // RasterizeSegmentDistanceField(buff, size.Width, size.Height, 20.3, 20.4, 30.3, 15.6); // o8
            */
            // RasterizePolygonsInteriorInvert(size, segments, buff, pq);
            // NormalizeAndSqrtBuffer(buff, res, -1, 1);
            // DistanceTransformTests.Program.DumpNormalizedImage2(buff, size.Width, size.Height, "buff_fill", false);

            RasterizeDistanceField(segments, buff, size.Width, size.Height);
            DistanceTransformTests.Program.DumpNormalizedImage2(buff, size.Width, size.Height, "buff");

            var temp = stackalloc float[res.Length];
            DistanceTransformTests.Program.EDT2(buff, temp, size.Width, size.Height, "buffedt");
            DistanceTransformTests.Program.DumpNormalizedImage2(buff, size.Width, size.Height, "buff_edt2", true);

            NormalizeAndSqrtBuffer(buff, res, -0.5f, 0.5f);
            DistanceTransformTests.Program.DumpNormalizedImage2(buff, size.Width, size.Height, "buff_normsqrt", false);

            RasterizePolygonsInteriorInvert(size, segments, buff, pq);

            DistanceTransformTests.Program.DumpNormalizedImage2(buff, size.Width, size.Height, "buff_sdf", false);
         }
      }

      private static unsafe void RasterizePolygonsInteriorInvert(Size size, DoubleLineSegment2[] segments, float* buff, PriorityQueue<(double y, bool start, int seg)> pq) {
         var nextYScanline = 0.5;
         var cmp = new SegComparer(segments);
         var active = new HashSet<int>();
         var activeIndices = new int[0];
         var pCurrentScaline = buff;
         while (pq.Count > 0) {
            var (y, start, si) = pq.Dequeue();
            while (y > nextYScanline) {
               // fill scanline.
               cmp.ScanlineY = nextYScanline;

               // sort indices & cache, segment order only changes on next event, because
               // no overlapping allowed.
               if (activeIndices == null) {
                  activeIndices = new int[active.Count];
                  var ni = 0;
                  foreach (var x in active) activeIndices[ni++] = x;
                  Array.Sort(activeIndices, cmp);
               }

               FillScanlineInvert05(pCurrentScaline, size.Width, activeIndices, cmp, nextYScanline);
               pCurrentScaline += size.Width;

               nextYScanline++;
            }

            if (start) active.Add(si);
            else active.Remove(si);
            activeIndices = null; // cache invalidate when add/remove events
         }
      }

      private static unsafe void NormalizeAndSqrtBuffer(float* buff, float[] res, float mag, float offset) {
         var max = float.MinValue;
         var pCurrentPixel = buff;
         for (var i = 0; i < res.Length; i++) {
            max = Math.Max(max, *pCurrentPixel);
            pCurrentPixel++;
         }
         var sqmax = (float)Math.Sqrt(max);

         pCurrentPixel = buff;
         if (max > 0 && max != float.PositiveInfinity) {
            for (var i = 0; i < res.Length; i++) {
               *pCurrentPixel = ((float)Math.Sqrt(*pCurrentPixel) / sqmax) * mag + offset;
               pCurrentPixel++;
            }
         }
      }

      private static unsafe void FillScanlineInvert05(float* pScanline, int scanlineWidth, int[] active, SegComparer sc, double scanY) {
         double? spanStart = null;
         for (var i = 0; i < active.Length; i++) {
            var si = active[i];
            var cx = sc.EvalX(si, scanY);
            if (spanStart == null) {
               spanStart = cx;
               continue;
            }

            var sx = (int)Math.Round(spanStart.Value); // 0.4 fills px 0, 0.6 fills px 1
            var ex = (int)Math.Round(cx) - 1; // 1.4 ends at px0, 1.6 ends at px1, as pixel center is at 0.5 offset.
            var pCurrentPixel = pScanline + sx;
            // Console.WriteLine(sx + " " + ex);
            for (var x = sx; x <= ex; x++) {
               *pCurrentPixel = 1.0f - *pCurrentPixel;
               pCurrentPixel++;
            }
            spanStart = null;
         }
      }

      private class SegComparer : IComparer<int> {
         private readonly DoubleLineSegment2[] segs;
         private readonly DoubleVector2[] segsPreprocessed;
         private static readonly Func<DoubleLineSegment2, DoubleVector2> preprocessXAtYFunc = GeometryOperations.PreprocessXAtY;

         public SegComparer(DoubleLineSegment2[] segs) {
            this.segs = segs;
            this.segsPreprocessed = segs.Map(preprocessXAtYFunc);
         }

         public double ScanlineY { get; set; }

         public double EvalX(int si, double y) {
            return segsPreprocessed[si].XAtYPreprocessed(y);
         }

         public int Compare(int a, int b) {
            var ax = segsPreprocessed[a].XAtYPreprocessed(ScanlineY);
            var bx = segsPreprocessed[b].XAtYPreprocessed(ScanlineY);
            return ax.CompareTo(bx);
         }
      }


      private static unsafe void RasterizeDistanceField(DoubleLineSegment2[] segments, float* buff, int width, int height) {
         foreach (var seg in segments) {
            // Console.WriteLine(seg);
            RasterizeSegmentDistanceField(buff, width, height, seg.X1, seg.Y1, seg.X2, seg.Y2);
         }
      }

      private static unsafe void RasterizeSegmentDistanceField(float* buff, int width, int height, double x0, double y0, double x1, double y1) {
         int R(double v) => (int)Math.Round(v - 0.5);
         if (Math.Abs(y1 - y0) < Math.Abs(x1 - x0)) {
            // shallow
            if (x0 > x1) {
               (x0, x1) = (x1, x0);
               (y0, y1) = (y1, y0);
            }
            RasterizeSegmentDistanceFieldInternal(buff, R(x0), R(y0), R(x1), R(y1), width, 1, new DoubleLineSegment2(new DoubleVector2(x0, y0), new DoubleVector2(x1, y1)));
         } else {
            // steep
            if (y0 > y1) {
               (x0, x1) = (x1, x0);
               (y0, y1) = (y1, y0);
            }
            RasterizeSegmentDistanceFieldInternal(buff, R(y0), R(x0), R(y1), R(x1), 1, width, new DoubleLineSegment2(new DoubleVector2(y0, x0), new DoubleVector2(y1, x1)));
         }
      }

      // bresenham variant, p1 must be in (-45, 45) degrees from p0.
      private static unsafe void RasterizeSegmentDistanceFieldInternal(float* buff, int x0, int y0, int x1, int y1, int stride, int step, DoubleLineSegment2 seg) {
         var e = 0;

         void Fill3(float* pixel, int x, int y, int str, ref DoubleLineSegment2 s) {
            var p = new DoubleVector2(x + 0.5, y + 0.5);
            var ppixel = pixel;
            *ppixel = Math.Min(*ppixel, (float)p.To(GeometryOperations.FindNearestPoint(s, p)).Norm2D());

            p = new DoubleVector2(x + 0.5, y - 0.5);
            ppixel = pixel - str;
            *ppixel = Math.Min(*ppixel, (float)p.To(GeometryOperations.FindNearestPoint(s, p)).Norm2D());

            p = new DoubleVector2(x + 0.5, y + 1.5);
            ppixel = pixel + str;
            *ppixel = Math.Min(*ppixel, (float)p.To(GeometryOperations.FindNearestPoint(s, p)).Norm2D());
         }

         Fill3(buff + y0 * stride + (x0 - 1) * step, x0 - 1, y0, stride, ref seg);

         int cap = 1000;
         int derr = x0 == x1 ? 0 : Math.Abs(cap * (y1 - y0) / (x1 - x0));
         int err = 0;
         int cy = y0;
         int dy = y0 <= y1 ? 1 : -1;
         int dpcpfordy = stride * dy;
         float* pcp = buff + y0 * stride + x0 * step;

         for (var x = x0; x <= x1; x++, pcp += step) {
            Fill3(pcp, x, cy, stride, ref seg);
            err += derr;

            if (err > cap) {
               err -= cap;
               cy += dy;
               pcp += dpcpfordy;
            }
         }

         Fill3(buff + y1 * stride + (x1 + 1) * step, x1 + 1, y1, stride, ref seg);
      }

      private static (PriorityQueue<(double y, bool start, int seg)>, DoubleLineSegment2[]) ToPQ(IReadOnlyList<Polygon2>[] contourLists, int dx, double sx, int dy, double sy, int ox, int oy) {
         var pq = new PriorityQueue<(double y, bool start, int seg)>(
            (a, b) => {
               var res = a.y.CompareTo(b.y);
               if (res != 0) return res;
               res = -a.start.CompareTo(b.start);
               if (res != 0) return res;
               return 0;
            });

         int numSegments = 0;
         foreach (var contourList in contourLists) {
            foreach (var poly in contourList) {
               numSegments += poly.Points.Count - 1;
            }
         }

         var segments = new DoubleLineSegment2[numSegments];
         var nextSegmentIndex = 0;
         foreach (var contourList in contourLists) {
            foreach (var poly in contourList) {
               var ps = poly.Points;
               var lastP = DoubleVector2.Zero;
               for (var i = 0; i < ps.Count; i++) {
                  var p = ps[i];
                  var q = new DoubleVector2((p.X + dx) * sx + ox, (p.Y + dy) * sy + oy);
                  if (i != 0) {
                     var a = q;
                     var b = lastP;
                     if (a.Y > b.Y) (a, b) = (b, a); // ensure a.Y < b.Y
                     var seg = new DoubleLineSegment2(a, b);
                     pq.Enqueue((a.Y, true, nextSegmentIndex));
                     pq.Enqueue((b.Y, false, nextSegmentIndex));
                     segments[nextSegmentIndex] = seg;
                     nextSegmentIndex++;
                  }
                  lastP = q;
               }
            }
         }

         Trace.Assert(nextSegmentIndex == numSegments);
         return (pq, segments);
      }
   }
}
