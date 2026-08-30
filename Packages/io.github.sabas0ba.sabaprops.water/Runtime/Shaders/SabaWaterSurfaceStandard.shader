Shader "SabaProps/Water/Surface Standard"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.16, 0.48, 0.55, 1)
        _DeepColor ("Deep Color", Color) = (0.015, 0.11, 0.18, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.72
        _Smoothness ("Smoothness", Range(0, 1)) = 0.9
        _WaveScale ("Wave Scale", Float) = 1.8
        _WaveStrength ("Normal Strength", Range(0, 1)) = 0.16
        _WaveSpeed ("Wave Speed", Float) = 0.35
        _FlowDirection ("Flow Direction", Vector) = (1, 0.2, 0, 0)
        _VertexWaveHeight ("Vertex Wave Height", Range(0, 0.5)) = 0.04
        _EdgeFade ("UV Edge Fade", Range(0, 0.5)) = 0
        _RippleStrength ("Rain Ripple Strength", Range(0, 1)) = 0
        _RippleDensity ("Rain Ripple Density", Float) = 1.5
        _RippleSpeed ("Rain Ripple Speed", Float) = 0.8
        _RefractionStrength ("Refraction Strength", Range(0, 0.1)) = 0.018
        _DepthDistance ("Depth Colour Distance", Float) = 3
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent-10"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }
        LOD 300

        GrabPass { "_SabaWaterGrab" }

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Cull Off
            ZWrite Off
            Blend One Zero

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "SabaWaterCommon.cginc"

            sampler2D _SabaWaterGrab;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            fixed4 _ShallowColor;
            fixed4 _DeepColor;
            float _Opacity;
            float _Smoothness;
            float _WaveScale;
            float _WaveStrength;
            float _WaveSpeed;
            float4 _FlowDirection;
            float _VertexWaveHeight;
            float _EdgeFade;
            float _RippleStrength;
            float _RippleDensity;
            float _RippleSpeed;
            float _RefractionStrength;
            float _DepthDistance;

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
                float4 grabPosition : TEXCOORD0;
                float4 screenPosition : TEXCOORD1;
                float3 worldPosition : TEXCOORD2;
                float3 worldNormal : TEXCOORD3;
                float2 uv : TEXCOORD4;
                float eyeDepth : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(v2f, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                worldPosition.y += SabaWaterHeight(
                    worldPosition, _WaveScale, _WaveSpeed, _FlowDirection.xy) * _VertexWaveHeight;

                output.position = UnityWorldToClipPos(worldPosition);
                output.grabPosition = ComputeGrabScreenPos(output.position);
                output.screenPosition = ComputeScreenPos(output.position);
                output.worldPosition = worldPosition;
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                output.uv = input.uv;
                output.eyeDepth = -UnityWorldToViewPos(worldPosition).z;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float3 baseNormal = normalize(input.worldNormal);
                float3 proceduralNormal = SabaWaterNormal(
                    input.worldPosition,
                    _WaveScale,
                    _WaveStrength,
                    _WaveSpeed,
                    _FlowDirection.xy);
                float3 normal = normalize(baseNormal + proceduralNormal - float3(0, 1, 0));
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - input.worldPosition);

                float4 refractedPosition = input.grabPosition;
                refractedPosition.xy += normal.xz * (_RefractionStrength * refractedPosition.w);
                float3 background = tex2Dproj(_SabaWaterGrab, UNITY_PROJ_COORD(refractedPosition)).rgb;

                float2 screenUv = input.screenPosition.xy / input.screenPosition.w;
                screenUv = UnityStereoTransformScreenSpaceTex(screenUv);
                float sceneEyeDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenUv));
                float waterDepth = max(0.0, sceneEyeDepth - input.eyeDepth);
                float depthFactor = saturate(waterDepth / max(0.01, _DepthDistance));

                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                float3 halfDirection = normalize(viewDirection + lightDirection);
                float fresnel = pow(1.0 - saturate(dot(normal, viewDirection)), 4.0);
                float specularPower = lerp(16.0, 256.0, _Smoothness);
                float specular = pow(saturate(dot(normal, halfDirection)), specularPower) * _Smoothness;
                float ripple = SabaRainRipple(
                    input.worldPosition.xz, _RippleDensity, _RippleSpeed) * _RippleStrength;

                float3 water = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthFactor);
                float3 reflection = SabaReflectionProbe(viewDirection, normal);
                float coverage = _Opacity * SabaUvEdgeFade(input.uv, _EdgeFade);
                water = lerp(background, water, saturate(coverage * (0.3 + depthFactor * 0.7)));
                water = lerp(
                    water,
                    reflection,
                    fresnel * (0.35 + _Smoothness * 0.45) * coverage);
                water += _LightColor0.rgb * (specular * 0.75 + ripple * 0.25) * coverage;
                return fixed4(water, 1.0);
            }
            ENDCG
        }
    }

    Fallback "SabaProps/Water/Surface Lite"
}
