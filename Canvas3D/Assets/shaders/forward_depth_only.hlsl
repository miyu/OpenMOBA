#include "helpers/registers.hlsl"

struct PSInput {
   float4 position : SV_Position;
};

PSInput VSMain(
   float3 position : POSITION, 
   float3 normal : NORMAL, 
   float4 color : COLOR, 
   float2 uv : TEXCOORD
) {
   PSInput result;

   result.position = mul(mul(projView, world), float4(position, 1));

   return result;
}

float4 PSMain(PSInput input) : SV_TARGET {
    return float4(1, 1, 1, 1);
}