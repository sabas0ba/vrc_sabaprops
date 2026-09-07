Shader "SabaProps/Water/Underwater Surface Lite"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.19, 0.64, 0.76, 1)
        _HighlightColor ("Surface Highlight", Color) = (0.82, 0.95, 1, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.58
        _WaveScale ("Wave Scale", Float) = 1.15
        _WaveStrength ("Wave Strength", Range(0, 1)) = 0.2
        _WaveSpeed ("Wave Speed", Float) = 0.32
        _FlowDirection ("Flow Direction", Vector) = (1, 0.2, 0, 0)
        _BoundaryUpDown ("Boundary Up / Down", Vector) = (1, 0, 0, 0)
        _BoundaryCardinal ("Boundary N / E / S / W", Vector) = (0, 0, 0, 0)
        _BoundaryDiagonal ("Boundary NE / SE / SW / NW", Vector) = (0, 0, 0, 0)
        _BoundaryEdgeFade ("Boundary Edge Fade", Range(0, 0.5)) = 0.08
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "SabaWaterCommon.cginc"

            fixed4 _ShallowColor;
            fixed4 _HighlightColor;
            float _Opacity;
            float _WaveScale;
            float _WaveStrength;
            float _WaveSpeed;
            float4 _FlowDirection;
            float4 _BoundaryUpDown;
            float4 _BoundaryCardinal;
            float4 _BoundaryDiagonal;
            float _BoundaryEdgeFade;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float3 worldPosition : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(v2f, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                output.uv = input.uv;
                output.position = UnityWorldToClipPos(output.worldPosition);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float boundaryMask = SabaBoundaryDirectionMask(
                    input.worldNormal,
                    _BoundaryUpDown,
                    _BoundaryCardinal,
                    _BoundaryDiagonal);
                clip(boundaryMask - 0.001);
                float3 normal = SabaWaterNormal(
                    input.worldPosition, _WaveScale, _WaveStrength, _WaveSpeed, _FlowDirection.xy);
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - input.worldPosition);
                float fresnel = pow(1.0 - saturate(abs(dot(normal, viewDirection))), 2.5);
                float waveHeight = SabaWaterHeight(
                    input.worldPosition, _WaveScale, _WaveSpeed, _FlowDirection.xy) * 0.5 + 0.5;
                float highlight = saturate(fresnel * 0.72 + smoothstep(0.72, 1.0, waveHeight) * 0.35);
                float edgeFade = SabaUvEdgeFade(input.uv, _BoundaryEdgeFade);
                return fixed4(
                    lerp(_ShallowColor.rgb, _HighlightColor.rgb, highlight),
                    _Opacity * lerp(0.72, 1.0, fresnel) * edgeFade);
            }
            ENDCG
        }
    }
    Fallback Off
}
