using System.IO;
using SabaProps.Trees.Editors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SabaProps.Trees.CITests
{
    /// <summary>Reproducible side views for manual crown and branch review.</summary>
    public static class TreeCrownReview
    {
        public static void AuditSeasonalScene()
        {
            EditorSceneManager.OpenScene(TreeBundledDemo.SeasonalScenePath);
            var trees = Object.FindObjectsOfType<LODGroup>();
            int overlaps = 0;
            foreach (var tree in trees)
            {
                string prefix = null;
                foreach (var filter in tree.GetComponentsInChildren<MeshFilter>())
                {
                    string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
                    string current = path.Substring(0, path.LastIndexOf("_LOD"));
                    if (prefix != null && prefix != current)
                        throw new System.InvalidOperationException("Mixed LOD species: " + tree.name);
                    prefix = current;
                }
            }
            for (int i = 0; i < trees.Length; i++)
            for (int j = i + 1; j < trees.Length; j++)
            {
                var first = trees[i].GetComponentInChildren<MeshFilter>();
                var second = trees[j].GetComponentInChildren<MeshFilter>();
                if (first.sharedMesh == second.sharedMesh) continue;
                if (!first.GetComponent<Renderer>().bounds.Intersects(
                    second.GetComponent<Renderer>().bounds)) continue;
                overlaps++;
                Debug.Log("[TreeAudit] Bounds overlap: " + trees[i].name + " / " + trees[j].name);
            }
            Debug.Log("[TreeAudit] Trees=" + trees.Length + "; mixed LOD references=0; different-preset bounds overlaps=" + overlaps);
        }

        public static void Capture()
        {
            TreeBundledDemo.GenerateForDistribution();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../CrownReview"));
            Directory.CreateDirectory(output);
            AuditSeasonalScene();
            Save(Camera.main, Path.Combine(output, "SeasonalTrees-overview.png"));
            foreach (TreeBotanicalPreset preset in new[] {
                TreeBotanicalPreset.SomeiYoshinoSpring,
                TreeBotanicalPreset.SomeiYoshinoSummer,
                TreeBotanicalPreset.GinkgoAutumn,
                TreeBotanicalPreset.JapaneseMaple,
                TreeBotanicalPreset.JapaneseWhiteBirch })
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var species = ScriptableObject.CreateInstance<TreeSpecies>();
                species.ApplyBotanicalPreset(preset);
                var material = new Material(TreeAssetLibrary.CreateOrLoadDefaultMaterial());
                material.SetFloat("_WindStrength", 0f);
                var tree = new GameObject(preset.ToString());
                var filter = tree.AddComponent<MeshFilter>();
                tree.AddComponent<MeshRenderer>().sharedMaterial = material;
                Mesh foliage = TreeMeshBuilder.Build(species, 0);
                filter.sharedMesh = foliage;
                Bounds bounds = foliage.bounds;
                var light = new GameObject("Review light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.transform.rotation = Quaternion.Euler(35f, -35f, 0f);
                RenderSettings.ambientLight = new Color(0.65f, 0.65f, 0.65f);
                var camera = new GameObject("Review camera").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.72f, 0.78f, 0.83f);
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x) * 1.12f;
                camera.transform.position = bounds.center + Vector3.back * 40f;
                camera.transform.LookAt(bounds.center);
                Save(camera, Path.Combine(output, preset + "-foliage.png"));
                float sideSize = camera.orthographicSize;
                camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.12f;
                camera.transform.position = bounds.center + Vector3.up * 40f;
                camera.transform.LookAt(bounds.center, Vector3.forward);
                Save(camera, Path.Combine(output, preset + "-top.png"));
                camera.orthographicSize = sideSize;
                camera.transform.position = bounds.center + Vector3.back * 40f;
                camera.transform.LookAt(bounds.center);
                species.appearance.leafShape = TreeLeafShape.None;
                // Removing foliage changes the bounds correction. Match the
                // root-ring scale so both views show the same skeleton size.
                Mesh bark = TreeMeshBuilder.Build(species, 0);
                float trunkRatio = foliage.vertices[0].magnitude /
                    Mathf.Max(1e-6f, bark.vertices[0].magnitude);
                tree.transform.localScale = Vector3.one * trunkRatio;
                filter.sharedMesh = bark;
                Save(camera, Path.Combine(output, preset + "-branches.png"));
                Vector3 junctionTarget = new Vector3(0f,
                    bounds.min.y + bounds.size.y * 0.42f, 0f);
                camera.orthographicSize = bounds.size.y * 0.18f;
                camera.transform.position = junctionTarget + Vector3.back * 40f;
                camera.transform.LookAt(junctionTarget);
                Save(camera, Path.Combine(output, preset + "-junction.png"));
                Object.DestroyImmediate(foliage);
                Object.DestroyImmediate(bark);
                Object.DestroyImmediate(species);
                Object.DestroyImmediate(material);
                Debug.Log("[CrownReview] " + preset + " captured in " + output);
            }
        }

        private static void Save(Camera camera, string path)
        {
            var target = new RenderTexture(900, 900, 24);
            var image = new Texture2D(900, 900, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
            }
        }
    }
}
