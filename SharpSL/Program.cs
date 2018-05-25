using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using FMatrix;

namespace SharpSL {
   using static NumericsStatics;
   using static SoftwareRenderer;

   public class RenderTarget<T> {
      public T[] Data;
      public int Width;
      public int Height;

      public Size Size => new Size(Width, Height);
   }

   public static class SoftwareRenderer {
      public static RenderTarget<T> CreateRenderTarget<T>(int width, int height) => new RenderTarget<T> {
         Data = new T[width * height],
         Width = width,
         Height = height
      };

      public static FMatrix4x4 CreateLookatProjView(Vector3 cameraPosition, Vector3 cameraLookat, Vector3 up, Size renderTargetSize, float vFov = (float)Math.PI / 4, float znear = 5.0f, float zfar = 1000.0f) {
         var aspect = renderTargetSize.Width / (float)renderTargetSize.Height;
         return FMatrix4x4.PerspectiveFovRH(vFov, aspect, znear, zfar) * FMatrix4x4.ViewLookAtRH(cameraPosition, cameraLookat, up);
      }

      public static void Fill<T>(RenderTarget<T> rt, Shader<Vector2, T> pixel) {
         for (var y = 0; y < rt.Height; y++) {
            for (var x = 0; x < rt.Width; x++) {
               rt.Data[y * rt.Width + x] = pixel.Compute(new Vector2(x / (float)rt.Width, y / (float)rt.Height));
            }
         }
      }

      public static void SaveImage<T>(RenderTarget<T> rt, string path) {
         switch (rt) {
            case RenderTarget<Vector3> cb:
               int ToU8(float x) => Math.Min(255, Math.Max(0, (int)(x * 255)));
               SaveImageInternal(cb, c => Color.FromArgb(ToU8(c.X), ToU8(c.Y), ToU8(c.Z)), path);
               break;
            default:
               throw new NotSupportedException();
         }
      }

      private static unsafe void SaveImageInternal<T>(RenderTarget<T> rt, Func<T, Color> pixelMapper, string path) {
         var result = new Bitmap(rt.Width, rt.Height, PixelFormat.Format24bppRgb);
         var bitmapData = result.LockBits(new Rectangle(0, 0, rt.Width, rt.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
         var pScan0 = (int*)bitmapData.Scan0.ToPointer();
         var pRow = pScan0;
         var i = 0;
         for (var y = 0; y < rt.Height; y++) {
            var pCurrentPixel = pRow;
            for (var x = 0; x < rt.Width; x++) {
               var c = pixelMapper(rt.Data[i++]);
               *(pCurrentPixel++) = c.ToArgb();
            }
            pRow = (int*)((byte*)pRow + bitmapData.Stride);
         }
         result.UnlockBits(bitmapData);
         result.Save(path);
      }
   }

   public static class Program {
      public static void Main(string[] args) {
         var sw = new Stopwatch();
         sw.Start();

         var rt = CreateRenderTarget<Vector3>(640, 480);
         var projView = CreateLookatProjView(Vector3.Zero, new Vector3(1, 0.5f, 1), Vector3.UnitY, rt.Size);
         var projViewInv = projView.InvertOrThrow();
         var atmosphereConfiguration = AtmosphereConfiguration.Earth;
         atmosphereConfiguration.SunDirectionUnit = new Vector3(1.5f, 0.7f, 1).Normalize();
         Fill(rt, SkyFromAtmosphere.Pixel.Configure(projViewInv, atmosphereConfiguration));
         SaveImage(rt, "out.png");

         Console.WriteLine($"Done in {sw.ElapsedMilliseconds}ms");
      }
   }
}
