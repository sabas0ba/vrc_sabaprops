Shader "Hidden/SabaProps/ScreenFxSamplingTest"
{
    Properties { _MainTex ("Packed eyes", 2D) = "white" {} }
    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Packages/io.github.sabas0ba.sabaprops.screenfx/Runtime/Shaders/SabaScreenFxSampling.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _Viewport;
            float4 _Delta;
            float4 frag(v2f_img input) : SV_Target
            {
                float2 centre = _Viewport.zw + _Viewport.xy * 0.5;
                float2 uv = SabaFxGrabUv(centre, _Delta.xy, _Viewport, _MainTex_TexelSize.xy);
                return float4(uv, tex2D(_MainTex, uv).b, 1);
            }
            ENDCG
        }
    }
}
