#ifndef __FORWARD_WATER_HLSL__
#define __FORWARD_WATER_HLSL__

#include "helpers/phong.hlsl"
#include "helpers/rng.hlsl"
#include "common.hlsl"

struct PSInput {
    float3 positionWorld : POSITION1;
    float4 position : SV_Position;
    float3 normalWorld : NORMAL1;
};

float3 computeWavePoint(float3 p) {
    float frequency = 0.32;
    float amplitude = 0.7f;
    
    float2x2 uvScramble = { 1.234f, 1.456f, 1.456f, -1.34f };
    float2x2 uvScramble2 = { -1.534f, 1.416f, 1.256f, -1.04f };
    
    float2 octaveDirection = normalize(mul(uvScramble2, float2(1, 1)));
    float2 octaveBasePhaseShift = mul(uvScramble2, mul(uvScramble, float2(100, 100)));
    
    float2 uv = p.xz;
    float h = 0;
    for (int i = 0; i < 6; i++) {
        float2 phaseShift = iTime * octaveDirection * 2 + octaveBasePhaseShift;
        float rawNoise = noise(uv * frequency + phaseShift); // [-1, 1]
        h += amplitude * rawNoise; // * (i == 0 ? 1 : 0);
        
        amplitude *= i == 0 ? 0.528f : 0.1828f;
        frequency *= 4.1337f;
        octaveDirection = normalize(mul(uvScramble2, octaveDirection));
        octaveBasePhaseShift = mul(uvScramble2, mul(uvScramble, octaveBasePhaseShift));
    }

    // also add some ridge noise to have more visible wavefronts.
    frequency = 0.08f;
    amplitude = 1.0f;
    octaveDirection = normalize(mul(uvScramble2, float2(0.1, 9)));
    octaveBasePhaseShift = mul(uvScramble2, mul(uvScramble, float2(234, -123)));
    for (int i = 0; i < 6; i++) {
        float2 phaseShift = iTime * octaveDirection * 0.3 + octaveBasePhaseShift;
        float rawNoise = noise(uv * frequency + phaseShift); // [-1, 1]
        float ridge = 1.0f - abs(rawNoise);
        ridge = ridge * ridge;
        h += amplitude * ridge;
        
        amplitude *= i == 0 ? 0.528f : 0.1828f;
        octaveDirection = normalize(mul(uvScramble2, octaveDirection));
        octaveBasePhaseShift = mul(uvScramble2, mul(uvScramble, octaveBasePhaseShift));
    }
    
    return p + float3(0, h, 0);
}

PSInput VSMain(
    float3 position : POSITION,
    float4x4 world : INSTANCE_TRANSFORM
) {
    PSInput result;
    
    // transform position to world space
    float4x4 batchWorld = mul(batchTransform, world);
    float3 positionWorldBase = mul(batchWorld, float4(position * 20 - float3(10, 10, 0), 1));
    
    // compute wave-offset position
    float3 positionWorldWave = computeWavePoint(positionWorldBase);
    
    // compute normals based on nearby wave offsets.
    float3 positionWorldXPlusWave = computeWavePoint(positionWorldBase + float3(0.01f, 0, 0));
    float3 positionWorldZPlusWave = computeWavePoint(positionWorldBase + float3(0, 0, 0.01f));
    float3 normalWorldWave = -normalize(cross(
        positionWorldXPlusWave - positionWorldWave, 
        positionWorldZPlusWave - positionWorldWave));
    
    // transform position / normal from object to world space.
    result.position = mul(projView, float4(positionWorldWave, 1));
    result.positionWorld = positionWorldWave.xyz;
    result.normalWorld = normalize(normalWorldWave.xyz); // must normalize in PS too
    return result;
}

float4 PSMain(PSInput input) : SV_TARGET {
    // Vector to directional light.    
	float3 Li = normalize(float3(-10, 3, -3));
	
    // Sea mood colors (can configure - currently using iq colors)
    float3 SEA_SHALLOW_COLOR = float3(25, 48, 56) / 255; // blue, shallow contribution
    float3 SEA_DEEP_COLOR = float3(204, 229, 153) / 255; // green, deep contribution.

	// Frag phong vectors.
    float3 P = input.positionWorld;	            // World-space position of water fragment
    float3 N = normalize(input.normalWorld);    // Normalized world-space normal of water fragment
    float3 V = normalize(cameraEye - P);        // Normalized direction from fragment to eye in world-space.
    float3 L = -V + 2.0f * dot(V, N) * N;       // Normalized direction from eye to fragment in world-space.
	                                            // L is not to sun! Reflective contrib is of environment (atmosphere).
    float3 H = normalize(L + V);                // Halfway between L, V.

    // Compute fresnel: how much reflection (as opposed to refraction)
    float fresnel = pow(1.0f - dot(V, H), 3.0f) * 0.6; // f0 = 0, too much reflection looks bad.
    //return float4(fresnel, fresnel, fresnel, 1);
    
    // Compute refracted fresnel light contribution (deeper gradient is from iq)
    float deepContribution = pow(dot(N, L) * 0.4 + 0.6, 80) * 0.12;
    float3 refracted = SEA_SHALLOW_COLOR + deepContribution * SEA_DEEP_COLOR;
    // refracted = float3(4, 57, 99) / 255.0f; // clear blue, more fantasylike.
    
    // sky light contribution is white for now.
    float3 reflected = 1 * float3(1, 1, 1);

    // compute final refraction and reflection mixture.
    float3 color = lerp(refracted, reflected, fresnel);
    
    // darken lower portion of waves, lighten higher portion, iq inspired
    float eyeToP = P - cameraEye;
    float distanceAttenuation = max(1.0 - dot(eyeToP, eyeToP) * 0.001, 0.0);
    color += SEA_DEEP_COLOR * (P.y * 0.4 - 0.7) * 0.18 * distanceAttenuation;
    
    // specular lighting contribution
    float specular = computeSpecular(Li, N, V, 100.0);
    color += float3(specular, specular, specular);
    
    return float4(color, 1);
    return float4(color.b, color.g, color.r, 1);
}

#endif // __FORWARD_WATER_HLSL__
