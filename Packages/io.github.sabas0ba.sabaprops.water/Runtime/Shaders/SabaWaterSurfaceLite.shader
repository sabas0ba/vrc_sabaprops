Shader "SabaProps/Water/Surface Lite"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.16, 0.48, 0.55, 1)
        _DeepColor ("Deep Color", Color) = (0.015, 0.11, 0.18, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.72
        _Smoothness ("Smoothness", Range(0, 1)) = 0.82
        _LightingResponse ("Lighting Response", Range(0, 1)) = 0.85
        _WaveScale ("Wave Scale", Float) = 1.8
        _WaveStrength ("Normal Strength", Range(0, 1)) = 0.12
        _WaveSpeed ("Wave Speed", Float) = 0.35
        _FlowDirection ("Flow Direction", Vector) = (1, 0.2, 0, 0)
        _VertexWaveHeight ("Vertex Wave Height", Range(0, 0.5)) = 0
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
        _FoamTrailStrength ("Residual Foam", Range(0, 1)) = 0.2
        _FoamDetail ("Foam Breakup", Range(0, 1)) = 0.6
        _FoamPatternScale ("Foam Pattern Scale", Float) = 1
        _FoamPatternSpeed ("Foam Pattern Speed", Range(0, 3)) = 1
        _FoamPatternWarp ("Foam Pattern Warp", Range(0, 1)) = 0.45
        _ShoreFoamWidth ("Shore Foam Width", Range(0, 0.5)) = 0
        _FlowTurbulence ("Flow Turbulence", Range(0, 1)) = 0
        _FlowFoamStrength ("Flow Aeration", Range(0, 1)) = 0
        _ReflectionStrength ("Reflection Strength", Range(0, 1.5)) = 0.65
        _ReflectionDistortion ("Reflection Distortion", Range(0, 1)) = 0.18
        _ReflectionBlur ("Reflection Blur", Range(0, 1)) = 0.18
        _RippleReflectionBlur ("Rain Reflection Haze", Range(0, 1)) = 0.35
        [HideInInspector] _RefractionStrength ("Refraction Strength", Range(0, 0.1)) = 0
        [HideInInspector] _DepthDistance ("Depth Distance", Float) = 3
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent-10"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }
        LOD 150
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

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
                float3 worldPosition : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float2 uv : TEXCOORD2;
                LIGHTING_COORDS(3, 4)
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
                output.worldPosition = worldPosition;
                output.worldNormal = UnityObjectToWorldNormal(v.normal);
                output.uv = v.uv;
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
                    flowData.y * _FlowTurbulence * 0.08,
                    0.0,
                    flowData.z * _FlowTurbulence * 0.055));

                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - input.worldPosition);
                float3 lightDirection = normalize(UnityWorldSpaceLightDir(input.worldPosition));
                float fresnel = pow(1.0 - saturate(dot(normal, viewDirection)), 4.0);
                float diffuse = saturate(dot(normal, lightDirection));
                float3 halfDirection = normalize(viewDirection + lightDirection);
                float specularPower = lerp(12.0, 192.0, _Smoothness);
                float specular = pow(saturate(dot(normal, halfDirection)), specularPower) * _Smoothness;

                float ripple = SabaRainRipple(
                    input.worldPosition.xz, _RippleDensity, _RippleSpeed) * _RippleStrength;
                float shallowEdge = 1.0 - SabaUvEdgeFade(input.uv, _ShallowEdgeWidth);
                float colourDepth = saturate(
                    (1.0 - shallowEdge) * 0.72 + fresnel * 0.2 + (1.0 - diffuse) * 0.08);
                float3 water = lerp(_ShallowColor.rgb, _DeepColor.rgb, colourDepth);
                UNITY_LIGHT_ATTENUATION(attenuation, input, input.worldPosition);
                float3 surfaceLighting = SabaWaterBaseLighting(
                    normal, input.worldPosition, attenuation);
                water = lerp(water, water * surfaceLighting, _LightingResponse);
                float2 rippleDirection = SabaSafeDirection(float2(
                    SabaHash21(floor(input.worldPosition.xz * _RippleDensity) + 7.3) - 0.5,
                    SabaHash21(floor(input.worldPosition.xz * _RippleDensity) + 19.7) - 0.5));
                float3 reflectionNormal = normalize(normal + float3(
                    rippleDirection.x,
                    0.0,
                    rippleDirection.y) * (ripple * _ReflectionDistortion));
                float reflectionRoughness = saturate(
                    _ReflectionBlur + ripple * _RippleReflectionBlur);
                float3 reflection = SabaReflectionProbe(
                    viewDirection, reflectionNormal, input.worldPosition, reflectionRoughness);
                water = lerp(water, reflection, saturate(
                    fresnel * (0.25 + _Smoothness * 0.35) * _ReflectionStrength));
                water += _LightColor0.rgb * (specular * 0.65 + ripple * 0.12) * attenuation;

                float crest = SabaCrestFoamLite(
                    input.worldPosition,
                    _WaveScale,
                    _WaveSpeed,
                    _FlowDirection.xy,
                    _CrestFoamThreshold,
                    _CrestFoamWidth,
                    _FoamDetail,
                    _FoamPatternScale,
                    _FoamPatternSpeed,
                    _FoamPatternWarp) * _FoamStrength;
                float2 flow = SabaSafeDirection(_FlowDirection.xy);
                float remnant = SabaCrestFoamLite(
                    input.worldPosition - float3(flow.x, 0.0, flow.y) * 0.65,
                    _WaveScale,
                    _WaveSpeed,
                    _FlowDirection.xy,
                    max(0.0, _CrestFoamThreshold - 0.08),
                    _CrestFoamWidth * 1.8,
                    1.0,
                    _FoamPatternScale * 0.83,
                    _FoamPatternSpeed * 0.61,
                    _FoamPatternWarp) * _FoamTrailStrength * _FoamStrength;
                float shore = (1.0 - SabaUvEdgeFade(input.uv, _ShoreFoamWidth)) * _FoamStrength;
                float slopeAeration = saturate((1.0 - saturate(baseNormal.y)) * 3.5);
                float flowFoam = smoothstep(0.5, 0.86, flowData.x)
                    * slopeAeration * _FlowFoamStrength;
                float foam = saturate(
                    crest + remnant + shore + flowFoam + ripple * _FoamStrength * 0.18);
                float3 litFoam = _FoamColor.rgb
                    * lerp(float3(1.0, 1.0, 1.0), surfaceLighting, _LightingResponse);
                water = lerp(water, litFoam, foam);

                float alpha = _Opacity * SabaUvEdgeFade(input.uv, _EdgeFade);
                return fixed4(water, alpha);
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
                    saturate(dot(normal, halfDirection)), lerp(12.0, 192.0, _Smoothness))
                    * _Smoothness;
                UNITY_LIGHT_ATTENUATION(attenuation, input, input.worldPosition);
                float coverage = _Opacity * SabaUvEdgeFade(input.uv, _EdgeFade);
                float3 contribution = _LightColor0.rgb * attenuation
                    * (diffuse * 0.12 + specular * 0.7) * coverage * _LightingResponse;
                return fixed4(contribution, 0.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
