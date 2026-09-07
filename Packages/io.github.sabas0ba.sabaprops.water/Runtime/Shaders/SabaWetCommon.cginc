#ifndef SABA_WET_COMMON_INCLUDED
#define SABA_WET_COMMON_INCLUDED

#include "SabaWaterCommon.cginc"

sampler2D _MainTex;
fixed4 _Color;
float _Opacity;
float _Metallic;
float _DrySmoothness;
float _WetSmoothness;
float _Wetness;
float _WetDarkening;
float _DropletScale;
float _DropletHeadNormalStrength;
float _DropletTrailNormalStrength;
float _DropletSpeed;
float _TrailPersistence;
float _TrailSlide;
fixed4 _DropletScatterColor;
float _DropletScatterStrength;

struct Input
{
    float2 uv_MainTex;
};

// x: combined mask, y: small-droplet scatter, z: head, w: trail.
inline float4 SabaDropletField(float2 uv)
{
    float2 scaled = uv * max(1.0, _DropletScale);
    float2 cell = floor(scaled);
    float2 local = frac(scaled);
    float2 random = SabaHash22(cell);
    float mass = saturate(random.y * 0.82 + SabaHash21(cell + 17.9) * 0.3);
    float fallSpeed = lerp(0.38, 1.42, mass);
    float cycle = frac(random.y + _Time.y * _DropletSpeed * fallSpeed);
    float startDelay = lerp(0.44, 0.06, mass);
    float travel = saturate((cycle - startDelay) / max(0.05, 1.0 - startDelay));
    travel = travel * travel * (3.0 - 2.0 * travel);
    float active = step(startDelay, cycle);
    float travellingY = lerp(0.94, -0.12, travel);
    float centreX = lerp(0.18, 0.82, random.x);
    float width = lerp(0.045, 0.078, mass);
    float endProgress = smoothstep(0.72, 1.0, travel);
    float headWidth = width * lerp(1.0, 0.28, endProgress);
    float headHeight = headWidth * 1.5;
    float headFade = 1.0 - smoothstep(0.82, 1.0, travel);
    float2 delta = local - float2(centreX, travellingY);

    // UV -Y is gravity. The lower half retains a rounded bulb while the
    // upper half narrows into the point that connects to the residual trail.
    float normalizedY = delta.y / max(0.001, headHeight);
    float gravityBulge = lerp(
        1.16,
        0.3,
        saturate(normalizedY * 0.5 + 0.5));
    float dropDistance = length(float2(
        delta.x / max(0.001, headWidth * gravityBulge),
        delta.y / max(0.001, headHeight)));
    float bead = 1.0 - smoothstep(0.72, 1.0, dropDistance);
    bead *= active * headFade;

    // The trail begins above the pointed end of the head. TrailSlide shortens
    // the oldest part instead of translating the complete trail past its head.
    float headTop = travellingY + headHeight * 0.72;
    float behindHead = local.y - headTop;
    float trailLength = lerp(0.36, 0.96, mass) * saturate(travel * 3.0);
    trailLength = max(width, trailLength - travel * _TrailSlide * 0.24);
    float trailVertical = step(0.0, behindHead)
        * (1.0 - smoothstep(trailLength * 0.58, trailLength, behindHead));
    float trailWidthValue = width * lerp(
        0.7,
        0.42,
        saturate(behindHead / max(width, trailLength)));
    float trailWidth = 1.0 - smoothstep(
        trailWidthValue * 0.7,
        trailWidthValue,
        abs(local.x - centreX));
    float trailAge = lerp(0.56, 0.94, _TrailPersistence)
        * (1.0 - smoothstep(0.91, 1.0, travel));
    float trail = trailWidth * trailVertical * trailAge * active;
    float sparse = step(0.34, SabaHash21(cell + 43.7));
    float headMask = bead * sparse;
    float trailMask = trail * sparse * lerp(0.42, 0.76, mass);
    float mask = max(headMask, trailMask);
    float smallDropletScatter = mask * (1.0 - mass) * (0.65 + headMask * 0.35);
    return float4(mask, smallDropletScatter, headMask, trailMask);
}

void surf(Input input, inout SurfaceOutputStandard output)
{
    fixed4 baseColour = tex2D(_MainTex, input.uv_MainTex) * _Color;
    float wetness = saturate(_Wetness);
    float4 dropletData = SabaDropletField(input.uv_MainTex) * wetness;
    float epsilon = 0.012 / max(1.0, _DropletScale);
    float4 dropletX = SabaDropletField(
        input.uv_MainTex + float2(epsilon, 0.0)) * wetness;
    float4 dropletY = SabaDropletField(
        input.uv_MainTex + float2(0.0, epsilon)) * wetness;

    float3 wetAlbedo = baseColour.rgb * lerp(1.0, 1.0 - _WetDarkening, wetness);
    output.Albedo = lerp(
        wetAlbedo,
        _DropletScatterColor.rgb,
        saturate(dropletData.y * _DropletScatterStrength));
    output.Metallic = _Metallic;
    output.Smoothness = lerp(_DrySmoothness, _WetSmoothness, wetness);
    output.Normal = normalize(float3(
        (dropletData.z - dropletX.z) * _DropletHeadNormalStrength
            + (dropletData.w - dropletX.w) * _DropletTrailNormalStrength,
        (dropletData.z - dropletY.z) * _DropletHeadNormalStrength
            + (dropletData.w - dropletY.w) * _DropletTrailNormalStrength,
        1.0));
    output.Occlusion = 1.0;
    output.Alpha = saturate(baseColour.a * _Opacity);
}

#endif
