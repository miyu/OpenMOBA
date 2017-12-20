#include "input_typedefs.hlsl"

#define REG_SCENE_DATA b0
#define REG_OBJECT_DATA b1

#define REG_DIFFUSE_MAP t0
#define REG_SHADOW_MAPS t10
#define REG_SHADOW_MAPS_ENTRIES t11

cbuffer Scene : register(REG_SCENE_DATA) {
   float4x4 projView;
   bool shadowTestEnabled;
   int numSpotlights;
}

cbuffer Object : register(REG_OBJECT_DATA) {
   float4x4 world;
}

//Texture2D DiffuseMap : register(REG_DIFFUSE_MAP);
Texture2DArray ShadowMaps : register(REG_SHADOW_MAPS);
StructuredBuffer<SpotlightDescription> SpotlightDescriptions : register(REG_SHADOW_MAPS_ENTRIES);
