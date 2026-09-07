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
        _DropletScatterColor ("Small Droplet Scatter", Color) = (0.68, 0.88, 0.96, 1)
        _DropletScatterStrength ("Small Droplet Scatter Strength", Range(0, 1)) = 0.62
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
        fixed4 _DropletScatterColor;
        float _DropletScatterStrength;

        struct Input
        {
            float2 uv_MainTex;
        };

        inline float3 SabaDropletField(float2 uv)
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
            float active = step(startDelay, cycle);
            float travellingY = lerp(0.94, -0.12, travel);
            float centreX = lerp(0.18, 0.82, random.x);
            float width = lerp(0.045, 0.078, mass);
            float endProgress = smoothstep(0.72, 1.0, travel);
            float headWidth = width * lerp(1.0, 0.28, endProgress);
            float headFade = 1.0 - smoothstep(0.82, 1.0, travel);
            float2 delta = local - float2(centreX, travellingY);
            float bead = 1.0 - smoothstep(
                headWidth * 0.68,
                headWidth,
                length(delta * float2(1.0, 1.12)));
            bead *= active * headFade;

            float trailSlide = travel * _TrailSlide * lerp(0.55, 1.0, mass);
            float trailOrigin = travellingY + headWidth * 0.28 - trailSlide;
            float behindHead = local.y - trailOrigin;
            float trailLength = lerp(0.36, 0.96, mass) * saturate(travel * 3.0);
            float trailVertical = step(0.0, behindHead)
                * (1.0 - smoothstep(trailLength * 0.58, max(width, trailLength), behindHead));
            float trailWidthValue = width * lerp(0.76, 0.5, saturate(behindHead / max(width, trailLength)));
            float trailWidth = 1.0 - smoothstep(
                trailWidthValue * 0.72,
                trailWidthValue,
                abs(local.x - centreX));
            float trailAge = lerp(0.56, 0.94, _TrailPersistence)
                * (1.0 - smoothstep(0.91, 1.0, travel));
            float trail = trailWidth * trailVertical * trailAge * active;
            float sparse = step(0.34, SabaHash21(cell + 43.7));
            float mask = max(bead, trail * lerp(0.42, 0.76, mass)) * sparse;
            float smallDropletScatter = mask * (1.0 - mass) * (0.65 + bead * 0.35);
            return float3(mask, smallDropletScatter, bead * sparse);
        }

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            fixed4 baseColour = tex2D(_MainTex, input.uv_MainTex) * _Color;
            float wetness = saturate(_Wetness);
            float3 dropletData = SabaDropletField(input.uv_MainTex) * wetness;
            float droplet = dropletData.x;
            float epsilon = 0.012 / max(1.0, _DropletScale);
            float dropletX = SabaDropletField(input.uv_MainTex + float2(epsilon, 0.0)).x;
            float dropletY = SabaDropletField(input.uv_MainTex + float2(0.0, epsilon)).x;

            float3 wetAlbedo = baseColour.rgb * lerp(1.0, 1.0 - _WetDarkening, wetness);
            output.Albedo = lerp(
                wetAlbedo,
                _DropletScatterColor.rgb,
                saturate(dropletData.y * _DropletScatterStrength));
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
