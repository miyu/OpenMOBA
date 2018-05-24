using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using static SharpSL.SharpSLStatics;

namespace SharpSL {
   public static class SkyFromAtmosphere {
      public static readonly PixelShader Pixel = new PixelShader();

      public struct PixelInput {
         public Vector2 UV;

         public Matrix4x4 CameraProjViewInv;
         public AtmosphereConfiguration AtmosphereConfiguration;
      }

      public class PixelShader : Shader<PixelInput, Vector3> {
         public override Vector3 Compute(PixelInput input) {
            var rayDirection = ComputeCameraRayDirection(input.UV, input.CameraProjViewInv);
            return 0.2f * AtmosphericScatteringNaive.Compute(rayDirection, input.AtmosphereConfiguration);
         }

         public Shader<Vector2, Vector3> Configure(Matrix4x4 projViewInv, AtmosphereConfiguration atmosphereConfiguration) => 
            ProxyIn<Vector2>(uv => new PixelInput {
               UV = uv,
               CameraProjViewInv = projViewInv,
               AtmosphereConfiguration = atmosphereConfiguration
            });
      }
   }
}
