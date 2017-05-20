struct PSInput {
   float4 objectPosition : TEXCOORD1;
   float4 transformedPosition : SV_Position;
   float4 color : COLOR;
};

cbuffer obj : register(b0) {
   float4x4 oprojViewWorld;
   float4x4 lprojViewWorld;
}
Texture2D light1ShadowMap;

SamplerState DiffuseSampler {
   Filter = MIN_MAG_MIP_LINEAR;
   AddressU = Wrap;
   AddressV = Wrap;
};

PSInput VSMain(float4 position : POSITION, float4 color : COLOR) {
    PSInput result;

    result.objectPosition = position;
    result.transformedPosition = mul(oprojViewWorld, position);
    result.color = color;

    return result;
}

float4 PSMain(PSInput input) : SV_TARGET
{
    //float4 lightPosition = input.lightPosition;
    float4 lightPosition = mul(lprojViewWorld, input.objectPosition);
   //return input.color;
//   float2 uv;
//   uv.x = 0.5f;
//   uv.y = 0.5f;
//   return input.color * light1ShadowMap.Sample(DiffuseSampler, uv);
   lightPosition.xyz /= lightPosition.w;
   if (lightPosition.x < -1.0f || lightPosition.x > 1.0f ||
      lightPosition.y < -1.0f || lightPosition.y > 1.0f ||
      lightPosition.z < 0.0f || lightPosition.z > 1.0f) {
      return input.color / 5.0f;
   }

   // now in clip space (-1:1), ensure within unit circle
   float r = lightPosition.x * lightPosition.x + lightPosition.y * lightPosition.y;
   if (r > 1) return input.color / 5.0f;

   //transform clip space coords to texture space coords (-1:1 to 0:1)
   lightPosition.x = lightPosition.x / 2 + 0.5;
   lightPosition.y = lightPosition.y / -2 + 0.5;

   //sample shadow map - point sampler
   float shadowMapDepth = light1ShadowMap.Sample(DiffuseSampler, lightPosition.xy).r;
   shadowMapDepth += 0.0001; // depth bias

   //if clip space z value greater than shadow map value then pixel is in shadow
   if (shadowMapDepth < lightPosition.z) return input.color / 5.0f;
   // return input.lightPosition.z;
   return input.color;

   return float4( 1.0f, 1.0f, 0.0f, 1.0f );
}