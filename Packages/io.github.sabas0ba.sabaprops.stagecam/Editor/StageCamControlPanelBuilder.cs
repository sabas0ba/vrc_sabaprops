using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VRC.SDK3.Components;

namespace SabaProps.StageCam.Editors
{
    /// <summary>既存のリグを操作する World Space Canvas を生成します。</summary>
    public static class StageCamControlPanelBuilder
    {
        [MenuItem("GameObject/SabaProps/Stage Camera Control Panel", false, 11)]
        public static void CreateFromMenu(MenuCommand command)
        {
            StageCamRig[] rigs = Object.FindObjectsOfType<StageCamRig>();
            StageCamControlPanel panel = Create(rigs);
            GameObjectUtility.SetParentAndAlign(panel.gameObject, command.context as GameObject);
            // 親に合わせる処理の後も、Canvas の寸法と撮影除外レイヤーを維持します。
            panel.transform.localScale = Vector3.one * 0.0018f;
            panel.gameObject.layer = StageCamAssets.ScreenLayer;
            Undo.RegisterCreatedObjectUndo(panel.gameObject, "Create Stage Camera Control Panel");
            Selection.activeGameObject = panel.gameObject;
        }

        public static StageCamControlPanel Create(StageCamRig[] rigs)
        {
            var root = new GameObject("Stage Camera Control Panel", typeof(RectTransform));
            root.layer = StageCamAssets.ScreenLayer;
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(1000f, 1080f);
            rect.localScale = Vector3.one * 0.0018f;

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.AddComponent<CanvasScaler>();
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<VRCUiShape>();
            var background = root.AddComponent<Image>();
            background.color = new Color(0.035f, 0.05f, 0.075f, 1f);

            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }

            StageCamControlPanel panel = root.AddUdonSharpComponent<StageCamControlPanel>();
            panel.rigs = rigs;

            Label(root.transform, "Title", "STAGE CAM  /  LOCAL CONTROL", 28f, 22f, 944f, 46f, 30);
            panel.cameraLabel = Label(root.transform, "Camera", "", 28f, 76f, 680f, 54f, 27);
            Button(panel, "Previous camera", "< CAM", "_PreviousCamera", 724f, 76f, 118f);
            Button(panel, "Next camera", "CAM >", "_NextCamera", 854f, 76f, 118f);

            RectTransform previewRect = Element(root.transform, "Preview", 28f, 148f, 560f, 315f);
            panel.preview = previewRect.gameObject.AddComponent<RawImage>();
            panel.preview.raycastTarget = false;
            panel.targetLabel = Label(root.transform, "Tracking", "", 612f, 148f, 360f, 105f, 24);
            Label(root.transform, "Preview help", "PREVIEW\n\nChanges apply to the selected camera on your client.",
                612f, 270f, 360f, 170f, 23);

            panel.playerLabel = Label(root.transform, "Player", "", 28f, 478f, 680f, 54f, 24);
            // 表示名をリッチテキストとして解釈させません。
            panel.playerLabel.supportRichText = false;
            panel.targetLabel.supportRichText = false;
            panel.cameraLabel.supportRichText = false;
            Button(panel, "Previous player", "< PLAYER", "_PreviousPlayer", 724f, 478f, 118f);
            Button(panel, "Next player", "PLAYER >", "_NextPlayer", 854f, 478f, 118f);
            Button(panel, "Track selected player", "TRACK SELECTED", "_ApplyPlayer", 28f, 546f, 302f);
            Button(panel, "Track self", "TRACK ME", "_TargetSelf", 342f, 546f, 302f);
            Button(panel, "Stop following", "STOP FOLLOW", "_StopFollowing", 656f, 546f, 316f);

            panel.settingsLabel = Label(root.transform, "Settings", "", 28f, 614f, 944f, 105f, 24);
            Button(panel, "Previous subject", "< SUBJECT", "_PreviousSubject", 28f, 738f, 224f);
            Button(panel, "Next subject", "SUBJECT >", "_NextSubject", 264f, 738f, 224f);
            Button(panel, "Reference mode", "BODY / WORLD", "_ToggleFollowMode", 500f, 738f, 224f);
            Button(panel, "Auto framing", "AUTO FRAME", "_ToggleAutoFraming", 736f, 738f, 236f);

            Button(panel, "Yaw left", "YAW -", "_YawLeft", 28f, 806f, 145f);
            Button(panel, "Yaw right", "YAW +", "_YawRight", 185f, 806f, 145f);
            Button(panel, "Pitch down", "PITCH -", "_PitchDown", 342f, 806f, 145f);
            Button(panel, "Pitch up", "PITCH +", "_PitchUp", 499f, 806f, 145f);
            Button(panel, "Closer", "CLOSER", "_Closer", 656f, 806f, 152f);
            Button(panel, "Further", "FURTHER", "_Further", 820f, 806f, 152f);
            Button(panel, "Camera work", "CAMERA WORK ON / OFF", "_ToggleCameraWork", 28f, 874f, 460f);
            Label(root.transform, "Framing help", "Auto frame ON: closer / further adjusts frame coverage.",
                512f, 874f, 460f, 60f, 21);
            Label(root.transform, "Runtime help", "LOCAL CONTROL: your changes are not shared.\nPickup-enabled cameras can also be adjusted by hand.",
                28f, 962f, 944f, 78f, 22);

            panel.RefreshView();
            UdonSharpEditorUtility.CopyProxyToUdon(panel);
            EditorUtility.SetDirty(panel);
            return panel;
        }

        private static RectTransform Element(Transform parent, string name, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = StageCamAssets.ScreenLayer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static Text Label(Transform parent, string name, string value, float x, float y, float width, float height, int size)
        {
            var text = Element(parent, name, x, y, width, height).gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = new Color(0.9f, 0.94f, 1f, 1f);
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        private static void Button(StageCamControlPanel panel, string name, string caption, string eventName, float x, float y, float width)
        {
            RectTransform rect = Element(panel.transform, name, x, y, width, 56f);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.22f, 0.32f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Text label = Label(rect, "Label", caption, 4f, 0f, width - 8f, 56f, width < 150f ? 19 : 22);
            label.alignment = TextAnchor.MiddleCenter;

            // 実行時は UdonBehaviour が受信します。Editor 専用のプロキシを参照しません。
            var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(panel);
            UnityEventTools.AddStringPersistentListener(button.onClick, backing.SendCustomEvent, eventName);
        }
    }
}
