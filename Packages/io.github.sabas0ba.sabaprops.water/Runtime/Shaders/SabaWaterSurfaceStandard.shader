Shader "SabaProps/Water/Surface Standard"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.16, 0.48, 0.55, 1)
        _DeepColor ("Deep Color", Color) = (0.015, 0.11, 0.18, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.72
        _Smoothness ("Smoothness", Range(0, 1)) = 0.9
        _LightingResponse ("Lighting Response", Range(0, 1)) = 0.92
        _WaveScale ("Wave Scale", Float) = 1.8
        _WaveStrength ("Normal Strength", Range(0, 1)) = 0.16
        _WaveSpeed ("Wave Speed", Float) = 0.35
        _FlowDirection ("Flow Direction", Vector) = (1, 0.2, 0, 0)
        _VertexWaveHeight ("Vertex Wave Height", Range(0, 0.5)) = 0.04
        _TideHeight ("Tide Height", Range(0, 0.5)) = 0
        _TideSpeed ("Tide Speed", Range(0, 1)) = 0.04
        _EdgeFade ("UV Edge Fade", Range(0, 0.5)) = 0
        _RippleStrength ("Rain Ripple Strength", Range(0, 1)) = 0
        _RippleDensity ("Rain Ripple Density", Float) = 1.5
        _RippleSpeed ("Rain Ripple Speed", Float) = 0.8
        _ShallowEdgeWidth ("Shallow Edge Width", Range(0, 0.5)) = 0
        _FoamColor ("Foam Color", Color) = (0.28, 0.58, 0.63, 1)
        _FoamStrength ("Foam Strength", Range(0, 1)) = 0
        _CrestFoamThreshold ("Crest Foam Threshold", Range(0, 1)) = 0.8
        _CrestFoamWidth ("Crest Foam Width", Range(0.01, 0.35)) = 0.08
        _FoamTrailStrength ("Residual Foam", Range(0, 1)) = 0.35
        _FoamDetail ("Foam Breakup", Range(0, 1)) = 0.8
        _FoamPatternScale ("Foam Pattern Scale", Float) = 1
        _FoamPatternSpeed ("Foam Pattern Speed", Range(0, 3)) = 1
        _FoamPatternWarp ("Foam Pattern Warp", Range(0, 1)) = 0.45
        _ShoreFoamWidth ("Shore Foam Width", Range(0, 0.5)) = 0
        _FlowTurbulence ("Flow Turbulence", Range(0, 1)) = 0
        _FlowFoamStrength ("Flow Aeration", Range(0, 1)) = 0
        _ReflectionStrength ("Reflection Strength", Range(0, 1.5)) = 0.9
        _ReflectionDistortion ("Reflection Distortion", Range(0, 1)) = 0.3
        _ReflectionBlur ("Reflection Blur", Range(0, 1)) = 0.08
        _RippleReflectionBlur ("Rain Reflection Haze", Range(0, 1)) = 0.55
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
            #pragma multi_compile _ UNITY_SPECCUBE_BOX_PROJECTION
            #pragma multi_compile _ UNITY_SPECCUBE_BLENDING

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            #include "SabaWaterCommon.cginc"

            sampler2D _SabaWaterGrab;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            fixed4 _ShallowColor;
            fixed4 _DeepColor;
            float _Opacity;
            float _Smoothness;
            float _LightingResponse;
            float _WaveScale;
            float _WaveStrength;
            float _WaveSpeed;
            float4 _FlowDirection;
            float _VertexWaveHeight;
            float _TideHeight;
            float _TideSpeed;
            float _EdgeFade;
            float _RippleStrength;
            float _RippleDensity;
            float _RippleSpeed;
            float _ShallowEdgeWidth;
            fixed4 _FoamColor;
            float _FoamStrength;
            float _CrestFoamThreshold;
            float _CrestFoamWidth;
            float _FoamTrailStrength;
            float _FoamDetail;
            float _FoamPatternScale;
            float _FoamPatternSpeed;
            float _FoamPatternWarp;
            float _ShoreFoamWidth;
            float _FlowTurbulence;
            float _FlowFoamStrength;
            float _ReflectionStrength;
            float _ReflectionDistortion;
            float _ReflectionBlur;
            float _RippleReflectionBlur;
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
                float4 pos : SV_POSITION;
                float4 grabPosition : TEXCOORD0;
                float4 screenPosition : TEXCOORD1;
                float3 worldPosition : TEXCOORD2;
                float3 worldNormal : TEXCOORD3;
                float2 uv : TEXCOORD4;
                float eyeDepth : TEXCOORD5;
                LIGHTING_COORDS(6, 7)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;
                worldPosition.y += SabaWaterHeight(
                    worldPosition, _WaveScale, _WaveSpeed, _FlowDirection.xy) * _VertexWaveHeight;
                worldPosition.y += SabaTideOffset(_TideHeight, _TideSpeed);

                output.pos = UnityWorldToClipPos(worldPosition);
                output.grabPosition = ComputeGrabScreenPos(output.pos);
                output.screenPosition = ComputeScreenPos(output.pos);
                output.worldPosition = worldPosition;
                output.worldNormal = UnityObjectToWorldNormal(v.normal);
                output.uv = v.uv;
                output.eyeDepth = -UnityWorldToViewPos(worldPosition).z;
                TRANSFER_VERTEX_TO_FRAGMENT(output);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 baseNormal = normalize(input.worldNormal);
                float3 proceduralNormal = SabaWaterNormal(
                    input.worldPosition,
                    _WaveScale,
                    _WaveStrength,
                    _WaveSpeed,
                    _FlowDirection.xy);
                float3 normal = normalize(baseNormal + proceduralNormal - float3(0, 1, 0));
                float3 flowData = SabaFlowTurbulence(
                    input.uv, input.worldPosition, _WaveScale, _WaveSpeed);
                normal = normalize(normal + float3(
                    flowData.y * _FlowTurbulence * 0.11,
                    0.0,
                    flowData.z * _FlowTurbulence * 0.075));
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - input.worldPosition);

                float3 rippleData = SabaRainRippleData(
                    input.worldPosition.xz, _RippleDensity, _RippleSpeed);
                float ripple = rippleData.x * _RippleStrength;
                float3 reflectionNormal = normalize(normal + float3(
                    rippleData.y,
                    0.0,
                    rippleData.z) * (_RippleStrength * _ReflectionDistortion));

                float4 refractedPosition = input.grabPosition;
                refractedPosition.xy += reflectionNormal.xz
                    * (_RefractionStrength * refractedPosition.w);
                float3 background = tex2Dproj(_SabaWaterGrab, UNITY_PROJ_COORD(refractedPosition)).rgb;

                float2 screenUv = input.screenPosition.xy / input.screenPosition.w;
                screenUv = UnityStereoTransformScreenSpaceTex(screenUv);
                float sceneEyeDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, screenUv));
                float waterDepth = max(0.0, sceneEyeDepth - input.eyeDepth);
                float depthFactor = saturate(waterDepth / max(0.01, _DepthDistance));
                float shallowEdge = 1.0 - SabaUvEdgeFade(input.uv, _ShallowEdgeWidth);
                depthFactor *= 1.0 - shallowEdge * 0.72;

                float3 lightDirection = normalize(UnityWorldSpaceLightDir(input.worldPosition));
                float3 halfDirection = normalize(viewDirection + lightDirection);
                float fresnel = pow(1.0 - saturate(dot(normal, viewDirection)), 4.0);
                float specularPower = lerp(16.0, 256.0, _Smoothness);
                float specular = pow(saturate(dot(normal, halfDirection)), specularPower) * _Smoothness;
                float3 water = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthFactor);
                UNITY_LIGHT_ATTENUATION(attenuation, input, input.worldPosition);
                float3 surfaceLighting = SabaWaterBaseLighting(
                    normal, input.worldPosition, attenuation);
                water = lerp(water, water * surfaceLighting, _LightingResponse);
                float reflectionRoughness = saturate(
                    _ReflectionBlur + ripple * _RippleReflectionBlur);
                float3 reflection = SabaReflectionProbe(
                    viewDirection, reflectionNormal, input.worldPosition, reflectionRoughness);
                float coverage = _Opacity * SabaUvEdgeFade(input.uv, _EdgeFade);
                water = lerp(background, water, saturate(coverage * (0.3 + depthFactor * 0.7)));
                water = lerp(
                    water,
                    reflection,
                    saturate(fresnel * (0.35 + _Smoothness * 0.45)
                        * coverage * _ReflectionStrength));
                water += _LightColor0.rgb * (specular * 0.75 + ripple * 0.25)
                    * coverage * attenuation;

                float2 breakingFoam = SabaBreakingFoam(
                    input.worldPosition,
                    _WaveScale,
                    _WaveSpeed,
                    _FlowDirection.xy,
                    _CrestFoamThreshold,
                    _CrestFoamWidth,
                    _FoamTrailStrength,
                    _FoamDetail,
                    _FoamPatternScale,
                    _FoamPatternSpeed,
                    _FoamPatternWarp);
                float crest = saturate(breakingFoam.x + breakingFoam.y) * _FoamStrength;
                float shore = (1.0 - smoothstep(
                    0.0, max(0.001, _ShoreFoamWidth * _DepthDistance), waterDepth))
                    * step(0.0001, _ShoreFoamWidth) * _FoamStrength;
                float slopeAeration = saturate((1.0 - saturate(baseNormal.y)) * 3.5);
                float flowFoam = smoothstep(0.42, 0.82, flowData.x)
                    * slopeAeration * _FlowFoamStrength;
                float foam = saturate(
                    crest + shore + flowFoam + ripple * _FoamStrength * 0.18) * coverage;
                float3 litFoam = _FoamColor.rgb
                    * lerp(float3(1.0, 1.0, 1.0), surfaceLighting, _LightingResponse);
                water = lerp(water, litFoam, foam);
                return fixed4(water, 1.0);
            }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode" = "ForwardAdd" }
            Cull Off
            ZWrite Off
            Blend One One

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vertAdd
            #pragma fragment fragAdd
            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            #include "SabaWaterCommon.cginc"

            float _Opacity;
            float _Smoothness;
            float _LightingResponse;
            float _WaveScale;
            float _WaveStrength;
            float _WaveSpeed;
            float4 _FlowDirection;
            float _VertexWaveHeight;
            float _TideHeight;
            float _TideSpeed;
            float _EdgeFade;

            struct appdataAdd
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2fAdd
            {
                float4 pos : SV_POSITION;
                float3 worldPosition : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float2 uv : TEXCOORD2;
                LIGHTING_COORDS(3, 4)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2fAdd vertAdd(appdataAdd v)
            {
                v2fAdd output;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2fAdd, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;
                worldPosition.y += SabaWaterHeight(
                    worldPosition, _WaveScale, _WaveSpeed, _FlowDirection.xy) * _VertexWaveHeight;
                worldPosition.y += SabaTideOffset(_TideHeight, _TideSpeed);
                output.pos = UnityWorldToClipPos(worldPosition);
                output.worldPosition = worldPosition;
                output.worldNormal = UnityObjectToWorldNormal(v.normal);
                output.uv = v.uv;
                TRANSFER_VERTEX_TO_FRAGMENT(output);
                return output;
            }

            fixed4 fragAdd(v2fAdd input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normal = normalize(input.worldNormal + SabaWaterNormal(
                    input.worldPosition, _WaveScale, _WaveStrength, _WaveSpeed, _FlowDirection.xy)
                    - float3(0.0, 1.0, 0.0));
                float3 lightDirection = normalize(UnityWorldSpaceLightDir(input.worldPosition));
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - input.worldPosition);
                float diffuse = saturate(dot(normal, lightDirection));
                float3 halfDirection = normalize(viewDirection + lightDirection);
                float specular = pow(
                    saturate(dot(normal, halfDirection)), lerp(16.0, 256.0, _Smoothness))
                    * _Smoothness;
                UNITY_LIGHT_ATTENUATION(attenuation, input, input.worldPosition);
                float coverage = _Opacity * SabaUvEdgeFade(input.uv, _EdgeFade);
                float3 contribution = _LightColor0.rgb * attenuation
                    * (diffuse * 0.1 + specular * 0.8) * coverage * _LightingResponse;
                return fixed4(contribution, 0.0);
            }
            ENDCG
        }
    }

    Fallback "SabaProps/Water/Surface Lite"
}
