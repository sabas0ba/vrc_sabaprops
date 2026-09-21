Shader "SabaProps/Water/Droplet Projector"
{
    Properties
    {
        _Color ("Droplet Tint", Color) = (0.78, 0.9, 1, 1)
        _Strength ("Opacity", Range(0, 1)) = 0.55
        _DropletScale ("Droplet Density", Range(1, 80)) = 18
        _DropletSpeed ("Fall Speed", Range(0, 2)) = 0.28
        _TrailPersistence ("Trail Persistence", Range(0, 1)) = 0.72
        _TrailSlide ("Trail Slide", Range(0, 1)) = 0.24
        _Relief ("Highlight Relief", Range(0, 1)) = 0.35
        _EdgeFade ("Projection Edge Fade", Range(0.001, 0.3)) = 0.08
        _AmbientResponse ("Ambient Lighting Response", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+30" "IgnoreProjector"="True" }
        Pass
        {
            ZWrite Off
            ColorMask RGB
            Offset -1, -1
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "SabaDropletField.cginc"
            float4x4 unity_Projector;
            float4x4 unity_ProjectorClip;
            fixed4 _Color;
            float _Strength, _Relief, _EdgeFade, _AmbientResponse;
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 projected : TEXCOORD0;
                float4 falloff : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(v2f, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.pos = UnityObjectToClipPos(input.vertex);
                output.projected = mul(unity_Projector, input.vertex);
                output.falloff = mul(unity_ProjectorClip, input.vertex);
                output.worldNormal = UnityObjectToWorldNormal(input.normal);
                return output;
            }
            fixed4 frag(v2f input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                clip(input.projected.w - 0.0001);
                float2 uv = input.projected.xy / input.projected.w;
                // Unity's projector falloff coordinate traverses near to far.
                float depth = input.falloff.x / input.falloff.w;
                float boundary = min(min(uv.x, 1.0-uv.x), min(uv.y, 1.0-uv.y));
                boundary = min(boundary, min(depth, 1.0-depth));
                clip(boundary);
                float fade = smoothstep(0.0, _EdgeFade, boundary);
                float4 drops = SabaDropletField(uv);
                float epsilon = 0.012 / max(1.0, _DropletScale);
                float gradient = SabaDropletField(uv + float2(epsilon, epsilon)).x - drops.x;
                float relief = lerp(0.65, 1.35, saturate(0.5 + gradient * _Relief * 8.0));
                // Projector draws need not receive the receiver's probe constants.
                float3 ambient = max(unity_AmbientSky.rgb,
                    max(UNITY_LIGHTMODEL_AMBIENT.rgb,
                    max(0.0, ShadeSH9(float4(normalize(input.worldNormal), 1.0)))));
                float3 lighting = lerp(float3(1,1,1), ambient, _AmbientResponse);
                return fixed4(_Color.rgb * lighting * relief,
                    saturate(drops.x * _Strength * _Color.a * fade));
            }
            ENDCG
        }
    }
    Fallback Off
}
