using UnityEditor;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;

namespace SabaProps.BodyContact.Editors
{
    /// <summary>
    /// Build &amp; Test でそのまま確認できる検証用シーンを生成します。
    /// <para>
    /// 静止・壁際・小型・往復移動・旋回のマネキンと、距離の目盛りがある床を置きます。
    /// リモートプレイヤーとの接触は、
    /// 同じシーンを 2 クライアントで起動して確認します。
    /// </para>
    /// <para>
    /// シーンはパッケージ内ではなく Assets 以下に生成します。VCC は更新時にパッケージの
    /// フォルダを置き換えるため、パッケージ内に置くと利用者の編集が失われます。
    /// </para>
    /// </summary>
    public static class BodyContactSampleScene
    {
        public const string SampleFolder = "Assets/SabaProps/BodyContact/Samples";
        public const string ScenePath = SampleFolder + "/BodyContactDemo.unity";

        // 検証用プロジェクトのテストが、これらの名前と位置でシーンを確認します。
        public const string FloorName = "Floor";
        public const string WallName = "Wall";
        public const string OpenDummyName = "Dummy Open";
        public const string WallDummyName = "Dummy Wall";
        public const string SmallDummyName = "Dummy Small";
        public const string MovingDummyName = "Dummy Moving";
        public const string TurningDummyName = "Dummy Turning";
        public static readonly Vector3 MovingDummyPosition = new Vector3(-2.5f, 0f, 6f);
        public static readonly Vector3 TurningDummyPosition = new Vector3(2.5f, 0f, 6f);

        /// <summary>プレイヤーの出現位置。ダミーの列を正面に見ます。</summary>
        public static readonly Vector3 SpawnPosition = new Vector3(0f, 0.05f, -2f);

        /// <summary>周囲に何もないダミー。頭・体幹の接触を確認します。</summary>
        public static readonly Vector3 OpenDummyPosition = new Vector3(0f, 0f, 2f);

        /// <summary>
        /// 手前に壁があるダミー。壁とダミーの間で体幹を接触させ、押し戻しが壁で止まることを確認します。
        /// </summary>
        public static readonly Vector3 WallDummyPosition = new Vector3(3.5f, 0f, 2f);

        /// <summary>体格の小さいダミー。寸法が目線の高さに比例することを確認します。</summary>
        public static readonly Vector3 SmallDummyPosition = new Vector3(-3.5f, 0f, 2f);

        /// <summary>小さいダミーの倍率。</summary>
        public const float SmallDummyScale = 0.6f;

