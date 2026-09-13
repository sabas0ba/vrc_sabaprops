Shader "SabaProps/Llama/Inference"
{
    Properties
    {
        _Weights ("Weights", 2D) = "black" {}
        _A ("A", 2D) = "black" {}
        _B ("B", 2D) = "black" {}
        _C ("C", 2D) = "black" {}
        [HideInInspector] _Operation ("Operation", Int) = 0
        [HideInInspector] _OffsetLo ("Offset low", Int) = 0
        [HideInInspector] _OffsetHi ("Offset high", Int) = 0
        [HideInInspector] _Count ("Count", Int) = 0
        [HideInInspector] _Inner ("Inner", Int) = 0
        [HideInInspector] _Width ("Width", Int) = 1
        [HideInInspector] _AWidth ("Input width", Int) = 1
        [HideInInspector] _HeadSize ("Head size", Int) = 1
        [HideInInspector] _KvMultiple ("KV grouping", Int) = 1
        [HideInInspector] _Position ("Position", Int) = 0
        [HideInInspector] _Token ("Token", Int) = 1
        [HideInInspector] _Rope ("RoPE", Int) = 0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "LlamaKernel.hlsl"
            float4 vert(float4 vertex : POSITION) : SV_POSITION { return UnityObjectToClipPos(vertex); }
            float4 frag(float4 position : SV_POSITION) : SV_Target { return LlamaKernel(int2(position.xy)); }
            ENDHLSL
        }
    }
    Fallback Off
}
