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
        _AerationStart ("Aeration Inception", Range(0, 1)) = 0.28
        _AerationGrowth ("Aeration Growth", Range(0.02, 0.8)) = 0.3
        _BubbleDetail ("Bubble Detail", Range(0, 1)) = 0.8
        _ClearFlowStrength ("Clear Flow Streaks", Range(0, 1)) = 0.2
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
            float _AerationStart;
            float _AerationGrowth;
            float _BubbleDetail;
            float _ClearFlowStrength;

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
                float longitudinalVortex = sin(
                    flowUv.x * 8.7 + sin(flowUv.y * 2.1 - time * 0.2) * 1.25);
                float broad = sin(
                    flowUv.y * 6.283 + longitudinalVortex * 0.72 + time * 0.17);
                float detail = sin(
                    flowUv.y * 17.31 - flowUv.x * 13.17 + time * 1.43);
                float bubbles = sin(
                    flowUv.y * 39.17 + flowUv.x * 31.73 - time * 3.7)
                    * sin(flowUv.y * 23.11 - flowUv.x * 27.41 + time * 2.9);
                float worldBreakup = sin(
                    input.worldPosition.x * 3.17 + input.worldPosition.z * 2.31 - time * 2.1);
                float roughSurface = saturate(
                    0.54 + broad * 0.28 + detail * 0.13 + worldBreakup * 0.12);
                float bubbleField = smoothstep(0.08, 0.82, 0.5 + bubbles * 0.5);
                float aeration = smoothstep(
                    _AerationStart,
                    min(1.0, _AerationStart + max(0.02, _AerationGrowth)),
                    input.uv.y);
                float clearStreaks = smoothstep(0.58, 0.9,
                    0.5 + longitudinalVortex * 0.32 + detail * 0.18) * (1.0 - aeration);
                float foam = smoothstep(_Breakup * 0.55, 0.95, roughSurface);
                foam = saturate(
                    foam * lerp(_ClearFlowStrength, 1.0, aeration)
                    + bubbleField * aeration * _BubbleDetail * 0.62
                    + clearStreaks * _ClearFlowStrength);

                float edge = smoothstep(0.0, _EdgeFade, input.uv.x)
                    * smoothstep(0.0, _EdgeFade, 1.0 - input.uv.x);
                float leading = smoothstep(0.0, 0.16, input.uv.y);
                float sideFilaments = (1.0 - edge) * aeration
                    * smoothstep(0.25, 0.8, 0.5 + detail * 0.5);
                float alpha = saturate(foam * edge + sideFilaments * 0.45)
                    * leading * _Opacity;
                float whiteness = saturate(foam * 0.72 + aeration * 0.42 + bubbleField * aeration * 0.25);
                float3 colour = lerp(_SecondaryColor.rgb, _Color.rgb, whiteness);
                return fixed4(colour, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
