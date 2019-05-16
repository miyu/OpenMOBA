using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Numerics;
using Dargon.Commons;
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
      private const int yScale = 1;

      public static void Main(string[] args) {
         TestEDT2("EDT2_In1");
         // TestEDT1();
      }

      private unsafe static void TestEDT2(string img) {
         var image = (Bitmap)Image.FromFile(img + ".png");
         var imageWidth = image.Width;
         var imageHeight = image.Height;
         var bits = new bool[imageWidth * imageHeight];
         for (var y = 0; y < imageHeight; y++) {
            for (var x = 0; x < imageWidth; x++) {
               bits[y * imageWidth + x] = image.GetPixel(x, y).R > 127;
            }
         }

         var edt2 = new float[bits.Length];
         EDT2(bits, edt2, imageWidth, imageHeight, img + "_INT");

         while (true) {
            var sw = new Stopwatch();
            sw.Start();
            var niters = 1000;
            for (var i = 0; i < niters; i++)
               EDT2(bits, edt2, imageWidth, imageHeight);
            var ms = sw.ElapsedMilliseconds;
            Console.WriteLine(niters + " IN " + ms + " " + imageWidth + "-by-" + imageHeight);
         }

         fixed (float* pEdt2Scan0 = edt2) {
            DumpNormalizedImage2(pEdt2Scan0, imageWidth, imageHeight, img + "EDT2_OUT");
         }
      }

      public static unsafe void DumpNormalizedImage2(float* imageData, int imageWidth, int imageHeight, string name, bool sqrt = false) {
         var max = float.NegativeInfinity;
         var size = imageWidth * imageHeight;
         for (var i = 0; i < size; i++) {
            var v = imageData[i];
            if (v == float.PositiveInfinity) {
               continue;
            }
            if (sqrt) v = (float)Math.Sqrt(v);
            if (v > max) max = v;
         }

         if (max <= 0) max = 1;

         var outputBitmap = new Bitmap(imageWidth, imageHeight, PixelFormat.Format24bppRgb);
         var outputBitmapData = outputBitmap.LockBits(new Rectangle(0, 0, imageWidth, imageHeight), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
         var pOutputScan0 = outputBitmapData.Scan0;
         var outputStride = outputBitmapData.Stride;
         var pInputCurrent = imageData;
         for (var y = 0; y < imageHeight; y++) {
            var pOutputCurrent = (byte*)pOutputScan0 + outputStride * y;
            for (var x = 0; x < imageWidth; x++) {
               var v = *pInputCurrent;
               if (v == float.PositiveInfinity) v = max;
               if (sqrt) v = (float)Math.Sqrt(v);
               var z = (byte)((v / max) * 255);
               *(pOutputCurrent++) = z;
               *(pOutputCurrent++) = z;
               *(pOutputCurrent++) = z;
               pInputCurrent++;
            }
         }
         outputBitmap.UnlockBits(outputBitmapData);
         outputBitmap.Save(Environment.CurrentDirectory + "/" + name + ".png", ImageFormat.Png);
         Console.WriteLine("!! " + Environment.CurrentDirectory);
      }

      private static void TestEDT1() {
         var n = 1024;
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

      private static unsafe void DumpRow(float* p, int w) {
         Console.Write("RowDump: ");
         for (var i = 0; i < w; i++) {
            Console.Write(p[i] + " ");
         }
         Console.WriteLine();
      }

      private static unsafe void EDT2(bool[] inputBools, float[] output, int width, int height, string dumpName = null) {
         Assert.Equals(width * height, inputBools.Length);
         Assert.Equals(width * height, output.Length);

         fixed (bool* pInputBools = inputBools) 
         fixed (float* m2 = output) {
            var size = width * height;
            var m1 = stackalloc float[size];

            // 1. bool[,] => float[,] m2[h,w]
            {
               var p = pInputBools;
               var q = m2;
               for (var i = 0; i < size; i++)
                  *(q++) = *(p++) ? 0 : float.PositiveInfinity;
            }

            if (dumpName != null) DumpNormalizedImage2(m2, width, height, dumpName + "_0_floats");
            // DumpRow(&m2[(height - 1) * width], width);
            
            EDT2(m1, m2, width, height, dumpName);
         }
      }

      public static unsafe void EDT2(float* inoutrw, float* temp, int width, int height, string dumpName) {
         var m2 = inoutrw;
         var m1 = temp;

         void RowsEDT1(float* p, float* q, int w, int h) {
            for (var y = 0; y < h; y++) {
               EDT1(w, p, q);
               p += w;
               q += w;
            }
         }

         void Transpose(float* p, float* q, int w, int h) {
            // w h of p.
            var pCurrent = p;
            var qScan0 = q;
            for (var y = 0; y < h; y++) {
               var qCurrent = qScan0 + y;
               for (var x = 0; x < w; x++) {
                  *qCurrent = *pCurrent;
                  pCurrent++;
                  qCurrent += h;
               }
            }
         }

         {
            // m2[h,w] float[,] => map(rows, EDT1) m1[h,w]
            RowsEDT1(m2, m1, width, height);
            if (dumpName != null) DumpNormalizedImage2(m1, width, height, dumpName + "_1_edt");
            // DumpRow(&m1[(height - 1) * width], width);

            // m1[h,w] map(rows, EDT1) => transpose(map(rows, EDT1)) m2[w,h]
            Transpose(m1, m2, width, height);
            if (dumpName != null) DumpNormalizedImage2(m2, height, width, dumpName + "_2_tr");
            // DumpRow(&m2[(width - 1) * height], height);

            // m2[w,h] transpose(map(rows, EDT1)) => map(rows of transpose(map(rows, EDT1)), EDT1) m1[w,h]
            RowsEDT1(m2, m1, height, width);
            if (dumpName != null) DumpNormalizedImage2(m1, height, width, dumpName + "_3_edt");
            // DumpRow(&m1[(width - 1) * height], height);

            // m1[w,h] map(rows of transpose(map(rows, EDT1)), EDT1) => transpose(map(rows of transpose(map(rows, EDT1)), EDT1)) m2[h,w]
            Transpose(m1, m2, height, width);
            if (dumpName != null) DumpNormalizedImage2(m2, width, height, dumpName + "_4_tr");
            // DumpRow(&m2[(height - 1) * width], width);
         }
      }

      private static unsafe void EuclideanDistanceTransform1(float[] input) {
         var output = stackalloc float[input.Length];
         for (var i = 0; i < 10000; i++)
            fixed (float* pInput = input)
               EDT1(input.Length, pInput, output);

         float avg = 0;
         int n = 0;
         while (true) {
            var sw = new Stopwatch();
            sw.Start();
            var niters = 1000;
            fixed (float* pInput = input)
               for (var i = 0; i < niters; i++) {
                  EDT1(input.Length, pInput, output);
               }
            var dt = sw.ElapsedMilliseconds;
            avg = (avg * n + dt) / (n + 1);
            n++;
            Console.WriteLine(dt + " for " + niters + ": " + avg);
         }
      }

      private static unsafe void EDT1(int gridLength, float* input, float* output, IDebugMultiCanvasHost debugMultiCanvasHost = null) {
         var k = 0;
         var v = stackalloc int[gridLength]; v[0] = 0;
         var z = stackalloc float[gridLength + 1]; z[0] = float.NegativeInfinity; z[1] = float.PositiveInfinity;
#if DEBUG_VIZ
         if (debugMultiCanvasHost != null) EDT1Viz(debugMultiCanvasHost, gridLength, input, output, -1, v, z);
#endif
         var q = 0;
         for (; q < gridLength && input[q] == float.PositiveInfinity; q++) {
#if DEBUG_VIZ
            if (debugMultiCanvasHost != null) EDT1Viz(debugMultiCanvasHost, gridLength, input, output, k, v, z, 0);
#endif
         }
         v[k] = q;
#if DEBUG_VIZ
         if (debugMultiCanvasHost != null) EDT1Viz(debugMultiCanvasHost, gridLength, input, output, k, v, z, 0);
#endif
         if (q == gridLength) {
            for (var i = 0; i < gridLength; i++) output[i] = float.PositiveInfinity;
            return;
         }

         q++;

         while (q < gridLength) {
            var iq = input[q];
            if (iq == float.PositiveInfinity) {
#if DEBUG_VIZ
               if (debugMultiCanvasHost != null) EDT1Viz(debugMultiCanvasHost, gridLength, input, output, k, v, z, q);
#endif
               q++;
               continue;
            }

            var vk = v[k];
            var ivk = input[vk];
            var s = ((iq + q * q) - (ivk + vk * vk)) / (2 * (q - vk));
            // Assert.IsTrue(!float.IsNaN(s) && !float.IsInfinity(s));

            if (s <= z[k]) {
               // Assert.IsTrue(k != 0);
#if DEBUG_VIZ
               if (debugMultiCanvasHost != null) EDT1Viz(debugMultiCanvasHost, gridLength, input, output, k, v, z, q);
               z[k] = float.PositiveInfinity; // for debug viz
               z[k + 1] = float.NaN; // for debug viz
#endif
               k--;
            } else {
#if DEBUG_VIZ
               if (debugMultiCanvasHost != null) EDT1Viz(debugMultiCanvasHost, gridLength, input, output, k, v, z, q);
#endif
               k++;
               v[k] = q;
               z[k] = s;
#if DEBUG_VIZ
               z[k + 1] = float.PositiveInfinity;
#endif
               q++;
            }
         }
         z[k + 1] = float.PositiveInfinity;
#if DEBUG_VIZ
         if (debugMultiCanvasHost != null) EDT1Viz(debugMultiCanvasHost, gridLength, input, output, k, v, z, gridLength - 1);
         if (debugMultiCanvasHost != null) EDT1Viz(debugMultiCanvasHost, gridLength, input, output, k, v, z, -1);
#endif
         k = 0;
         q = 0;

         while (q < gridLength) {
            while (z[k + 1] < q) k++;
            var vk = v[k];
            var qmvk = (q - vk);
            output[q] = qmvk * qmvk + input[vk];
            q++;
         }
      }

      private static unsafe void EDT1Viz(IDebugMultiCanvasHost debugMultiCanvasHost, int gridLength, float* input, float* output, int k, int* v, float* z, int? sweeplineIndex = null) {
         var canvas = debugMultiCanvasHost.CreateAndAddCanvas(frameCounter++);
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