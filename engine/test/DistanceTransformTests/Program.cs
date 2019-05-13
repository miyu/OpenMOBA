using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Dargon.PlayOn;
using Dargon.PlayOn.DevTool.Debugging;
using Dargon.PlayOn.Foundation;
using Dargon.PlayOn.Foundation.Terrain;
using Dargon.PlayOn.Foundation.Terrain.Declarations;
using Dargon.PlayOn.Geometry;

namespace DistanceTransformTests {
   public class Program {
      private static readonly float renderScale = 1.0f;
      private static readonly Size bounds = new Size(1280, 720);
      private static readonly Random random = new Random(3);
      private static readonly DebugMultiCanvasHost host = DebugMultiCanvasHost.CreateAndShowCanvas(bounds, new Point(50, 50), new OrthographicXYProjector());
      private static int frameCounter = 0;
      private const int yScale = 10;

      public static void Main(string[] args) {
         var n = 64;
         var input = new float[n];
         for (var i = 0; i < n;) {
            var span = Math.Min(random.Next(10) + 1, n - i);
            var val = random.Next(2) == 0 ? 0 : float.PositiveInfinity;
            for (var j = 0; j < span; j++) {
               input[i++] = val;
            }
         }
         for (var i = 0; i < n; i++) {
            input[i] = (float)Math.Sin(i * Math.PI * 2 / 32) * 2 * yScale + 6 * yScale;
            if (i >= 35 && i < 40) input[i] -= 2 * yScale;
            if (i == 10 || i == 45 || i == 46) input[i] -= 5 * yScale;
            // if (i >= 58) input[i] = 9 * yScale;
            if (i >= 20 && i <= 22) input[i] = float.PositiveInfinity;
            if (i == n - 1) input[i] = 1 * yScale;
         }
         EuclideanDistanceTransform1(input);
      }

      private static unsafe void EuclideanDistanceTransform1(float[] input) {
         var output = new float[input.Length];
         fixed (float* pInput = input)
         fixed (float* pOutput = output)
            EDT1(input.Length, pInput, pOutput, sizeof(float));
      }

      private static unsafe void EDT1(int gridLength, float* input, float* output, int stride) {
         var k = 0;
         var v = stackalloc int[gridLength]; v[0] = 0;
         var z = stackalloc float[gridLength + 1]; z[0] = float.NegativeInfinity; z[1] = float.PositiveInfinity;
         EDT1Viz(gridLength, input, output, -1, v, z);
         EDT1Viz(gridLength, input, output, k, v, z, 0);
         for (var q = 1; q < gridLength; q++) {
            six:
            var vk = v[k];
            var s = ((input[q] + q * q) - (input[vk] + vk * vk)) / (2 * q - 2 * vk);
            if (float.IsNaN(s) || float.IsInfinity(input[q])) {
               continue;
            }

            if (s <= z[k]) {
               if (k == 0) { // no further parabolas to drop, but current parabola is valid!
                  v[0] = q;
               } else {
                  EDT1Viz(gridLength, input, output, k, v, z, q);
                  z[k] = float.PositiveInfinity; // for debug viz
                  z[k + 1] = float.NaN; // for debug viz
                  k--;
                  goto six;
               }
            } else {
               EDT1Viz(gridLength, input, output, k, v, z, q);
               k++;
               v[k] = q;
               z[k] = s;
               z[k + 1] = float.PositiveInfinity;
            }
         }
         EDT1Viz(gridLength, input, output, k, v, z, gridLength - 1);
         EDT1Viz(gridLength, input, output, k, v, z, -1);

         k = 0;
         for (var q = 0; q < gridLength; q++) {
            while (z[k + 1] < q) k++;
            var vk = v[k];
            var qmvk = (q - vk);
            output[q] = qmvk * qmvk + input[vk];
         }
      }

