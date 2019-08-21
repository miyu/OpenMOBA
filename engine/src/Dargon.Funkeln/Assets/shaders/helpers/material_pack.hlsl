#ifndef __PBR_MATERIAL_PACK__
#define __PBR_MATERIAL_PACK__

float packMaterial(float metallic, float roughness) {
   const float pbrPackResolution = 512;
   float m = floor(metallic * (pbrPackResolution - 1));
   float r = floor(roughness * (pbrPackResolution - 1));
   return (m * pbrPackResolution + r) / (pbrPackResolution * pbrPackResolution);
}

void unpackMaterial(float material, out float metallic, out float roughness) {
   const float pbrPackResolution = 512;
   float expanded = material * pbrPackResolution * pbrPackResolution;
   float r = fmod(expanded, pbrPackResolution);
   float m = (expanded - r) / pbrPackResolution;
   metallic = saturate(m / (pbrPackResolution - 1));
   roughness = saturate(r / (pbrPackResolution - 1));
}

#endif // __PBR_MATERIAL_PACK__