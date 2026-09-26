using System.Collections;
using NUnit.Framework;
using SabaProps.Liquid.Editors;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using VRC.SDK3.ClientSim;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;

namespace SabaProps.Liquid.WorldTests
{
    /// <summary>
    /// Runs the interactive world under ClientSim and presses its buttons.
    /// <para>
    /// Fires the one-shot nozzle and starts the continuous one, lets the humid
    /// rooms and the rain patch run, then steps the dark room through lit,
    /// blacklight-only and dark. Checks what each should do to the mannequins
    /// and writes review images of every area to TestResults.
    /// </para>
    /// </summary>
    public class LiquidInteractivePlayTests
    {
        private const float RunSeconds = 20f;

        // Pool order, as LiquidInteractiveScene.Create builds it.
        private const int BenchFirst = 0;
        private const int ViscosityFirst = 3;
        private const int SaunaFirst = ViscosityFirst + 6;
        private const int BathroomFirst = SaunaFirst + 2;
        private const int DampFirst = BathroomFirst + 2;
        private const int DarkFirst = DampFirst + 2;
        private const int PrefabTarget = DarkFirst + 4;
        private const int InTheRain = PrefabTarget + 1;
        private const int UnderUmbrella = InTheRain + 1;
        private const int SprayTargetLeft = UnderUmbrella + 1;
        private const int SprayTargetRight = SprayTargetLeft + 1;

