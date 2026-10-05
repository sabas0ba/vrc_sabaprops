// SabaProps Foliage - Built-in Render Pipeline, GPU instancing friendly.
//
// Designed for VRChat worlds and avatars, where no C# runs at runtime. Every
// per-instance effect (colour variation, wind phase, distance shrink) is
// derived from data that already lives in the mesh or in the object-to-world
// matrix, so nothing needs to be pushed from script.
Shader "SabaProps/Foliage"
{
    Properties
    {
        [Header(Albedo)]
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo (optional)", 2D) = "white" {}
        [Toggle(_ALPHATEST_ON)] _AlphaTest ("Alpha Cutout", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

        [Header(Per Instance Variance)]
        _HueVariance ("Hue", Range(0, 0.5)) = 0.035
        _SaturationVariance ("Saturation", Range(0, 1)) = 0.15
        _ValueVariance ("Brightness", Range(0, 1)) = 0.22

        [Header(Wind)]
        _WindDirection ("Direction (XZ in xz)", Vector) = (1, 0, 0.4, 0)
        _WindStrength ("Strength", Range(0, 2)) = 0.18
        _WindSpeed ("Speed", Range(0, 8)) = 1.5
        _WindWaveLength ("Wave Length (m)", Range(0.5, 64)) = 12
        _WindTurbulence ("Turbulence", Range(0, 1)) = 0.35
        _WindGust ("Gust", Range(0, 1)) = 0.5
        _BendPower ("Bend Falloff", Range(1, 8)) = 2.2

        [Header(Distance)]
        [Toggle(_DISTANCEFADE_ON)] _DistanceFade ("Distance Shrink", Float) = 1
        _FadeStart ("Shrink Start (m)", Float) = 35
        _FadeEnd ("Shrink End (m)", Float) = 55

        [Header(Lighting)]
        _Wrap ("Diffuse Wrap", Range(0, 1)) = 0.45
        _Translucency ("Translucency", Range(0, 2)) = 0.65
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 0
    }

    SubShader
    {
        // DisableBatching is mandatory: static/dynamic batching bakes vertices
        // into world space, which would destroy both the per-object matrix the
        // wind relies on and Unity's ability to GPU instance these renderers.
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+50"
            "DisableBatching" = "True"
            "IgnoreProjector" = "True"
        }

        LOD 200
        Cull [_Cull]

        CGPROGRAM
        #pragma surface surf SabaFoliage vertex:vert addshadow fullforwardshadows noforwardadd nodynlightmap
        #pragma target 3.0
        #pragma multi_compile_instancing
        #pragma instancing_options assumeuniformscaling
        #pragma shader_feature_local _ALPHATEST_ON
        #pragma shader_feature_local _DISTANCEFADE_ON

        #include "SabaFoliageCore.cginc"

        sampler2D _MainTex;
        fixed4 _Color;
        half _Cutoff;

        half _HueVariance;
        half _SaturationVariance;
        half _ValueVariance;

        float4 _WindDirection;
        half _WindStrength;
        half _WindSpeed;
        half _WindWaveLength;
        half _WindTurbulence;
        half _WindGust;
        half _BendPower;

        float _FadeStart;
        float _FadeEnd;

        half _Wrap;
        half _Translucency;

        struct Input
        {
            float2 uv_MainTex;
            float4 color : COLOR;
        };

        // Wrapped diffuse plus a cheap back-lit term. No specular: foliage
        // rarely needs it and this keeps the per-pixel cost close to Lambert.
        half4 LightingSabaFoliage(SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
        {
            half ndl = dot(s.Normal, lightDir);
            half wrapped = saturate((ndl + _Wrap) / (1.0 + _Wrap));

            // Light travelling through the blade towards the viewer.
            half back = saturate(dot(-viewDir, lightDir));
            back *= back;

            half3 direct = s.Albedo * _LightColor0.rgb * (wrapped * atten);
            half3 through = s.Albedo * _LightColor0.rgb * (back * _Translucency * atten * 0.5);

            return half4(direct + through, s.Alpha);
        }

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);

            float3 rootOS = v.texcoord3.xyz;
            float stiffness = v.texcoord3.w;
            float elementSeed = v.color.a;
            float encodedBend = v.texcoord.y;
            float surfaceConstraint = step(0.5, -encodedBend);
            float heightRatio = saturate(lerp(
                encodedBend,
                -encodedBend - 1.0,
                surfaceConstraint));
            // Restore the regular UV before the surface shader interpolates it
            // for texture sampling.
            v.texcoord.y = heightRatio;

            float3 rootWS = mul(unity_ObjectToWorld, float4(rootOS, 1.0)).xyz;

            // One seed per element. In GPU Instanced mode rootWS differs per
            // blade and per instance; in Merged Chunks mode the merge bakes the
            // instance offset into rootOS, so the same expression keeps working.
            float seed = frac(SabaFoliageHash13(rootWS) + elementSeed * 0.6180339);

            #ifdef _DISTANCEFADE_ON
                float dist = distance(rootWS, _WorldSpaceCameraPos);
                float shrink = 1.0 - saturate((dist - _FadeStart) / max(_FadeEnd - _FadeStart, 0.001));
                // Collapse towards the element's own root, not the pivot, so
                // merged chunks shrink blade by blade instead of as one lump.
                v.vertex.xyz = lerp(rootOS, v.vertex.xyz, shrink);
            #endif

            float3 posWS = mul(unity_ObjectToWorld, v.vertex).xyz;

            float bend = pow(heightRatio, _BendPower) * stiffness;
            float3 windWS = SabaFoliageWind(
                rootWS, posWS, bend, seed,
                _WindDirection.xz, _WindStrength, _WindSpeed,
                _WindWaveLength, _WindTurbulence, _WindGust);

            // Surface-grown foliage uses its vertex normal as the supporting
            // surface normal and enables clipping through encoded UV0.y.
            // Remove only displacement into that surface; tangential and
            // outward movement remain, so the leaves still sway without
            // crossing a wall or floor. Ordinary foliage keeps UV0.y in 0..1.
            float3 windOS = mul((float3x3)unity_WorldToObject, windWS);
            float3 surfaceNormalOS = normalize(v.normal);
            float inwardWind = min(0.0, dot(windOS, surfaceNormalOS));
            windOS -= surfaceNormalOS * inwardWind * surfaceConstraint;

            v.vertex.xyz += windOS;

            // Bake the per-instance tint into the interpolated vertex colour so
            // the fragment stage stays as cheap as possible.
            v.color.rgb = SabaFoliageVaryColor(
                v.color.rgb, seed, _HueVariance, _SaturationVariance, _ValueVariance);
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
            fixed4 c = tex * _Color;
            c.rgb *= IN.color.rgb;

            #ifdef _ALPHATEST_ON
                clip(c.a - _Cutoff);
            #endif

            o.Albedo = c.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }

    FallBack "Diffuse"
    CustomEditor "SabaProps.Foliage.Editors.SabaFoliageShaderGUI"
}
