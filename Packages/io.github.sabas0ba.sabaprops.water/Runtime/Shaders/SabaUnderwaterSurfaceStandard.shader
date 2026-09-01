Shader "SabaProps/Water/Underwater Surface Standard"
{
    Properties
    {
        _Tint ("Refraction Tint", Color) = (0.12, 0.48, 0.58, 1)
        _HighlightColor ("Surface Highlight", Color) = (0.84, 0.96, 1, 1)
        _DistortionStrength ("Refraction Distortion", Range(0, 0.08)) = 0.022
        _WaveScale ("Wave Scale", Float) = 1.15
        _WaveStrength ("Wave Strength", Range(0, 1)) = 0.25
        _WaveSpeed ("Wave Speed", Float) = 0.32
        _FlowDirection ("Flow Direction", Vector) = (1, 0.2, 0, 0)
        _TintStrength ("Tint Strength", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        GrabPass { "_SabaUnderwaterSurfaceGrab" }

        Pass
        {
            Cull Front
            ZWrite Off
            Blend One Zero

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "SabaWaterCommon.cginc"

            sampler2D _SabaUnderwaterSurfaceGrab;
            fixed4 _Tint;
            fixed4 _HighlightColor;
            float _DistortionStrength;
            float _WaveScale;
            float _WaveStrength;
            float _WaveSpeed;
            float4 _FlowDirection;
            float _TintStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float4 grabPosition : TEXCOORD0;
                float3 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(v2f, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.position = UnityWorldToClipPos(output.worldPosition);
                output.grabPosition = ComputeGrabScreenPos(output.position);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float3 normal = SabaWaterNormal(
                    input.worldPosition, _WaveScale, _WaveStrength, _WaveSpeed, _FlowDirection.xy);
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - input.worldPosition);
                float fresnel = pow(1.0 - saturate(abs(dot(normal, viewDirection))), 3.0);

                float4 projected = input.grabPosition;
                projected.xy += normal.xz * (_DistortionStrength * projected.w);
                float3 refracted = tex2Dproj(
                    _SabaUnderwaterSurfaceGrab, UNITY_PROJ_COORD(projected)).rgb;
                refracted = lerp(refracted, refracted * _Tint.rgb, _TintStrength);
                refracted = lerp(refracted, _HighlightColor.rgb, fresnel * 0.42);
                return fixed4(refracted, 1.0);
            }
            ENDCG
        }
    }
    Fallback "SabaProps/Water/Underwater Surface Lite"
}
