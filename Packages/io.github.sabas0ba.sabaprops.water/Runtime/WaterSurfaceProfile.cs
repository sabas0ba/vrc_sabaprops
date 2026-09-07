using UnityEngine;

namespace SabaProps.Water
{
    public enum WaterBodyKind
    {
        Puddle,
        River,
        Lake,
        Ocean,
    }

    public enum WaterQuality
    {
        Lite,
        Standard,
    }

    /// <summary>
    /// Material parameters shared by generated water surfaces.
    /// The material stores the baked values, so the profile is not required at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "SabaProps/Water/Surface Profile", fileName = "WaterSurfaceProfile")]
    public sealed class WaterSurfaceProfile : ScriptableObject
    {
        public const string LiteShaderName = "SabaProps/Water/Surface Lite";
        public const string StandardShaderName = "SabaProps/Water/Surface Standard";

        public WaterBodyKind bodyKind = WaterBodyKind.Lake;
        public WaterQuality quality = WaterQuality.Lite;
        public Material material;

        [Header("Colour")]
        public Color shallowColor = new Color(0.16f, 0.48f, 0.55f, 1f);
        public Color deepColor = new Color(0.015f, 0.11f, 0.18f, 1f);
        [Range(0f, 1f)] public float opacity = 0.72f;
        [Range(0f, 1f)] public float smoothness = 0.82f;
        [Range(0f, 1f)] public float lightingResponse = 0.85f;

        [Header("Motion")]
        [Min(0.01f)] public float waveScale = 1.8f;
        [Range(0f, 1f)] public float waveStrength = 0.12f;
        [Min(0f)] public float waveSpeed = 0.35f;
        public Vector2 flowDirection = new Vector2(1f, 0.2f);
        [Range(0f, 0.5f)] public float vertexWaveHeight;
        [Range(0f, 0.5f)] public float tideHeight;
        [Range(0f, 1f)] public float tideSpeed = 0.04f;

        [Header("Puddle and rain")]
        [Range(0f, 0.5f)] public float edgeFade;
        [Range(0f, 1f)] public float rippleStrength;
        [Min(0.1f)] public float rippleDensity = 1.5f;
        [Min(0f)] public float rippleSpeed = 0.8f;

        [Header("Depth and foam")]
        [Range(0f, 0.5f)] public float shallowEdgeWidth;
        public Color foamColor = new Color(0.86f, 0.95f, 1f, 1f);
        [Range(0f, 1f)] public float foamStrength;
        [Range(0f, 1f)] public float crestFoamThreshold = 0.8f;
        [Range(0.01f, 0.35f)] public float crestFoamWidth = 0.08f;
        [Range(0f, 1f)] public float foamTrailStrength = 0.2f;
        [Range(0f, 1f)] public float foamDetail = 0.6f;
        [Range(0f, 0.5f)] public float shoreFoamWidth;

        [Header("Flow and aeration")]
        [Range(0f, 1f)] public float flowTurbulence;
        [Range(0f, 1f)] public float flowFoamStrength;

        [Header("Reflection Probe")]
        [Range(0f, 1.5f)] public float reflectionStrength = 0.65f;
        [Range(0f, 1f)] public float reflectionDistortion = 0.18f;
        [Range(0f, 1f)] public float reflectionBlur = 0.18f;
        [Range(0f, 1f)] public float rippleReflectionBlur = 0.35f;

        [Header("Standard quality")]
        [Range(0f, 0.1f)] public float refractionStrength = 0.018f;
        [Min(0f)] public float depthDistance = 3f;

