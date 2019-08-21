using System;
using Dargon.Luna.Lang;

namespace Dargon.Luna.Demos {
   public class DiffuseShader : Shader {
      public struct VertexInput {
         [POSITION] public float4 vertex;
         [TEXCOORD] public float4 uv;
         [POSITION] public float4 normal;
         [COLOR] public float4 color;
      }

      public struct FragmentInput {
         [SV_POSITION] public float4 position;
         [NORMAL] public float4 normalWorld;
         [COLOR] public float4 color;
      }

      public FragmentInput Vert(VertexInput i) {
         FragmentInput o = default;
         o.position = ObjectToClipPosition(i.vertex);
         o.normalWorld = ObjectToWorldNormal(i.normal);
         o.color = i.color;
         return o;
      }

      public float4 Frag(FragmentInput i) {
         float3 n = normalize(i.normalWorld.xyz);
         ref var dl0 = ref Lighting.Directional[0];
         float lambert = dot(i.normalWorld.xyz, dl0.direction.xyz);
         return float4(lambert * (i.color * dl0.color).xyz, 1);
      }
   }

   public struct DirectionalLight {
      public float4 direction;
      public float4 color;
      public float intensity;
   }
}
