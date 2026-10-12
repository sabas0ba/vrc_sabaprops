using UnityEditor;
using UnityEngine;

namespace SabaProps.Liquid.Editors
{
    /// <summary>Generated base colours plus deterministic, approximate micro-surface maps.</summary>
    public static class LiquidApartmentMaterials
    {
        public const string TextureFolder = "Packages/io.github.sabas0ba.sabaprops.liquid/Textures/Apartment";
        private const string Folder = LiquidAssets.MaterialFolder + "/Apartment/Finish";

        public static Material Get(string name, Color tint)
        {
            string image = null;
            float smoothness = 0.32f, scale = 1, strength = 0.06f;
            int pattern = 0;
            if (name == "Oak") { image = "Oak"; tint = Color.white; smoothness = 0.42f; }
            if (name == "Floor") { image = "Floor"; tint = Color.white; scale = 0.5f; smoothness = 0.48f; }
            if (name == "Sofa" || name == "Linen" || name == "Rug" || name == "Curtain")
            { image = "Linen"; pattern = 1; scale = 3; smoothness = 0.08f; strength = 0.14f; }
            if (name == "Tile" || name == "Counter")
            { image = "Stone"; tint = Color.white; pattern = 2; scale = 1; smoothness = name == "Counter" ? 0.65f : 0.38f; }
            if (name == "Wallpaper") { image = "Stone"; pattern = 2; scale = 4; smoothness = 0.1f; strength = 0.015f; }
            if (name == "Ceramic") smoothness = 0.72f;
            if (name == "Metal" || name == "Screen") smoothness = 0.84f;
            Material material = LiquidAssets.CreateOrLoadSurfaceMaterial(Folder + "/" + name + ".mat", tint, smoothness);
            material.color = tint;
            material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_Metallic", name == "Metal" ? 0.85f : 0);
            material.enableInstancing = true;
            if (image != null)
            {
                material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/" + image + ".png");
                material.mainTextureScale = Vector2.one * scale;
                material.SetTexture("_BumpMap", MicroNormal(pattern));
                material.SetFloat("_BumpScale", strength);
                material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_MetallicGlossMap", Smoothness(name, smoothness));
                material.SetFloat("_GlossMapScale", 1);
                material.EnableKeyword("_METALLICGLOSSMAP");
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D Smoothness(string name, float mean)
        {
            string path = Folder + "/Smoothness_" + name + ".asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null) return texture;
            const int resolution = 128;
            texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, true);
            texture.name = "Approximate smoothness " + name;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Trilinear;
            var pixels = new Color[resolution * resolution];
            for (int y = 0; y < resolution; y++)
                for (int x = 0; x < resolution; x++)
                {
                    float u = x * Mathf.PI * 2 / resolution, v = y * Mathf.PI * 2 / resolution;
                    float variation = Mathf.Sin(u * 7 + Mathf.Cos(v * 3)) * Mathf.Cos(v * 11);
                    pixels[y * resolution + x] = new Color(0, 0, 0, Mathf.Clamp01(mean + variation * Mathf.Min(0.05f, mean * 0.2f)));
                }
            texture.SetPixels(pixels); texture.Apply(true, true);
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        private static Texture2D MicroNormal(int pattern)
        {
            string path = Folder + "/MicroNormal" + pattern + ".asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture != null) return texture;
            const int resolution = 256;
            texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, true);
            texture.name = "Approximate micro surface " + pattern;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Trilinear;
            var pixels = new Color[resolution * resolution];
            for (int y = 0; y < resolution; y++)
                for (int x = 0; x < resolution; x++)
                {
                    float u = x * Mathf.PI * 2 / resolution, v = y * Mathf.PI * 2 / resolution;
                    float dx = Mathf.Cos(u * 24 + Mathf.Sin(v * 2)), dy = 0.1f * Mathf.Sin(v * 4);
                    if (pattern == 1) { dx = Mathf.Cos(u * 32); dy = Mathf.Cos(v * 32); }
                    if (pattern == 2) { dx = Mathf.Cos(u * 17 + v * 13); dy = Mathf.Sin(v * 19 - u * 7); }
                    Vector3 normal = new Vector3(dx, dy, 2).normalized;
                    pixels[y * resolution + x] = new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1);
                }
            texture.SetPixels(pixels); texture.Apply(true, true);
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        public static void ConfigureLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.55f, 0.59f);
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.38f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.19f, 0.18f);
            foreach (Light light in Object.FindObjectsOfType<Light>())
            {
                if (light.type == LightType.Directional)
                {
                    light.transform.rotation = Quaternion.Euler(35, 145, 0);
                    light.intensity = 1.25f;
                    light.color = new Color(1, 0.94f, 0.82f);
                    light.shadows = LightShadows.Soft;
                }
                if (light.name.EndsWith(" lamp"))
                {
                    light.shadows = LightShadows.Soft;
                    light.shadowBias = 0.03f;
                    light.shadowNormalBias = 0.12f;
                    light.color = new Color(1, 0.95f, 0.88f);
                }
                if (light.name == "Living UV") light.enabled = false;
            }
        }

        public static void CaptureReflections()
        {
            bool asynchronous = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try { CaptureReflectionsSynchronous(); }
            finally { ShaderUtil.allowAsyncCompilation = asynchronous; }
        }

        private static void CaptureReflectionsSynchronous()
        {
            foreach (Renderer renderer in GameObject.Find("Apartment").GetComponentsInChildren<Renderer>())
                if (renderer.gameObject.layer == 0)
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.ReflectionProbeStatic);
            Vector3[] centres = { new Vector3(-2.5f, 1.5f, 2), new Vector3(3.5f, 1.5f, 3), new Vector3(3.5f, 1.5f, -2) };
            Vector3[] sizes = { new Vector3(7, 3, 6), new Vector3(5, 3, 4), new Vector3(5, 3, 6) };
            for (int i = 0; i < centres.Length; i++)
            {
                string path = Folder + "/Reflection" + i + ".exr";
                var probe = new GameObject("Apartment reflection " + i).AddComponent<ReflectionProbe>();
                probe.transform.position = centres[i];
                probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;
                probe.resolution = 128; probe.hdr = true; probe.cullingMask = 1;
                probe.nearClipPlane = 0.05f; probe.farClipPlane = 30;
                probe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.SolidColor;
                probe.backgroundColor = new Color(0.55f, 0.64f, 0.73f);
                probe.size = sizes[i]; probe.boxProjection = true; probe.blendDistance = 0.3f;
                if (!Lightmapping.BakeReflectionProbe(probe, path))
                    throw new System.InvalidOperationException("Could not bake apartment reflection " + i);
                AssetDatabase.ImportAsset(path);
                probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Custom;
                probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            }
        }
    }
}
