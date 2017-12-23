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

   float metallic = 0.4f;
   float roughness = 0.8f;

   // raw material color (albedo)
   float4 base = input.color * SampleDiffuseMap(input.uv, input.positionObject.xyz, input.normalObject.xyz);

   // metallic doesn't diffuse.
   float4 specular = lerp(base, float4(0, 0, 0, base.w), metallic);

   //float4 colorAccumulator = float4(base.xyz * 0.2f, base.w);
   float4 colorAccumulator = float4(0, 0, 0, base.w);
   for (uint i = 0; i != numSpotlights; i++) {
      float lf = computeSpotlightLighting(input.positionWorld, input.normalWorld, ShadowMaps, SpotlightDescriptions[i]);
      // colorAccumulator += diffuse * float4(lighting, 0);
      float3 so = SpotlightDescriptions[i].origin;
      float3 sd = SpotlightDescriptions[i].direction;
      float N = input.normalWorld;
      float3 L = normalize(so - input.positionWorld);
      float3 V = normalize(cameraEye - input.positionWorld);
      float3 H = normalize(L + V);
      float vDotH = max(0.0f, dot(V, H));
      float vDotN = max(0.0f, dot(V, N));
      float nDotH = max(0.0f, dot(N, H));
      float nDotL = max(0.0f, dot(N, L));
      float f0 = 0.2f;
      float ks = ctSpecularFactor(f0, vDotH);
      float ct = cookTorranceBrdf(vDotH, vDotN, nDotH, nDotL, roughness, ks);
      colorAccumulator += lf * (specular * ks * ct);
      //colorAccumulator += lf * (base * (1.0f - ks) * (1.0f / PI) * nDotL + specular * ks * ct);
   }

   return colorAccumulator;


   // Traditional diffuse lighting
   // float4 diffuse = input.color * SampleDiffuseMap(input.uv, input.positionObject.xyz, input.normalObject.xyz);
   // 
   // float4 colorAccumulator = float4(0, 0, 0, diffuse.w);
   // if (!shadowTestEnabled) {
   //    colorAccumulator = diffuse;
   // } else {
   //    for (uint i = 0; i != numSpotlights; i++) {
   //       float3 lighting = computeSpotlightLighting(input.positionWorld, input.normalWorld, ShadowMaps, SpotlightDescriptions[i]);
   //       colorAccumulator += diffuse * float4(lighting, 0);
   //    }
   // }
   //
   // return colorAccumulator;
}