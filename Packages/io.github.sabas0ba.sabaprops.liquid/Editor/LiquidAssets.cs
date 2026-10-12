using System.IO;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Liquid.Editors
{
    /// <summary>
    /// The asset paths the generators write into the project, and the helpers
    /// that create them.
    /// <para>
    /// Everything here goes under <c>Assets/</c>, never into the package. VCC
    /// replaces a package folder wholesale on upgrade, so anything written there
    /// would be thrown away along with any edits made to it.
    /// </para>
    /// </summary>
    public static class LiquidAssets
    {
        public const string RootFolder = "Assets/SabaProps/Liquid";
        public const string MaterialFolder = RootFolder + "/Materials";

        public const string BodyProjectorShader = "SabaProps/Liquid/Body Projector";
        public const string CanvasUpdateShader = "Hidden/SabaProps/Liquid/Canvas Update";

        public const string CanvasUpdateMaterialPath = MaterialFolder + "/LiquidCanvasUpdate.mat";

        /// <summary>Creates every folder along an "Assets/a/b/c" style path.</summary>
        public static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] parts = folderPath.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        /// <summary>
        /// The material path for one Body Canvas projector.
        /// <para>
        /// Each canvas needs its own material: a Projector cannot take a
        /// MaterialPropertyBlock, and Udon cannot construct a Material, so the
        /// per-canvas textures and rows can only live on separate assets.
        /// </para>
        /// </summary>
        public static string BodyProjectorMaterialPath(int index)
        {
            return MaterialFolder + "/LiquidBodyProjector_" + index.ToString("00") + ".mat";
        }

        /// <summary>
        /// Loads an opaque Standard material, or creates it with the given colour
        /// and smoothness.
        /// </summary>
        public static Material CreateOrLoadSurfaceMaterial(string assetPath, Color colour, float smoothness)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            Material material = CreateOrLoadMaterial(assetPath, "Standard");
            if (material == null)
            {
                return null;
            }

            material.color = colour;
            material.SetFloat("_Glossiness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Loads a transparent Standard material, or creates it.
        /// <para>
        /// The Standard shader's "Transparent" mode is an inspector convenience:
        /// the inspector sets these blend states, keywords and queue when the
        /// mode changes. A material made from code has to set them itself.
        /// </para>
        /// </summary>
        public static Material CreateOrLoadTransparentMaterial(string assetPath, Color colour, float smoothness)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            Material material = CreateOrLoadMaterial(assetPath, "Standard");
            if (material == null)
            {
                return null;
            }

            material.color = colour;
            material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        public const string DropletTexturePath = MaterialFolder + "/LiquidDroplet.asset";

        /// <summary>
        /// A soft round droplet for the stream particles. Generated rather than
        /// taken from the editor's built-in particle texture, which is not an
        /// asset a scene can carry into a package sample.
        /// </summary>
        public static Texture2D CreateOrLoadDropletTexture()
        {
            return CreateOrLoadDropletTexture(MaterialFolder);
        }

        /// <summary>The droplet texture in <paramref name="folder"/>.</summary>
        public static Texture2D CreateOrLoadDropletTexture(string folder)
        {
            string path = folder + "/LiquidDroplet.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder(folder);
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "LiquidDroplet" };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((1f - r) * 3f);
                    // A brighter spot up and to the left, so a droplet reads as a lit bead.
                    float highlight = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(dx, dy), new Vector2(-0.35f, 0.35f)) * 2.5f);
                    float shade = Mathf.Lerp(0.75f, 1f, highlight);
                    texture.SetPixel(x, y, new Color(shade, shade, shade, alpha));
                }
            }

            texture.Apply();
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        /// <summary>
        /// A soft, uneven puff for steam: a gaussian blob broken up by a few
        /// overlapping lobes, so neighbouring puffs do not look stamped.
        /// </summary>
        public static Texture2D CreateOrLoadPuffTexture(string folder)
        {
            string path = folder + "/LiquidPuff.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder(folder);
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "LiquidPuff" };
            Vector2[] lobes = { new Vector2(-0.25f, -0.1f), new Vector2(0.2f, 0.15f), new Vector2(0.05f, -0.3f), new Vector2(-0.1f, 0.3f) };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                    float alpha = Mathf.Exp(-p.sqrMagnitude * 3f) * 0.6f;
                    foreach (Vector2 lobe in lobes)
                    {
                        alpha += Mathf.Exp(-(p - lobe).sqrMagnitude * 12f) * 0.25f;
                    }

                    alpha *= Mathf.Clamp01((1f - p.magnitude) * 2.5f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
                }
            }

            texture.Apply();
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        /// <summary>
        /// The translucent particle material the streams share. Settings are
        /// applied every time, so an older material from a previous version of
        /// the generator is brought up to date.
        /// </summary>
        public static Material StreamMaterial(string assetPath)
        {
            return StreamMaterial(assetPath, MaterialFolder);
        }

        /// <summary>The stream material, with its texture kept in <paramref name="textureFolder"/>.</summary>
        public static Material StreamMaterial(string assetPath, string textureFolder)
        {
            return FadeParticleMaterial(assetPath, CreateOrLoadDropletTexture(textureFolder));
        }

        /// <summary>The steam material: soft puffs, faded in and out by the particle colour.</summary>
        public static Material PuffMaterial(string assetPath, string textureFolder)
        {
            return FadeParticleMaterial(assetPath, CreateOrLoadPuffTexture(textureFolder));
        }

        private static Material FadeParticleMaterial(string assetPath, Texture2D texture)
        {
            Material material = CreateOrLoadMaterial(assetPath, "Particles/Standard Unlit");
            if (material == null)
            {
                return null;
            }

            material.mainTexture = texture;
            // Fade mode, set the way the particle shader's inspector sets it.
            material.SetFloat("_Mode", 2f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_ALPHABLEND_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Loads a material using the given shader, or creates it.</summary>
        public static Material CreateOrLoadMaterial(string assetPath, string shaderName)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("[SabaProps Liquid] シェーダー " + shaderName + " が見つかりません。");
                return null;
            }

            EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));

            var material = new Material(shader);
            AssetDatabase.CreateAsset(material, assetPath);
            return material;
        }
    }
}
