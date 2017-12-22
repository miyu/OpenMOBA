#include "input_typedefs.hlsl"

#define REG_SCENE_DATA b0
#define REG_OBJECT_DATA b1

#define REG_DIFFUSE_MAP t0
#define REG_DIFFUSE_CUBE_MAP t1
#define REG_SHADOW_MAPS t10
#define REG_SHADOW_MAPS_ENTRIES t11

cbuffer Scene : register(REG_SCENE_DATA) {
   float4x4 projView;
   bool shadowTestEnabled;
   int numSpotlights;
}

cbuffer Object : register(REG_OBJECT_DATA) {
   int diffuseSamplingMode;
}

Texture2D DiffuseMap : register(REG_DIFFUSE_MAP);
TextureCube<float4> DiffuseCubeMap : register(REG_DIFFUSE_CUBE_MAP);

SamplerState PointSampler {
   Filter = MIN_MAG_MIP_POINT;
   AddressU = Wrap;
   AddressV = Wrap;
};

SamplerState LinearSampler {
   Filter = MIN_MAG_MIP_LINEAR;
   AddressU = Wrap;
   AddressV = Wrap;
};

Texture2DArray ShadowMaps : register(REG_SHADOW_MAPS);
StructuredBuffer<SpotlightDescription> SpotlightDescriptions : register(REG_SHADOW_MAPS_ENTRIES);

float4 SampleDiffuseMap(float2 texuv, float3 dir, float3 normal) {
   if (diffuseSamplingMode == 0) {
      return DiffuseMap.Sample(LinearSampler, texuv);
   } else if (diffuseSamplingMode == 1) {
      float c = DiffuseMap.Sample(LinearSampler, texuv).x;
      return float4(c, c, c, 1);
   } else if (diffuseSamplingMode == 2) {
      return DiffuseCubeMap.Sample(LinearSampler, dir);
   } else if (diffuseSamplingMode == 3) {
      return DiffuseCubeMap.Sample(LinearSampler, normal);
   } else {
      return float4(1, 0, 1, 1);
   }
}