      private static unsafe void EDT1Viz(int gridLength, float* input, float* output, int k, int* v, float* z, int? sweeplineIndex = null) {
         var canvas = host.CreateAndAddCanvas(frameCounter++);
         canvas.BatchDraw(() => {
            float displayHeight = 10 * yScale;
            canvas.Transform = Matrix4x4.CreateScale((float)bounds.Width / (gridLength - 1), bounds.Height / displayHeight, 1) * Matrix4x4.CreateScale(1, -1, 1) * Matrix4x4.CreateTranslation(0, bounds.Height, 0);
            canvas.DrawLine(DoubleVector2.Zero, new DoubleVector2(gridLength - 1, 0), StrokeStyle.BlackHairLineSolid);
            canvas.DrawLine(DoubleVector2.Zero, new DoubleVector2(0, displayHeight), StrokeStyle.BlackHairLineSolid);
            var xMax = gridLength - 1;

            for (var i = gridLength - 1; i >= 0; i--) {
               var sx = i;
               var y = input[i] == 0 ? 0 : bounds.Height;
               var stroke = StrokeStyle.GrayHairLineSolid;
               // if (sweeplineIndex.HasValue && i > sweeplineIndex.Value) stroke = StrokeStyle.GrayHairLineSolid;
               DrawParabola(canvas, i, input[i], 0, gridLength - 1, stroke);
               //canvas.DrawLine(new DoubleVector2(sx - 0.1f, y), new DoubleVector2(sx + 0.1f, y), StrokeStyle.CyanThick5Solid);

               //var oy = output[i];
               //canvas.DrawLine(new DoubleVector2(sx - 0.1f, oy), new DoubleVector2(sx + 0.1f, oy), StrokeStyle.LimeThick5Solid);
            }


            for (var i = 0; i <= k; i++) {
               var vi = v[i];
               var sx = z[i];
               var ex = z[i + 1];

               sx = Math.Max(0, Math.Min(xMax, sx));
               ex = Math.Max(0, Math.Min(xMax, ex));

               DrawParabola(canvas, vi, input[vi], sx, i == k ? gridLength - 1 : ex, StrokeStyle.OrangeThick10Solid);
            }

            if (k >= 0 && sweeplineIndex != -1) {
               var vk = v[k];
               var sx = z[k];
               sx = Math.Max(0, Math.Min(xMax, sx));

               canvas.DrawLine(new DoubleVector2(vk, 0), new DoubleVector2(vk, displayHeight), StrokeStyle.CyanHairLineSolid);
               canvas.DrawPoint(new DoubleVector2(vk, input[vk]), StrokeStyle.CyanThick10Solid);
               DrawParabola(canvas, vk, input[vk], 0, sx, StrokeStyle.CyanHairLineSolid);
               DrawParabola(canvas, vk, input[vk], sx, xMax, StrokeStyle.CyanThick5Solid);
            }

            if (sweeplineIndex.HasValue && sweeplineIndex != -1) {
               var vk = sweeplineIndex.Value;
               canvas.DrawLine(new DoubleVector2(vk, 0), new DoubleVector2(vk, displayHeight), StrokeStyle.RedThick3Solid);
               DrawParabola(canvas, vk, input[vk], 0, gridLength - 1, StrokeStyle.RedThick5Solid);
            }

            var fillStyle = new FillStyle(SystemColors.Control);
            canvas.FillTriangle(new DoubleVector2(0, displayHeight), new DoubleVector2(gridLength, displayHeight), new DoubleVector2(0, displayHeight + 10), fillStyle);
            canvas.FillTriangle(new DoubleVector2(gridLength, displayHeight), new DoubleVector2(0, displayHeight + 10), new DoubleVector2(gridLength, displayHeight + 10), fillStyle);
         });
      }

      private static float SampleParabola(float x, float cx) => (cx - x) * (cx - x);

      private static void DrawParabola(IDebugCanvas canvas, int cx, float cy, float sx, float ex, StrokeStyle strokeStyle) {
         DoubleVector2? lastPoint = null;
         var dx = (ex - sx);
         var nIters = (float)Math.Ceiling(dx / 0.1f);
         for (int i = 0; i < nIters; i++) {
            var x = sx + dx * i / (nIters - 1);
            var y = SampleParabola(x, cx) + cy;
            var p = new DoubleVector2(x, y);
            // canvas.DrawPoint(p, StrokeStyle.RedThick5Solid);
            if (lastPoint.HasValue) {
               canvas.DrawLine(lastPoint.Value, p, strokeStyle);
            }
            lastPoint = p;
         }
      }

      private static void RenderSomething(int canvasIndex, Polygon2 subject, Polygon2 clip) {
         var canvas = host.CreateAndAddCanvas(canvasIndex);
         canvas.Transform = Matrix4x4.CreateTranslation(150, 150, 0) * Matrix4x4.CreateScale(2);
         // canvas.DrawLineStrip(subject.Points.Concat(new[] { subject.Points[0] }).ToArray(), StrokeStyle.CyanThick3Solid);
         // canvas.DrawLineStrip(clip.Points.Concat(new[] { clip.Points[0] }).ToArray(), StrokeStyle.RedThick3Solid);

         canvas.Transform = Matrix4x4.CreateTranslation(450, 150, 0) * Matrix4x4.CreateScale(2);
         // canvas.DrawLineStrip(subject.Points.Concat(new[] { subject.Points[0] }).ToArray(), StrokeStyle.CyanHairLineSolid);
         // canvas.DrawLineStrip(clip.Points.Concat(new[] { clip.Points[0] }).ToArray(), StrokeStyle.RedHairLineSolid);

         // if (PolygonOperations.TryConvexClip(subject, clip, out var result)) {
            // canvas.DrawLineStrip(result.Points.Concat(new[] { result.Points[0] }).ToArray(), StrokeStyle.LimeThick5Solid);
         // }
      }
   }
}