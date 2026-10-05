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
using VRC.SDKBase;
using VRC.Udon;

namespace SabaProps.Liquid.WorldTests
{
    /// <summary>
    /// Runs the painting world under ClientSim and uses its tools.
    /// <para>
    /// Draws lines with the pens, places every stamp and rubs part of it out,
    /// each through the tool's own path: aim from the tip, the network event,
    /// and the canvas the hit belongs to. Then replays the drawing from the
    /// paint log the way a late joiner receives it. Also checks that the
    /// unattended Sources reach the walls and the paving, and that the dark
    /// studio's light reaches its Surface Canvas. Writes review images to
    /// TestResults.
    /// </para>
    /// <para>
    /// ClientSim has one player, so what this cannot show is the drawing
    /// arriving on another client. The tools are driven without being held:
    /// the test clears each tool's pickup reference, which is what makes a
    /// tool act for the local client.
    /// </para>
    /// </summary>
    public class LiquidPaintingPlayTests
    {
        // The studio's back wall is at z = 4.85; tools are held here, facing it.
        private const float HoldZ = 3.2f;

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
        public IEnumerator Painting_ToolsSourcesAndTheLogBehave()
        {
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            LiquidSampleScene.EnsurePrefabs();
            LiquidPaintingScene.Create();
            EditorSceneManager.OpenScene(LiquidPaintingScene.ScenePath);

            // Rain that falls throughout the run, rather than when the server clock says.
            foreach (LiquidWeather weather in Object.FindObjectsOfType<LiquidWeather>())
            {
                weather.clearSeconds = 0f;
                UdonSharpEditorUtility.CopyProxyToUdon(weather);
            }

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
            Assert.Greater(Networking.LocalPlayer.GetJumpImpulse(), 0f, "the world does not let players jump");
            Assert.Greater(Networking.LocalPlayer.GetWalkSpeed(), 2f, "the world leaves the walk speed at VRChat's default");
            var menu = Object.FindObjectOfType<ClientSimMenu>(true);
            if (menu != null)
            {
                menu.CloseMenu();
            }

            LiquidCanvasPool pool = Object.FindObjectOfType<LiquidCanvasPool>();
            UdonBehaviour log = UdonSharpEditorUtility.GetBackingUdonBehaviour(pool.paintLog);
            LiquidBodyCanvas studio = LiquidPaintingSceneTests.Surface(LiquidPaintingScene.StudioName);
            LiquidBodyCanvas darkStudio = LiquidPaintingSceneTests.Surface(LiquidPaintingScene.DarkStudioName);
            LiquidBodyCanvas patio = LiquidPaintingSceneTests.Surface(LiquidPaintingScene.RainPatioName);
            GameObject table = GameObject.Find(LiquidPaintingScene.ToolTableName);

            // The lamp stays on while the paint goes on, whatever the clock says.
            LiquidLightZone zone = GameObject.Find(LiquidPaintingScene.DarkStudioName).GetComponentInChildren<LiquidLightZone>();
            UdonBehaviour zoneBacking = UdonSharpEditorUtility.GetBackingUdonBehaviour(zone);
            SetLight(zoneBacking, true, false);

            Assert.AreEqual(0f, Pigment(studio.projectorMaterial), "the studio starts with something on it");
            LiquidDemoPlayTests.Capture("liquid-painting-overview.png", new Vector3(0f, 9f, -12f), new Vector3(0f, 0.5f, 1.5f), 60f);

            // --- Pens: three lines across the back wall ---------------------------------------
            string[] inks = { LiquidSourceBuilder.BlackInkName, LiquidSourceBuilder.RedInkName, LiquidSourceBuilder.BlueInkName };
            int strokes = 0;
            for (int i = 0; i < inks.Length; i++)
            {
                LiquidPaintTool pen = Tool(table, inks[i] + " Pen");
                float y = 2.5f - i * 0.25f;
                yield return Draw(pen, new Vector3(-2.3f, y, HoldZ), new Vector3(0.9f, y + (i == 1 ? 0.25f : 0f), HoldZ), 1.2f, i == 2);
                int received = Received(pen);
                TestContext.WriteLine(inks[i] + " pen sent " + received + " strokes");
                Assert.Greater(received, 5, inks[i] + " pen drew almost nothing");
                strokes += received;
            }

            yield return Wait(0.3f);
            float afterPens = Pigment(studio.projectorMaterial);
            Assert.Greater(afterPens, 0f, "the pens left nothing on the studio wall");

            // --- Stamps: one of each shape in a row, and one on the floor ----------------------
            LiquidPaintTool[] stamps = System.Array.FindAll(table.GetComponentsInChildren<LiquidPaintTool>(),
                t => t.mode == LiquidPaintTool.ModeStamp);
            Assert.AreEqual(LiquidPaintingScene.StampShapes.Length, stamps.Length);
            for (int i = 0; i < stamps.Length; i++)
            {
                yield return Stamp(stamps[i], new Vector3(-2.1f + i * 0.7f, 1.45f, HoldZ), Vector3.forward);
                Assert.AreEqual(1, Received(stamps[i]), stamps[i].transform.parent.name + " did not stamp once");
                strokes++;
            }

            yield return Stamp(stamps[0], new Vector3(0.6f, 1.2f, 2.2f), new Vector3(0f, -1f, 0.35f));
            Assert.AreEqual(2, Received(stamps[0]), "the stamp did not land on the floor");
            strokes++;

            yield return Wait(0.3f);
            float afterStamps = Pigment(studio.projectorMaterial);
            Assert.Greater(afterStamps, afterPens * 1.1f, "the stamps added little to the wall");
            Assert.AreEqual(strokes, (int)log.GetProgramVariable("_count"), "the paint log did not record every stroke on the studio");
            LiquidDemoPlayTests.Capture("liquid-painting-studio.png", new Vector3(-0.6f, 1.7f, 0.2f), new Vector3(-0.6f, 1.75f, 4.85f), 62f);
            LiquidDemoPlayTests.Capture("liquid-painting-floor.png", new Vector3(0.6f, 2.2f, 1.2f), new Vector3(0.6f, 0f, 2.7f), 50f);

            // --- Eraser: rub out the right end of the lines ------------------------------------
            LiquidPaintTool eraser = Tool(table, "Wide Eraser");
            yield return Draw(eraser, new Vector3(0.55f, 2.75f, HoldZ), new Vector3(0.55f, 1.95f, HoldZ), 0.8f, false);
            Assert.Greater(Received(eraser), 2, "the eraser did nothing");
            strokes += Received(eraser);
            yield return Wait(0.3f);
            float afterEraser = Pigment(studio.projectorMaterial);
            Assert.Less(afterEraser, afterStamps * 0.98f, "the eraser removed nothing");
            Assert.Greater(afterEraser, afterStamps * 0.5f, "the eraser removed far more than it touched");
            LiquidDemoPlayTests.Capture("liquid-painting-erased.png", new Vector3(-0.6f, 1.7f, 0.2f), new Vector3(-0.6f, 1.75f, 4.85f), 62f);

            // --- Late join: the log hands over the strokes, and they draw again ----------------
            yield return Wait(1f);
            float settled = Pigment(studio.projectorMaterial);
            log.SendCustomEvent("_onPreSerialization");
            LiquidResetPanel reset = Object.FindObjectOfType<LiquidResetPanel>();
            UdonSharpEditorUtility.GetBackingUdonBehaviour(reset).SendCustomEvent(nameof(LiquidResetPanel.ClearSurfaces));
            yield return Wait(0.5f);
            Assert.AreEqual(0f, Pigment(studio.projectorMaterial), "clearing the walls left something on the studio");
            Assert.AreEqual(0, (int)log.GetProgramVariable("_count"), "clearing the walls kept the paint log");

            log.SendCustomEvent("_onDeserialization");
            yield return Wait(3f);
            Assert.AreEqual(strokes, (int)log.GetProgramVariable("_replayed"), "the log did not replay every stroke");
            float replayed = Pigment(studio.projectorMaterial);
            TestContext.WriteLine("studio coverage: drawn " + settled + ", replayed " + replayed);
            Assert.Greater(replayed, settled * 0.6f, "the replay drew much less than the original");
            Assert.Less(replayed, settled * 1.4f, "the replay drew much more than the original");
            LiquidDemoPlayTests.Capture("liquid-painting-replayed.png", new Vector3(-0.6f, 1.7f, 0.2f), new Vector3(-0.6f, 1.75f, 4.85f), 62f);

            // A second hand-over changes nothing: those present already have the strokes.
            log.SendCustomEvent("_onDeserialization");
            yield return Wait(1.5f);
            Assert.AreEqual(strokes, (int)log.GetProgramVariable("_replayed"), "a later hand-over drew the strokes again");

            // --- Unattended Sources on the world ----------------------------------------------
            foreach (LiquidBodyCanvas bay in GameObject.Find(LiquidPaintingScene.WallTestName).GetComponentsInChildren<LiquidBodyCanvas>())
            {
                Assert.Greater(LiquidDemoPlayTests.Coverage(bay.projectorMaterial), 0f,
                    bay.transform.parent.parent.name + ": the sprayers left nothing on the wall");
            }

            Assert.Greater(LiquidDemoPlayTests.Coverage(patio.projectorMaterial), 0f, "rain left nothing on the patio");
            Vector3 wallTest = LiquidPaintingScene.WallTestCentre;
            LiquidDemoPlayTests.Capture("liquid-painting-walls.png", wallTest + new Vector3(0f, 1.6f, -3.6f), wallTest + new Vector3(0f, 1.4f, 1.2f), 62f);
            Vector3 patioCentre = LiquidPaintingScene.RainPatioCentre;
            LiquidDemoPlayTests.Capture("liquid-painting-rain.png", patioCentre + new Vector3(2.6f, 2.4f, -3.2f), patioCentre + new Vector3(-0.2f, 0.2f, 0f), 55f);

            // --- Dark studio: glowing ink under the lamp, the blacklight, and neither -----------
            Assert.Greater(LiquidDemoPlayTests.Coverage(darkStudio.projectorMaterial), 0f, "no paint on the dark studio wall");
            Assert.AreEqual(1f, darkStudio.projectorMaterial.GetVector("_LocalAmbient").w, "the light zone does not reach the dark studio's walls");

            GameObject dark = GameObject.Find(LiquidPaintingScene.DarkStudioName);
            Vector3 d = LiquidPaintingScene.DarkStudioCentre;
            float backWall = d.z + LiquidPaintingScene.DarkStudioSize.z * 0.5f - 0.15f;
            LiquidPaintTool green = Tool(dark, LiquidSourceBuilder.FluorescentGreenInkName + " Pen");
            LiquidPaintTool luminous = Tool(dark, LiquidSourceBuilder.LuminousInkName + " Pen");
            LiquidPaintTool white = Tool(dark, LiquidSourceBuilder.WhiteInkName + " Pen");
            yield return Draw(green, new Vector3(d.x - 1.6f, 1.5f, backWall - 1.5f), new Vector3(d.x + 1.6f, 1.2f, backWall - 1.5f), 0.9f, true);
            yield return Draw(luminous, new Vector3(d.x - 1.6f, 1.0f, backWall - 1.5f), new Vector3(d.x + 1.6f, 0.8f, backWall - 1.5f), 0.9f, false);
            yield return Draw(white, new Vector3(d.x - 1.6f, 0.55f, backWall - 1.5f), new Vector3(d.x + 1.6f, 0.45f, backWall - 1.5f), 0.9f, false);
            Assert.Greater(Received(green), 3);
            yield return Wait(0.5f);

            Vector3 eye = new Vector3(d.x, 1.5f, d.z - 1.9f);
            Vector3 look = new Vector3(d.x, 1.4f, backWall);
            LiquidDemoPlayTests.Capture("liquid-painting-dark-lit.png", eye, look, 70f);

            SetLight(zoneBacking, false, true);
            yield return Wait(1f);
            Assert.Greater(darkStudio.projectorMaterial.GetVector("_Glow").x, 0f, "the blacklight sends no ultraviolet to the walls");
            LiquidDemoPlayTests.Capture("liquid-painting-dark-uv.png", eye, look, 70f);

            SetLight(zoneBacking, false, false);
            yield return Wait(2f);
            Vector4 glow = darkStudio.projectorMaterial.GetVector("_Glow");
            Assert.AreEqual(0f, glow.x, "ultraviolet remains with the blacklight off");
            Assert.Greater(glow.y, 0.5f, "luminous ink lost its charge within two seconds");
            LiquidDemoPlayTests.Capture("liquid-painting-dark.png", eye, look, 70f);
        }

