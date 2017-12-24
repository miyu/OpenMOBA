#include "helpers/lighting.hlsl"
#include "helpers/pbr.hlsl"

struct PSInput {
   float4 positionObject : POSITION1;
   float4 positionWorld : POSITION2;
   float4 position : SV_Position;
   float4 normalObject : NORMAL1;
   float4 normalWorld : NORMAL2;
   float4 normal : NORMAL3;
   float4 color : COLOR;
   float2 uv : TEXCOORD;
};

PSInput VSMain(
   float3 position : POSITION, 
   float3 normal : NORMAL, 
   float4 color : COLOR, 
   float2 uv : TEXCOORD,
   float4x4 world : INSTANCE_TRANSFORM
) {
   PSInput result;

   result.positionObject = float4(position, 1);
   result.positionWorld = mul(world, float4(position, 1));
   result.position = mul(mul(projView, world), float4(position, 1));
   result.normalObject = float4(normal, 1);
   result.normalWorld = normalize(mul(world, float4(normal, 0)));
   result.normal = normalize(mul(mul(projView, world), float4(normal, 0)));
   result.color = color;
   result.uv = uv;

   return result;
}

float4 PSMain(PSInput input) : SV_TARGET {
   uint numStructs, structStride;
   SpotlightDescriptions.GetDimensions(numStructs, structStride);

   float metallic = 1.0f;// 0.4f;
   float roughness = 0.15f; // 1.0f; // 0.2f;// 0.8f;

   // raw material color (albedo)
   float4 base = input.color * SampleDiffuseMap(input.uv, input.positionObject.xyz, input.normalObject.xyz);

   // metallic doesn't diffuse.
   float4 specular = lerp(base, float4(0.1, 0.1, 0.1, base.w), metallic);

   //float4 colorAccumulator = float4(base.xyz * 0.2f, base.w);
   /**/
   float4 colorAccumulator = float4(0, 0, 0, base.w);
   for (uint i = 0; i != numSpotlights; i++) {
      float lf = computeSpotlightLighting(input.positionWorld, input.normalWorld, ShadowMaps, SpotlightDescriptions[i]);
      lf *= 2.0f;
      // colorAccumulator += diffuse * float4(lighting, 0);
      float3 so = SpotlightDescriptions[i].origin;
      float3 sd = SpotlightDescriptions[i].direction;
      float3 N = normalize(input.normalWorld);
      float3 L = normalize(so - input.positionWorld);
      float3 V = normalize(cameraEye - input.positionWorld);
      float3 H = normalize(L + V);
      float vDotH = max(1E-5, dot(V, H)); // max avoids artifacts near 0
      float vDotN = max(1E-5, dot(V, N));
      float nDotH = max(1E-5, dot(N, H));
      float nDotL = max(1E-5, dot(N, L));
      float f0 = 0.2f;
      float ks = ctSpecularFactor(f0, vDotH);
      //float xx = saturate(ctSpecularFactor(f0, vDotH));
      //float xx = 1;
      //float xx = saturate(ctDistributionGGX(nDotH, roughness));
      //float xx = saturate(ctGeometryGGX(nDotL, vDotN, roughness));
      //float xx = saturate(nDotH);
      float ct = cookTorranceBrdf(nDotH, nDotL, vDotN, roughness, ks);
      float4 diffuseContribution = base * lambertianBrdf();
      float4 specularContribution = specular * cookTorranceBrdf(nDotH, nDotL, vDotN, roughness, ks);
      colorAccumulator += lf * float4(diffuseContribution.xyz * (1.0f - ks) + specularContribution.xyz * ks, 0.0f);
      //float4 cxz = lf * (base * (1.0f - ks) * (1.0f / PI) * nDotL + specular * ks * ct);
      //return cxz;
      //return float4(xx, xx, xx, 1);
      //colorAccumulator += lf * (specular * ks * ct);
      //colorAccumulator += lf * (base * (1.0f - ks) * (1.0f / PI) * nDotL + specular * ks * ct);
   }

   return colorAccumulator;
   /**/

   /*
   // Traditional diffuse lighting
   float4 diffuse = input.color * SampleDiffuseMap(input.uv, input.positionObject.xyz, input.normalObject.xyz);
   
   float4 colorAccumulator = float4(0, 0, 0, diffuse.w);
   if (!shadowTestEnabled) {
      colorAccumulator = diffuse;
   } else {
      for (uint i = 0; i != numSpotlights; i++) {
         float3 lighting = computeSpotlightLighting(input.positionWorld, input.normalWorld, ShadowMaps, SpotlightDescriptions[i]);
         colorAccumulator += diffuse * float4(lighting, 0);
      }
   }
   return colorAccumulator;
   /**/
}