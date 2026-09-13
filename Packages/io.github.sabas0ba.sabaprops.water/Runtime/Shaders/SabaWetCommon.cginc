#ifndef SABA_WET_COMMON_INCLUDED
#define SABA_WET_COMMON_INCLUDED

#include "SabaDropletField.cginc"

sampler2D _MainTex;
fixed4 _Color;
float _Opacity;
float _Metallic;
float _DrySmoothness;
float _WetSmoothness;
float _Wetness;
float _WetDarkening;
float _DropletHeadNormalStrength;
float _DropletTrailNormalStrength;
fixed4 _DropletScatterColor;
float _DropletScatterStrength;

struct Input
{
    float2 uv_MainTex;
};

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
