Shader "SabaProps/Water/Fog Particle"
{
    Properties
    {
        _Color ("Fog Color", Color) = (0.68, 0.76, 0.8, 0.2)
        _NoiseScale ("Noise Scale", Float) = 2
        _NoiseAmount ("Noise Amount", Range(0, 1)) = 0.55
        _NoiseSpeed ("Noise Speed", Vector) = (0.03, 0.01, 0, 0)
        _InvFade ("Soft Particle Factor", Range(0.01, 5)) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_particles
            #include "UnityCG.cginc"

            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            fixed4 _Color;
            float _NoiseScale;
            float _NoiseAmount;
            float4 _NoiseSpeed;
            float _InvFade;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 projected : TEXCOORD1;
                float3 worldPosition : TEXCOORD2;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.projected = ComputeScreenPos(output.position);
                output.projected.z = -UnityObjectToViewPos(input.vertex).z;
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.color = input.color * _Color;
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 centred = input.uv * 2.0 - 1.0;
                float radial = saturate(1.0 - dot(centred, centred));
                radial = radial * radial * (3.0 - 2.0 * radial);

                float2 noisePosition = input.worldPosition.xz * _NoiseScale + _Time.y * _NoiseSpeed.xy;
                float noise = sin(noisePosition.x + sin(noisePosition.y * 1.71));
                noise = noise * 0.5 + 0.5;
                noise = lerp(1.0, noise, _NoiseAmount);

                float soft = 1.0;
                #if defined(SOFTPARTICLES_ON)
                    float sceneDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(
                        _CameraDepthTexture, UNITY_PROJ_COORD(input.projected)));
                    soft = saturate(_InvFade * (sceneDepth - input.projected.z));
                #endif

                fixed4 colour = input.color;
                colour.a *= radial * noise * soft;
                return colour;
            }
            ENDCG
        }
    }
    Fallback Off
}
