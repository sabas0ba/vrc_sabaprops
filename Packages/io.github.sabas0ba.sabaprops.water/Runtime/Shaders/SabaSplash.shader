Shader "SabaProps/Water/Splash"
{
    Properties
    {
        _Color ("Splash Color", Color) = (0.75, 0.9, 1, 0.75)
        _Softness ("Edge Softness", Range(0.01, 0.5)) = 0.18
        _LightingResponse ("Lighting Response", Range(0, 1)) = 0.9
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_particles
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            fixed4 _Color;
            float _Softness;
            float _LightingResponse;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                half3 probeLighting : TEXCOORD1;
                float3 worldPosition : TEXCOORD2;
                half3 worldNormal : TEXCOORD3;
                LIGHTING_COORDS(4, 5)
            };

            v2f vert(appdata v)
            {
                v2f output;
                output.pos = UnityObjectToClipPos(v.vertex);
                output.color = v.color * _Color;
                output.uv = v.uv;
                output.worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;
                output.worldNormal = UnityObjectToWorldNormal(v.normal);
                output.probeLighting = max(
                    0.0, ShadeSH9(float4(normalize(output.worldNormal), 1.0)));
                TRANSFER_VERTEX_TO_FRAGMENT(output);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 centred = input.uv * 2.0 - 1.0;
                float taper = lerp(0.48, 1.0, saturate(input.uv.y * 1.25));
                float radius = length(float2(centred.x / taper, centred.y * 0.84));
                float alpha = 1.0 - smoothstep(1.0 - _Softness, 1.0, radius);
                float3 lightDirection = normalize(UnityWorldSpaceLightDir(input.worldPosition));
                half diffuse = saturate(dot(normalize(input.worldNormal), lightDirection));
                UNITY_LIGHT_ATTENUATION(attenuation, input, input.worldPosition);
                half3 lighting = input.probeLighting + _LightColor0.rgb * diffuse * attenuation;
                fixed4 colour = input.color;
                colour.rgb *= lerp(half3(1.0, 1.0, 1.0), lighting, _LightingResponse);
                colour.a *= alpha;
                return colour;
            }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode" = "ForwardAdd" }
            Blend SrcAlpha One

            CGPROGRAM
            #pragma target 2.0
            #pragma vertex vertAdd
            #pragma fragment fragAdd
            #pragma multi_compile_particles
            #pragma multi_compile_fwdadd_fullshadows
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            fixed4 _Color;
            float _Softness;
            float _LightingResponse;

            struct appdataAdd
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2fAdd
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 worldPosition : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                LIGHTING_COORDS(3, 4)
            };

            v2fAdd vertAdd(appdataAdd v)
            {
                v2fAdd output;
                output.pos = UnityObjectToClipPos(v.vertex);
                output.color = v.color * _Color;
                output.uv = v.uv;
                output.worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;
                output.worldNormal = UnityObjectToWorldNormal(v.normal);
                TRANSFER_VERTEX_TO_FRAGMENT(output);
                return output;
            }

            fixed4 fragAdd(v2fAdd input) : SV_Target
            {
                float2 centred = input.uv * 2.0 - 1.0;
                float taper = lerp(0.48, 1.0, saturate(input.uv.y * 1.25));
                float radius = length(float2(centred.x / taper, centred.y * 0.84));
                float alpha = 1.0 - smoothstep(1.0 - _Softness, 1.0, radius);
                float3 lightDirection = normalize(UnityWorldSpaceLightDir(input.worldPosition));
                float diffuse = saturate(dot(normalize(input.worldNormal), lightDirection));
                UNITY_LIGHT_ATTENUATION(attenuation, input, input.worldPosition);
                fixed4 colour = input.color;
                colour.rgb *= _LightColor0.rgb * diffuse * attenuation * _LightingResponse;
                colour.a *= alpha;
                return colour;
            }
            ENDCG
        }
    }
    Fallback Off
}
