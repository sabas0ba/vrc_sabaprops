using System.Collections;
using System.IO;
using NUnit.Framework;
using SabaProps.Tablet.Editors;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Rendering.PostProcessing;
using VRC.SDK3.ClientSim;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace SabaProps.Tablet.WorldTests
{
    public class TabletClientSimTests
    {
        private bool restoreOptionsEnabled;
        private EnterPlayModeOptions restoreOptions;


        [OneTimeSetUp]
        public void CompilePrograms()
        {
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = true;
            if (Application.isPlaying)
            {
                yield return new ExitPlayMode();
            }

            ClientSimRuntimeLoader.EndUnityTesting();
            EditorSettings.enterPlayModeOptions = restoreOptions;
            EditorSettings.enterPlayModeOptionsEnabled = restoreOptionsEnabled;
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator Sample_RunsSummonPagesTogglesAndTeleportThroughUdon()
        {
            return RunWorld(false);
        }

        [UnityTest]
        public IEnumerator Gallery_RunsTheWorldAndEveryDisplayThroughUdon()
        {
            return RunWorld(true);
        }

        private IEnumerator RunWorld(bool gallery)
        {
            restoreOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            restoreOptions = EditorSettings.enterPlayModeOptions;
            if (gallery) TabletThemeGallery.Create();
            else TabletSampleScene.Create();
            if (gallery)
            {
                VRCSceneDescriptor descriptor = Object.FindObjectOfType<VRCSceneDescriptor>();
                Assert.That(descriptor, Is.Not.Null);
                Assert.That(descriptor.spawns.Length, Is.GreaterThan(0));
                Assert.That(descriptor.ReferenceCamera, Is.EqualTo(Camera.main.gameObject));
                Assert.That(Camera.main.orthographic, Is.False);
                Assert.That(Camera.main.cullingMask, Is.Not.EqualTo(1 << 23));
                Assert.That(Object.FindObjectsOfType<TabletKeyTrigger>(true).Length, Is.EqualTo(1));
                Assert.That(Object.FindObjectsOfType<TabletReachTrigger>(true).Length, Is.EqualTo(1));
                Assert.That(Object.FindObjectsOfType<TabletController>(true).Length, Is.EqualTo(TabletThemePresets.Files.Length + 1));
            }
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            ClientSimRuntimeLoader.BeginUnityTesting(new ClientSimSettings
            {
                enableClientSim = true,
                initializationDelay = 0f,
                spawnPlayer = true,
                localPlayerIsMaster = true,
                displayLogs = false,
            });

            // SDK 3.10.4 can poll input before ClientSim injects its input system.
            LogAssert.ignoreFailingMessages = true;
            yield return new EnterPlayMode();
            for (int frame = 0; frame < 600 && Networking.LocalPlayer == null; frame++)
            {
                yield return null;
            }

            Assert.That(Networking.LocalPlayer, Is.Not.Null, "ClientSim did not spawn a player");
            for (int frame = 0; frame < 120; frame++)
            {
                yield return null;
            }

            foreach (ClientSimMenu menu in Resources.FindObjectsOfTypeAll<ClientSimMenu>())
            {
                if (menu.gameObject.scene.IsValid())
                {
                    menu.CloseMenu();
                }
            }
            LogAssert.ignoreFailingMessages = false;
            UdonBehaviour controller = UdonSharpEditorUtility.GetBackingUdonBehaviour(
                GameObject.Find(TabletSampleScene.TabletName).GetComponent<TabletController>());
            Transform body = (Transform)controller.GetProgramVariable("body");
            Assert.That(body.gameObject.activeSelf, Is.True, "Start did not show the sample tablet");
            Capture(body, "tablet-clientsim.png");

            controller.SendCustomEvent("_Stow");
            Assert.That(body.gameObject.activeSelf, Is.False);
            GameObject.Find(TabletSampleScene.StandName).GetComponent<UdonBehaviour>().SendCustomEvent("_interact");
            Assert.That(body.gameObject.activeSelf, Is.True, "Interact did not summon the tablet through Udon");

            GameObject[] pages = (GameObject[])controller.GetProgramVariable("pages");
            Assert.That(pages[0].activeSelf, Is.True);
            controller.SendCustomEvent("_NextPage");
            Assert.That(pages[0].activeSelf, Is.False);
            Assert.That(pages[1].activeSelf, Is.True);

            bool toggledMirror = false;
            foreach (TabletToggle toggle in Object.FindObjectsOfType<TabletToggle>(true))
            {
                if (toggle.objects.Length > 0 && toggle.objects[0].name == "Mirror LQ")
                {
                    UdonSharpEditorUtility.GetBackingUdonBehaviour(toggle).SendCustomEvent("_TurnOn");
                    Assert.That(toggle.objects[0].activeSelf, Is.True);
                    foreach (TabletToggle peer in toggle.exclusive)
                        if (peer.objects.Length > 0 && peer.objects[0].name == "Mirror HQ")
                            Assert.That(peer.objects[0].activeSelf, Is.False, "HQ mirror remained active");
                    toggledMirror = true;
                }
            }
            Assert.That(toggledMirror, Is.True, "Mirror LQ runtime module is missing");

            string[] titles = (string[])controller.GetProgramVariable("pageTitles");
            for (int i = 0; i < titles.Length; i++)
            {
                if (titles[i] != "Bed Mirrors" && titles[i] != "Post Effects") continue;
                controller.SetProgramVariable("tabletArgument", i);
                controller.SendCustomEvent("_ShowPage");
                yield return null;
                Capture(body, titles[i] == "Bed Mirrors" ? "tablet-bed-mirrors.png" : "tablet-post-effects.png");
            }

            foreach (TabletToggle toggleProxy in Object.FindObjectsOfType<TabletToggle>(true))
            {
                UdonBehaviour toggle = UdonSharpEditorUtility.GetBackingUdonBehaviour(toggleProxy);
                if (toggleProxy.objects.Length > 0 && toggleProxy.objects[0].name.StartsWith("Bed Mirror"))
                {
                    toggle.SendCustomEvent("_Toggle");
                    Assert.That(toggleProxy.objects[0].activeSelf, Is.True);
                }
                if (toggleProxy.colliders.Length == 2 && toggleProxy.colliders[0].gameObject.name == "Bed")
                {
                    toggle.SendCustomEvent("_Toggle");
                    foreach (Collider collider in toggleProxy.colliders) Assert.That(collider.enabled, Is.False);
                    Assert.That(GameObject.Find("Bed"), Is.Not.Null);
                    toggle.SendCustomEvent("_Toggle");
                    foreach (Collider collider in toggleProxy.colliders) Assert.That(collider.enabled, Is.True);
                }
            }

            TabletPostEffects effectsProxy = Object.FindObjectOfType<TabletPostEffects>();
            UdonBehaviour effects = UdonSharpEditorUtility.GetBackingUdonBehaviour(effectsProxy);
            Behaviour[] volumes = (Behaviour[])effects.GetProgramVariable("volumes");
            foreach (TabletSlider sliderProxy in Object.FindObjectsOfType<TabletSlider>(true))
            {
                UdonBehaviour slider = UdonSharpEditorUtility.GetBackingUdonBehaviour(sliderProxy);
                string eventName = (string)slider.GetProgramVariable("eventName");
                float value = eventName == "_SetBrightness" ? -1f : eventName == "_SetHue" ? 90f : 1f;
                slider.SetProgramVariable("value", value);
                slider.SendCustomEvent("_SetValue");
                yield return null;
                yield return null;
                Assert.That((float)slider.GetProgramVariable("value"), Is.EqualTo(value));
            }
            Assert.That(effectsProxy.animator.GetFloat("Brightness"), Is.EqualTo(-1f));
            Assert.That(effectsProxy.animator.GetFloat("Hue"), Is.EqualTo(90f));
            Assert.That(effectsProxy.animator.GetFloat("Glow"), Is.EqualTo(1f));
            Assert.That(Weight(volumes[0]), Is.EqualTo(0.5f).Within(0.02f));
            Assert.That(Weight(volumes[1]), Is.EqualTo(0f).Within(0.02f));
            Assert.That(Weight(volumes[3]), Is.EqualTo(0.5f).Within(0.02f));
            Assert.That(Weight(volumes[4]), Is.EqualTo(1f).Within(0.02f));
            Capture(body, "tablet-post-effects-adjusted.png", true);
            effects.SendCustomEvent("_Toggle");
            foreach (Behaviour volume in volumes) Assert.That(volume.enabled, Is.False);
            effects.SendCustomEvent("_Toggle");
            foreach (Behaviour volume in volumes) Assert.That(volume.enabled, Is.True);

            UdonBehaviour teleport = UdonSharpEditorUtility.GetBackingUdonBehaviour(
                Object.FindObjectOfType<TabletTeleport>(true));
            Transform[] destinations = (Transform[])teleport.GetProgramVariable("destinations");
            teleport.SetProgramVariable("tabletArgument", 0);
            teleport.SendCustomEvent("_TeleportToDestination");
            yield return null;
            Vector3 position = Networking.LocalPlayer.GetPosition();
            Vector3 destination = destinations[0].position;
            Assert.That(Vector2.Distance(new Vector2(position.x, position.z),
                new Vector2(destination.x, destination.z)), Is.LessThan(0.25f));
            yield return null;
            Assert.That(body.gameObject.activeSelf, Is.False, "Distant tablet was not automatically stowed");
            controller.SendCustomEvent("_Summon");
            Assert.That(body.gameObject.activeSelf, Is.True);
            if (gallery)
            {
                for (int i = 0; i < TabletThemePresets.Files.Length; i++)
                {
                    string name = TabletThemePresets.Load(i).name;
                    TabletController display = GameObject.Find(TabletThemeGallery.DisplayPrefix + name).GetComponent<TabletController>();
                    UdonBehaviour runtime = UdonSharpEditorUtility.GetBackingUdonBehaviour(display);
                    Assert.That(runtime.enabled, Is.True);
                    runtime.SendCustomEvent("_Stow");
                    Assert.That(display.body.gameObject.activeSelf, Is.False);
                    GameObject.Find("Theme Stand - " + name).GetComponent<UdonBehaviour>().SendCustomEvent("_interact");
                    Assert.That(display.body.gameObject.activeSelf, Is.True, name + " did not summon");
                    runtime.SendCustomEvent("_NextPage");
                    Assert.That(display.pages[1].activeSelf, Is.True);
                    runtime.SetProgramVariable("tabletArgument", System.Array.IndexOf(display.pageTitles, "Bed Mirrors"));
                    runtime.SendCustomEvent("_ShowPage");
                    yield return null;
                    foreach (TabletButton button in display.buttons)
                    {
                        if (!(button.target is TabletToggle toggle) || toggle.objects.Length == 0 || toggle.objects[0].name != "Bed Mirror Head") continue;
                        bool before = toggle.objects[0].activeSelf;
                        UdonSharpEditorUtility.GetBackingUdonBehaviour(button).SendCustomEvent("_interact");
                        Assert.That(toggle.objects[0].activeSelf, Is.EqualTo(!before), name + " toggle did not reach the World");
                    }
                    runtime.SendCustomEvent("_Stow");
                }
            }
        }

        private static float Weight(Behaviour volume)
        {
            return (float)volume.GetType().GetField("weight").GetValue(volume);
        }

        private static void Capture(Transform body, string filename, bool postEffects = false)
        {
            Camera camera = new GameObject("Tablet verification camera").AddComponent<Camera>();
            camera.transform.position = body.position - body.forward * 0.7f;
            camera.transform.rotation = Quaternion.LookRotation(body.forward, body.up);
            camera.orthographic = true;
            camera.orthographicSize = 0.18f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 10f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.cullingMask = ~((1 << 9) | (1 << 10) | (1 << 18));
            camera.backgroundColor = new Color(0.15f, 0.17f, 0.20f);
            if (postEffects)
            {
                var layer = camera.gameObject.AddComponent<PostProcessLayer>();
                layer.volumeLayer = 1 << 22;
                layer.volumeTrigger = camera.transform;
                string[] resources = AssetDatabase.FindAssets("t:PostProcessResources");
                layer.Init(AssetDatabase.LoadAssetAtPath<PostProcessResources>(AssetDatabase.GUIDToAssetPath(resources[0])));
                camera.allowHDR = true;
            }
            RenderTexture target = RenderTexture.GetTemporary(1280, 800, 24,
                postEffects ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, 1280f, 800f), 0, 0);
            image.Apply();
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../TestResults"));
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, filename), image.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            Object.Destroy(image);
            Object.Destroy(camera.gameObject);
        }
    }
}
