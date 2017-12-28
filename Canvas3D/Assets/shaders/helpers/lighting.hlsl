#ifndef __LIGHTING_HLSL__
#define __LIGHTING_HLSL__

#include "registers.hlsl"

float3 computeSpotlightLighting(float3 objectWorld, float4 normalWorld, Texture2DArray shadowMap, SpotlightDescription spotlight);
bool testShadowMap(float3 objectWorld, Texture2DArray shadowMap, float4x4 projView, AtlasLocation shadowMapLocation);

float3 computeSpotlightLighting(float3 objectWorld, float3 normalWorld, Texture2DArray shadowMap, SpotlightDescription spotlight) {
   float shadowing = float(testShadowMap(objectWorld, shadowMap, spotlight.projView, spotlight.shadowMapLocation));
   float d = distance(objectWorld, spotlight.origin);
   float distanceAttenuation = clamp(1.0f / (spotlight.distanceAttenuationConstant + d * spotlight.distanceAttenuationLinear + d * d * spotlight.distanceAttenuationQuadratic), 0.0f, 1.0f);
   float3 spotlightToObjectWorld = normalize(objectWorld - spotlight.origin);
   float dawt = dot(spotlightToObjectWorld, spotlight.direction);
   //float dawt = distance(objectWorld.xyz, float3(0, 0, 0)) / 10; //abs(dot(normalize(objectWorld.xyz), float3(0, -1, 0)));;
   float spotlightAttenuation = pow(max(dawt, 0), spotlight.spotlightAttenuationPower);
   float diffuseFactor = max(0, dot(-spotlight.direction, normalize(normalWorld)));
   return diffuseFactor * shadowing * spotlight.color.xyz * distanceAttenuation * spotlight.color.w * spotlightAttenuation;
} 

// Todo: Branching here is probably real bad
bool testShadowMap(float3 objectWorld, Texture2DArray shadowMap, float4x4 projView, AtlasLocation shadowMapLocation) {
    float4 lightPosition = mul(projView, float4(objectWorld, 1.0f));
    lightPosition.xyz /= lightPosition.w;
    
    if (lightPosition.x < -1.0f || lightPosition.x > 1.0f ||
      lightPosition.y < -1.0f || lightPosition.y > 1.0f ||
      lightPosition.z < 0.0f || lightPosition.z > 1.0f)
    {
        return false;
    }
   
    // now in clip space (-1:1), ensure within unit circle
    float r = lightPosition.x * lightPosition.x + lightPosition.y * lightPosition.y;
    if (r > 1)
    {
        return false;
    }

    //transform clip space coords to texture space coords (-1:1 to 0:1)
    lightPosition.x = lightPosition.x / 2 + 0.5;
    lightPosition.y = lightPosition.y / -2 + 0.5;

    //transform from texture coords to where it is in atlas
    float3 sampleLocation = shadowMapLocation.position + float3(lightPosition.xy * shadowMapLocation.size, 0);
    float shadowMapDepth = shadowMap.Sample(LinearSampler, sampleLocation).r;
    shadowMapDepth += 0.00003; // depth bias

    //if clip space z value greater than shadow map value then pixel is in shadow
    if (shadowMapDepth < lightPosition.z)
    {
        return false;
    }
    
    return true;
}

#endif // __LIGHTING_HLSL__
