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

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single);
            Material treeMaterial = CreateTreeMaterial();
            Material groundMaterial = CreateGroundMaterial();
            ConfigureCameraAndLight();

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Tree Demo Ground";
            ground.transform.localScale = new Vector3(1.15f, 1f, 0.65f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;

            TreeArchetype[] archetypes =
            {
                TreeArchetype.Broadleaf,
                TreeArchetype.Conifer,
                TreeArchetype.Deadwood,
                TreeArchetype.DesertScrub,
            };
            for (int i = 0; i < archetypes.Length; i++)
            {
                CreateTree(archetypes[i], i, treeMaterial);
            }

            CreateLabel(
                "LOD0 / LOD1 / LOD2 are shared per species",
                new Vector3(-5.2f, 0.04f, -2.45f));

            TreeAssetLibrary.EnsureFolder(OutputRoot);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SabaProps Trees] Bundled demo created at " + ScenePath);
            return scene;
        }

        private static void CreateTree(
            TreeArchetype archetype,
            int index,
            Material material)
        {
            string displayName = TreeAssetLibrary.DisplayName(archetype);
            var species = ScriptableObject.CreateInstance<TreeSpecies>();
            species.name = displayName;
            species.ApplyArchetypePreset(archetype);
            // The user-facing sample demonstrates the same recursive generator
            // and LOD transitions without carrying release-scale branch budgets.
            species.structure.maxBranches = Mathf.Min(
                species.structure.maxBranches,
                192);
            species.appearance.leavesPerTip = Mathf.Min(
                species.appearance.leavesPerTip,
                6);
            species.material = material;
            species.meshSeed = 710 + index;
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
            root.transform.position = new Vector3(-4.2f + index * 2.8f, 0f, 0.35f);
            CreateLabel(displayName, root.transform.position + new Vector3(-0.65f, 0.02f, -1.55f));
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
            material.SetFloat("_WindStrength", 0.1f);
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
            labelObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            TextMesh label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.fontSize = 56;
            label.characterSize = 0.15f;
            label.anchor = TextAnchor.LowerLeft;
            label.color = new Color(0.08f, 0.09f, 0.07f, 1f);
        }

        private static void ConfigureCameraAndLight()
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 3.2f, -10.5f);
                camera.transform.rotation = Quaternion.Euler(9f, 0f, 0f);
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
