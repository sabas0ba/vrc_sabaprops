using System.IO;
using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRC.SDK3.Components;

namespace SabaProps.Capture.Editors
{
    /// <summary>
    /// サンプルシーンを生成します。被写体、2 台のカメラ、2 つの Recorder と再生パネルを置きます。
    /// <para>
    /// Recorder を 2 つ置くのは、入力と満杯時の動作を見比べるためです。片方は常時描画している
    /// カメラの RenderTexture を複製し、満杯になると間引きます。もう片方は無効にしたカメラを
    /// 撮影時にだけ描画し、満杯になると古い画像から上書きします。
    /// </para>
    /// <para>
    /// 被写体の 1 つは周回します。動くものが無いと、撮影した画像がすべて同じになり、
    /// タイムラインを動かしても変化が分からないためです。
    /// </para>
    /// <para>
    /// シーンとアセットは Assets の下へ書き出します。パッケージのフォルダは VCC が更新時に
    /// 丸ごと置き換えるため、そこへ書くと利用者の編集が失われます。
    /// </para>
    /// </summary>
    public static class CaptureSampleScene
    {
        public const string RootFolder = "Assets/SabaProps/Capture";
        public const string SampleFolder = RootFolder + "/Samples";

        public const string ScenePath = SampleFolder + "/CaptureDemo.unity";
        public const string LiveTexturePath = SampleFolder + "/LiveCam.renderTexture";
        public const string LiveScreenMaterialPath = SampleFolder + "/LiveScreen.mat";
        public const string GroundMaterialPath = SampleFolder + "/DemoGround.mat";
        public const string OrbitClipPath = SampleFolder + "/SubjectOrbit.anim";

        // 検証用のテストがシーンをこの名前でたどるため、定数にしています。
        public const string SetRootName = "Set";
        public const string SubjectName = "Orbiting Subject";
        public const string LiveCameraName = "Live Camera";
        public const string TimelapseCameraName = "Timelapse Camera";
        public const string LiveScreenName = "Live Screen";
        public const string TextureRecorderName = "Texture Recorder";
        public const string CameraRecorderName = "Camera Recorder";
        public const string TexturePanelName = "Texture Panel";
        public const string CameraPanelName = "Camera Panel";

        /// <summary>被写体を置く場所の中心。2 台のカメラはここを向きます。</summary>
        public static readonly Vector3 SetCentre = new Vector3(0f, 0f, 5f);

        /// <summary>プレイヤーの出現位置。被写体と 2 枚のパネルが視界に入ります。</summary>
        public static readonly Vector3 SpawnPosition = new Vector3(0f, 0.05f, -1.5f);

        /// <summary>被写体が 1 周する時間 (s)。</summary>
        public const float OrbitPeriod = 20f;

        /// <summary>撮影間隔 (s)。満杯までを 10 秒未満にして、満杯時の動作をすぐ見られるようにします。</summary>
        public const float SampleInterval = 1f;

        public const int SampleMaxFrames = 8;

        private const float OrbitRadius = 2f;
        private const float OrbitHeight = 1.2f;

