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
        public const string SeasonRoot = "6 Seasons";
        public const string GroundRoot = "Ground";

        public const string InstancedPlotName = "GPU Instanced";
        public const string MergedPlotName = "Merged Chunks";
        public const string SkinnedPlotName = "Skinned Mesh";

        /// <summary>Player spawn, on the flat ground and facing up the garden.</summary>
        public static readonly Vector3 SpawnPosition = new Vector3(0f, 0.05f, -7f);

        /// <summary>Plot side length, and the spacing between plot centres.</summary>
        private const float PlotSize = 7f;
        private const float Pitch = 9f;
        private const float PlotDensity = 10f;

        /// <summary>Column centres, four across.</summary>
        private static readonly float[] Columns = { -13.5f, -4.5f, 4.5f, 13.5f };

        /// <summary>
        /// Five centres, for the rows that need one column per species or
        /// per season. Both outgrew the four-wide grid the rest of the
        /// garden is laid out on, and stretching that grid would have moved
        /// every other section for the sake of two.
        /// </summary>
        private static readonly float[] WideColumns = { -18f, -9f, 0f, 9f, 18f };

        /// <summary>
        /// Rows the single-species section needs to show every species.
        /// <para>
        /// The section that grows is the one at the front, so everything behind
        /// it is pushed back rather than a species falling off the end of a row
        /// and quietly not being shown.
        /// </para>
        /// </summary>
        private static int SingleSpeciesRows
        {
            get
            {
                return Mathf.Max(1,
                    Mathf.CeilToInt(FoliageAssetLibrary.AllKinds.Length / (float)WideColumns.Length));
            }
        }

        /// <summary>
        /// Where a section starts, counting from the spawn end of the garden.
        /// Section 0 is the single-species block; the rest follow whatever depth
        /// it turned out to need.
        /// </summary>
        private static float SectionZ(int section)
        {
            return section == 0 ? 0f : (SingleSpeciesRows - 1 + section) * Pitch;
        }

        /// <summary>Far edge of the last section, used to size the ground.</summary>
        private static float GardenBack
        {
            get { return SectionZ(5) + PlotSize; }
        }

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
                view.LookAt(new Vector3(0f, 0.5f, GardenBack * 0.5f), Quaternion.Euler(38f, 0f, 0f), GardenBack + 8f);
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
            BuildSeasons(material, fields);

            var stats = new List<FoliageBuildStats>(fields.Count);
            foreach (FoliageField field in fields)
            {
                stats.Add(FoliageFieldBuilder.Build(field));
            }

            GameObject world = FoliageVrcWorld.TryCreateWorld(
                SpawnPosition, Quaternion.identity, Camera.main);

            // Only when the movement sample has been imported. VRChat's defaults
            // walk at 2 m/s and cannot jump, which makes a garden this size
            // tedious to look around.
            bool movement = FoliageVrcWorld.TryAddDemoMovement(world);

            FoliageAssetLibrary.EnsureFolder(SampleFolder);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log(Summarise(fields, stats, world != null, movement));
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

            for (int i = 0; i < FoliageAssetLibrary.AllKinds.Length; i++)
            {
                FoliageSpeciesKind kind = FoliageAssetLibrary.AllKinds[i];
                FoliageSpecies species = Of(stock, kind);
                if (species == null)
                {
                    continue;
                }

                FoliageField field = CreatePlot(
                    root, FoliageAssetLibrary.DisplayName(kind),
                    new Vector3(WideColumns[i % WideColumns.Length], 0f, (i / WideColumns.Length) * Pitch),
                    DensityFor(kind), 3001 + i, FoliageOutputMode.MergedChunks);

                AddSpecies(field, species, 1f);
                fields.Add(field);
            }
        }

        /// <summary>
        /// Five species with one parameter block pushed somewhere else.
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

            FoliageSpecies rice = CreateVariant("Grain_Rice", FoliageSpeciesKind.Grain, material,
                species =>
                {
                    // The same generator as the wheat next to it. Rice bows under
                    // the weight of its grain and carries no awns, and that is
                    // the whole of the difference.
                    species.grain.earDroop = 0.85f;
                    species.grain.awnLength = 0f;
                    species.grain.earLength = 0.14f;
                    species.grain.earWidth = 0.022f;
                    species.grain.grainRows = 6;
                    species.grain.height = 0.72f;
                    species.grain.rootColor = new Color(0.298f, 0.396f, 0.180f, 1f);
                    species.grain.tipColor = new Color(0.573f, 0.596f, 0.298f, 1f);
                    species.grain.earColor = new Color(0.678f, 0.639f, 0.361f, 1f);
                    species.minSpacing = 0.1f;
                });

            AddVariantPlot(root, fields, "Grass - Tall", 0, tallGrass, 6f);
            AddVariantPlot(root, fields, "Clover - Broad", 1, wideClover, 9f);
            AddVariantPlot(root, fields, "Sunflower - Dwarf", 2, dwarfSunflower, 3.5f);
            AddVariantPlot(root, fields, "Reed - Splayed", 3, bareReed, 5f);
            AddVariantPlot(root, fields, "Grain - Rice", 4, rice, 9f);
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
                root, name, new Vector3(WideColumns[column], 0f, SectionZ(1)),
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

            string[] names = { "Mound", "Ramp", "Terrace", SkinnedPlotName };

            for (int i = 0; i < names.Length; i++)
            {
                FoliageField field = CreatePlot(
                    root, names[i], new Vector3(Columns[i], 0f, SectionZ(2)),
                    PlotDensity, 3201 + i, FoliageOutputMode.MergedChunks);

                // The ramp is steeper than the sunflower's slope limit, so the
                // same mix comes out grass and clover only. That difference is
                // the point of the plot.
                AddSpecies(field, grass, 1f);
                AddSpecies(field, clover, 0.5f);
                AddSpecies(field, sunflower, 0.1f);

                if (names[i] == SkinnedPlotName)
                {
                    // The skinned ground carries no collider, so without this the
                    // rays fall straight through it to the flat plane below.
                    SkinnedMeshRenderer skinned = FindSkinnedGround();
                    if (skinned != null)
                    {
                        field.skinnedGround.Add(skinned);
                    }
                }

                fields.Add(field);
            }
        }

        private static SkinnedMeshRenderer FindSkinnedGround()
        {
            foreach (SkinnedMeshRenderer skinned in UnityEngine.Object.FindObjectsOfType<SkinnedMeshRenderer>())
            {
                if (skinned.name == SkinnedPlotName + " Ground")
                {
                    return skinned;
                }
            }

            return null;
        }

        /// <summary>Four mixes of the stock species.</summary>
        private static void BuildMixes(List<FoliageSpecies> stock, List<FoliageField> fields)
        {
            Transform root = CreateRoot(MixRoot);

            FoliageSpecies grass = Of(stock, FoliageSpeciesKind.GrassClump);
            FoliageSpecies clover = Of(stock, FoliageSpeciesKind.Clover);
            FoliageSpecies sunflower = Of(stock, FoliageSpeciesKind.Sunflower);
            FoliageSpecies reed = Of(stock, FoliageSpeciesKind.Reed);
            FoliageSpecies smallFlower = Of(stock, FoliageSpeciesKind.SmallFlower);

            FoliageField meadow = CreatePlot(
                root, "Meadow", new Vector3(Columns[0], 0f, SectionZ(3)),
                PlotDensity, 3301, FoliageOutputMode.MergedChunks);
            AddSpecies(meadow, grass, 1f);
            AddSpecies(meadow, clover, 0.45f);
            AddSpecies(meadow, sunflower, 0.06f);
            fields.Add(meadow);

            FoliageField waterside = CreatePlot(
                root, "Waterside", new Vector3(Columns[1], 0f, SectionZ(3)),
                PlotDensity, 3302, FoliageOutputMode.MergedChunks);
            AddSpecies(waterside, grass, 1f);
            AddSpecies(waterside, reed, 0.22f);
            fields.Add(waterside);

            FoliageField flowerbed = CreatePlot(
                root, "Flowerbed", new Vector3(Columns[2], 0f, SectionZ(3)),
                PlotDensity, 3303, FoliageOutputMode.MergedChunks);
            AddSpecies(flowerbed, clover, 1f);
            AddSpecies(flowerbed, sunflower, 0.3f);
            fields.Add(flowerbed);

            // The case the small flower exists for: flowers as the ground cover
            // rather than as an accent scattered through one. Grass is the
            // minority here, which is the whole difference from the meadow.
            FoliageField flowerField = CreatePlot(
                root, "Flower Field", new Vector3(Columns[3], 0f, SectionZ(3)),
                PlotDensity, 3304, FoliageOutputMode.MergedChunks);
            AddSpecies(flowerField, smallFlower, 1f);
            AddSpecies(flowerField, grass, 0.3f);
            fields.Add(flowerField);
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
                    root, names[i], new Vector3(Columns[i], 0f, SectionZ(4)),
                    PlotDensity, 3401, modes[i]);

                AddSpecies(field, grass, 1f);
                AddSpecies(field, clover, 0.4f);
                fields.Add(field);
            }
        }

        /// <summary>
        /// The same mix, the same seed, four times over. Only the season differs,
        /// so the four plots are one comparison rather than four fields.
        /// </summary>
        private static void BuildSeasons(Material material, List<FoliageField> fields)
        {
            Transform root = CreateRoot(SeasonRoot);

            for (int i = 0; i < FoliageAssetLibrary.AllSeasons.Length && i < WideColumns.Length; i++)
            {
                FoliageSeason season = FoliageAssetLibrary.AllSeasons[i];

                // One seed for every plot: each clump stands where its
                // neighbour's does, so what is left to see is the season alone.
                FoliageField field = CreatePlot(
                    root, season.ToString(), new Vector3(WideColumns[i], 0f, SectionZ(5)),
                    PlotDensity, 3501, FoliageOutputMode.MergedChunks);

                AddSpecies(field, SeasonalSpecies(FoliageSpeciesKind.GrassClump, season, material), 1f);
                AddSpecies(field, SeasonalSpecies(FoliageSpeciesKind.Clover, season, material), 0.45f);
                AddSpecies(field, SeasonalSpecies(FoliageSpeciesKind.Sunflower, season, material), 0.06f);

                fields.Add(field);
            }
        }

        /// <summary>
        /// The stock preset for a kind in one season, created on demand. Summer
        /// resolves to the plain preset rather than a copy of it.
        /// </summary>
        private static FoliageSpecies SeasonalSpecies(
            FoliageSpeciesKind kind, FoliageSeason season, Material material)
        {
            FoliageSpecies species = FoliageAssetLibrary.CreateOrLoadDefaultSpecies(kind, material, season);
            if (species == null)
            {
                return null;
            }

            FoliageAssetLibrary.WriteSpeciesMesh(species);
            return species;
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
                case FoliageSpeciesKind.SmallFlower: return 12f;
                case FoliageSpeciesKind.Weed: return 6f;
                case FoliageSpeciesKind.Grain: return 9f;
                case FoliageSpeciesKind.Dandelion: return 5f;
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

            // Covers every plot with room to walk between them, and follows the
            // layout rather than restating it: adding a species lengthens the
            // garden, and ground that did not grow with it would leave the last
            // row raycasting into nothing.
            //
            // A Plane primitive is 10 m square before scaling. The front edge
            // clears the spawn at z = -7; the sides clear the season row, which
            // reaches out to x = 18 plus half a plot.
            const float front = -12f;
            float back = GardenBack + 5f;

            CreateGroundPiece(root.transform, PrimitiveType.Plane, "Flat",
                new Vector3(0f, 0f, (front + back) * 0.5f), Quaternion.identity,
                new Vector3(4.6f, 1f, (back - front) * 0.1f), material);

            // --- section 3's ground -----------------------------------------
            float z = SectionZ(2);

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

            BuildSkinnedGround(root.transform, new Vector3(Columns[3], 0f, z), material);
        }

        /// <summary>
        /// A lumpy surface driven by bones, with no collider of its own.
        /// <para>
        /// This is the case a MeshCollider cannot cover: the shape only exists
        /// once the skin is evaluated. The plot above it lists this renderer as
        /// skinned ground, and the scatterer bakes the pose into a throwaway
        /// collider for the duration of the build.
        /// </para>
        /// </summary>
        private static void BuildSkinnedGround(Transform parent, Vector3 origin, Material material)
        {
            const int cells = 8;
            const float size = 7.6f;
            const int boneGrid = 3;

            var root = new GameObject(SkinnedPlotName + " Ground");
            root.transform.SetParent(parent, false);
            root.transform.position = origin;

            // Bones on a coarse grid, each lifted by a fixed amount. Fixed, not
            // random: the demo has to look the same on every machine.
            var bones = new Transform[boneGrid * boneGrid];
            var bindPoses = new Matrix4x4[bones.Length];
            float[] lift = { 0.15f, 0.95f, 0.35f, 0.75f, 1.35f, 0.25f, 0.45f, 0.65f, 1.05f };

            for (int i = 0; i < bones.Length; i++)
            {
                int bx = i % boneGrid;
                int bz = i / boneGrid;

                var bone = new GameObject($"Bone_{bx}_{bz}").transform;
                bone.SetParent(root.transform, false);

                // Bind flat, then lift. Computing the bind pose from the lifted
                // position would make bind pose and current pose identical, and
                // the skin would deform the mesh by exactly nothing.
                bone.localPosition = new Vector3(
                    Mathf.Lerp(-size * 0.5f, size * 0.5f, bx / (float)(boneGrid - 1)),
                    0f,
                    Mathf.Lerp(-size * 0.5f, size * 0.5f, bz / (float)(boneGrid - 1)));

                bones[i] = bone;
                bindPoses[i] = bone.worldToLocalMatrix * root.transform.localToWorldMatrix;
            }

            var vertices = new Vector3[(cells + 1) * (cells + 1)];
            var normals = new Vector3[vertices.Length];
            var weights = new BoneWeight[vertices.Length];

            for (int vz = 0; vz <= cells; vz++)
            {
                for (int vx = 0; vx <= cells; vx++)
                {
                    int index = vz * (cells + 1) + vx;

                    vertices[index] = new Vector3(
                        Mathf.Lerp(-size * 0.5f, size * 0.5f, vx / (float)cells),
                        0f,
                        Mathf.Lerp(-size * 0.5f, size * 0.5f, vz / (float)cells));

                    normals[index] = Vector3.up;

                    // Rigid binding to the nearest bone. Smooth weights would
                    // read better, but this is ground for a demo, and the hard
                    // creases make it obvious the surface is skinned.
                    weights[index] = new BoneWeight
                    {
                        boneIndex0 = NearestBone(bones, root.transform.TransformPoint(vertices[index])),
                        weight0 = 1f,
                    };
                }
            }

            var triangles = new int[cells * cells * 6];
            int t = 0;

            for (int vz = 0; vz < cells; vz++)
            {
                for (int vx = 0; vx < cells; vx++)
                {
                    int a = vz * (cells + 1) + vx;
                    int b = a + 1;
                    int c = a + cells + 1;
                    int d = c + 1;

                    triangles[t++] = a;
                    triangles[t++] = c;
                    triangles[t++] = b;
                    triangles[t++] = b;
                    triangles[t++] = c;
                    triangles[t++] = d;
                }
            }

            var mesh = new Mesh { name = "SabaFoliage_SkinnedGround" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.boneWeights = weights;
            mesh.bindposes = bindPoses;
            mesh.RecalculateBounds();

            var skinned = root.AddComponent<SkinnedMeshRenderer>();
            skinned.sharedMesh = mesh;
            skinned.bones = bones;
            skinned.rootBone = bones[bones.Length / 2];
            skinned.sharedMaterial = material;
            skinned.updateWhenOffscreen = true;

            // Pose it only now that the bind pose is recorded.
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].localPosition += Vector3.up * lift[i];
            }
        }

        private static int NearestBone(Transform[] bones, Vector3 worldPosition)
        {
            int nearest = 0;
            float best = float.MaxValue;

            for (int i = 0; i < bones.Length; i++)
            {
                float distance = (bones[i].position - worldPosition).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = i;
                }
            }

            return nearest;
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
            List<FoliageField> fields, List<FoliageBuildStats> stats,
            bool worldCreated, bool movementAdded)
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

            if (worldCreated)
            {
                text.AppendLine(movementAdded
                    ? "  移動設定: FoliageDemoMovement を配置しました。歩行 4 m/s・走行 9 m/s・ジャンプ可です。"
                    : "  移動設定: 未導入です。VRChat の既定は歩行 2 m/s・ジャンプ不可なので、"
                      + " Tools > SabaProps > Foliage > Import VRChat Demo Movement のあと再実行すると追加されます。");
            }

            return text.ToString().TrimEnd();
        }
    }
}
