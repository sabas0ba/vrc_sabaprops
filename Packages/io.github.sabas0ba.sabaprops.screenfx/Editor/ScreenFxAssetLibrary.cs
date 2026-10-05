using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SabaProps.ScreenFx.Editors
{
    /// <summary>Owns generated project assets and stable Shader names.</summary>
    public static class ScreenFxAssetLibrary
    {
        public const string RootFolder = "Assets/SabaProps/ScreenFx";
        public const string MaterialsFolder = RootFolder + "/Materials";

        public const string StandardShaderName = "SabaProps/Screen FX/Composite";
        public const string LiteShaderName = "SabaProps/Screen FX/Composite Lite";
        public const string DepthKeyword = "_SABA_FX_DEPTH";
        public const string LiteSuffix = "_Lite";

        public static string ShaderName(bool lite) => lite ? LiteShaderName : StandardShaderName;

        public static string MaterialName(ScreenFxPreset preset, bool lite) =>
            "ScreenFx_" + preset.id + (lite ? LiteSuffix : string.Empty);

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

        public static List<Object> CreateOrLoadDefaults()
        {
            var assets = new List<Object>();
            foreach (ScreenFxPreset preset in ScreenFxPresets.All)
            {
                foreach (bool lite in new[] { false, true })
                {
                    Material material = CreateOrLoadMaterial(preset, lite);
                    if (material != null)
                    {
                        assets.Add(material);
                    }
                }
            }

            AssetDatabase.SaveAssets();
            return assets;
        }

        /// <summary>
        /// Returns the project copy of a preset material, creating it on first
        /// use. An existing asset is returned as is, so edits made in the
        /// project survive.
        /// </summary>
        public static Material CreateOrLoadMaterial(ScreenFxPreset preset, bool lite)
        {
            EnsureFolder(MaterialsFolder);
            string path = MaterialsFolder + "/" + MaterialName(preset, lite) + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Material material = CreateMaterial(preset, lite);
            if (material != null)
            {
                AssetDatabase.CreateAsset(material, path);
            }

            return material;
        }

        /// <summary>An unsaved material holding the preset, for tests and previews.</summary>
        public static Material CreateMaterial(ScreenFxPreset preset, bool lite)
        {
            Shader shader = LoadShader(ShaderName(lite));
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader)
            {
                name = MaterialName(preset, lite),
                enableInstancing = true,
            };
            SetValues(material, preset);
            return material;
        }

        /// <summary>Resets every property to the shader default, then applies the preset.</summary>
        public static void ApplyPreset(Material material, ScreenFxPreset preset)
        {
            var defaults = new Material(material.shader);
            material.CopyPropertiesFromMaterial(defaults);
            Object.DestroyImmediate(defaults);
            material.DisableKeyword(DepthKeyword);
            SetValues(material, preset);
            EditorUtility.SetDirty(material);
        }

        /// <summary>
        /// Switches the distance fog on. Only meaningful on the GrabPass shader,
        /// and only in a world that renders a camera depth texture.
        /// </summary>
        public static void SetUseDepth(Material material, bool useDepth)
        {
            material.SetFloat("_UseDepth", useDepth ? 1f : 0f);
            if (useDepth)
            {
                material.EnableKeyword(DepthKeyword);
            }
            else
            {
                material.DisableKeyword(DepthKeyword);
            }
        }

        public static Shader LoadShader(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError(
                    $"[SabaProps Screen FX] Shader '{shaderName}' が見つかりません。packageのimport状態を確認してください。");
            }

            return shader;
        }

        private static void SetValues(Material material, ScreenFxPreset preset)
        {
            foreach (ScreenFxValue value in preset.values)
            {
                // The Lite shader carries a subset of the properties.
                if (!material.HasProperty(value.property))
                {
                    continue;
                }

                switch (value.kind)
                {
                    case ScreenFxValueKind.Float:
                        material.SetFloat(value.property, value.value.x);
                        break;
                    case ScreenFxValueKind.Color:
                        material.SetColor(value.property, value.value);
                        break;
                    default:
                        material.SetVector(value.property, value.value);
                        break;
                }
            }
        }
    }
}
