using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SabaProps.Foliage;

namespace SabaProps.Trees.Editors
{
    /// <summary>Generates the compact LOD sample distributed in Samples~.</summary>
    public static class TreeBundledDemo
    {
        public const string OutputRoot = "Assets/SabaProps/TreesBundledDemo";
        public const string ScenePath = OutputRoot + "/TreesDemo.unity";
        public const string SeasonalScenePath =
            OutputRoot + "/SeasonalTreesDemo.unity";
        private const string AssetFolder = OutputRoot + "/Assets";

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

        /// <summary>Creates and saves the compact sample scene.</summary>
        public static Scene Create()
        {
            if (AssetDatabase.IsValidFolder(OutputRoot))
            {
                AssetDatabase.DeleteAsset(OutputRoot);
            }
            TreeAssetLibrary.EnsureFolder(AssetFolder);

            Material treeMaterial = CreateTreeMaterial();
            Material groundMaterial = CreateGroundMaterial();

            CreateScene(
                new[]
                {
                    TreeBotanicalPreset.JapaneseZelkova,
                    TreeBotanicalPreset.JapaneseMaple,
                    TreeBotanicalPreset.JapaneseCedar,
                    TreeBotanicalPreset.HinokiCypress,
                    TreeBotanicalPreset.JapaneseRedPine,
                },
                "Japanese trees: broadleaf / conifers",
                ScenePath,
                treeMaterial,
                groundMaterial);
            CreateScene(
                new[]
                {
                    TreeBotanicalPreset.JapaneseWhiteBirch,
                    TreeBotanicalPreset.SomeiYoshinoSpring,
                    TreeBotanicalPreset.SomeiYoshinoSummer,
                    TreeBotanicalPreset.GinkgoSummer,
                    TreeBotanicalPreset.GinkgoAutumn,
                },
                "Seasonal trees: spring / summer / autumn",
                SeasonalScenePath,
                treeMaterial,
                groundMaterial);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            Debug.Log(
                "[SabaProps Trees] Bundled demos created at "
                + ScenePath + " and " + SeasonalScenePath);
            return scene;
        }

        private static void CreateScene(
            TreeBotanicalPreset[] presets,
            string heading,
            string scenePath,
            Material treeMaterial,
            Material groundMaterial)
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single);
            ConfigureCameraAndLight();

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Tree Demo Ground";
            ground.transform.localScale = new Vector3(2.55f, 1f, 0.86f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;

            for (int i = 0; i < presets.Length; i++)
            {
                CreateTree(presets[i], i, treeMaterial);
            }

            CreateLabel(heading, new Vector3(-8.5f, 6.35f, 0.45f));
            TreeAssetLibrary.EnsureFolder(OutputRoot);
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static void CreateTree(
            TreeBotanicalPreset preset,
            int index,
            Material material)
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

            GameObject root = TreeAssetLibrary.CreateLodGroupInstance(species);
            if (root == null)
            {
                return;
            }
            root.name = displayName + " Tree";
            root.transform.position = new Vector3(
                -6.8f + index * 3.4f,
                0f,
                0.45f);
            root.transform.localScale = Vector3.one * 0.52f;
            CreateLabel(
                DemoLabel(preset),
                root.transform.position + new Vector3(-1.20f, 0.025f, -1.75f));
        }

        private static string DemoLabel(TreeBotanicalPreset preset)
        {
            switch (preset)
            {
                case TreeBotanicalPreset.JapaneseZelkova: return "Zelkova";
                case TreeBotanicalPreset.JapaneseMaple: return "Maple";
                case TreeBotanicalPreset.JapaneseCedar: return "Sugi";
                case TreeBotanicalPreset.JapaneseWhiteBirch: return "White Birch";
                case TreeBotanicalPreset.JapaneseRedPine: return "Akamatsu";
                case TreeBotanicalPreset.HinokiCypress: return "Hinoki";
                case TreeBotanicalPreset.SomeiYoshinoSpring: return "Sakura / Spring";
                case TreeBotanicalPreset.SomeiYoshinoSummer: return "Sakura / Summer";
                case TreeBotanicalPreset.GinkgoSummer: return "Ginkgo / Summer";
                case TreeBotanicalPreset.GinkgoAutumn: return "Ginkgo / Autumn";
                default: return preset.ToString();
            }
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

        private static void CreateLabel(string text, Vector3 position)
        {
            var labelObject = new GameObject(text + " Label");
            labelObject.transform.position = position;
            labelObject.transform.rotation = Quaternion.identity;
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.fontSize = 56;
            label.characterSize = 0.055f;
            label.anchor = TextAnchor.LowerLeft;
            label.color = new Color(0.08f, 0.09f, 0.07f, 1f);
        }

        private static void ConfigureCameraAndLight()
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.orthographic = true;
                camera.orthographicSize = 5.55f;
                camera.transform.position = new Vector3(0f, 5.2f, -21f);
                camera.transform.rotation = Quaternion.LookRotation(
                    new Vector3(0f, 2.8f, 0f) - camera.transform.position,
                    Vector3.up);
                camera.farClipPlane = 100f;
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
