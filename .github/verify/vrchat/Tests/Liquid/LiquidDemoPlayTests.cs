using System.Collections;
using NUnit.Framework;
using SabaProps.Liquid.Editors;
using UdonSharp.Compiler;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UdonSharpEditor;
using VRC.SDK3.ClientSim;
using VRC.SDKBase;
using VRC.Udon;

namespace SabaProps.Liquid.WorldTests
{
    /// <summary>
    /// Runs the demo world under ClientSim and looks at it.
    /// <para>
    /// The edit mode tests check how the scene is built. This checks that it
    /// works unattended: after a few seconds of play, the sprayers, showers and
    /// tanks have put liquid on every mannequin, with nobody touching anything.
    /// It also writes review images of each row and yard to TestResults.
    /// </para>
    /// <para>
    /// The weather yards run on a cycle of a minute or more, so the test makes
    /// both fall continuously before entering play mode, then stops the snow to
    /// watch it melt. The mannequin under each roof must stay untouched.
    /// </para>
    /// </summary>
    public class LiquidDemoPlayTests
    {
        private const float RunSeconds = 20f;
        private const float MeltSeconds = 8f;

        // Order of the mannequins in the pool, as LiquidSampleScene.Create builds them.
        private const int ClothedFirst = 0;
        private const int ClothedCount = 6;
        private const int SourceFirst = ClothedFirst + ClothedCount;
        private const int SourceCount = 7;
        private const int RainFirst = SourceFirst + SourceCount;
        private const int SnowFirst = RainFirst + 5;
        private const int YardCount = 5;

