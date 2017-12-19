#include "helpers/lighting.hlsl"

struct PSInput {
   float4 positionObject : POSITION1;
   float4 positionWorld : POSITION2;
   float4 position : SV_Position;
   float4 normal : NORMAL;
   float4 color : COLOR;
   float2 uv : TEXCOORD;
};

Texture2D diffuseMap;

PSInput VSMain(
   float3 position : POSITION, 
   float3 normal : NORMAL, 
   float4 color : COLOR, 
   float2 uv : TEXCOORD
) {
   PSInput result;

   result.positionObject = float4(position, 1);
   result.positionWorld = mul(world, float4(position, 1));
   result.position = mul(mul(projView, world), float4(position, 1));
   result.normal = mul(mul(projView, world), float4(normal, 0));
   result.color = color;
   result.uv = uv;

   return result;
}

float4 PSMain(PSInput input) : SV_TARGET {
   uint numStructs, structStride;
   SpotlightDescriptions.GetDimensions(numStructs, structStride);

   float4 diffuse = input.color * diffuseMap.Sample(DiffuseSampler, input.uv);

   float4 colorAccumulator = float4(0, 0, 0, diffuse.w);
   if (!shadowTestEnabled) {
      colorAccumulator = diffuse;
   } else {
      for (uint i = 0; i != 1; i++) {
         float3 lighting = computeSpotlightLighting(input.positionWorld, ShadowMaps, SpotlightDescriptions[i]);
         colorAccumulator += float4(lighting, 0);
      }
   }

   return colorAccumulator;
}