using System.IO;
using SabaProps.Flock.Editors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SabaProps.Flock.DocsCapture
{
    /// <summary>Captures the bundled sample with the actual Unity shader.</summary>
    public static class FlockDocsCapture
    {
        private const string PackagePath = "Packages/io.github.sabas0ba.sabaprops.flock";
        private const string OutputFolder = "Documentation~/images/captured";
        private const int Width = 1600;
        private const int Height = 900;

        private struct Shot
        {
            public string Name;
            public Vector3 Position;
            public Vector3 Target;
            public float FieldOfView;
        }

        private static readonly Shot[] Shots =
        {
            new Shot { Name = "sample-overview", Position = new Vector3(0f, 9f, -34f), Target = new Vector3(0f, 9f, 0f), FieldOfView = 45f },
            new Shot { Name = "starling", Position = new Vector3(-7f, 18f, -5.8f), Target = new Vector3(-7f, 18f, 0f), FieldOfView = 38f },
            new Shot { Name = "goose", Position = new Vector3(7f, 18f, -7f), Target = new Vector3(7f, 18f, 0f), FieldOfView = 38f },
            new Shot { Name = "sardine", Position = new Vector3(-7f, 6f, -4.8f), Target = new Vector3(-7f, 6f, 0f), FieldOfView = 38f },
            new Shot { Name = "anthias", Position = new Vector3(-7f, 0f, -4.8f), Target = new Vector3(-7f, 0f, 0f), FieldOfView = 38f },
        };

        private static readonly Shot[] WorldShots =
        {
            new Shot { Name = "world-sky", Position = new Vector3(0f, 1.65f, -42f), Target = new Vector3(0f, 15f, 18f), FieldOfView = 60f },
            new Shot { Name = "world-small-tank", Position = new Vector3(120f, 1.65f, -1.1f), Target = new Vector3(120f, 1.15f, 0f), FieldOfView = 60f },
            new Shot { Name = "world-small-tank-close", Position = new Vector3(120f, 1.2f, -0.55f), Target = new Vector3(120f, 1.15f, 0f), FieldOfView = 60f },
            new Shot { Name = "world-large-tank", Position = new Vector3(150f, 1.65f, -6.5f), Target = new Vector3(150f, 2.1f, 0f), FieldOfView = 60f },
            new Shot { Name = "world-river", Position = new Vector3(203f, 1.65f, -4.8f), Target = new Vector3(202f, -0.6f, 0f), FieldOfView = 60f },
            new Shot { Name = "world-river-bridge", Position = new Vector3(191f, 2.1f, -1f), Target = new Vector3(200f, -0.6f, 0f), FieldOfView = 60f },
        };

        [MenuItem("Tools/SabaProps/Flock/Capture Docs Images", false, 200)]
        public static void Capture()
        {
            EditorSceneManager.OpenScene(FlockSampleScene.ScenePath);
            CaptureShots(Shots, false);
        }

        [MenuItem("Tools/SabaProps/Flock/Capture World Situations", false, 201)]
        public static void CaptureWorld()
        {
            EditorSceneManager.OpenScene(FlockWorldSample.ScenePath);
            CaptureShots(WorldShots, true);
        }

        public static void CaptureExpanded()
        {
            Capture();
            EditorSceneManager.OpenScene(FlockWorldSample.ScenePath);
            FlockLightingPreview.Day(); CaptureShots(WorldShots, false, true);
            FlockLightingPreview.Evening();
            CaptureShots(new[] { Rename(WorldShots[0], "world-sky-evening"), Rename(WorldShots[3], "world-tank-evening") }, false, true);
            FlockLightingPreview.Night();
            CaptureShots(new[] { Rename(WorldShots[0], "world-sky-night"), Rename(WorldShots[3], "world-tank-night") }, false, true);
            EditorSceneManager.OpenScene(FlockComparisonScene.ScenePath);
            FlockLightingPreview.Day();
            CaptureShots(new[]
            {
                new Shot { Name = "compare-swimming", Position = new Vector3(0f, 2f, 34f), Target = new Vector3(0f, 2f, 55f), FieldOfView = 60f },
                new Shot { Name = "compare-flying", Position = new Vector3(0f, 2f, 59f), Target = new Vector3(0f, 2f, 80f), FieldOfView = 60f },
            }, false, true);
            foreach (string id in new[] { "squid", "octopus", "jellyfish", "garden-eel", "crab", "eel", "urchin", "anemone", "oyster", "flying-fish", "seahorse", "chicken", "chick" })
            {
                foreach (FlockSwarm swarm in Object.FindObjectsOfType<FlockSwarm>())
                {
                    if (swarm.presetId != id || swarm.settings.pattern != FlockPattern.Anchored) continue;
                    var visible = new System.Collections.Generic.List<Renderer>();
                    foreach (Renderer renderer in Object.FindObjectsOfType<Renderer>())
                    {
                        if (!renderer.enabled) continue;
                        visible.Add(renderer);
                        renderer.enabled = renderer.GetComponent<MeshFilter>() != null
                            && renderer.transform.IsChildOf(swarm.transform)
                            && renderer.sharedMaterial != null
                            && renderer.sharedMaterial.shader.name == "SabaProps/Flock/Swarm";
                    }
                    float length = swarm.species.bodyLength;
                    Vector3 target = swarm.transform.position + Vector3.up * (swarm.species.grounded ? length * 0.5f : 0f);
                    CaptureShots(new[] { new Shot { Name = "species-" + id, Target = target,
                        Position = target + new Vector3(length * 2.4f, length * 0.6f, length), FieldOfView = 38f } }, false, true);
                    foreach (Renderer renderer in visible) renderer.enabled = true;
                }
            }
        }

        private static Shot Rename(Shot original, string name)
        {
            original.Name = name;
            return original;
        }

        private static void CaptureShots(Shot[] shots, bool skybox, bool sceneBackground = false)
        {
            string destination = Path.Combine(Path.GetFullPath(PackagePath), OutputFolder);
            Directory.CreateDirectory(destination);

            foreach (Shot shot in shots)
            {
                Render(shot, Path.Combine(destination, shot.Name + ".jpg"), skybox, sceneBackground);
            }

            Debug.Log($"[SabaProps Flock] Captured {shots.Length} docs images in {destination}");
        }

        private static void Render(Shot shot, string path, bool skybox, bool sceneBackground)
        {
            var holder = new GameObject("Flock Docs Capture");
            Camera camera = holder.AddComponent<Camera>();
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4,
            };
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;

            try
            {
                camera.transform.position = shot.Position;
                camera.transform.rotation = Quaternion.LookRotation(shot.Target - shot.Position);
                camera.fieldOfView = shot.FieldOfView;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = shot.Name.StartsWith("compare-") ? 30f : 200f;
                camera.clearFlags = skybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                camera.backgroundColor = sceneBackground ? RenderSettings.ambientLight : new Color(0.08f, 0.12f, 0.18f);
                camera.allowMSAA = true;
                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;
                image.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToJPG(90));
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Object.DestroyImmediate(holder);
                Object.DestroyImmediate(image);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}
