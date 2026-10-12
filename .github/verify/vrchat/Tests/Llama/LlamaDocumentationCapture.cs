using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SabaProps.Llama.WorldTests
{
    /// <summary>実際の Import ウィンドウから文書用画像を撮影します。</summary>
    public static class LlamaDocumentationCapture
    {
        private const string Title = "SabaProps Llama Documentation";
        private static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "../TestResults/Documentation"));
        private static EditorWindow window;
        private static string modelFolder;
        private static double captureAt, captureDeadline;
        private static bool raised;

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

        /// <summary>
        /// -executeMethod の入口です。-batchmode は指定しません。
        /// <see cref="LlamaWorldRunTests.GgufVariable"/> が GGUF を指していればそのモデル、
        /// なければ未学習の検証用モデルを変換済みモデルとして表示します。
        /// </summary>
        public static void Run()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
                throw new PlatformNotSupportedException("Editor window screenshots require Windows Unity Editor.");
            Directory.CreateDirectory(Folder);
            string path = Environment.GetEnvironmentVariable(LlamaWorldRunTests.GgufVariable);
            LlamaModelAsset model;
            if (string.IsNullOrEmpty(path))
            {
                model = LlamaProgramBuilder.Build(LlamaCheckpoint.Fixture(), LlamaTokenizer.Fixture(), 8);
                model.tokenizerSha256 = "generated";
            }
            else
            {
                LlamaGguf gguf = LlamaGguf.Read(path);
                model = LlamaProgramBuilder.Build(gguf.checkpoint, gguf.tokenizer, 64);
                model.sourceFormat = "gguf-llama";
                model.tokenizerSha256 = gguf.checkpoint.sha256;
            }
            model.sourceAndLicense = "documentation capture";
            modelFolder = LlamaImportWindow.Save(model, "DocumentationCapture");

            // 既存レイアウトへドッキングさせないよう、先に独立ウィンドウとして開きます。
            EditorWindow.GetWindow<LlamaImportWindow>(true, Title, true);
            window = LlamaImportWindow.Open(model);
            window.titleContent = new GUIContent(Title);
            window.ShowUtility();
            window.position = new Rect(60f, 60f, 560f, 470f);
            window.Focus();
            window.Repaint();
            captureAt = EditorApplication.timeSinceStartup + 3f;
            captureDeadline = captureAt + 30f;
            raised = false;
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            if (EditorApplication.timeSinceStartup < captureAt) return;
            try
            {
                IntPtr handle = FindWindow(null, Title);
                if (handle == IntPtr.Zero) throw new InvalidOperationException("Screenshot window is unavailable.");
                if (!raised)
                {
                    // 撮影するウィンドウだけを一時的に最前面へ置きます。閉じると設定も解除されます。
                    if (!SetWindowPos(handle, new IntPtr(-1), 0, 0, 0, 0, 0x0013))
                        throw new InvalidOperationException("Could not bring the screenshot window to the front.");
                    window.Repaint();
                    raised = true;
                    captureAt = EditorApplication.timeSinceStartup + 1f;
                    return;
                }
                Rect rect = window.position;
                IntPtr expected = GetAncestor(handle, 2);
                foreach (Vector2 point in new[] { rect.center, rect.position + Vector2.one * 20f, rect.position + rect.size - Vector2.one * 20f })
                {
                    IntPtr visible = GetAncestor(WindowFromPoint(new ScreenPoint { x = Mathf.RoundToInt(point.x), y = Mathf.RoundToInt(point.y) }), 2);
                    if (visible == expected) continue;
                    if (EditorApplication.timeSinceStartup > captureDeadline)
                        throw new InvalidOperationException("The screenshot window remains obscured.");
                    SetWindowPos(expected, new IntPtr(-1), 0, 0, 0, 0, 0x0013);
                    window.Focus();
                    captureAt = EditorApplication.timeSinceStartup + 1f;
                    return;
                }
                int width = Mathf.RoundToInt(rect.width), height = Mathf.RoundToInt(rect.height);
                Color[] pixels = InternalEditorUtility.ReadScreenPixel(rect.position, width, height);
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.SetPixels(pixels);
                image.Apply();
                File.WriteAllBytes(Path.Combine(Folder, "import-window.png"), image.EncodeToPNG());
                Object.DestroyImmediate(image);
                Finish(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Finish(1);
            }
        }

        private static void Finish(int code)
        {
            EditorApplication.update -= Update;
            if (window != null) window.Close();
            if (modelFolder != null) AssetDatabase.DeleteAsset(modelFolder);
            if (code == 0) Debug.Log("[SabaProps Llama] Documentation screenshot saved: " + Folder);
            EditorApplication.Exit(code);
        }
    }
}
