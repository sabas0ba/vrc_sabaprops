Shader "SabaProps/Body Contact/Debug Lines"
{
    SubShader
    {
        Tags { "Queue"="Overlay+10" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        CGINCLUDE
        #include "UnityCG.cginc"
        struct appdata
        {
            float4 vertex : POSITION;
            fixed4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct v2f
        {
            float4 position : SV_POSITION;
            fixed4 color : COLOR;
            UNITY_VERTEX_OUTPUT_STEREO
        };
        v2f vert(appdata input)
        {
            v2f output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_OUTPUT(v2f, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.position = UnityObjectToClipPos(input.vertex);
            output.color = input.color;
            return output;
        }
        fixed4 fragVisible(v2f input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return input.color;
        }
        fixed4 fragHidden(v2f input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            return fixed4(input.color.rgb, input.color.a * 0.4);
        }
        ENDCG

        // 奥に隠れた線と手前の線を明度で区別し、判定形状の位置関係を残します。
        Pass
        {
            Name "OCCLUDED"
            ZTest Greater
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragHidden
            #pragma multi_compile_instancing
            ENDCG
        }
        Pass
        {
            Name "VISIBLE"
            ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragVisible
            #pragma multi_compile_instancing
            ENDCG
        }
    }
}
