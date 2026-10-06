#ifndef SABA_SCREEN_FX_CORE_INCLUDED
#define SABA_SCREEN_FX_CORE_INCLUDED

#include "UnityCG.cginc"
#include "UnityLightingCommon.cginc"

// Shared by SabaScreenFx.shader (GrabPass) and SabaScreenFxLite.shader
// (blend only). The renderer is a unit cube: the camera position inside the
// cube decides the weight, and the +Z face is expanded to cover the screen.
//
// Coordinate spaces used below:
//   tangent  view-space direction divided by depth (x right, y up). Both eyes
//            share it, so a pattern drawn in it sits at infinity in VR.
//   q        tangent scaled so the frustum edges are near +-1. Used for
//            anything tied to the edge of the view.

float _Weight;
float _EdgeFade;
float _LightResponse;

float _Wobble;
float _WobbleSpeed;
float _WobbleScale;
float _Haze;
float _HazeScale;
float _HazeSpeed;
float _DoubleVision;

float _Blur;
float _BlurEdge;
float _RadialBlur;
float _Chromatic;

float _LensDrops;
float _LensDropScale;
float _LensDropSlide;
float _Frost;
fixed4 _FrostColor;
float _Splat;
fixed4 _SplatColor;
float _SplatScale;
float _SplatDrip;

float _Exposure;
float _Saturation;
float _Contrast;
fixed4 _Tint;

fixed4 _FogColor;
float _FogVeil;
float _FogDensity;
float _FogNoise;

float _Glare;
fixed4 _GlareColor;
float4 _GlareDirection;
float _GlareFocus;

float _Vignette;
fixed4 _VignetteColor;
float _VignetteRadius;
float _VignetteSoftness;
float _Pulse;
float _PulseRate;

float _Particle;
fixed4 _ParticleColor;
float _ParticleSize;
float _ParticleDensity;
float4 _ParticleVelocity;
float _ParticleStretch;
float _ParticleSway;

float _SpeedLines;
fixed4 _SpeedLineColor;
float _SpeedLineInner;
float _SpeedLineRate;

float _Caustics;
float _CausticsScale;

float _Grain;

float _Blink;
float _BlinkAuto;
float _BlinkRate;

// Set by the VRChat client: 0 outside mirrors. Stays 0 in the editor.
float _VRChatMirrorMode;

#define SABA_FX_TWO_PI 6.2831853
// Half of a typical interpupillary distance, in metres.
#define SABA_FX_HALF_IPD 0.032

struct SabaFxAppdata
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct SabaFxV2f
{
    float4 pos : SV_POSITION;
    // xy: GrabPass uv, zw: depth texture uv.
    float4 screenUv : TEXCOORD0;
    // xy: q, zw: tangent.
    float4 view : TEXCOORD1;
    // x: weight, y: eye sign (-1 left, +1 right, 0 mono), zw: screen direction of world down.
    float4 params : TEXCOORD2;
    float3 light : TEXCOORD3;
    float3 ray : TEXCOORD4;
    UNITY_VERTEX_OUTPUT_STEREO
};

struct SabaFxContext
{
    float2 q;
    float2 tangent;
    float2 pixel;
    float2 fall;
    float3 ray;
    float3 light;
    float weight;
    float eyeSign;
    float beat;
};

struct SabaFxLens
{
    float2 offset;
    float dropSpecular;
    float frostMask;
    float frostRidge;
};

inline float SabaFxHash21(float2 value)
{
    value = frac(value * float2(123.34, 456.21));
    value += dot(value, value + 45.32);
    return frac(value.x * value.y);
}

inline float2 SabaFxHash22(float2 value)
{
    float first = SabaFxHash21(value);
    return float2(first, SabaFxHash21(value + first + 19.19));
}

inline float SabaFxNoise(float2 position)
{
    float2 cell = floor(position);
    float2 blend = frac(position);
    blend = blend * blend * (3.0 - 2.0 * blend);
    float bottom = lerp(SabaFxHash21(cell), SabaFxHash21(cell + float2(1.0, 0.0)), blend.x);
    float top = lerp(
        SabaFxHash21(cell + float2(0.0, 1.0)), SabaFxHash21(cell + float2(1.0, 1.0)), blend.x);
    return lerp(bottom, top, blend.y);
}

