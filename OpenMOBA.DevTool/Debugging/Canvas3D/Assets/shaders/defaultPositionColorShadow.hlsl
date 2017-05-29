#include "helpers.hlsl"

struct PSInput {
   float4 objectPosition : TEXCOORD1;
   float4 transformedPosition : SV_Position;
   float4 color : COLOR;
};

PSInput VSMain(float4 position : POSITION, float4 color : COLOR) {
    PSInput result;

    result.objectPosition = position;
    result.transformedPosition = mul(mul(projView, world), position);
    result.color = color;

    return result;
}

float4 PSMain(PSInput input) : SV_TARGET
{
    ShadowMapEntry entry = shadowMapEntries[0];
    ShadowMapSampleResult result = TestShadowMap(mul(world, input.objectPosition), shadowMaps, entry);
    float v = result.isIlluminated ? 1.0f : 0.2f;
    //float x = shadowMaps.Sample(DiffuseSampler, float3(0.5f, 0.5f, 0.0f));
    //float v = x == 0.0f ? 1.0f : 0.0f; //entry.location.position.z == 0 ? 1.0f : 0.0f;
    return input.color * v;
}