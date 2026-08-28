using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SabaProps.Foliage;

namespace SabaProps.Trees.Editors
{
    /// <summary>Generates the compact grouped-planting sample distributed in Samples~.</summary>
    public static class TreeBundledDemo
    {
        public const string OutputRoot = "Assets/SabaProps/TreesBundledDemo";
        public const string ScenePath = OutputRoot + "/TreesDemo.unity";
        public const string SeasonalScenePath =
            OutputRoot + "/SeasonalTreesDemo.unity";
        private const string AssetFolder = OutputRoot + "/Assets";

        private static readonly TreeBotanicalPreset[] AllPresets =
        {
            TreeBotanicalPreset.JapaneseZelkova,
            TreeBotanicalPreset.JapaneseMaple,
            TreeBotanicalPreset.JapaneseCedar,
            TreeBotanicalPreset.HinokiCypress,
            TreeBotanicalPreset.JapaneseRedPine,
            TreeBotanicalPreset.JapaneseWhiteBirch,
            TreeBotanicalPreset.SomeiYoshinoSpring,
            TreeBotanicalPreset.SomeiYoshinoSummer,
            TreeBotanicalPreset.GinkgoSummer,
            TreeBotanicalPreset.GinkgoAutumn,
        };

        [MenuItem("Tools/SabaProps/Trees/Create Bundled Demo", false, 2)]
        public static void CreateAndOpen()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Create();
            }
        }

        /// <summary>Prompt-free entry point used to refresh Samples~.</summary>
        public static void GenerateForDistribution()
        {
            Create();
        }

        /// <summary>Creates and saves grouped planting and seasonal sample scenes.</summary>
        public static Scene Create()
        {
            if (AssetDatabase.IsValidFolder(OutputRoot))
            {
                AssetDatabase.DeleteAsset(OutputRoot);
            }
            TreeAssetLibrary.EnsureFolder(AssetFolder);

            Material treeMaterial = CreateTreeMaterial();
            CreateGroundMaterial();
            CreateRoadMaterial();
            CreateSpeciesAssets(treeMaterial);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            CreatePlantingScene();
            CreateSeasonalPlantingScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            Debug.Log(
                "[SabaProps Trees] Bundled grouped demos created at "
                + ScenePath + " and " + SeasonalScenePath);
            return scene;
        }

        private static void CreateSpeciesAssets(Material material)
        {
            foreach (TreeBotanicalPreset preset in AllPresets)
            {
                string displayName = TreeAssetLibrary.DisplayName(preset);
                var species = ScriptableObject.CreateInstance<TreeSpecies>();
                species.name = displayName;
                species.ApplyBotanicalPreset(preset);
                species.material = material;
                AssetDatabase.CreateAsset(
                    species,
                    AssetFolder + "/" + displayName + ".asset");

                Mesh[] meshes = new Mesh[3];
                for (int lod = 0; lod < meshes.Length; lod++)
                {
                    meshes[lod] = TreeMeshBuilder.Build(species, lod);
                    meshes[lod].name = displayName + "_LOD" + lod;
                    AssetDatabase.CreateAsset(
                        meshes[lod],
                        AssetFolder + "/" + meshes[lod].name + ".asset");
                }
                species.lod0Mesh = meshes[0];
                species.lod1Mesh = meshes[1];
                species.lod2Mesh = meshes[2];
                EditorUtility.SetDirty(species);
            }
        }

        private static Dictionary<TreeBotanicalPreset, TreeSpecies>
            LoadSpeciesAssets()
        {
            var result = new Dictionary<TreeBotanicalPreset, TreeSpecies>();
            foreach (TreeBotanicalPreset preset in AllPresets)
            {
                string displayName = TreeAssetLibrary.DisplayName(preset);
                TreeSpecies species = AssetDatabase.LoadAssetAtPath<TreeSpecies>(
                    AssetFolder + "/" + displayName + ".asset");
                if (species != null)
                {
                    // A scene switch can unload generated Mesh objects that were
                    // not referenced by the previous scene. Resolve each asset
                    // explicitly before building the next group of instances.
                    species.lod0Mesh = AssetDatabase.LoadAssetAtPath<Mesh>(
                        AssetFolder + "/" + displayName + "_LOD0.asset");
                    species.lod1Mesh = AssetDatabase.LoadAssetAtPath<Mesh>(
                        AssetFolder + "/" + displayName + "_LOD1.asset");
                    species.lod2Mesh = AssetDatabase.LoadAssetAtPath<Mesh>(
                        AssetFolder + "/" + displayName + "_LOD2.asset");
                    species.material = AssetDatabase.LoadAssetAtPath<Material>(
                        AssetFolder + "/TreesDemo.mat");
                    result.Add(preset, species);
                }
            }
            return result;
        }

        private static void CreatePlantingScene()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single);
            IReadOnlyDictionary<TreeBotanicalPreset, TreeSpecies> species =
                LoadSpeciesAssets();
            Material groundMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                AssetFolder + "/TreesDemoGround.mat");
            Material roadMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                AssetFolder + "/TreesDemoRoad.mat");
            ConfigureCameraAndLight();
            CreateGround(groundMaterial);
            CreateRoad(
                "Street",
                new Vector3(0f, 0.025f, -5f),
                new Vector3(27f, 0.05f, 2.6f),
                roadMaterial);

            CreateMixedWoodland(species);
            CreateConiferPlantation(species);
            CreateStreetAvenue(species);

            CreateLabel(
                "Grouped Japanese trees: woodland / plantation / street",
                new Vector3(-13.1f, 8.7f, 10.2f),
                0.072f);
            CreateLabel("Mixed woodland", new Vector3(-12.4f, 0.05f, -0.2f), 0.06f);
            CreateLabel("Sugi / Hinoki plantation", new Vector3(2.1f, 0.05f, -0.2f), 0.06f);
            CreateLabel("Zelkova street trees", new Vector3(-12.4f, 0.08f, -7.35f), 0.06f);

            TreeAssetLibrary.EnsureFolder(OutputRoot);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void CreateMixedWoodland(
            IReadOnlyDictionary<TreeBotanicalPreset, TreeSpecies> species)
        {
            var root = new GameObject("Mixed Woodland - Height Variation");
            Vector2[] positions =
            {
                new Vector2(-11.4f, 1.1f), new Vector2(-8.9f, 0.7f),
                new Vector2(-6.4f, 1.4f), new Vector2(-3.7f, 0.8f),
                new Vector2(-10.4f, 3.3f), new Vector2(-7.6f, 3.0f),
                new Vector2(-4.8f, 3.8f), new Vector2(-2.7f, 3.0f),
                new Vector2(-11.7f, 5.8f), new Vector2(-9.0f, 5.4f),
                new Vector2(-6.2f, 6.1f), new Vector2(-3.5f, 5.5f),
                new Vector2(-10.4f, 8.2f), new Vector2(-7.4f, 8.5f),
                new Vector2(-4.5f, 7.9f),
            };
            TreeBotanicalPreset[] mix =
            {
                TreeBotanicalPreset.JapaneseZelkova,
                TreeBotanicalPreset.JapaneseMaple,
                TreeBotanicalPreset.JapaneseZelkova,
                TreeBotanicalPreset.JapaneseRedPine,
            };
            var random = new FoliageRandom(4101);
            for (int i = 0; i < positions.Length; i++)
            {
                TreeBotanicalPreset preset = mix[random.RangeInt(0, mix.Length)];
                CreateTreeInstance(
                    root.transform,
                    species[preset],
                    "Woodland " + (i + 1),
                    new Vector3(positions[i].x, 0f, positions[i].y),
                    0.46f * random.Range(0.78f, 1.22f),
                    random.Range(0f, 360f));
            }
        }

        private static void CreateConiferPlantation(
            IReadOnlyDictionary<TreeBotanicalPreset, TreeSpecies> species)
        {
            var root = new GameObject("Sugi Hinoki Plantation");
            var random = new FoliageRandom(4102);
            int index = 0;
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    TreeBotanicalPreset preset = (row + column) % 3 == 0
                        ? TreeBotanicalPreset.HinokiCypress
                        : TreeBotanicalPreset.JapaneseCedar;
                    Vector3 position = new Vector3(
                        3.3f + column * 2.7f + random.Range(-0.16f, 0.16f),
                        0f,
                        1.1f + row * 2.35f + random.Range(-0.12f, 0.12f));
                    CreateTreeInstance(
                        root.transform,
                        species[preset],
                        "Plantation " + (++index),
                        position,
                        0.44f * random.Range(0.86f, 1.16f),
                        random.Range(0f, 360f));
                }
            }
        }

        private static void CreateStreetAvenue(
            IReadOnlyDictionary<TreeBotanicalPreset, TreeSpecies> species)
        {
            var root = new GameObject("Zelkova Street Avenue");
            var random = new FoliageRandom(4103);
            int index = 0;
            for (int row = 0; row < 2; row++)
            {
                float z = row == 0 ? -3.25f : -6.75f;
                for (int column = 0; column < 7; column++)
                {
                    CreateTreeInstance(
                        root.transform,
                        species[TreeBotanicalPreset.JapaneseZelkova],
                        "Street Zelkova " + (++index),
                        new Vector3(-11.1f + column * 3.7f, 0f, z),
                        0.43f * random.Range(0.92f, 1.08f),
                        random.Range(0f, 360f));
                }
            }
        }

        private static void CreateSeasonalPlantingScene()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single);
            IReadOnlyDictionary<TreeBotanicalPreset, TreeSpecies> species =
                LoadSpeciesAssets();
            Material groundMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                AssetFolder + "/TreesDemoGround.mat");
            Material roadMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                AssetFolder + "/TreesDemoRoad.mat");
            ConfigureCameraAndLight();
            CreateGround(groundMaterial);
            CreateRoad(
                "Sakura Avenue Path",
                new Vector3(-7f, 0.025f, 2f),
                new Vector3(3f, 0.05f, 16.5f),
                roadMaterial);
            CreateRoad(
                "Ginkgo Avenue Path",
                new Vector3(7f, 0.025f, 2f),
                new Vector3(3f, 0.05f, 16.5f),
                roadMaterial);

            CreateSeasonalAvenue(
                species,
                "Sakura Avenue - Spring and Summer",
                TreeBotanicalPreset.SomeiYoshinoSpring,
                TreeBotanicalPreset.SomeiYoshinoSummer,
                -8.9f,
                -5.1f,
                4201);
            CreateSeasonalAvenue(
                species,
                "Ginkgo Avenue - Summer and Autumn",
                TreeBotanicalPreset.GinkgoSummer,
                TreeBotanicalPreset.GinkgoAutumn,
                5.1f,
                8.9f,
                4202);
            CreateBirchGrove(species);

            CreateLabel(
                "Seasonal grouped trees: spring / summer / autumn",
                new Vector3(-13.1f, 8.7f, 10.2f),
                0.072f);
            CreateLabel("Sakura avenue", new Vector3(-11.0f, 0.06f, -7.2f), 0.06f);
            CreateLabel("White birch grove", new Vector3(-2.1f, 0.06f, -2.4f), 0.06f);
            CreateLabel("Ginkgo avenue", new Vector3(4.1f, 0.06f, -7.2f), 0.06f);

            EditorSceneManager.SaveScene(scene, SeasonalScenePath);
        }

        private static void CreateSeasonalAvenue(
            IReadOnlyDictionary<TreeBotanicalPreset, TreeSpecies> species,
            string name,
            TreeBotanicalPreset leftPreset,
            TreeBotanicalPreset rightPreset,
            float leftX,
            float rightX,
            int seed)
        {
            var root = new GameObject(name);
            var random = new FoliageRandom(seed);
            int index = 0;
            for (int row = 0; row < 6; row++)
            {
                float z = -5.6f + row * 2.85f;
                CreateTreeInstance(
                    root.transform,
                    species[leftPreset],
                    "Left Row " + (++index),
                    new Vector3(leftX, 0f, z),
                    0.43f * random.Range(0.91f, 1.09f),
                    random.Range(0f, 360f));
                CreateTreeInstance(
                    root.transform,
                    species[rightPreset],
                    "Right Row " + index,
                    new Vector3(rightX, 0f, z),
                    0.43f * random.Range(0.91f, 1.09f),
                    random.Range(0f, 360f));
            }
        }

        private static void CreateBirchGrove(
            IReadOnlyDictionary<TreeBotanicalPreset, TreeSpecies> species)
        {
            var root = new GameObject("White Birch Grove - Height Variation");
            Vector2[] positions =
            {
                new Vector2(-2.0f, -0.5f), new Vector2(0.2f, -0.9f),
                new Vector2(2.0f, 0.2f), new Vector2(-1.1f, 1.8f),
                new Vector2(1.3f, 2.5f), new Vector2(-2.2f, 4.0f),
                new Vector2(0.1f, 4.5f), new Vector2(2.1f, 4.0f),
                new Vector2(-1.0f, 6.5f), new Vector2(1.3f, 6.9f),
                new Vector2(-2.0f, 8.5f), new Vector2(0.3f, 8.9f),
                new Vector2(2.2f, 8.2f),
            };
            var random = new FoliageRandom(4203);
            for (int i = 0; i < positions.Length; i++)
            {
                CreateTreeInstance(
                    root.transform,
                    species[TreeBotanicalPreset.JapaneseWhiteBirch],
                    "White Birch " + (i + 1),
                    new Vector3(positions[i].x, 0f, positions[i].y),
                    0.43f * random.Range(0.78f, 1.23f),
                    random.Range(0f, 360f));
            }
        }

        private static GameObject CreateTreeInstance(
            Transform parent,
            TreeSpecies species,
            string name,
            Vector3 position,
            float scale,
            float yaw)
        {
            GameObject root = TreeAssetLibrary.CreateLodGroupInstance(species);
            if (root == null)
            {
                throw new System.InvalidOperationException(
                    "Bundled tree instance is missing generated LOD assets: "
                    + (species != null ? species.name : "null species"));
            }
            root.name = name + " ("
                + TreeAssetLibrary.DisplayName(species.botanicalPreset) + ")";
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            root.transform.localScale = Vector3.one * scale;
            return root;
        }

        private static void CreateGround(Material material)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Tree Demo Ground";
            ground.transform.localScale = new Vector3(3f, 1f, 2.15f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void CreateRoad(
            string name,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            GameObject road = GameObject.CreatePrimitive(PrimitiveType.Cube);
            road.name = name;
            road.transform.position = position;
            road.transform.localScale = scale;
            road.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Material CreateTreeMaterial()
        {
            Shader shader = Shader.Find(FoliageShaderContract.ShaderName);
            var material = new Material(shader)
            {
                name = "TreesDemo",
                enableInstancing = true,
            };
            material.SetFloat(FoliageShaderContract.DistanceFadeProperty, 0f);
            material.DisableKeyword(FoliageShaderContract.DistanceFadeKeyword);
            material.SetFloat("_Cull", 2f);
            material.SetFloat("_WindStrength", 0.14f);
            AssetDatabase.CreateAsset(material, AssetFolder + "/TreesDemo.mat");
            return material;
        }

        private static Material CreateGroundMaterial()
        {
            var material = new Material(Shader.Find("Standard"))
            {
                name = "TreesDemoGround",
                color = new Color(0.20f, 0.24f, 0.17f, 1f),
            };
            material.SetFloat("_Glossiness", 0.03f);
            AssetDatabase.CreateAsset(material, AssetFolder + "/TreesDemoGround.mat");
            return material;
        }

        private static Material CreateRoadMaterial()
        {
            var material = new Material(Shader.Find("Standard"))
            {
                name = "TreesDemoRoad",
                color = new Color(0.17f, 0.18f, 0.17f, 1f),
            };
            material.SetFloat("_Glossiness", 0.08f);
            AssetDatabase.CreateAsset(material, AssetFolder + "/TreesDemoRoad.mat");
            return material;
        }

        private static void CreateLabel(string text, Vector3 position, float size)
        {
            var labelObject = new GameObject(text + " Label");
            labelObject.transform.position = position;
            labelObject.transform.rotation = Quaternion.identity;
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.fontSize = 56;
            label.characterSize = size;
            label.anchor = TextAnchor.LowerLeft;
            label.color = new Color(0.08f, 0.09f, 0.07f, 1f);
        }

        private static void ConfigureCameraAndLight()
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.orthographic = false;
                camera.fieldOfView = 46f;
                camera.transform.position = new Vector3(0f, 13f, -27f);
                camera.transform.rotation = Quaternion.LookRotation(
                    new Vector3(0f, 3.1f, 1.6f) - camera.transform.position,
                    Vector3.up);
                camera.farClipPlane = 120f;
            }

            Light light = Object.FindObjectOfType<Light>();
            if (light != null)
            {
                light.transform.rotation = Quaternion.Euler(48f, -36f, 0f);
                light.color = new Color(1f, 0.96f, 0.87f, 1f);
                light.intensity = 1.15f;
                light.shadows = LightShadows.Soft;
            }
        }
    }
}
