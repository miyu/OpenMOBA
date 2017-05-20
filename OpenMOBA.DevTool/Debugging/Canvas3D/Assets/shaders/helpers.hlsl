struct ShadowMapEntry
{
    float x;
    float y;
    float width;
    float height;
    float4x4 projViewWorld;
    float4 color;
};

struct ShadowMapSampleResult
{
    bool isIlluminated;
};

float4 TestShadowMap(float4 , ShadowMapEntry entry)
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