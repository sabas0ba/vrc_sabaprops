Shader "SabaProps/Water/Fog Volume"
{
    Properties
    {
        _Color ("Fog Color", Color) = (0.62, 0.7, 0.72, 1)
        _Density ("Density", Range(0, 2)) = 0.24
        _NoiseScale ("Noise Scale", Float) = 0.18
        _NoiseAmount ("Noise Amount", Range(0, 1)) = 0.45
        _NoiseSpeed ("Noise Speed", Vector) = (0.03, 0.01, 0.02, 0)
        _HeightFalloff ("Height Falloff", Range(0, 8)) = 1.5
        [Toggle(_FOG_HIGH_QUALITY)] _HighQuality ("High Quality (20 samples)", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+20" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Front
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _FOG_HIGH_QUALITY
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Density;
            float _NoiseScale;
            float _NoiseAmount;
            float4 _NoiseSpeed;
            float _HeightFalloff;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float3 localPosition : TEXCOORD0;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.localPosition = input.vertex.xyz;
                return output;
            }

            inline float densityAt(float3 localPosition, float3 worldPosition)
            {
                float3 animated = worldPosition * _NoiseScale + _Time.y * _NoiseSpeed.xyz;
                float waveA = sin(animated.x + sin(animated.z * 1.37));
                float waveB = sin(animated.z * 1.73 - animated.y * 0.91);
                float noise = saturate((waveA + waveB) * 0.25 + 0.5);
                float height = exp(-max(0.0, localPosition.y + 0.5) * _HeightFalloff);
                return lerp(1.0, noise, _NoiseAmount) * height;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float3 rayOrigin = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos.xyz, 1.0)).xyz;
                float3 rayDirection = normalize(input.localPosition - rayOrigin);
                float3 directionSign = step(0.0, rayDirection) * 2.0 - 1.0;
                float3 safeDirection = directionSign * max(abs(rayDirection), 1e-4);
                float3 inverseDirection = rcp(safeDirection);

                float3 first = (-0.5 - rayOrigin) * inverseDirection;
                float3 second = (0.5 - rayOrigin) * inverseDirection;
                float3 nearer = min(first, second);
                float3 farther = max(first, second);
                float enter = max(max(nearer.x, nearer.y), nearer.z);
                float leave = min(min(farther.x, farther.y), farther.z);
                enter = max(enter, 0.0);
                clip(leave - enter);

                float3 localStart = rayOrigin + rayDirection * enter;
                float3 localEnd = rayOrigin + rayDirection * leave;
                float3 worldStart = mul(unity_ObjectToWorld, float4(localStart, 1.0)).xyz;
                float3 worldEnd = mul(unity_ObjectToWorld, float4(localEnd, 1.0)).xyz;
                float worldLength = distance(worldStart, worldEnd);

                #if defined(_FOG_HIGH_QUALITY)
                    const int sampleCount = 20;
                #else
                    const int sampleCount = 6;
                #endif

                float transmittance = 1.0;
                float accumulated = 0.0;
                [loop]
                for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                {
                    float fraction = (sampleIndex + 0.5) / sampleCount;
                    float3 localSample = lerp(localStart, localEnd, fraction);
                    float3 worldSample = lerp(worldStart, worldEnd, fraction);
                    float localDensity = densityAt(localSample, worldSample);
                    float alpha = 1.0 - exp(-_Density * localDensity * worldLength / sampleCount);
                    accumulated += transmittance * alpha;
                    transmittance *= 1.0 - alpha;
                }

                return fixed4(_Color.rgb, saturate(accumulated * _Color.a));
            }
            ENDCG
        }
    }
    Fallback Off
}
