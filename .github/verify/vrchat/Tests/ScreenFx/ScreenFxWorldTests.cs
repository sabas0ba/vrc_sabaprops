using System.Collections;
using System.IO;
using NUnit.Framework;
using SabaProps.ScreenFx.Samples;
using SabaProps.ScreenFx.Samples.Editors;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VRC.SDK3.ClientSim;
using VRC.SDK3.ClientSim.Editor;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace SabaProps.ScreenFx.WorldTests
{
    public class ScreenFxWorldTests
    {
        private string _folder;
        private bool _optionsEnabled;
        private EnterPlayModeOptions _options;

        [OneTimeSetUp]
        public void Compile()
        {
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
        }

        [SetUp]
        public void CreateDemo()
        {
            _optionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            _options = EditorSettings.enterPlayModeOptions;
            _folder = AssetDatabase.GenerateUniqueAssetPath("Assets/ScreenFxWorldTest");
            ScreenFxDemoBuilder.Create(_folder);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            LogAssert.ignoreFailingMessages = true;
            if (Application.isPlaying) yield return new ExitPlayMode();
            ClientSimRuntimeLoader.EndUnityTesting();
            EditorSettings.enterPlayModeOptionsEnabled = _optionsEnabled;
            EditorSettings.enterPlayModeOptions = _options;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(_folder);
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void PackagedDemoRetainsItsReferencesAfterImport()
        {
            const string path = "Assets/ScreenFxSample/Demo/ScreenFxUdonDemo.unity";
            Assert.IsTrue(File.Exists(path));
            EditorSceneManager.OpenScene(path);
            var root = GameObject.Find(ScreenFxDemoBuilder.RootName);
            Assert.IsNotNull(root);
            var driver = root.GetComponent<ScreenFxDriver>();
            Assert.IsNotNull(driver.target);
            Assert.AreEqual(24, driver.presets.Length);
            Assert.AreEqual(driver, root.GetComponentInChildren<ScreenFxPanel>().driver);
            foreach (string dependency in AssetDatabase.GetDependencies(path, true))
                if (dependency.StartsWith("Assets/")) Assert.IsTrue(dependency.StartsWith("Assets/ScreenFxSample/"), dependency);
        }

        [Test]
        public void DemoAndPrefabHaveCompleteReferencesAndCompiledPrograms()
        {
            Assert.IsNotNull(Object.FindObjectOfType<VRCSceneDescriptor>());
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_folder + "/ScreenFxControlRig.prefab");
            Assert.IsNotNull(prefab);
            var driver = prefab.GetComponent<ScreenFxDriver>();
            Assert.AreEqual(24, driver.presets.Length);
            Assert.AreEqual(24, driver.litePresets.Length);
            Assert.IsTrue(driver.target.transform.IsChildOf(prefab.transform));
            Assert.IsFalse(driver.target.enabled);
            var panel = prefab.GetComponentInChildren<ScreenFxPanel>();
            Assert.AreEqual(driver, panel.driver);
            foreach (var proxy in prefab.GetComponentsInChildren<UdonSharp.UdonSharpBehaviour>())
            {
                var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy);
                var asset = UdonSharpEditorUtility.GetUdonSharpProgramAsset(backing);
                Assert.IsNotNull(asset.SerializedProgramAsset.RetrieveProgram(), proxy.name);
            }
            foreach (var button in prefab.GetComponentsInChildren<Button>())
            {
                Assert.AreEqual(1, button.onClick.GetPersistentEventCount());
                var receiver = button.onClick.GetPersistentTarget(0) as UdonBehaviour;
                Assert.IsNotNull(receiver);
                Assert.IsTrue(receiver.transform.IsChildOf(prefab.transform));
            }
            foreach (Material material in driver.presets)
                Assert.IsTrue(AssetDatabase.GetAssetPath(material).StartsWith(_folder + "/"));
            Assert.Throws<System.ArgumentException>(() => ScreenFxDemoBuilder.Create(_folder));
        }

        [UnityTest]
        public IEnumerator ClientSimRunsButtonsIsolationResetTimeoutAndTriggers()
        {
            EditorSceneManager.OpenScene("Assets/ScreenFxSample/Demo/ScreenFxUdonDemo.unity");
            // 同じ Prefab の二つ目を置き、参照と実行時 Material の独立を検査する。
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ScreenFxSample/Demo/ScreenFxControlRig.prefab");
            var copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            copy.name = "Independent Copy";
            copy.transform.position = new Vector3(-5f, 0f, 0f);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            ClientSimRuntimeLoader.BeginUnityTesting(new ClientSimSettings
            {
                enableClientSim = true, initializationDelay = 0f, spawnPlayer = true,
                localPlayerIsMaster = true, displayLogs = false,
            });
            LogAssert.ignoreFailingMessages = true;
            yield return new EnterPlayMode();
            for (int i = 0; i < 600 && Networking.LocalPlayer == null; i++) yield return null;
            Assert.IsNotNull(Networking.LocalPlayer);
            for (int i = 0; i < 120; i++) yield return null;
            LogAssert.ignoreFailingMessages = false;

            var root = GameObject.Find(ScreenFxDemoBuilder.RootName);
            var driver = root.GetComponent<ScreenFxDriver>();
            var renderer = driver.target;
            var other = GameObject.Find("Independent Copy").GetComponent<ScreenFxDriver>().target;
            var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(driver);
            Material preset = driver.presets[0];
            float exposure = preset.GetFloat("_Exposure");
            Assert.IsFalse(renderer.enabled);
            Click(root, "_On");
            for (float until = Time.time + 0.5f; Time.time < until;) yield return null;
            Assert.IsTrue(renderer.enabled);
            Assert.Greater(renderer.sharedMaterial.GetFloat("_Weight"), 0.95f);
            Click(root, "_ExposureUp");
            yield return null;
            Assert.AreEqual(exposure + 0.25f, renderer.sharedMaterial.GetFloat("_Exposure"), 0.001f);
            Assert.AreEqual(exposure, preset.GetFloat("_Exposure"), 0.001f);
            Assert.AreEqual(exposure, other.sharedMaterial.GetFloat("_Exposure"), 0.001f);
            Assert.IsFalse(other.enabled);
            Click(root, "_Reset");
            yield return null;
            Assert.AreEqual(exposure, renderer.sharedMaterial.GetFloat("_Exposure"), 0.001f);
            Click(root, "_Lite");
            yield return null;
            StringAssert.Contains("Lite", renderer.sharedMaterial.shader.name);
            Click(root, "_Next");
            yield return null;
            Assert.AreEqual(1, (int)backing.GetProgramVariable("presetIndex"));
            Click(root, "_Off");
            Assert.IsFalse(renderer.enabled);
            backing.SetProgramVariable("autoOffSeconds", 0.2f);
            Click(root, "_On");
            for (float until = Time.time + 0.4f; Time.time < until;) yield return null;
            Assert.IsFalse(renderer.enabled);

            var zone = GameObject.Find(ScreenFxDemoBuilder.TriggerName).GetComponent<ScreenFxDriver>();
            Networking.LocalPlayer.TeleportTo(new Vector3(5f, 0.2f, 5f), Quaternion.identity);
            for (float until = Time.time + 1f; Time.time < until;) yield return null;
            Assert.IsTrue(zone.target.enabled, "Trigger enter did not enable the effect");
            Networking.LocalPlayer.TeleportTo(new Vector3(0f, 0.2f, -3f), Quaternion.identity);
            for (float until = Time.time + 1f; Time.time < until;) yield return null;
            Assert.IsFalse(zone.target.enabled, "Trigger exit did not stop the effect");
        }

        private static void Click(GameObject root, string name)
        {
            root.transform.Find("Control Panel/" + name).GetComponent<Button>().onClick.Invoke();
        }
    }

    public static class ScreenFxDemoCapture
    {
        public static void FinalizeDelivery()
        {
            const string sample = "Assets/ScreenFxSample";
            string demo = AssetDatabase.IsValidFolder(sample + "/Demo") ? sample + "/Demo" : "Assets/ScreenFxDemoDeliverable";
            if (!AssetDatabase.IsValidFolder(sample + "/CompiledPrograms"))
                AssetDatabase.CreateFolder(sample, "CompiledPrograms");
            foreach (string name in new[] { "ScreenFxDriver", "ScreenFxPanel" })
            {
                var program = AssetDatabase.LoadAssetAtPath<UdonSharp.UdonSharpProgramAsset>(sample + "/" + name + ".asset");
                string source = AssetDatabase.GetAssetPath(program.SerializedProgramAsset);
                string destination = sample + "/CompiledPrograms/" + Path.GetFileName(source);
                if (source != destination)
                {
                    string error = AssetDatabase.MoveAsset(source, destination);
                    if (!string.IsNullOrEmpty(error)) throw new System.InvalidOperationException(error);
                }
            }
            AssetDatabase.SaveAssets();
            foreach (string dependency in AssetDatabase.GetDependencies(demo + "/ScreenFxUdonDemo.unity", true))
            {
                if (dependency.StartsWith("Assets/") && !dependency.StartsWith(demo + "/") && !dependency.StartsWith(sample + "/"))
                    throw new System.InvalidOperationException("配布先に含まれない依存: " + dependency);
            }
            if (demo != sample + "/Demo")
            {
                string error = AssetDatabase.MoveAsset(demo, sample + "/Demo");
                if (!string.IsNullOrEmpty(error)) throw new System.InvalidOperationException(error);
            }
            Debug.Log("[Screen FX] All project asset dependencies are contained in the sample and demo.");
        }

        public static void Configure()
        {
            if (!UpdateLayers.AreLayersSetup()) UpdateLayers.SetupEditorLayers();
            if (!UpdateLayers.IsCollisionLayerMatrixSetup()) UpdateLayers.SetupCollisionLayerMatrix();
            ClientSimProjectSettingsSetup.ApplyClientSimInputAxes();
            ClientSimProjectSettingsSetup.SetAudioSettings();
            var settings = new SerializedObject(AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/ProjectSettings.asset"));
            settings.FindProperty("activeInputHandler").intValue = 2;
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        public static void Generate()
        {
            const string folder = "Assets/ScreenFxDemoDeliverable";
            ScreenFxDemoBuilder.Create(folder);
            var camera = Camera.main;
            camera.transform.position = new Vector3(0f, 1.65f, -3f);
            camera.fieldOfView = 60f;
            var target = new RenderTexture(1600, 900, 24);
            var previous = RenderTexture.active;
            camera.targetTexture = target;
            var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try
            {
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory("TestResults");
                File.WriteAllBytes("TestResults/screenfx-udon-demo.png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
