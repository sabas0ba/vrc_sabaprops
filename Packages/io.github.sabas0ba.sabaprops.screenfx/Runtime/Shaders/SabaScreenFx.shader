Shader "SabaProps/Screen FX/Composite"
{
    Properties
    {
        [Header(Volume)]
        _Weight ("Weight", Range(0, 1)) = 1
        _EdgeFade ("Edge Fade (m)", Range(0, 10)) = 1
        _LightResponse ("Lighting Response", Range(0, 1)) = 0.8
        [Toggle(_SABA_FX_DEPTH)] _UseDepth ("Use Scene Depth", Float) = 0

        [Header(Distortion)]
        _Wobble ("Wobble", Range(0, 1)) = 0
        _WobbleSpeed ("Wobble Speed", Range(0, 5)) = 1
        _WobbleScale ("Wobble Scale", Range(0.2, 8)) = 1
        _Haze ("Heat Haze", Range(0, 1)) = 0
        _HazeScale ("Heat Haze Scale", Range(1, 40)) = 12
        _HazeSpeed ("Heat Haze Speed", Range(0, 5)) = 1
        _DoubleVision ("Double Vision", Range(0, 1)) = 0

        [Header(Sampling)]
        _Blur ("Blur", Range(0, 1)) = 0
        _BlurEdge ("Blur Edge Only", Range(0, 1)) = 0
        _RadialBlur ("Radial Blur", Range(0, 1)) = 0
        _Chromatic ("Chromatic Aberration", Range(0, 1)) = 0

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
        _Saturation ("Saturation", Range(0, 2)) = 1
        _Contrast ("Contrast", Range(0.5, 2)) = 1
        _Tint ("Tint (A = Amount)", Color) = (1, 1, 1, 0)

        [Header(Fog)]
        _FogColor ("Fog Color", Color) = (0.7, 0.75, 0.78, 1)
        _FogVeil ("Fog Veil", Range(0, 1)) = 0
        _FogDensity ("Fog Density (Depth)", Range(0, 1)) = 0
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

        // Unnamed on purpose: each volume grabs what is behind it, so
        // overlapping volumes stack instead of the last one winning.
        GrabPass { }

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Cull Off
            ZWrite Off
            ZTest Always
            Blend One Zero

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex SabaFxVert
            #pragma fragment frag
            #pragma shader_feature_local _SABA_FX_DEPTH
            #pragma multi_compile_instancing
            #include "SabaScreenFxCore.cginc"
            #include "SabaScreenFxSampling.cginc"

            UNITY_DECLARE_SCREENSPACE_TEXTURE(_GrabTexture);
            float4 _GrabTexture_TexelSize;
            #if defined(_SABA_FX_DEPTH)
                UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            #endif

            // The taps run inside dynamic branches, where implicit-derivative
            // sampling is not allowed.
            #if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                #define SABA_FX_GRAB_SLICE(uv) float3(uv, (float)unity_StereoEyeIndex)
                #define SABA_FX_SAMPLE_GRAB(uv) UNITY_SAMPLE_TEX2DARRAY_LOD(_GrabTexture, SABA_FX_GRAB_SLICE(uv), 0.0).rgb
            #else
                #define SABA_FX_SAMPLE_GRAB(uv) tex2Dlod(_GrabTexture, float4(uv, 0.0, 0.0)).rgb
            #endif

            // Eight points of a sunflower spiral inside the unit disc.
            static const float2 SabaFxDisc[8] =
            {
                float2(0.250, 0.000), float2(-0.319, 0.292),
                float2(0.049, -0.557), float2(0.402, 0.525),
                float2(-0.739, -0.131), float2(0.700, -0.444),
                float2(-0.234, 0.870), float2(-0.445, -0.860),
            };

            inline float3 SampleScene(float2 grabUv, float2 offset, SabaFxContext context, SabaFxLens lens)
            {
                // q is y-up; the grab texture may not be.
            #if UNITY_UV_STARTS_AT_TOP
                float2 toUv = float2(0.5, -0.5 * _ProjectionParams.x);
            #else
                float2 toUv = float2(0.5, 0.5 * _ProjectionParams.x);
            #endif
            #if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                toUv.y = 0.5;
            #endif
                float4 eyeViewport = float4(1.0, 1.0, 0.0, 0.0);
            #if defined(UNITY_SINGLE_PASS_STEREO) && !defined(UNITY_STEREO_INSTANCING_ENABLED) && !defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                eyeViewport = unity_StereoScaleOffset[unity_StereoEyeIndex];
            #endif

                float weight = context.weight;
                float edge = saturate(length(context.q) * 0.8);
                float blur = _Blur * weight * lerp(1.0, edge * edge, _BlurEdge)
                    + lens.frostMask * 0.5;
                float radial = _RadialBlur * weight;
                float chromatic = _Chromatic * weight;
                float doubled = _DoubleVision * weight;

                UNITY_BRANCH
                if (blur + radial + chromatic + doubled < 1e-3)
                {
                    return SABA_FX_SAMPLE_GRAB(SabaFxGrabUv(grabUv, offset * toUv, eyeViewport, _GrabTexture_TexelSize.xy));
                }

                // A per-pixel rotation trades the banding of eight fixed taps for noise.
                float sine;
                float cosine;
                sincos(SabaFxHash21(context.pixel) * SABA_FX_TWO_PI, sine, cosine);

                float3 sum = float3(0.0, 0.0, 0.0);
                float3 weightSum = float3(0.0, 0.0, 0.0);
                for (int index = 0; index < 8; index++)
                {
                    float fraction = (index + 0.5) / 8.0;
                    float2 disc = SabaFxDisc[index];
                    float2 tap = float2(
                        disc.x * cosine - disc.y * sine,
                        disc.x * sine + disc.y * cosine) * (blur * 0.06);
                    tap -= context.q * (fraction * radial * 0.25);
                    tap += context.q * ((fraction - 0.5) * chromatic * 0.06);
                    tap += float2(0.07, 0.02) * (doubled * (frac(index * 0.5) * 4.0 - 1.0));

                    // Taps further along the radial axis lean towards blue,
                    // which turns the radial spread into a colour fringe.
                    float3 spectral = saturate(1.0 - abs(2.0 * fraction - float3(0.0, 1.0, 2.0)));
                    float3 tapWeight = lerp(
                        float3(1.0, 1.0, 1.0), spectral, saturate(chromatic * 8.0));
                    sum += SABA_FX_SAMPLE_GRAB(SabaFxGrabUv(grabUv, (offset + tap) * toUv, eyeViewport, _GrabTexture_TexelSize.xy)) * tapWeight;
                    weightSum += tapWeight;
                }

                return sum / weightSum;
            }

            fixed4 frag(SabaFxV2f input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SabaFxContext context = SabaFxMakeContext(input);
                SabaFxLens lens = SabaFxComputeLens(context);
                float2 offset = lens.offset + SabaFxDistortion(context);

                float3 colour = SampleScene(input.screenUv.xy, offset, context, lens);
                colour = SabaFxGrade(colour, context);

                float depthFog = 0.0;
            #if defined(_SABA_FX_DEPTH)
                float sceneDepth = LinearEyeDepth(
                    SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, input.screenUv.zw));
                depthFog = 1.0 - exp(-max(0.0, sceneDepth) * _FogDensity * context.weight);
            #endif

                float3 add = float3(0.0, 0.0, 0.0);
                float transmittance = 1.0;
                SabaFxOverlay(context, lens, depthFog, add, transmittance);
                return fixed4(colour * transmittance + add, 1.0);
            }
            ENDCG
        }
    }
    Fallback "SabaProps/Screen FX/Composite Lite"
}
