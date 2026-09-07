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
        _TrailPersistence ("Trail Persistence", Range(0, 1)) = 0.72
        _TrailSlide ("Trail Slide", Range(0, 1)) = 0.24
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
        float _TrailPersistence;
        float _TrailSlide;

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
            float mass = saturate(random.y * 0.82 + SabaHash21(cell + 17.9) * 0.3);
            float fallSpeed = lerp(0.38, 1.42, mass);
            float cycle = frac(random.y + _Time.y * _DropletSpeed * fallSpeed);
            float startDelay = lerp(0.44, 0.06, mass);
            float travel = saturate((cycle - startDelay) / max(0.05, 1.0 - startDelay));
            travel = travel * travel * (3.0 - 2.0 * travel);
            float travellingY = lerp(0.94, -0.12, travel);
            float centreX = lerp(0.18, 0.82, random.x);
            float width = lerp(0.045, 0.078, mass);
            float2 delta = local - float2(centreX, travellingY);
            float bead = 1.0 - smoothstep(
                width * 0.72,
                width,
                length(delta * float2(1.0, 1.12)));

            float trailSlide = travel * _TrailSlide * lerp(0.55, 1.0, mass);
            float trailOrigin = travellingY + width * 0.35 - trailSlide;
            float behindHead = local.y - trailOrigin;
            float trailLength = lerp(0.18, 0.62, mass) * saturate(travel * 2.2);
            float trailVertical = step(0.0, behindHead)
                * (1.0 - smoothstep(0.0, max(width, trailLength), behindHead));
            float trailWidth = 1.0 - smoothstep(width * 0.72, width, abs(local.x - centreX));
            float trailAge = lerp(1.0, 1.0 - travel * 0.62, 1.0 - _TrailPersistence);
            float trail = trailWidth * trailVertical * trailAge;
            float sparse = step(0.34, SabaHash21(cell + 43.7));
            return max(bead, trail * lerp(0.38, 0.72, mass)) * sparse;
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
