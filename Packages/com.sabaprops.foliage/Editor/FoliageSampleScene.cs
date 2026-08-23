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

        public const string MeadowName = "Meadow (GPU Instanced)";
        public const string ClearingName = "Clearing (Merged Chunks)";

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
                view.LookAt(new Vector3(0f, 0.5f, 0f), Quaternion.Euler(16f, 22f, 0f), 28f);
            }
        }

        /// <summary>
        /// Replaces the open scene with the demo, builds both fields and saves
        /// the result to <see cref="ScenePath"/>. Prompt free, so tests and
        /// batch mode can call it directly.
        /// </summary>
        public static Scene Create()
        {
            if (!FoliageAssetLibrary.CreateOrLoadDefaults(
                    out _, out FoliageSpecies grass, out FoliageSpecies sunflower))
            {
                return default;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            ConfigureLight();
            ConfigureCamera();
            BuildGround(CreateOrLoadGroundMaterial());

            FoliageField meadow = CreateMeadow(grass, sunflower);
            FoliageField clearing = CreateClearing(grass);

            FoliageBuildStats meadowStats = FoliageFieldBuilder.Build(meadow);
            FoliageBuildStats clearingStats = FoliageFieldBuilder.Build(clearing);

            FoliageAssetLibrary.EnsureFolder(SampleFolder);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log(Summarise(meadowStats, clearingStats));
            return scene;
        }

        // ------------------------------------------------------------------

        private static void ConfigureLight()
        {
            Light light = Object.FindObjectOfType<Light>();
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
                new Vector3(0f, 6.5f, -24f), Quaternion.Euler(11f, 0f, 0f));
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
            var root = new GameObject("Ground");

            CreateGroundPiece(root.transform, PrimitiveType.Plane, "Flat",
                new Vector3(0f, 0f, 0f), Quaternion.identity, new Vector3(4f, 1f, 4f), material);

            CreateGroundPiece(root.transform, PrimitiveType.Sphere, "Mound",
                new Vector3(-9f, -3.1f, 3f), Quaternion.identity, new Vector3(14f, 8f, 14f), material);

            // 30 degrees sits past the sunflower slope limit but inside the
            // grass one, so the ramp comes out grass-only with no per-object
            // setup — the slope filter demonstrating itself.
            CreateGroundPiece(root.transform, PrimitiveType.Cube, "Ramp",
                new Vector3(-9f, 0.6f, -6f), Quaternion.Euler(-30f, 0f, 0f), new Vector3(10f, 0.4f, 7f), material);
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

        private static FoliageField CreateMeadow(FoliageSpecies grass, FoliageSpecies sunflower)
        {
            var go = new GameObject(MeadowName);
            go.transform.position = new Vector3(-9f, 0f, 0f);

            var field = go.AddComponent<FoliageField>();
            field.shape = FoliageAreaShape.Rectangle;
            field.size = new Vector2(18f, 18f);
            field.density = 4.5f;
            field.seed = 1024;
            field.chunkSize = 6f;
            field.outputMode = FoliageOutputMode.GpuInstanced;
            field.species.Add(grass);
            field.species.Add(sunflower);

            return field;
        }

        private static FoliageField CreateClearing(FoliageSpecies grass)
        {
            var go = new GameObject(ClearingName);
            go.transform.position = new Vector3(9f, 0f, 0f);

            var field = go.AddComponent<FoliageField>();
            field.shape = FoliageAreaShape.Circle;
            field.radius = 7f;
            field.density = 8f;
            field.seed = 2048;
            field.chunkSize = 5f;
            field.outputMode = FoliageOutputMode.MergedChunks;
            field.species.Add(grass);

            return field;
        }

        private static string Summarise(FoliageBuildStats meadow, FoliageBuildStats clearing)
        {
            var text = new StringBuilder();
            text.AppendLine($"[SabaProps Foliage] サンプルシーンを {ScenePath} に作成しました。");
            AppendStats(text, MeadowName, meadow);
            AppendStats(text, ClearingName, clearing);
            return text.ToString().TrimEnd();
        }

        private static void AppendStats(StringBuilder text, string name, FoliageBuildStats stats)
        {
            if (stats == null)
            {
                text.AppendLine($"  {name}: ビルドに失敗しました。Console のエラーを確認してください。");
                return;
            }

            text.AppendLine(
                $"  {name}: {stats.instanceCount:N0} 個体 / {stats.rendererCount:N0} Renderer / " +
                $"{stats.triangleCount:N0} 三角形 / 推定 {stats.EstimatedDrawCalls:N0} ドローコール / " +
                $"{stats.buildSeconds:0.00} 秒");
        }
    }
}