        // In each yard, the four in the open come first and the sheltered one last.
        private const int ShelteredInYard = 4;

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
        public IEnumerator Demo_PutsLiquidOnEveryMannequinUnattended()
        {
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
            LiquidSampleScene.Create();
            EditorSceneManager.OpenScene(LiquidSampleScene.ScenePath);

            // Weather that falls throughout the run, rather than when the server clock says.
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

            // The SDK's input initialisation logs during startup; allow it only there.
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

            float until = Time.time + RunSeconds;
            while (Time.time < until)
            {
                yield return null;
            }

            LiquidCanvasPool pool = Object.FindObjectOfType<LiquidCanvasPool>();
            Assert.IsNotNull(pool.mannequins);

            Assert.AreEqual(SnowFirst + YardCount, pool.mannequins.Length, "the pool order changed; update the indices");

            CaptureGrid("liquid-demo-clothed.png", pool.mannequins, ClothedFirst, ClothedCount);
            CaptureGrid("liquid-demo-clothed-closeup.png", pool.mannequins, ClothedFirst, 3, 1.3f, 1.35f);
            CaptureGrid("liquid-demo-sources.png", pool.mannequins, SourceFirst, SourceCount);
            CaptureGrid("liquid-demo-immersion.png", pool.mannequins, SourceFirst + 1, 2, 1.7f, 0.75f);
            Capture("liquid-demo-overview.png", new Vector3(0f, 7f, -14f), new Vector3(0f, 0.5f, 2f), 60f);
            Capture("liquid-demo-yards.png", new Vector3(0f, 6f, 8.5f), new Vector3(0f, 0.8f, 18.5f), 70f);
            CaptureGrid("liquid-demo-rain.png", pool.mannequins, RainFirst, YardCount);
            CaptureGrid("liquid-demo-snow.png", pool.mannequins, SnowFirst, YardCount);
            CaptureGrid("liquid-demo-snow-closeup.png", pool.mannequins, SnowFirst, 4, 1.3f, 1.35f);

            for (int i = 0; i < RainFirst; i++)
            {
                LiquidBodyCanvas mannequin = pool.mannequins[i];
                string bay = mannequin.transform.parent.parent.name;
                TestContext.WriteLine(bay + ": immersion levels " + mannequin.projectorMaterial.GetVector("_ImmersionLevels")
                    + " amounts " + mannequin.projectorMaterial.GetVector("_ImmersionAmounts"));
                float coverage = Coverage(mannequin.projectorMaterial);
                Assert.Greater(coverage, 0f, bay + ": nothing has landed on the mannequin after " + RunSeconds + " s");
            }

            for (int i = 0; i < YardCount; i++)
            {
                LiquidBodyCanvas rained = pool.mannequins[RainFirst + i];
                LiquidBodyCanvas snowed = pool.mannequins[SnowFirst + i];
                Vector4 snow = snowed.projectorMaterial.GetVector("_Snow");
                TestContext.WriteLine("rain " + i + ": coverage " + Coverage(rained.projectorMaterial) + ", snow " + i + ": " + snow);

                if (i == ShelteredInYard)
                {
                    Assert.AreEqual(0f, Coverage(rained.projectorMaterial), "rain reached the mannequin under the roof");
                    Assert.AreEqual(0f, rained.projectorMaterial.GetVector("_Snow").y, "rain soaked the mannequin under the roof");
                    Assert.AreEqual(0f, snow.x, "snow reached the mannequin under the roof");
                }
                else
                {
                    Assert.Greater(Coverage(rained.projectorMaterial), 0f, "rain " + i + ": nothing landed after " + RunSeconds + " s");
                    Assert.Greater(rained.projectorMaterial.GetVector("_Snow").y, 0f, "rain " + i + ": the body did not soak");
                    Assert.Greater(snow.x, 0f, "snow " + i + ": no snow settled after " + RunSeconds + " s");
                }
            }

            LiquidWeather snowfall = System.Array.Find(Object.FindObjectsOfType<LiquidWeather>(), w => w.snow);
            Assert.Greater(snowfall.groundMaterials[0].GetVector("_WeatherState").y, 0f, "the snow yard's ground stayed bare");

            // Stop the snow and let it melt.
            Vector4 before = pool.mannequins[SnowFirst].projectorMaterial.GetVector("_Snow");
            UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(snowfall);
            backing.SetProgramVariable("precipitationSeconds", 0f);
            until = Time.time + MeltSeconds;
            while (Time.time < until)
            {
                yield return null;
            }

            Vector4 after = pool.mannequins[SnowFirst].projectorMaterial.GetVector("_Snow");
            TestContext.WriteLine("snow before melting " + before + ", after " + after);
            Assert.Less(after.x, before.x, "the snow did not melt once it stopped falling");
            Assert.Greater(after.y, 0f, "melting snow left no water behind");
            CaptureGrid("liquid-demo-snow-melting.png", pool.mannequins, SnowFirst, YardCount);
        }

        /// <summary>
        /// Runs the comparison world unattended and writes one review image per
        /// row: liquids, surfaces, body colours and liquid colours.
        /// </summary>
        [UnityTest]
        public IEnumerator Comparison_PutsLiquidOnEveryMannequinUnattended()
        {
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
            LiquidSampleScene.CreateComparison();
            EditorSceneManager.OpenScene(LiquidSampleScene.ComparisonScenePath);

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
            var menu = Object.FindObjectOfType<ClientSimMenu>(true);
            if (menu != null)
            {
                menu.CloseMenu();
            }

            float until = Time.time + RunSeconds;
            while (Time.time < until)
            {
                yield return null;
            }

            LiquidBodyCanvas[] m = Object.FindObjectOfType<LiquidCanvasPool>().mannequins;
            int liquids = LiquidSourceBuilder.PresetNames.Length;
            int surfaces = liquids;
            int bodies = surfaces + LiquidSurfaceBuilder.PresetNames.Length + 1;
            int colours = bodies + 7;
            Assert.AreEqual(colours + LiquidSourceBuilder.GreyscalePaintNames.Length + 2, m.Length,
                "the pool order changed; update the indices");

            CaptureGrid("liquid-comparison-liquids.png", m, 0, liquids);
            CaptureGrid("liquid-comparison-surfaces.png", m, surfaces, bodies - surfaces);
            CaptureGrid("liquid-comparison-surfaces-closeup.png", m, surfaces, bodies - surfaces, 1.3f, 1.35f);
            CaptureGrid("liquid-comparison-body-colours.png", m, bodies, colours - bodies);
            CaptureGrid("liquid-comparison-liquid-colours.png", m, colours, m.Length - colours);
            Capture("liquid-comparison-overview.png", new Vector3(0f, 8f, -10f), new Vector3(0f, 0.5f, 10f), 70f);

            foreach (LiquidBodyCanvas mannequin in m)
            {
                Assert.Greater(Coverage(mannequin.projectorMaterial), 0f,
                    mannequin.transform.parent.parent.name + ": nothing has landed after " + RunSeconds + " s");
            }
        }

