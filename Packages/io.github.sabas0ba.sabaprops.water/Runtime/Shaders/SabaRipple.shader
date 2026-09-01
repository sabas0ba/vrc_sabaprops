Shader "SabaProps/Water/Ripple"
{
    Properties
    {
        _Color ("Ripple Color", Color) = (0.72, 0.9, 1, 0.65)
        _RingWidth ("Ring Width", Range(0.005, 0.2)) = 0.055
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+5" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        Offset -1, -1
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_particles
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _RingWidth;

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
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.color = input.color * _Color;
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float phase = 1.0 - saturate(input.color.a / max(_Color.a, 1e-4));
                float radius = length(input.uv - 0.5);
                float targetRadius = lerp(0.08, 0.48, phase);
                float ring = 1.0 - smoothstep(_RingWidth, _RingWidth * 1.8, abs(radius - targetRadius));
                float bounds = 1.0 - smoothstep(0.47, 0.5, radius);
                fixed4 colour = input.color;
                colour.a = _Color.a * ring * bounds * (1.0 - phase);
                return colour;
            }
            ENDCG
        }
    }
    Fallback Off
}
