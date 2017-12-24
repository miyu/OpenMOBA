//-------------------------------------------------------------------------------------------------
// See https://github.com/KhronosGroup/glTF/blob/master/specification/2.0/README.md
//     http://www.codinglabs.net/article_physically_based_rendering_cook_torrance.aspx
//     https://en.wikipedia.org/wiki/Specular_highlight#Cook.E2.80.93Torrance_model
//     https://cdn2.unrealengine.com/Resources/files/2013SiggraphPresentationsNotes-26915738.pdf
// For Lambertian and Cook-Torrance BRDFs.
// Like in UE's 2013 siggraph paper, will use alpha = roughness^2 ("disney reparameterization")
//
// All helper functions assume xDotY is in [0, 1]
//-------------------------------------------------------------------------------------------------
static const float PI = 3.14159265f;

//-------------------------------------------------------------------------------------------------
// Lambertian BRDF
//-------------------------------------------------------------------------------------------------
float3 lambertianBrdf() {
   return (float3)(1.0f / PI);
}

//-------------------------------------------------------------------------------------------------
// Cook-Torrance Specular BRDF
// See: http://www.codinglabs.net/article_physically_based_rendering_cook_torrance.aspx
// All functions assume xDotY is in [0, 1]
//-------------------------------------------------------------------------------------------------
// Is x positive? 1.0f, else 0.0f.
float ctUtilChi(float x) {
   return float(x > 0);
}

// Microfacet Distribution D function via GGX model.
// Article equates (m.n)^2 with nDotH^2
// alpha is roughness^2
// I added saturate - intuitively you can't have <0 or >1
// probability of a microfacet pointing in a direction.
float ctDistributionGGX(float nDotH, float roughness) {
   // "disney reparameterization"; see UE paper.
   float alpha = roughness * roughness;
   float nDotH2 = nDotH * nDotH;
   float alpha2 = alpha * alpha;
   float temp = nDotH2 * (alpha2 - 1.0f) + 1;
   return saturate(alpha2 / (PI * temp * temp));
   //float numerator = alpha2 * ctUtilChi(nDotH);
   //float temp = (nDotH2 * alpha2) + (1 - nDotH2);
   //return numerator / (PI * temp * temp);
}

// Fresnel F function via Shlick C. (1994) approximation
// F = F_0 + (1 - F_0)(1 - cos(theta))^5
// f0 is ((n1-n2)/(n1+n2))^2
// theta is angle between viewing direction and half vector
// for Cook-Torrance BRDF (eq V.H for unit V, H).
// 
// Alt interpretation: Computes k_specular, % specular reflection.
// This happens to use the same computation as CT's F in our implementation.
float ctFresnelShlick(float f0, float vDotH) {
   // Assumes dielectric material. See article for better conductor formula.
   return f0 + (1.0f - f0) * pow(1.0f - vDotH, 5.0f);
}

float ctFresnelShlick3(float3 f0, float vDotH) {
   return float3(
      ctFresnelShlick(f0.x, vDotH),
      ctFresnelShlick(f0.y, vDotH),
      ctFresnelShlick(f0.z, vDotH)
   );
}

// Light attenuation via microfacet shadowing G function.
float ctGeometryUE4(float nDotL, float nDotV, float roughness) {
   // another reparameterization; see UE paper.
   roughness = (roughness + 1.0f) / 2.0f;
   float alpha = roughness * roughness;
   float k = alpha / 2.0f;
   float g1l = nDotL / (nDotL * (1.0f - k) + k);
   float g1v = nDotV / (nDotV * (1.0f - k) + k);
   return g1l * g1v;

   //float vDotH2 = vDotH * vDotH;
   //float temp = (1.0f - saturate(vDotH2)) / saturate(vDotH2);
   //return ctUtilChi(saturate(vDotH) / saturate(vDotN)) * 2.0f / (1.0f + sqrt(1.0f + alpha * alpha * temp));
}

// Unweighted Cook-Torrance BRDF.
float3 cookTorranceBrdf(float nDotH, float nDotL, float nDotV, float roughness, float3 F) {
   float D = ctDistributionGGX(nDotH, roughness);
   float G = ctGeometryUE4(nDotL, nDotV, roughness);
   float3 numerator = D * F * G;
   float denominator = 4 * nDotL * nDotV;
   return numerator / denominator;
}

