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
    float z = diffuseMap.Sample(DiffuseSampler, input.uv);
    return pow(z, 4);
    uint w, h;
    diffuseMap.GetDimensions(w, h);
    
    float dx = float2(1.0f / w, 0.0f);
    float dy = float2(0.0f, 1.0f / h);
    float center = diffuseMap.Sample(DiffuseSampler, input.uv);
    float tl = diffuseMap.Sample(DiffuseSampler, input.uv - dx - dy);
    float tc = diffuseMap.Sample(DiffuseSampler, input.uv - dy);
    float tr = diffuseMap.Sample(DiffuseSampler, input.uv + dx - dy);
    float cl = diffuseMap.Sample(DiffuseSampler, input.uv - dx);
    float cc = diffuseMap.Sample(DiffuseSampler, input.uv);
    float cr = diffuseMap.Sample(DiffuseSampler, input.uv + dx);
    float bl = diffuseMap.Sample(DiffuseSampler, input.uv - dx + dy);
    float bc = diffuseMap.Sample(DiffuseSampler, input.uv + dy);
    float br = diffuseMap.Sample(DiffuseSampler, input.uv + dx + dy);
    
    //float dx_ = abs(2 * cc - cl - cr);
    //float dy_ = abs(2 * cc - tc - bc);
    //float score = dx_ + dy_;
    float score = abs(8 * cc - tl - tc - tr - cl - cr - bl - bc - br);
    //float score = abs(16 * cc - 3 * cl - 3 * cr - 3 * tc - 3 * bc - tl - tr - bl - br);
    return score > 9.0E-7f;
}