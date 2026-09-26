using System.IO;
using SabaProps.Tablet.Authoring;
using TMPro;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SabaProps.Tablet.Editors
{
    /// <summary>同じ Bed Mirrors ページで 6 種を比較する Editor 用シーン。</summary>
    public static class TabletThemeGallery
    {
        public const string ScenePath = TabletAssets.SampleFolder + "/TabletThemeGallery.unity";
        private const int PreviewLayer = 23;

        [MenuItem("Tools/SabaProps/Tablet/Create Theme Gallery", false, 3)]
        public static void CreateAndOpen()
        {
            if (Application.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Create();
        }

        // バッチとテストでは独立した検証プロジェクトから呼びます。
        public static void Create()
        {
            Scene scene = TabletSampleScene.Create();
            TabletDefinition original = Object.FindObjectOfType<TabletDefinition>();
            // 比較画像はサンプルの暖色ライトや影の影響を受けない中立な照明にします。
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.65f, 0.65f, 0.65f);
            RenderSettings.fog = false;
            foreach (Light light in Object.FindObjectsOfType<Light>())
            {
                light.enabled = light.type == LightType.Directional;
                light.color = Color.white;
                light.intensity = 0.8f;
                light.shadows = LightShadows.None;
            }
            Camera camera = Camera.main;
            camera.transform.position = new Vector3(0f, 1.2f, -1f);
            camera.transform.rotation = Quaternion.identity;
            camera.orthographic = true;
            camera.orthographicSize = 0.45f;
            camera.cullingMask = 1 << PreviewLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.88f, 0.89f, 0.92f);

            for (int i = 0; i < TabletThemePresets.Files.Length; i++)
            {
                TabletTheme theme = TabletThemePresets.Load(i);
                if (theme == null) throw new System.InvalidOperationException("Theme asset is missing: " + TabletThemePresets.Files[i]);
                TabletDefinition definition = i == 0 ? original : Object.Instantiate(original.gameObject).GetComponent<TabletDefinition>();
                definition.gameObject.name = theme.name;
                definition.transform.position = new Vector3((i % 3 - 1) * 0.4f, 1.2f + (i < 3 ? 0.205f : -0.205f), 0f);
                definition.interactItems.Clear();
                definition.generatedFolder = TabletAssets.GeneratedFolder + "/ThemeGallery/" + TabletThemePresets.Files[i];
                TabletThemePresets.Apply(definition, theme);
                TabletController controller = definition.GetComponent<TabletController>();
                int page = System.Array.IndexOf(controller.pageTitles, "Bed Mirrors");
                for (int p = 0; p < controller.pages.Length; p++) controller.pages[p].SetActive(p == page);
                controller.titleLabel.text = controller.pageTitles[page];
                controller.pageLabel.text = (page + 1) + " / " + controller.pages.Length;
                foreach (TabletToggle toggle in definition.GetComponentsInChildren<TabletToggle>(true))
                    foreach (TabletButton button in toggle.buttons) button.SetLit(toggle.isOn);
                foreach (UdonSharpBehaviour behaviour in definition.GetComponentsInChildren<UdonSharpBehaviour>(true))
                {
                    behaviour.enabled = false;
                    UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour).enabled = false;
                }
                foreach (Transform child in definition.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = PreviewLayer;
                var captionObject = new GameObject("Theme Name", typeof(RectTransform));
                captionObject.layer = PreviewLayer;
                captionObject.transform.SetParent(definition.transform.Find(TabletBuilder.BodyName), false);
                captionObject.transform.localPosition = new Vector3(0f, 0.18f, -0.02f);
                TextMeshPro caption = captionObject.AddComponent<TextMeshPro>();
                caption.font = controller.titleLabel.font;
                caption.text = theme.name;
                caption.color = new Color(0.16f, 0.18f, 0.24f);
                caption.alignment = TextAlignmentOptions.Center;
                caption.fontSize = 0.23f;
                caption.rectTransform.sizeDelta = new Vector2(0.36f, 0.033f);
            }
            // 比較用の静止表示です。動作レビューは単一 Theme の TabletDemo で行います。
            Selection.activeGameObject = original.gameObject;
            Object.DestroyImmediate(GameObject.Find("VRCWorld"));
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../TestResults"));
            Directory.CreateDirectory(folder);
            RenderTexture target = RenderTexture.GetTemporary(1920, 1200, 24);
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1920, 1200, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1200), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(folder, "tablet-theme-gallery.png"), image.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            Object.DestroyImmediate(image);
            Debug.Log("[SabaProps Tablet] Theme gallery saved: " + ScenePath);
        }
    }
}