        /// <summary>
        /// How much of the canvas holds pigment or film. The tank and the mud tub
        /// work through immersion, which the canvas does not store, so those two
        /// count their immersion amounts instead.
        /// </summary>
        internal static float Coverage(Material projector)
        {
            Vector4 immersion = projector.GetVector("_ImmersionAmounts");
            if (immersion.x > 0f || immersion.y > 0f)
            {
                return immersion.x + immersion.y;
            }

            float total = 0f;
            foreach (string name in new[] { "_PigmentTex", "_FilmTex" })
            {
                var texture = projector.GetTexture(name) as RenderTexture;
                if (texture == null)
                {
                    continue;
                }

                var readback = new Texture2D(texture.width, texture.height, TextureFormat.RGBAHalf, false, true);
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = texture;
                readback.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                readback.Apply();
                RenderTexture.active = previous;

                foreach (Color c in readback.GetPixels())
                {
                    total += name == "_PigmentTex" ? c.a : c.r;
                }

                Object.DestroyImmediate(readback);
            }

            return total;
        }

        /// <summary>
        /// One portrait per mannequin, from the walkway side of its bay, side by
        /// side in a single image so the bays can be compared directly.
        /// </summary>
        internal static void CaptureGrid(string fileName, LiquidBodyCanvas[] mannequins, int first, int count)
        {
            CaptureGrid(fileName, mannequins, first, count, 2.6f, 1.05f);
        }

        internal static void CaptureGrid(string fileName, LiquidBodyCanvas[] mannequins, int first, int count,
            float distance, float lookHeight)
        {
            const int tileWidth = 300;
            const int tileHeight = 460;
            var target = new RenderTexture(tileWidth * count, tileHeight, 24, RenderTextureFormat.ARGB32);
            target.Create();

            var cameraObject = new GameObject("Review Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.targetTexture = target;
            camera.fieldOfView = 38f;
            camera.cullingMask = ~(1 << 19);

            for (int i = 0; i < count; i++)
            {
                Transform bay = mannequins[first + i].transform.parent.parent;
                camera.rect = new Rect((float)i / count, 0f, 1f / count, 1f);
                camera.transform.position = bay.position + bay.forward * distance + Vector3.up * (lookHeight + 0.25f);
                camera.transform.LookAt(bay.position + Vector3.up * lookHeight);
                camera.Render();
            }

            Save(target, fileName);
            camera.targetTexture = null;
            Object.DestroyImmediate(cameraObject);
            target.Release();
            Object.DestroyImmediate(target);
        }

        private static void Save(RenderTexture target, string fileName)
        {
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;

            string directory = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "TestResults");
            System.IO.Directory.CreateDirectory(directory);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, fileName), image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }

        internal static void Capture(string fileName, Vector3 position, Vector3 lookAt, float fieldOfView)
        {
            const int width = 1280;
            const int height = 720;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            target.Create();

            var cameraObject = new GameObject("Review Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = position;
            camera.transform.LookAt(lookAt);
            camera.fieldOfView = fieldOfView;
            camera.targetTexture = target;
            // ClientSim's menu layer stays out of review images.
            camera.cullingMask = ~(1 << 19);
            camera.Render();

            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            camera.targetTexture = null;

            string directory = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "TestResults");
            System.IO.Directory.CreateDirectory(directory);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, fileName), image.EncodeToPNG());

            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(image);
            target.Release();
            Object.DestroyImmediate(target);
        }
    }
}