        /// <summary>The named tool under <paramref name="area"/>.</summary>
        private static LiquidPaintTool Tool(GameObject area, string name)
        {
            Transform root = area.transform.Find(name);
            Assert.IsNotNull(root, name + " is not under " + area.name);
            return root.GetComponentInChildren<LiquidPaintTool>(true);
        }

        private static int Received(LiquidPaintTool tool)
        {
            return (int)UdonSharpEditorUtility.GetBackingUdonBehaviour(tool).GetProgramVariable("_strokesReceived");
        }

        /// <summary>
        /// Holds the tool at <paramref name="from"/> facing +Z, presses use, moves
        /// it to <paramref name="to"/> over <paramref name="seconds"/> and
        /// releases. With <paramref name="wavy"/>, the tool bobs as it goes.
        /// </summary>
        private static IEnumerator Draw(LiquidPaintTool tool, Vector3 from, Vector3 to, float seconds, bool wavy)
        {
            Transform root = tool.transform.parent;
            Vector3 restPosition = root.position;
            Quaternion restRotation = root.rotation;
            UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(tool);
            // Without a pickup, the tool acts for the local client as if it were held.
            backing.SetProgramVariable("pickup", null);
            root.SetPositionAndRotation(from, Quaternion.identity);
            Physics.SyncTransforms();
            yield return null;

            backing.SendCustomEvent(nameof(LiquidPaintTool.Trigger));
            float start = Time.time;
            while (Time.time - start < seconds)
            {
                float t = (Time.time - start) / seconds;
                Vector3 position = Vector3.Lerp(from, to, t);
                if (wavy)
                {
                    position.y += Mathf.Sin(t * Mathf.PI * 6f) * 0.08f;
                }

                root.SetPositionAndRotation(position, Quaternion.identity);
                yield return null;
            }

            backing.SendCustomEvent(nameof(LiquidPaintTool.Release));
            yield return null;

            // Put it down: back where it lay, and no longer held, so its marker goes away.
            root.SetPositionAndRotation(restPosition, restRotation);
            backing.SetProgramVariable("pickup", root.GetComponent<VRC.SDK3.Components.VRCPickup>());
            yield return null;
            yield return null;
        }

