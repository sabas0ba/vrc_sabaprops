#ifndef SABA_WATER_COMMON_INCLUDED
#define SABA_WATER_COMMON_INCLUDED

#include "UnityCG.cginc"
#include "Lighting.cginc"
#include "UnityStandardUtils.cginc"

inline float SabaWaterHeight(
    float3 worldPosition,
    float waveScale,
    float waveSpeed,
    float2 flowDirection);

inline float SabaHash21(float2 value)
{
    value = frac(value * float2(123.34, 456.21));
    value += dot(value, value + 45.32);
    return frac(value.x * value.y);
}

inline float2 SabaHash22(float2 value)
{
    float first = SabaHash21(value);
    return float2(first, SabaHash21(value + first + 19.19));
}

inline float2 SabaSafeDirection(float2 direction)
{
    float lengthSquared = dot(direction, direction);
    return lengthSquared > 1e-5 ? direction * rsqrt(lengthSquared) : float2(1.0, 0.0);
}

inline float3 SabaWaterProbeLighting(float3 worldNormal)
{
    // Unity supplies per-renderer Light Probe SH coefficients here. When a
    // renderer has no probe, the same function falls back to ambient SH.
    return max(0.0, ShadeSH9(float4(normalize(worldNormal), 1.0)));
}

inline float3 SabaWaterBaseLighting(
    float3 worldNormal,
    float3 worldPosition,
    float attenuation)
{
    float3 lightDirection = normalize(UnityWorldSpaceLightDir(worldPosition));
    float diffuse = saturate(dot(normalize(worldNormal), lightDirection));
    return SabaWaterProbeLighting(worldNormal) + _LightColor0.rgb * diffuse * attenuation;
}

inline float SabaTideOffset(float tideHeight, float tideSpeed)
{
    return sin(_Time.y * tideSpeed) * tideHeight;
}

inline float3 SabaWaterNormal(
    float3 worldPosition,
    float waveScale,
    float waveStrength,
    float waveSpeed,
    float2 flowDirection)
{
    // Keep the fragment normal matched to the vertex displacement. A finite
    // difference costs two extra evaluations, but avoids the visibly regular
    // crossing pattern produced by two analytic sine waves.
    float sampleDistance = max(0.018, 0.11 / max(0.1, waveScale));
    float centre = SabaWaterHeight(worldPosition, waveScale, waveSpeed, flowDirection);
    float heightX = SabaWaterHeight(
        worldPosition + float3(sampleDistance, 0.0, 0.0),
        waveScale,
        waveSpeed,
        flowDirection);
    float heightZ = SabaWaterHeight(
        worldPosition + float3(0.0, 0.0, sampleDistance),
        waveScale,
        waveSpeed,
        flowDirection);
    float2 gradient = float2(heightX - centre, heightZ - centre) / sampleDistance;
    return normalize(float3(-gradient.x * waveStrength, 1.0, -gradient.y * waveStrength));
}

inline float SabaWaterHeight(
    float3 worldPosition,
    float waveScale,
    float waveSpeed,
    float2 flowDirection)
{
    float2 flow = SabaSafeDirection(flowDirection);
    float2 directionB = float2(
        flow.x * 0.358 - flow.y * 0.934,
        flow.x * 0.934 + flow.y * 0.358);
    float2 directionC = float2(
        flow.x * -0.615 - flow.y * 0.788,
        flow.x * 0.788 + flow.y * -0.615);
    float2 directionD = float2(
        flow.x * -0.891 - flow.y * -0.454,
        flow.x * -0.454 + flow.y * -0.891);
    float time = _Time.y * waveSpeed;
    float2 position = worldPosition.xz;

    // Irrational scale ratios, independent travel speeds and a low-frequency
    // domain warp keep Wave Scale useful without exposing a tiled wave lattice.
    float warp = sin(dot(position, directionD) * waveScale * 0.173 - time * 0.29);
    float waveA = sin(dot(position, flow) * waveScale + time + warp * 0.31);
    float waveB = sin(dot(position, directionB) * waveScale * 1.618 - time * 0.73 - warp * 0.18);
    float waveC = sin(dot(position, directionC) * waveScale * 0.731 + time * 1.21 + warp * 0.23);
    float waveD = sin(dot(position, directionD) * waveScale * 2.173 - time * 1.47);
    return waveA * 0.43 + waveB * 0.27 + waveC * 0.19 + waveD * 0.11;
}

