//-------------------------------------------------------------------------------------------------
// See http://www.codinglabs.net/article_physically_based_rendering_cook_torrance.aspx
//     https://en.wikipedia.org/wiki/Specular_highlight#Cook.E2.80.93Torrance_model
//     https://cdn2.unrealengine.com/Resources/files/2013SiggraphPresentationsNotes-26915738.pdf
// For Lambertian and Cook-Torrance BRDFs.
//
// All helper functions assume xDotY is in [0, 1]
//-------------------------------------------------------------------------------------------------
static const float PI = 3.14159265f;

//-------------------------------------------------------------------------------------------------
// Lambertian BRDF
//-------------------------------------------------------------------------------------------------
float lambertianBrdf() {
   return 1 / PI;
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
float ctDistributionGGX(float nDotH, float alpha) {
   float nDotH2 = nDotH * nDotH;
   float alpha2 = alpha * alpha;
   float numerator = alpha2 * ctUtilChi(nDotH);
   float temp = (nDotH2 * alpha2) + (1 - nDotH2);
   return numerator / (PI * temp * temp);
}

// Fresnel F function via Shlick C. (1994) approximation
// F = F_0 + (1 - F_0)(1 - cos(theta))^5
// f0 is ((n1-n2)/(n1+n2))^2
// theta is angle between viewing direction and half vector
// for Cook-Torrance BRDF (eq V.H for unit V, H).
float ctFresnelShlick(float f0, float vDotH) {
   return f0 + (1.0f - f0) * pow(1.0f - vDotH, 5.0f);
}

// Light attenuation via microfacet shadowing G function.
float ctGeometryGGX(float vDotH, float vDotN, float alpha) {
   float vDotH2 = vDotH * vDotH;
   float temp = (1.0f - vDotH2) / vDotH2;
   return ctUtilChi(vDotH / vDotN) * 2.0f / (1.0f + sqrt(1.0f + alpha * alpha * temp));
}

// Computes k_specular, % specular reflection.
// This happens to use the same computation as CT's F in our implementation.
float ctSpecularFactor(float f0, float vDotH) {
   return ctFresnelShlick(f0, vDotH);
}

// Unweighted Cook-Torrance BRDF.
float cookTorranceBrdf(float vDotH, float vDotN, float nDotH, float nDotL, float roughness, float F) {
   float alpha = roughness;
   float D = ctDistributionGGX(nDotH, alpha);
   float G = ctGeometryGGX(vDotH, vDotN, alpha);
   float numerator = D * F * G;
   float denominator = 4 * vDotN * nDotL;
   return numerator / denominator;
}