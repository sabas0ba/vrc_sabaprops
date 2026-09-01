Shader "SabaProps/Water/Underwater Lite"
{
    Properties
    {
        _Tint ("Water Tint", Color) = (0.02, 0.24, 0.32, 0.48)
        _Density ("Distance Density", Range(0, 1)) = 0.08
        _DistortionStrength ("Surface Motion", Range(0, 0.05)) = 0.006
        _CausticsStrength ("Caustics Strength", Range(0, 1)) = 0.18
        _CausticsScale ("Caustics Scale", Float) = 1.5
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+50" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Front
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            fixed4 _Tint;
            float _Density;
            float _DistortionStrength;
            float _CausticsStrength;
            float _CausticsScale;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float4 screenPosition : TEXCOORD0;
                float3 worldPosition : TEXCOORD1;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
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
                float sceneDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenUv));
                float fog = saturate(1.0 - exp(-max(0.0, sceneDepth) * _Density));

                float2 causticUv = input.worldPosition.xz * _CausticsScale;
                float causticA = sin(causticUv.x + _Time.y * 1.3 + sin(causticUv.y));
                float causticB = sin(causticUv.y * 1.37 - _Time.y + sin(causticUv.x * 0.81));
                float caustic = pow(saturate(1.0 - abs(causticA - causticB)), 6.0);

                float motion = sin((screenUv.x + screenUv.y) * 32.0 + _Time.y * 1.7);
                float3 colour = _Tint.rgb + caustic * _CausticsStrength;
                colour += motion * _DistortionStrength;
                float alpha = _Tint.a * lerp(0.3, 1.0, fog);
                return fixed4(colour, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
