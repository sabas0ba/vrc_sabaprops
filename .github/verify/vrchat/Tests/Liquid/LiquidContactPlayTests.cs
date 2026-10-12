using System.Collections;
using NUnit.Framework;
using SabaProps.Liquid.Editors;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VRC.SDK3.ClientSim;
using VRC.Udon;

namespace SabaProps.Liquid.WorldTests
{
    public class LiquidContactPlayTests
    {
        private bool _optionsEnabled;
        private EnterPlayModeOptions _options;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            ClientSimRuntimeLoader.EndUnityTesting();
            EditorSettings.enterPlayModeOptionsEnabled = _optionsEnabled;
            EditorSettings.enterPlayModeOptions = _options;
            LogAssert.ignoreFailingMessages = false;
        }

        [UnityTest]
        public IEnumerator Wipe_TransfersActualPaintToCloth_AndLocalToggleHidesProjectors()
        {
            _optionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            _options = EditorSettings.enterPlayModeOptions;
            LiquidContactScene.Create();
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
            var pen = GameObject.Find("Wet red pen").GetComponentInChildren<LiquidPaintTool>();
            var penBacking = UdonSharpEditorUtility.GetBackingUdonBehaviour(pen);
            penBacking.SetProgramVariable("pickup", null);
            pen.transform.parent.rotation = Quaternion.identity;
            pen.transform.parent.position += new Vector3(-1.7f, 1.4f, 1.89f) - pen.tip.position;
            Physics.SyncTransforms();
            penBacking.SendCustomEvent(nameof(LiquidPaintTool.Trigger));
            yield return Wait(0.6f);
            penBacking.SendCustomEvent(nameof(LiquidPaintTool.Release));
            Assert.Greater((int)penBacking.GetProgramVariable("_strokesReceived"), 0,
                "no paint event; tip=" + pen.tip.position + " direction=" + pen.tip.forward);
            LiquidBodyCanvas surface = GameObject.Find("Contact Surfaces").GetComponentInChildren<LiquidBodyCanvas>();
            float before = Sum(surface.projectorMaterial);
            Assert.Greater(before, 0.001f, "pen did not paint the wall");
            var wipe = GameObject.Find("Sponge").GetComponentInChildren<LiquidWipeTool>();
            var wipeBacking = UdonSharpEditorUtility.GetBackingUdonBehaviour(wipe);
            wipeBacking.SetProgramVariable("pickup", null);
            wipe.transform.parent.position += new Vector3(-1.7f, 1.4f, 1.90f) - wipe.tip.position;
            Physics.SyncTransforms();
            wipeBacking.SendCustomEvent(nameof(LiquidWipeTool.WipeOnce));
            yield return Wait(0.4f);
            Assert.Less(Sum(surface.projectorMaterial), before, "wall was not wiped");
            Assert.Greater(Sum(wipe.clothCanvas.projectorMaterial), 0.001f, "cloth did not receive paint");
            var contact = Object.FindObjectOfType<LiquidContact>();
            UdonSharpEditorUtility.GetBackingUdonBehaviour(contact).SendCustomEvent(nameof(LiquidContact.HideLiquid));
            yield return Wait(0.2f);
            Assert.IsFalse(surface.projectorObject.activeSelf);
            UdonSharpEditorUtility.GetBackingUdonBehaviour(contact).SendCustomEvent(nameof(LiquidContact.ShowLiquid));
            yield return Wait(0.2f);
            Assert.IsTrue(surface.projectorObject.activeSelf);
            var balloon = Object.FindObjectOfType<LiquidWaterBalloon>();
            var balloonBacking = UdonSharpEditorUtility.GetBackingUdonBehaviour(balloon);
            balloonBacking.SetProgramVariable("respawnSeconds", 1f);
            balloon.transform.position = new Vector3(-1.7f, 1.5f, 0.8f);
            balloon.body.isKinematic = false;
            balloon.body.velocity = Vector3.forward * 6;
            Physics.SyncTransforms();
            yield return Wait(0.6f);
            Assert.IsTrue((bool)balloonBacking.GetProgramVariable("_burst"), "balloon did not burst on wall impact");
            Assert.Greater((int)UdonSharpEditorUtility.GetBackingUdonBehaviour(balloon.splash).GetProgramVariable("_received"), 0);
            yield return Wait(1.1f);
            Assert.IsFalse((bool)balloonBacking.GetProgramVariable("_burst"), "balloon did not respawn");
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        private static float Sum(Material material)
        {
            var texture = material.GetTexture("_PigmentTex") as RenderTexture;
            Assert.IsNotNull(texture);
            RenderTexture previous = RenderTexture.active;
            var read = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true);
            try
            {
                RenderTexture.active = texture;
                read.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                read.Apply();
                float sum = 0;
                foreach (Color32 pixel in read.GetPixels32()) sum += pixel.a / 255f;
                return sum;
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(read); }
        }
    }
}
