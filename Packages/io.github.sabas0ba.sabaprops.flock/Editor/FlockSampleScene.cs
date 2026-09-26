using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace SabaProps.Flock.Editors
{
    /// <summary>
    /// Generates the compact, editable scene distributed in Samples~. Run this
    /// in a clean project when refreshing the bundled scene and its mesh assets.
    /// </summary>
    public static class FlockSampleScene
    {
        public const string OutputRoot = "Assets/SabaProps/FlockSample";
        public const string ScenePath = OutputRoot + "/FlockSample.unity";

        private struct Example
        {
            public string Preset;
            public FlockPattern Pattern;
            public int Count;
            public Vector3 Area;
        }

        private static readonly Example[][] Rows =
        {
            new[]
            {
                new Example { Preset = "starling", Pattern = FlockPattern.Murmuration, Count = 18, Area = new Vector3(2.8f, 1.4f, 2f) },
                new Example { Preset = "goose", Pattern = FlockPattern.VFormation, Count = 7, Area = new Vector3(3.6f, 1.5f, 2.5f) },
            },
            new[]
            {
                new Example { Preset = "black-kite", Pattern = FlockPattern.Thermal, Count = 5, Area = new Vector3(2.5f, 2f, 2.5f) },
                new Example { Preset = "gull", Pattern = FlockPattern.Cruise, Count = 8, Area = new Vector3(3f, 1.5f, 2f) },
            },
            new[]
            {
                new Example { Preset = "sardine", Pattern = FlockPattern.BaitBall, Count = 18, Area = new Vector3(1.8f, 1.2f, 1.8f) },
                new Example { Preset = "mackerel", Pattern = FlockPattern.Stream, Count = 12, Area = new Vector3(2.5f, 1.2f, 2f) },
            },
            new[]
            {
                new Example { Preset = "anthias", Pattern = FlockPattern.Tornado, Count = 12, Area = new Vector3(1.8f, 1.5f, 1.8f) },
                new Example { Preset = "neon-tetra", Pattern = FlockPattern.Wander, Count = 10, Area = new Vector3(1.5f, 0.8f, 1.2f) },
            },
        };

        private static readonly string[] RowNames =
        {
            "Sky formations", "Sky cruising", "Sea schools", "Reef and aquarium",
        };

        private static readonly Color[] RowColors =
        {
            new Color(0.57f, 0.74f, 0.85f),
            new Color(0.44f, 0.64f, 0.77f),
            new Color(0.06f, 0.23f, 0.32f),
            new Color(0.06f, 0.17f, 0.23f),
        };

        /// <summary>Prompt-free entry point for refreshing the bundled sample.</summary>
        public static void GenerateForDistribution()
        {
            if (AssetDatabase.IsValidFolder(OutputRoot))
            {
                throw new InvalidOperationException(
                    $"{OutputRoot} already exists. Use a clean project to regenerate the sample.");
            }

            FlockAssetLibrary.EnsureFolder(OutputRoot);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            ConfigureCameraAndLight();

            var root = new GameObject("SabaProps Flock Sample");
            var swarms = new GameObject("Copy These Swarms");
            swarms.transform.SetParent(root.transform, false);
            var presentation = new GameObject("Presentation Only");
            presentation.transform.SetParent(root.transform, false);

            for (int row = 0; row < Rows.Length; row++)
            {
                float y = 18f - row * 6f;
                var section = new GameObject(RowNames[row]);
                section.transform.SetParent(swarms.transform, false);
                section.transform.localPosition = new Vector3(0f, y, 0f);

                CreateBackdrop(presentation.transform, row, y);
                for (int column = 0; column < Rows[row].Length; column++)
                {
                    Example example = Rows[row][column];
                    float x = column == 0 ? -7f : 7f;
                    FlockSwarm swarm = FlockSwarmBuilder.Create(
                        example.Preset, section, new Vector3(x, 0f, 0f));
                    swarm.settings.pattern = example.Pattern;
                    swarm.settings.count = example.Count;
                    swarm.settings.area = example.Area;
                    swarm.settings.seed = 100 + row * 2 + column;
                    swarm.settings.lodMode = FlockLodMode.Single;
                    swarm.settings.detail = FlockDetail.High;
                    FlockSwarmBuilder.Rebuild(swarm);
                    AddLabel(presentation.transform, swarm.species.displayName + " / " + example.Pattern,
                        new Vector3(x, y - 2.15f, -0.2f), row < 2 ? Color.black : Color.white);
                }
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = swarms;
            Debug.Log($"[SabaProps Flock] Sample scene created: {ScenePath}");
        }

        private static void ConfigureCameraAndLight()
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 9f, -34f);
                camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 9f, 0f) - camera.transform.position);
                camera.fieldOfView = 45f;
                camera.farClipPlane = 200f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.08f, 0.12f, 0.18f);
            }

            foreach (Light light in UnityEngine.Object.FindObjectsOfType<Light>())
            {
                if (light.type == LightType.Directional)
                {
                    light.transform.rotation = Quaternion.Euler(35f, -25f, 0f);
                    light.intensity = 1.25f;
                }
            }
        }

        private static void CreateBackdrop(Transform parent, int row, float y)
        {
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = RowNames[row] + " backdrop";
            panel.transform.SetParent(parent, false);
            panel.transform.localPosition = new Vector3(0f, y, 4f);
            panel.transform.localScale = new Vector3(28f, 5.5f, 0.1f);
            UnityEngine.Object.DestroyImmediate(panel.GetComponent<Collider>());

            Material material = new Material(Shader.Find("Unlit/Color"))
            {
                name = "Backdrop " + row,
                color = RowColors[row],
            };
            string path = OutputRoot + "/Backdrop" + row + ".mat";
            AssetDatabase.CreateAsset(material, path);
            MeshRenderer renderer = panel.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void AddLabel(Transform parent, string label, Vector3 position, Color color)
        {
            var go = new GameObject("Label " + label);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var text = go.AddComponent<TextMesh>();
            text.text = label;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.045f;
            text.fontSize = 48;
            text.color = color;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                text.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
        }
    }
}