        public void Normalize()
        {
            waveScale = Mathf.Max(0.01f, waveScale);
            lightingResponse = Mathf.Clamp01(lightingResponse);
            waveStrength = Mathf.Clamp01(waveStrength);
            waveSpeed = Mathf.Max(0f, waveSpeed);
            vertexWaveHeight = Mathf.Clamp(vertexWaveHeight, 0f, 0.5f);
            tideHeight = Mathf.Clamp(tideHeight, 0f, 0.5f);
            tideSpeed = Mathf.Clamp01(tideSpeed);
            edgeFade = Mathf.Clamp(edgeFade, 0f, 0.5f);
            rippleStrength = Mathf.Clamp01(rippleStrength);
            rippleDensity = Mathf.Max(0.1f, rippleDensity);
            rippleSpeed = Mathf.Max(0f, rippleSpeed);
            shallowEdgeWidth = Mathf.Clamp(shallowEdgeWidth, 0f, 0.5f);
            foamStrength = Mathf.Clamp01(foamStrength);
            crestFoamThreshold = Mathf.Clamp01(crestFoamThreshold);
            crestFoamWidth = Mathf.Clamp(crestFoamWidth, 0.01f, 0.35f);
            foamTrailStrength = Mathf.Clamp01(foamTrailStrength);
            foamDetail = Mathf.Clamp01(foamDetail);
            shoreFoamWidth = Mathf.Clamp(shoreFoamWidth, 0f, 0.5f);
            flowTurbulence = Mathf.Clamp01(flowTurbulence);
            flowFoamStrength = Mathf.Clamp01(flowFoamStrength);
            reflectionStrength = Mathf.Clamp(reflectionStrength, 0f, 1.5f);
            reflectionDistortion = Mathf.Clamp01(reflectionDistortion);
            reflectionBlur = Mathf.Clamp01(reflectionBlur);
            rippleReflectionBlur = Mathf.Clamp01(rippleReflectionBlur);
            refractionStrength = Mathf.Clamp(refractionStrength, 0f, 0.1f);
            depthDistance = Mathf.Max(0.01f, depthDistance);

            if (flowDirection.sqrMagnitude < 1e-6f)
            {
                flowDirection = Vector2.right;
            }
            else
            {
                flowDirection.Normalize();
            }
        }

        /// <summary>Writes the profile into its material without creating runtime dependencies.</summary>
        public void ApplyToMaterial()
        {
            Normalize();

            if (material == null)
            {
                return;
            }

            string shaderName = quality == WaterQuality.Standard
                ? StandardShaderName
                : LiteShaderName;
            Shader shader = Shader.Find(shaderName);
            if (shader != null && material.shader != shader)
            {
                material.shader = shader;
            }

            material.SetColor("_ShallowColor", shallowColor);
            material.SetColor("_DeepColor", deepColor);
            material.SetFloat("_Opacity", opacity);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_LightingResponse", lightingResponse);
            material.SetFloat("_WaveScale", waveScale);
            material.SetFloat("_WaveStrength", waveStrength);
            material.SetFloat("_WaveSpeed", waveSpeed);
            material.SetVector("_FlowDirection", new Vector4(flowDirection.x, flowDirection.y, 0f, 0f));
            material.SetFloat("_VertexWaveHeight", vertexWaveHeight);
            material.SetFloat("_TideHeight", tideHeight);
            material.SetFloat("_TideSpeed", tideSpeed);
            material.SetFloat("_EdgeFade", edgeFade);
            material.SetFloat("_RippleStrength", rippleStrength);
            material.SetFloat("_RippleDensity", rippleDensity);
            material.SetFloat("_RippleSpeed", rippleSpeed);
            material.SetFloat("_ShallowEdgeWidth", shallowEdgeWidth);
            material.SetColor("_FoamColor", foamColor);
            material.SetFloat("_FoamStrength", foamStrength);
            material.SetFloat("_CrestFoamThreshold", crestFoamThreshold);
            material.SetFloat("_CrestFoamWidth", crestFoamWidth);
            material.SetFloat("_FoamTrailStrength", foamTrailStrength);
            material.SetFloat("_FoamDetail", foamDetail);
            material.SetFloat("_ShoreFoamWidth", shoreFoamWidth);
            material.SetFloat("_FlowTurbulence", flowTurbulence);
            material.SetFloat("_FlowFoamStrength", flowFoamStrength);
            material.SetFloat("_ReflectionStrength", reflectionStrength);
            material.SetFloat("_ReflectionDistortion", reflectionDistortion);
            material.SetFloat("_ReflectionBlur", reflectionBlur);
            material.SetFloat("_RippleReflectionBlur", rippleReflectionBlur);
            material.SetFloat("_RefractionStrength", refractionStrength);
            material.SetFloat("_DepthDistance", depthDistance);
            material.enableInstancing = true;
        }

        private void OnValidate()
        {
            ApplyToMaterial();
        }
    }
}
