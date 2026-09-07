using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace SabaProps.StageCam.Editors
{
    /// <summary>
    /// Generates a demo scene: a stage, two screens, and two rigs that film
    /// whoever is standing on it.
    /// <para>
    /// Two rigs rather than one, because the interesting thing about this
    /// package is the contrast. One holds a fixed angle on the performer's face
    /// and reframes itself for their height; the other keeps circling. Reading
    /// one against the other is what makes the settings mean something.
    /// </para>
    /// <para>
    /// The performer is whoever walks on: both rigs target the local player at
    /// startup, so a single person in the world sees themselves on the screens.
    /// </para>
    /// <para>
    /// Generated rather than shipped inside the package, for the reason in
    /// <see cref="StageCamAssets"/>: VCC replaces the package folder on upgrade
    /// and would take the user's edits with it.
    /// </para>
    /// </summary>
    public static class StageCamSampleScene
    {
        public const string ScenePath = StageCamAssets.SampleFolder + "/StageCamDemo.unity";

        public const string FaceTexturePath = StageCamAssets.SampleFolder + "/FaceCam.renderTexture";
        public const string CraneTexturePath = StageCamAssets.SampleFolder + "/CraneCam.renderTexture";
        public const string FaceScreenMaterialPath = StageCamAssets.SampleFolder + "/FaceScreen.mat";
        public const string CraneScreenMaterialPath = StageCamAssets.SampleFolder + "/CraneScreen.mat";
        public const string GroundMaterialPath = StageCamAssets.SampleFolder + "/DemoGround.mat";
        public const string StageMaterialPath = StageCamAssets.SampleFolder + "/DemoStage.mat";

        // Named as constants because the world tests navigate the scene by them.
        public const string StageRootName = "Stage";
        public const string ScreensRootName = "Screens";
        public const string CamerasRootName = "Cameras";
        public const string FaceRigName = "Face Cam";
        public const string CraneRigName = "Crane Cam";
        public const string FaceScreenName = "Face Screen";
        public const string CraneScreenName = "Crane Screen";

        /// <summary>Stage centre. The performer stands here; the rigs orbit it.</summary>
        public static readonly Vector3 StageCentre = new Vector3(0f, 0f, 4f);

        /// <summary>Player spawn, in front of the stage and facing it.</summary>
        public static readonly Vector3 SpawnPosition = new Vector3(0f, 0.05f, -4f);

        private const float StageHeight = 0.4f;

        [MenuItem("Tools/SabaProps/Stage Cam/Create Sample Scene", false, 1)]
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
                view.LookAt(StageCentre + new Vector3(0f, 1.2f, 0f), Quaternion.Euler(18f, 0f, 0f), 16f);
            }
        }

        /// <summary>
        /// Replaces the open scene with the demo and saves it to
        /// <see cref="ScenePath"/>. Prompt free, so tests and batch mode can call
        /// it directly.
        /// </summary>
        public static Scene Create()
        {
            StageCamAssets.EnsureFolder(StageCamAssets.SampleFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            ConfigureLight();
            ConfigureSceneCamera();

            BuildGround();

            RenderTexture faceTexture = StageCamAssets.CreateOrLoadRenderTexture(FaceTexturePath);
            RenderTexture craneTexture = StageCamAssets.CreateOrLoadRenderTexture(CraneTexturePath);

            BuildScreens(faceTexture, craneTexture);

            var cameras = new GameObject(CamerasRootName);
            StageCamRig face = BuildFaceRig(cameras.transform, faceTexture);
            StageCamRig crane = BuildCraneRig(cameras.transform, craneTexture);

            BuildWorld();

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log(Summarise(face, crane));
            return scene;
        }

        // ------------------------------------------------------------------
        // Rigs
        // ------------------------------------------------------------------

        /// <summary>
        /// A fixed angle on the performer's face, framed for their height.
        /// <para>
        /// Set to orbit in the performer's own frame, so it stays slightly off
        /// their left as they turn rather than swinging round behind them.
        /// Touching it re-targets it to whoever touched it, which is the whole
        /// setup a stage needs when the performer changes.
        /// </para>
        /// </summary>
        private static StageCamRig BuildFaceRig(Transform parent, RenderTexture texture)
        {
            GameObject root = CreateRigRoot(FaceRigName, parent, new Vector3(1.6f, 1.4f, 1.6f));

            // Interact needs a collider on the object the behaviour is on.
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.3f, 0.3f, 0.4f);

            Camera camera = CreateRigCamera(root.transform, texture, 38f);

            StageCamRig rig = root.AddUdonSharpComponent<StageCamRig>();
            rig.rigRoot = root.transform;
            rig.framingCamera = camera;
            rig.targetLocalPlayerOnStart = true;

            rig.subject = StageCamRig.SubjectFace;
            rig.followMode = StageCamRig.FollowBodyOrbit;
            rig.orbitYaw = 14f;
            rig.orbitPitch = 3f;
            rig.subjectHeightOffset = -0.05f;

            // Head and shoulders. 0.35 of eye height is roughly that much of a
            // person, whatever avatar they are wearing.
            rig.autoFraming = true;
            rig.screenFraction = 0.55f;
            rig.subjectHeightScale = 0.35f;

            rig.positionTimeConstant = 0.3f;
            rig.rotationTimeConstant = 0.22f;

            EditorUtility.SetDirty(rig);
            return rig;
        }

        /// <summary>
        /// The crane. Circles the performer while rising and closing in, then
        /// comes back.
        /// <para>
        /// Orbits in world space rather than in the performer's frame: a move
        /// that is supposed to reveal the stage from a new side has to be
        /// measured against the stage, not against whichever way they turned.
        /// </para>
        /// <para>
        /// This is the one with the pickup. Grab it, put it where the shot
        /// should start from, and let go: that becomes the new base angle, with
        /// the sweep still running on top of it.
        /// </para>
        /// </summary>
        private static StageCamRig BuildCraneRig(Transform parent, RenderTexture texture)
        {
            GameObject root = CreateRigRoot(CraneRigName, parent, new Vector3(-3.4f, 1.9f, 1.2f));

            var body = root.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;

            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.3f, 0.3f, 0.4f);

            // The SDK3 component, not the VRC_Pickup base it derives from: that
            // base is what Udon and this package's `handle` field talk to, but a
            // world has to carry the concrete one for the build to accept it.
            var pickup = root.AddComponent<VRCPickup>();
            pickup.pickupable = true;
            pickup.allowManipulationWhenEquipped = true;

            Camera camera = CreateRigCamera(root.transform, texture, 45f);

            StageCamRig rig = root.AddUdonSharpComponent<StageCamRig>();
            rig.rigRoot = root.transform;
            rig.framingCamera = camera;
            rig.handle = pickup;
            rig.targetLocalPlayerOnStart = true;

            rig.subject = StageCamRig.SubjectChest;
            rig.followMode = StageCamRig.FollowWorldOrbit;
            rig.orbitYaw = -40f;
            rig.orbitPitch = 16f;
            rig.orbitDistance = 4.5f;
            rig.subjectHeightOffset = 0.1f;

            rig.cameraWork = true;
            rig.cameraWorkPeriod = 20f;
            rig.cameraWorkYawSweep = 110f;
            rig.cameraWorkPitchRise = 10f;
            rig.cameraWorkDolly = -1.2f;

            rig.allowPickupAdjust = true;
            rig.positionTimeConstant = 0.35f;
            rig.rotationTimeConstant = 0.3f;

            EditorUtility.SetDirty(rig);
            return rig;
        }

        private static GameObject CreateRigRoot(string name, Transform parent, Vector3 position)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            return root;
        }

        /// <summary>
        /// The camera that fills a screen's render texture.
        /// <para>
        /// The screen layer is culled: a camera that can see the screen showing
        /// its own output produces a feedback tunnel instead of a picture.
        /// </para>
        /// </summary>
        private static Camera CreateRigCamera(Transform parent, RenderTexture texture, float fieldOfView)
        {
            var go = new GameObject("Camera");
            go.transform.SetParent(parent, false);

            var camera = go.AddComponent<Camera>();
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 120f;
            camera.targetTexture = texture;
            camera.cullingMask = ~(1 << StageCamAssets.ScreenLayer);
            camera.allowHDR = false;
            camera.allowMSAA = false;

            return camera;
        }

        // ------------------------------------------------------------------
        // Scene furniture
        // ------------------------------------------------------------------

        private static void BuildGround()
        {
            Material ground = StageCamAssets.CreateOrLoadSurfaceMaterial(
                GroundMaterialPath, new Color(0.28f, 0.29f, 0.32f), 0.15f);

            Material stage = StageCamAssets.CreateOrLoadSurfaceMaterial(
                StageMaterialPath, new Color(0.42f, 0.33f, 0.26f), 0.35f);

            var root = new GameObject(StageRootName);

            CreatePiece(
                root.transform, PrimitiveType.Cube, "Floor",
                new Vector3(0f, -0.5f, 2f), Quaternion.identity, new Vector3(40f, 1f, 40f), ground);

            CreatePiece(
                root.transform, PrimitiveType.Cube, "Platform",
                StageCentre + new Vector3(0f, StageHeight * 0.5f, 0f), Quaternion.identity,
                new Vector3(9f, StageHeight, 7f), stage);

            // A back wall, so the crane sweep has something behind the performer
            // and the shot does not open onto empty skybox halfway round.
            CreatePiece(
                root.transform, PrimitiveType.Cube, "Backdrop",
                new Vector3(0f, 3.5f, 8.2f), Quaternion.identity, new Vector3(16f, 7f, 0.4f), ground);
        }

        private static void BuildScreens(RenderTexture faceTexture, RenderTexture craneTexture)
        {
            Material faceMaterial = StageCamAssets.CreateOrLoadScreenMaterial(FaceScreenMaterialPath, faceTexture);
            Material craneMaterial = StageCamAssets.CreateOrLoadScreenMaterial(CraneScreenMaterialPath, craneTexture);

            var root = new GameObject(ScreensRootName);

            CreateScreen(root.transform, FaceScreenName, new Vector3(-3.6f, 4.2f, 7.95f), faceMaterial);
            CreateScreen(root.transform, CraneScreenName, new Vector3(3.6f, 4.2f, 7.95f), craneMaterial);
        }

        /// <summary>
        /// 観客側 (-Z) を向く 16:9 のスクリーン。Quad の既定の法線は -Z です。
        /// </summary>
        private static void CreateScreen(Transform parent, string name, Vector3 position, Material material)
        {
            GameObject screen = CreatePiece(
                parent, PrimitiveType.Quad, name,
                position, Quaternion.identity, new Vector3(5.33f, 3f, 1f), material);

            screen.layer = StageCamAssets.ScreenLayer;

            // A screen is scenery, and a collider on it would be one more thing
            // for the pickup to bump into.
            Object.DestroyImmediate(screen.GetComponent<Collider>());
        }

        private static GameObject CreatePiece(
            Transform parent, PrimitiveType type, string name,
            Vector3 position, Quaternion rotation, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;

            if (material != null)
            {
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            return go;
        }

        private static void ConfigureLight()
        {
            Light light = Object.FindObjectOfType<Light>();
            if (light == null)
            {
                var go = new GameObject("Directional Light");
                light = go.AddComponent<Light>();
                light.type = LightType.Directional;
            }

            light.transform.rotation = Quaternion.Euler(42f, 160f, 0f);
            light.color = new Color(1f, 0.97f, 0.92f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
        }

        /// <summary>
        /// The scene's own camera, which VRChat uses only as the reference for
        /// clipping planes and clear flags. It is not one of the rig cameras.
        /// </summary>
        private static void ConfigureSceneCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            camera.transform.SetPositionAndRotation(
                new Vector3(0f, 2.2f, -6f), Quaternion.Euler(6f, 0f, 0f));
            camera.farClipPlane = 200f;
        }

        /// <summary>
        /// The VRChat world root: a spawn in front of the stage, facing it.
        /// <para>
        /// A direct reference rather than the reflection the foliage package
        /// uses. This package declares com.vrchat.worlds as a dependency, so the
        /// SDK is always there and its API is worth checking at compile time.
        /// </para>
        /// </summary>
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

        private static string Summarise(StageCamRig face, StageCamRig crane)
        {
            var text = new StringBuilder();
            text.AppendLine($"[SabaProps Stage Cam] サンプルシーンを {ScenePath} に作成しました。");
            text.AppendLine("ステージに乗ると、2 枚のスクリーンに自分が映ります。");
            text.AppendLine(
                $"・{FaceRigName}: 顔を追い、身長に合わせて距離を調整します。"
                + "触れるとその人がターゲットになります。");
            text.AppendLine(
                $"・{CraneRigName}: 周囲を {crane.cameraWorkYawSweep} 度回り込みます。"
                + "掴んで置き直すと、その位置が新しい基準アングルになります。");
            text.AppendLine(
                "リアルタイムカメラを 2 台動かしています。実際のワールドでは負荷を測って"
                + "台数と解像度を決めてください。");

            if (face == null || crane == null)
            {
                text.AppendLine("警告: リグの生成に失敗しました。Console を確認してください。");
            }

            return text.ToString();
        }
    }
}
