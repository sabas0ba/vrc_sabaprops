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
        _DropletHeadNormalStrength ("Droplet Head Normal", Range(0, 2)) = 0.34
        _DropletTrailNormalStrength ("Droplet Trail Normal", Range(0, 2)) = 0.14
        _DropletSpeed ("Droplet Speed", Range(0, 2)) = 0.28
        _TrailPersistence ("Trail Persistence", Range(0, 1)) = 0.72
        _TrailSlide ("Trail Slide", Range(0, 1)) = 0.24
        _DropletScatterColor ("Small Droplet Scatter", Color) = (0.68, 0.88, 0.96, 1)
        _DropletScatterStrength ("Small Droplet Scatter Strength", Range(0, 1)) = 0.62
        [HideInInspector] _DropletStrength ("Legacy Droplet Normal", Range(0, 2)) = 0.65
        [HideInInspector] _Opacity ("Opacity", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 250

        CGPROGRAM
        #pragma target 3.0
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma multi_compile_instancing
        #include "SabaWetCommon.cginc"
        ENDCG
    }
    Fallback "Standard"
}
