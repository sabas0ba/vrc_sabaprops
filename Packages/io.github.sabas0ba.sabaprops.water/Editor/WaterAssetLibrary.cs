using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Water.Editors
{
    /// <summary>Owns generated project assets and stable Shader names.</summary>
    public static class WaterAssetLibrary
    {
        public const string RootFolder = "Assets/SabaProps/Water";
        public const string MaterialsFolder = RootFolder + "/Materials";
        public const string ProfilesFolder = RootFolder + "/Profiles";
        public const string GeneratedFolder = RootFolder + "/Generated";
        public const string GeneratedPuddlesFolder = GeneratedFolder + "/Puddles";
        public const string GeneratedSurfacesFolder = GeneratedFolder + "/Surfaces";
        public const string GeneratedSharedFolder = GeneratedFolder + "/Shared";

        public const string RainShaderName = "SabaProps/Water/Rain";
        public const string SplashShaderName = "SabaProps/Water/Splash";
        public const string RippleShaderName = "SabaProps/Water/Ripple";
        public const string FogParticleShaderName = "SabaProps/Water/Fog Particle";
        public const string FogVolumeShaderName = "SabaProps/Water/Fog Volume";
        public const string UnderwaterLiteShaderName = "SabaProps/Water/Underwater Lite";
        public const string UnderwaterStandardShaderName = "SabaProps/Water/Underwater Standard";
        public const string UnderwaterSurfaceLiteShaderName = "SabaProps/Water/Underwater Surface Lite";
        public const string UnderwaterSurfaceStandardShaderName = "SabaProps/Water/Underwater Surface Standard";
        public const string CausticsShaderName = "SabaProps/Water/Caustics";
        public const string LightShaftShaderName = "SabaProps/Water/Light Shaft";
        public const string WetSurfaceShaderName = "SabaProps/Water/Wet Surface";
        public const string WetSurfaceTransparentShaderName =
            "SabaProps/Water/Wet Surface Transparent";

        public const string RainMaterialName = "Rain";
        public const string SplashMaterialName = "Splash";
        public const string RippleMaterialName = "Ripple";
        public const string FogParticleMaterialName = "FogParticle";
        public const string CloudMaterialName = "Cloud";
        public const string FogVolumeLiteMaterialName = "FogVolume_Lite";
        public const string FogVolumeHighMaterialName = "FogVolume_High";
        public const string UnderwaterLiteMaterialName = "Underwater_Lite";
        public const string UnderwaterStandardMaterialName = "Underwater_Standard";
        public const string UnderwaterSurfaceLiteMaterialName = "UnderwaterSurface_Lite";
        public const string UnderwaterSurfaceStandardMaterialName = "UnderwaterSurface_Standard";
        public const string CausticsMaterialName = "Caustics";
        public const string LightShaftMaterialName = "LightShaft";
        public const string WetSurfaceMaterialName = "WetSurface";
        public const string WetSurfaceTransparentMaterialName = "WetSurface_Transparent";

        public static readonly WaterBodyKind[] AllBodyKinds =
        {
            WaterBodyKind.Puddle,
            WaterBodyKind.River,
            WaterBodyKind.Lake,
            WaterBodyKind.Ocean,
        };

        public static readonly WaterQuality[] AllQualities =
        {
            WaterQuality.Lite,
            WaterQuality.Standard,
        };

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

        public static List<UnityEngine.Object> CreateOrLoadDefaults()
        {
            var assets = new List<UnityEngine.Object>();
            foreach (WaterBodyKind bodyKind in AllBodyKinds)
            {
                foreach (WaterQuality quality in AllQualities)
                {
                    WaterSurfaceProfile profile = CreateOrLoadProfile(bodyKind, quality);
                    if (profile != null)
                    {
                        assets.Add(profile);
                        if (profile.material != null)
                        {
                            assets.Add(profile.material);
                        }
                    }
                }
            }

            foreach (string materialName in EnvironmentMaterialNames())
            {
                Material material = CreateOrLoadEnvironmentMaterial(materialName);
                if (material != null)
                {
                    assets.Add(material);
                }
            }

            Mesh rippleQuad = CreateOrLoadHorizontalQuad();
            if (rippleQuad != null)
            {
                assets.Add(rippleQuad);
            }

            Mesh lightShaft = CreateOrLoadLightShaft();
            if (lightShaft != null)
            {
                assets.Add(lightShaft);
            }

            AssetDatabase.SaveAssets();
            return assets;
        }

        public static WaterSurfaceProfile CreateOrLoadProfile(
            WaterBodyKind bodyKind,
            WaterQuality quality)
        {
            EnsureFolder(ProfilesFolder);
            EnsureFolder(MaterialsFolder);

            string assetName = bodyKind + "_" + quality;
            string profilePath = ProfilesFolder + "/" + assetName + ".asset";
            string materialPath = MaterialsFolder + "/Water_" + assetName + ".mat";

            WaterSurfaceProfile profile = AssetDatabase.LoadAssetAtPath<WaterSurfaceProfile>(profilePath);
            bool createdProfile = profile == null;
            if (createdProfile)
            {
                profile = ScriptableObject.CreateInstance<WaterSurfaceProfile>();
                profile.name = assetName;
                profile.bodyKind = bodyKind;
                profile.quality = quality;
                ApplyPreset(profile);
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            bool migratedProfile = MigrateProfileDefaults(profile);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            bool createdMaterial = material == null;
            if (createdMaterial)
            {
                string shaderName = quality == WaterQuality.Standard
                    ? WaterSurfaceProfile.StandardShaderName
                    : WaterSurfaceProfile.LiteShaderName;
                Shader shader = LoadShader(shaderName);
                if (shader == null)
                {
                    return profile;
                }

                material = new Material(shader)
                {
                    name = "Water_" + assetName,
                    enableInstancing = true,
                };
                AssetDatabase.CreateAsset(material, materialPath);
            }

            if (profile.material != material)
            {
                profile.material = material;
                EditorUtility.SetDirty(profile);
            }

            if (createdProfile || createdMaterial || migratedProfile || material.shader == null)
            {
                profile.ApplyToMaterial();
                EditorUtility.SetDirty(material);
            }

            return profile;
        }

        private static bool MigrateProfileDefaults(WaterSurfaceProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            var previousFoamColour = new Color(0.86f, 0.95f, 1f, 1f);
            if (Vector4.Distance(profile.foamColor, previousFoamColour) > 0.001f)
            {
                return false;
            }

            profile.foamColor = Color.Lerp(profile.shallowColor, previousFoamColour, 0.22f);
            EditorUtility.SetDirty(profile);
            return true;
        }

        public static Material CreateOrLoadEnvironmentMaterial(string materialName)
        {
            EnsureFolder(MaterialsFolder);
            string path = MaterialsFolder + "/" + materialName + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            string shaderName = ShaderForMaterial(materialName);
            Shader shader = LoadShader(shaderName);
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader)
            {
                name = materialName,
                enableInstancing = true,
            };
            ConfigureEnvironmentMaterial(materialName, material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static Mesh CreateOrLoadHorizontalQuad()
        {
            EnsureFolder(GeneratedSharedFolder);
            const string path = GeneratedSharedFolder + "/HorizontalQuad.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                return existing;
            }

            Mesh mesh = WaterMeshBuilder.BuildHorizontalQuad();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        public static Mesh CreateOrLoadLightShaft()
        {
            EnsureFolder(GeneratedSharedFolder);
            const string path = GeneratedSharedFolder + "/LightShaft.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                return existing;
            }

            Mesh mesh = WaterMeshBuilder.BuildLightShaft(5f, 0.25f, 2.4f);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        public static Mesh WriteUniqueMesh(Mesh mesh, string folder, string baseName)
        {
            if (mesh == null)
            {
                return null;
            }

            EnsureFolder(folder);
            string path = AssetDatabase.GenerateUniqueAssetPath(
                folder + "/" + SanitizeFileName(baseName) + ".asset");
            mesh.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        public static Mesh ReplaceOrWriteMesh(
            Mesh generated,
            Mesh existing,
            string folder,
            string baseName)
        {
            if (generated == null)
            {
                return null;
            }

            if (existing != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(existing)))
            {
                string previousName = existing.name;
                Undo.RegisterCompleteObjectUndo(existing, "Rebuild Water Path");
                EditorUtility.CopySerialized(generated, existing);
                UnityEngine.Object.DestroyImmediate(generated);
                existing.name = previousName;
                EditorUtility.SetDirty(existing);
                return existing;
            }

            return WriteUniqueMesh(generated, folder, baseName);
        }

        public static Shader LoadShader(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError(
                    $"[SabaProps Water] Shader '{shaderName}' が見つかりません。packageのimport状態を確認してください。");
            }

            return shader;
        }

        private static IEnumerable<string> EnvironmentMaterialNames()
        {
            yield return RainMaterialName;
            yield return SplashMaterialName;
            yield return RippleMaterialName;
            yield return FogParticleMaterialName;
            yield return CloudMaterialName;
            yield return FogVolumeLiteMaterialName;
            yield return FogVolumeHighMaterialName;
            yield return UnderwaterLiteMaterialName;
            yield return UnderwaterStandardMaterialName;
            yield return UnderwaterSurfaceLiteMaterialName;
            yield return UnderwaterSurfaceStandardMaterialName;
            yield return CausticsMaterialName;
            yield return LightShaftMaterialName;
            yield return WetSurfaceMaterialName;
            yield return WetSurfaceTransparentMaterialName;
        }

        private static string ShaderForMaterial(string materialName)
        {
            switch (materialName)
            {
                case RainMaterialName: return RainShaderName;
                case SplashMaterialName: return SplashShaderName;
                case RippleMaterialName: return RippleShaderName;
                case FogParticleMaterialName:
                case CloudMaterialName: return FogParticleShaderName;
                case FogVolumeLiteMaterialName:
                case FogVolumeHighMaterialName: return FogVolumeShaderName;
                case UnderwaterLiteMaterialName: return UnderwaterLiteShaderName;
                case UnderwaterStandardMaterialName: return UnderwaterStandardShaderName;
                case UnderwaterSurfaceLiteMaterialName: return UnderwaterSurfaceLiteShaderName;
                case UnderwaterSurfaceStandardMaterialName: return UnderwaterSurfaceStandardShaderName;
                case CausticsMaterialName: return CausticsShaderName;
                case LightShaftMaterialName: return LightShaftShaderName;
                case WetSurfaceMaterialName: return WetSurfaceShaderName;
                case WetSurfaceTransparentMaterialName: return WetSurfaceTransparentShaderName;
                default: throw new ArgumentOutOfRangeException(nameof(materialName), materialName, null);
            }
        }

        private static void ConfigureEnvironmentMaterial(string materialName, Material material)
        {
            switch (materialName)
            {
                case RainMaterialName:
                case SplashMaterialName:
                case RippleMaterialName:
                    material.SetFloat("_LightingResponse", 0.9f);
                    break;
                case FogParticleMaterialName:
                    material.SetColor("_Color", new Color(0.68f, 0.76f, 0.8f, 0.2f));
                    material.SetFloat("_NoiseScale", 0.35f);
                    material.SetFloat("_NoiseAmount", 0.55f);
                    break;
                case CloudMaterialName:
                    material.SetColor("_Color", new Color(0.9f, 0.93f, 0.95f, 0.22f));
                    material.SetFloat("_NoiseScale", 0.04f);
                    material.SetFloat("_NoiseAmount", 0.72f);
                    break;
                case FogVolumeLiteMaterialName:
                    material.SetColor("_Color", new Color(0.62f, 0.7f, 0.72f, 1f));
                    material.SetFloat("_Density", 0.16f);
                    material.DisableKeyword("_FOG_HIGH_QUALITY");
                    material.SetFloat("_HighQuality", 0f);
                    break;
                case FogVolumeHighMaterialName:
                    material.SetColor("_Color", new Color(0.62f, 0.7f, 0.72f, 1f));
                    material.SetFloat("_Density", 0.22f);
                    material.EnableKeyword("_FOG_HIGH_QUALITY");
                    material.SetFloat("_HighQuality", 1f);
                    break;
                case UnderwaterLiteMaterialName:
                    material.SetColor("_Tint", new Color(0.02f, 0.24f, 0.32f, 0.48f));
                    material.SetFloat("_CausticsStrength", 0.025f);
                    break;
                case UnderwaterStandardMaterialName:
                    material.SetColor("_Tint", new Color(0.015f, 0.2f, 0.3f, 1f));
                    material.SetFloat("_VolumeDistortionStrength", 0.0015f);
                    material.SetFloat("_CausticsStrength", 0.025f);
                    break;
                case UnderwaterSurfaceLiteMaterialName:
                    material.SetColor("_ShallowColor", new Color(0.19f, 0.64f, 0.76f, 1f));
                    material.SetFloat("_Opacity", 0.58f);
                    material.SetVector("_BoundaryUpDown", new Vector4(1f, 0f, 0f, 0f));
                    material.SetVector("_BoundaryCardinal", Vector4.zero);
                    material.SetVector("_BoundaryDiagonal", Vector4.zero);
                    material.SetFloat("_BoundaryEdgeFade", 0.09f);
                    break;
                case UnderwaterSurfaceStandardMaterialName:
                    material.SetColor("_Tint", new Color(0.12f, 0.48f, 0.58f, 1f));
                    material.SetFloat("_DistortionStrength", 0.016f);
                    material.SetVector("_BoundaryUpDown", new Vector4(1f, 0f, 0f, 0f));
                    material.SetVector("_BoundaryCardinal", Vector4.zero);
                    material.SetVector("_BoundaryDiagonal", Vector4.zero);
                    material.SetFloat("_BoundaryEdgeFade", 0.12f);
                    break;
                case WetSurfaceMaterialName:
                case WetSurfaceTransparentMaterialName:
                    material.SetColor("_Color", new Color(0.32f, 0.42f, 0.48f, 1f));
                    material.SetFloat("_Wetness", 0.8f);
                    material.SetFloat("_TrailPersistence", 0.72f);
                    material.SetFloat("_TrailSlide", 0.24f);
                    material.SetFloat("_DropletHeadNormalStrength", 0.34f);
                    material.SetFloat("_DropletTrailNormalStrength", 0.14f);
                    material.SetColor("_DropletScatterColor", new Color(0.68f, 0.88f, 0.96f, 1f));
                    if (materialName == WetSurfaceTransparentMaterialName)
                    {
                        material.SetFloat("_Opacity", 0.62f);
                    }
                    break;
            }
        }

        private static void ApplyPreset(WaterSurfaceProfile profile)
        {
            profile.shallowColor = new Color(0.16f, 0.48f, 0.55f, 1f);
            profile.deepColor = new Color(0.015f, 0.11f, 0.18f, 1f);
            profile.opacity = profile.quality == WaterQuality.Standard ? 0.78f : 0.66f;
            profile.smoothness = profile.quality == WaterQuality.Standard ? 0.9f : 0.78f;
            profile.lightingResponse = profile.quality == WaterQuality.Standard ? 0.92f : 0.82f;
            profile.waveScale = 1.8f;
            profile.waveStrength = 0.1f;
            profile.waveSpeed = 0.3f;
            profile.flowDirection = new Vector2(1f, 0.2f);
            profile.vertexWaveHeight = 0f;
            profile.tideHeight = 0f;
            profile.tideSpeed = 0.04f;
            profile.edgeFade = 0f;
            profile.rippleStrength = 0f;
            profile.rippleDensity = 1.5f;
            profile.rippleSpeed = 0.8f;
            profile.shallowEdgeWidth = 0f;
            profile.foamColor = new Color(0.28f, 0.58f, 0.63f, 1f);
            profile.foamStrength = 0f;
            profile.crestFoamThreshold = 0.8f;
            profile.crestFoamWidth = 0.08f;
            profile.foamTrailStrength = 0.2f;
            profile.foamDetail = 0.6f;
            profile.foamPatternScale = 1f;
            profile.foamPatternSpeed = 1f;
            profile.foamPatternWarp = 0.45f;
            profile.shoreFoamWidth = 0f;
            profile.flowTurbulence = 0f;
            profile.flowFoamStrength = 0f;
            profile.reflectionStrength = profile.quality == WaterQuality.Standard ? 0.9f : 0.65f;
            profile.reflectionDistortion = profile.quality == WaterQuality.Standard ? 0.3f : 0.18f;
            profile.reflectionBlur = profile.quality == WaterQuality.Standard ? 0.08f : 0.18f;
            profile.rippleReflectionBlur = profile.quality == WaterQuality.Standard ? 0.55f : 0.35f;
            profile.refractionStrength = 0.018f;
            profile.depthDistance = 3f;

            switch (profile.bodyKind)
            {
                case WaterBodyKind.Puddle:
                    profile.shallowColor = new Color(0.24f, 0.34f, 0.36f, 1f);
                    profile.deepColor = new Color(0.08f, 0.12f, 0.13f, 1f);
                    profile.opacity = profile.quality == WaterQuality.Standard ? 0.58f : 0.48f;
                    profile.waveScale = 5f;
                    profile.waveStrength = 0.025f;
                    profile.waveSpeed = 0.12f;
                    profile.edgeFade = 0.22f;
                    profile.rippleStrength = 0.9f;
                    profile.rippleDensity = 1.35f;
                    profile.shallowEdgeWidth = 0.24f;
                    profile.foamStrength = 0f;
                    profile.reflectionStrength = profile.quality == WaterQuality.Standard ? 1.2f : 0.78f;
                    profile.reflectionDistortion = profile.quality == WaterQuality.Standard ? 0.62f : 0.32f;
                    profile.reflectionBlur = profile.quality == WaterQuality.Standard ? 0.035f : 0.14f;
                    profile.rippleReflectionBlur = profile.quality == WaterQuality.Standard ? 0.82f : 0.48f;
                    profile.depthDistance = 0.25f;
                    break;
                case WaterBodyKind.River:
                    profile.waveScale = 2.8f;
                    profile.waveStrength = 0.09f;
                    profile.waveSpeed = 0.85f;
                    profile.flowDirection = Vector2.up;
                    profile.vertexWaveHeight = profile.quality == WaterQuality.Standard ? 0.025f : 0f;
                    profile.rippleStrength = 0.12f;
                    profile.foamStrength = 0f;
                    profile.crestFoamThreshold = 0.68f;
                    profile.crestFoamWidth = 0.11f;
                    profile.foamTrailStrength = profile.quality == WaterQuality.Standard ? 0.72f : 0.42f;
                    profile.foamDetail = 0.9f;
                    profile.shoreFoamWidth = 0.12f;
                    profile.flowTurbulence = profile.quality == WaterQuality.Standard ? 0.9f : 0.58f;
                    profile.flowFoamStrength = 0f;
                    profile.reflectionBlur = profile.quality == WaterQuality.Standard ? 0.18f : 0.28f;
                    profile.depthDistance = 1.5f;
                    break;
                case WaterBodyKind.Ocean:
                    profile.shallowColor = new Color(0.08f, 0.36f, 0.46f, 1f);
                    profile.deepColor = new Color(0.005f, 0.045f, 0.11f, 1f);
                    profile.waveScale = 0.72f;
                    profile.waveStrength = profile.quality == WaterQuality.Standard ? 0.24f : 0.14f;
                    profile.waveSpeed = 0.42f;
                    profile.vertexWaveHeight = profile.quality == WaterQuality.Standard ? 0.18f : 0.05f;
                    profile.tideHeight = profile.quality == WaterQuality.Standard ? 0.11f : 0.04f;
                    profile.tideSpeed = 0.045f;
                    profile.shallowEdgeWidth = 0.3f;
                    profile.foamStrength = 0f;
                    profile.crestFoamThreshold = 0.68f;
                    profile.crestFoamWidth = profile.quality == WaterQuality.Standard ? 0.085f : 0.12f;
                    profile.foamTrailStrength = profile.quality == WaterQuality.Standard ? 0.66f : 0.3f;
                    profile.foamDetail = profile.quality == WaterQuality.Standard ? 0.92f : 0.62f;
                    profile.shoreFoamWidth = 0.16f;
                    profile.reflectionStrength = profile.quality == WaterQuality.Standard ? 1.05f : 0.76f;
                    profile.reflectionBlur = profile.quality == WaterQuality.Standard ? 0.075f : 0.16f;
                    profile.depthDistance = 8f;
                    break;
                case WaterBodyKind.Lake:
                default:
                    profile.waveScale = 1.4f;
                    profile.waveStrength = 0.08f;
                    profile.waveSpeed = 0.22f;
                    profile.vertexWaveHeight = profile.quality == WaterQuality.Standard ? 0.045f : 0.01f;
                    profile.tideHeight = profile.quality == WaterQuality.Standard ? 0.025f : 0.008f;
                    profile.tideSpeed = 0.025f;
                    profile.rippleStrength = 0.32f;
                    profile.shallowEdgeWidth = 0.08f;
                    profile.foamStrength = 0f;
                    profile.crestFoamThreshold = 0.76f;
                    profile.crestFoamWidth = 0.09f;
                    profile.foamTrailStrength = profile.quality == WaterQuality.Standard ? 0.28f : 0.12f;
                    profile.foamDetail = 0.72f;
                    profile.depthDistance = 4f;
                    break;
            }

            // Foam is initially disabled. When enabled, start from a lightly
            // aerated version of this body's surface colour instead of an
            // unrelated white stripe; users can still tint it independently.
            profile.foamColor = Color.Lerp(
                profile.shallowColor,
                new Color(0.86f, 0.95f, 1f, 1f),
                0.22f);

            profile.Normalize();
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return string.IsNullOrWhiteSpace(value) ? "WaterMesh" : value;
        }
    }
}
