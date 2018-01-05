#ifndef __INPUT_TYPEDEFS_HLSL__
#define __INPUT_TYPEDEFS_HLSL__

struct AtlasLocation {
   float3 position;
   float2 size;
};

struct SpotlightDescription {
   float3 origin;
   float3 direction;

   float4 color;
   float distanceAttenuationConstant;
   float distanceAttenuationLinear;
   float distanceAttenuationQuadratic;
   float spotlightAttenuationPower;

   float4x4 projView;
   AtlasLocation shadowMapLocation;
};

struct MaterialResourceDescription {
   int baseTextureIndex;
   int padding0, padding1, padding2;
   float4 baseColor;
};

#endif // __INPUT_TYPEDEFS_HLSL__
