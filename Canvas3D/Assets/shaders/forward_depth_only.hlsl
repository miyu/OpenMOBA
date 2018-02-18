#include "helpers/registers.hlsl"

struct PSInput {
   float4 position : SV_Position;
};

PSInput VSMain(
   float3 position : POSITION,
   float3 normal : NORMAL,
   float4 vertexColor : VERTEX_COLOR,
   float2 uv : TEXCOORD,
   float4x4 world : INSTANCE_TRANSFORM,
   float metallic : INSTANCE_METALLIC,
   float roughness : INSTANCE_ROUGHNESS,
   int materialResourcesIndex : INSTANCE_MATERIAL_RESOURCES_INDEX,
   float4 instanceColor : INSTANCE_COLOR
) {
   PSInput result;

   //float4x4 world = float4x4(world_1, world_2, world_3, world_4);
   result.position = mul(mul(projView, mul(batchTransform, world)), float4(position, 1));

   return result;
}

float4 PSMain(PSInput input) : SV_TARGET {
    return 0;
}