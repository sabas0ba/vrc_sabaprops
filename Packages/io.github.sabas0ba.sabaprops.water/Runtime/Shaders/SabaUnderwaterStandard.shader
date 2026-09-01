Shader "SabaProps/Water/Underwater Standard"
{
    Properties
    {
        _Tint ("Water Tint", Color) = (0.015, 0.2, 0.3, 1)
        _Density ("Distance Density", Range(0, 1)) = 0.07
        _DistortionStrength ("Distortion Strength", Range(0, 0.05)) = 0.009
        _ChromaticAberration ("Chromatic Aberration", Range(0, 0.02)) = 0.0015
        _CausticsStrength ("Caustics Strength", Range(0, 1)) = 0.2
        _CausticsScale ("Caustics Scale", Float) = 1.5
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+50" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        GrabPass { "_SabaUnderwaterGrab" }

        Pass
        {
            Cull Front
            ZWrite Off
            ZTest Always
            Blend One Zero

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _SabaUnderwaterGrab;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            fixed4 _Tint;
            float _Density;
            float _DistortionStrength;
            float _ChromaticAberration;
            float _CausticsStrength;
            float _CausticsScale;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float4 grabPosition : TEXCOORD0;
                float4 screenPosition : TEXCOORD1;
                float3 worldPosition : TEXCOORD2;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.grabPosition = ComputeGrabScreenPos(output.position);
                output.screenPosition = ComputeScreenPos(output.position);
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float3 cameraLocal = mul(
                    unity_WorldToObject, float4(_WorldSpaceCameraPos.xyz, 1.0)).xyz;
                float inside = 0.5001 - max(max(abs(cameraLocal.x), abs(cameraLocal.y)), abs(cameraLocal.z));
                clip(inside);

                float2 screenUv = input.screenPosition.xy / input.screenPosition.w;
                screenUv = UnityStereoTransformScreenSpaceTex(screenUv);
                float waveA = sin(screenUv.y * 47.0 + _Time.y * 1.4);
                float waveB = sin(screenUv.x * 31.0 - _Time.y * 0.9);
                float2 distortion = float2(waveA, waveB) * _DistortionStrength;

                float4 projected = input.grabPosition;
                projected.xy += distortion * projected.w;
                float4 projectedR = projected;
                float4 projectedB = projected;
                projectedR.x += _ChromaticAberration * projected.w;
                projectedB.x -= _ChromaticAberration * projected.w;

                float3 background;
                background.r = tex2Dproj(_SabaUnderwaterGrab, UNITY_PROJ_COORD(projectedR)).r;
                background.g = tex2Dproj(_SabaUnderwaterGrab, UNITY_PROJ_COORD(projected)).g;
                background.b = tex2Dproj(_SabaUnderwaterGrab, UNITY_PROJ_COORD(projectedB)).b;

                float sceneDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenUv));
                float fog = saturate(1.0 - exp(-max(0.0, sceneDepth) * _Density));
                float3 colour = lerp(background, _Tint.rgb, fog * 0.88);

                float2 causticUv = input.worldPosition.xz * _CausticsScale;
                float causticA = sin(causticUv.x + _Time.y * 1.3 + sin(causticUv.y));
                float causticB = sin(causticUv.y * 1.37 - _Time.y + sin(causticUv.x * 0.81));
                float caustic = pow(saturate(1.0 - abs(causticA - causticB)), 7.0);
                colour += caustic * _CausticsStrength * (1.0 - fog * 0.6);
                return fixed4(colour, 1.0);
            }
            ENDCG
        }
    }
    Fallback "SabaProps/Water/Underwater Lite"
}
