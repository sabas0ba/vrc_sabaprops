using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SabaProps.Foliage.Editors
{
    /// <summary>
    /// Generates the compact, prebuilt scene distributed through the package's
    /// Samples~ folder. It intentionally contains no merged fields so the sample
    /// stays small enough to import with Package Manager.
    /// </summary>
    public static class FoliageBundledDemo
    {
        private enum VineDemoProfile
        {
            EnglishIvy,
            BostonIvy,
            CreepingFig,
        }

        public const string OutputRoot = "Assets/SabaProps/FoliageBundledDemo";
        public const string ScenePath = OutputRoot + "/FoliageDemo.unity";
        private const string AssetFolder = OutputRoot + "/Assets";

        [MenuItem("Tools/SabaProps/Foliage/Create Bundled Demo", false, 2)]
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
            FoliageAssetLibrary.EnsureFolder(AssetFolder);

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single);
            Material foliage = CreateFoliageMaterial();
            Material groundMaterial = CreateStandardMaterial(
                "DemoGround",
                new Color(0.19f, 0.23f, 0.16f, 1f));
            Material wallMaterial = CreateStandardMaterial(
                "DemoWall",
                new Color(0.43f, 0.42f, 0.37f, 1f));

            ConfigureCameraAndLight();
            GameObject ground = CreatePrimitive(
                PrimitiveType.Plane,
                "Ground Surface",
                new Vector3(0f, 0f, 0f),
                new Vector3(0.8f, 1f, 0.65f),
                groundMaterial);
            GameObject wall = CreatePrimitive(
                PrimitiveType.Cube,
                "Wall Surface",
                new Vector3(0f, 1.55f, 1.8f),
                new Vector3(6.4f, 3.1f, 0.3f),
                wallMaterial);
            GameObject slope = CreatePrimitive(
                PrimitiveType.Cube,
                "Slope Surface",
                new Vector3(2.15f, 0.64f, 0.10f),
                new Vector3(2.25f, 0.12f, 3.2f),
                wallMaterial);
            slope.transform.rotation = Quaternion.Euler(-24f, 0f, 0f);
            Physics.SyncTransforms();

            CreateSpeciesGallery(foliage);
            CreateVinePattern(
                "English Ivy - Floor to Wall",
                ground.GetComponent<Collider>(),
                new[] { wall.GetComponent<Collider>() },
                foliage,
                VineDemoProfile.EnglishIvy,
                901,
                new List<Vector3>
                {
                    new Vector3(-2.15f, 0.02f, 0.45f),
                    new Vector3(-2.10f, 0.02f, 1.48f),
                    new Vector3(-2.05f, 0.65f, 1.64f),
                    new Vector3(-1.75f, 2.75f, 1.64f),
                });
            CreateVinePattern(
                "Boston Ivy - Vertical Pigment",
                wall.GetComponent<Collider>(),
                new Collider[0],
                foliage,
                VineDemoProfile.BostonIvy,
                902,
                new List<Vector3>
                {
                    new Vector3(-0.25f, 0.08f, 1.64f),
                    new Vector3(-0.35f, 0.85f, 1.64f),
                    new Vector3(0.15f, 1.70f, 1.64f),
                    new Vector3(0.45f, 2.78f, 1.64f),
                });
            CreateVinePattern(
                "Creeping Fig - Floor Slope Wall",
                ground.GetComponent<Collider>(),
                new[]
                {
                    slope.GetComponent<Collider>(),
                    wall.GetComponent<Collider>(),
                },
                foliage,
                VineDemoProfile.CreepingFig,
                903,
                new List<Vector3>
                {
                    new Vector3(2.15f, 0.02f, -1.72f),
                    new Vector3(2.15f, 0.22f, -0.95f),
                    new Vector3(2.15f, 0.82f, 0.48f),
                    new Vector3(2.15f, 1.34f, 1.56f),
                    new Vector3(1.90f, 2.72f, 1.64f),
                });
            CreateRhizomePatch(
                ground.GetComponent<Collider>(),
                foliage);

            CreateLabel("Floor / wall", new Vector3(-2.95f, 3.35f, 1.55f), 0.18f);
            CreateLabel("Vertical", new Vector3(-0.65f, 3.35f, 1.55f), 0.18f);
            CreateLabel("Slope / wall", new Vector3(1.35f, 3.35f, 1.55f), 0.18f);
            CreateLabel("Rhizome patch", new Vector3(-2.8f, 0.05f, -2.1f), 0.16f);
            CreateLabel("Generated species", new Vector3(0.4f, 0.05f, -2.1f), 0.16f);

            FoliageAssetLibrary.EnsureFolder(OutputRoot);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SabaProps Foliage] Bundled demo created at " + ScenePath);
            return scene;
        }

        private static void CreateSpeciesGallery(Material material)
        {
            var kinds = new[]
            {
                FoliageSpeciesKind.GrassClump,
                FoliageSpeciesKind.SmallFlower,
                FoliageSpeciesKind.Dandelion,
            };
            for (int kindIndex = 0; kindIndex < kinds.Length; kindIndex++)
            {
                FoliageSpeciesKind kind = kinds[kindIndex];
                var species = ScriptableObject.CreateInstance<FoliageSpecies>();
                species.name = kind.ToString();
                species.kind = kind;
                species.material = material;
                species.meshSeed = 110 + kindIndex;
                string speciesPath = AssetFolder + "/" + kind + ".asset";
                AssetDatabase.CreateAsset(species, speciesPath);

                Mesh mesh = FoliageMeshBuilder.Build(species);
                mesh.name = kind + "Mesh";
                AssetDatabase.CreateAsset(mesh, AssetFolder + "/" + mesh.name + ".asset");
                species.generatedMesh = mesh;
                EditorUtility.SetDirty(species);

                for (int instance = 0; instance < 5; instance++)
                {
                    var item = new GameObject(kind + " " + (instance + 1));
                    item.transform.position = new Vector3(
                        -3.05f + kindIndex * 0.95f,
                        0f,
                        -1.45f + instance * 0.32f);
                    item.transform.rotation = Quaternion.Euler(
                        0f,
                        instance * 137.5f,
                        0f);
                    item.transform.localScale = Vector3.one
                        * (0.82f + instance * 0.07f);
                    item.AddComponent<MeshFilter>().sharedMesh = mesh;
                    item.AddComponent<MeshRenderer>().sharedMaterial = material;
                }
            }
        }

        private static void CreateVinePattern(
            string name,
            Collider primarySurface,
            Collider[] additionalSurfaces,
            Material material,
            VineDemoProfile profile,
            int seed,
            List<Vector3> guidePoints)
        {
            var gameObject = new GameObject(name);
            SurfaceVine vine = gameObject.AddComponent<SurfaceVine>();
            vine.targetSurface = primarySurface;
            vine.additionalSurfaces.AddRange(additionalSurfaces);
            vine.material = material;
            vine.growth.mode = SurfaceGrowthMode.ProjectedSpline;
            vine.growth.pathCount = profile == VineDemoProfile.BostonIvy ? 6 : 4;
            vine.growth.coverage = profile == VineDemoProfile.BostonIvy ? 0.72f : 0.58f;
            vine.growth.stepLength = 0.075f;
            vine.growth.maxPathLength = 4.4f;
            vine.growth.branchesPerMetre = profile == VineDemoProfile.BostonIvy ? 0.8f : 0.52f;
            vine.growth.rootSpread = 0.14f;
            vine.growth.guideAttraction = 0.64f;
            vine.growth.pathLengthVariance = 0.16f;
            vine.growth.directionJitter = 0.24f;
            vine.growth.projectionDistance = 0.42f;
            vine.growth.seed = seed;
            vine.guidePoints = guidePoints;
            if (profile == VineDemoProfile.BostonIvy)
            {
                vine.morphology.ApplyBostonIvyPreset();
                vine.morphology.autumnAmount = 0.06f;
            }
            else if (profile == VineDemoProfile.EnglishIvy)
            {
                vine.morphology.ApplyEnglishIvyPreset();
            }
            else
            {
                vine.morphology.ApplyCreepingFigPreset();
            }

            var projector = new SurfaceGrowthAuthoringBuilder.ColliderProjector(
                vine.transform,
                vine.targetSurface,
                vine.additionalSurfaces);
            SurfaceGrowthGraph graph = SurfaceGrowthGraphBuilder.Build(
                vine.growth,
                vine.guidePoints,
                projector.Project);
            Mesh mesh = SurfaceGrowthMeshBuilder.BuildVine(
                graph,
                vine.growth,
                vine.morphology);
            mesh.name = profile + "SurfaceMesh";
            AssetDatabase.CreateAsset(mesh, AssetFolder + "/" + mesh.name + ".asset");
            vine.generatedGraph = graph;
            vine.generatedMesh = mesh;
            gameObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            gameObject.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void CreateRhizomePatch(Collider ground, Material material)
        {
            var gameObject = new GameObject("Houttuynia Rhizome Patch");
            gameObject.transform.position = new Vector3(-1.5f, 0.01f, -0.85f);
            RhizomePatch patch = gameObject.AddComponent<RhizomePatch>();
            patch.targetSurface = ground;
            patch.material = material;
            patch.growth.mode = SurfaceGrowthMode.SurfaceCrawl;
            patch.growth.pathCount = 8;
            patch.growth.coverage = 0.78f;
            patch.growth.maxPathLength = 1.5f;
            patch.growth.stepLength = 0.12f;
            patch.growth.branchesPerMetre = 1.2f;
            patch.growth.maxBranchDepth = 2;
            patch.growth.seed = 1204;
            patch.morphology.accentAmount = 0.2f;
            patch.morphology.flowerChance = 0.24f;

            SurfaceGrowthGraph graph = SurfaceGrowthGraphBuilder.Build(
                patch.growth,
                patch.guidePoints,
                ProjectGround);
            Mesh mesh = SurfaceGrowthMeshBuilder.BuildRhizomePatch(
                graph,
                patch.growth,
                patch.morphology);
            mesh.name = "HouttuyniaRhizomePatchMesh";
            AssetDatabase.CreateAsset(mesh, AssetFolder + "/" + mesh.name + ".asset");
            patch.generatedGraph = graph;
            patch.generatedMesh = mesh;
            gameObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            gameObject.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static bool ProjectGround(
            Vector3 candidate,
            Vector3 normalHint,
            float maximumDistance,
            out SurfacePoint point)
        {
            candidate.y = 0f;
            point = new SurfacePoint(candidate, Vector3.up);
            return true;
        }

        private static Material CreateFoliageMaterial()
        {
            Shader shader = Shader.Find(FoliageShaderContract.ShaderName);
            if (shader == null)
            {
                return null;
            }
            var material = new Material(shader)
            {
                name = "FoliageDemo",
                enableInstancing = true,
            };
            material.SetFloat("_WindStrength", 0.08f);
            AssetDatabase.CreateAsset(material, AssetFolder + "/FoliageDemo.mat");
            return material;
        }

        private static Material CreateStandardMaterial(string name, Color colour)
        {
            var material = new Material(Shader.Find("Standard"))
            {
                name = name,
                color = colour,
            };
            material.SetFloat("_Glossiness", 0.04f);
            AssetDatabase.CreateAsset(material, AssetFolder + "/" + name + ".mat");
            return material;
        }

        private static GameObject CreatePrimitive(
            PrimitiveType type,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            GameObject gameObject = GameObject.CreatePrimitive(type);
            gameObject.name = name;
            gameObject.transform.position = position;
            gameObject.transform.localScale = scale;
            gameObject.GetComponent<MeshRenderer>().sharedMaterial = material;
            return gameObject;
        }

        private static void CreateLabel(string text, Vector3 position, float size)
        {
            var labelObject = new GameObject(text + " Label");
            labelObject.transform.position = position;
            labelObject.transform.rotation = Quaternion.identity;
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.fontSize = 64;
            label.characterSize = size * 0.14f;
            label.anchor = TextAnchor.LowerLeft;
            label.color = new Color(0.08f, 0.09f, 0.07f, 1f);
        }

        private static void ConfigureCameraAndLight()
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 2.5f, -7.4f);
                camera.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
                camera.farClipPlane = 80f;
            }

            Light light = Object.FindObjectOfType<Light>();
            if (light != null)
            {
                light.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
                light.color = new Color(1f, 0.96f, 0.87f, 1f);
                light.intensity = 1.15f;
                light.shadows = LightShadows.Soft;
            }
        }
    }
}
