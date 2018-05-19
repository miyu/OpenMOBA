#ifndef __FORWARD_WATER_HLSL__
#define __FORWARD_WATER_HLSL__

#include "helpers/atmosphere.hlsl"
#include "helpers/phong.hlsl"
#include "helpers/rng.hlsl"
#include "common.hlsl"

struct PSInput {
    float3 positionWorld : POSITION1;
    float4 position : SV_Position;
    float3 normalWorld : NORMAL1;
};

float3 computeWavePoint(float3 p) {
    float scale = 2;
    float timeScale = 0.6f;
    float frequency = 0.32 / scale;
    float amplitude = 0.4f * scale;
    
    float2x2 uvScramble = { 1.234f, 1.456f, 1.456f, -1.34f };
    float2x2 uvScramble2 = { -1.534f, 1.416f, 1.256f, -1.04f };
    float2x2 uvScramble3 = { 0.234f, -0.216f, 0.256f, -0.34f };
    
    float2 octaveDirection = normalize(mul(uvScramble2, float2(1, 1)));
    float2 octaveBasePhaseShift = mul(uvScramble2, mul(uvScramble, float2(100, 100)));
    
    float2 uv = p.xz;
    float h = 0;
    for (int i = 0; i < 7; i++) {
        float2 phaseShift = iTime * octaveDirection * 2 * timeScale + octaveBasePhaseShift;
        float rawNoise = noise(uv * frequency + phaseShift); // [-1, 1]
        h += amplitude * rawNoise; // * (i == 0 ? 1 : 0);

        if (i == 0) {
            amplitude *= 0.528f;
            frequency *= 2.1337f;
        } else if (i == 1) {
            amplitude *= 0.3828f;
            frequency *= 2.1337f;
        } else if (i == 2 || true) {
            amplitude *= 0.428f;
            frequency *= 2.1337f;
        } else {
            amplitude *= 0.8028f;
            frequency *= 1.1337f;
        }

        //amplitude *= i == 0 ? 0.528f : 0.3828f;
        octaveDirection = normalize(mul(uvScramble2, octaveDirection));
        octaveBasePhaseShift = mul(uvScramble2, mul(uvScramble, octaveBasePhaseShift));
    }

    // also add some ridge noise to have more visible wavefronts.
    frequency = 0.08f / scale;
    amplitude = 0.8f * scale;
    octaveDirection = normalize(mul(uvScramble2, float2(0.1, 9)));
    octaveBasePhaseShift = mul(uvScramble2, mul(uvScramble, float2(234, -123)));
    for (int i = 0; i < 5; i++) {
        float2 phaseShift = iTime * octaveDirection * 0.3 * timeScale + octaveBasePhaseShift;
        float rawNoise = noise(uv * frequency + phaseShift); // [-1, 1]
        float ridge = 1.0f - abs(rawNoise);
        ridge = ridge * ridge;
        h += amplitude * (ridge - 0.5f);

        if (i == 0) {
            //amplitude *= 0.828f;
            frequency *= 0.627f;
        } else if (i == 1) {
            //amplitude *= 0.8828f;
			frequency *= 0.627f;
        } else {
            //amplitude *= 0.3828f;
			//frequency *= 0.627f;
        }

        octaveDirection = normalize(mul(uvScramble3, octaveDirection));
        octaveBasePhaseShift = mul(uvScramble, mul(uvScramble3, octaveBasePhaseShift));
    }
    h *= 1;
    
    return p + float3(0, h, 0);
}

