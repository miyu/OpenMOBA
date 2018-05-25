using System.Numerics;
using FMatrix;

namespace SharpSL {
   using static NumericsStatics;
   using static SoftwareRenderer;

   public static class Program {
      public static void Main(string[] args) {
         var rt = CreateRenderTarget<Vector3>(640, 480);
         var projView = CreateLookatProjView(Vector3.Zero, Vec3(0.8f, 0.7f, 1), Vector3.UnitY, rt.Size, 1.5f);

         var atmosphereConfiguration = AtmosphereConfiguration.Earth;
         atmosphereConfiguration.SunDirectionUnit = Vec3(3.5f, 2.5f, 1).Normalize();

         Fill(rt, SkyFromAtmosphere.Pixel.Configure(projView.InvertOrThrow(), atmosphereConfiguration));
         SaveImage(rt, "out.png");
      }
   }
}