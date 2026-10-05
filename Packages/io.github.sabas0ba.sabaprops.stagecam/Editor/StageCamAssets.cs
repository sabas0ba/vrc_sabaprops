using System.IO;
using UnityEditor;
using UnityEngine;

namespace SabaProps.StageCam.Editors
{
    /// <summary>
    /// The asset paths the demo writes into the project, and the small helpers
    /// that create them.
    /// <para>
    /// Everything here goes under <c>Assets/</c>, never into the package.
    /// VCC replaces a package folder wholesale on upgrade, so anything written
    /// there would be thrown away along with any edits made to it.
    /// </para>
    /// </summary>
    public static class StageCamAssets
    {
        public const string RootFolder = "Assets/SabaProps/StageCam";
        public const string SampleFolder = RootFolder + "/Samples";

        /// <summary>
        /// The layer the screens sit on, excluded from the rig cameras.
        /// <para>
        /// Without this the cameras film the screens that are showing what they
        /// filmed, and the picture smears into a tunnel. Layer 4 is Water in
        /// both Unity's defaults and VRChat's layer list, and world objects are
        /// allowed on it.
        /// </para>
        /// </summary>
        public const int ScreenLayer = 4;

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
        /// Creates a render texture asset, or returns the one already there.
        /// <para>
        /// 16:9 at 1024 wide, with a depth buffer because the rig cameras render
        /// a normal opaque scene into it. Sized rather than left at the default
        /// so the demo states a cost rather than inheriting one: a realtime
        /// camera into a render texture is the expensive part of this package.
        /// </para>
        /// </summary>
        public static RenderTexture CreateOrLoadRenderTexture(string assetPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));

            var texture = new RenderTexture(1024, 576, 24, RenderTextureFormat.Default)
            {
                name = Path.GetFileNameWithoutExtension(assetPath),
                antiAliasing = 1,
                useMipMap = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            AssetDatabase.CreateAsset(texture, assetPath);
            return texture;
        }

        /// <summary>Creates an unlit material showing a texture, or loads it.</summary>
        public static Material CreateOrLoadScreenMaterial(string assetPath, Texture texture)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing != null)
            {
                existing.mainTexture = texture;
                EditorUtility.SetDirty(existing);
                return existing;
            }

            // Unlit, because a screen is emissive: lighting it as a surface would
            // make the picture change with where it is standing.
            Shader shader = Shader.Find("Unlit/Texture");
            if (shader == null)
            {
                Debug.LogError("[SabaProps Stage Cam] Unlit/Texture シェーダーが見つかりません。");
                return null;
            }

            EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));

            var material = new Material(shader) { mainTexture = texture };
            AssetDatabase.CreateAsset(material, assetPath);
            return material;
        }

        /// <summary>Creates an opaque Standard material, or loads it.</summary>
        public static Material CreateOrLoadSurfaceMaterial(string assetPath, Color colour, float smoothness)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("[SabaProps Stage Cam] Standard シェーダーが見つかりません。");
                return null;
            }

            EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));

            var material = new Material(shader);
            material.color = colour;
            material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_Metallic", 0f);

            AssetDatabase.CreateAsset(material, assetPath);
            return material;
        }
    }
}
