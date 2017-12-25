#include "input_typedefs.hlsl"

#define REG_SCENE_DATA b0
#define REG_OBJECT_DATA b1

#define REG_DIFFUSE_MAP t0
#define REG_DIFFUSE_CUBE_MAP t1
#define REG_ENVIRONMENT_CUBE_MAP t8
#define REG_SHADOW_MAPS t10
#define REG_SHADOW_MAPS_ENTRIES t11

cbuffer Scene : register(REG_SCENE_DATA) {
   float4 cameraEye;
   float4x4 projView;
   int pbrEnabled;
   int shadowTestEnabled;
   int numSpotlights;
}

cbuffer Object : register(REG_OBJECT_DATA) {
   int diffuseSamplingMode;
}

Texture2D DiffuseMap : register(REG_DIFFUSE_MAP);
TextureCube<float4> DiffuseCubeMap : register(REG_DIFFUSE_CUBE_MAP);
TextureCube<float4> EnvironmentCubeMap : register(REG_ENVIRONMENT_CUBE_MAP);

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

bool SampleDiffuseMapSecondDerivative(Texture2D diffuseMap, float2 uv);

float4 SampleDiffuseMap(float2 texuv, float3 dir, float3 normal) {
   [branch] if (diffuseSamplingMode == 0) {
      return DiffuseMap.Sample(LinearSampler, texuv);
   } else [branch] if (diffuseSamplingMode == 10) {
      float c = DiffuseMap.Sample(LinearSampler, texuv).x;
      return float4(c, c, c, 1);
   } else [branch] if (diffuseSamplingMode == 11) {
      float c = float(SampleDiffuseMapSecondDerivative(DiffuseMap, texuv));
      return float4(c, c, c, 1);
   } else [branch] if (diffuseSamplingMode == 20) {
      return DiffuseCubeMap.Sample(LinearSampler, dir);
   } else [branch] if (diffuseSamplingMode == 21) {
      return DiffuseCubeMap.Sample(LinearSampler, normal);
   } else {
      return float4(1, 0, 1, 1);
   }
}

// derivatives are now binary l0l
bool SampleDiffuseMapSecondDerivative(Texture2D diffuseMap, float2 uv) {
   float w, h;
   diffuseMap.GetDimensions(w, h);

   float ox = float2(1.0f / w, 0.0f);
   float oy = float2(0.0f, 1.0f / h);
   float center = diffuseMap.Sample(LinearSampler, uv);
   float d = -diffuseMap.Sample(LinearSampler, uv - 2 * ox - 2 * oy)
      - 2 * diffuseMap.Sample(LinearSampler, uv - 1 * ox - 2 * oy)
      - 2 * diffuseMap.Sample(LinearSampler, uv - 2 * ox - 1 * oy)
      - 4 * diffuseMap.Sample(LinearSampler, uv - 1 * ox - 1 * oy)
      - 1 * diffuseMap.Sample(LinearSampler, uv + 2 * ox + 2 * oy)
      - 2 * diffuseMap.Sample(LinearSampler, uv + 1 * ox + 2 * oy)
      - 2 * diffuseMap.Sample(LinearSampler, uv + 2 * ox + 1 * oy)
      - 4 * diffuseMap.Sample(LinearSampler, uv + 1 * ox + 1 * oy)
      - 1 * diffuseMap.Sample(LinearSampler, uv - 2 * ox + 2 * oy)
      - 2 * diffuseMap.Sample(LinearSampler, uv - 1 * ox + 2 * oy)
      - 2 * diffuseMap.Sample(LinearSampler, uv - 2 * ox + 1 * oy)
      - 4 * diffuseMap.Sample(LinearSampler, uv - 1 * ox + 1 * oy)
      - 1 * diffuseMap.Sample(LinearSampler, uv + 2 * ox - 2 * oy)
      - 2 * diffuseMap.Sample(LinearSampler, uv + 1 * ox - 2 * oy)
      - 2 * diffuseMap.Sample(LinearSampler, uv + 2 * ox - 1 * oy)
      - 4 * diffuseMap.Sample(LinearSampler, uv + 1 * ox - 1 * oy)
      + center * 36;
   return abs(d) > 1E-3f;

//   float tl = diffuseMap.Sample(DiffuseSampler, input.uv - ox - oy);
//   float tc = diffuseMap.Sample(DiffuseSampler, input.uv - oy);
//   float tr = diffuseMap.Sample(DiffuseSampler, input.uv + ox - oy);
//   float cl = diffuseMap.Sample(DiffuseSampler, input.uv - ox);
//   float cc = diffuseMap.Sample(DiffuseSampler, input.uv);
//   float cr = diffuseMap.Sample(DiffuseSampler, input.uv + ox);
//   float bl = diffuseMap.Sample(DiffuseSampler, input.uv - ox + oy);
//   float bc = diffuseMap.Sample(DiffuseSampler, input.uv + oy);
//   float br = diffuseMap.Sample(DiffuseSampler, input.uv + ox + oy);
//
//   float score1 = abs(4 * cc - cl - cr - tc - bc);
//   float score2 = abs(tl + tr + bl + br - cl - cr - tc - bc);
//   return max(score1, score2) > 5.0E-6f;
}