using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;

namespace SabaProps.BodyContact.Editors
{
    /// <summary>
    /// Build &amp; Test でそのまま確認できる検証用シーンを生成します。
    /// <para>
    /// README の検証手順に対応する 3 体の固定ダミーを置きます。開けた場所のダミー、
    /// 背後に壁があるダミー、体格の小さいダミーです。リモートプレイヤーとの接触は、
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

        /// <summary>プレイヤーの出現位置。ダミーの列を正面に見ます。</summary>
        public static readonly Vector3 SpawnPosition = new Vector3(0f, 0.05f, -2f);

        /// <summary>周囲に何もないダミー。歩いて入る、手を押し込む、の基本動作を確認します。</summary>
        public static readonly Vector3 OpenDummyPosition = new Vector3(0f, 0f, 2f);

        /// <summary>
        /// 手前に壁があるダミー。壁とダミーの間に立って手を押し込み、押し戻しが壁で止まることを確認します。
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
            BodyContactMenu.AddDummy(system, OpenDummyPosition).name = OpenDummyName;
            BodyContactMenu.AddDummy(system, WallDummyPosition).name = WallDummyName;

            GameObject small = BodyContactMenu.AddDummy(system, SmallDummyPosition);
            small.name = SmallDummyName;
            small.transform.localScale = new Vector3(SmallDummyScale, SmallDummyScale, SmallDummyScale);

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

            CreateBox(
                environment.transform, FloorName,
                new Vector3(0f, -0.5f, 2f), new Vector3(24f, 1f, 16f));

            // ダミーの手前 1.1 m に置きます。壁とダミーの間に立つと、押し戻される先が壁になります。
            CreateBox(
                environment.transform, WallName,
                WallDummyPosition + new Vector3(0f, 1.25f, -1.1f), new Vector3(2.4f, 2.5f, 0.2f));
        }

        // Unity 標準のマテリアルのままにし、シーンのためだけのマテリアルを増やしません。
        private static void CreateBox(Transform parent, string name, Vector3 position, Vector3 size)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = size;
            box.isStatic = true;
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
