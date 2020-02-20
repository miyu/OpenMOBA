using System.Numerics;
using FMatrix;

namespace SharpSL {
   using static NumericsStatics;
   using static SoftwareRenderer;

   public static class Program {
      public static void Main(string[] args) {
         var rt = CreateBuffer2D<Vector3>(1080, 1080);
         var projView = CreateLookatProjView(Vector3.Zero, Vec3(0.8f, 0.7f, 1), Vector3.UnitY, rt.Size, 1.5f);

         // var atmosphereConfiguration = AtmosphereConfiguration.Earth;
         // atmosphereConfiguration.SunDirectionUnit = Vec3(3.5f, 2.5f, 1).Normalize();
         // Fill(rt, SkyFromAtmosphere.Pixel.Configure(projView.InvertOrThrow(), atmosphereConfiguration));

         for (var i = 0; i < 1000; i++) {
            Fill(rt, Terrain.Pixel.Configure(projView.InvertOrThrow(), 1337 + i / 100.0f));
            SaveImage(rt, i+".png");
         }
      }
   }
}