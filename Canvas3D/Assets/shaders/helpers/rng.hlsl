#ifndef __RNG_HLSL__
#define __RNG_HLSL__

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

#endif // __RNG_HLSL__