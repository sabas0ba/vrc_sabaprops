// PC VRChat / Built-in Render Pipeline向け。最大8 contactの計算はvertex
// stageだけで行い、fragment costは通常のopaque fabricに近い値に保つ。
Shader "SabaProps/Soft Surface"
{
    Properties
    {
        [Header(Non Toon Surface)]
        _Color ("Tint", Color) = (0.86, 0.67, 0.55, 1)
        _MainTex ("Albedo (optional)", 2D) = "white" {}
        _WeaveScale ("Weave Scale", Range(8, 180)) = 72
        _WeaveContrast ("Weave Contrast", Range(0, 0.3)) = 0
        _SurfaceGrainScale ("Surface Grain Scale", Range(8, 300)) = 145
        _SurfaceGrainStrength ("Surface Grain Strength", Range(0, 0.15)) = 0.032
        _Smoothness ("Smoothness", Range(0, 1)) = 0.045

        [Header(Deformation)]
        _Hardness ("Hardness", Range(0, 1)) = 0.35
        _MaximumIndent ("Maximum Indent (m)", Range(0.005, 0.25)) = 0.09
        _ContactRadius ("Contact Radius (m)", Range(0.04, 0.8)) = 0.24
        _RimLift ("Rim Lift (m)", Range(0, 0.04)) = 0.008
        _WrinkleStrength ("Wrinkle Strength (m)", Range(0, 0.03)) = 0.006
        _WrinkleFrequency ("Wrinkle Frequency", Range(4, 40)) = 18
        _LateralSpread ("Lateral Spread", Range(0, 2)) = 0.65

        [HideInInspector] _Contact0 ("Contact 0", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Contact1 ("Contact 1", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Contact2 ("Contact 2", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Contact3 ("Contact 3", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Contact4 ("Contact 4", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Contact5 ("Contact 5", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Contact6 ("Contact 6", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Contact7 ("Contact 7", Vector) = (0, 0, 0, 0)
        [HideInInspector] _ContactShape0 ("Contact Shape 0", Vector) = (1, 0, 0, 0.055)
        [HideInInspector] _ContactShape1 ("Contact Shape 1", Vector) = (1, 0, 0, 0.055)
        [HideInInspector] _ContactShape2 ("Contact Shape 2", Vector) = (1, 0, 0, 0.055)
        [HideInInspector] _ContactShape3 ("Contact Shape 3", Vector) = (1, 0, 0, 0.055)
        [HideInInspector] _ContactShape4 ("Contact Shape 4", Vector) = (1, 0, 0, 0.055)
        [HideInInspector] _ContactShape5 ("Contact Shape 5", Vector) = (1, 0, 0, 0.055)
        [HideInInspector] _ContactShape6 ("Contact Shape 6", Vector) = (1, 0, 0, 0.055)
        [HideInInspector] _ContactShape7 ("Contact Shape 7", Vector) = (1, 0, 0, 0.055)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "DisableBatching" = "True"
        }
        LOD 260

        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow fullforwardshadows noforwardadd
        #pragma target 3.0

        #include "SabaSoftSurfaceCore.cginc"

        sampler2D _MainTex;
        fixed4 _Color;
        half _WeaveScale;
        half _WeaveContrast;
        half _SurfaceGrainScale;
        half _SurfaceGrainStrength;
        half _Smoothness;

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            float3 positionOS = v.vertex.xyz;
            float3 normalOS = v.normal;
            SabaSoftDeform(positionOS, normalOS, saturate(v.color.r));
            v.vertex.xyz = positionOS;
            v.normal = normalOS;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);

            // 直交する2方向の糸をfragment ALUだけで近似する。texture未指定でも
            // close-upで完全な単色に見えない程度に抑えてある。
            float2 weaveUv = IN.uv_MainTex * _WeaveScale;
            half warp = sin(weaveUv.x * 6.2831853) * 0.5 + 0.5;
            half weft = sin(weaveUv.y * 6.2831853 + 1.5707963) * 0.5 + 0.5;
            half weave = (warp * weft - 0.25) * _WeaveContrast;

            // texture未指定時の乾いた肌／布表面に使う低振幅のmicro grain。
            // Standard lightingを維持し、toon向けの段階化は行わない。
            float2 grainUv = IN.uv_MainTex * _SurfaceGrainScale;
            half grainA = sin(dot(grainUv, float2(0.73, 0.41)) * 6.2831853);
            half grainB = sin(dot(grainUv, float2(-0.29, 0.96)) * 5.117);
            half grain = grainA * grainB * _SurfaceGrainStrength;

            o.Albedo = tex.rgb * _Color.rgb * (1.0 + weave + grain * 0.35);
            o.Metallic = 0;
            o.Smoothness = saturate(_Smoothness + weave * 0.25 - abs(grain) * 0.65);
            o.Occlusion = lerp(0.82, 1.0, IN.color.g);
            o.Alpha = tex.a * _Color.a;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
