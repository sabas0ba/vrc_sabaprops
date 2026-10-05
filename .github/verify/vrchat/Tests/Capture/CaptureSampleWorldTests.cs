using System.Collections;
using System.IO;
using NUnit.Framework;
using SabaProps.Capture.Editors;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VRC.SDK3.ClientSim;
using VRC.SDKBase;
using VRC.Udon;

namespace SabaProps.Capture.WorldTests
{
    /// <summary>
    /// Runs the sample scene as a world under ClientSim and drives the recorders through
    /// their UdonBehaviours.
    /// <para>
    /// CaptureRecorderTests calls the C# proxy in edit mode, so it never goes through the
    /// Udon VM and cannot call Object.Destroy. Here the compiled programs run: the
    /// RenderTexture constructor, Camera.Render, VRCGraphics.Blit and Destroy are resolved
    /// as Udon externs, and Start and Update arrive as Udon events. An extern the VM does
    /// not expose halts the behaviour with an error log, which fails the test.
    /// </para>
    /// <para>
    /// This is still the editor's GPU and ClientSim's event loop. It says nothing about
    /// Quest or about the client's own Blit path; see Documentation~/build-and-test.md.
    /// </para>
    /// <para>
    /// One test, not several: entering and leaving play mode is per-fixture state.
    /// </para>
    /// </summary>
    public class CaptureSampleWorldTests
    {
        private const double Timeout = 20.0;

        private bool _restoreOptionsEnabled;
        private EnterPlayModeOptions _restoreOptions;

