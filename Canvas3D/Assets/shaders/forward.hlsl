#include "helpers/lighting.hlsl"
#include "helpers/pbr.hlsl"

struct PSInput {
   float4 positionObject : POSITION1;
   float4 positionWorld : POSITION2;
   float4 position : SV_Position;
   float4 normalObject : NORMAL1;
   float4 normalWorld : NORMAL2;
   float4 normal : NORMAL3;
   float4 color : COLOR;
   float2 uv : TEXCOORD;
};

PSInput VSMain(
   float3 position : POSITION, 
   float3 normal : NORMAL, 
   float4 color : COLOR, 
   float2 uv : TEXCOORD,
   float4x4 world : INSTANCE_TRANSFORM
) {
   PSInput result;

   result.positionObject = float4(position, 1);
   result.positionWorld = mul(world, float4(position, 1));
   result.position = mul(mul(projView, world), float4(position, 1));
   result.normalObject = float4(normal, 1);
   result.normalWorld = normalize(mul(world, float4(normal, 0)));
   result.normal = normalize(mul(mul(projView, world), float4(normal, 0)));
   result.color = color;
   result.uv = uv;

   return result;
}

float4 PSMain(PSInput input) : SV_TARGET {
   uint numStructs, structStride;
   SpotlightDescriptions.GetDimensions(numStructs, structStride);

   float metallic, roughness;
   if (input.positionWorld.y < 0.01f) {
      metallic = 0.0f;
      roughness = 0.04f;
   } else if (length(input.positionWorld - float3(0, 0.5f, 0)) <= 0.7f) {
      metallic = 1.0f;// 0.4f;
      roughness = 0.01f; 
      // roughness = 1.0f;
   } else {
      metallic = 0.0f;// 0.4f;
      roughness = 0.04f; // 1.0f; // 0.2f;// 0.8f;
   }

   // float metallic = 1.0f;// 0.4f;
   // float roughness = 0.04f; // 1.0f; // 0.2f;// 0.8f;

   // raw material color (albedo)
   float4 baseAndTransparency = input.color * SampleDiffuseMap(input.uv, input.positionObject.xyz, input.normalObject.xyz);
   float3 base = baseAndTransparency.xyz;
   float transparency = baseAndTransparency.w;

   float3 diffuse, F0;
   pbrMaterialDiffuseF0(base, metallic, diffuse, F0);

   // metallic doesn't diffuse.

   /**/
   float3 N = normalize(input.normalWorld);
   float3 V = normalize(cameraEye - input.positionWorld);

   float4 colorAccumulator = float4(0, 0, 0, transparency);
   for (uint i = 0; i != numSpotlights; i++) {
      float lightFactor = computeSpotlightLighting(input.positionWorld, input.normalWorld, ShadowMaps, SpotlightDescriptions[i]);
      lightFactor *= 4.0f;
      // colorAccumulator += diffuse * float4(lighting, 0);
      float3 so = SpotlightDescriptions[i].origin;
      float3 sd = SpotlightDescriptions[i].direction;
      float3 L = normalize(so - input.positionWorld);
      float3 H = normalize(L + V);
      float vDotH = max(1E-5, dot(V, H)); // max avoids artifacts near 0
      float vDotN = max(1E-5, dot(V, N));
      float nDotH = max(1E-5, dot(N, H));
      float nDotL = max(1E-5, dot(N, L));
      float3 ks = ctFresnelShlick3(F0, vDotH);
      //float xx = saturate(ctSpecularFactor(f0, vDotH));
      //float xx = saturate(ctDistributionGGX(nDotH, roughness));
      //float xx = saturate(ctGeometryGGX(nDotL, vDotN, roughness));
      //float xx = saturate(nDotH);
      float3 diffuseFactor = diffuse * (1.0f - ks) * lambertianBrdf();
      float3 specularFactor = cookTorranceBrdf(nDotH, nDotL, vDotN, roughness, ks);
      colorAccumulator += float4(lightFactor * (diffuseFactor + specularFactor), 0.0f);
      //float4 cxz = lf * (base * (1.0f - ks) * (1.0f / PI) * nDotL + specular * ks * ct);
      //return cxz;
      //return float4(xx, xx, xx, 1);
      //colorAccumulator += lf * (specular * ks * ct);
      //colorAccumulator += lf * (base * (1.0f - ks) * (1.0f / PI) * nDotL + specular * ks * ct);
   }

   // environment lighting
   {
      // L is V reflection based on normal.
      float3 L = -V + 2.0f * dot(V, N) * N;
   
      float3 lightFactor = float3(1, 1, 1) * 0.2f;// *(dot(L, N)) * 4.0f;
      float3 H = normalize(L + V); // is N
      float vDotH = max(1E-5, dot(V, H)); // max avoids artifacts near 0
      float vDotN = max(1E-5, dot(V, N));
      float nDotH = max(1E-5, dot(N, H));
      float nDotL = max(1E-5, dot(N, L));
      float3 ks = ctFresnelShlick3(F0, vDotH);
      float3 diffuseFactor = diffuse * (1.0f - ks) * lambertianBrdf();
      float3 specularFactor = cookTorranceBrdf(nDotH, nDotL, vDotN, roughness, ks);
      colorAccumulator += float4(lightFactor * (diffuseFactor + specularFactor), 0.0f);
      float D = ctDistributionGGX(nDotH, roughness);
      float G = ctGeometryUE4(nDotL, vDotN, roughness);
      //float xx = D * G * ks;
      //return xx * float4(1, 1, 1, 0) + float4(0, 0, 0, 1);
   }
   //float2 rand = input.positionWorld.xy; //seed w/ uv
   //const int NSAMPLES = 10;
   //for (int i = 0; i < NSAMPLES; i++)
   //{
   //   rand = random2(rand);
   //
   //   float3 normal = normalize(input.normalWorld);
   //   float3 helper = normal.x >= 0.8f ? float3(0.0f, 1.0f, 0.0f) : float3(1.0f, 0.0f, 0.0f);
   //   float3 tangent = cross(normal, helper);
   //   float3 bitangent = cross(normal, tangent);
   //   float3 samp = cosineSampleHemisphere(rand);
   //   float3 L = normalize(tangent * samp.x + bitangent * samp.y + normal * samp.z);
   //
   //   float3 lightFactor = float3(1,1,1) * (dot(L, normal) / NSAMPLES) * 4.0f;
   //   //float3 L = input.normalWorld;
   //   float3 H = normalize(L + V);
   //   float vDotH = max(1E-5, dot(V, H)); // max avoids artifacts near 0
   //   //float vDotN = max(1E-5, dot(V, N));
   //   //float nDotH = max(1E-5, dot(N, H));
   //   //float nDotL = max(1E-5, dot(N, L));
   //   float3 ks = ctFresnelShlick3(F0, vDotH);
   //   float3 diffuseFactor = diffuse * (1.0f - ks) * lambertianBrdf();
   //   float3 specularFactor = 0; // cookTorranceBrdf(nDotH, nDotL, vDotN, roughness, ks);
   //   colorAccumulator += float4(lightFactor * (diffuseFactor + specularFactor), 0.0f);
   //}
   return pow(colorAccumulator, 1.0f / 2.2f);
   /**/

   /*
   // Traditional diffuse lighting
   float4 diffuse = input.color * SampleDiffuseMap(input.uv, input.positionObject.xyz, input.normalObject.xyz);
   
   float4 colorAccumulator = float4(0, 0, 0, diffuse.w);
   if (!shadowTestEnabled) {
      colorAccumulator = diffuse;
   } else {
      for (uint i = 0; i != numSpotlights; i++) {
         float3 lighting = computeSpotlightLighting(input.positionWorld, input.normalWorld, ShadowMaps, SpotlightDescriptions[i]);
         colorAccumulator += diffuse * float4(lighting, 0);
      }
   }
   return colorAccumulator;
   /**/
}