// Composites a layer of the given coverage over what has been accumulated.
// The scene colour is multiplied by transmittance at the end, so the same
// accumulation serves both the GrabPass and the blend-only shader.
inline void SabaFxOver(inout float3 add, inout float transmittance, float3 colour, float coverage)
{
    coverage = saturate(coverage);
    add = add * (1.0 - coverage) + colour * coverage;
    transmittance *= 1.0 - coverage;
}

inline float SabaFxVolumeWeight()
{
#if defined(USING_STEREO_MATRICES)
    // Both eyes must agree, or the effect flickers per eye at the boundary.
    float3 cameraPosition =
        (unity_StereoWorldSpaceCameraPos[0] + unity_StereoWorldSpaceCameraPos[1]) * 0.5;
#else
    float3 cameraPosition = _WorldSpaceCameraPos.xyz;
#endif
    float3 local = mul(unity_WorldToObject, float4(cameraPosition, 1.0)).xyz;
    float3 axisScale = float3(
        length(unity_ObjectToWorld._m00_m10_m20),
        length(unity_ObjectToWorld._m01_m11_m21),
        length(unity_ObjectToWorld._m02_m12_m22));
    float3 inside = (0.5 - abs(local)) * axisScale;
    float nearest = min(min(inside.x, inside.y), inside.z);
    float fade = _EdgeFade > 1e-4 ? saturate(nearest / _EdgeFade) : step(0.0, nearest);
    return fade * saturate(_Weight);
}

