using System.Numerics;
using FMatrix;

namespace SharpSL {
   using static NumericsStatics;
   using static SoftwareRenderer;

   public static class Program {
      public static void Main(string[] args) {
         var rt = CreateRenderTarget<Vector3>(640, 480);
         var projView = CreateLookatProjView(Vector3.Zero, Vec3(1, 0.5f, 1), Vector3.UnitY, rt.Size);

         var atmosphereConfiguration = AtmosphereConfiguration.Earth;
         atmosphereConfiguration.SunDirectionUnit = Vec3(1.5f, 0.7f, 1).Normalize();

         Fill(rt, SkyFromAtmosphere.Pixel.Configure(projView.InvertOrThrow(), atmosphereConfiguration));
         SaveImage(rt, "out.png");
      }
   }
}