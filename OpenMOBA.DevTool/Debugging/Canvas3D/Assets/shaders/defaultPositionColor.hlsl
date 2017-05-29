struct PSInput {
   float4 position : SV_POSITION;
   float4 color : COLOR;
};

// float4x4 projView;
// float4x4 world;
cbuffer ObjectBuffer : register(b0) {
   float4x4 projView;
   float4x4 world;
   float4x4 throwaway;
}


PSInput VSMain(float4 position : POSITION, float4 color : COLOR) {
   PSInput result;

   result.position = mul(projView, position);
   result.color = color;

   return result;
}

float4 PSMain(PSInput input) : SV_TARGET
{
   return input.color;
}