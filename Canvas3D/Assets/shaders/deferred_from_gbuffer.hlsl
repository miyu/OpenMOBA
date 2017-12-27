#include "helpers/lighting.hlsl"
#include "helpers/pbr.hlsl"
#include "common.hlsl"

Texture2D BaseColorMap : register(t0);
Texture2D NormalMaterialMap : register(t1);
Texture2D DepthMap : register(t2);

struct PSInput {
   float4 position : SV_Position;
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
   
   result.position = positionWorld; // mul(projView, positionWorld);
   result.uv = uv;

   return result;
}

float3 computeWorldFromUvAndZ(float2 uv, float z) {
   //float2 xy = input.uv * 2 - float2(1.0f, 1.0f);
   //xy.y *= -1;
   float x = uv.x * 2.0f - 1.0f;
   float y = uv.y * -2.0f + 1.0f;
   float4 homogeneous = float4(x, y, z, 1.0f);
   float4 P = mul(projViewInv, homogeneous);
   return P.xyz / P.w;
}

float4 PSMain(PSInput input) : SV_TARGET {
   float depth = DepthMap.Sample(PointSampler, input.uv).x - 0.0005f;
   float3 P = computeWorldFromUvAndZ(input.uv, depth);

   float3 base = BaseColorMap.Sample(PointSampler, input.uv).xyz;
   float transparency = 1.0f;
   float4 normalAndMaterial = NormalMaterialMap.Sample(PointSampler, input.uv);
   float3 N = (normalAndMaterial.xyz * 2) - 1;
   
   float metallic, roughness;
   //pbrDeferredUnpackMaterial(normalAndMaterial.w, metallic, roughness);
   pbrMaterialProperties(P, metallic, roughness);
   //if (length(P - float3(0, 0.5f, 0)) <= 0.73f) {
   //   //N = normalize(P - float3(0, 0.5f, 0));
   //}
   //return float4(metallic, roughness, 0, 1);
   
   return depth > 0.999f ? float4(0.2, 0.2, 0.2, 1) : commonComputeFragmentOutput(P, N, base, transparency, metallic, roughness);
}
