struct PointLight
{
    float4 color;
    float4x4 projViewWorld;
};

struct AtlasLocation {
   float3 position;
   float2 size;
};

struct ShadowMapEntry {
    AtlasLocation location;
    float4x4 projView;
    float4 color;
    float power;
};

struct ShadowMapSampleResult {
   bool isIlluminated;
};
