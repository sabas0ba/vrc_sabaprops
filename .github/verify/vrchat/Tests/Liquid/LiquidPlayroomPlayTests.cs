using System.Collections;
using NUnit.Framework;
using SabaProps.Liquid.Editors;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VRC.SDK3.ClientSim;

namespace SabaProps.Liquid.WorldTests
{
    public class LiquidPlayroomPlayTests
    {
        private bool _optionsEnabled;
        private EnterPlayModeOptions _options;

        protected virtual void CreateScene() { LiquidPlayroomScene.Create(); }
        protected virtual void CaptureSurface(string phase) { }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            ClientSimRuntimeLoader.EndUnityTesting();
            EditorSettings.enterPlayModeOptionsEnabled = _optionsEnabled;
            EditorSettings.enterPlayModeOptions = _options;
            LogAssert.ignoreFailingMessages = false;
            CaptureSurface("clean");
        }

        [UnityTest]
        public IEnumerator Sprayer_PaintsFloorAndWall()
        {
            _optionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            _options = EditorSettings.enterPlayModeOptions;
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
            CreateScene();
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            ClientSimRuntimeLoader.BeginUnityTesting(new ClientSimSettings {
                enableClientSim = true, initializationDelay = 0, spawnPlayer = true,
                localPlayerIsMaster = true, displayLogs = false });
            LogAssert.ignoreFailingMessages = true;
            yield return new EnterPlayMode();
            for (int i = 0; i < 120; i++) yield return null;
            LogAssert.ignoreFailingMessages = false;
            var surface = Object.FindObjectOfType<LiquidCanvasPool>().surfaces[0];
            var nozzle = GameObject.Find("Red Paint sprayer").GetComponentInChildren<LiquidNozzle>();
            var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(nozzle);
            foreach (Vector3 direction in new[] { Vector3.down, Vector3.right })
            {
                UdonSharpEditorUtility.GetBackingUdonBehaviour(surface).SendCustomEvent(nameof(LiquidBodyCanvas.Clear));
                for (int i = 0; i < 5; i++) yield return null;
                nozzle.transform.parent.rotation = Quaternion.LookRotation(direction);
                nozzle.transform.parent.position += new Vector3(4, 1, 0) - nozzle.nozzle.position;
                Physics.SyncTransforms();
                backing.SendCustomEvent(nameof(LiquidNozzle.Trigger));
                float deadline = Time.realtimeSinceStartup + 2;
                while (Time.realtimeSinceStartup < deadline) yield return null;
                backing.SendCustomEvent(nameof(LiquidNozzle.Release));
                var texture = surface.projectorMaterial.GetTexture("_PigmentTex") as RenderTexture;
                Assert.IsNotNull(texture);
                RenderTexture previous = RenderTexture.active;
                var read = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true);
                try
                {
                    RenderTexture.active = texture;
                    read.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                    read.Apply();
                    int pigment = 0;
                    foreach (Color32 pixel in read.GetPixels32()) pigment += pixel.a;
                    Assert.Greater(pigment, 0, "sprayer did not paint direction " + direction);
                }
                finally { RenderTexture.active = previous; Object.DestroyImmediate(read); }
                CaptureSurface(direction == Vector3.down ? "floor" : "wall");
            }
        }
    }
}
