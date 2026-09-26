using System.Collections.Generic;
using System.IO;
using SabaProps.Tablet.Authoring;
using TMPro;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SabaProps.Tablet.Editors
{
    /// <summary>通常の World デモと、操作可能なテーマ展示を生成します。</summary>
    public static class TabletThemeGallery
    {
        public const string ScenePath = TabletAssets.SampleFolder + "/TabletThemeGallery.unity";
        public const string DisplayPrefix = "Theme Display - ";
        private const int PreviewLayer = 23;

        [MenuItem("Tools/SabaProps/Tablet/Create Theme Gallery", false, 3)]
        public static void CreateAndOpen()
        {
            if (Application.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Create();
        }

        public static void Create()
        {
            var scene = TabletSampleScene.Create();
            TabletDefinition original = Object.FindObjectOfType<TabletDefinition>();
            var displays = new List<TabletController>();
            for (int i = 0; i < TabletThemePresets.Files.Length; i++)
            {
                TabletTheme theme = TabletThemePresets.Load(i);
                if (theme == null) throw new System.InvalidOperationException("Theme asset is missing: " + TabletThemePresets.Files[i]);
                TabletDefinition definition = Object.Instantiate(original.gameObject).GetComponent<TabletDefinition>();
                definition.gameObject.name = DisplayPrefix + theme.name;
                definition.transform.position = new Vector3(-6.5f + i % 3 * 1.5f, 1.25f, -4f + i / 3 * 2f);
                definition.keyTrigger = false;
                definition.reachTrigger = false;
                definition.startVisible = true;
                definition.interactItems.Clear();
                definition.generatedFolder = TabletAssets.GeneratedFolder + "/ThemeGallery/" + TabletThemePresets.Files[i];

                GameObject stand = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stand.name = "Theme Stand - " + theme.name;
                stand.transform.position = definition.transform.position + Vector3.down * 0.8f;
                stand.transform.localScale = new Vector3(0.5f, 0.9f, 0.3f);
                definition.interactItems.Add(stand);
                TabletThemePresets.Apply(definition, theme);
                TabletController controller = definition.GetComponent<TabletController>();
                controller.autoStowDistance = 0f;
                UdonSharpEditorUtility.CopyProxyToUdon(controller);
                ShareToggles(original.GetComponent<TabletController>(), controller);
                Caption(controller, theme.name);
                StandLabel(stand, controller, theme.name);
                displays.Add(controller);
            }
            // 撮影だけを別カメラと一時配置で行い、World のカメラと動作設定を保持します。
            RenderComparison(displays);
            Selection.activeGameObject = original.gameObject;
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[SabaProps Tablet] Functional theme gallery saved: " + ScenePath);
        }

        private static void ShareToggles(TabletController original, TabletController display)
        {
            var shared = new Dictionary<string, TabletToggle>();
            foreach (TabletButton button in original.buttons)
                if (button.target is TabletToggle toggle) shared[toggle.gameObject.name] = toggle;
            var removed = new HashSet<GameObject>();
            foreach (TabletButton button in display.buttons)
            {
                if (!(button.target is TabletToggle generated) || !shared.TryGetValue(generated.gameObject.name, out TabletToggle target)) continue;
                removed.Add(generated.gameObject);
                button.target = target;
                var buttons = new List<TabletButton>(target.buttons) { button };
                target.buttons = buttons.ToArray();
                button.SetLit(target.isOn);
                UdonSharpEditorUtility.CopyProxyToUdon(button);
                UdonSharpEditorUtility.CopyProxyToUdon(target);
            }
            foreach (GameObject module in removed) Object.DestroyImmediate(module);
        }

        private static void Caption(TabletController controller, string title)
        {
            var go = new GameObject("Theme Name", typeof(RectTransform));
            go.transform.SetParent(controller.body, false);
            go.transform.localPosition = new Vector3(0f, 0.18f, -0.02f);
            TextMeshPro caption = go.AddComponent<TextMeshPro>();
            caption.font = controller.titleLabel.font;
            caption.text = title;
            caption.color = new Color(0.025f, 0.03f, 0.045f);
            caption.alignment = TextAlignmentOptions.Center;
            caption.fontSize = 0.23f;
            caption.rectTransform.sizeDelta = new Vector2(0.4f, 0.033f);
        }

        private static void StandLabel(GameObject stand, TabletController controller, string title)
        {
            var go = new GameObject("Stand Label", typeof(RectTransform));
            go.transform.position = stand.transform.position + new Vector3(0f, 0.2f, -0.151f);
            go.transform.SetParent(stand.transform, true);
            TextMeshPro label = go.AddComponent<TextMeshPro>();
            label.font = controller.titleLabel.font;
            label.text = title + "\nInteract: summon / stow";
            label.color = new Color(0.025f, 0.03f, 0.045f);
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 0.2f;
            label.rectTransform.sizeDelta = new Vector2(0.46f, 0.12f);
        }

        private static void RenderComparison(List<TabletController> displays)
        {
            var cameraObject = new GameObject("Theme Preview Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 1.2f, -1f);
            camera.orthographic = true;
            int rows = (displays.Count + 2) / 3;
            camera.orthographicSize = rows * 0.18f + 0.12f;
            camera.cullingMask = 1 << PreviewLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.88f, 0.89f, 0.92f);
            var positions = new List<Vector3>();
            var layers = new Dictionary<GameObject, int>();
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(1920, rows * 600, 24);
            Texture2D image = null;
            var ambientMode = RenderSettings.ambientMode;
            Color ambientLight = RenderSettings.ambientLight;
            bool fog = RenderSettings.fog;
            Light[] lights = Object.FindObjectsOfType<Light>();
            var lightColors = new List<Color>();
            var lightEnabled = new List<bool>();
            var shadows = new List<LightShadows>();
            try
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.65f, 0.65f, 0.65f);
                RenderSettings.fog = false;
                foreach (Light light in lights)
                {
                    lightColors.Add(light.color); lightEnabled.Add(light.enabled); shadows.Add(light.shadows);
                    light.enabled = light.type == LightType.Directional;
                    light.color = Color.white;
                    light.shadows = LightShadows.None;
                }
                for (int i = 0; i < displays.Count; i++)
                {
                    TabletController controller = displays[i];
                    positions.Add(controller.body.position);
                    controller.body.position = new Vector3((i % 3 - 1) * 0.4f, 1.2f + ((rows - 1) * 0.5f - i / 3) * 0.36f, 0f);
                    foreach (Transform child in controller.body.GetComponentsInChildren<Transform>(true))
                    {
                        layers[child.gameObject] = child.gameObject.layer;
                        child.gameObject.layer = PreviewLayer;
                    }
                    controller.tabletArgument = System.Array.IndexOf(controller.pageTitles, "Bed Mirrors");
                    controller._ShowPage();
                }
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(1920, rows * 600, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1920, rows * 600), 0, 0);
                image.Apply();
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../TestResults"));
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "tablet-theme-gallery.png"), image.EncodeToPNG());
            }
            finally
            {
                for (int i = 0; i < positions.Count; i++)
                {
                    displays[i].body.position = positions[i];
                    displays[i].tabletArgument = 0;
                    displays[i]._ShowPage();
                }
                foreach (var item in layers) item.Key.layer = item.Value;
                for (int i = 0; i < lightColors.Count; i++)
                {
                    lights[i].color = lightColors[i]; lights[i].enabled = lightEnabled[i]; lights[i].shadows = shadows[i];
                }
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambientLight;
                RenderSettings.fog = fog;
                RenderTexture.active = previous;
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(target);
                if (image != null) Object.DestroyImmediate(image);
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
