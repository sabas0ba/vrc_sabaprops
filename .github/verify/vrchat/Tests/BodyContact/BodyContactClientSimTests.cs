using System.Collections;
using NUnit.Framework;
using SabaProps.BodyContact.Editors;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VRC.SDK3.ClientSim;
using VRC.SDKBase;
using VRC.Udon;

namespace SabaProps.BodyContact.WorldTests
{
    public class BodyContactClientSimTests
    {
        private bool _restoreEnabled;
        private EnterPlayModeOptions _restoreOptions;
        private AssetBundle _bundle;

        [SetUp]
        public void SetUp()
        {
            _restoreEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            _restoreOptions = EditorSettings.enterPlayModeOptions;
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = true;
            if (Application.isPlaying) yield return new ExitPlayMode();
            if (_bundle != null) _bundle.Unload(true);
            ClientSimRuntimeLoader.EndUnityTesting();
            EditorSettings.enterPlayModeOptions = _restoreOptions;
            EditorSettings.enterPlayModeOptionsEnabled = _restoreEnabled;
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator GeneratedScene_UpdatesHudMeshAndControlsThroughUdon()
        {
            BodyContactSampleScene.Create();
            EditorSceneManager.OpenScene(BodyContactSampleScene.ScenePath);
            return RunScene();
        }

        [UnityTest]
        public IEnumerator DeliveredScene_UpdatesHudMeshAndControlsThroughUdon()
        {
            string path = System.Environment.GetEnvironmentVariable("BODYCONTACT_DELIVERED_SCENE");
            if (string.IsNullOrEmpty(path)) Assert.Ignore("Set BODYCONTACT_DELIVERED_SCENE to audit a delivered scene.");
            EditorSceneManager.OpenScene(path);
            return RunScene();
        }

        [UnityTest]
        public IEnumerator BuiltWorld_UpdatesHudAndMeshThroughUdon()
        {
            string path = System.Environment.GetEnvironmentVariable("BODYCONTACT_BUILT_WORLD");
            if (string.IsNullOrEmpty(path)) Assert.Ignore("Set BODYCONTACT_BUILT_WORLD to audit a client bundle.");
            BodyContactSampleScene.Create();
            return RunScene(path);
        }

        private IEnumerator RunScene(string bundlePath = null)
        {
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            ClientSimRuntimeLoader.BeginUnityTesting(new ClientSimSettings
            {
                enableClientSim = true, initializationDelay = 0f, spawnPlayer = true,
                localPlayerIsMaster = true, displayLogs = false,
            });
            // SDK input can run before ClientSim finishes injecting its input provider.
            LogAssert.ignoreFailingMessages = true;
            yield return new EnterPlayMode();
            if (bundlePath != null)
            {
                _bundle = AssetBundle.LoadFromFile(bundlePath);
                Assert.That(_bundle, Is.Not.Null);
                string[] paths = _bundle.GetAllScenePaths();
                Assert.That(paths.Length, Is.EqualTo(1));
                var operation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(paths[0]);
                while (!operation.isDone) yield return null;
            }
            for (int frame = 0; frame < 600 && Networking.LocalPlayer == null; frame++) yield return null;
            Assert.That(Networking.LocalPlayer, Is.Not.Null);
            // EditMode coroutine iterations are not player frames. Wait in game time so
            // ClientSim and Udon Start can finish without invoking their events manually.
            float startupDeadline = Time.time + 1f;
            while (Time.time < startupDeadline) yield return null;
            foreach (ClientSimMenu menu in Resources.FindObjectsOfTypeAll<ClientSimMenu>())
                if (menu.gameObject.scene.IsValid()) menu.CloseMenu();
            LogAssert.ignoreFailingMessages = false;

            UdonBehaviour system = GameObject.Find("Body Contact").GetComponent<UdonBehaviour>();
            Assert.That(system.ProgramId, Is.Not.Zero, "built system has no serialized program");
            UdonBehaviour view = (UdonBehaviour)system.GetProgramVariable("debugView");
            Assert.That(view, Is.Not.Null, "runtime system -> debug view reference");
            Assert.That(view.GetProgramVariable("source"), Is.SameAs(system));
            Assert.That((bool)view.GetProgramVariable("visible"), Is.True, "Gizmo initial state");
            Assert.That((bool)view.GetProgramVariable("hudVisible"), Is.True, "HUD initial state");
            Text label = (Text)view.GetProgramVariable("statusLabel");
            Transform hud = (Transform)view.GetProgramVariable("hudRoot");
            MeshFilter filter = (MeshFilter)view.GetProgramVariable("lineFilter");
            MeshRenderer renderer = (MeshRenderer)view.GetProgramVariable("lineRenderer");
            Assert.That(hud.gameObject.activeInHierarchy, Is.True);
            Assert.That(renderer.enabled, Is.True);
            foreach (string controlName in new[] { "Gizmo", "HUD", "Contact" })
            {
                UdonBehaviour button = GameObject.Find(controlName).GetComponent<UdonBehaviour>();
                Text buttonLabel = (Text)button.GetProgramVariable("label");
                string expected = controlName.ToUpperInvariant();
                Assert.That(buttonLabel.text, Is.EqualTo(expected + ": ON"),
                    controlName + " must update its label automatically before any custom event");
                button.Interact();
                float labelDeadline = Time.time + 0.4f;
                while (Time.time < labelDeadline) yield return null;
                Assert.That(buttonLabel.text, Is.EqualTo(expected + ": OFF"),
                    "Interact and automatic Update must both work");
                button.Interact();
            }
            // ClientSim does not dispatch PostLateUpdate; execute the same body through the Udon VM.
            system.SendCustomEvent("_postLateUpdate");
            float nextRefresh = Time.time + 0.3f;
            while (Time.time < nextRefresh) yield return null;
            system.SendCustomEvent("_postLateUpdate");
            Assert.That(label.text, Does.Contain("HEAD / BODY ONLY"));
            Assert.That(label.isActiveAndEnabled, Is.True);
            Assert.That((int)system.GetProgramVariable("activeProbeCount"), Is.EqualTo(3));
            Assert.That(filter.mesh.vertexCount, Is.GreaterThan(0));
            bool hasLine = false;
            Vector3[] vertices = filter.mesh.vertices;
            for (int i = 0; i + 1 < vertices.Length; i += 2)
                if ((vertices[i] - vertices[i + 1]).sqrMagnitude > 1e-8f) { hasLine = true; break; }
            Assert.That(hasLine, Is.True, "nondegenerate debug lines");
            Vector3 head = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
            Assert.That(Vector3.Distance(hud.position, head), Is.LessThan(1f), "HUD follows the head");
            CaptureHud(hud, bundlePath != null ? "built" : "scene");

            foreach (BodyContactControl controlProxy in Object.FindObjectsOfType<BodyContactControl>())
            {
                UdonBehaviour control = UdonSharpEditorUtility.GetBackingUdonBehaviour(controlProxy);
                int mode = (int)control.GetProgramVariable("controlMode");
                control.SendCustomEvent("_interact");
                if (mode == 1) Assert.That(renderer.enabled, Is.False);
                if (mode == 2) Assert.That(hud.gameObject.activeSelf, Is.False);
                if (mode == 3) Assert.That((bool)system.GetProgramVariable("contactEnabled"), Is.False);
                control.SendCustomEvent("_interact");
            }
            Assert.That(renderer.enabled && hud.gameObject.activeInHierarchy, Is.True);
            Assert.That(system.enabled && view.enabled, Is.True, "Udon must not halt");
        }

        private void CaptureHud(Transform hud, string suffix)
        {
            var host = new GameObject("HUD audit camera");
            Camera camera = host.AddComponent<Camera>();
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.fieldOfView = 75f;
            VRCPlayerApi.TrackingData head = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            camera.transform.SetPositionAndRotation(head.position, head.rotation);
            var target = new RenderTexture(1024, 768, 24);
            var pixels = new Texture2D(1024, 768, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0);
                pixels.Apply();
                Color32[] visible = pixels.GetPixels32();
                System.IO.Directory.CreateDirectory("TestResults");
                System.IO.File.WriteAllBytes("TestResults/bodycontact-hud-" + suffix + ".png", pixels.EncodeToPNG());
                hud.gameObject.SetActive(false);
                camera.Render();
                pixels.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0);
                pixels.Apply();
                Color32[] hidden = pixels.GetPixels32();
                int changed = 0;
                for (int i = 0; i < visible.Length; i++)
                    if (Mathf.Abs(visible[i].r - hidden[i].r) + Mathf.Abs(visible[i].g - hidden[i].g)
                        + Mathf.Abs(visible[i].b - hidden[i].b) > 50) changed++;
                Assert.That(changed, Is.GreaterThan(100), "HUD must change rendered pixels, not just Text.text");
            }
            finally
            {
                hud.gameObject.SetActive(true);
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
            }
        }
    }
}