PSInput VSMain(
    float3 position : POSITION,
    float4x4 world : INSTANCE_TRANSFORM
) {
    PSInput result;
    
    // transform position to world space
    float4x4 batchWorld = mul(batchTransform, world);
    float3 positionWorldBase = mul(batchWorld, float4(position, 1));
    
    // compute wave-offset position
    float3 positionWorldWave = computeWavePoint(positionWorldBase);
    
    // compute normals based on nearby wave offsets.
    float3 positionWorldXPlusWave = computeWavePoint(positionWorldBase + float3(0.1f, 0, 0));
    float3 positionWorldZPlusWave = computeWavePoint(positionWorldBase + float3(0, 0, 0.1f));
    float3 normalWorldWave = -normalize(cross(
        positionWorldXPlusWave - positionWorldWave, 
        positionWorldZPlusWave - positionWorldWave));
    
    // transform position / normal from object to world space.
    result.position = mul(cameraProjView, float4(positionWorldWave, 1));
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
    float3 L = -V + 2.0f * dot(V, N) * N;       // L is not to sun! Reflective contrib is of environment (atmosphere).
    float3 H = normalize(L + V);                // Halfway between L, V.
    //return float4(N, 1);
    //return float4(float3(1,1,1) * P.y / 20, 1);

    // Compute fresnel: how much reflection (as opposed to refraction)
    float f0 = 0.05f;
    float fresnel = f0 + (1 - f0) * pow(1.0f - dot(V, H), 3.0f); // f0 = 0, too much reflection looks bad.
    //return float4(fresnel, fresnel, fresnel, 1);
    
    // Compute refracted fresnel light contribution (deeper gradient is from iq)
    float deepContribution = pow(dot(N, L) * 0.4 + 0.6, 80) * 0.12;
    float3 refracted = SEA_SHALLOW_COLOR + deepContribution * SEA_DEEP_COLOR;
    // refracted = float3(4, 57, 99) / 255.0f; // clear blue, more fantasylike.
    //refracted = float3(0,0,0);
    
    float3 rayDirection = -V;
    rayDirection = reflect(rayDirection, N);
    rayDirection.y = abs(rayDirection.y);
    //rayDirection.y = clamp(rayDirection.y, 0.1, 1);
    rayDirection = normalize(rayDirection);
    //rayDirection = normalize(lerp(rayDirection, float3(0, 1, 0), 0.1));

    //rayDirection.z = -rayDirection.z;
    float3 aColor;
    float3 camPos = float3(0.0,6372e3,0.0);
    float3 uSunPosition = float3(0.0,2.7,-1.0);
    uSunPosition.y = 0.15 + (sin(iTime * 5.5 * 0.1) + 0.0 * 0.9) * 0.2;
    //uSunPosition = float3(0, 1, 0);
    uSunPosition = float3(0, abs(sin(iTime * 0.3)), cos(iTime * 0.3));
    uSunPosition = normalize(uSunPosition);
    //rayDirection.y = abs(rayDirection.y);

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
    //aColor = float3(1,1,1);
    
    float3 aColorAmb = atmosphere(
        float3(0, 1, 0),           		// normalized ray direction
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
    //aColorAmb = float3(1,1,1);

    //return float4(aColor, 1);
    float3 reflected = aColor;
    //reflected = clamp(aColor, float3(0, 0, 0), float3(1,1,1));
    //reflected *= 10000;
    float3 acontrib = (aColorAmb + aColor) / 2;
    
    // // darken lower portion of waves, lighten higher portion, iq inspired
    float eyeToP = P - cameraEye;
    float distanceAttenuation = 0.8 + 0.2 * max(1.0 - dot(eyeToP, eyeToP) * 0.001, 0.0);
    refracted += SEA_DEEP_COLOR * (P.y * 0.25 - 0.4) * 0.18 * distanceAttenuation;

    // grayscale - water is clear, color comes from atmosphere.
    refracted =  (refracted.x + refracted.y + refracted.z) * 0.333 *  float3(1, 1, 1) * 3;
    refracted *= acontrib;// 0; //refracted * reflected * 0.8 + refracted * 0.2;
    //refracted = 0;

    // compute final refraction and reflection mixture.
    float3 color = lerp(refracted, reflected, fresnel);
    
    // specular lighting contribution
    float specular = computeSpecular(uSunPosition, N, V, 200.0);
    color += float3(specular, specular, specular) * aColor;
    
    return float4(color * 0.2, 1);
    return float4(color, 1);
    return float4(color.b, color.g, color.r, 1);
}

#endif // __FORWARD_WATER_HLSL__
