using System;
using System.Drawing;
using System.Numerics;

namespace SharpSL {
   using static SharpSLStatics;
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

      public static Matrix4x4 CreateLookatProjView(Vector3 cameraPosition, Vector3 cameraLookat, Size renderTargetSize, float vFov = 40.0f) {
         return default(Matrix4x4);
      }

      public static void Fill<T>(RenderTarget<T> rt, Shader<Vector2, T> pixel) {
         throw new NotImplementedException();
      }
   }

   public static class Program {
      public static void Main(string[] args) {
         var rt = CreateRenderTarget<Vector3>(160, 120);
         var projView = CreateLookatProjView(Vector3.Zero, Vector3.UnitX, rt.Size);
//         var projViewInv = MatrixCM.Invert(projView);

//         Fill(rt, SkyFromAtmosphere.Pixel.Configure(projViewInv, AtmosphereConfiguration.Earth));
      }
   }
}
