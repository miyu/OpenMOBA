struct PSInput {
   float4 position : SV_POSITION;
   float4 color : COLOR;
};

float4x4 projViewWorld;

PSInput VSMain(float4 position : POSITION, float4 color : COLOR) {
   PSInput result;

   result.position = mul(projViewWorld, position);
   result.color = color;

   return result;
}

float4 PSMain(PSInput input) : SV_TARGET
{
   return input.color;
}