        [MenuItem("Tools/SabaProps/Body Contact/Create Sample Scene", false, 1)]
        public static void CreateAndOpen()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            // 新しいフィールドを保存する前に、Udonの変数定義を最新にします。
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
            Scene scene = Create();
            if (!scene.IsValid())
            {
                return;
            }

            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                view.LookAt(OpenDummyPosition + new Vector3(0f, 1f, 0f), Quaternion.Euler(20f, 0f, 0f), 9f);
            }
        }

        /// <summary>
        /// 開いているシーンを検証用シーンで置き換え、<see cref="ScenePath"/> へ保存します。
        /// 確認ダイアログを出さないため、テストとバッチモードから直接呼べます。
        /// </summary>
        public static Scene Create()
        {
            EnsureFolder(SampleFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.transform.SetPositionAndRotation(new Vector3(0f, 2.2f, -5f), Quaternion.Euler(14f, 0f, 0f));
            }

            BuildEnvironment();

            GameObject root = BodyContactMenu.CreateSystem();
            BodyContactSystem system = root.GetComponent<BodyContactSystem>();
            system.startupDiagnostics = true;
            BodyContactMenu.AddDummy(system, OpenDummyPosition).name = OpenDummyName;
            BodyContactMenu.AddDummy(system, WallDummyPosition).name = WallDummyName;

            GameObject small = BodyContactMenu.AddDummy(system, SmallDummyPosition);
            small.name = SmallDummyName;
            small.transform.localScale = new Vector3(SmallDummyScale, SmallDummyScale, SmallDummyScale);

            GameObject moving = BodyContactMenu.AddDummy(system, MovingDummyPosition);
            moving.name = MovingDummyName;
            BodyContactSampleVisuals.AddMotion(moving, true);
            GameObject turning = BodyContactMenu.AddDummy(system, TurningDummyPosition);
            turning.name = TurningDummyName;
            BodyContactSampleVisuals.AddMotion(turning, false);

            var signs = new GameObject("Test labels");
            BodyContactSampleVisuals.Label(signs.transform, "Open label", "STATIC / HEAD + TORSO", OpenDummyPosition + Vector3.up * 2.1f);
            BodyContactSampleVisuals.Label(signs.transform, "Wall label", "WALL / BLOCKED BY WORLD", WallDummyPosition + Vector3.up * 2.9f);
            BodyContactSampleVisuals.Label(signs.transform, "Small label", "SMALL / 0.6x", SmallDummyPosition + Vector3.up * 1.5f);
            BodyContactSampleVisuals.Label(signs.transform, "Moving label", "TRANSLATE / +/-0.8 m / 8 s", MovingDummyPosition + Vector3.up * 2.1f);
            BodyContactSampleVisuals.Label(signs.transform, "Turning label", "TURN / +/-60 deg / 8 s", TurningDummyPosition + Vector3.up * 2.1f);
            BodyContactSampleVisuals.Label(signs.transform, "Grid legend", "CORE STARTUP 1\nFLOOR: 1 m squares / 10 cm subdivisions\nCONTACT: HEAD / BODY ONLY", new Vector3(0f, 2.8f, 8f));

            CreateControl(system, "Gizmo", new Vector3(-1.5f, 1f, -3.5f), 1);
            CreateControl(system, "HUD", new Vector3(0f, 1f, -3.5f), 2);
            CreateControl(system, "Contact", new Vector3(1.5f, 1f, -3.5f), 3);
            CreateTestStation(system);

            BuildWorld(camera);

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log(
                "[SabaProps Body Contact] 検証用シーンを " + ScenePath + " に作成しました。"
                + "VRChat SDK の Build & Test で起動し、パッケージの README の検証手順に沿って確認してください。");
            return scene;
        }

        private static void BuildEnvironment()
        {
            var environment = new GameObject("Environment");

            GameObject floor = CreateBox(
                environment.transform, FloorName,
                new Vector3(0f, -0.5f, 2f), new Vector3(24f, 1f, 16f));
            floor.GetComponent<Renderer>().sharedMaterial = BodyContactSampleVisuals.Material(
                "MetricFloor", "SabaProps/Body Contact/Metric Floor", Color.white);

            // ダミーの手前 1.1 m に置きます。壁とダミーの間に立つと、押し戻される先が壁になります。
            GameObject wall = CreateBox(
                environment.transform, WallName,
                WallDummyPosition + new Vector3(0f, 1.25f, -1.1f), new Vector3(2.4f, 2.5f, 0.2f));
            wall.GetComponent<Renderer>().sharedMaterial = BodyContactSampleVisuals.Material(
                "TestWall", "Standard", new Color(0.32f, 0.4f, 0.48f));
        }

        private static GameObject CreateBox(Transform parent, string name, Vector3 position, Vector3 size)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = size;
            box.isStatic = true;
            return box;
        }

        private static void BuildWorld(Camera referenceCamera)
        {
            var world = new GameObject("VRCWorld");

            var spawn = new GameObject("Spawn");
            spawn.transform.SetParent(world.transform, false);
            spawn.transform.SetPositionAndRotation(SpawnPosition, Quaternion.identity);

            var descriptor = world.AddComponent<VRCSceneDescriptor>();
            descriptor.spawns = new[] { spawn.transform };
            descriptor.RespawnHeightY = -50f;

            if (referenceCamera != null)
            {
                descriptor.ReferenceCamera = referenceCamera.gameObject;
            }
        }

        private static void CreateControl(BodyContactSystem system, string name, Vector3 position, int mode)
        {
            GameObject button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = name;
            button.transform.position = position;
            button.transform.localScale = new Vector3(0.8f, 0.3f, 0.15f);
            BodyContactControl control = button.AddUdonSharpComponent<BodyContactControl>();
            control.source = system;
            control.controlMode = mode;
            control.debugView = system.debugView;
            var labelRoot = new GameObject(name + " Label");
            labelRoot.transform.position = position + Vector3.up * 0.4f;
            labelRoot.transform.localScale = Vector3.one * 0.002f;
            Canvas canvas = labelRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)labelRoot.transform).sizeDelta = new Vector2(480f, 160f);
            var labelObject = new GameObject("Text");
            labelObject.transform.SetParent(labelRoot.transform, false);
            UnityEngine.UI.Text label = labelObject.AddComponent<UnityEngine.UI.Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 24;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            label.text = name.ToUpperInvariant() + ": WAITING FOR UDON";
            RectTransform rect = (RectTransform)label.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            control.label = label;
            UdonSharpEditorUtility.CopyProxyToUdon(control);
        }

        private static void CreateTestStation(BodyContactSystem system)
        {
            GameObject seat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seat.name = "Contact Test Station";
            seat.transform.position = new Vector3(5.5f, 0.45f, -1f);
            seat.transform.localScale = new Vector3(0.8f, 0.15f, 0.8f);
            VRCStation station = seat.AddComponent<VRCStation>();
            var enter = new GameObject("Seat entry");
            enter.transform.SetParent(seat.transform, false);
            enter.transform.position = new Vector3(5.5f, 0.55f, -1f);
            var exit = new GameObject("Seat exit");
            exit.transform.SetParent(seat.transform, false);
            exit.transform.position = new Vector3(5.5f, 0.05f, -2f);
            station.stationEnterPlayerLocation = enter.transform;
            station.stationExitPlayerLocation = exit.transform;
            BodyContactStationRelay relay = seat.AddUdonSharpComponent<BodyContactStationRelay>();
            relay.source = system;
            relay.interactToSit = true;
            UdonSharpEditorUtility.CopyProxyToUdon(relay);
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