        /// <summary>Holds the stamp at <paramref name="position"/> pointing along <paramref name="direction"/> and presses use once.</summary>
        private static IEnumerator Stamp(LiquidPaintTool tool, Vector3 position, Vector3 direction)
        {
            Transform root = tool.transform.parent;
            Vector3 restPosition = root.position;
            Quaternion restRotation = root.rotation;
            UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(tool);
            // Without a pickup, the tool acts for the local client as if it were held.
            backing.SetProgramVariable("pickup", null);
            root.SetPositionAndRotation(position, Quaternion.LookRotation(direction, Vector3.up));
            Physics.SyncTransforms();
            yield return null;

            backing.SendCustomEvent(nameof(LiquidPaintTool.Trigger));
            yield return null;
            backing.SendCustomEvent(nameof(LiquidPaintTool.Release));
            yield return null;

            // Put it down: back where it lay, and no longer held, so its marker goes away.
            root.SetPositionAndRotation(restPosition, restRotation);
            backing.SetProgramVariable("pickup", root.GetComponent<VRC.SDK3.Components.VRCPickup>());
            yield return null;
            yield return null;
        }

        /// <summary>
        /// The pigment on a canvas, summed over its texels. Unlike the coverage the
        /// other play tests use, this leaves the film out: ink dries within
        /// seconds, and a drying film would read as something having been erased.
        /// </summary>
        private static float Pigment(Material projector)
        {
            var texture = projector.GetTexture("_PigmentTex") as RenderTexture;
            if (texture == null)
            {
                return 0f;
            }

            var readback = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = texture;
            readback.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            readback.Apply();
            RenderTexture.active = previous;

            float total = 0f;
            foreach (Color32 c in readback.GetPixels32())
            {
                total += c.a / 255f;
            }

            Object.DestroyImmediate(readback);
            return total;
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
    }
}
