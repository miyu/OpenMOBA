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
   float val = diffuseMap.Sample(DiffuseSampler, input.uv);
   return val;
}