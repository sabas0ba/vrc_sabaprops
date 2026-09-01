Shader "SabaProps/Water/Whitewater"
{
    Properties
    {
        _Color ("Foam Color", Color) = (0.88, 0.96, 1, 1)
        _SecondaryColor ("Aerated Water Color", Color) = (0.36, 0.72, 0.82, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.82
        _FlowScale ("Flow Scale", Float) = 2.4
        _FlowSpeed ("Flow Speed", Float) = 1.3
        _Breakup ("Breakup", Range(0, 1)) = 0.55
        _EdgeFade ("Edge Fade", Range(0.001, 0.5)) = 0.12
    }

    SubShader
    {
        Tags { "Queue" = "Transparent-5" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _SecondaryColor;
            float _Opacity;
            float _FlowScale;
            float _FlowSpeed;
            float _Breakup;
            float _EdgeFade;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(v2f, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xyz;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float time = _Time.y * _FlowSpeed;
                float2 flowUv = float2(input.uv.x, input.uv.y * _FlowScale - time);
                float broad = sin(flowUv.y * 6.283 + sin(flowUv.x * 9.7 + time * 0.37));
                float detail = sin(flowUv.y * 17.31 - flowUv.x * 13.17 + time * 1.43);
                float worldBreakup = sin(
                    input.worldPosition.x * 3.17 + input.worldPosition.z * 2.31 - time * 2.1);
                float foam = saturate(0.58 + broad * 0.31 + detail * 0.16 + worldBreakup * 0.12);
                foam = smoothstep(_Breakup * 0.55, 0.95, foam);

                float edge = smoothstep(0.0, _EdgeFade, input.uv.x)
                    * smoothstep(0.0, _EdgeFade, 1.0 - input.uv.x);
                float leading = smoothstep(0.0, 0.16, input.uv.y);
                float alpha = foam * edge * leading * _Opacity;
                float3 colour = lerp(_SecondaryColor.rgb, _Color.rgb, foam);
                return fixed4(colour, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
