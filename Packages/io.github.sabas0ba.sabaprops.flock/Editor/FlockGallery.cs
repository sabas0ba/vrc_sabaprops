using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SabaProps.Flock.Editors
{
    /// <summary>
    /// Builds a scene that shows every preset twice: a labelled close-up row
    /// per habitat at High detail, and a few full-size flocks far away that
    /// switch through the LOD tiers and into silhouettes.
    /// </summary>
    public static class FlockGallery
    {
        public const string ScenePath = FlockAssetLibrary.RootFolder + "/FlockGallery.unity";

        /// <summary>Individuals per close-up swarm. Few enough to tell them apart.</summary>
        private const int CloseUpCount = 3;

        public static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            UnityEngine.SceneManagement.Scene scene =
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var root = new GameObject("Flock Gallery");
            var rows = new Dictionary<FlockHabitat, float>
            {
                { FlockHabitat.Sky, 0f },
                { FlockHabitat.Sea, 0f },
                { FlockHabitat.Reef, 0f },
                { FlockHabitat.Aquarium, 0f },
            };
            var rowParents = new Dictionary<FlockHabitat, Transform>();
            FlockHabitat[] order = { FlockHabitat.Sky, FlockHabitat.Sea, FlockHabitat.Reef, FlockHabitat.Aquarium };
            for (int i = 0; i < order.Length; i++)
            {
                var row = new GameObject($"Row {FlockSwarmEditor.HabitatLabel(order[i])}");
                row.transform.SetParent(root.transform, false);
                row.transform.localPosition = new Vector3(0f, 1.5f, 6f + 6f * i);
                rowParents[order[i]] = row.transform;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            foreach (FlockPreset preset in FlockSpeciesCatalog.All)
            {
                FlockSpecies s = preset.Species;
                float size = Mathf.Max(s.Span, s.bodyLength);
                float half = Mathf.Max(1.2f * size, 0.15f);
                float x = rows[preset.Habitat] + half;
                rows[preset.Habitat] = x + half + 0.6f;

                FlockSwarm swarm = FlockSwarmBuilder.Create(s.id, rowParents[preset.Habitat].gameObject, new Vector3(x, 0f, 0f));
                swarm.settings.pattern = FlockPattern.Wander;
                swarm.settings.count = CloseUpCount;
                swarm.settings.area = new Vector3(half, Mathf.Max(0.5f * half, 1.5f * s.bodyLength), half);
                swarm.settings.speedScale = 0.35f;
                swarm.settings.lodMode = FlockLodMode.Single;
                swarm.settings.detail = FlockDetail.High;
                FlockSwarmBuilder.Rebuild(swarm);

                AddLabel(swarm.transform, s.displayName, font, half);
            }

            // Centre each row on the origin so the camera sees all of them.
            foreach (FlockHabitat habitat in order)
            {
                Transform row = rowParents[habitat];
                row.localPosition -= new Vector3(0.5f * rows[habitat], 0f, 0f);
            }

            AddDistantFlocks(root.transform);
            FrameCamera();

            FlockAssetLibrary.EnsureFolder(FlockAssetLibrary.RootFolder);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = root;
            Debug.Log($"[SabaProps Flock] {FlockSpeciesCatalog.All.Count} 種のギャラリーを {ScenePath} に作成しました。");
        }

        private static void AddDistantFlocks(Transform parent)
        {
            var far = new GameObject("Distant Flocks");
            far.transform.SetParent(parent, false);
            FlockSwarmBuilder.Create("starling", far, new Vector3(0f, 70f, 260f));
            FlockSwarmBuilder.Create("goose", far, new Vector3(-180f, 90f, 320f));
            FlockSwarmBuilder.Create("black-kite", far, new Vector3(200f, 60f, 220f));
            FlockSwarmBuilder.Create("gull", far, new Vector3(-60f, 25f, 90f));
        }

        private static void AddLabel(Transform parent, string text, Font font, float half)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, -Mathf.Max(half, 0.3f) - 0.2f, -half);
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.UpperCenter;
            mesh.characterSize = 0.05f;
            mesh.fontSize = 48;
            mesh.color = Color.white;
            if (font != null)
            {
                mesh.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
        }

        private static void FrameCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            camera.transform.position = new Vector3(0f, 4f, -14f);
            camera.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
            camera.farClipPlane = 2000f;
        }
    }
}