        private bool _optionsEnabled;
        private EnterPlayModeOptions _options;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return new ExitPlayMode();
            ClientSimRuntimeLoader.EndUnityTesting();
            EditorSettings.enterPlayModeOptionsEnabled = _optionsEnabled;
            EditorSettings.enterPlayModeOptions = _options;
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator Interactive_SourcesHumidityAndLightBehave()
        {
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
            LiquidPrefabBuilder.BuildAll();
            LiquidInteractiveScene.Create();
            EditorSceneManager.OpenScene(LiquidInteractiveScene.ScenePath);

            _optionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            _options = EditorSettings.enterPlayModeOptions;
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

            LogAssert.ignoreFailingMessages = true;
            yield return new EnterPlayMode();
            for (int i = 0; i < 120; i++)
            {
                yield return null;
            }

            LogAssert.ignoreFailingMessages = false;
            Assert.IsNotNull(Networking.LocalPlayer);
            var menu = Object.FindObjectOfType<ClientSimMenu>(true);
            if (menu != null)
            {
                menu.CloseMenu();
            }

            // The lamp stays on while the paint goes on, whatever the clock says.
            LiquidLightZone zone = GameObject.Find(LiquidInteractiveScene.DarkRoomName).GetComponentInChildren<LiquidLightZone>();
            UdonBehaviour zoneBacking = UdonSharpEditorUtility.GetBackingUdonBehaviour(zone);
            SetLight(zoneBacking, true, false);

            // Press Fire on the one-shot nozzle and Start on the continuous one.
            LiquidNozzle[] nozzles = GameObject.Find(LiquidInteractiveScene.NozzleBenchName).GetComponentsInChildren<LiquidNozzle>();
            foreach (LiquidNozzle nozzle in nozzles)
            {
                UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(nozzle);
                if (nozzle.mode == LiquidNozzle.ModeOneShot)
                {
                    backing.SendCustomEvent(nameof(LiquidNozzle.Fire));
                }
                else if (nozzle.mode == LiquidNozzle.ModeContinuous)
                {
                    backing.SendCustomEvent(nameof(LiquidNozzle.Toggle));
                }
            }

            // Start the carried nozzles on the rack, as if someone had picked them up and pressed use.
            foreach (VRCPickup pickup in GameObject.Find(LiquidInteractiveScene.SprayPlayName).GetComponentsInChildren<VRCPickup>())
            {
                LiquidNozzle nozzle = pickup.GetComponentInChildren<LiquidNozzle>();
                UdonSharpEditorUtility.GetBackingUdonBehaviour(nozzle).SendCustomEvent(nameof(LiquidNozzle.Trigger));
            }

            // A single splash of water runs off and dries within the run; fire the one-shot again
            // shortly before looking, so the check sees a fresh splash.
            yield return Wait(RunSeconds - 3f);
            foreach (LiquidNozzle nozzle in nozzles)
            {
                if (nozzle.mode == LiquidNozzle.ModeOneShot)
                {
                    UdonSharpEditorUtility.GetBackingUdonBehaviour(nozzle).SendCustomEvent(nameof(LiquidNozzle.Fire));
                }
            }

            yield return Wait(3f);

            LiquidCanvasPool pool = Object.FindObjectOfType<LiquidCanvasPool>();
            LiquidBodyCanvas[] m = pool.mannequins;
            Assert.AreEqual(SprayTargetRight + 1, m.Length, "the pool order changed; update the indices");

            LiquidDemoPlayTests.Capture("liquid-interactive-overview.png", new Vector3(0f, 9f, -16f), new Vector3(2f, 0.5f, 5f), 62f);
            LiquidDemoPlayTests.CaptureGrid("liquid-interactive-nozzles.png", m, BenchFirst, 3);
            LiquidDemoPlayTests.CaptureGrid("liquid-interactive-viscosity.png", m, ViscosityFirst, 6);
            LiquidDemoPlayTests.Capture("liquid-interactive-particles.png", new Vector3(8f, 1.6f, -2.5f), new Vector3(8f, 1.1f, 2f), 60f);
            LiquidDemoPlayTests.CaptureGrid("liquid-interactive-humid.png", m, SaunaFirst, 4, 1.5f, 1.1f);
            LiquidDemoPlayTests.CaptureGrid("liquid-interactive-humid-closeup.png", m, SaunaFirst, 2, 0.9f, 1.35f);
            LiquidDemoPlayTests.Capture("liquid-interactive-rooms.png", new Vector3(-8f, 2f, 7f), new Vector3(-8f, 1.2f, 13f), 70f);
            LiquidDemoPlayTests.CaptureGrid("liquid-interactive-rain.png", m, PrefabTarget, 3);
            LiquidDemoPlayTests.Capture("liquid-interactive-prefabs.png", new Vector3(7f, 2.5f, -11.5f), new Vector3(8f, 0.8f, -5.5f), 70f);

            foreach (LiquidNozzle nozzle in nozzles)
            {
                UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(nozzle);
                TestContext.WriteLine(nozzle.transform.parent.name + ": running " + backing.GetProgramVariable("_running")
                    + ", shots received " + backing.GetProgramVariable("_shotsReceived")
                    + ", volume " + backing.GetProgramVariable("_volume")
                    + ", rays cast " + backing.GetProgramVariable("_raysCast")
                    + ", hit " + backing.GetProgramVariable("_raysHit")
                    + ", delivered " + backing.GetProgramVariable("_stampsDelivered"));
                if (nozzle.mode == LiquidNozzle.ModeOneShot)
                {
                    Assert.AreEqual(2, (int)backing.GetProgramVariable("_shotsReceived"), "the one-shot events did not all arrive");
                    Assert.AreEqual(backing.GetProgramVariable("_raysHit"), backing.GetProgramVariable("_stampsDelivered"),
                        "hits were held but never delivered");
                }
            }

            for (int i = BenchFirst; i < BenchFirst + 3; i++)
            {
                TestContext.WriteLine(m[i].transform.parent.parent.name + " coverage " + LiquidDemoPlayTests.Coverage(m[i].projectorMaterial));
            }

            for (int i = BenchFirst; i < BenchFirst + 3; i++)
            {
                Assert.Greater(LiquidDemoPlayTests.Coverage(m[i].projectorMaterial), 0f,
                    m[i].transform.parent.parent.name + ": the nozzle did not reach its mannequin");
            }

            for (int i = ViscosityFirst; i < ViscosityFirst + 6; i++)
            {
                Assert.Greater(LiquidDemoPlayTests.Coverage(m[i].projectorMaterial), 0f,
                    m[i].transform.parent.parent.name + ": nothing landed");
            }

            for (int i = SaunaFirst; i < SaunaFirst + 2; i++)
            {
                Vector4 dew = m[i].projectorMaterial.GetVector("_Condensation");
                TestContext.WriteLine("sauna " + i + ": " + dew);
                Assert.Greater(dew.x, 0.3f, "the sauna did not condense on its mannequin");
            }

            Assert.AreEqual(0f, m[DampFirst].projectorMaterial.GetVector("_Condensation").y, "the dry-air mannequin felt humidity");
            Assert.AreEqual(0.7f, m[DampFirst + 1].projectorMaterial.GetVector("_Condensation").y, 0.01f,
                "the damp-air mannequin does not feel 70 % humidity");
            Assert.AreEqual(0f, m[DampFirst + 1].projectorMaterial.GetVector("_Condensation").x,
                "70 % humidity condensed although it is below the threshold");

            // The sauna treats bodies as bare skin: its swimwear mannequin, whose torso the builder
            // gives a cloth surface, sweats like skin. The bathroom keeps the clothes on.
            // Runtime state lives in the Udon program, not on the C# proxy.
            Assert.IsTrue((bool)UdonSharpEditorUtility.GetBackingUdonBehaviour(m[SaunaFirst + 1]).GetProgramVariable("_bareApplied"),
                "the sauna did not treat its mannequin as bare skin");
            float saunaTorso = m[SaunaFirst + 1].projectorMaterial.GetVector("_SurfaceBodyA").x;
            float bathTorso = m[BathroomFirst + 1].projectorMaterial.GetVector("_SurfaceBodyA").x;
            TestContext.WriteLine("torso absorbency: sauna " + saunaTorso + ", bathroom " + bathTorso);
            Assert.Less(saunaTorso, 0.4f, "the sauna torso still absorbs like cloth");
            Assert.Greater(bathTorso, 0.6f, "the bathroom torso lost its cloth surface");
            Assert.IsFalse((bool)UdonSharpEditorUtility.GetBackingUdonBehaviour(m[BathroomFirst + 1]).GetProgramVariable("_bareApplied"));

            Assert.Greater(LiquidDemoPlayTests.Coverage(m[SprayTargetLeft].projectorMaterial), 0f,
                "the carried nozzles did not reach the left target");
            Assert.Greater(LiquidDemoPlayTests.Coverage(m[SprayTargetRight].projectorMaterial), 0f,
                "the carried nozzles did not reach the right target");
            LiquidDemoPlayTests.CaptureGrid("liquid-interactive-spray-targets.png", m, SprayTargetLeft, 2);
            LiquidDemoPlayTests.CaptureGrid("liquid-interactive-sauna-bare.png", m, SaunaFirst, 2, 1.1f, 1.2f);

            Assert.Greater(LiquidDemoPlayTests.Coverage(m[InTheRain].projectorMaterial), 0f, "the rain patch did not wet its mannequin");
            Assert.AreEqual(0f, LiquidDemoPlayTests.Coverage(m[UnderUmbrella].projectorMaterial), "rain reached under the umbrella");
            Assert.AreEqual(0f, m[UnderUmbrella].projectorMaterial.GetVector("_Snow").y, "the mannequin under the umbrella soaked");

            // Dark room: lit, blacklight only, then dark.
            for (int i = DarkFirst; i < DarkFirst + 4; i++)
            {
                Assert.AreEqual(1f, m[i].projectorMaterial.GetVector("_LocalAmbient").w, "the light zone does not reach " + i);
                Assert.Greater(LiquidDemoPlayTests.Coverage(m[i].projectorMaterial), 0f, "no paint on dark room mannequin " + i);
            }

            Assert.Greater(Glow(m[DarkFirst + 1].projectorMaterial).x, 0f, "fluorescent paint left no fluorescence");
            Assert.Greater(Glow(m[DarkFirst + 3].projectorMaterial).y, 0f, "luminous paint left no luminescence");
            Assert.AreEqual(0f, Glow(m[DarkFirst].projectorMaterial).x + Glow(m[DarkFirst].projectorMaterial).y,
                "ordinary paint glows");

            LiquidDemoPlayTests.CaptureGrid("liquid-interactive-dark-lit.png", m, DarkFirst, 4, 1.8f, 1.1f);

            SetLight(zoneBacking, false, true);
            yield return Wait(1f);
            Assert.Greater(m[DarkFirst + 1].projectorMaterial.GetVector("_Glow").x, 0f, "the blacklight sends no ultraviolet");
            LiquidDemoPlayTests.CaptureGrid("liquid-interactive-dark-uv.png", m, DarkFirst, 4, 1.8f, 1.1f);

            SetLight(zoneBacking, false, false);
            yield return Wait(2f);
            Vector4 glow = m[DarkFirst + 3].projectorMaterial.GetVector("_Glow");
            TestContext.WriteLine("dark room glow after the lights went out: " + glow);
            Assert.AreEqual(0f, glow.x, "ultraviolet remains with the blacklight off");
            Assert.Greater(glow.y, 0.5f, "luminous paint lost its charge within two seconds");
            LiquidDemoPlayTests.CaptureGrid("liquid-interactive-dark.png", m, DarkFirst, 4, 1.8f, 1.1f);

            // The reset panel: clearing the mannequins empties a canvas nothing is spraying any more.
            Assert.Greater(LiquidDemoPlayTests.Coverage(m[BenchFirst].projectorMaterial), 0f);
            LiquidResetPanel reset = Object.FindObjectOfType<LiquidResetPanel>();
            UdonSharpEditorUtility.GetBackingUdonBehaviour(reset).SendCustomEvent(nameof(LiquidResetPanel.ClearMannequins));
            yield return Wait(0.5f);
            Assert.AreEqual(0f, LiquidDemoPlayTests.Coverage(m[BenchFirst].projectorMaterial), "clearing left liquid on the mannequin");
        }

        private static void SetLight(UdonBehaviour zone, bool lamp, bool blacklight)
        {
            zone.SetProgramVariable("_manual", true);
            zone.SetProgramVariable("_lampOn", lamp);
            zone.SetProgramVariable("_blacklightOn", blacklight);
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                yield return null;
            }
        }

        /// <summary>Total fluorescent (x) and luminous (y) share on the glow canvas.</summary>
        private static Vector2 Glow(Material projector)
        {
            var texture = projector.GetTexture("_GlowTex") as RenderTexture;
            if (texture == null)
            {
                return Vector2.zero;
            }

            var readback = new Texture2D(texture.width, texture.height, TextureFormat.RGBAHalf, false, true);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = texture;
            readback.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            readback.Apply();
            RenderTexture.active = previous;

            Vector2 total = Vector2.zero;
            foreach (Color c in readback.GetPixels())
            {
                total += new Vector2(c.r, c.g);
            }

            Object.DestroyImmediate(readback);
            return total;
        }
    }
}
