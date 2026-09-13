using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SabaProps.SoftProps.Editors
{
    public static class SoftPropsDemo
    {
        public const string PackageRoot = "Packages/io.github.sabas0ba.sabaprops.softprops";
        public const string SampleRoot = PackageRoot + "/Samples~/ReviewDemo";
        public const string ImportRoot = "Assets/SabaProps/SoftPropsDemoMotion";
        public const string SceneName = "SoftPropsDemo.unity";
        public const string GeneratedScene = SoftPropGenerator.OutputRoot + "/" + SceneName;

        [MenuItem("Tools/SabaProps/Soft Props/Open Demo Scene", false, 0)]
        public static void OpenDemo()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ImportSample();
            EditorSceneManager.OpenScene(ImportRoot + "/" + SceneName);
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.LookAt(new Vector3(0f, 0.7f, 1f),
                    Quaternion.Euler(30f, 0f, 0f), 9f);
        }

        public static void ImportSample()
        {
            // 既存demoを開く場合も、旧版のdemo-owned programを共有先へ移す。
            string sharedProgram = SoftPropsVrcBridge.EnsureSharedControllerProgram();
            SoftPropsVrcBridge.CompileControllerProgram(sharedProgram);
            // 再実行で利用者が編集したサンプルを上書きしない。
            if (File.Exists(ImportRoot + "/" + SceneName)) return;
            if (Directory.Exists(ImportRoot))
            {
                if (Directory.GetFileSystemEntries(ImportRoot).Length != 0)
                    throw new IOException("サンプル導入先が既に存在します: " + ImportRoot);
                Directory.Delete(ImportRoot);
            }
            Directory.CreateDirectory("Assets/SabaProps");
            FileUtil.CopyFileOrDirectory(SampleRoot, ImportRoot);
            var importGuids = new Dictionary<string, string>();
            foreach (string meta in Directory.GetFiles(ImportRoot, "*.meta", SearchOption.AllDirectories))
            {
                string guid = Regex.Match(File.ReadAllText(meta), @"guid: ([a-f0-9]{32})").Groups[1].Value;
                if (guid.Length == 32) importGuids[guid] = Guid.NewGuid().ToString("N");
            }
            foreach (string path in Directory.GetFiles(ImportRoot, "*", SearchOption.AllDirectories))
                CopySampleFile(path, path, importGuids);
            string existingProgram = SoftPropsVrcBridge.FindControllerProgram();
            if (existingProgram != null)
            {
                // Refresh前に複製programへの参照を既存programへ接続する。
                // UdonSharpは1 scriptにつき1 program assetのみを許容する。
                string importedProgram = ImportRoot + "/SoftSurfaceContactController.asset";
                string importedGuid = Regex.Match(File.ReadAllText(importedProgram + ".meta"),
                    @"guid: ([a-f0-9]{32})").Groups[1].Value;
                string existingGuid = AssetDatabase.AssetPathToGUID(existingProgram);
                foreach (string path in Directory.GetFiles(ImportRoot, "*", SearchOption.AllDirectories))
                {
                    if (!path.EndsWith(".prefab", StringComparison.Ordinal)
                        && !path.EndsWith(".unity", StringComparison.Ordinal)) continue;
                    File.WriteAllText(path, File.ReadAllText(path).Replace(importedGuid, existingGuid), new UTF8Encoding(false));
                }
                File.Delete(importedProgram);
                File.Delete(importedProgram + ".meta");
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        public static void GenerateSample()
        {
            Generate();
            var files = new List<string>();
            foreach (string path in AssetDatabase.GetDependencies(GeneratedScene, true))
            {
                if (!path.StartsWith("Assets/", StringComparison.Ordinal) || Directory.Exists(path)) continue;
                if (!path.StartsWith(SoftPropGenerator.OutputRoot + "/", StringComparison.Ordinal)
                    && !path.StartsWith(SoftPropsVrcBridge.SharedRoot + "/", StringComparison.Ordinal)
                    && !path.StartsWith("Assets/SerializedUdonPrograms/", StringComparison.Ordinal))
                    throw new IOException("Unexpected sample dependency: " + path);
                files.Add(path);
            }
            var replacements = new Dictionary<string, string>();
            foreach (string path in files)
            {
                string destination = SampleDestination(path);
                string guid = File.Exists(destination + ".meta")
                    ? Regex.Match(File.ReadAllText(destination + ".meta"), @"guid: ([a-f0-9]{32})").Groups[1].Value
                    : Guid.NewGuid().ToString("N");
                replacements.Add(AssetDatabase.AssetPathToGUID(path), guid);
            }
            foreach (string path in files)
            {
                string destination = SampleDestination(path);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                CopySampleFile(path, destination, replacements);
                CopySampleFile(path + ".meta", destination + ".meta", replacements);
            }
            Debug.Log("[SabaProps Soft Props] Bundled demo exported: " + files.Count + " assets");
        }

        private static string SampleDestination(string path)
        {
            if (path == SoftPropsVrcBridge.SharedProgramPath)
                return SampleRoot + "/SoftSurfaceContactController.asset";
            return path.StartsWith(SoftPropGenerator.OutputRoot + "/", StringComparison.Ordinal)
                ? SampleRoot + path.Substring(SoftPropGenerator.OutputRoot.Length)
                : SampleRoot + "/Programs/SoftSurfaceContactController.asset";
        }

        private static void CopySampleFile(string source, string destination, Dictionary<string, string> replacements)
        {
            byte[] bytes = File.ReadAllBytes(source);
            string text = Encoding.UTF8.GetString(bytes);
            if (text.StartsWith("%YAML", StringComparison.Ordinal) || source.EndsWith(".meta", StringComparison.Ordinal))
            {
                text = Regex.Replace(text, @"\b[a-f0-9]{32}\b", match =>
                    replacements.TryGetValue(match.Value, out string replacement) ? replacement : match.Value);
                text = Regex.Replace(text.Replace("\r\n", "\n"), @"[ \t]+$", "", RegexOptions.Multiline);
                File.WriteAllText(destination, text, new UTF8Encoding(false));
            }
            else File.WriteAllBytes(destination, bytes);
        }

        // 配布用シーンの再生成は独立した検証projectで実行する。
        public static void Generate()
        {
            SoftPropGenerator.GenerateAll();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.60f);
            var floor = Material("DemoFloor", new Color(0.25f, 0.29f, 0.31f));
            var table = Material("DemoTable", new Color(0.66f, 0.68f, 0.66f));
            var signage = Material("DemoSignage", new Color(0.08f, 0.12f, 0.14f));
            Box("Floor", new Vector3(0f, -0.1f, 1f), new Vector3(12f, 0.2f, 12f), floor);
            Box("Back wall", new Vector3(0f, 1.5f, 6.8f), new Vector3(12f, 3f, 0.15f), table);

            var sun = new GameObject("Key Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 6f, -9f);
            camera.transform.LookAt(new Vector3(0f, 0.7f, 1f));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.18f, 0.22f, 0.25f);
            camera.nearClipPlane = 0.03f;
            camera.fieldOfView = 45f;
            camera.farClipPlane = 60f;
            camera.gameObject.AddComponent<AudioListener>();
            SoftPropsVrcBridge.AddWorldDescriptor(camera);

            Place("Futon", new Vector3(-3f, 0f, 2.3f));
            Place("Bed", new Vector3(2.8f, 0f, 2.3f));
            Place("Sofa", new Vector3(0f, 0f, 4.7f));
            Place("Cushion", new Vector3(-1.1f, 0f, 2.3f));
            Label("FUTON / soft, slow recovery", new Vector3(-3f, 0.65f, 3.6f), 0.055f);
            Label("BED / firmer, fast recovery", new Vector3(2.8f, 1.4f, 3.6f), 0.055f);
            Label("SOFA / seats + back cushions", new Vector3(0f, 1.65f, 5.1f), 0.055f);
            Label("CUSHION", new Vector3(-1.1f, 0.45f, 2.8f), 0.045f);

            Box("Probe table", new Vector3(-2.1f, 0.38f, -0.8f), new Vector3(2.1f, 0.76f, 1.05f), table);
            var probes = Place("ContactProbeTest", new Vector3(-2.1f, 0.76f, -0.8f));
            // サンプルのラベルは正面（spawn側）から読める向きに統一する。
            foreach (TextMesh label in probes.GetComponentsInChildren<TextMesh>())
            {
                label.transform.rotation = Quaternion.identity;
                label.transform.localScale = Vector3.one * 0.45f;
            }
            Label("LIVE CONTACT TEST", new Vector3(-2.1f, 1.7f, -0.25f), 0.075f);
            Box("Live instructions", new Vector3(-2.1f, 1.52f, -0.19f), new Vector3(2.08f, 0.62f, 0.05f), signage);
            Label("VRChat Build & Test: pick up a probe\nTouch the skin, then lift to check recovery.\nVisual deformation only; colliders stay fixed.",
                new Vector3(-2.1f, 1.43f, -0.25f), 0.036f);

            Box("Preview table", new Vector3(2f, 0.38f, -0.8f), new Vector3(2.65f, 0.76f, 1.05f), table);
            Label("STATIC SHAPE REFERENCE", new Vector3(2f, 1.7f, -0.25f), 0.075f);
            Box("Reference instructions", new Vector3(2f, 1.52f, -0.19f), new Vector3(2.62f, 0.62f, 0.05f), signage);
            Label("Fixed shader inputs / visible without Play\nSame material, mesh and load; different footprints.",
                new Vector3(2f, 1.43f, -0.25f), 0.036f);
            Preview("Finger", 1.15f, new Vector4(1f, 0f, 0f, 0.055f));
            Preview("Rod", 2f, new Vector4(1f, 0f, 0.22f, 0.045f));
            Preview("Plate", 2.85f, new Vector4(1f, 0f, 0.20f, -0.12f));
            for (int profile = 0; profile < 3; profile++)
            {
                float x = (profile - 1) * 3f;
                float hardness = profile == 0 ? 0.15f : profile == 1 ? 0.45f : 0.80f;
                float recovery = profile == 0 ? 0.8f : profile == 1 ? 0.4f : 0.12f;
                Box("Automatic table " + profile, new Vector3(x, 0.38f, -3f), new Vector3(2.1f, 0.76f, 1f), table);
                var comparison = Place("ContactProbeTest", new Vector3(x, 0.76f, -3f));
                comparison.name = "Automatic comparison " + profile;
                var controller = comparison.GetComponentInChildren<SoftSurfaceContactController>();
                controller.automaticProbe = true;
                controller.hardness = hardness;
                controller.recoverySeconds = recovery;
                controller.pressDepth = controller.maximumIndent * Mathf.Lerp(1f, 0.28f, hardness) * 0.65f;
                foreach (Rigidbody body in comparison.GetComponentsInChildren<Rigidbody>())
                {
                    body.isKinematic = true;
                    body.useGravity = false;
                    foreach (Component component in body.GetComponents<Component>())
                        if (component.GetType().Name == "VRCPickup" || component.GetType().Name == "VRCObjectSync"
                            || component.GetType().Name == "VRCContactSender")
                            UnityEngine.Object.DestroyImmediate(component);
                }
                foreach (TextMesh label in comparison.GetComponentsInChildren<TextMesh>())
                {
                    label.transform.rotation = Quaternion.identity;
                    label.transform.localScale = Vector3.one * 0.4f;
                }
                Label("AUTO / hardness " + hardness + " / recovery " + recovery + "s",
                    new Vector3(x, 1.65f, -2.45f), 0.052f);
                Label("Prescribed motion / hardness-scaled travel\nPlay: approach - press - hold - release", new Vector3(x, 1.43f, -2.45f), 0.032f);
                controller.statusLabel = StatusLabel(new Vector3(x, 1.05f, -3.55f));
            }
            SoftPropsVrcBridge.CompileControllerProgram(SoftPropGenerator.ProgramAssetPath);
            Label("SABA PROPS / SOFT SURFACES", new Vector3(0f, 2.4f, 6.65f), 0.12f);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, GeneratedScene))
                throw new IOException("Demo sceneを保存できませんでした。");
            Capture(camera, "demo-overview.png");
            camera.fieldOfView = 60f;
            camera.transform.position = new Vector3(2f, 2f, -2.1f);
            camera.transform.LookAt(new Vector3(2f, 0.85f, -0.8f));
            Capture(camera, "demo-footprints.png");
            // 保存済みのoverview cameraは変更しない。
            EditorSceneManager.OpenScene(GeneratedScene);
        }

        private static void Preview(string name, float x, Vector4 shape)
        {
            var surface = new GameObject("Static " + name);
            surface.transform.position = new Vector3(x, 0.86f, -0.8f);
            Mesh mesh = SoftPropMeshBuilder.BuildRoundedBox("Reference " + name,
                new Vector3(0.76f, 0.16f, 0.65f), 0.05f, 76, 65);
            AssetDatabase.CreateAsset(mesh, SoftPropGenerator.MeshFolder + "/Reference" + name + ".asset");
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            var source = AssetDatabase.LoadAssetAtPath<Material>(SoftPropGenerator.MaterialFolder + "/DefaultSkinMatte.mat");
            var material = new Material(source) { name = "Reference " + name };
            material.SetFloat("_Hardness", 0.30f);
            material.SetFloat("_RimLift", 0.004f);
            material.SetVector("_Contact0", new Vector4(0f, 0.08f, 0f, 0.7f));
            material.SetVector("_ContactShape0", shape);
            AssetDatabase.CreateAsset(material, SoftPropGenerator.MaterialFolder + "/Reference" + name + ".mat");
            surface.AddComponent<MeshRenderer>().sharedMaterial = material;
            Label(name, new Vector3(x, 1.05f, -0.42f), 0.045f);
        }

        private static GameObject Place(string name, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SoftPropGenerator.PrefabFolder + "/" + name + ".prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = position;
            return instance;
        }

        private static Material Material(string name, Color color)
        {
            var material = new Material(Shader.Find("Standard")) { name = name };
            material.color = color;
            material.SetFloat("_Glossiness", 0.08f);
            AssetDatabase.CreateAsset(material, SoftPropGenerator.MaterialFolder + "/" + name + ".mat");
            return material;
        }

        private static void Box(string name, Vector3 position, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = position;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static TextMesh Label(string text, Vector3 position, float size)
        {
            var label = new GameObject(text.Split('\n')[0]).AddComponent<TextMesh>();
            label.transform.position = position;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<Renderer>().sharedMaterial = label.font.material;
            label.text = text;
            label.fontSize = 64;
            label.characterSize = size * 0.4f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = new Color(0.93f, 0.95f, 0.96f);
            return label;
        }

        private static UnityEngine.UI.Text StatusLabel(Vector3 position)
        {
            var canvas = new GameObject("Probe status", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.transform.position = position;
            canvas.transform.localScale = Vector3.one * 0.001f;
            var label = new GameObject("Status", typeof(RectTransform), typeof(UnityEngine.UI.Text))
                .GetComponent<UnityEngine.UI.Text>();
            label.transform.SetParent(canvas.transform, false);
            label.rectTransform.sizeDelta = new Vector2(1900f, 120f);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 32;
            label.alignment = TextAnchor.MiddleCenter;
            label.text = "Gap / compression";
            label.raycastTarget = false;
            return label;
        }

        private static void Capture(Camera camera, string name)
        {
            var target = new RenderTexture(1600, 1000, 24);
            var texture = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0);
                texture.Apply();
                Directory.CreateDirectory("ReviewCaptures");
                File.WriteAllBytes("ReviewCaptures/" + name, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
