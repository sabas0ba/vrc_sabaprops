using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VRC.SDK3.Components;

namespace SabaProps.Capture.Editors
{
    /// <summary>
    /// Recorder の撮影操作と、保持している画像の再生を行う World Space Canvas を生成します。
    /// </summary>
    public static class CapturePanelBuilder
    {
        /// <summary>サムネイルの数。タイムラインの幅を等分します。</summary>
        public const int ThumbnailCount = 8;

        /// <summary>
        /// パネルを置くレイヤー。ワールド内のカメラから外すため、Stage Cam のスクリーンと同じ
        /// Water (4) を使います。撮影したパネル自体が次の画像に映り込むのを防ぎます。
        /// </summary>
        public const int PanelLayer = 4;

        [MenuItem("GameObject/SabaProps/Capture Playback Panel", false, 22)]
        public static void CreateFromMenu(MenuCommand command)
        {
            CaptureRecorder recorder = Object.FindObjectOfType<CaptureRecorder>();
            CapturePlayer player = Create(recorder);
            GameObjectUtility.SetParentAndAlign(player.gameObject, command.context as GameObject);
            // 親に合わせる処理の後も、Canvas の寸法とレイヤーを維持します。
            player.transform.localScale = Vector3.one * 0.0015f;
            player.gameObject.layer = PanelLayer;
            Undo.RegisterCreatedObjectUndo(player.gameObject, "Create Capture Playback Panel");
            Selection.activeGameObject = player.gameObject;
        }

        public static CapturePlayer Create(CaptureRecorder recorder)
        {
            var root = new GameObject("Capture Playback Panel", typeof(RectTransform));
            root.layer = PanelLayer;
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(1280f, 1000f);
            rect.localScale = Vector3.one * 0.0015f;

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

            CapturePlayer player = root.AddUdonSharpComponent<CapturePlayer>();
            player.recorder = recorder;

            Label(root.transform, "Title", "CAPTURE  /  LOCAL TIMELINE", 28f, 18f, 1224f, 46f, 30);

            // 16:9 のプレビュー。
            RectTransform displayRect = Element(root.transform, "Display", 28f, 76f, 800f, 450f);
            player.display = displayRect.gameObject.AddComponent<RawImage>();
            player.display.raycastTarget = false;
            player.display.color = Color.white;

            player.statusLabel = Label(root.transform, "Status", "", 852f, 76f, 400f, 150f, 24);
            player.positionLabel = Label(root.transform, "Position", "", 852f, 238f, 400f, 56f, 28);
            Label(root.transform, "Help",
                "Images are kept in local VRAM only.\nThey are not shared and are lost when you leave.",
                852f, 306f, 400f, 110f, 20);

            var thumbnails = new RawImage[ThumbnailCount];
            const float stripX = 28f;
            const float stripWidth = 1224f;
            const float gap = 8f;
            float thumbWidth = (stripWidth - gap * (ThumbnailCount - 1)) / ThumbnailCount;
            float thumbHeight = thumbWidth * 9f / 16f;
            for (int i = 0; i < ThumbnailCount; i++)
            {
                RectTransform thumbRect = Element(root.transform, "Thumbnail " + i,
                    stripX + i * (thumbWidth + gap), 546f, thumbWidth, thumbHeight);
                RawImage thumbnail = thumbRect.gameObject.AddComponent<RawImage>();
                thumbnail.raycastTarget = false;
                thumbnail.enabled = false;
                thumbnails[i] = thumbnail;
            }

            player.thumbnails = thumbnails;
            player.timeline = Timeline(player, stripX, 546f + thumbHeight + 16f, stripWidth);

            float row = 546f + thumbHeight + 76f;
            Button(player, "First", "FIRST", "_First", 28f, row, 110f);
            Button(player, "Step backward", "<", "_StepBackward", 150f, row, 110f);
            Button(player, "Play", "PLAY / PAUSE", "_TogglePlay", 272f, row, 260f);
            Button(player, "Step forward", ">", "_StepForward", 544f, row, 110f);
            Button(player, "Latest", "LATEST", "_Latest", 666f, row, 200f);
            Button(player, "Slower", "SLOWER", "_Slower", 878f, row, 180f);
            Button(player, "Faster", "FASTER", "_Faster", 1070f, row, 182f);

            row += 72f;
            if (recorder != null)
            {
                Button(recorder, root.transform, "Record", "REC / PAUSE", "_ToggleRecording", 28f, row, 300f);
                Button(recorder, root.transform, "Capture now", "SHOT", "_CaptureNow", 340f, row, 200f);
                Button(recorder, root.transform, "Clear", "CLEAR", "_Clear", 552f, row, 200f);
                Button(recorder, root.transform, "Release", "RELEASE VRAM", "_ReleaseFrames", 764f, row, 488f);
            }

            UdonSharpEditorUtility.CopyProxyToUdon(player);
            EditorUtility.SetDirty(player);
            return player;
        }

        private static Slider Timeline(CapturePlayer player, float x, float y, float width)
        {
            GameObject go = DefaultControls.CreateSlider(new DefaultControls.Resources());
            go.name = "Timeline";
            SetLayerRecursively(go, PanelLayer);

            var rect = (RectTransform)go.transform;
            rect.SetParent(player.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, 44f);

            var slider = go.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = 1f;

            // 実行時は UdonBehaviour が受信します。Editor 専用のプロキシを参照しません。
            var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(player);
            UnityEventTools.AddStringPersistentListener(slider.onValueChanged, backing.SendCustomEvent, "_OnScrub");
            return slider;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static RectTransform Element(Transform parent, string name, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = PanelLayer;
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
            text.supportRichText = false;
            text.text = value;
            return text;
        }

        private static void Button(CapturePlayer player, string name, string caption, string eventName, float x, float y, float width)
        {
            Button(player, player.transform, name, caption, eventName, x, y, width);
        }

        private static void Button(UdonSharpBehaviour receiver, Transform parent, string name, string caption, string eventName,
            float x, float y, float width)
        {
            RectTransform rect = Element(parent, name, x, y, width, 56f);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.22f, 0.32f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Text label = Label(rect, "Label", caption, 4f, 0f, width - 8f, 56f, 22);
            label.alignment = TextAnchor.MiddleCenter;

            var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(receiver);
            UnityEventTools.AddStringPersistentListener(button.onClick, backing.SendCustomEvent, eventName);
        }
    }
}
