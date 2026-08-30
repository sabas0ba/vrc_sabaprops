Shader "SabaProps/Water/Surface Lite"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.16, 0.48, 0.55, 1)
        _DeepColor ("Deep Color", Color) = (0.015, 0.11, 0.18, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.72
        _Smoothness ("Smoothness", Range(0, 1)) = 0.82
        _WaveScale ("Wave Scale", Float) = 1.8
        _WaveStrength ("Normal Strength", Range(0, 1)) = 0.12
        _WaveSpeed ("Wave Speed", Float) = 0.35
        _FlowDirection ("Flow Direction", Vector) = (1, 0.2, 0, 0)
        _VertexWaveHeight ("Vertex Wave Height", Range(0, 0.5)) = 0
        _EdgeFade ("UV Edge Fade", Range(0, 0.5)) = 0
        _RippleStrength ("Rain Ripple Strength", Range(0, 1)) = 0
        _RippleDensity ("Rain Ripple Density", Float) = 1.5
        _RippleSpeed ("Rain Ripple Speed", Float) = 0.8
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

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "SabaWaterCommon.cginc"

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

                float3 worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                worldPosition.y += SabaWaterHeight(
                    worldPosition, _WaveScale, _WaveSpeed, _FlowDirection.xy) * _VertexWaveHeight;

                output.position = UnityWorldToClipPos(worldPosition);
                output.worldPosition = worldPosition;
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                output.uv = input.uv;
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
                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                float fresnel = pow(1.0 - saturate(dot(normal, viewDirection)), 4.0);
                float diffuse = saturate(dot(normal, lightDirection));
                float3 halfDirection = normalize(viewDirection + lightDirection);
                float specularPower = lerp(12.0, 192.0, _Smoothness);
                float specular = pow(saturate(dot(normal, halfDirection)), specularPower) * _Smoothness;

                float ripple = SabaRainRipple(
                    input.worldPosition.xz, _RippleDensity, _RippleSpeed) * _RippleStrength;
                float colourDepth = saturate(fresnel * 0.55 + (1.0 - diffuse) * 0.2);
                float3 water = lerp(_ShallowColor.rgb, _DeepColor.rgb, colourDepth);
                float3 reflection = SabaReflectionProbe(viewDirection, normal);
                water = lerp(water, reflection, fresnel * (0.25 + _Smoothness * 0.35));
                water += _LightColor0.rgb * (diffuse * 0.08 + specular * 0.65 + ripple * 0.22);
                water += UNITY_LIGHTMODEL_AMBIENT.rgb * 0.12;

                float alpha = _Opacity * SabaUvEdgeFade(input.uv, _EdgeFade);
                return fixed4(water, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