inline float3 SabaRainRippleLayerData(
    float2 worldPosition,
    float density,
    float speed,
    float2 basisX,
    float2 offset,
    float layerSeed)
{
    float2 basisY = float2(-basisX.y, basisX.x);
    float2 rotated = float2(dot(worldPosition, basisX), dot(worldPosition, basisY));
    float2 scaled = (rotated + offset) * max(0.1, density);
    float2 cell = floor(scaled);
    float2 local = frac(scaled);
    float2 identity = cell + layerSeed;
    float2 centre = lerp(0.16, 0.84, SabaHash22(identity));
    float phaseOffset = SabaHash21(identity + 31.71);
    float period = lerp(0.61, 1.43, SabaHash21(identity + 7.13));
    float phase = frac(_Time.y * speed * period + phaseOffset);
    float radius = phase * lerp(0.42, 0.68, SabaHash21(identity + 83.17));
    float width = lerp(0.025, 0.07, phase);
    float2 delta = local - centre;
    float radialDistance = max(length(delta), 1e-4);
    float signedDistance = radialDistance - radius;
    float distanceToRing = abs(signedDistance);
    float ring = 1.0 - smoothstep(width, width * 1.8, distanceToRing);
    float intermittent = step(SabaHash21(identity + 17.41), 0.72);
    float visibility = (1.0 - phase) * step(0.055, phase) * intermittent;
    float2 radialGradient = delta / radialDistance * sign(signedDistance) * ring * visibility;
    float2 worldGradient = basisX * radialGradient.x + basisY * radialGradient.y;
    return float3(ring * visibility, worldGradient);
}

inline float3 SabaRainRippleData(
    float2 worldPosition,
    float density,
    float speed)
{
    // Independent rotated grids may overlap. This removes the one-ring-per-cell
    // cadence while retaining a fixed, texture-free shader cost.
    float3 layerA = SabaRainRippleLayerData(
        worldPosition, density * 0.73, speed, float2(0.9397, 0.3420), float2(1.7, 9.2), 11.3);
    float3 layerB = SabaRainRippleLayerData(
        worldPosition, density * 1.07, speed * 0.91, float2(0.4695, 0.8829), float2(8.1, 2.4), 47.9);
    float3 layerC = SabaRainRippleLayerData(
        worldPosition, density * 1.43, speed * 1.13, float2(-0.2079, 0.9781), float2(4.6, 6.8), 83.1);
    float3 data = layerA * 0.72 + layerB * 0.58 + layerC * 0.46;
    data.x = saturate(data.x);
    return data;
}

inline float SabaRainRipple(
    float2 worldPosition,
    float density,
    float speed)
{
    return SabaRainRippleData(worldPosition, density, speed).x;
}

inline float SabaCrestFoamLite(
    float3 worldPosition,
    float waveScale,
    float waveSpeed,
    float2 flowDirection,
    float threshold,
    float crestWidth,
    float detailStrength,
    float patternScale,
    float patternSpeed,
    float patternWarp)
{
    float height = SabaWaterHeight(worldPosition, waveScale, waveSpeed, flowDirection) * 0.5 + 0.5;
    float lowerEdge = max(0.0, threshold - max(0.01, crestWidth));
    float crest = smoothstep(lowerEdge, threshold, height)
        * (1.0 - smoothstep(threshold, min(1.0, threshold + max(0.01, crestWidth)), height));
    float2 flow = SabaSafeDirection(flowDirection);
    float2 position = worldPosition.xz * waveScale * max(0.1, patternScale);
    float patternTime = _Time.y * waveSpeed * patternSpeed;
    float domainWarp = sin(dot(position, flow) * 0.47 - patternTime * 0.31)
        * patternWarp;
    float detail = 0.5 + 0.5 * sin(
        dot(position, float2(-flow.y, flow.x)) * 5.17 + domainWarp * 2.3
        + dot(position, flow) * 1.31
        - patternTime * 2.3);
    return crest * lerp(1.0, smoothstep(0.24, 0.78, detail), detailStrength);
}

inline float2 SabaBreakingFoam(
    float3 worldPosition,
    float waveScale,
    float waveSpeed,
    float2 flowDirection,
    float threshold,
    float crestWidth,
    float trailStrength,
    float detailStrength,
    float patternScale,
    float patternSpeed,
    float patternWarp)
{
    float2 flow = SabaSafeDirection(flowDirection);
    float sampleDistance = max(0.08, 0.38 / max(0.1, waveScale));
    float3 flowOffset = float3(flow.x, 0.0, flow.y) * sampleDistance;
    float height = SabaWaterHeight(worldPosition, waveScale, waveSpeed, flowDirection) * 0.5 + 0.5;
    float ahead = SabaWaterHeight(
        worldPosition + flowOffset, waveScale, waveSpeed, flowDirection) * 0.5 + 0.5;
    float behind = SabaWaterHeight(
        worldPosition - flowOffset, waveScale, waveSpeed, flowDirection) * 0.5 + 0.5;
    float slope = saturate(abs(ahead - behind) / max(0.01, crestWidth));
    float crestBand = smoothstep(threshold - crestWidth, threshold, height)
        * (1.0 - smoothstep(threshold, threshold + crestWidth, height));
    float leadingFace = saturate((behind - ahead) * 4.0 + 0.45);

    float2 advected = worldPosition.xz * waveScale * max(0.1, patternScale)
        - flow * (_Time.y * waveSpeed * patternSpeed * 0.73);
    float warpPhase = sin(dot(advected, float2(0.73, 1.19))
        - _Time.y * waveSpeed * patternSpeed * 0.37) * patternWarp;
    float breakup = 0.5 + 0.28 * sin(
        dot(advected, float2(4.37, -3.11)) + warpPhase * 2.7)
        + 0.22 * sin(dot(advected, float2(-7.19, 5.83)) - warpPhase * 1.9 + 1.7);
    breakup = lerp(1.0, smoothstep(0.18, 0.78, breakup), detailStrength);
    float activeWhitecap = crestBand * leadingFace * lerp(0.55, 1.0, slope) * breakup;

    // A shifted, wider band approximates persistent Stage-B foam carried
    // behind a breaking crest without storing a simulation history texture.
    float remnantHeight = SabaWaterHeight(
        worldPosition - flowOffset * 2.4, waveScale, waveSpeed, flowDirection) * 0.5 + 0.5;
    float remnantBand = smoothstep(threshold - crestWidth * 3.2, threshold, remnantHeight)
        * (1.0 - smoothstep(threshold, threshold + crestWidth * 2.2, remnantHeight));
    float remnantBreakup = smoothstep(0.08, 0.7, 0.5 + 0.5 * sin(
        dot(advected, float2(8.31, 2.73)) + warpPhase * 3.1
        - _Time.y * waveSpeed * patternSpeed * 0.27));
    float remnant = remnantBand * remnantBreakup * trailStrength;
    return float2(activeWhitecap, remnant);
}

