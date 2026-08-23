using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SabaProps.Foliage.Editors
{
    /// <summary>
    /// Generates a demo scene that is already built and already looks like
    /// something.
    /// <para>
    /// Laid out as a garden of plots rather than one big field: each plot
    /// changes exactly one thing from its neighbour, so the effect of a species,
    /// a parameter, the ground underneath or the output mode can be read by
    /// walking from one to the next.
    /// </para>
    /// <para>
    /// The scene is generated rather than shipped inside the package for two
    /// reasons: a built field is thousands of GameObjects or a pile of merged
    /// mesh binaries, neither of which belongs in a texture-free package, and
    /// VCC replaces the package folder wholesale on upgrade, which would take
    /// any edits the user made with it.
    /// </para>
    /// </summary>
    public static class FoliageSampleScene
    {
        public const string SampleFolder = FoliageAssetLibrary.RootFolder + "/Samples";
        public const string ScenePath = SampleFolder + "/FoliageDemo.unity";
        public const string GroundMaterialPath = SampleFolder + "/DemoGround.mat";
        public const string VariantFolder = SampleFolder + "/Species";

        // Section roots. Numbered so the hierarchy reads in walking order, and
        // named as constants because the tests navigate by them.
        public const string SingleSpeciesRoot = "1 Single Species";
        public const string VariantRoot = "2 Parameter Variants";
        public const string TerrainRoot = "3 Terrain";
        public const string MixRoot = "4 Combinations";
        public const string OutputRoot = "5 Output Modes";
        public const string GroundRoot = "Ground";

        public const string InstancedPlotName = "GPU Instanced";
        public const string MergedPlotName = "Merged Chunks";

        /// <summary>Player spawn, on the flat ground and facing up the garden.</summary>
        public static readonly Vector3 SpawnPosition = new Vector3(0f, 0.05f, -7f);

        /// <summary>Plot side length, and the spacing between plot centres.</summary>
        private const float PlotSize = 7f;
        private const float Pitch = 9f;
        private const float PlotDensity = 10f;

        /// <summary>Column centres, four across.</summary>
        private static readonly float[] Columns = { -13.5f, -4.5f, 4.5f, 13.5f };

        [MenuItem("Tools/SabaProps/Foliage/Create Sample Scene", false, 1)]
        public static void CreateAndOpen()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = Create();
            if (!scene.IsValid())
            {
                return;
            }

            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                view.LookAt(new Vector3(0f, 0.5f, 16f), Quaternion.Euler(38f, 0f, 0f), 44f);
            }
        }

        /// <summary>
        /// Replaces the open scene with the demo, builds every plot and saves the
        /// result to <see cref="ScenePath"/>. Prompt free, so tests and batch
        /// mode can call it directly.
        /// </summary>
        public static Scene Create()
        {
            List<FoliageSpecies> stock = FoliageAssetLibrary.CreateOrLoadDefaults(out Material material);
            if (material == null || stock == null || stock.Count == 0)
            {
                return default;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            ConfigureLight();
            ConfigureCamera();

            Material ground = CreateOrLoadGroundMaterial();
            BuildGround(ground);

            var fields = new List<FoliageField>();

            BuildSingleSpecies(stock, fields);
            BuildVariants(material, fields);
            BuildTerrain(stock, fields);
            BuildMixes(stock, fields);
            BuildOutputModes(stock, fields);

            var stats = new List<FoliageBuildStats>(fields.Count);
            foreach (FoliageField field in fields)
            {
                stats.Add(FoliageFieldBuilder.Build(field));
            }

            GameObject world = FoliageVrcWorld.TryCreateWorld(
                SpawnPosition, Quaternion.identity, Camera.main);

            FoliageAssetLibrary.EnsureFolder(SampleFolder);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log(Summarise(fields, stats, world != null));
            return scene;
        }

        // ------------------------------------------------------------------
        // Sections
        // ------------------------------------------------------------------

        /// <summary>
        /// One plot per species, same size, density and seed. The only variable
        /// is the species, which is what makes them comparable.
        /// </summary>
        private static void BuildSingleSpecies(List<FoliageSpecies> stock, List<FoliageField> fields)
        {
            Transform root = CreateRoot(SingleSpeciesRoot);

            for (int i = 0; i < FoliageAssetLibrary.AllKinds.Length && i < Columns.Length; i++)
            {
                FoliageSpeciesKind kind = FoliageAssetLibrary.AllKinds[i];
                FoliageSpecies species = Of(stock, kind);
                if (species == null)
                {
                    continue;
                }

                FoliageField field = CreatePlot(
                    root, FoliageAssetLibrary.DisplayName(kind),
                    new Vector3(Columns[i], 0f, 0f),
                    DensityFor(kind), 3001 + i, FoliageOutputMode.MergedChunks);

                AddSpecies(field, species, 1f);
                fields.Add(field);
            }
        }

        /// <summary>
        /// The same four species with one parameter block pushed somewhere else.
        /// Written to their own assets under the sample folder so the stock
        /// presets keep whatever the user has tuned them to.
        /// </summary>
        private static void BuildVariants(Material material, List<FoliageField> fields)
        {
            Transform root = CreateRoot(VariantRoot);
            FoliageAssetLibrary.EnsureFolder(VariantFolder);

            FoliageSpecies tallGrass = CreateVariant("GrassSeed_Tall", FoliageSpeciesKind.GrassClump, material,
                species =>
                {
                    species.grass.height = 1.05f;
                    species.grass.bladeCount = 5;
                    species.grass.width = 0.03f;
                    species.grass.bend = 0.75f;
                    species.grass.clumpRadius = 0.12f;
                    species.minSpacing = 0.12f;
                });

            FoliageSpecies wideClover = CreateVariant("Clover_Broad", FoliageSpeciesKind.Clover, material,
                species =>
                {
                    species.clover.leafletCount = 4;
                    species.clover.height = 0.19f;
                    species.clover.leafLength = 0.085f;
                    species.clover.leafWidth = 0.095f;
                    species.clover.notch = 0.34f;
                });

            FoliageSpecies dwarfSunflower = CreateVariant("Sunflower_Dwarf", FoliageSpeciesKind.Sunflower, material,
                species =>
                {
                    species.sunflower.height = 0.55f;
                    species.sunflower.headRadius = 0.11f;
                    species.sunflower.petalCount = 22;
                    species.sunflower.petalLength = 0.09f;
                    species.sunflower.headTilt = 18f;
                    species.minSpacing = 0.25f;
                });

            FoliageSpecies bareReed = CreateVariant("Reed_Splayed", FoliageSpeciesKind.Reed, material,
                species =>
                {
                    species.reed.bladeCount = 6;
                    species.reed.height = 0.7f;
                    species.reed.spread = 0.45f;
                    species.reed.spike = false;
                    species.reed.clumpRadius = 0.07f;
                    species.minSpacing = 0.18f;
                });

            AddVariantPlot(root, fields, "Grass - Tall", 0, tallGrass, 6f);
            AddVariantPlot(root, fields, "Clover - Broad", 1, wideClover, 9f);
            AddVariantPlot(root, fields, "Sunflower - Dwarf", 2, dwarfSunflower, 3.5f);
            AddVariantPlot(root, fields, "Reed - Splayed", 3, bareReed, 5f);
        }

        private static void AddVariantPlot(
            Transform root, List<FoliageField> fields,
            string name, int column, FoliageSpecies species, float density)
        {
            if (species == null)
            {
                return;
            }

            FoliageField field = CreatePlot(
                root, name, new Vector3(Columns[column], 0f, Pitch),
                density, 3101 + column, FoliageOutputMode.MergedChunks);

            AddSpecies(field, species, 1f);
            fields.Add(field);
        }

        /// <summary>
        /// The same mix over three kinds of ground. What changes between these
        /// plots is underneath them, not in the field settings.
        /// </summary>
        private static void BuildTerrain(List<FoliageSpecies> stock, List<FoliageField> fields)
        {
            Transform root = CreateRoot(TerrainRoot);

            FoliageSpecies grass = Of(stock, FoliageSpeciesKind.GrassClump);
            FoliageSpecies clover = Of(stock, FoliageSpeciesKind.Clover);
            FoliageSpecies sunflower = Of(stock, FoliageSpeciesKind.Sunflower);

            string[] names = { "Mound", "Ramp", "Terrace" };

            for (int i = 0; i < names.Length; i++)
            {
                FoliageField field = CreatePlot(
                    root, names[i], new Vector3(Columns[i], 0f, Pitch * 2f),
                    PlotDensity, 3201 + i, FoliageOutputMode.MergedChunks);

                // The ramp is steeper than the sunflower's slope limit, so the
                // same mix comes out grass and clover only. That difference is
                // the point of the plot.
                AddSpecies(field, grass, 1f);
                AddSpecies(field, clover, 0.5f);
                AddSpecies(field, sunflower, 0.1f);

                fields.Add(field);
            }
        }

        /// <summary>Three mixes of the same stock species.</summary>
        private static void BuildMixes(List<FoliageSpecies> stock, List<FoliageField> fields)
        {
            Transform root = CreateRoot(MixRoot);

            FoliageSpecies grass = Of(stock, FoliageSpeciesKind.GrassClump);
            FoliageSpecies clover = Of(stock, FoliageSpeciesKind.Clover);
            FoliageSpecies sunflower = Of(stock, FoliageSpeciesKind.Sunflower);
            FoliageSpecies reed = Of(stock, FoliageSpeciesKind.Reed);

            FoliageField meadow = CreatePlot(
                root, "Meadow", new Vector3(Columns[0], 0f, Pitch * 3f),
                PlotDensity, 3301, FoliageOutputMode.MergedChunks);
            AddSpecies(meadow, grass, 1f);
            AddSpecies(meadow, clover, 0.45f);
            AddSpecies(meadow, sunflower, 0.06f);
            fields.Add(meadow);

            FoliageField waterside = CreatePlot(
                root, "Waterside", new Vector3(Columns[1], 0f, Pitch * 3f),
                PlotDensity, 3302, FoliageOutputMode.MergedChunks);
            AddSpecies(waterside, grass, 1f);
            AddSpecies(waterside, reed, 0.22f);
            fields.Add(waterside);

            FoliageField flowerbed = CreatePlot(
                root, "Flowerbed", new Vector3(Columns[2], 0f, Pitch * 3f),
                PlotDensity, 3303, FoliageOutputMode.MergedChunks);
            AddSpecies(flowerbed, clover, 1f);
            AddSpecies(flowerbed, sunflower, 0.3f);
            fields.Add(flowerbed);
        }

        /// <summary>
        /// The same field twice, differing only in output mode, so the renderer
        /// and draw call counts in the two inspectors can be read side by side.
        /// </summary>
        private static void BuildOutputModes(List<FoliageSpecies> stock, List<FoliageField> fields)
        {
            Transform root = CreateRoot(OutputRoot);

            FoliageSpecies grass = Of(stock, FoliageSpeciesKind.GrassClump);
            FoliageSpecies clover = Of(stock, FoliageSpeciesKind.Clover);

            var modes = new[] { FoliageOutputMode.GpuInstanced, FoliageOutputMode.MergedChunks };
            var names = new[] { InstancedPlotName, MergedPlotName };

            for (int i = 0; i < modes.Length; i++)
            {
                FoliageField field = CreatePlot(
                    root, names[i], new Vector3(Columns[i], 0f, Pitch * 4f),
                    PlotDensity, 3401, modes[i]);

                AddSpecies(field, grass, 1f);
                AddSpecies(field, clover, 0.4f);
                fields.Add(field);
            }
        }

        // ------------------------------------------------------------------
        // Plot and species helpers
        // ------------------------------------------------------------------

        private static float DensityFor(FoliageSpeciesKind kind)
        {
            switch (kind)
            {
                case FoliageSpeciesKind.Sunflower: return 2.5f;
                case FoliageSpeciesKind.Reed: return 4f;
                case FoliageSpeciesKind.Clover: return 14f;
                case FoliageSpeciesKind.GrassClump:
                default: return PlotDensity;
            }
        }

        private static Transform CreateRoot(string name)
        {
            return new GameObject(name).transform;
        }

        private static FoliageField CreatePlot(
            Transform parent, string name, Vector3 position,
            float density, int seed, FoliageOutputMode mode)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var field = go.AddComponent<FoliageField>();
            field.shape = FoliageAreaShape.Rectangle;
            field.size = new Vector2(PlotSize, PlotSize);
            field.density = density;
            field.seed = seed;
            field.chunkSize = 4f;
            field.outputMode = mode;

            return field;
        }

        /// <summary>The stock species of a given kind, or null if it is missing.</summary>
        private static FoliageSpecies Of(List<FoliageSpecies> species, FoliageSpeciesKind kind)
        {
            return species.Find(entry => entry != null && entry.kind == kind);
        }

        private static void AddSpecies(FoliageField field, FoliageSpecies species, float weight)
        {
            if (species == null)
            {
                return;
            }

            field.species.Add(species);
            field.speciesWeights.Add(weight);
        }

        /// <summary>
        /// A species asset that starts from the stock preset for its kind and
        /// then has one thing changed. Lives under the sample folder so the
        /// user's own presets are left alone.
        /// </summary>
        private static FoliageSpecies CreateVariant(
            string assetName, FoliageSpeciesKind kind, Material material, Action<FoliageSpecies> configure)
        {
            string path = $"{VariantFolder}/{assetName}.asset";

            var species = AssetDatabase.LoadAssetAtPath<FoliageSpecies>(path);
            bool created = species == null;

            if (created)
            {
                species = ScriptableObject.CreateInstance<FoliageSpecies>();
                species.name = assetName;
            }

            species.kind = kind;
            species.material = material;
            configure(species);

            if (created)
            {
                AssetDatabase.CreateAsset(species, path);
            }
            else
            {
                EditorUtility.SetDirty(species);
            }

            FoliageAssetLibrary.WriteSpeciesMesh(species);
            return species;
        }

        // ------------------------------------------------------------------
        // Scene furniture
        // ------------------------------------------------------------------

        private static void ConfigureLight()
        {
            Light light = UnityEngine.Object.FindObjectOfType<Light>();
            if (light == null)
            {
                var go = new GameObject("Directional Light");
                light = go.AddComponent<Light>();
                light.type = LightType.Directional;
            }

            light.transform.rotation = Quaternion.Euler(46f, -40f, 0f);
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
        }

        private static void ConfigureCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            camera.transform.SetPositionAndRotation(
                new Vector3(0f, 9f, -17f), Quaternion.Euler(16f, 0f, 0f));
            camera.farClipPlane = 300f;
        }

        private static Material CreateOrLoadGroundMaterial()
        {
            FoliageAssetLibrary.EnsureFolder(SampleFolder);

            var existing = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader)
            {
                name = "DemoGround",
                color = new Color(0.196f, 0.235f, 0.149f),
            };
            material.SetFloat("_Glossiness", 0.05f);

            AssetDatabase.CreateAsset(material, GroundMaterialPath);
            return material;
        }

        private static void BuildGround(Material material)
        {
            var root = new GameObject(GroundRoot);

            // Covers every plot with room to walk between them.
            CreateGroundPiece(root.transform, PrimitiveType.Plane, "Flat",
                new Vector3(0f, 0f, 16f), Quaternion.identity, new Vector3(4.2f, 1f, 5.6f), material);

            // --- section 3's ground -----------------------------------------
            float z = Pitch * 2f;

            // A gentle ellipsoid: shows ground snapping and normal alignment.
            CreateGroundPiece(root.transform, PrimitiveType.Sphere, "Mound",
                new Vector3(Columns[0], -2.4f, z), Quaternion.identity, new Vector3(10f, 6f, 10f), material);

            // 28 degrees is past the sunflower's slope limit and inside the grass
            // and clover ones, so the slope filter demonstrates itself.
            //
            // Deep enough to carry the whole plot: 7 m of plot laid on a 28
            // degree slope needs 7 / cos(28) ≈ 7.9 m of ramp, and anything the
            // ramp does not cover falls through to the flat plane, where the
            // filter has nothing to do.
            CreateGroundPiece(root.transform, PrimitiveType.Cube, "Ramp",
                new Vector3(Columns[1], 2.3f, z), Quaternion.Euler(-28f, 0f, 0f), new Vector3(8f, 0.4f, 8.6f), material);

            for (int step = 0; step < 3; step++)
            {
                CreateGroundPiece(root.transform, PrimitiveType.Cube, $"Terrace_{step}",
                    new Vector3(Columns[2], 0.15f + step * 0.3f, z - 2.4f + step * 2.4f),
                    Quaternion.identity, new Vector3(7f, 0.3f + step * 0.6f, 2.4f), material);
            }
        }

        private static void CreateGroundPiece(
            Transform parent, PrimitiveType type, string name,
            Vector3 position, Quaternion rotation, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;

            if (material != null)
            {
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
        }

        // ------------------------------------------------------------------

        private static string Summarise(
            List<FoliageField> fields, List<FoliageBuildStats> stats, bool worldCreated)
        {
            var text = new StringBuilder();
            text.AppendLine($"[SabaProps Foliage] サンプルシーンを {ScenePath} に作成しました。");

            int instances = 0;
            int renderers = 0;
            int triangles = 0;
            float seconds = 0f;

            for (int i = 0; i < fields.Count; i++)
            {
                FoliageBuildStats entry = stats[i];
                string section = fields[i].transform.parent != null
                    ? fields[i].transform.parent.name
                    : "-";

                if (entry == null)
                {
                    text.AppendLine($"  {section} / {fields[i].name}: ビルドに失敗しました。");
                    continue;
                }

                instances += entry.instanceCount;
                renderers += entry.rendererCount;
                triangles += entry.triangleCount;
                seconds += entry.buildSeconds;

                text.AppendLine(
                    $"  {section} / {fields[i].name}: {entry.instanceCount:N0} 個体 / " +
                    $"{entry.rendererCount:N0} Renderer / 推定 {entry.EstimatedDrawCalls:N0} ドローコール");
            }

            text.AppendLine(
                $"  合計: {instances:N0} 個体 / {renderers:N0} Renderer / {triangles:N0} 三角形 / {seconds:0.00} 秒");

            text.AppendLine(worldCreated
                ? "  VRChat: VRCSceneDescriptor と Spawn を配置しました。そのままアップロードできます。"
                : "  VRChat: Worlds SDK が見つからないため VRCSceneDescriptor は配置していません。"
                  + " SDK を導入してから再実行すると追加されます。");

            return text.ToString().TrimEnd();
        }
    }
}
