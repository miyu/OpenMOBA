#include "types.hlsl"

struct PSInput
{
    float4 position : SV_POSITION;
    float4 lightPosition : TEXCOORD0;
    float4 color : COLOR;
};

cbuffer obj : register(b0)
{
    float4x4 oprojViewWorld;
    float4x4 lprojViewWorld;
}

StructuredBuffer<PointLight> lights : register(t1);

// Texture2D light1ShadowMap;

SamplerState DiffuseSampler
{
    Filter = MIN_MAG_MIP_LINEAR;
    AddressU = Wrap;
    AddressV = Wrap;
};

PSInput VSMain(float4 position : POSITION, float4 color : COLOR)
{
    PSInput result;

    result.position = mul(oprojViewWorld, position);
    result.lightPosition = mul(lprojViewWorld, position);
    result.color = color;

    return result;
}

float4 PSMain(PSInput input) : SV_TARGET
{
    input.lightPosition.xyz /= input.lightPosition.w;
    if (input.lightPosition.x < -1.0f || input.lightPosition.x > 1.0f ||
      input.lightPosition.y < -1.0f || input.lightPosition.y > 1.0f ||
      input.lightPosition.z < 0.0f || input.lightPosition.z > 1.0f)
    {
        return input.color / 5.0f;
    }

   // now in clip space (-1:1), ensure within unit circle
    float r = input.lightPosition.x * input.lightPosition.x + input.lightPosition.y * input.lightPosition.y;
    if (r > 1)
        return input.color / 5.0f;

   //transform clip space coords to texture space coords (-1:1 to 0:1)
    input.lightPosition.x = input.lightPosition.x / 2 + 0.5;
    input.lightPosition.y = input.lightPosition.y / -2 + 0.5;

   //sample shadow map - point sampler
    float shadowMapDepth = light1ShadowMap.Sample(DiffuseSampler, input.lightPosition.xy).r;
    shadowMapDepth += 0.0001; // depth bias

   //if clip space z value greater than shadow map value then pixel is in shadow
    if (shadowMapDepth < input.lightPosition.z)
        return input.color / 5.0f;
   // return input.lightPosition.z;
    return input.color;

    return float4(1.0f, 1.0f, 0.0f, 1.0f);
}