inline float3 SabaFlowTurbulence(
    float2 uv,
    float3 worldPosition,
    float flowScale,
    float flowSpeed)
{
    float time = _Time.y * flowSpeed;
    float scale = max(0.1, flowScale);
    float across = uv.x * 2.0 - 1.0;
    float downstream = uv.y * scale;
    float broadPhase = downstream * 6.283 - time * 1.9
        + sin(across * 7.3 + time * 0.31) * 0.72;
    float finePhase = downstream * 17.17 - time * 4.1
        - across * 13.31 + sin(downstream * 2.7) * 0.48;
    float vortexPhase = dot(worldPosition.xz, float2(2.17, -1.63)) - time * 1.37;
    float broad = sin(broadPhase);
    float fine = sin(finePhase);
    float vortex = sin(vortexPhase);
    float foam = saturate(0.46 + broad * 0.26 + fine * 0.17 + vortex * 0.11);
    return float3(foam, cos(broadPhase) * 0.7 + cos(finePhase) * 0.3, cos(finePhase));
}

inline float SabaUvEdgeFade(float2 uv, float fadeWidth)
{
    float enabled = step(1e-4, fadeWidth);
    float2 centred = abs(uv * 2.0 - 1.0);
    float distanceToEdge = 1.0 - max(centred.x, centred.y);
    float faded = smoothstep(0.0, max(fadeWidth, 1e-4), distanceToEdge);
    return lerp(1.0, faded, enabled);
}

inline float SabaBoundaryDirectionMask(
    float3 worldNormal,
    float4 upDown,
    float4 cardinal,
    float4 diagonal)
{
    float3 normal = normalize(worldNormal);
    if (abs(normal.y) >= max(abs(normal.x), abs(normal.z)))
    {
        return normal.y >= 0.0 ? upDown.x : upDown.y;
    }

    float angle = atan2(normal.x, normal.z);
    float sector = frac(angle / 6.2831853 + 1.0625) * 8.0;
    int index = (int)floor(sector);
    if (index == 0) return cardinal.x;
    if (index == 1) return diagonal.x;
    if (index == 2) return cardinal.y;
    if (index == 3) return diagonal.y;
    if (index == 4) return cardinal.z;
    if (index == 5) return diagonal.z;
    if (index == 6) return cardinal.w;
    return diagonal.w;
}

inline float3 SabaReflectionProbe(
    float3 viewDirection,
    float3 normal,
    float3 worldPosition,
    float roughness)
{
    float3 reflected = reflect(-viewDirection, normal);
#if defined(UNITY_SPECCUBE_BOX_PROJECTION)
    reflected = BoxProjectedCubemapDirection(
        reflected,
        worldPosition,
        unity_SpecCube0_ProbePosition,
        unity_SpecCube0_BoxMin,
        unity_SpecCube0_BoxMax);
#endif
    half4 encoded = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, reflected, saturate(roughness) * 6.0);
    float3 reflection = DecodeHDR(encoded, unity_SpecCube0_HDR);

#if defined(UNITY_SPECCUBE_BLENDING)
    float blend = unity_SpecCube0_BoxMin.w;
    if (blend < 0.99999)
    {
        float3 reflectedB = reflect(-viewDirection, normal);
#if defined(UNITY_SPECCUBE_BOX_PROJECTION)
        reflectedB = BoxProjectedCubemapDirection(
            reflectedB,
            worldPosition,
            unity_SpecCube1_ProbePosition,
            unity_SpecCube1_BoxMin,
            unity_SpecCube1_BoxMax);
#endif
        half4 encodedB = UNITY_SAMPLE_TEXCUBE_SAMPLER_LOD(
            unity_SpecCube1,
            unity_SpecCube0,
            reflectedB,
            saturate(roughness) * 6.0);
        reflection = lerp(DecodeHDR(encodedB, unity_SpecCube1_HDR), reflection, blend);
    }
#endif
    return reflection;
}

#endif
