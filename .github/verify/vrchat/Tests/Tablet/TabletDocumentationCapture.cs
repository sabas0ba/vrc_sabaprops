using System;
using System.IO;
using System.Runtime.InteropServices;
using SabaProps.Tablet.Authoring;
using SabaProps.Tablet.Editors;
using TMPro;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SabaProps.Tablet.WorldTests
{
    /// <summary>実際のシーンと Editor ウィンドウから文書用画像を撮影します。</summary>
    public static class TabletDocumentationCapture
    {
        private static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "../TestResults/Documentation"));
        private static TabletDefinition definition;
        private static EditorWindow window;
        private static int step;
        private static double captureAt;
        private static bool raised;
        private static double captureDeadline;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string className, string windowName);
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr handle, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
        [StructLayout(LayoutKind.Sequential)]
        private struct ScreenPoint { public int x; public int y; }
        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(ScreenPoint point);
        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr handle, uint flags);

        public static void RunInspectors()
        {
            RequireWindows();
            Directory.CreateDirectory(Folder);
            TabletSampleScene.Create();
            definition = Object.FindObjectOfType<TabletDefinition>();
            step = 2;
            OpenNextWindow();
            EditorApplication.update += Update;
        }

        public static void Run()
        {
            RequireWindows();
            Directory.CreateDirectory(Folder);
            TabletThemeGallery.Create();
            RenderWorld("world-gallery.png", new Vector3(-0.75f, 8f, -14f), new Vector3(-0.75f, 1f, 0f));
            TabletSampleScene.Create();
            definition = Object.FindObjectOfType<TabletDefinition>();
            TabletController controller = definition.GetComponent<TabletController>();
            foreach (TabletToggle toggle in definition.GetComponentsInChildren<TabletToggle>(true))
                foreach (TabletButton button in toggle.buttons) button.SetLit(toggle.isOn);
            for (int i = 0; i < controller.pages.Length; i++)
            {
                controller.tabletArgument = i;
                controller._ShowPage();
                RenderTablet(controller, "page-" + controller.pageTitles[i].ToLowerInvariant().Replace(" ", "-") + ".png");
            }
            controller.tabletArgument = 0;
            controller._ShowPage();
            CaptureBedLayout();
            step = 0;
            OpenNextWindow();
            EditorApplication.update += Update;
        }

        private static void OpenNextWindow()
        {
            if (window != null) window.Close();
            switch (step)
            {
                case 0:
                    window = EditorWindow.GetWindow<TabletSetupWindow>(true, "Tablet Setup", true);
                    TabletSetupWindow.Open(definition);
                    break;
                case 1:
                    Selection.activeGameObject = definition.gameObject;
                    window = EditorWindow.GetWindow<TabletThemeWindow>(true, "Tablet Themes", true);
                    TabletThemeWindow.Open();
                    break;
                case 2:
                    var definitionInspector = ScriptableObject.CreateInstance<TabletInspectorCaptureWindow>();
                    definitionInspector.target = definition;
                    window = definitionInspector;
                    break;
                case 3:
                    var themeInspector = ScriptableObject.CreateInstance<TabletInspectorCaptureWindow>();
                    themeInspector.target = TabletThemePresets.Load(6);
                    window = themeInspector;
                    break;
                default:
                    EditorApplication.update -= Update;
                    Debug.Log("[SabaProps Tablet] Documentation screenshots saved: " + Folder);
                    EditorApplication.Exit(0);
                    return;
            }
            window.titleContent = new GUIContent("Tablet Documentation " + step);
            window.ShowUtility();
            window.position = new Rect(60f, 60f, 720f, 850f);
            window.Focus();
            window.Repaint();
            captureAt = EditorApplication.timeSinceStartup + 3f;
            raised = false;
            captureDeadline = captureAt + 30f;
        }

        private static void Update()
        {
            if (EditorApplication.timeSinceStartup < captureAt) return;
            try
            {
                if (!raised)
                {
                    IntPtr handle = FindWindow(null, window.titleContent.text);
                    if (handle == IntPtr.Zero) throw new InvalidOperationException("Screenshot window is unavailable: " + window.titleContent.text);
                    // 撮影するウィンドウだけを一時的に最前面へ置きます。閉じると設定も解除されます。
                    if (!SetWindowPos(handle, new IntPtr(-1), 0, 0, 0, 0, 0x0013))
                        throw new InvalidOperationException("Could not bring the screenshot window to the front.");
                    window.Repaint();
                    raised = true;
                    captureAt = EditorApplication.timeSinceStartup + 1f;
                    return;
                }
                string[] names = { "setup-window.png", "theme-window.png", "tablet-inspector.png", "theme-inspector.png" };
                Rect rect = window.position;
                IntPtr expected = GetAncestor(FindWindow(null, window.titleContent.text), 2);
                foreach (Vector2 point in new[] { rect.center, rect.position + Vector2.one * 20f, rect.position + rect.size - Vector2.one * 20f })
                {
                    IntPtr visible = GetAncestor(WindowFromPoint(new ScreenPoint { x = Mathf.RoundToInt(point.x), y = Mathf.RoundToInt(point.y) }), 2);
                    if (visible == expected) continue;
                    if (EditorApplication.timeSinceStartup > captureDeadline)
                        throw new InvalidOperationException("The screenshot window remains obscured: " + window.titleContent.text);
                    SetWindowPos(expected, new IntPtr(-1), 0, 0, 0, 0, 0x0013);
                    window.Focus();
                    captureAt = EditorApplication.timeSinceStartup + 1f;
                    return;
                }
                int width = Mathf.RoundToInt(rect.width);
                int height = Mathf.RoundToInt(rect.height);
                Color[] pixels = InternalEditorUtility.ReadScreenPixel(rect.position, width, height);
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.SetPixels(pixels);
                image.Apply();
                File.WriteAllBytes(Path.Combine(Folder, names[step]), image.EncodeToPNG());
                Object.DestroyImmediate(image);
                step++;
                OpenNextWindow();
            }
            catch (Exception exception)
            {
                EditorApplication.update -= Update;
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void RequireWindows()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
                throw new PlatformNotSupportedException("Editor window screenshots require Windows Unity Editor.");
        }

        private static void RenderTablet(TabletController controller, string file)
        {
            var go = new GameObject("Documentation camera");
            Camera camera = go.AddComponent<Camera>();
            camera.transform.position = controller.body.position - controller.body.forward * 0.7f;
            camera.transform.rotation = Quaternion.LookRotation(controller.body.forward, controller.body.up);
            camera.orthographic = true;
            camera.orthographicSize = 0.16f;
            camera.cullingMask = 1 << 23;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.10f, 0.12f, 0.15f);
            Transform[] children = controller.body.GetComponentsInChildren<Transform>(true);
            int[] layers = new int[children.Length];
            for (int i = 0; i < children.Length; i++)
            {
                layers[i] = children[i].gameObject.layer;
                children[i].gameObject.layer = 23;
            }
            try { Render(camera, file); }
            finally
            {
                for (int i = 0; i < children.Length; i++) children[i].gameObject.layer = layers[i];
            }
            Object.DestroyImmediate(go);
        }

        private static void RenderWorld(string file, Vector3 position, Vector3 lookAt)
        {
            var go = new GameObject("Documentation world camera");
            Camera camera = go.AddComponent<Camera>();
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation(lookAt - position);
            camera.fieldOfView = 42f;
            Render(camera, file);
            Object.DestroyImmediate(go);
        }

        private static void CaptureBedLayout()
        {
            foreach (TabletPage page in definition.pages)
            {
                if (page.title != "Bed Mirrors") continue;
                for (int i = 0; i < 5; i++)
                {
                    GameObject mirror = page.entries[i].objects[0];
                    mirror.GetComponent<VRC.SDK3.Components.VRCMirrorReflection>().enabled = false;
                    var tint = new Color(0.25f, 0.65f, 0.80f, 0.22f);
                    Material material = TabletAssets.ColorMaterial(null, tint, 0.1f, "Layout preview");
                    mirror.GetComponent<Renderer>().sharedMaterial = material;
                    mirror.SetActive(true);
                }
            }
            // 配置確認用に立方体の辺を描きます。裏面から見えないミラーの位置も示します。
            var edges = new GameObject("Bed layout edges");
            var edgeMaterial = new Material(Shader.Find("Unlit/Color"));
            edgeMaterial.color = new Color(0.08f, 0.32f, 0.42f);
            for (int corner = 0; corner < 8; corner++)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    int next = corner ^ (1 << axis);
                    if (next < corner) continue;
                    var go = new GameObject("Cube edge");
                    go.transform.SetParent(edges.transform, false);
                    LineRenderer line = go.AddComponent<LineRenderer>();
                    line.positionCount = 2;
                    line.SetPositions(new[] { CubeCorner(corner), CubeCorner(next) });
                    line.startWidth = line.endWidth = 0.012f;
                    line.sharedMaterial = edgeMaterial;
                }
            }
            RenderWorld("bed-cube-layout.png", new Vector3(10.5f, 5f, -2f), new Vector3(6f, 1.2f, 3f));
            Object.DestroyImmediate(edgeMaterial);
            // 配置説明用の一時素材を保存せず、設定の撮影には元のシーンを生成し直します。
            TabletSampleScene.Create();
            definition = Object.FindObjectOfType<TabletDefinition>();
        }

        private static Vector3 CubeCorner(int index)
        {
            return new Vector3(4.5f + (index & 1) * 3f, ((index >> 1) & 1) * 3f,
                1.5f + ((index >> 2) & 1) * 3f);
        }

        private static void Render(Camera camera, string file)
        {
            foreach (TextMeshPro label in Object.FindObjectsOfType<TextMeshPro>())
                label.ForceMeshUpdate();
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(1600, 1000, 24);
            Texture2D image = null;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0f, 0f, 1600f, 1000f), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(Folder, file), image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(target);
                if (image != null) Object.DestroyImmediate(image);
            }
        }
    }

    /// <summary>既存の Inspector を移動せず、同じ Inspector GUI を専用ウィンドウに表示します。</summary>
    public class TabletInspectorCaptureWindow : EditorWindow
    {
        public Object target;
        private Editor inspector;
        private Vector2 scroll;

        private void OnGUI()
        {
            if (target == null) return;
            if (inspector == null) inspector = Editor.CreateEditor(target);
            EditorGUILayout.LabelField(target.GetType().Name, EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            inspector.OnInspectorGUI();
            EditorGUILayout.EndScrollView();
        }

        private void OnDisable()
        {
            if (inspector != null) Object.DestroyImmediate(inspector);
        }
    }
}
