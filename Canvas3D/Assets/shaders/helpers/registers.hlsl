#ifndef __REGISTERS_HLSL__
#define __REGISTERS_HLSL__

#include "input_typedefs.hlsl"
#include "material_pack.hlsl"

#define REG_SCENE_DATA b0
#define REG_OBJECT_DATA b1
#define REG_TEXTURE_DATA b2

#define REG_SHADOW_MAPS t10
#define REG_SPOTLIGHT_DESCRIPTIONS t11
#define REG_MATERIAL_RESOURCE_DESCRIPTIONS t12

cbuffer Scene : register(REG_SCENE_DATA) {
   float4 cameraEye;
   float4x4 projView;
   float4x4 projViewInv;
   int pbrEnabled;
   int shadowTestEnabled;
   int numSpotlights;
}

cbuffer Batch : register(REG_OBJECT_DATA) {
   float4x4 batchTransform;
   int diffuseSamplingMode;
   int batchMaterialResourcesIndexOverride;
}

#define FOREACH_BODY(SLOT_NUM) \
   Texture2D TEX_##SLOT_NUM##_2D : register(t##SLOT_NUM);

#include "foreach_texture_2d.hlsl"

#define FOREACH_BODY(SLOT_NUM) \
   TextureCube<float4> TEX_##SLOT_NUM##_CUBE : register(t##SLOT_NUM);

#include "foreach_texture_cube.hlsl"

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
StructuredBuffer<SpotlightDescription> SpotlightDescriptions : register(REG_SPOTLIGHT_DESCRIPTIONS);
StructuredBuffer<MaterialResourceDescription> MaterialResourceDescriptions : register(REG_MATERIAL_RESOURCE_DESCRIPTIONS);

bool SampleDiffuseMapSecondDerivative(const Texture2D tex, float2 uv);

float4 SampleTextureInternal_2D(const Texture2D tex, const float2 uv) {
   float4 samp = tex.Sample(LinearSampler, uv);
   [forcecase] switch (diffuseSamplingMode) {
      case 0:
         return samp;
      case 10: {
         float c = samp.x;
         return float4(c, c, c, 1);
      }
      //case 11: {
      //   float c = float(SampleDiffuseMapSecondDerivative(tex, uv));
      //   return float4(c, c, c, 1);
      //}
      case 12:
         return float4(samp.xyz, 1.0f);
      case 13: {
         float material = samp.w;
         float metallic, roughness;
         unpackMaterial(material, metallic, roughness);
         return float4(metallic, roughness, 0.0f, 1.0f);
      }
      default:
         return float4(1, 0, 1, 1);
   }
}

float4 SampleTextureInternal_Cube(const TextureCube<float4> tex, float3 dir, float3 normal) {
   [forcecase] switch (diffuseSamplingMode) {
   case 20:
      return tex.Sample(LinearSampler, dir);
   case 21:
      return tex.Sample(LinearSampler, normal);
   default:
      return float4(1, 0, 1, 1);
   }
}

float4 SampleTextureInternal(int slot, float2 uv, float3 dir, float3 normal) {
   //return SampleTextureInternal_2D(TEX_48_2D, uv);
   //return SampleTextureInternal_Cube(TEX_88_CUBE, dir, normal);
   return 1;

   [branch] if (slot == -1) {
      return float4(1, 1, 1, 1);
   }

   [forcecase] switch (slot) {
      #define FOREACH_BODY(SLOT_NUM) \
         case SLOT_NUM : \
            return SampleTextureInternal_2D(TEX_##SLOT_NUM##_2D, uv);\

      #include "foreach_texture_2d.hlsl"
      
      #define FOREACH_BODY(SLOT_NUM) \
         case SLOT_NUM : \
            return SampleTextureInternal_Cube(TEX_##SLOT_NUM##_CUBE, dir, normal);\

      #include "foreach_texture_cube.hlsl"

      default:
         return float4(0, 1, 1, 1);
   }
}

#define SampleTexture(slot, input) \
   SampleTextureInternal(slot, input##.uv, input##.positionObject.xyz, input##.normalObject.xyz)

// derivatives are now binary l0l
bool SampleDiffuseMapSecondDerivative(const Texture2D tex, float2 uv) {
   float w, h;
   tex.GetDimensions(w, h);

   float2 ox = float2(1.0f / w, 0.0f);
   float2 oy = float2(0.0f, 1.0f / h);
   float center = tex.Sample(LinearSampler, uv).x;
   float d = -tex.Sample(LinearSampler, uv - 2 * ox - 2 * oy).x
      - 2 * tex.Sample(LinearSampler, uv - 1 * ox - 2 * oy).x
      - 2 * tex.Sample(LinearSampler, uv - 2 * ox - 1 * oy).x
      - 4 * tex.Sample(LinearSampler, uv - 1 * ox - 1 * oy).x
      - 1 * tex.Sample(LinearSampler, uv + 2 * ox + 2 * oy).x
      - 2 * tex.Sample(LinearSampler, uv + 1 * ox + 2 * oy).x
      - 2 * tex.Sample(LinearSampler, uv + 2 * ox + 1 * oy).x
      - 4 * tex.Sample(LinearSampler, uv + 1 * ox + 1 * oy).x
      - 1 * tex.Sample(LinearSampler, uv - 2 * ox + 2 * oy).x
      - 2 * tex.Sample(LinearSampler, uv - 1 * ox + 2 * oy).x
      - 2 * tex.Sample(LinearSampler, uv - 2 * ox + 1 * oy).x
      - 4 * tex.Sample(LinearSampler, uv - 1 * ox + 1 * oy).x
      - 1 * tex.Sample(LinearSampler, uv + 2 * ox - 2 * oy).x
      - 2 * tex.Sample(LinearSampler, uv + 1 * ox - 2 * oy).x
      - 2 * tex.Sample(LinearSampler, uv + 2 * ox - 1 * oy).x
      - 4 * tex.Sample(LinearSampler, uv + 1 * ox - 1 * oy).x
      + center * 36;
   return abs(d) > 1E-3f;
}

#endif // __REGISTERS_HLSL__
