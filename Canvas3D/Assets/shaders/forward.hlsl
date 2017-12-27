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
};

PSInput VSMain(
   float3 position : POSITION, 
   float3 normal : NORMAL, 
   float4 color : COLOR, 
   float2 uv : TEXCOORD,
   float4x4 world : INSTANCE_TRANSFORM
) {
   PSInput result;

   float4x4 batchWorld = mul(batchTransform, world);
   float4 positionWorld = mul(batchWorld, float4(position, 1));
   float4 normalWorld = mul(batchWorld, float4(normal, 0));

   result.positionObject = position;
   result.positionWorld = positionWorld.xyz;
   result.position = mul(projView, positionWorld);
   result.normalObject = normal;
   result.normalWorld = normalize(normalWorld.xyz); // must normalize in PS
   result.color = color;
   result.uv = uv;

   return result;
}

float4 PSMain(PSInput input) : SV_TARGET {
   float3 P = input.positionWorld;
   float3 N = normalize(input.normalWorld);
   
   float4 baseAndTransparency = input.color * SampleDiffuseMap(input.uv, input.positionObject.xyz, input.normalObject.xyz);
   float3 base = baseAndTransparency.xyz;
   float transparency = baseAndTransparency.w;
   
   float metallic, roughness;
   pbrMaterialProperties(input.positionWorld, metallic, roughness);
   
   return commonComputeFragmentOutput(P, N, base, transparency, metallic, roughness);
}

#endif // __FORWARD_HLSL__
