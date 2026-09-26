// 天候に応じて濡れと積雪を描くワールドの面のシェーダ。
//
// LiquidWeather が _WeatherState にサーバー時刻から求めた地面の状態を渡します。
//   x: 濡れ（0〜1）  y: 積雪（0〜1）  z: 結露（0〜1、LiquidHumidity が渡します）
// 濡れると下地が暗く艶を帯び、上を向いた面のくぼみ（ノイズの低い所）から水たまりになります。
// 雪は上を向いた面に積もり、縁はむらになります。
//
// 屋根の下など、降らない場所の面にはこのマテリアルを使わないでください。
// 遮りの判定は受け手（プレイヤーとマネキン）にだけ行い、面ごとには行いません。
Shader "SabaProps/Liquid/Weather Surface"
{
    Properties
    {
        _Color ("Color", Color) = (0.5, 0.5, 0.5, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0, 1)) = 0.2
        _Metallic ("Metallic", Range(0, 1)) = 0
        _WeatherState ("Weather State (wet, snow, dew)", Vector) = (0, 0, 0, 0)
        _PuddleScale ("Puddle Scale (1/m)", Float) = 0.6
        _PuddleAmount ("Puddle Amount", Range(0, 1)) = 0.6
        _SnowColor ("Snow Color", Color) = (0.9, 0.92, 0.95, 1)
        _SnowPatchScale ("Snow Patch Scale (1/m)", Float) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        #include "SabaLiquidCanvas.cginc"

        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness;
        half _Metallic;
        float4 _WeatherState;
        float _PuddleScale;
        float _PuddleAmount;
        fixed4 _SnowColor;
        float _SnowPatchScale;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float3 worldNormal;
        };

        void surf(Input input, inout SurfaceOutputStandard o)
        {
            fixed4 base = tex2D(_MainTex, input.uv_MainTex) * _Color;
            float3 albedo = base.rgb;
            float smoothness = _Glossiness;

            float upFacing = saturate((input.worldNormal.y - 0.3) / 0.5);
            float2 ground = input.worldPos.xz;

            // 濡れ。吸った下地は暗くなり、表面は艶を帯びます。
            float wet = saturate(_WeatherState.x);
            albedo *= lerp(1.0, 0.6, wet);
            smoothness = lerp(smoothness, 0.8, wet);

            // 水たまり。濡れが進むほど、ノイズの低い所から広がります。
            float hollow = SabaLiquidValueNoise(ground * _PuddleScale) * 0.65
                + SabaLiquidValueNoise(ground * _PuddleScale * 3.1 + 7.3) * 0.35;
            float threshold = wet * _PuddleAmount * 0.45;
            float puddle = smoothstep(0.0, 0.04, threshold - hollow) * upFacing;
            albedo *= lerp(1.0, 0.45, puddle);
            smoothness = lerp(smoothness, 0.97, puddle);

            // 積雪。上を向いた面から覆い、縁はむらになります。
            float patch = SabaLiquidValueNoise(ground * _SnowPatchScale + input.worldPos.y * 1.7);
            float snow = smoothstep(0.25, 0.6, saturate(_WeatherState.y) * upFacing * 1.3 + (patch - 0.5) * 0.35);
            albedo = lerp(albedo, _SnowColor.rgb, snow);
            smoothness = lerp(smoothness, 0.3, snow);

            // 結露。細かい水滴の膜で白っぽく曇り、水滴の所だけ艶が出ます。向きに関係なく付きます。
            float dew = saturate(_WeatherState.z);
            if (dew > 0.001)
            {
                float2 across = float2(input.worldPos.x + input.worldPos.z, input.worldPos.y) + ground * 0.3;
                float steepness = saturate(1.0 - abs(input.worldNormal.y));
                float4 beads = SabaLiquidBeads(across, 0.004, dew, float2(0.0, -1.0), steepness, _Time.y);
                albedo = lerp(albedo, albedo * 0.75 + 0.1, dew * 0.5);
                smoothness = lerp(smoothness, 0.35, dew * 0.6);
                smoothness = lerp(smoothness, 0.85, beads.x * dew * 0.5);
            }

            o.Albedo = albedo;
            o.Metallic = _Metallic * (1.0 - snow);
            o.Smoothness = smoothness;
            o.Alpha = base.a;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
