using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Capture.Editors
{
    /// <summary>Recorder と再生パネルをシーンへ置くメニュー。</summary>
    public static class CaptureMenu
    {
        [MenuItem("GameObject/SabaProps/Capture Recorder", false, 20)]
        public static void CreateRecorder(MenuCommand command)
        {
            CaptureRecorder recorder = CreateRecorderObject();
            GameObjectUtility.SetParentAndAlign(recorder.gameObject, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(recorder.gameObject, "Create Capture Recorder");
            Selection.activeGameObject = recorder.gameObject;

            Debug.Log(
                "[SabaProps Capture] Recorder を作成しました。"
                + "Source Texture に撮影元の RenderTexture を、または Source Camera にタイムラプス用の Camera を割り当ててください。");
        }

        [MenuItem("GameObject/SabaProps/Capture Recorder with Playback Panel", false, 21)]
        public static void CreateRecorderWithPanel(MenuCommand command)
        {
            var root = new GameObject("Capture");
            CaptureRecorder recorder = CreateRecorderObject();
            recorder.transform.SetParent(root.transform, false);

            CapturePlayer player = CapturePanelBuilder.Create(recorder);
            player.transform.SetParent(root.transform, false);
            player.transform.localPosition = new Vector3(0f, 1.4f, 0f);

            GameObjectUtility.SetParentAndAlign(root, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(root, "Create Capture Recorder with Playback Panel");
            Selection.activeGameObject = recorder.gameObject;
        }

        [MenuItem("Tools/SabaProps/Capture/Documentation", false, 100)]
        public static void OpenDocumentation()
        {
            Application.OpenURL(
                "https://github.com/sabas0ba/vrc_sabaprops/blob/main/Packages/io.github.sabas0ba.sabaprops.capture/README.md");
        }

        private static CaptureRecorder CreateRecorderObject()
        {
            var go = new GameObject("Capture Recorder");
            CaptureRecorder recorder = go.AddUdonSharpComponent<CaptureRecorder>();
            UdonSharpEditorUtility.CopyProxyToUdon(recorder);
            return recorder;
        }
    }
}
