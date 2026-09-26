// 曇るガラス。浴室の鏡の前に重ねる透明な板に使います。
//
// LiquidHumidity が _WeatherState.z に曇りの度合い（0〜1）を渡します。曇りは細かい水滴の白い膜で、
// 度合いが増すと、大きくなった水滴が垂れて縦の筋状に晴れた跡ができます。
// VRCMirrorReflection の面そのものは差し替えられないため、手前にこの板を置いて曇らせます。
Shader "SabaProps/Liquid/Fogged Glass"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.86, 0.88, 0.9, 1)
        _MaxOpacity ("Max Opacity", Range(0, 1)) = 0.85
        _WeatherState ("Weather State (wet, snow, fog)", Vector) = (0, 0, 0, 0)
        _DropletScale ("Droplet Scale (1/m)", Float) = 160
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "SabaLiquidCanvas.cginc"

            fixed4 _FogColor;
            float _MaxOpacity;
            float4 _WeatherState;
            float _DropletScale;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float fog = saturate(_WeatherState.z);
                if (fog <= 0.001)
                {
                    return fixed4(0.0, 0.0, 0.0, 0.0);
                }

                // 面内の座標。水平はワールドの xz をまとめ、縦はワールドの y です。
                float2 p = float2(input.worldPos.x + input.worldPos.z, input.worldPos.y);

                // 曇りのむら。
                float mist = 0.75 + 0.5 * SabaLiquidValueNoise(p * 9.0);

                // 垂れた跡。曇りが進んだ所から、縦に細い筋が晴れていきます。
                float column = SabaLiquidHash(float2(floor(p.x * 45.0), 3.7));
                float runLength = saturate((fog - 0.55) * 2.2) * column;
                float streakTop = 1.0 - runLength * (0.6 + 0.4 * SabaLiquidHash(float2(floor(p.x * 45.0), 9.1)));
                float inStreak = column > 0.8 ? step(frac(p.y * 0.6), streakTop) * step(0.6, fog) : 0.0;
                float streakWidth = abs(frac(p.x * 45.0) - 0.5);
                float cleared = inStreak * step(streakWidth, 0.18);

                // 水滴。曇りの上に、光を拾う粒として見えます。
                float4 beads = SabaLiquidBeads(p * _DropletScale / 160.0, 0.004, fog, float2(0.0, -1.0));
                float bead = beads.x * saturate(fog * 1.5 - 0.3);

                float opacity = saturate(fog * mist) * _MaxOpacity * (1.0 - cleared);
                opacity = saturate(opacity + bead * 0.25);
                float3 light = max(ShadeSH9(float4(0.0, 0.0, 1.0, 1.0)), UNITY_LIGHTMODEL_AMBIENT.rgb * 2.0);
                return fixed4(_FogColor.rgb * light * (1.0 + bead * 0.5), opacity);
            }
            ENDCG
        }
    }

    Fallback Off
}
