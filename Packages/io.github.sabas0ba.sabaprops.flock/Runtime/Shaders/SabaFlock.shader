Shader "SabaProps/Flock/Swarm"
{
    Properties
    {
        [Header(Lighting)]
        _Wrap ("Diffuse Wrap", Range(0, 1)) = 0.3
        _SheenStrength ("Scale Sheen Strength", Range(0, 2)) = 0.8
        _SheenPower ("Scale Sheen Sharpness", Range(2, 128)) = 24

        [Header(Distance)]
        _SilhouetteColor ("Silhouette Color (A = Strength)", Color) = (0.12, 0.12, 0.14, 1)
        _SilhouetteStart ("Silhouette Start (m)", Float) = 80
        _SilhouetteEnd ("Silhouette End (m)", Float) = 300
        _MediumColor ("Water / Haze Color", Color) = (0.1, 0.35, 0.45, 1)
        _MediumDensity ("Water / Haze Density (1/m)", Range(0, 1)) = 0

        [Header(Motion)]
        _TimeScale ("Time Scale", Float) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" "DisableBatching" = "True" }
        Cull Back

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "SabaFlockRendering.cginc"
            ENDCG
        }
        Pass
        {
            Tags { "LightMode" = "ForwardAdd" }
            Blend One One
            ZWrite Off
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #define SABA_FLOCK_ADDITIVE_PASS
            #include "SabaFlockRendering.cginc"
            ENDCG
        }
    }
    Fallback Off
}
