#ifndef __FORWARD_HLSL__
#define __FORWARD_HLSL__

#include "helpers/lighting.hlsl"
#include "helpers/pbr.hlsl"
#include "common.hlsl"

struct PSInput {
   float3 positionObject : POSITION1;
   float3 positionWorld : POSITION2;
   float4 position : SV_Position;
   float3 normalObject : NORMAL1;
   float3 normalWorld : NORMAL2;
   float4 color : COLOR;
   float2 uv : TEXCOORD;
   float metallic : MATERIAL_METALLIC;
   float roughness : MATERIAL_ROUGHNESS;
   int materialResourcesIndex : MATERIAL_INDEX;
};

float rand(float2 p) {
   return frac(sin(dot(p, float2(1234.23, 2345.28))) * 21337.21337);
}

float noise(float2 p) {
   float2 cell = floor(p);
   float2 fractional = frac(p);	
   float2 tween = fractional * fractional * (3.0 - 2.0 * fractional); // just 3x^2 - 2x^3 tween.
   float bl = rand(cell + float2(0.0f, 0.0f));
   float br = rand(cell + float2(1.0f, 0.0f));
   float tl = rand(cell + float2(0.0f, 1.0f));
   float tr = rand(cell + float2(1.0f, 1.0f));
   return -1.0 + 2.0 * lerp(lerp(bl, br, tween.x), lerp(tl, tr, tween.x), tween.y);
}

float3 computeHeight(float3 p) {
   float h = 0;
   float2 uv = p.xy / (1024 * 0.1f);
   
   float frequency = 64;
   float amplitude = 0.5f;
   
   float2x2 uvScramble = { 
      1.234f, 1.456f,
	  1.456f, -1.234f }; // todo: normalize?

   for (int i = 0; i < 5; i++) {
      h += amplitude * noise(uv * frequency);
	  amplitude *= 0.1828f;
	  frequency *= 2.1337f;
	  uv = mul(uvScramble, uv);
   }

   return p + float3(0, 0, h);
}

PSInput VSMain(
   float3 position : POSITION, 
   float3 normal : NORMAL, 
   float4 vertexColor : VERTEX_COLOR, 
   float2 uv : TEXCOORD,
   float4x4 world : INSTANCE_TRANSFORM,
   float metallic : INSTANCE_METALLIC,
   float roughness : INSTANCE_ROUGHNESS,
   int materialResourcesIndex : INSTANCE_MATERIAL_RESOURCES_INDEX,
   float4 instanceColor : INSTANCE_COLOR
) {
   PSInput result;
   
   float3 positionXPlus = position + float3(0.001f, 0, 0);
   float3 positionYPlus = position + float3(0, 0.001f, 0);
   
   position = computeHeight(position);
   positionXPlus = computeHeight(positionXPlus);
   positionYPlus = computeHeight(positionYPlus);
   normal = normalize(cross((positionXPlus - position), (positionYPlus - position)));

   float4x4 batchWorld = mul(batchTransform, world);
   float4 positionWorld = mul(batchWorld, float4(position, 1));
   float4 normalWorld = mul(batchWorld, float4(normal, 0));

   result.positionObject = position;
   result.positionWorld = positionWorld.xyz;
   result.position = mul(projView, positionWorld);
   result.normalObject = normal;
   result.normalWorld = normalize(normalWorld.xyz); // must normalize in PS
   result.color = vertexColor * instanceColor;
   result.uv = uv;
   result.metallic = metallic;
   result.roughness = roughness;
   result.materialResourcesIndex = materialResourcesIndex;

   return result;
}

float4 PSMain(PSInput input) : SV_TARGET {
   float3 P = input.positionWorld;
   float3 N = normalize(input.normalWorld);
   float3 V = normalize(cameraEye - P);
   float3 L = -V + 2.0f * dot(V, N) * N;
   float3 H = normalize(L + V);
   float vDotH = dot(V, H);
   float f0 = 0.0f;
   float fresnel = f0 + (1.0f - f0) * pow(1.0f - vDotH, 3.0f) * 0.5;
   return float4(fresnel, fresnel, fresnel, 1);
}

#endif // __FORWARD_HLSL__