        [MenuItem("Tools/SabaProps/Capture/Create Sample Scene", false, 1)]
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
                view.LookAt(new Vector3(0f, 1.4f, 3f), Quaternion.Euler(16f, 0f, 0f), 12f);
            }
        }

        /// <summary>
        /// 開いているシーンをサンプルに置き換え、<see cref="ScenePath"/> へ保存します。
        /// 確認のダイアログを出さないので、テストとバッチモードから直接呼べます。
        /// </summary>
        public static Scene Create()
        {
            EnsureFolder(SampleFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            ConfigureLight();
            ConfigureSceneCamera();

            RenderTexture liveTexture = CreateOrLoadLiveTexture();
            BuildSet(liveTexture);

            var cameras = new GameObject("Cameras");
            Camera liveCamera = BuildCamera(
                cameras.transform, LiveCameraName, new Vector3(0f, 2.4f, 1f), new Vector3(0f, 0.8f, 0f), 50f);
            liveCamera.targetTexture = liveTexture;

            Camera timelapseCamera = BuildCamera(
                cameras.transform, TimelapseCameraName, new Vector3(0f, 6.5f, 2f), new Vector3(0f, 0.3f, 0f), 45f);
            // 無効にしておくと、Recorder が撮影時に Render() を呼んだときだけ描画します。
            timelapseCamera.enabled = false;

            var recorders = new GameObject("Recorders");
            CaptureRecorder textureRecorder = BuildRecorder(
                recorders.transform, TextureRecorderName, CaptureRecorder.PolicyThin);
            textureRecorder.sourceTexture = liveTexture;
            UdonSharpEditorUtility.CopyProxyToUdon(textureRecorder);

            CaptureRecorder cameraRecorder = BuildRecorder(
                recorders.transform, CameraRecorderName, CaptureRecorder.PolicyRing);
            cameraRecorder.sourceCamera = timelapseCamera;
            UdonSharpEditorUtility.CopyProxyToUdon(cameraRecorder);

            BuildPanel(textureRecorder, TexturePanelName, "TEXTURE INPUT  /  THIN WHEN FULL",
                new Vector3(-2.7f, 1.5f, 2.2f), -40f);
            BuildPanel(cameraRecorder, CameraPanelName, "CAMERA INPUT  /  OVERWRITE WHEN FULL",
                new Vector3(2.7f, 1.5f, 2.2f), 40f);

            BuildWorld();

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log(Summarise());
            return scene;
        }

        // ------------------------------------------------------------------
        // Recorder とパネル
        // ------------------------------------------------------------------

        private static CaptureRecorder BuildRecorder(Transform parent, string name, int fullPolicy)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            CaptureRecorder recorder = go.AddUdonSharpComponent<CaptureRecorder>();
            recorder.interval = SampleInterval;
            recorder.maxFrames = SampleMaxFrames;
            recorder.fullPolicy = fullPolicy;
            recorder.recordOnStart = true;
            EditorUtility.SetDirty(recorder);
            return recorder;
        }

        private static void BuildPanel(CaptureRecorder recorder, string name, string title, Vector3 position, float yaw)
        {
            CapturePlayer player = CapturePanelBuilder.Create(recorder);
            player.gameObject.name = name;
            player.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            // 2 枚のパネルがどちらの Recorder のものかを、表題で区別します。
            Transform titleLabel = player.transform.Find("Title");
            if (titleLabel != null)
            {
                titleLabel.GetComponent<Text>().text = title;
            }
        }

        // ------------------------------------------------------------------
        // カメラ
        // ------------------------------------------------------------------

        /// <summary>
        /// 被写体の中心を向くカメラ。パネルとスクリーンのレイヤーは映しません。
        /// 映すと、撮影した画像の中に、その画像を表示しているパネルが入り込みます。
        /// </summary>
        private static Camera BuildCamera(Transform parent, string name, Vector3 position, Vector3 lookAtOffset, float fieldOfView)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(SetCentre + lookAtOffset - position, Vector3.up);

            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 120f;
            camera.cullingMask = ~(1 << CapturePanelBuilder.PanelLayer);
            camera.allowHDR = false;
            camera.allowMSAA = false;
            return camera;
        }

        // ------------------------------------------------------------------
        // 被写体と背景
        // ------------------------------------------------------------------

        private static void BuildSet(RenderTexture liveTexture)
        {
            Material ground = CreateOrLoadSurfaceMaterial(GroundMaterialPath, new Color(0.3f, 0.31f, 0.34f));

            var root = new GameObject(SetRootName);

            CreatePiece(root.transform, PrimitiveType.Cube, "Floor",
                new Vector3(0f, -0.5f, 3f), new Vector3(30f, 1f, 30f), ground);
            CreatePiece(root.transform, PrimitiveType.Cube, "Backdrop",
                new Vector3(0f, 2.5f, 8.5f), new Vector3(12f, 5f, 0.4f), ground);

            // 高さの違う 3 つの箱。カメラから見た前後関係が正しく描かれることを確かめる目印です。
            CreateBlock(root.transform, "Red Block", new Vector3(-1.2f, 0.5f, 5.6f), new Vector3(1f, 1f, 1f),
                SampleFolder + "/DemoRed.mat", new Color(0.78f, 0.22f, 0.2f));
            CreateBlock(root.transform, "Blue Block", new Vector3(1.1f, 0.75f, 5.9f), new Vector3(0.8f, 1.5f, 0.8f),
                SampleFolder + "/DemoBlue.mat", new Color(0.2f, 0.4f, 0.8f));
            CreateBlock(root.transform, "Yellow Block", new Vector3(0.2f, 0.3f, 4.2f), new Vector3(0.6f, 0.6f, 0.6f),
                SampleFolder + "/DemoYellow.mat", new Color(0.9f, 0.75f, 0.2f));

            BuildSubject(root.transform);

            Material screenMaterial = CreateOrLoadScreenMaterial(LiveScreenMaterialPath, liveTexture);
            GameObject screen = CreatePiece(root.transform, PrimitiveType.Quad, LiveScreenName,
                new Vector3(0f, 3.6f, 8.28f), new Vector3(3.2f, 1.8f, 1f), screenMaterial);
            screen.layer = CapturePanelBuilder.PanelLayer;
            Object.DestroyImmediate(screen.GetComponent<Collider>());
        }

        /// <summary>
        /// 中心の周りを一定の速さで回る球。Udon を使わず、Animation コンポーネントで動かします。
        /// </summary>
        private static void BuildSubject(Transform parent)
        {
            var pivot = new GameObject("Orbit Pivot");
            pivot.transform.SetParent(parent, false);
            pivot.transform.position = SetCentre + new Vector3(0f, OrbitHeight, 0f);

            GameObject subject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            subject.name = SubjectName;
            subject.transform.SetParent(pivot.transform, false);
            subject.transform.localPosition = new Vector3(OrbitRadius, 0f, 0f);
            subject.transform.localScale = Vector3.one * 0.7f;
            subject.GetComponent<MeshRenderer>().sharedMaterial = CreateOrLoadSurfaceMaterial(
                SampleFolder + "/DemoSubject.mat", new Color(0.95f, 0.95f, 0.95f));

            // プレイヤーが押されないよう、当たり判定は持たせません。
            Object.DestroyImmediate(subject.GetComponent<Collider>());

            AnimationClip clip = CreateOrLoadOrbitClip();
            var animation = subject.AddComponent<Animation>();
            animation.AddClip(clip, clip.name);
            animation.clip = clip;
            animation.playAutomatically = true;
        }

        /// <summary>
        /// 半径 OrbitRadius の円を OrbitPeriod 秒で 1 周する動き。
        /// 接線を円の導関数から与えるので、周の継ぎ目で速度が変わりません。
        /// </summary>
        private static AnimationClip CreateOrLoadOrbitClip()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(OrbitClipPath);
            if (existing != null)
            {
                return existing;
            }

            const int segments = 16;
            float omega = 2f * Mathf.PI / OrbitPeriod;
            var x = new Keyframe[segments + 1];
            var y = new Keyframe[segments + 1];
            var z = new Keyframe[segments + 1];

            for (int i = 0; i <= segments; i++)
            {
                float time = OrbitPeriod * i / segments;
                float angle = omega * time;
                float slopeX = -OrbitRadius * omega * Mathf.Sin(angle);
                float slopeZ = OrbitRadius * omega * Mathf.Cos(angle);
                x[i] = new Keyframe(time, OrbitRadius * Mathf.Cos(angle), slopeX, slopeX);
                y[i] = new Keyframe(time, 0f, 0f, 0f);
                z[i] = new Keyframe(time, OrbitRadius * Mathf.Sin(angle), slopeZ, slopeZ);
            }

            // Animation コンポーネントが再生できるのは legacy のクリップだけです。
            var clip = new AnimationClip { name = "SubjectOrbit", legacy = true, wrapMode = WrapMode.Loop };
            clip.SetCurve("", typeof(Transform), "localPosition.x", new AnimationCurve(x));
            clip.SetCurve("", typeof(Transform), "localPosition.y", new AnimationCurve(y));
            clip.SetCurve("", typeof(Transform), "localPosition.z", new AnimationCurve(z));

            AssetDatabase.CreateAsset(clip, OrbitClipPath);
            return clip;
        }

        private static void CreateBlock(Transform parent, string name, Vector3 position, Vector3 scale, string materialPath, Color colour)
        {
            CreatePiece(parent, PrimitiveType.Cube, name, position, scale,
                CreateOrLoadSurfaceMaterial(materialPath, colour));
        }

        private static GameObject CreatePiece(
            Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = scale;

            if (material != null)
            {
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            return go;
        }

        // ------------------------------------------------------------------
        // シーンの土台
        // ------------------------------------------------------------------

        private static void ConfigureLight()
        {
            Light light = Object.FindObjectOfType<Light>();
            if (light == null)
            {
                var go = new GameObject("Directional Light");
                light = go.AddComponent<Light>();
                light.type = LightType.Directional;
            }

            // 出現位置の側から奥へ照らします。逆向きにすると、プレイヤーとカメラから見える面が陰になります。
            light.transform.rotation = Quaternion.Euler(45f, 25f, 0f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
        }

        /// <summary>
        /// シーンに最初からあるカメラ。VRChat は描画範囲と背景の基準としてだけ使います。
        /// 撮影用の 2 台とは別のものです。
        /// </summary>
        private static void ConfigureSceneCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            camera.transform.SetPositionAndRotation(new Vector3(0f, 1.7f, -3f), Quaternion.Euler(4f, 0f, 0f));
            camera.farClipPlane = 200f;
        }

        private static void BuildWorld()
        {
            var world = new GameObject("VRCWorld");

            var spawn = new GameObject("Spawn");
            spawn.transform.SetParent(world.transform, false);
            spawn.transform.SetPositionAndRotation(SpawnPosition, Quaternion.identity);

            var descriptor = world.AddComponent<VRCSceneDescriptor>();
            descriptor.spawns = new[] { spawn.transform };
            descriptor.RespawnHeightY = -50f;

            if (Camera.main != null)
            {
                descriptor.ReferenceCamera = Camera.main.gameObject;
            }
        }

        // ------------------------------------------------------------------
        // アセット
        // ------------------------------------------------------------------

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

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

        /// <summary>
        /// Live Camera の描画先。16:9 で、Recorder の既定の保存枠 (384 x 216) と同じ縦横比です。
        /// 通常のシーンを描くので深度を持たせます。
        /// </summary>
        private static RenderTexture CreateOrLoadLiveTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RenderTexture>(LiveTexturePath);
            if (existing != null)
            {
                return existing;
            }

            var texture = new RenderTexture(768, 432, 24, RenderTextureFormat.Default)
            {
                name = Path.GetFileNameWithoutExtension(LiveTexturePath),
                antiAliasing = 1,
                useMipMap = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            AssetDatabase.CreateAsset(texture, LiveTexturePath);
            return texture;
        }

        private static Material CreateOrLoadScreenMaterial(string assetPath, Texture texture)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing != null)
            {
                existing.mainTexture = texture;
                EditorUtility.SetDirty(existing);
                return existing;
            }

            // スクリーンは自ら光るものなので、照明の影響を受けない Unlit にします。
            Shader shader = Shader.Find("Unlit/Texture");
            if (shader == null)
            {
                Debug.LogError("[SabaProps Capture] Unlit/Texture シェーダーが見つかりません。");
                return null;
            }

            var material = new Material(shader) { mainTexture = texture };
            AssetDatabase.CreateAsset(material, assetPath);
            return material;
        }

        private static Material CreateOrLoadSurfaceMaterial(string assetPath, Color colour)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("[SabaProps Capture] Standard シェーダーが見つかりません。");
                return null;
            }

            var material = new Material(shader);
            material.color = colour;
            material.SetFloat("_Glossiness", 0.2f);
            material.SetFloat("_Metallic", 0f);

            AssetDatabase.CreateAsset(material, assetPath);
            return material;
        }

        private static string Summarise()
        {
            var text = new StringBuilder();
            text.AppendLine($"[SabaProps Capture] サンプルシーンを {ScenePath} に作成しました。");
            text.AppendLine("起動すると 2 つの Recorder が撮影を始め、それぞれの再生パネルに画像が並びます。");
            text.AppendLine(
                $"・{TextureRecorderName}: {LiveCameraName} の RenderTexture を {SampleInterval} 秒ごとに複製します。"
                + $"{SampleMaxFrames} 枚で満杯になると間引き、撮影間隔を 2 倍にします。");
            text.AppendLine(
                $"・{CameraRecorderName}: 無効にした {TimelapseCameraName} を撮影時にだけ描画します。"
                + "満杯になると古い画像から上書きします。");
            return text.ToString();
        }
    }
}
