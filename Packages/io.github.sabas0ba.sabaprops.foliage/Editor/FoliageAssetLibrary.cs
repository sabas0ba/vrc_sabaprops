using System.Collections.Generic;
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

        /// <summary>
        /// Absolute path to a file inside this package, or null if the package
        /// cannot be located.
        /// <para>
        /// Resolved from the assembly rather than hard-coded, so it holds
        /// whether the package is embedded under Packages/ or installed by VCC
        /// into the project's package cache.
        /// </para>
        /// </summary>
        public static string PackagePath(string relativePath)
        {
            UnityEditor.PackageManager.PackageInfo info =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(FoliageAssetLibrary).Assembly);

            if (info == null || string.IsNullOrEmpty(info.resolvedPath))
            {
                return null;
            }

            return Path.Combine(info.resolvedPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

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

        /// <summary>Every species kind the package can generate.</summary>
        public static readonly FoliageSpeciesKind[] AllKinds =
        {
            FoliageSpeciesKind.GrassClump,
            FoliageSpeciesKind.Clover,
            FoliageSpeciesKind.Sunflower,
            FoliageSpeciesKind.Reed,
        };

        /// <summary>Asset file name, and inspector label, for a species kind.</summary>
        public static string DisplayName(FoliageSpeciesKind kind)
        {
            switch (kind)
            {
                case FoliageSpeciesKind.Sunflower: return "Sunflower";
                case FoliageSpeciesKind.Clover: return "Clover";
                case FoliageSpeciesKind.Reed: return "Reed";
                case FoliageSpeciesKind.GrassClump:
                default: return "GrassSeed";
            }
        }

        /// <summary>Every season, in calendar order.</summary>
        public static readonly FoliageSeason[] AllSeasons =
        {
            FoliageSeason.Spring,
            FoliageSeason.Summer,
            FoliageSeason.Autumn,
            FoliageSeason.WinterSnow,
            FoliageSeason.WinterBare,
        };

        /// <summary>
        /// Asset file name for a seasonal variant of a species.
        /// <para>
        /// Summer carries no suffix. It is the season a species is authored in,
        /// and leaving its name alone keeps every asset and scene reference from
        /// before seasons existed pointing at the same file.
        /// </para>
        /// </summary>
        public static string DisplayName(FoliageSpeciesKind kind, FoliageSeason season)
        {
            string baseName = DisplayName(kind);
            return season == FoliageSeason.Summer ? baseName : baseName + "_" + season;
        }

        /// <summary>Name of the serialised tint a season reads from.</summary>
        public static string SeasonProperty(FoliageSeason season)
        {
            switch (season)
            {
                case FoliageSeason.Spring: return "spring";
                case FoliageSeason.Autumn: return "autumn";
                case FoliageSeason.WinterSnow: return "winterSnow";
                case FoliageSeason.WinterBare: return "winterBare";
                case FoliageSeason.Summer:
                default: return "summer";
            }
        }

        /// <summary>Name of the serialised parameter block a kind reads from.</summary>
        public static string ParameterProperty(FoliageSpeciesKind kind)
        {
            switch (kind)
            {
                case FoliageSpeciesKind.Sunflower: return "sunflower";
                case FoliageSpeciesKind.Clover: return "clover";
                case FoliageSpeciesKind.Reed: return "reed";
                case FoliageSpeciesKind.GrassClump:
                default: return "grass";
            }
        }

        /// <summary>
        /// Creates, or loads, the shared material and the stock species for the
        /// requested kinds, along with their meshes. Returns null when the shader
        /// is missing, which is the only failure a caller can do anything about.
        /// </summary>
        public static List<FoliageSpecies> CreateOrLoadDefaults(
            out Material material, params FoliageSpeciesKind[] kinds)
        {
            material = CreateOrLoadDefaultMaterial();
            if (material == null)
            {
                return null;
            }

            if (kinds == null || kinds.Length == 0)
            {
                kinds = AllKinds;
            }

            var species = new List<FoliageSpecies>(kinds.Length);
            foreach (FoliageSpeciesKind kind in kinds)
            {
                FoliageSpecies entry = CreateOrLoadDefaultSpecies(kind, material);
                if (entry == null)
                {
                    continue;
                }

                WriteSpeciesMesh(entry);
                species.Add(entry);
            }

            return species;
        }

        /// <summary>Creates a built-in species preset if it is missing.</summary>
        public static FoliageSpecies CreateOrLoadDefaultSpecies(
            FoliageSpeciesKind kind, Material material, FoliageSeason season = FoliageSeason.Summer)
        {
            EnsureFolder(SpeciesFolder);

            string assetName = DisplayName(kind, season);
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
            species.season = season;

            ApplyPreset(species, kind);

            AssetDatabase.CreateAsset(species, path);
            return species;
        }

        private static void ApplyPreset(FoliageSpecies species, FoliageSpeciesKind kind)
        {
            if (kind == FoliageSpeciesKind.Clover)
            {
                species.meshSeed = 21;

                // Ground cover: dense, low and tolerant of slopes, so it fills
                // the gaps grass leaves rather than competing with it.
                species.placementWeight = 0.5f;
                species.minSpacing = 0.05f;
                species.scaleRange = new Vector2(0.85f, 1.25f);
                species.maxTilt = 12f;
                species.alignToGroundNormal = 0.6f;
                species.slopeLimits = new Vector2(0f, 40f);
                species.castShadows = false;
                return;
            }

            if (kind == FoliageSpeciesKind.Reed)
            {
                species.meshSeed = 13;

                // Reeds grow in stands, not as isolated stalks, so they are
                // packed far tighter than their height would suggest. They still
                // want flat ground: reeds out of a hillside read as a mistake.
                species.placementWeight = 0.35f;
                species.minSpacing = 0.11f;
                species.scaleRange = new Vector2(0.75f, 1.3f);
                species.maxTilt = 4f;
                species.alignToGroundNormal = 0.1f;
                species.slopeLimits = new Vector2(0f, 18f);
                species.castShadows = true;
                return;
            }

            if (kind == FoliageSpeciesKind.Sunflower)
            {
                species.meshSeed = 7;
                species.placementWeight = 0.06f;
                species.minSpacing = 0.45f;
                species.scaleRange = new Vector2(0.8f, 1.25f);
                species.maxTilt = 6f;
                species.alignToGroundNormal = 0.15f;
                species.slopeLimits = new Vector2(0f, 25f);

                // Heliotropism: a field of sunflowers all faces the same way.
                species.faceSun = true;
                species.faceSunJitter = 16f;

                // A sunflower is an annual. In autumn it has shed its petals and
                // stands as a seed head on a drying stalk; by winter it is gone
                // altogether. Recolouring a flower in full bloom to straw would
                // produce something that does not exist.
                species.seasonPalette.autumn.appearance = SeasonAppearance.Dormant;
                species.seasonPalette.winterSnow.appearance = SeasonAppearance.Absent;
                species.seasonPalette.winterBare.appearance = SeasonAppearance.Absent;

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
