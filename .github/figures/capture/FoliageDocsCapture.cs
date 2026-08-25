using System.Collections.Generic;
using System.IO;
using SabaProps.Foliage.Editors;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Foliage.DocsCapture
{
    /// <summary>
    /// Renders the screenshots the documentation uses for "what it looks like",
    /// from the sample scene, with the real shader and real lighting.
    /// <para>
    /// The parameter figures beside these are generated without Unity (see
    /// .github/figures/), because their job is to compare shapes and that has
    /// to run in CI. This is the other half: the part no offline renderer can
    /// stand in for -- wind, translucency, shadows, and a field of thousands
    /// rather than one plant on a white plate.
    /// </para>
    /// <para>
    /// Deliberately not part of the shipped package: it exists to produce files
    /// that are committed once, and every user of the package would otherwise
    /// carry it. See README.md in this folder for how to run it.
    /// </para>
    /// </summary>
    public static class FoliageDocsCapture
    {
        private const string PackagePath = "Packages/io.github.sabas0ba.sabaprops.foliage";
        private const string OutputFolder = "Documentation~/images/captured";

        private const int Width = 1600;
        private const int Height = 900;

        /// <summary>
        /// Where the camera sits relative to what it frames. Fixed so that a
        /// re-capture after a change to the package differs only by the change.
        /// </summary>
        private static readonly Vector3 ViewDirection = new Vector3(0.35f, 0.42f, -1f);

        private struct Shot
        {
            public string Name;

            /// <summary>Section root to frame, or null for the whole garden.</summary>
            public string Root;

            /// <summary>How much wider than the framed bounds to pull back.</summary>
            public float Margin;
        }

        private static readonly Shot[] Shots =
        {
            new Shot { Name = "sample-scene", Root = null, Margin = 1.05f },
            new Shot { Name = "single-species", Root = FoliageSampleScene.SingleSpeciesRoot, Margin = 1.15f },
            new Shot { Name = "terrain", Root = FoliageSampleScene.TerrainRoot, Margin = 1.15f },
            new Shot { Name = "output-modes", Root = FoliageSampleScene.OutputRoot, Margin = 1.2f },
        };

        [MenuItem("Tools/SabaProps/Foliage/Capture Docs Images", false, 200)]
        public static void Capture()
        {
            string destination = Path.Combine(Path.GetFullPath(PackagePath), OutputFolder);
            Directory.CreateDirectory(destination);

            FoliageSampleScene.Create();

            var written = new List<string>();
            foreach (Shot shot in Shots)
            {
                string path = Path.Combine(destination, shot.Name + ".jpg");
                if (Render(shot, path))
                {
                    written.Add(path);
                }
            }

            Debug.Log(
                $"SabaProps Foliage: captured {written.Count} image(s) into {destination}\n"
                + string.Join("\n", written));
        }

        private static bool Render(Shot shot, string path)
        {
            if (!TryFrame(shot, out Bounds bounds))
            {
                Debug.LogWarning($"SabaProps Foliage: nothing to capture for '{shot.Name}'");
                return false;
            }

            var holder = new GameObject("SabaProps Docs Capture");
            var camera = holder.AddComponent<Camera>();

            // Multisampling matters more here than anywhere else in the package:
            // foliage is thousands of blade edges, and aliasing on them is the
            // first thing a reader notices.
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 8,
            };

            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;

            try
            {
                camera.fieldOfView = 42f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 400f;
                camera.allowMSAA = true;

                Camera main = Camera.main;
                if (main != null)
                {
                    camera.clearFlags = main.clearFlags;
                    camera.backgroundColor = main.backgroundColor;
                }

                Place(camera, bounds, shot.Margin);

                camera.targetTexture = target;
                camera.Render();

                RenderTexture.active = target;
                image.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                image.Apply();

                File.WriteAllBytes(path, image.EncodeToJPG(90));
                return true;
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

        /// <summary>Bounds of everything the shot should contain.</summary>
        private static bool TryFrame(Shot shot, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;

            foreach (Renderer renderer in Object.FindObjectsOfType<Renderer>())
            {
                if (shot.Root != null && !IsUnder(renderer.transform, shot.Root))
                {
                    continue;
                }

                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return any;
        }

        private static bool IsUnder(Transform transform, string rootName)
        {
            for (Transform current = transform; current != null; current = current.parent)
            {
                if (current.name == rootName)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Place(Camera camera, Bounds bounds, float margin)
        {
            // Far enough back that the framed bounds fit the narrower of the two
            // field of view angles, which for a 16:9 frame is the vertical one.
            float radius = bounds.extents.magnitude * margin;
            float distance = radius / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);

            Vector3 direction = ViewDirection.normalized;
            camera.transform.position = bounds.center + direction * distance;
            camera.transform.rotation = Quaternion.LookRotation(-direction, Vector3.up);
        }
    }
}
