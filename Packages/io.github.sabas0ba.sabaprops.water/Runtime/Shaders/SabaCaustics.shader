Shader "SabaProps/Water/Caustics"
{
    Properties
    {
        _Color ("Caustics Color", Color) = (0.32, 0.85, 1, 0.28)
        _Scale ("Scale", Float) = 2
        _Speed ("Speed", Float) = 0.7
        _Sharpness ("Sharpness", Range(1, 16)) = 8
        _Intensity ("Intensity", Range(0, 2)) = 0.5
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+15" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        Offset -1, -1
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Scale;
            float _Speed;
            float _Sharpness;
            float _Intensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float3 worldPosition : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 position = input.worldPosition.xz * _Scale;
                float first = sin(position.x + _Time.y * _Speed + sin(position.y * 1.31));
                float second = sin(position.y * 1.47 - _Time.y * _Speed * 0.83 + sin(position.x));
                float lines = pow(saturate(1.0 - abs(first - second)), _Sharpness);
                float2 edgeDistance = abs(input.uv * 2.0 - 1.0);
                float edge = 1.0 - smoothstep(0.82, 1.0, max(edgeDistance.x, edgeDistance.y));
                fixed4 colour = _Color;
                colour.rgb *= _Intensity;
                colour.a *= lines * edge;
                return colour;
            }
            ENDCG
        }
    }
    Fallback Off
}