SabaFxV2f SabaFxVert(SabaFxAppdata input)
{
    SabaFxV2f output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_OUTPUT(SabaFxV2f, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float weight = SabaFxVolumeWeight();
    // A mirror would apply the effect to its own image and the main view
    // would apply it again on top.
    float visible = step(0.5, input.normal.z) * step(1e-4, weight) * step(_VRChatMirrorMode, 0.5);

#if defined(UNITY_REVERSED_Z)
    float nearDepth = 0.999;
#else
    float nearDepth = UNITY_NEAR_CLIP_VALUE + 0.001;
#endif
    // The other five faces, and the whole cube while the weight is zero,
    // collapse to a point outside the clip volume.
    output.pos = visible > 0.5
        ? float4(input.uv * 2.0 - 1.0, nearDepth, 1.0)
        : float4(2.0, 2.0, 2.0, 1.0);

    // unity_CameraInvProjection inverts the unflipped OpenGL-style projection.
    float2 glNdc = float2(output.pos.x, output.pos.y * _ProjectionParams.x);
    float4 viewRay = mul(unity_CameraInvProjection, float4(glNdc, 1.0, 1.0));
    viewRay.xyz /= viewRay.w;
    float2 tangent = viewRay.xy / max(1e-5, -viewRay.z);

    output.view.xy = tangent * float2(unity_CameraProjection._m00, unity_CameraProjection._m11);
    output.view.zw = tangent;
    output.ray = mul((float3x3)UNITY_MATRIX_I_V, viewRay.xyz);

    output.params.x = weight;
#if defined(USING_STEREO_MATRICES)
    output.params.y = unity_StereoEyeIndex * 2.0 - 1.0;
#endif
    float2 down = mul((float3x3)UNITY_MATRIX_V, float3(0.0, -1.0, 0.0)).xy;
    output.params.zw = normalize(down + float2(0.0, -1e-4));

    float3 ambient = max(0.0, ShadeSH9(float4(0.0, 1.0, 0.0, 1.0)));
    output.light = lerp(
        float3(1.0, 1.0, 1.0), saturate(ambient + _LightColor0.rgb * 0.5), _LightResponse);

#if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
    // Array-backed eye textures use full-range UVs for each slice.
    output.screenUv.xy = ComputeNonStereoScreenPos(output.pos).xy;
    output.screenUv.zw = output.screenUv.xy;
#else
    output.screenUv.xy = ComputeGrabScreenPos(output.pos).xy;
    output.screenUv.zw = UnityStereoTransformScreenSpaceTex(ComputeNonStereoScreenPos(output.pos).xy);
#endif
    return output;
}

inline SabaFxContext SabaFxMakeContext(SabaFxV2f input)
{
    SabaFxContext context;
    context.q = input.view.xy;
    context.tangent = input.view.zw;
    context.pixel = floor(input.screenUv.zw * _ScreenParams.xy);
    context.fall = input.params.zw;
    context.ray = input.ray;
    context.light = input.light;
    context.weight = input.params.x;
    context.eyeSign = input.params.y;

    // Two beats and a rest per cycle.
    float phase = frac(_Time.y * _PulseRate);
    float first = saturate(sin(phase * SABA_FX_TWO_PI));
    float second = saturate(sin((phase - 0.3) * SABA_FX_TWO_PI));
    first *= first;
    second *= second;
    context.beat = first * first + 0.6 * second * second;
    return context;
}

inline void SabaFxDropLayer(
    float2 position, float scale, float slide, float amount, float seed, inout SabaFxLens lens)
{
    float2 grid = position * scale;
    float column = floor(grid.x);
    // Each column slides at its own rate so neighbouring drops do not move in step.
    grid.y += _Time.y * slide * (0.3 + 0.7 * SabaFxHash21(float2(column, seed)))
        + SabaFxHash21(float2(seed, column)) * 13.0;
    float2 cell = floor(grid);
    float2 local = frac(grid) - 0.5;
    float present = step(SabaFxHash21(cell + seed), amount);
    float2 random = SabaFxHash22(cell + seed + 31.7);
    float radius = lerp(0.1, 0.28, random.x);
    float2 delta = local - (random - 0.5) * 0.4;
    delta.y *= 0.85;
    float mask = present * (1.0 - smoothstep(0.75, 1.0, length(delta) / radius));

    // Sampling past the centre gives the small inverted image a drop shows.
    lens.offset -= delta / scale * mask * 1.6;
    float2 normal = delta / radius;
    float highlight = saturate(dot(normal, float2(-0.45, 0.6)));
    lens.dropSpecular += mask * highlight * highlight * highlight;
}

inline SabaFxLens SabaFxComputeLens(SabaFxContext context)
{
    SabaFxLens lens;
    lens.offset = float2(0.0, 0.0);
    lens.dropSpecular = 0.0;
    lens.frostMask = 0.0;
    lens.frostRidge = 0.0;

    float drops = _LensDrops * context.weight;
    UNITY_BRANCH
    if (drops > 1e-3)
    {
        SabaFxDropLayer(context.q, _LensDropScale, _LensDropSlide, drops * 0.7, 3.7, lens);
        SabaFxDropLayer(
            context.q + 4.3, _LensDropScale * 2.3, _LensDropSlide * 0.35, drops * 0.5, 11.1, lens);
    }

    float frost = _Frost * context.weight;
    UNITY_BRANCH
    if (frost > 1e-3)
    {
        float coarse = SabaFxNoise(context.q * 9.0 + 3.1);
        float fine = SabaFxNoise(context.q * 23.0 - 7.7);
        float ridge = 1.0 - abs(fine * 2.0 - 1.0);
        float edge = length(context.q) + (coarse - 0.5) * 0.5 + (ridge - 0.5) * 0.15;
        float threshold = lerp(1.7, 0.3, frost);
        lens.frostMask = smoothstep(threshold, threshold + 0.4, edge);
        lens.frostRidge = ridge;
        lens.offset += (float2(coarse, fine) - 0.5) * lens.frostMask * 0.08;
    }

    return lens;
}

// Screen-space offset in q units, applied before the scene is sampled.
inline float2 SabaFxDistortion(SabaFxContext context)
{
    float2 offset = float2(0.0, 0.0);
    float2 q = context.q;

    float wobble = _Wobble * context.weight;
    UNITY_BRANCH
    if (wobble > 1e-3)
    {
        float time = _Time.y * _WobbleSpeed;
        float2 position = q * _WobbleScale;
        offset += wobble * 0.05 * float2(
            sin(position.y * 2.1 + time * 1.3) + sin(time * 0.7),
            cos(position.x * 1.7 - time * 1.1) + sin(time * 0.9 + 1.3));
        // A slow roll and a slow zoom on top of the sway.
        offset += float2(-q.y, q.x) * (wobble * 0.08 * sin(time * 0.53));
        offset += q * (wobble * 0.04 * sin(time * 0.41));
    }

    float haze = _Haze * context.weight;
    UNITY_BRANCH
    if (haze > 1e-3)
    {
        float2 position = context.tangent * _HazeScale;
        position.y -= _Time.y * _HazeSpeed;
        offset += (float2(SabaFxNoise(position), SabaFxNoise(position + 17.3)) - 0.5) * haze * 0.03;
    }

    offset -= q * (context.beat * _Pulse * context.weight * 0.015);
    return offset;
}

inline float3 SabaFxGrade(float3 colour, SabaFxContext context)
{
    float weight = context.weight;
    colour *= exp2(_Exposure * weight);
    colour *= lerp(float3(1.0, 1.0, 1.0), _Tint.rgb, _Tint.a * weight);
    float luminance = dot(colour, float3(0.2126, 0.7152, 0.0722));
    colour = lerp(float3(luminance, luminance, luminance), colour, lerp(1.0, _Saturation, weight));
    colour = (colour - 0.18) * lerp(1.0, _Contrast, weight) + 0.18;
    return max(colour, 0.0);
}

// The blend-only shader cannot multiply the scene per channel, so exposure
// and tint are approximated by a neutral darkening and a thin coloured veil.
inline void SabaFxGradeLite(SabaFxContext context, inout float3 add, inout float transmittance)
{
    float exposure = _Exposure * context.weight;
    transmittance *= exp2(min(exposure, 0.0));
    add += context.light * (max(exposure, 0.0) * 0.06);

    float tintLuminance = dot(_Tint.rgb, float3(0.2126, 0.7152, 0.0722));
    SabaFxOver(
        add, transmittance, _Tint.rgb * context.light * 0.3,
        _Tint.a * context.weight * 0.6 * saturate(1.2 - tintLuminance));
}

inline float SabaFxParticleLayer(
    float2 position, float2 fall, float amount, float depthScale, float seed)
{
    float2 velocity = fall * _ParticleVelocity.y
        + float2(-fall.y, fall.x) * _ParticleVelocity.x;
    float speed = length(velocity);
    float2 along = speed > 1e-4 ? velocity / speed : fall;
    float2 across = float2(-along.y, along.x);

    // The grid's y axis follows the motion, so dividing it stretches every
    // particle along its own path.
    float2 grid = float2(
        dot(position, across),
        dot(position, along) - _Time.y * speed / depthScale);
    grid *= _ParticleDensity * depthScale;
    grid.y /= 1.0 + _ParticleStretch;
    grid.y += SabaFxHash21(float2(floor(grid.x), seed)) * 37.0;

    float2 cell = floor(grid);
    float2 local = frac(grid) - 0.5;
    float present = step(SabaFxHash21(cell + seed), amount);
    float2 random = SabaFxHash22(cell + seed + 31.7);
    float2 centre = (random - 0.5) * 0.3;
    centre.x += sin(_Time.y * (0.8 + random.y * 1.7) + random.x * SABA_FX_TWO_PI)
        * _ParticleSway * 0.1;
    float radius = min(0.25, _ParticleSize * (0.6 + 0.4 * random.y));
    return present * (1.0 - smoothstep(radius * 0.4, radius, length(local - centre)));
}

inline float SabaFxParticles(SabaFxContext context, float amount)
{
    // Three layers at 1.5 m, 2.6 m and 4.4 m. The per-eye shift gives each
    // layer its own stereo depth instead of leaving all of them at infinity.
    float total = 0.0;
    float depthScale = 1.0;
    for (int layer = 0; layer < 3; layer++)
    {
        float2 position = context.tangent;
        position.x += context.eyeSign * SABA_FX_HALF_IPD / (1.5 * depthScale);
        total += SabaFxParticleLayer(position, context.fall, amount, depthScale, 5.3 + layer * 17.0)
            / depthScale;
        depthScale *= 1.7;
    }

    return saturate(total);
}

inline float SabaFxSpeedLines(float2 q, float amount)
{
    float radius = length(q);
    float angle = (atan2(q.y, q.x) / SABA_FX_TWO_PI + 0.5) * 90.0;
    float across = abs(frac(angle) - 0.5) * 2.0;
    float2 random = SabaFxHash22(float2(floor(angle), 7.3));
    float present = step(random.x, amount * 0.6);
    float travel = frac(radius * 0.35 - _Time.y * _SpeedLineRate * (0.5 + random.y) + random.x * 9.0);
    float segment = smoothstep(0.0, 0.15, travel) * (1.0 - smoothstep(0.35, 0.7, travel));
    float core = 1.0 - smoothstep(0.0, 0.35, across);
    float periphery = smoothstep(_SpeedLineInner, _SpeedLineInner + 0.6, radius);
    return present * segment * core * periphery;
}

inline float SabaFxSplat(float2 q, float amount)
{
    float2 position = q * _SplatScale;
    position.y += _Time.y * _SplatDrip;
    float noise = SabaFxNoise(position) * 0.65 + SabaFxNoise(position * 2.7 + 11.3) * 0.35;
    float threshold = lerp(0.95, 0.3, amount);
    return smoothstep(threshold, threshold + 0.07, noise);
}

// Everything that can be expressed as a layer over the scene. depthFog is
// the distance fog the GrabPass shader derives from scene depth, 0 otherwise.
inline void SabaFxOverlay(
    SabaFxContext context, SabaFxLens lens, float depthFog,
    inout float3 add, inout float transmittance)
{
    float weight = context.weight;
    float2 q = context.q;

    float caustics = _Caustics * weight;
    UNITY_BRANCH
    if (caustics > 1e-3)
    {
        float2 position = context.tangent * _CausticsScale;
        float first = sin(position.x + _Time.y * 1.3 + sin(position.y));
        float second = sin(position.y * 1.37 - _Time.y + sin(position.x * 0.81));
        float pattern = saturate(1.0 - abs(first - second));
        pattern *= pattern * pattern;
        add += context.light * (pattern * pattern * caustics * 0.3);
    }

    float fog = 1.0 - (1.0 - depthFog) * (1.0 - saturate(_FogVeil * weight));
    UNITY_BRANCH
    if (fog > 1e-3)
    {
        float wisps = SabaFxNoise(context.tangent * 1.8 + _Time.y * float2(0.05, 0.01));
        fog *= lerp(1.0, saturate(wisps * 1.6), _FogNoise);
        SabaFxOver(add, transmittance, _FogColor.rgb * context.light, fog);
    }

    float glare = _Glare * weight;
    UNITY_BRANCH
    if (glare > 1e-3)
    {
        // A zero direction means a uniform wash; otherwise the wash peaks
        // while looking towards the light.
        float lengthSquared = dot(_GlareDirection.xyz, _GlareDirection.xyz);
        float focus = 1.0;
        if (lengthSquared > 1e-4)
        {
            float alignment = saturate(
                dot(normalize(context.ray), _GlareDirection.xyz * rsqrt(lengthSquared)));
            focus = 0.12 + pow(alignment, _GlareFocus);
        }

        add += _GlareColor.rgb * (glare * focus * 1.5);
    }

    float particles = _Particle * weight;
    UNITY_BRANCH
    if (particles > 1e-3)
    {
        SabaFxOver(
            add, transmittance, _ParticleColor.rgb * context.light,
            SabaFxParticles(context, particles) * _ParticleColor.a);
    }

    float speedLines = _SpeedLines * weight;
    UNITY_BRANCH
    if (speedLines > 1e-3)
    {
        SabaFxOver(
            add, transmittance, _SpeedLineColor.rgb,
            SabaFxSpeedLines(q, speedLines) * _SpeedLineColor.a);
    }

    SabaFxOver(
        add, transmittance, _FrostColor.rgb * context.light,
        lens.frostMask * (0.35 + 0.65 * lens.frostRidge * lens.frostRidge) * _FrostColor.a);

    float splat = _Splat * weight;
    UNITY_BRANCH
    if (splat > 1e-3)
    {
        SabaFxOver(
            add, transmittance, _SplatColor.rgb * context.light,
            SabaFxSplat(q, splat) * _SplatColor.a);
    }

    add += context.light * (lens.dropSpecular * 0.35);

    transmittance *= 1.0
        - _Grain * weight * 0.5 * SabaFxHash21(context.pixel + frac(_Time.y) * 91.7);

    float vignette = _Vignette * weight;
    UNITY_BRANCH
    if (vignette > 1e-3)
    {
        float radius = _VignetteRadius - context.beat * _Pulse * weight * 0.12;
        SabaFxOver(
            add, transmittance, _VignetteColor.rgb,
            smoothstep(radius, radius + _VignetteSoftness, length(q)) * vignette * _VignetteColor.a);
    }

    float blinkCycle = sin(_Time.y * _BlinkRate * SABA_FX_TWO_PI) * 0.5 + 0.5;
    blinkCycle *= blinkCycle * blinkCycle;
    float closure = saturate((_Blink + _BlinkAuto * blinkCycle * blinkCycle) * weight);
    UNITY_BRANCH
    if (closure > 1e-3)
    {
        // Negative at full closure so the centre line closes completely.
        float opening = (1.0 - closure) * 1.25 - closure * 0.2;
        float lid = smoothstep(opening, opening + 0.18, abs(q.y) + q.x * q.x * 0.22);
        SabaFxOver(add, transmittance, float3(0.0, 0.0, 0.0), lid);
    }
}

#endif
