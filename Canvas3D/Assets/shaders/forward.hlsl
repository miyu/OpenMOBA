#ifndef __FORWARD_HLSL__
#define __FORWARD_HLSL__

#include "helpers/lighting.hlsl"
#include "helpers/pbr.hlsl"
#include "common.hlsl"

struct PSInput {
   float3 positionObject : POSITION1;
   float3 positionWorld : POSITION2;
   float4 position : SV_Position;
   float3 normalObject : NORMAL1;
   float3 normalWorld : NORMAL2;
   float4 color : COLOR;
   float2 uv : TEXCOORD;
   float metallic : MATERIAL_METALLIC;
   float roughness : MATERIAL_ROUGHNESS;
   int materialResourcesIndex : MATERIAL_INDEX;
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

   float4x4 batchWorld = mul(batchTransform, world);
   float4 positionWorld = mul(batchWorld, float4(position, 1));
   float4 normalWorld = mul(batchWorld, float4(normal, 0));

   result.positionObject = position;
   result.positionWorld = positionWorld.xyz;
   result.position = mul(cameraProjView, positionWorld);
   result.normalObject = normal;
   result.normalWorld = normalize(normalWorld.xyz); // must normalize in PS
   result.color = vertexColor * instanceColor;
   result.uv = uv;
   result.metallic = metallic;
   result.roughness = roughness;
   result.materialResourcesIndex = materialResourcesIndex;

   return result;
}

float4 PSMain(PSInput input) : SV_TARGET {
   float3 P = input.positionWorld;
   float3 N = normalize(input.normalWorld);
   
   int materialResourcesIndex = batchMaterialResourcesIndexOverride != -1 ? batchMaterialResourcesIndexOverride : input.materialResourcesIndex;
   MaterialResourceDescription materialResources = MaterialResourceDescriptions[materialResourcesIndex];
   float4 materialSampledColor = materialResources.baseColor * SampleTexture(materialResources.baseTextureIndex, input);
   float4 baseAndTransparency = input.color * materialSampledColor;
   float3 base = baseAndTransparency.xyz;
   return float4(base, 1.0);
   float transparency = baseAndTransparency.w;
   
   float metallic = input.metallic;
   float roughness = input.roughness;
   return commonComputeFragmentOutput(P, N, base, transparency, metallic, roughness);
}

#endif // __FORWARD_HLSL__
