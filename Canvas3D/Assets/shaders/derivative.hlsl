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
    float w, h;
    diffuseMap.GetDimensions(w, h);
    
    float ox = float2(1.0f / w, 0.0f);
    float oy = float2(0.0f, 1.0f / h);
    float center = diffuseMap.Sample(DiffuseSampler, input.uv);
    float d = -diffuseMap.Sample(DiffuseSampler, input.uv - 2 * ox - 2 * oy)
        - 2 * diffuseMap.Sample(DiffuseSampler, input.uv - 1 * ox - 2 * oy)
        - 2 * diffuseMap.Sample(DiffuseSampler, input.uv - 2 * ox - 1 * oy)
        - 4 * diffuseMap.Sample(DiffuseSampler, input.uv - 1 * ox - 1 * oy)
        - 1 * diffuseMap.Sample(DiffuseSampler, input.uv + 2 * ox + 2 * oy)
        - 2 * diffuseMap.Sample(DiffuseSampler, input.uv + 1 * ox + 2 * oy)
        - 2 * diffuseMap.Sample(DiffuseSampler, input.uv + 2 * ox + 1 * oy)
        - 4 * diffuseMap.Sample(DiffuseSampler, input.uv + 1 * ox + 1 * oy)
        - 1 * diffuseMap.Sample(DiffuseSampler, input.uv - 2 * ox + 2 * oy)
        - 2 * diffuseMap.Sample(DiffuseSampler, input.uv - 1 * ox + 2 * oy)
        - 2 * diffuseMap.Sample(DiffuseSampler, input.uv - 2 * ox + 1 * oy)
        - 4 * diffuseMap.Sample(DiffuseSampler, input.uv - 1 * ox + 1 * oy)
        - 1 * diffuseMap.Sample(DiffuseSampler, input.uv + 2 * ox - 2 * oy)
        - 2 * diffuseMap.Sample(DiffuseSampler, input.uv + 1 * ox - 2 * oy)
        - 2 * diffuseMap.Sample(DiffuseSampler, input.uv + 2 * ox - 1 * oy)
        - 4 * diffuseMap.Sample(DiffuseSampler, input.uv + 1 * ox - 1 * oy)
        + center * 36;
    return abs(d) > 5.0E-5f;
     //abs(d) > 5.0E-6f;
    
    float tl = diffuseMap.Sample(DiffuseSampler, input.uv - ox - oy);
    float tc = diffuseMap.Sample(DiffuseSampler, input.uv - oy);
    float tr = diffuseMap.Sample(DiffuseSampler, input.uv + ox - oy);
    float cl = diffuseMap.Sample(DiffuseSampler, input.uv - ox);
    float cc = diffuseMap.Sample(DiffuseSampler, input.uv);
    float cr = diffuseMap.Sample(DiffuseSampler, input.uv + ox);
    float bl = diffuseMap.Sample(DiffuseSampler, input.uv - ox + oy);
    float bc = diffuseMap.Sample(DiffuseSampler, input.uv + oy);
    float br = diffuseMap.Sample(DiffuseSampler, input.uv + ox + oy);
    
    float score1 = abs(4 * cc - cl - cr - tc - bc);
    float score2 = abs(tl + tr + bl + br - cl - cr - tc - bc);
    return max(score1, score2) > 5.0E-6f;
}