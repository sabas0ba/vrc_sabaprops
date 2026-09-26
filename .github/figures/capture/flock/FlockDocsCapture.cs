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

        [MenuItem("Tools/SabaProps/Flock/Capture Docs Images", false, 200)]
        public static void Capture()
        {
            EditorSceneManager.OpenScene(FlockSampleScene.ScenePath);
            string destination = Path.Combine(Path.GetFullPath(PackagePath), OutputFolder);
            Directory.CreateDirectory(destination);

            foreach (Shot shot in Shots)
            {
                Render(shot, Path.Combine(destination, shot.Name + ".jpg"));
            }

            Debug.Log($"[SabaProps Flock] Captured {Shots.Length} docs images in {destination}");
        }

        private static void Render(Shot shot, string path)
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
                camera.farClipPlane = 200f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.08f, 0.12f, 0.18f);
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
