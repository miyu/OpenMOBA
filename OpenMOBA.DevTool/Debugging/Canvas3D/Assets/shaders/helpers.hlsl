#include "Registers.hlsl"

SamplerState DiffuseSampler {
   Filter = MIN_MAG_MIP_LINEAR;
   AddressU = Wrap;
   AddressV = Wrap;
};

ShadowMapSampleResult TestShadowMap(float4 objectWorld, Texture2DArray shadowMap, ShadowMapEntry entry)
{
    ShadowMapSampleResult result;

    float4 lightPosition = mul(entry.projView, objectWorld);
    lightPosition.xyz /= lightPosition.w;
    
    if (lightPosition.x < -1.0f || lightPosition.x > 1.0f ||
      lightPosition.y < -1.0f || lightPosition.y > 1.0f ||
      lightPosition.z < 0.0f || lightPosition.z > 1.0f)
    {
        result.isIlluminated = false;
        return result;
    }
   
    // now in clip space (-1:1), ensure within unit circle
    float r = lightPosition.x * lightPosition.x + lightPosition.y * lightPosition.y;
    if (r > 1)
    {
        result.isIlluminated = false;
        return result;
    }

    //transform clip space coords to texture space coords (-1:1 to 0:1)
    lightPosition.x = lightPosition.x / 2 + 0.5;
    lightPosition.y = lightPosition.y / -2 + 0.5;

    //transform from texture coords to where it is in atlas
    float3 sampleLocation = entry.location.position + float3(lightPosition.xy * entry.location.size, 0);
    float shadowMapDepth = shadowMap.Sample(DiffuseSampler, sampleLocation).r;
    shadowMapDepth += 0.00001; // depth bias

    //if clip space z value greater than shadow map value then pixel is in shadow
    if (shadowMapDepth < lightPosition.z)
    {
        result.isIlluminated = false;
        return result;
    }
    
    result.isIlluminated = true;
    return result;
}
