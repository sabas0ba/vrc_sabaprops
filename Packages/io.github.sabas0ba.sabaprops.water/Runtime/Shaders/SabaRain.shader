Shader "SabaProps/Water/Rain"
{
    Properties
    {
        _Color ("Rain Color", Color) = (0.72, 0.86, 1, 0.72)
        _CoreWidth ("Core Width", Range(0.02, 0.8)) = 0.22
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
            #pragma multi_compile_instancing
            #pragma multi_compile_particles
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            fixed4 _Color;
            float _CoreWidth;
            float _LightingResponse;

            struct appdata
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                UNITY_VERTEX_OUTPUT_STEREO
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
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
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
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float across = abs(input.uv.x * 2.0 - 1.0);
                float core = 1.0 - smoothstep(_CoreWidth, 1.0, across);
                float ends = smoothstep(0.0, 0.14, input.uv.y)
                    * smoothstep(0.0, 0.14, 1.0 - input.uv.y);
                float3 lightDirection = normalize(UnityWorldSpaceLightDir(input.worldPosition));
                half diffuse = saturate(dot(normalize(input.worldNormal), lightDirection));
                UNITY_LIGHT_ATTENUATION(attenuation, input, input.worldPosition);
                half3 lighting = input.probeLighting + _LightColor0.rgb * diffuse * attenuation;
                fixed4 colour = input.color;
                colour.rgb *= lerp(half3(1.0, 1.0, 1.0), lighting, _LightingResponse);
                colour.a *= core * ends;
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
            #pragma multi_compile_instancing
            #pragma multi_compile_particles
            #pragma multi_compile_fwdadd_fullshadows
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            fixed4 _Color;
            float _CoreWidth;
            float _LightingResponse;

            struct appdataAdd
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2fAdd
            {
                UNITY_VERTEX_OUTPUT_STEREO
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
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2fAdd, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
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
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float across = abs(input.uv.x * 2.0 - 1.0);
                float core = 1.0 - smoothstep(_CoreWidth, 1.0, across);
                float ends = smoothstep(0.0, 0.14, input.uv.y)
                    * smoothstep(0.0, 0.14, 1.0 - input.uv.y);
                float3 lightDirection = normalize(UnityWorldSpaceLightDir(input.worldPosition));
                float diffuse = saturate(dot(normalize(input.worldNormal), lightDirection));
                UNITY_LIGHT_ATTENUATION(attenuation, input, input.worldPosition);
                fixed4 colour = input.color;
                colour.rgb *= _LightColor0.rgb * diffuse * attenuation * _LightingResponse;
                colour.a *= core * ends;
                return colour;
            }
            ENDCG
        }
    }
    Fallback Off
}
