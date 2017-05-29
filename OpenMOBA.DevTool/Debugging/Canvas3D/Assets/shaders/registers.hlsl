#include "types.hlsl"

#define REG_OBJECT_DATA b0

#define REG_SHADOW_MAPS t10
#define REG_SHADOW_MAPS_ENTRIES t11

cbuffer ObjectBuffer : register(REG_OBJECT_DATA)
{
    float4x4 projView;
    float4x4 world;
    float4x4 derp;
}

Texture2DArray shadowMaps : register(REG_SHADOW_MAPS);
StructuredBuffer<ShadowMapEntry> shadowMapEntries : register(REG_SHADOW_MAPS_ENTRIES);