        [OneTimeSetUp]
        public void CompilePrograms()
        {
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // ClientSim logs its startup exception again on the way down.
            LogAssert.ignoreFailingMessages = true;
            if (Application.isPlaying)
            {
                yield return new ExitPlayMode();
            }

            ClientSimRuntimeLoader.EndUnityTesting();
            EditorSettings.enterPlayModeOptions = _restoreOptions;
            EditorSettings.enterPlayModeOptionsEnabled = _restoreOptionsEnabled;
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator Sample_RecordsThinsStopsAndReleasesThroughUdon()
        {
            _restoreOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            _restoreOptions = EditorSettings.enterPlayModeOptions;

            CaptureSampleScene.Create();

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

            // The startup dialog sits in front of the player and would cover the overview image.
            // It is only there to close a few frames after the player appears.
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

            UdonBehaviour textureRecorder = Backing<CaptureRecorder>(CaptureSampleScene.TextureRecorderName);
            UdonBehaviour cameraRecorder = Backing<CaptureRecorder>(CaptureSampleScene.CameraRecorderName);
            CapturePlayer texturePanel = GameObject.Find(CaptureSampleScene.TexturePanelName).GetComponent<CapturePlayer>();
            UdonBehaviour texturePlayer = UdonSharpEditorUtility.GetBackingUdonBehaviour(texturePanel);

            // --- Record On Start: both recorders fill past the fifth slot on their own.
            double deadline = EditorApplication.timeSinceStartup + Timeout;
            while ((Count(textureRecorder) < 5 || Count(cameraRecorder) < 5)
                   && EditorApplication.timeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(Count(textureRecorder), Is.GreaterThanOrEqualTo(5), "the texture recorder did not capture on its own");
            Assert.That(Count(cameraRecorder), Is.GreaterThanOrEqualTo(5), "the camera recorder did not capture on its own");

            RenderTexture[] textureFrames = (RenderTexture[])textureRecorder.GetProgramVariable("frames");
            RenderTexture[] cameraFrames = (RenderTexture[])cameraRecorder.GetProgramVariable("frames");
            foreach (RenderTexture frame in new[] { textureFrames[0], cameraFrames[0] })
            {
                Assert.That(frame, Is.Not.Null);
                Assert.That(frame.IsCreated(), Is.True);
                Assert.That(frame.width, Is.EqualTo(384));
                Assert.That(frame.height, Is.EqualTo(216));
                Assert.That(frame.depth, Is.EqualTo(0));
                Assert.That(Brightness(frame), Is.GreaterThan(0.05f), $"{frame.name} holds no picture");
            }

            // The camera path renders through a staging buffer with depth and must put the
            // camera back the way it found it.
            RenderTexture staging = (RenderTexture)cameraRecorder.GetProgramVariable("staging");
            Assert.That(staging, Is.Not.Null);
            Assert.That(staging.depth, Is.GreaterThan(0));
            Camera timelapse = GameObject.Find(CaptureSampleScene.TimelapseCameraName).GetComponent<Camera>();
            Assert.That(timelapse.enabled, Is.False);
            Assert.That(timelapse.targetTexture, Is.Null, "the recorder left its staging buffer on the camera");

            // The subject orbits, so frames taken seconds apart must not be the same picture.
            Assert.That(Difference(cameraFrames[0], cameraFrames[4]), Is.GreaterThan(1e-4f),
                "frames taken four seconds apart are identical");

            // --- The player runs in Udon as well: it follows the newest frame and reports state.
            yield return null;
            Assert.That(texturePanel.display.texture, Is.Not.Null, "the panel shows no frame");
            Assert.That(texturePanel.statusLabel.text, Does.StartWith("REC"));
            SaveOverview("capture-sample.png");
            // A capture that lands between the operation and the player's next Update must
            // not pull the playhead back to the frame that was on screen before.
            texturePlayer.SendCustomEvent("_First");
            textureRecorder.SendCustomEvent("_CaptureNow");
            for (int frame = 0; frame < 5; frame++)
            {
                yield return null;
            }

            Assert.That(texturePanel.positionLabel.text, Does.StartWith("1 / "),
                "a capture right after FIRST undid the operation");
            Assert.That(texturePanel.display.texture, Is.EqualTo(textureFrames[0]));
            texturePlayer.SendCustomEvent("_Latest");
            yield return null;

            // --- Thin: a smaller capacity is applied on restart, and filling it doubles the interval.
            textureRecorder.SendCustomEvent("_Clear");
            textureRecorder.SetProgramVariable("interval", 0.2f);
            textureRecorder.SetProgramVariable("maxFrames", 4);
            textureRecorder.SendCustomEvent("_StartRecording");
            Assert.That((int)textureRecorder.GetProgramVariable("capacity"), Is.EqualTo(4));
            Assert.That((int)textureRecorder.GetProgramVariable("allocatedFrames"), Is.LessThanOrEqualTo(4),
                "frames beyond the new capacity were not destroyed");

            // The first frame is stamped with the time from the start call to the next Update,
            // so it is read back rather than assumed to be zero.
            deadline = EditorApplication.timeSinceStartup + Timeout;
            while (Count(textureRecorder) < 1 && EditorApplication.timeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(Count(textureRecorder), Is.GreaterThanOrEqualTo(1), "recording did not restart");
            double firstFrameTime = ((double[])textureRecorder.GetProgramVariable("frameTimes"))[0];

            deadline = EditorApplication.timeSinceStartup + Timeout;
            while ((double)textureRecorder.GetProgramVariable("currentInterval") < 0.3
                   && EditorApplication.timeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That((double)textureRecorder.GetProgramVariable("currentInterval"), Is.EqualTo(0.4).Within(1e-6),
                "filling the buffer did not double the interval");
            Assert.That(Count(textureRecorder), Is.LessThanOrEqualTo(4));
            double[] times = (double[])textureRecorder.GetProgramVariable("frameTimes");
            int head = (int)textureRecorder.GetProgramVariable("head");
            Assert.That(times[head], Is.EqualTo(firstFrameTime).Within(1e-9),
                "thinning dropped the first frame of the session");
            textureRecorder.SendCustomEvent("_StopRecording");

            // --- Stop: recording ends on the frame that fills the buffer.
            cameraRecorder.SendCustomEvent("_Clear");
            cameraRecorder.SetProgramVariable("interval", 0.2f);
            cameraRecorder.SetProgramVariable("maxFrames", 3);
            cameraRecorder.SetProgramVariable("fullPolicy", CaptureRecorder.PolicyStop);
            cameraRecorder.SendCustomEvent("_StartRecording");

            deadline = EditorApplication.timeSinceStartup + Timeout;
            while ((bool)cameraRecorder.GetProgramVariable("recording")
                   && EditorApplication.timeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That((bool)cameraRecorder.GetProgramVariable("recording"), Is.False, "the stop policy never stopped");
            Assert.That(Count(cameraRecorder), Is.EqualTo(3));

            // --- Release: every frame and the staging buffer go back.
            cameraRecorder.SendCustomEvent("_ReleaseFrames");
            textureRecorder.SendCustomEvent("_ReleaseFrames");
            foreach (UdonBehaviour recorder in new[] { cameraRecorder, textureRecorder })
            {
                Assert.That(Count(recorder), Is.EqualTo(0));
                Assert.That((int)recorder.GetProgramVariable("allocatedFrames"), Is.EqualTo(0));
            }

            // Destroy takes effect at the end of a player-loop frame, and this coroutine is
            // not stepped in lockstep with that loop, so wait for it rather than count yields.
            deadline = EditorApplication.timeSinceStartup + Timeout;
            while ((staging != null || cameraFrames[0] != null) && EditorApplication.timeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(staging == null, Is.True, "the staging buffer was not destroyed");
            Assert.That(cameraFrames[0] == null, Is.True, "the frames were not destroyed");
        }

        private static UdonBehaviour Backing<T>(string name) where T : UdonSharp.UdonSharpBehaviour
        {
            GameObject go = GameObject.Find(name);
            Assert.That(go, Is.Not.Null, $"'{name}' is missing from the scene");
            UdonBehaviour udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(go.GetComponent<T>());
            Assert.That(udon, Is.Not.Null, $"'{name}' has no UdonBehaviour");
            return udon;
        }

        private static int Count(UdonBehaviour recorder)
        {
            return (int)recorder.GetProgramVariable("count");
        }

        private static Color[] Pixels(RenderTexture source)
        {
            var pixels = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = source;
                pixels.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
                pixels.Apply();
                return pixels.GetPixels();
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(pixels);
            }
        }

        /// <summary>Mean of the colour channels. A frame nothing was written to reads as zero.</summary>
        private static float Brightness(RenderTexture source)
        {
            float sum = 0f;
            Color[] pixels = Pixels(source);
            foreach (Color pixel in pixels)
            {
                sum += pixel.r + pixel.g + pixel.b;
            }

            return sum / (pixels.Length * 3f);
        }

        /// <summary>Mean absolute difference between two frames of the same size.</summary>
        private static float Difference(RenderTexture a, RenderTexture b)
        {
            Color[] first = Pixels(a);
            Color[] second = Pixels(b);
            float sum = 0f;
            for (int i = 0; i < first.Length; i++)
            {
                sum += Mathf.Abs(first[i].r - second[i].r)
                       + Mathf.Abs(first[i].g - second[i].g)
                       + Mathf.Abs(first[i].b - second[i].b);
            }

            return sum / (first.Length * 3f);
        }

        /// <summary>
        /// Writes what a player standing at the spawn sees to TestResults, so the layout
        /// and the panels' contents can be reviewed by eye.
        /// </summary>
        private static void SaveOverview(string filename)
        {
            const int width = 1600;
            const int height = 900;

            Camera camera = new GameObject("Capture verification camera").AddComponent<Camera>();
            // Eye height, a step ahead of the spawn so the player's own avatar is behind the camera.
            camera.transform.position = CaptureSampleScene.SpawnPosition + new Vector3(0f, 1.6f, 0.5f);
            camera.transform.rotation = Quaternion.LookRotation(
                new Vector3(0f, 1.7f, 5f) - camera.transform.position, Vector3.up);
            camera.fieldOfView = 75f;

            RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
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
