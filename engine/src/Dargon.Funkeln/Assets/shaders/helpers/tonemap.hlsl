#ifndef __TONEMAP_HLSL__
#define __TONEMAP_HLSL__

float3 ToneMapFilmic(in float3 color) {
   color = max(0, color - 0.004f);
   color = (color * (6.2f * color + 0.5f)) / (color * (6.2f * color + 1.7f) + 0.06f);
   return color;
}

float3 ToneMap(in float3 c) {
   return ToneMapFilmic(c);
}

float3 ProcessOut(in float3 c) {
   float fExposure = 0.2f;
   c = 1.0 - exp(-fExposure * c);
   //c = ToneMap(c);
   return c;
}

#endif // __TONEMAP_HLSL__
