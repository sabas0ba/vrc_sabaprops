Shader "SabaProps/Water/Splash"
{
    Properties
    {
        _Color ("Splash Color", Color) = (0.75, 0.9, 1, 0.75)
        _Softness ("Edge Softness", Range(0.01, 0.5)) = 0.18
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
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
            float _Softness;

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
                float radius = length(input.uv * 2.0 - 1.0);
                float alpha = 1.0 - smoothstep(1.0 - _Softness, 1.0, radius);
                fixed4 colour = input.color;
                colour.a *= alpha;
                return colour;
            }
            ENDCG
        }
    }
    Fallback Off
}
