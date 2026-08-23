#ifndef SABAPROPS_FOLIAGE_CORE_INCLUDED
#define SABAPROPS_FOLIAGE_CORE_INCLUDED

// ---------------------------------------------------------------------------
// SabaProps Foliage - shared vertex helpers.
//
// Mesh channel layout expected by this shader (see FoliageMeshBuilder.cs):
//
//   POSITION   object space, the element root sits on the local XZ plane
//   NORMAL     already biased towards +Y so that back faces light plausibly
//   COLOR.rgb  baked albedo (root->tip gradient with ambient occlusion)
//   COLOR.a    per element random seed (0..1), one value per blade / petal
//   TEXCOORD0  x = across the element, y = height ratio 0..1 (the bend mask)
//   TEXCOORD3  xyz = element root in object space, w = wind stiffness (0..1)
//
// TEXCOORD3 is used deliberately: Unity reserves TEXCOORD1/2 for baked and
// realtime lightmap UVs, so anything stored there would be overwritten the
// moment a user marks the renderer as GI contributing.
// ---------------------------------------------------------------------------

// Cheap 3D -> 1D hash. Stable enough for per-instance variation and costs a
// handful of ALU in the vertex stage.
float SabaFoliageHash13(float3 p)
{
    p = frac(p * 0.3183099 + float3(0.1, 0.1, 0.1));
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

// Hue rotation around the grey axis. Cheaper than a full RGB<->HSV round trip
// and accurate enough for subtle foliage variation.
float3 SabaFoliageHueShift(float3 col, float turns)
{
    const float3 k = float3(0.57735027, 0.57735027, 0.57735027);
    float angle = turns * 6.28318531;
    float c = cos(angle);
    float s = sin(angle);
    return col * c + cross(k, col) * s + k * dot(k, col) * (1.0 - c);
}

// Per instance colour variation driven purely by the element's world position,
// so it survives prefab instancing, scene reloads and mesh merging without any
// per-instance property block (which Unity does not serialise anyway).
float3 SabaFoliageVaryColor(float3 albedo, float seed, float hueVar, float satVar, float valVar)
{
    float r0 = seed * 2.0 - 1.0;
    float r1 = frac(seed * 7.31) * 2.0 - 1.0;
    float r2 = frac(seed * 13.77) * 2.0 - 1.0;

    albedo = SabaFoliageHueShift(albedo, r0 * hueVar);

    float luma = dot(albedo, float3(0.299, 0.587, 0.114));
    albedo = lerp(luma.xxx, albedo, saturate(1.0 + r1 * satVar));

    albedo *= saturate(1.0 + r2 * valVar);
    return albedo;
}

// World space wind displacement.
//   rootWS   - element root, keeps every vertex of one blade in phase
//   posWS    - the vertex itself, used so tall elements lag behind slightly
//   bend     - 0 at the root, 1 at the tip (already raised to _BendPower)
//   seed     - per element random, breaks up the shared wave
float3 SabaFoliageWind(
    float3 rootWS, float3 posWS, float bend, float seed,
    float2 windDir, float strength, float speed,
    float waveLength, float turbulence, float gustStrength)
{
    float2 dir = normalize(windDir + float2(1e-5, 0.0));
    float travel = dot(rootWS.xz, dir) / max(waveLength, 0.5);
    float phase = travel * 6.28318531 + _Time.y * speed + seed * 6.28318531;

    // Two octaves keep the motion from looking like a pure sine.
    float wave = sin(phase) * 0.65 + sin(phase * 2.37 + 1.7) * 0.35;

    // Slow, large scale gusts that sweep across the whole field.
    float gust = sin(travel * 1.13 + _Time.y * speed * 0.27) * 0.5 + 0.5;
    gust = lerp(1.0, gust * 1.6, gustStrength);

    // High frequency flutter, strongest at the very tip.
    float flutter = sin(phase * 4.13 + seed * 27.0) * turbulence * 0.18;

    float amount = bend * strength * gust;
    float sway = (wave + flutter) * amount;

    float3 offset = float3(dir.x, 0.0, dir.y) * sway;

    // Pull the tip down as it bends over so the element does not visibly
    // stretch. Approximates arc-length preservation for free.
    offset.y -= abs(sway) * 0.35;

    // Taller vertices lag a touch behind their root, adding a whip feel.
    offset *= 1.0 + saturate((posWS.y - rootWS.y) * 0.15);

    return offset;
}

#endif // SABAPROPS_FOLIAGE_CORE_INCLUDED
