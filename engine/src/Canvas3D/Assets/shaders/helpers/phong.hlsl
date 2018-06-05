#ifndef __PHONG_HLSL__
#define __PHONG_HLSL__

// Li - Vector to light (opposite of directional light direction)
// N - Normalized normal at P.
// V - Normalized from P to Eye.
float computeSpecular(float3 Li, float3 N, float3 V, float specularExponent) {
    return clamp(pow(max(dot(-reflect(Li, N), V), 0.0), specularExponent), 0, 1);
}

#endif // __PHONG_HLSL__