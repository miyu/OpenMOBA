#ifndef __FORWARD_HLSL__
#define __FORWARD_HLSL__

#include "helpers/atmosphere.hlsl"
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

   float4x4 batchWorld = mul(batchTransform, world);
   float4 positionWorld = mul(batchWorld, float4(position, 1));
   float4 normalWorld = mul(batchWorld, float4(normal, 0));

   result.positionObject = position;
   result.positionWorld = positionWorld.xyz;
   result.position = mul(cameraProjView, positionWorld);
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

    // ray points from center of world to atmosphere.
    float2 uv = input.uv * 2 - 1;
    uv.y *= -1;
    float4 rayA = mul(mainProjViewInv, float4(uv, 1.0, 1));
	rayA /= rayA.w;
    float4 rayB = mul(mainProjViewInv, float4(uv, 0.1, 1));
	rayB /= rayB.w;

    float4 rayDirection = rayA - rayB; //mul(mainProjViewInv, float4(uv, 1.0, 1));
    //rayDirection *= 1.0 / rayDirection.w;
	rayDirection.xyz = normalize(rayDirection.xyz);
    //return float4(normalize(rayDirection.xyz).x, 0, 0, 1);
    //return float4(normalize(rayDirection.xyz), 1);

    float3 aColor;
    float3 camPos = float3(0.0,6372e3,0.0);
    float3 uSunPosition = float3(0.0,2.7,-1.0);
    uSunPosition.y = 0.15 + (sin(iTime * 5.5 * 0.1) + 0.0 * 0.9) * 0.2;
	//uSunPosition = float3(0, 1, 0);
	uSunPosition = float3(0, abs(sin(iTime * 0.3)), cos(iTime * 0.3));
	uSunPosition = normalize(uSunPosition);

    aColor = atmosphere(
        rayDirection,           		// normalized ray direction
        camPos,               			// ray origin
        uSunPosition,                   // position of the sun
        22.0,                           // intensity of the sun
        6371e3,                         // radius of the planet in meters
        6471e3,                         // radius of the atmosphere in meters
        float3(5.5e-6, 13.0e-6, 22.4e-6), // Rayleigh scattering coefficient
        21e-6,                          // Mie scattering coefficient
        32e3,                            // Rayleigh scale height
        1.2e3,                          // Mie scale height
        0.758                            // Mie preferred scattering direction
    );
    return float4(aColor * 0.2, 1);
}

#endif // __FORWARD_HLSL__
