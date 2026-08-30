Shader "SabaProps/Water/Light Shaft"
{
    Properties
    {
        _Color ("Shaft Color", Color) = (0.2, 0.72, 0.92, 0.12)
        _Intensity ("Intensity", Range(0, 2)) = 0.5
        _PulseSpeed ("Pulse Speed", Float) = 0.25
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+25" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            CGPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Intensity;
            float _PulseSpeed;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPosition : TEXCOORD1;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float across = 1.0 - abs(input.uv.x * 2.0 - 1.0);
                float along = sin(input.uv.y * UNITY_PI);
                float pulse = sin(input.worldPosition.x * 0.7 + input.worldPosition.z * 0.5
                    + _Time.y * _PulseSpeed) * 0.15 + 0.85;
                fixed4 colour = _Color;
                colour.rgb *= _Intensity;
                colour.a *= across * across * along * pulse;
                return colour;
            }
            ENDCG
        }
    }
    Fallback Off
}
