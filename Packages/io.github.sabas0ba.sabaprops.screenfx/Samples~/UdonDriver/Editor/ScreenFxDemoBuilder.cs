using System;
using SabaProps.ScreenFx.Editors;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VRC.SDK3.Components;

namespace SabaProps.ScreenFx.Samples.Editors
{
    /// <summary>シーンと、シーン外参照を持たない再利用用 Prefab を生成します。</summary>
    public static class ScreenFxDemoBuilder
    {
        public const string RootName = "Screen FX - Copyable Control Rig";
        public const string TriggerName = "Screen FX - Copyable Trigger Rig";

        [MenuItem("Tools/SabaProps/Screen FX/Create Udon Demo Scene")]
        public static void CreateFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ScreenFxAssetLibrary.EnsureFolder("Assets/SabaProps/ScreenFx");
            Create(AssetDatabase.GenerateUniqueAssetPath("Assets/SabaProps/ScreenFx/UdonDemo"));
        }

        public static void Create(string folder)
        {
            if (!folder.StartsWith("Assets/", StringComparison.Ordinal) || AssetDatabase.IsValidFolder(folder))
                throw new ArgumentException("新規の Assets 配下フォルダーを指定してください。", nameof(folder));
            ScreenFxAssetLibrary.EnsureFolder(folder + "/Materials");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            int count = ScreenFxPresets.All.Length;
            var standard = new Material[count];
            var lite = new Material[count];
            var names = new string[count];
            for (int i = 0; i < count; i++)
            {
                names[i] = ScreenFxPresets.All[i].id;
                standard[i] = ScreenFxAssetLibrary.CreateMaterial(ScreenFxPresets.All[i], false);
                lite[i] = ScreenFxAssetLibrary.CreateMaterial(ScreenFxPresets.All[i], true);
                AssetDatabase.CreateAsset(standard[i], folder + "/Materials/" + names[i] + ".mat");
                AssetDatabase.CreateAsset(lite[i], folder + "/Materials/" + names[i] + "-Lite.mat");
            }

            var root = new GameObject(RootName);
            ScreenFxDriver driver = CreateDriver(root, standard, lite, names);
            driver.autoOffSeconds = 20f;
            CreatePanel(root.transform, driver);
            CopyProxies(root);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, folder + "/ScreenFxControlRig.prefab", InteractionMode.AutomatedAction);

            var trigger = new GameObject(TriggerName);
            trigger.transform.position = new Vector3(5f, 1.5f, 5f);
            var box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(3f, 3f, 3f);
            ScreenFxDriver triggerDriver = CreateDriver(trigger, standard, lite, names);
            triggerDriver.presetIndex = Array.IndexOf(names, "Underwater");
            triggerDriver.activateOnTrigger = true;
            triggerDriver.autoOffSeconds = 20f;
            CopyProxies(trigger);
            PrefabUtility.SaveAsPrefabAssetAndConnect(trigger, folder + "/ScreenFxTriggerRig.prefab", InteractionMode.AutomatedAction);

