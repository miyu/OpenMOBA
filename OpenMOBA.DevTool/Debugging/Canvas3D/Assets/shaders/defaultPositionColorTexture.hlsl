struct PSInput {
   float4 position : SV_POSITION;
   float4 color : COLOR;
   float2 uv : TEXCOORD;
};

float4x4 projViewWorld;
Texture2D diffuseMap;

SamplerState DiffuseSampler {
   Filter = MIN_MAG_MIP_LINEAR;
   AddressU = Wrap;
   AddressV = Wrap;
};

PSInput VSMain(float4 position : POSITION, float4 color : COLOR, float2 uv : TEXCOORD) {
   PSInput result;

   result.position = mul(projViewWorld, position);
   result.color = color;
   result.uv = uv;

   return result;
}

float4 PSMain(PSInput input) : SV_TARGET
{
    uint w, h;
    diffuseMap.GetDimensions(w, h);
    
    float dx = float2(1.0f / w, 0.0f);
    float dy = float2(0.0f, 1.0f / h);
    float center = diffuseMap.Sample(DiffuseSampler, input.uv);
    float left = diffuseMap.Sample(DiffuseSampler, input.uv + dx);
    float right = diffuseMap.Sample(DiffuseSampler, input.uv - dx);
    float top = diffuseMap.Sample(DiffuseSampler, input.uv - dy);
    float bottom = diffuseMap.Sample(DiffuseSampler, input.uv + dy);
    return abs(4 * center - left - right - top - bottom) > 1.0E-5f;
}