using System.Numerics;
using FMatrix;

namespace SharpSL {
   using static NumericsStatics;

   public static class SkyFromAtmosphere {
      public static readonly PixelShader Pixel = new PixelShader();

      public struct PixelInput {
         public Vector2 UV;

         public FMatrix4x4 CameraProjViewInv;
         public AtmosphereConfiguration AtmosphereConfiguration;
      }

      public class PixelShader : Shader<PixelInput, Vector3> {
         public override Vector3 Compute(PixelInput input) {
            var rayDirection = ComputeCameraRayDirection(input.UV, input.CameraProjViewInv);
            return 0.2f * AtmosphericScatteringNaive.Compute(rayDirection, input.AtmosphereConfiguration);
         }

         public Shader<Vector2, Vector3> Configure(FMatrix4x4 projViewInv, AtmosphereConfiguration atmosphereConfiguration) {
            return ProxyIn<Vector2>(uv => new PixelInput {
               UV = uv,
               CameraProjViewInv = projViewInv,
               AtmosphereConfiguration = atmosphereConfiguration
            });
         }
      }
   }
}