            BuildRoom(folder);
            var world = new GameObject("VRCWorld");
            var descriptor = world.AddComponent<VRCSceneDescriptor>();
            var spawn = new GameObject("Spawn").transform;
            spawn.SetParent(world.transform, false);
            spawn.position = new Vector3(0f, 0.15f, -3f);
            descriptor.spawns = new[] { spawn };
            var camera = new GameObject("Main Camera", typeof(Camera));
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 1.6f, -3f);
            var cam = camera.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.18f, 0.23f, 0.3f);
            cam.farClipPlane = 80f;
            descriptor.ReferenceCamera = camera;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            RenderSettings.ambientLight = new Color(0.65f, 0.65f, 0.65f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, folder + "/ScreenFxUdonDemo.unity");
            Selection.activeGameObject = root;
            Debug.Log("[Screen FX] Demo created: " + folder);
        }

        private static ScreenFxDriver CreateDriver(GameObject root, Material[] standard, Material[] lite, string[] names)
        {
            var volume = ScreenFxRigFactory.CreateVolumeObject(ScreenFxPresets.All[0], standard[0], false);
            volume.name = "Effect Volume - Runtime Material Instance";
            volume.transform.SetParent(root.transform, false);
            volume.transform.localScale = Vector3.one * 6f;
            var driver = root.AddUdonSharpComponent<ScreenFxDriver>();
            driver.target = volume.GetComponent<Renderer>();
            driver.target.enabled = false;
            driver.presets = standard;
            driver.litePresets = lite;
            driver.presetNames = names;
            driver.targetWeight = 0f;
            driver.fadeSeconds = 0.35f;
            driver.followLocalPlayer = true;
            return driver;
        }

        private static void CopyProxies(GameObject root)
        {
            foreach (var proxy in root.GetComponentsInChildren<UdonSharp.UdonSharpBehaviour>(true))
                UdonSharpEditorUtility.CopyProxyToUdon(proxy);
        }

        private static void CreatePanel(Transform parent, ScreenFxDriver driver)
        {
            var go = new GameObject("Control Panel", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.localPosition = new Vector3(0f, 1.45f, 0f);
            rect.sizeDelta = new Vector2(1000f, 840f);
            rect.localScale = Vector3.one * 0.002f;
            go.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
            go.AddComponent<VRCUiShape>();
            go.AddComponent<Image>().color = new Color(0.025f, 0.04f, 0.065f);
            var panel = go.AddUdonSharpComponent<ScreenFxPanel>();
            panel.driver = driver;
            Label(rect, "SCREEN FX / LOCAL CONTROL", 24f, 22f, 950f, 48f, 32);
            Label(rect, "Only your view changes. Auto OFF after 20 seconds.\nCopy the whole Control Rig or use its prefab.", 24f, 78f, 950f, 70f, 24);
            panel.status = Label(rect, "Rain / GrabPass / OFF", 24f, 164f, 950f, 85f, 26);
            Button(panel, "ON", "_On", 24f, 265f, 460f);
            Button(panel, "OFF NOW", "_Off", 516f, 265f, 460f);
            Button(panel, "< PRESET", "_Previous", 24f, 337f, 460f);
            Button(panel, "PRESET >", "_Next", 516f, 337f, 460f);
            Button(panel, "GRABPASS / LITE", "_Lite", 24f, 409f, 460f);
            Button(panel, "RESET PARAMETERS", "_Reset", 516f, 409f, 460f);
            Button(panel, "WEIGHT -", "_WeightDown", 24f, 481f, 460f);
            Button(panel, "WEIGHT +", "_WeightUp", 516f, 481f, 460f);
            Button(panel, "EXPOSURE -", "_ExposureDown", 24f, 553f, 460f);
            Button(panel, "EXPOSURE +", "_ExposureUp", 516f, 553f, 460f);
            Button(panel, "PARTICLES -", "_ParticlesDown", 24f, 625f, 460f);
            Button(panel, "PARTICLES +", "_ParticlesUp", 516f, 625f, 460f);
            Label(rect, "Preset / mode changes reset its parameters.\nLite: no refraction or blur. OFF NOW stops immediately.\nBlue zone on the right: enter / exit trigger example.", 24f, 711f, 950f, 105f, 22);
        }

        private static RectTransform Element(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static Text Label(Transform parent, string caption, float x, float y, float width, float height, int size)
        {
            var text = Element(parent, "Label", x, y, width, height).gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = new Color(0.91f, 0.95f, 1f);
            text.raycastTarget = false;
            text.supportRichText = false;
            text.text = caption;
            return text;
        }

        private static void Button(ScreenFxPanel panel, string caption, string eventName, float x, float y, float width)
        {
            RectTransform rect = Element(panel.transform, eventName, x, y, width, 58f);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = eventName == "_Off" ? new Color(0.5f, 0.12f, 0.12f) : new Color(0.1f, 0.23f, 0.34f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Label(rect, caption, 12f, 0f, width - 24f, 58f, 25).alignment = TextAnchor.MiddleCenter;
            var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(panel);
            UnityEventTools.AddStringPersistentListener(button.onClick, backing.SendCustomEvent, eventName);
        }

        private static void BuildRoom(string folder)
        {
            Material floor = Colour(folder, "Floor", new Color(0.25f, 0.3f, 0.34f));
            Box("Ground", new Vector3(0f, -0.15f, 5f), new Vector3(20f, 0.3f, 20f), floor);
            Material white = Colour(folder, "White", new Color(0.85f, 0.88f, 0.9f));
            Material blue = Colour(folder, "Blue", new Color(0.12f, 0.55f, 0.8f));
            Material orange = Colour(folder, "Orange", new Color(0.9f, 0.3f, 0.08f));
            Box("Back wall", new Vector3(0f, 2.5f, 12f), new Vector3(20f, 5f, 0.3f), white);
            for (int i = 0; i < 9; i++)
                Box("Depth marker " + i, new Vector3((i % 3 - 1) * 5f, 1.5f, 3f + i / 3 * 3f),
                    new Vector3(0.5f, 3f, 0.5f), i % 2 == 0 ? orange : blue);
            Box("Trigger zone floor", new Vector3(5f, 0.015f, 5f), new Vector3(3f, 0.025f, 3f), blue);
            var sign = new GameObject("Trigger instructions").AddComponent<TextMesh>();
            sign.transform.position = new Vector3(5f, 2.9f, 5f);
            sign.text = "LOCAL UNDERWATER\nEnter blue zone / Exit to stop\nAuto OFF: 20 seconds";
            sign.anchor = TextAnchor.MiddleCenter;
            sign.alignment = TextAlignment.Center;
            sign.characterSize = 0.055f;
            sign.fontSize = 48;
            sign.color = Color.white;
        }

        private static Material Colour(string folder, string name, Color colour)
        {
            var material = new Material(Shader.Find("Unlit/Color")) { color = colour };
            AssetDatabase.CreateAsset(material, folder + "/Materials/Demo-" + name + ".mat");
            return material;
        }

        private static void Box(string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