void pbrMaterialDiffuseF0(float3 base, float metallic, out float3 diffuse, out float3 F0) {
   // Via glTF section on metallic/roughness materials
   const float3 DIELECTRIC_SPECULAR = float3(0.04f, 0.04f, 0.04f);
   const float3 BLACK = float3(0.0f, 0.0f, 0.0f);
   
   diffuse = lerp(base * (1 - DIELECTRIC_SPECULAR.x), BLACK, metallic);
   F0 = lerp(DIELECTRIC_SPECULAR, base, metallic);
}

void pbrPrecompute(float3 base, float metallic, out float3 diffuse, out float3 F0) {
}

float2 random2(float2 p) {
   float2 k1 = float2(57.72156649015328, 5.606512090824026);
   float2 k2 = float2(23.57111317192329, 16.06695152415291);
   return float2(
      frac(cos(dot(p, k1)) * 12345.6789),
      frac(sin(dot(p, k2)) * 1359.21337)
   );
}

float2 concentricSampleDisk(float2 rand) {
   float2 randoffset = 2.0f * rand - float2(1.0f, 1.0f);
   if (length(randoffset) == 0) {
      return float2(0.0f, 0.0f);
   }
   float theta, r;
   if (abs(randoffset.x) > abs(randoffset.y)) {
      r = randoffset.x;
      theta = (PI / 4) * (randoffset.y / randoffset.x);
   } else {
      r = randoffset.y;
      theta = (PI / 2) - (PI / 4) * (randoffset.x / randoffset.y);
   }
   return r * float2(cos(theta), sin(theta));
}

float3 cosineSampleHemisphere(float2 rand) {
   float2 d = concentricSampleDisk(rand);
   float z = sqrt(max(0.0f, 1.0f - d.x * d.x - d.y * d.y));
   return float3(d.x, d.y, z);
}

void pbrMaterialProperties(float3 pWorld, out float metallic, out float roughness) {
   if (pWorld.y < 0.01f) {
      metallic = 0.0f;
      roughness = 0.04f;
   }
   else if (length(pWorld - float3(0, 0.5f, 0)) <= 0.8f) {
      //metallic = 0.0f;
      metallic = 0.0f;// 0.4f;
      roughness = 0.9f; 
      //roughness = 1.0f;
   }
   else {
      metallic = 0.0f;// 0.4f;
      roughness = 0.04f; // 1.0f; // 0.2f;// 0.8f;
   }
}

void pbrPrecomputeDots(float3 pWorld, float3 nWorld, out float3 N, out float3 V, out float nDotV) {
   N = normalize(nWorld);
   V = normalize(cameraEye - pWorld);
   nDotV = max(1E-5, dot(N, V));
}

float3 pbrComputeUnattenuatedLightContribution(float3 P, float3 N, float3 L, float3 V, float3 nDotV, float3 diffuse, float3 F0, float roughness) {
   float3 H = normalize(L + V);
   float vDotH = max(1E-5, dot(V, H)); // max avoids artifacts near 0
   float nDotH = max(1E-5, dot(N, H));
   float nDotL = max(1E-5, dot(N, L));
   float3 ks = saturate(ctFresnelShlick3(F0, vDotH));
   float3 diffuseFactor = diffuse * (1.0f - ks) * lambertianBrdf();
   float3 specularFactor = cookTorranceBrdf(nDotH, nDotL, nDotV, roughness, ks);
   return diffuseFactor + specularFactor;
}

float3 pbrComputeSpotlightDirectContribution(float3 P, float3 N, int spotlightIndex, float3 V, float3 nDotV, float3 diffuse, float3 F0, float roughness) {
   float3 so = SpotlightDescriptions[spotlightIndex].origin;
   float3 L = normalize(so - P);
   float3 unattenuatedLightContribution = pbrComputeUnattenuatedLightContribution(P, N, L, V, nDotV, diffuse, F0, roughness);
   float3 attenuation = computeSpotlightLighting(P, N, ShadowMaps, SpotlightDescriptions[spotlightIndex]);
   return unattenuatedLightContribution * attenuation;
}

float3 pbrEvaluateScene() {

}
