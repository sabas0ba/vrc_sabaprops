Shader "SabaProps/Screen FX/Composite Lite"
{
    Properties
    {
        [Header(Volume)]
        _Weight ("Weight", Range(0, 1)) = 1
        _EdgeFade ("Edge Fade (m)", Range(0, 10)) = 1
        _LightResponse ("Lighting Response", Range(0, 1)) = 0.8

        [Header(Lens)]
        _LensDrops ("Lens Drops", Range(0, 1)) = 0
        _LensDropScale ("Lens Drop Scale", Range(2, 30)) = 8
        _LensDropSlide ("Lens Drop Slide", Range(0, 2)) = 0.3
        _Frost ("Frost", Range(0, 1)) = 0
        _FrostColor ("Frost Color", Color) = (0.85, 0.93, 1, 0.9)
        _Splat ("Splat", Range(0, 1)) = 0
        _SplatColor ("Splat Color", Color) = (0.2, 0.13, 0.07, 1)
        _SplatScale ("Splat Scale", Range(0.5, 12)) = 3
        _SplatDrip ("Splat Drip", Range(0, 1)) = 0.05

        [Header(Color)]
        _Exposure ("Exposure (EV)", Range(-5, 5)) = 0
        _Tint ("Tint (A = Amount)", Color) = (1, 1, 1, 0)

        [Header(Fog)]
        _FogColor ("Fog Color", Color) = (0.7, 0.75, 0.78, 1)
        _FogVeil ("Fog Veil", Range(0, 1)) = 0
        _FogNoise ("Fog Noise", Range(0, 1)) = 0

        [Header(Glare)]
        _Glare ("Glare", Range(0, 1)) = 0
        _GlareColor ("Glare Color", Color) = (1, 0.97, 0.9, 1)
        _GlareDirection ("Glare Direction (World, 0 = Uniform)", Vector) = (0, 0, 0, 0)
        _GlareFocus ("Glare Focus", Range(1, 64)) = 8

        [Header(Vignette)]
        _Vignette ("Vignette", Range(0, 1)) = 0
        _VignetteColor ("Vignette Color", Color) = (0, 0, 0, 1)
        _VignetteRadius ("Vignette Radius", Range(0, 1.5)) = 0.6
        _VignetteSoftness ("Vignette Softness", Range(0.05, 1.5)) = 0.6
        _Pulse ("Pulse", Range(0, 1)) = 0
        _PulseRate ("Pulse Rate (Hz)", Range(0.2, 4)) = 1.2

        [Header(Particles)]
        _Particle ("Particles", Range(0, 1)) = 0
        _ParticleColor ("Particle Color", Color) = (1, 1, 1, 0.8)
        _ParticleSize ("Particle Size", Range(0.02, 0.25)) = 0.12
        _ParticleDensity ("Particle Density", Range(1, 20)) = 6
        _ParticleVelocity ("Particle Velocity (X Drift, Y Fall)", Vector) = (0, 0.4, 0, 0)
        _ParticleStretch ("Particle Stretch", Range(0, 30)) = 0
        _ParticleSway ("Particle Sway", Range(0, 1)) = 0

        [Header(Speed Lines)]
        _SpeedLines ("Speed Lines", Range(0, 1)) = 0
        _SpeedLineColor ("Speed Line Color", Color) = (1, 1, 1, 0.6)
        _SpeedLineInner ("Speed Line Inner Radius", Range(0, 1.5)) = 0.5
        _SpeedLineRate ("Speed Line Rate", Range(0, 8)) = 3

        [Header(Caustics)]
        _Caustics ("Caustics", Range(0, 1)) = 0
        _CausticsScale ("Caustics Scale", Range(0.5, 20)) = 4

        [Header(Grain)]
        _Grain ("Grain", Range(0, 1)) = 0

        [Header(Eyelids)]
        _Blink ("Eyelid Closure", Range(0, 1)) = 0
        _BlinkAuto ("Automatic Blink", Range(0, 1)) = 0
        _BlinkRate ("Blink Rate (Hz)", Range(0.05, 2)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Overlay"
            "RenderType" = "Overlay"
            "IgnoreProjector" = "True"
            "DisableBatching" = "True"
        }

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Cull Off
            ZWrite Off
            ZTest Always
            Blend One OneMinusSrcAlpha

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex SabaFxVert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "SabaScreenFxCore.cginc"

            fixed4 frag(SabaFxV2f input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SabaFxContext context = SabaFxMakeContext(input);
                SabaFxLens lens = SabaFxComputeLens(context);

                float3 add = float3(0.0, 0.0, 0.0);
                float transmittance = 1.0;
                SabaFxGradeLite(context, add, transmittance);
                SabaFxOverlay(context, lens, 0.0, add, transmittance);
                return fixed4(add, 1.0 - transmittance);
            }
            ENDCG
        }
    }
    Fallback Off
}
