#ifndef SABA_WATER_COMMON_INCLUDED
#define SABA_WATER_COMMON_INCLUDED

#include "UnityCG.cginc"

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
    float2 flow = SabaSafeDirection(flowDirection);
    float2 perpendicular = float2(-flow.y, flow.x);
    float time = _Time.y * waveSpeed;

    float phaseA = dot(worldPosition.xz, flow) * waveScale + time;
    float phaseB = dot(worldPosition.xz, perpendicular) * (waveScale * 1.73) - time * 0.71;

    float2 gradient =
        cos(phaseA) * flow * waveScale +
        cos(phaseB) * perpendicular * (waveScale * 1.73) * 0.45;
    gradient *= waveStrength;

    return normalize(float3(-gradient.x, 1.0, -gradient.y));
}

inline float SabaWaterHeight(
    float3 worldPosition,
    float waveScale,
    float waveSpeed,
    float2 flowDirection)
{
    float2 flow = SabaSafeDirection(flowDirection);
    float2 perpendicular = float2(-flow.y, flow.x);
    float time = _Time.y * waveSpeed;
    return sin(dot(worldPosition.xz, flow) * waveScale + time) * 0.65
        + sin(dot(worldPosition.xz, perpendicular) * waveScale * 1.71 - time * 0.73) * 0.35;
}

inline float SabaRainRipple(
    float2 worldPosition,
    float density,
    float speed)
{
    float2 scaled = worldPosition * max(0.1, density);
    float2 cell = floor(scaled);
    float2 local = frac(scaled);
    float2 centre = lerp(0.22, 0.78, SabaHash22(cell));
    float phase = frac(_Time.y * speed + SabaHash21(cell));
    float radius = phase * 0.54;
    float width = lerp(0.035, 0.075, phase);
    float distanceToRing = abs(length(local - centre) - radius);
    float ring = 1.0 - smoothstep(width, width * 1.8, distanceToRing);
    return ring * (1.0 - phase) * step(0.08, phase);
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
