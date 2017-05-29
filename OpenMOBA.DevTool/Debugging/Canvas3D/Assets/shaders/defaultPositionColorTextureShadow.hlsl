#include "helpers.hlsl"

struct PSInput {
   float4 objectPosition : TEXCOORD1;
   float4 transformedPosition : SV_Position;
   float4 color : COLOR;
   float2 uv : TEXCOORD;
};

Texture2D diffuseMap : register(t0);

PSInput VSMain(float4 position : POSITION, float4 color : COLOR, float2 uv : TEXCOORD) {
    PSInput result;

    result.objectPosition = position;
    result.transformedPosition = mul(mul(projView, world), position);
    result.color = color;
    result.uv = uv;

    return result;
}

float4 PSMain(PSInput input) : SV_TARGET
{
    uint numStructs, structStride;
    shadowMapEntries.GetDimensions(numStructs, structStride);

    float illum = 0.2f;
    for (uint i = 0; i != 2; i++)
    {
        ShadowMapSampleResult result = TestShadowMap(mul(world, input.objectPosition), shadowMaps, shadowMapEntries[i]);
        if (result.isIlluminated)
        {
            illum += 0.4f;
        }
    }
    //float illum = numStructs <= 60;  //== 256 ? 1.0f : 0.0f;
    //float illum = shadowMapEntries[1].location.size.x == 1.0f ? 1.0f : 0.0f;
    return input.color * illum * diffuseMap.Sample(DiffuseSampler, input.uv);
}