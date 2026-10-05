using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SabaProps.Flock.Editors
{
    /// <summary>Real-scale species specimens and comparisons with one variable changed.</summary>
    public static class FlockComparisonScene
    {
        public const string Root = "Assets/SabaProps/FlockComparisons";
        public const string ScenePath = Root + "/FlockComparisons.unity";

        public static void GenerateForDistribution()
        {
            if (AssetDatabase.IsValidFolder(Root)) throw new InvalidOperationException("Use a clean project to generate comparisons.");
            FlockAssetLibrary.EnsureFolder(Root);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            UnityEngine.Object.DestroyImmediate(Camera.main.gameObject);
            var specimens = new GameObject("01 All species - real scale - select and press F");
            int index = 0;
            foreach (FlockPreset preset in FlockSpeciesCatalog.All)
            {
                Vector3 position = new Vector3((index % 10) * 6f - 27f, 1.5f, (index / 10) * 6f);
                FlockSwarm swarm = Create(preset.Species.id, specimens.transform, position,
                    FlockPattern.Anchored, 1, Vector3.one * preset.Species.bodyLength * 1.1f);
                swarm.gameObject.name = preset.Species.id + " - " + preset.Species.displayName;
                Label(swarm.transform, preset.Species.id + " / " + preset.Species.bodyLength.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " m",
                    new Vector3(0f, -Mathf.Max(preset.Species.bodyLength, 0.2f), 0f), 0.035f);
                index++;
            }

            var fish = new GameObject("02 Swimming comparison - sardine - same seed count area speed");
            FlockPattern[] swimming = { FlockPattern.Stream, FlockPattern.BaitBall, FlockPattern.Tornado, FlockPattern.Wander };
            for (int i = 0; i < swimming.Length; i++)
            {
                FlockSwarm swarm = Create("sardine", fish.transform, new Vector3((i - 1.5f) * 6f, 2f, 55f),
                    swimming[i], 24, new Vector3(2f, 1f, 2f));
                Label(swarm.transform, swimming[i].ToString(), new Vector3(0f, -1.6f, 0f), 0.06f);
            }
            var birds = new GameObject("03 Flying comparison - starling - same seed count area speed");
            FlockPattern[] flying = { FlockPattern.Cruise, FlockPattern.Murmuration, FlockPattern.VFormation, FlockPattern.Thermal };
            for (int i = 0; i < flying.Length; i++)
            {
                FlockSwarm swarm = Create("starling", birds.transform, new Vector3((i - 1.5f) * 6f, 2f, 80f),
                    flying[i], 24, new Vector3(2f, 1f, 2f));
                Label(swarm.transform, flying[i].ToString(), new Vector3(0f, -1.6f, 0f), 0.06f);
            }
            AddCamera("Comparison - all species", new Vector3(0f, 14f, -35f), new Vector3(0f, 1.5f, 18f), true);
            AddCamera("Comparison - swimming", new Vector3(0f, 2f, 34f), new Vector3(0f, 2f, 55f), false);
            AddCamera("Comparison - flying", new Vector3(0f, 2f, 59f), new Vector3(0f, 2f, 80f), false);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[SabaProps Flock] Species and movement comparisons created: " + ScenePath);
        }

        private static FlockSwarm Create(string id, Transform parent, Vector3 position, FlockPattern pattern, int count, Vector3 area)
        {
            var go = new GameObject("Flock " + id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            FlockSwarm swarm = go.AddComponent<FlockSwarm>();
            FlockSwarmBuilder.ApplyPreset(swarm, id);
            swarm.settings.pattern = pattern; swarm.settings.count = count; swarm.settings.area = area;
            swarm.settings.seed = 42; swarm.settings.clusterRadius = 0.6f;
            swarm.settings.lodMode = FlockLodMode.Single; swarm.settings.detail = FlockDetail.High;
            FlockSwarmBuilder.Rebuild(swarm);
            return swarm;
        }

        private static void Label(Transform parent, string text, Vector3 position, float size)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false); go.transform.localPosition = position;
            TextMesh mesh = go.AddComponent<TextMesh>();
            mesh.text = text; mesh.fontSize = 40; mesh.characterSize = size; mesh.anchor = TextAnchor.MiddleCenter;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.font = font; go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        private static void AddCamera(string name, Vector3 position, Vector3 target, bool enabled)
        {
            var go = new GameObject(name);
            go.transform.position = position; go.transform.rotation = Quaternion.LookRotation(target - position);
            Camera camera = go.AddComponent<Camera>(); camera.enabled = enabled;
            camera.farClipPlane = enabled ? 150f : 30f; camera.nearClipPlane = 0.01f; camera.fieldOfView = 60f;
            if (enabled) go.tag = "MainCamera";
        }

        [MenuItem("Tools/SabaProps/Flock/Sample View/Compare species")]
        public static void Species() { FlockWorldSample.SetView("Comparison - all species"); }
        [MenuItem("Tools/SabaProps/Flock/Sample View/Compare swimming")]
        public static void Swimming() { FlockWorldSample.SetView("Comparison - swimming"); }
        [MenuItem("Tools/SabaProps/Flock/Sample View/Compare flying")]
        public static void Flying() { FlockWorldSample.SetView("Comparison - flying"); }
    }
}
