Shader "SabaProps/Water/Rain"
{
    Properties
    {
        _Color ("Rain Color", Color) = (0.72, 0.86, 1, 0.72)
        _CoreWidth ("Core Width", Range(0.02, 0.8)) = 0.22
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
            float _CoreWidth;

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
                float across = abs(input.uv.x * 2.0 - 1.0);
                float core = 1.0 - smoothstep(_CoreWidth, 1.0, across);
                float ends = smoothstep(0.0, 0.14, input.uv.y)
                    * smoothstep(0.0, 0.14, 1.0 - input.uv.y);
                fixed4 colour = input.color;
                colour.a *= core * ends;
                return colour;
            }
            ENDCG
        }
    }
    Fallback Off
}
