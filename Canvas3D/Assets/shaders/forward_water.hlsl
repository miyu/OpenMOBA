#ifndef __FORWARD_WATER_HLSL__
#define __FORWARD_WATER_HLSL__

#include "helpers/atmosphere.hlsl"
#include "helpers/phong.hlsl"
#include "helpers/rng.hlsl"
#include "common.hlsl"

struct HS_CONSTANT_DATA {
    float Edges[3] : SV_TessFactor;
    float Inside[1] : SV_InsideTessFactor;
};

struct HSInput {
    float4 position : SV_POSITION;
    float3 positionWorld : POSITION2;
};

struct DSInput {
    float3 positionWorld : POSITION2;
};

struct GSInput {
    float4 position : SV_POSITION;
    float3 positionWorld : POSITION2;
    float3 positionWorldRaw : POSITION3;
};

struct PSInput {
    float4 position : SV_POSITION;
    float3 positionWorld : POSITION2;
    float3 normalWorld : NORMAL1;
};

float3 computeWavePoint(float3 p) {
    float scale = 2;
    float timeScale = 0.6f;
    float frequency = 0.04f / scale;
    float amplitude = 0.25f * scale;
    
    float2x2 uvScramble = { -1.234f, 1.456f, 1.456f, -1.34f };
    uvScramble *= 0.3f;

    float2x2 uvScramble2 = { -1.534f, 1.716f, 2.356f, -1.4f };
    uvScramble2 *= 0.3f;

    float2x2 uvScramble3 = { 0.234f, -0.216f, 0.256f, -0.34f };
    
    float2 octaveDirection = normalize(mul(uvScramble2, float2(1, 1)));
    float2 octaveBasePhaseShift = mul(uvScramble2, mul(uvScramble, float2(100, 100)));
    
    float2 uv = p.xz;
    float h = 0;
    for (int i = 0; i < 12; i++) {
        float2 phaseShift = iTime * octaveDirection * 2 * timeScale;
        float rawNoise = noise(uv * frequency + phaseShift + octaveBasePhaseShift); // [-1, 1]

        if (false && i >= 4) {
            // https://www.desmos.com/calculator/cu1asmf1ri
            // float xx = pow(rawNoise * 3, 2);
            // float ridge = (1 - xx) * exp(-xx / 1);
            // h += amplitude * (ridge - 0.3f);

            float xx = exp((rawNoise - 1) * 2)*2 - 1.03;
            h += amplitude * xx; // * (i == 0 ? 1 : 0);

        } else {
            h += amplitude * rawNoise; // * (i == 0 ? 1 : 0);
        }

        if (i == 0) {
            amplitude *= 0.928f;
            frequency *= 2.3337f;
        } else if (i == 1) {
            amplitude *= 0.8828f;
            frequency *= 2.1337f;
        } else if (i == 2) {
            amplitude *= 0.8828f;
            frequency *= 2.1337f;
        } else if (i == 3) {
            amplitude *= 0.4828f;
            frequency *= 1.1337f;
        } else {
            if (i % 2 == 0) {
                amplitude *= 0.8028f;
                frequency *= 1.43337f;
            } else {
                amplitude *= 0.9028f;
                frequency *= 1.3337f;
            }
        }

        octaveDirection = normalize(mul(uvScramble2, octaveDirection));
        octaveBasePhaseShift = mul(uvScramble2, mul(uvScramble, octaveBasePhaseShift));
    }

    // also add some ridge noise to have more visible wavefronts.
    frequency = 0.08f / scale;
    amplitude = 1.2f * scale;
    octaveDirection = normalize(mul(uvScramble2, float2(0.1, 9)));
    octaveBasePhaseShift = mul(uvScramble2, mul(uvScramble, float2(234, -123)));
    for (int i = 0; i < 7; i++) {
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

HSInput VSMain(
    float3 position : POSITION,
    float4x4 world : INSTANCE_TRANSFORM
) {
    HSInput result;
    
    // transform position to world space
    float4x4 batchWorld = mul(batchTransform, world);
    float3 positionWorldBase = mul(batchWorld, float4(position, 1));
    
    // compute wave-offset position
    float3 positionWorldWave = positionWorldBase; // computeWavePoint(positionWorldBase);
    
    // transform position / normal from object to world space.
    result.positionWorld = positionWorldWave.xyz;
    result.position = mul(cameraProjView, float4(result.positionWorld, 1.0f));	
    return result;
}

HS_CONSTANT_DATA SampleHSFunction(InputPatch<HSInput, 3> ip, uint PatchID : SV_PrimitiveID) {    
   HS_CONSTANT_DATA Output;

   float4 a = ip[0].position;
   float4 b = ip[1].position;
   float4 c = ip[2].position;
   a /= a.w;
   b /= b.w;
   c /= c.w;

   float thres = 1.3;
   if ((a.x > thres && b.x > thres && c.x > thres) || (a.x < -thres && b.x < -thres && c.x < -thres) || 
       (a.y > thres && b.y > thres && c.y > thres) || (a.y < -thres && b.y < -thres && c.y < -thres)) {
	   Output.Edges[0] = Output.Edges[1] = Output.Edges[2] = 0;
	   Output.Inside[0] = 0;
	   return Output;
   }

   if ((abs(a.x) > 1 || abs(a.y) > 1) &&
       (abs(b.x) > 1 || abs(b.y) > 1) &&
       (abs(c.x) > 1 || abs(c.y) > 1)) {
	   Output.Edges[0] = Output.Edges[1] = Output.Edges[2] = 1;
	   Output.Inside[0] = 1;
	   return Output;
   }

   float area = abs(cross(
     float3((a - b).xy, 0),
     float3((a - c).xy, 0)).z / 2);

   float fi = 1; //sqrt(max(1, min(1000, area * 2000)));
   float fa = max(1, min(1000, length((b - c).xy) * 50));
   float fb = max(1, min(1000, length((a - c).xy) * 50));
   float fc = max(1, min(1000, length((a - b).xy) * 50));

   //Output.Edges[0] = Output.Edges[1] = Output.Edges[2] = f;
   Output.Edges[0] = sqrt(fa);
   Output.Edges[1] = sqrt(fb);
   Output.Edges[2] = sqrt(fc);
   Output.Inside[0] = sqrt((fa + fb + fc) / 3);

   return Output;
}

[domain("tri")]
[partitioning("pow2")]
[outputtopology("triangle_cw")]
[outputcontrolpoints(3)]
[patchconstantfunc("SampleHSFunction")]
DSInput HSMain(InputPatch<HSInput, 3> p, uint i : SV_OutputControlPointID, uint PatchID : SV_PrimitiveID ) {
   DSInput res;
   res.positionWorld = p[i].positionWorld;
   return res;
}

[domain("tri")]
GSInput DSMain(HS_CONSTANT_DATA input, float3 UV : SV_DomainLocation, const OutputPatch<DSInput,3> TrianglePatch) {
    GSInput res;

    res.positionWorldRaw = UV.x * TrianglePatch[0].positionWorld + UV.y * TrianglePatch[1].positionWorld + UV.z * TrianglePatch[2].positionWorld;
    res.positionWorld = computeWavePoint(res.positionWorldRaw);
    res.position = mul(cameraProjView, float4(res.positionWorld, 1.0f));
    
    return res;    
}

float3 computeWaveNormal(float3 positionWorldWave, float3 positionWorldRaw) {
    //// compute normals based on nearby wave offsets.
    //float3 positionWorldXPlusWave = computeWavePoint(positionWorldRaw + float3(0.003f, 0, 0));
    //float3 positionWorldZPlusWave = computeWavePoint(positionWorldRaw + float3(0, 0, 0.003f));

    positionWorldWave = computeWavePoint(positionWorldRaw * float3(1,0,1));
    // compute normals based on nearby wave offsets.
    float3 positionWorldXPlusWave = computeWavePoint(positionWorldRaw * float3(1,0,1) + float3(0.2f, 0, 0));
    float3 positionWorldZPlusWave = computeWavePoint(positionWorldRaw * float3(1,0,1) + float3(0, 0, 0.2f));
    
    return -normalize(cross(
        positionWorldXPlusWave - positionWorldWave, 
        positionWorldZPlusWave - positionWorldWave));
}

[maxvertexcount(72)]
void GSMain(triangle GSInput input[3], inout TriangleStream<PSInput> TriStream) {
    PSInput A, B, C;
    A.position = input[0].position;
    A.positionWorld = input[0].positionWorld;
    
    B.position = input[1].position;
    B.positionWorld = input[1].positionWorld;
    
    C.position = input[2].position;
    C.positionWorld = input[2].positionWorld;
    
    if ((abs(A.position.x / A.position.w) > 1 || abs(A.position.y / A.position.w) > 1) &&
        (abs(B.position.x / B.position.w) > 1 || abs(B.position.y / B.position.w) > 1) &&
        (abs(C.position.x / C.position.w) > 1 || abs(C.position.y / C.position.w) > 1)) {
        return;
    }

    A.normalWorld = computeWaveNormal(input[0].positionWorld, input[0].positionWorldRaw);
    B.normalWorld = computeWaveNormal(input[1].positionWorld, input[1].positionWorldRaw);
    C.normalWorld = computeWaveNormal(input[2].positionWorld, input[2].positionWorldRaw);

    TriStream.Append(A);
    TriStream.Append(B);
    TriStream.Append(C);
    TriStream.RestartStrip();
}

float4 PSMain(PSInput input) : SV_TARGET {
    //return float4(1,1,1,1);
    // Vector to directional light.    
    float3 Li = normalize(float3(-10, 3, -3));
    //return float4(frac(input.positionWorld), 1);
    
    // Sea mood colors (can configure - currently using iq colors)
    float3 SEA_SHALLOW_COLOR = float3(25, 48, 56) / 255; // blue, shallow contribution
    float3 SEA_DEEP_COLOR = float3(204, 229, 153) / 255; // green, deep contribution.

    // Frag phong vectors.
    float3 P = input.positionWorld;	            // World-space position of water fragment
    float3 N = normalize(input.normalWorld);    // Normalized world-space normal of water fragment
    
    float3 positionWorldWave = computeWavePoint(P * float3(1,0,1));
    // compute normals based on nearby wave offsets.
    float3 positionWorldXPlusWave = computeWavePoint(P * float3(1,0,1) + float3(0.01f, 0, 0));
    float3 positionWorldZPlusWave = computeWavePoint(P * float3(1,0,1) + float3(0, 0, 0.01f));
    N = -normalize(cross(
        positionWorldXPlusWave - positionWorldWave, 
        positionWorldZPlusWave - positionWorldWave));

    //return float4((N - N2) * 0.5 + float3(0.5, 0.5, 0.5), 1);


    float3 V = normalize(cameraEye - P);        // Normalized direction from fragment to eye in world-space.
    float3 L = -V + 2.0f * dot(V, N) * N;       // L is not to sun! Reflective contrib is of environment (atmosphere).
    float3 H = normalize(L + V);                // Halfway between L, V.
    //return float4(N * float3(0,1,0), 1);
    //return float4(N, 1);
    //return float4(float3(1,1,1) * P.y / 20, 1);

    // Compute fresnel: how much reflection (as opposed to refraction)
    float f0 = 0.05f;
    float fresnel = f0 + (1 - f0) * pow(1.0f - dot(V, H), 3.0f); // f0 = 0, too much reflection looks bad.
    fresnel = fresnel * 0.9 + 0.1;
    //return float4(fresnel, fresnel, fresnel, 1);
    
    // Compute refracted fresnel light contribution (deeper gradient is from iq)
    float deepContribution = pow(dot(N, L) * 0.4 + 0.6, 80) * 0.12;
    float3 refracted = SEA_SHALLOW_COLOR + deepContribution * SEA_DEEP_COLOR;
    // refracted = float3(4, 57, 99) / 255.0f; // clear blue, more fantasylike.
    //refracted = float3(0,0,0);
    
    float3 rayDirection = -V;
    rayDirection = reflect(rayDirection, N);
    //rayDirection.y = abs(rayDirection.y);
    rayDirection.y = clamp(rayDirection.y, 0.08, 1);
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
    refracted += SEA_DEEP_COLOR * (P.y * 0.2 - 0.4) * 0.18 * distanceAttenuation;

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
