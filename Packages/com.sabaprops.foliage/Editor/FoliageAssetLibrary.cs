using System.IO;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Foliage.Editors
{
    /// <summary>
    /// Owns everything this package writes into the user's project. Generated
    /// content never lands inside the package folder itself, because VCC replaces
    /// that directory wholesale on every upgrade.
    /// </summary>
    public static class FoliageAssetLibrary
    {
        public const string RootFolder = "Assets/SabaProps/Foliage";
        public const string MaterialsFolder = RootFolder + "/Materials";
        public const string SpeciesFolder = RootFolder + "/Species";
        public const string GeneratedFolder = RootFolder + "/Generated";
        public const string GeneratedMeshFolder = GeneratedFolder + "/Species";
        public const string GeneratedMergedFolder = GeneratedFolder + "/Merged";

        public const string ShaderName = "SabaProps/Foliage";

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

        public static Shader LoadShader()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[SabaProps Foliage] シェーダー '{ShaderName}' が見つかりません。パッケージが正しくインポートされているか確認してください。");
            }

            return shader;
        }

        /// <summary>
        /// Rebuilds a species' mesh and writes it to disk, reusing the existing
        /// asset so its GUID — and therefore every scene reference to it —
        /// survives the rebuild.
        /// </summary>
        public static Mesh WriteSpeciesMesh(FoliageSpecies species)
        {
            if (species == null)
            {
                return null;
            }

            Mesh generated = FoliageMeshBuilder.Build(species);
            if (generated == null)
            {
                return null;
            }

            EnsureFolder(GeneratedMeshFolder);
            string path = $"{GeneratedMeshFolder}/{SanitizeFileName(species.name)}_{StableSuffix(species)}.asset";

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                // CopySerialized keeps the asset instance (and its GUID + fileID)
                // while swapping in the new geometry.
                EditorUtility.CopySerialized(generated, existing);
                Object.DestroyImmediate(generated);

                // CopySerialized also copies the object name, which would then
                // disagree with the file it lives in.
                existing.name = Path.GetFileNameWithoutExtension(path);
                EditorUtility.SetDirty(existing);

                species.generatedMesh = existing;
                EditorUtility.SetDirty(species);
                return existing;
            }

            generated.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(generated, path);

            species.generatedMesh = generated;
            EditorUtility.SetDirty(species);
            return generated;
        }

        /// <summary>Folder that holds the merged chunk meshes for one field.</summary>
        public static string MergedFolderFor(FoliageField field)
        {
            return $"{GeneratedMergedFolder}/{SanitizeFileName(field.name)}_{field.BuildId}";
        }

        public static void DeleteMergedFolder(FoliageField field)
        {
            string folder = MergedFolderFor(field);
            if (AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        /// <summary>
        /// Creates the shared foliage material, or returns the existing one.
        /// GPU instancing is enabled on it — without that flag Unity silently
        /// falls back to one draw call per renderer.
        /// </summary>
        public static Material CreateOrLoadDefaultMaterial()
        {
            EnsureFolder(MaterialsFolder);
            string path = MaterialsFolder + "/SabaProps_Foliage.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                if (!existing.enableInstancing)
                {
                    existing.enableInstancing = true;
                    EditorUtility.SetDirty(existing);
                }

                return existing;
            }

            Shader shader = LoadShader();
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader)
            {
                name = "SabaProps_Foliage",
                enableInstancing = true,
            };

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// Creates, or loads, the shared material and both stock species along
        /// with their meshes. Returns false when the shader is missing, which is
        /// the only failure a caller can do anything about.
        /// </summary>
        public static bool CreateOrLoadDefaults(
            out Material material, out FoliageSpecies grass, out FoliageSpecies sunflower)
        {
            material = CreateOrLoadDefaultMaterial();
            grass = null;
            sunflower = null;

            if (material == null)
            {
                return false;
            }

            grass = CreateOrLoadDefaultSpecies(FoliageSpeciesKind.GrassClump, material);
            sunflower = CreateOrLoadDefaultSpecies(FoliageSpeciesKind.Sunflower, material);

            WriteSpeciesMesh(grass);
            WriteSpeciesMesh(sunflower);
            return true;
        }

        /// <summary>Creates the two built-in species presets if they are missing.</summary>
        public static FoliageSpecies CreateOrLoadDefaultSpecies(FoliageSpeciesKind kind, Material material)
        {
            EnsureFolder(SpeciesFolder);

            string assetName = kind == FoliageSpeciesKind.Sunflower ? "Sunflower" : "GrassSeed";
            string path = $"{SpeciesFolder}/{assetName}.asset";

            var existing = AssetDatabase.LoadAssetAtPath<FoliageSpecies>(path);
            if (existing != null)
            {
                if (existing.material == null)
                {
                    existing.material = material;
                    EditorUtility.SetDirty(existing);
                }

                return existing;
            }

            var species = ScriptableObject.CreateInstance<FoliageSpecies>();
            species.name = assetName;
            species.kind = kind;
            species.material = material;

            ApplyPreset(species, kind);

            AssetDatabase.CreateAsset(species, path);
            return species;
        }

        private static void ApplyPreset(FoliageSpecies species, FoliageSpeciesKind kind)
        {
            if (kind == FoliageSpeciesKind.Sunflower)
            {
                species.meshSeed = 7;
                species.placementWeight = 0.06f;
                species.minSpacing = 0.45f;
                species.scaleRange = new Vector2(0.8f, 1.25f);
                species.maxTilt = 6f;
                species.alignToGroundNormal = 0.15f;
                species.slopeLimits = new Vector2(0f, 25f);

                // Sunflowers are sparse and tall enough to be worth a shadow.
                species.castShadows = true;
                return;
            }

            species.meshSeed = 1;
            species.placementWeight = 1f;
            species.minSpacing = 0.06f;
            species.scaleRange = new Vector2(0.8f, 1.3f);
            species.maxTilt = 9f;
            species.alignToGroundNormal = 0.35f;
            species.slopeLimits = new Vector2(0f, 45f);

            // Thousands of grass shadow casters is the single easiest way to
            // tank a world's frame rate, so this stays off by default.
            species.castShadows = false;
        }

        /// <summary>Short, stable suffix derived from the asset GUID.</summary>
        private static string StableSuffix(Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
            {
                return "mem";
            }

            string guid = AssetDatabase.AssetPathToGUID(path);
            return string.IsNullOrEmpty(guid) ? "mem" : guid.Substring(0, 8);
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "Unnamed";
            }

            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value.Replace(' ', '_');
        }
    }
}
