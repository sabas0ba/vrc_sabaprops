using System.Collections;
using NUnit.Framework;
using SabaProps.Liquid.Editors;
using UdonSharp.Compiler;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using VRC.SDK3.ClientSim;
using VRC.SDKBase;

namespace SabaProps.Liquid.WorldTests
{
    /// <summary>
    /// Runs the demo world under ClientSim and looks at it.
    /// <para>
    /// The edit mode tests check how the scene is built. This checks that it
    /// works unattended: after a few seconds of play, the sprayers, showers and
    /// tanks have put liquid on every mannequin, with nobody touching anything.
    /// It also writes review images of both comparison rows to TestResults.
    /// </para>
    /// </summary>
    public class LiquidDemoPlayTests
    {
        private const float RunSeconds = 20f;

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

            int liquids = LiquidSourceBuilder.PresetNames.Length;
            CaptureGrid("liquid-demo-liquids.png", pool.mannequins, 0, liquids);
            CaptureGrid("liquid-demo-sources.png", pool.mannequins, liquids, pool.mannequins.Length - liquids);
            Capture("liquid-demo-overview.png", new Vector3(0f, 7f, -14f), new Vector3(0f, 0.5f, 2f), 60f);
            CaptureGrid("liquid-demo-immersion.png", pool.mannequins, liquids + 1, 2, 1.7f, 0.75f);
            foreach (LiquidBodyCanvas mannequin in pool.mannequins)
            {
                string bay = mannequin.transform.parent.parent.name;
                TestContext.WriteLine(bay + ": immersion levels " + mannequin.projectorMaterial.GetVector("_ImmersionLevels")
                    + " amounts " + mannequin.projectorMaterial.GetVector("_ImmersionAmounts"));
                float coverage = Coverage(mannequin.projectorMaterial);
                Assert.Greater(coverage, 0f, bay + ": nothing has landed on the mannequin after " + RunSeconds + " s");
            }
        }

        /// <summary>
        /// How much of the canvas holds pigment or film. The tank and the mud tub
        /// work through immersion, which the canvas does not store, so those two
        /// count their immersion amounts instead.
        /// </summary>
        private static float Coverage(Material projector)
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
        private static void CaptureGrid(string fileName, LiquidBodyCanvas[] mannequins, int first, int count)
        {
            CaptureGrid(fileName, mannequins, first, count, 2.6f, 1.05f);
        }

        private static void CaptureGrid(string fileName, LiquidBodyCanvas[] mannequins, int first, int count,
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

        private static void Capture(string fileName, Vector3 position, Vector3 lookAt, float fieldOfView)
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
