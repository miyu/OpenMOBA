#ifndef __COMMON_HLSL__
#define __COMMON_HLSL__

#include "helpers/lighting.hlsl"
#include "helpers/pbr.hlsl"

float4 commonComputeFragmentOutput(float3 P, float3 N, float3 base, float transparency, float metallic, float roughness) {
   float3 V = normalize(cameraEye - P);

   float3 colorAccumulator = 0;
   
   float3 diffuse, F0;
   pbrMaterialDiffuseF0(base, metallic, diffuse, F0);

   float nDotV = dot(N, V);
   for (uint i = 0; i != numSpotlights; i++) {
      colorAccumulator += pbrComputeSpotlightDirectContribution(P, N, i, V, nDotV, diffuse, F0, roughness);
   }
	  
   // environment lighting
   // [branch] if (false)
   // {
   //    // L is V reflection based on normal.
   //    float3 L = -V + 2.0f * dot(V, N) * N;
   //    float3 unattenuatedLightContribution = pbrComputeUnattenuatedLightContribution(P, N, L, V, nDotV, diffuse, F0, roughness);
   //    colorAccumulator += unattenuatedLightContribution * 0.05f;
   // }

   return float4(colorAccumulator, transparency);
}

#endif // __COMMON_HLSL__
