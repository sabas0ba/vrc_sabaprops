#ifndef SABA_WATER_COMMON_INCLUDED
#define SABA_WATER_COMMON_INCLUDED

#include "UnityCG.cginc"

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

inline float SabaRainRippleLayer(
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
    float distanceToRing = abs(length(local - centre) - radius);
    float ring = 1.0 - smoothstep(width, width * 1.8, distanceToRing);
    float intermittent = step(SabaHash21(identity + 17.41), 0.72);
    return ring * (1.0 - phase) * step(0.055, phase) * intermittent;
}

inline float SabaRainRipple(
    float2 worldPosition,
    float density,
    float speed)
{
    // Independent rotated grids may overlap. This removes the one-ring-per-cell
    // cadence while retaining a fixed, texture-free shader cost.
    float layerA = SabaRainRippleLayer(
        worldPosition, density * 0.73, speed, float2(0.9397, 0.3420), float2(1.7, 9.2), 11.3);
    float layerB = SabaRainRippleLayer(
        worldPosition, density * 1.07, speed * 0.91, float2(0.4695, 0.8829), float2(8.1, 2.4), 47.9);
    float layerC = SabaRainRippleLayer(
        worldPosition, density * 1.43, speed * 1.13, float2(-0.2079, 0.9781), float2(4.6, 6.8), 83.1);
    return saturate(layerA * 0.72 + layerB * 0.58 + layerC * 0.46);
}

inline float SabaUvEdgeFade(float2 uv, float fadeWidth)
{
    float enabled = step(1e-4, fadeWidth);
    float2 centred = abs(uv * 2.0 - 1.0);
    float distanceToEdge = 1.0 - max(centred.x, centred.y);
    float faded = smoothstep(0.0, max(fadeWidth, 1e-4), distanceToEdge);
    return lerp(1.0, faded, enabled);
}

inline float3 SabaReflectionProbe(float3 viewDirection, float3 normal)
{
    float3 reflected = reflect(-viewDirection, normal);
    half4 encoded = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, reflected);
    return DecodeHDR(encoded, unity_SpecCube0_HDR);
}

#endif
