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

        [Header("Motion")]
        [Min(0.01f)] public float waveScale = 1.8f;
        [Range(0f, 1f)] public float waveStrength = 0.12f;
        [Min(0f)] public float waveSpeed = 0.35f;
        public Vector2 flowDirection = new Vector2(1f, 0.2f);
        [Range(0f, 0.5f)] public float vertexWaveHeight;

        [Header("Puddle and rain")]
        [Range(0f, 0.5f)] public float edgeFade;
        [Range(0f, 1f)] public float rippleStrength;
        [Min(0.1f)] public float rippleDensity = 1.5f;
        [Min(0f)] public float rippleSpeed = 0.8f;

        [Header("Standard quality")]
        [Range(0f, 0.1f)] public float refractionStrength = 0.018f;
        [Min(0f)] public float depthDistance = 3f;

        public void Normalize()
        {
            waveScale = Mathf.Max(0.01f, waveScale);
            waveStrength = Mathf.Clamp01(waveStrength);
            waveSpeed = Mathf.Max(0f, waveSpeed);
            vertexWaveHeight = Mathf.Clamp(vertexWaveHeight, 0f, 0.5f);
            edgeFade = Mathf.Clamp(edgeFade, 0f, 0.5f);
            rippleStrength = Mathf.Clamp01(rippleStrength);
            rippleDensity = Mathf.Max(0.1f, rippleDensity);
            rippleSpeed = Mathf.Max(0f, rippleSpeed);
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
            material.SetFloat("_WaveScale", waveScale);
            material.SetFloat("_WaveStrength", waveStrength);
            material.SetFloat("_WaveSpeed", waveSpeed);
            material.SetVector("_FlowDirection", new Vector4(flowDirection.x, flowDirection.y, 0f, 0f));
            material.SetFloat("_VertexWaveHeight", vertexWaveHeight);
            material.SetFloat("_EdgeFade", edgeFade);
            material.SetFloat("_RippleStrength", rippleStrength);
            material.SetFloat("_RippleDensity", rippleDensity);
            material.SetFloat("_RippleSpeed", rippleSpeed);
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
