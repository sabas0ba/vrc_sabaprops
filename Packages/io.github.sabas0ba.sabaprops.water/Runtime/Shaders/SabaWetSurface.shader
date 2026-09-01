Shader "SabaProps/Water/Wet Surface"
{
    Properties
    {
        _Color ("Base Color", Color) = (0.72, 0.74, 0.76, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Metallic ("Metallic", Range(0, 1)) = 0
        _DrySmoothness ("Dry Smoothness", Range(0, 1)) = 0.28
        _WetSmoothness ("Wet Smoothness", Range(0, 1)) = 0.92
        _Wetness ("Wetness", Range(0, 1)) = 0.75
        _WetDarkening ("Wet Darkening", Range(0, 1)) = 0.32
        _DropletScale ("Droplet Scale", Float) = 18
        _DropletStrength ("Droplet Normal Strength", Range(0, 2)) = 0.65
        _DropletSpeed ("Droplet Speed", Range(0, 2)) = 0.28
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 250

        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma multi_compile_instancing
        #include "SabaWaterCommon.cginc"

        sampler2D _MainTex;
        fixed4 _Color;
        float _Metallic;
        float _DrySmoothness;
        float _WetSmoothness;
        float _Wetness;
        float _WetDarkening;
        float _DropletScale;
        float _DropletStrength;
        float _DropletSpeed;

        struct Input
        {
            float2 uv_MainTex;
        };

        inline float SabaDropletField(float2 uv)
        {
            float2 scaled = uv * max(1.0, _DropletScale);
            float2 cell = floor(scaled);
            float2 local = frac(scaled);
            float2 random = SabaHash22(cell);
            float fallSpeed = lerp(0.45, 1.35, random.y);
            float travellingY = frac(random.y - _Time.y * _DropletSpeed * fallSpeed);
            float2 delta = local - float2(lerp(0.18, 0.82, random.x), travellingY);
            float bead = 1.0 - smoothstep(0.035, 0.16, length(delta * float2(1.0, 1.35)));
            float trailWidth = 1.0 - smoothstep(0.015, 0.055, abs(delta.x));
            float trailLength = smoothstep(-0.7, -0.03, delta.y) * (1.0 - smoothstep(-0.03, 0.08, delta.y));
            float sparse = step(0.34, SabaHash21(cell + 43.7));
            return max(bead, trailWidth * trailLength * 0.46) * sparse;
        }

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            fixed4 baseColour = tex2D(_MainTex, input.uv_MainTex) * _Color;
            float wetness = saturate(_Wetness);
            float droplet = SabaDropletField(input.uv_MainTex) * wetness;
            float epsilon = 0.012 / max(1.0, _DropletScale);
            float dropletX = SabaDropletField(input.uv_MainTex + float2(epsilon, 0.0));
            float dropletY = SabaDropletField(input.uv_MainTex + float2(0.0, epsilon));

            output.Albedo = baseColour.rgb * lerp(1.0, 1.0 - _WetDarkening, wetness);
            output.Metallic = _Metallic;
            output.Smoothness = lerp(_DrySmoothness, _WetSmoothness, wetness);
            output.Normal = normalize(float3(
                (droplet - dropletX) * _DropletStrength,
                (droplet - dropletY) * _DropletStrength,
                1.0));
            output.Occlusion = 1.0;
            output.Alpha = baseColour.a;
        }
        ENDCG
    }
    Fallback "Standard"
}
