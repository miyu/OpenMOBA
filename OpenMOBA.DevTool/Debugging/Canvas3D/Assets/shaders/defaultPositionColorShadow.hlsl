#include "helpers.hlsl"

struct PSInput {
   float4 objectPosition : TEXCOORD1;
   float4 transformedPosition : SV_Position;
   float4 color : COLOR;
};

cbuffer obj : register(b0) {
   float4x4 oprojView;
   float4x4 lprojView;
   float4x4 world;
}
Texture2DArray shadowMaps : register(REG_SHADOW_MAPS);
StructuredBuffer<ShadowMapEntry> shadowMapEntries : register(REG_SHADOW_MAPS_ENTRIES);

PSInput VSMain(float4 position : POSITION, float4 color : COLOR) {
    PSInput result;

    result.objectPosition = position;
    result.transformedPosition = mul(mul(oprojView, world), position);
    result.color = color;

    return result;
}

float4 PSMain(PSInput input) : SV_TARGET
{
    // ShadowMapEntry entry;
    // entry.location.position = float3(0, 0, 0);
    // entry.location.size = float2(1, 1);
    // entry.projViewWorld = lprojViewWorld;
    // entry.color = input.color;
    //uint nums, str;
    //shadowMapEntries.GetDimensions(nums, str);
    ShadowMapEntry entry = shadowMapEntries[0];
    //return entry.color;
    // return shadowMapEntries[0].projView._11_12_13_14 == lprojView._11_12_13_14 ? 1 : 0;

    ShadowMapSampleResult result = TestShadowMap(mul(world, input.objectPosition), shadowMaps, entry);
    float v = result.isIlluminated ? 1.0f : 0.2f;
    return input.color * v;
}