using System.Collections;
using NUnit.Framework;
using SabaProps.Liquid.Editors;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VRC.SDK3.ClientSim;
using VRC.SDKBase;
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
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
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
            Networking.LocalPlayer.TeleportTo(new Vector3(-5, 0.02f, -4), Quaternion.identity);
            yield return Wait(0.3f);
            LiquidDemoPlayTests.Capture("liquid-contact-overview.png", new Vector3(0, 3, -5), new Vector3(0, 1, 1), 65f);
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
            ClientSimPlayerAvatarManager avatar = null;
            foreach (var candidate in Resources.FindObjectsOfTypeAll<ClientSimPlayerAvatarManager>())
                if (candidate.gameObject.scene.IsValid()
                    && Vector3.Distance(candidate.GetBonePosition(HumanBodyBones.LeftHand),
                        Networking.LocalPlayer.GetBonePosition(HumanBodyBones.LeftHand)) < 0.001f)
                { avatar = candidate; break; }
            Assert.IsNotNull(avatar, "ClientSim avatar manager was not instantiated");
            Transform hand = avatar.GetBoneTransform(HumanBodyBones.LeftHand);
            Assert.IsNotNull(hand, "ClientSim avatar has no left hand");
            var animator = hand.GetComponentInParent<Animator>();
            animator.enabled = false;
            hand.position = new Vector3(-1.7f, 1.4f, 1.89f);
            var pool = Object.FindObjectOfType<LiquidCanvasPool>();
            LiquidBodyCanvas handCanvas = null;
            float contactDeadline = Time.realtimeSinceStartup + 8f;
            while (handCanvas == null && Time.realtimeSinceStartup < contactDeadline)
            {
                foreach (LiquidBodyCanvas limb in pool.limbs)
                {
                    var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(limb);
                    if ((int)backing.GetProgramVariable("_playerId") == Networking.LocalPlayer.playerId
                        && (HumanBodyBones)backing.GetProgramVariable("followBone") == HumanBodyBones.LeftHand)
                        handCanvas = limb;
                }
                yield return null;
            }
            Assert.IsNotNull(handCanvas, "hand contact did not allocate a limb canvas");
            Assert.Greater(Sum(handCanvas.projectorMaterial), 0.001f, "hand did not receive wall paint");
            hand.position += Vector3.back * 0.5f;
            yield return Wait(0.2f);
            Assert.Less(Vector3.Distance(hand.position, handCanvas.canvasRoot.position), 0.01f,
                "paint canvas did not follow the hand");
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

            var puddle = GameObject.Find("Step Splash").GetComponent<LiquidImmersionVolume>();
            var splashBacking = UdonSharpEditorUtility.GetBackingUdonBehaviour(puddle.stepSplash);
            int splashes = (int)splashBacking.GetProgramVariable("_received");
            Networking.LocalPlayer.TeleportTo(new Vector3(-3f, 0.02f, -2f), Quaternion.identity);
            yield return Wait(0.7f);
            Assert.Greater((int)splashBacking.GetProgramVariable("_received"), splashes,
                "entering shallow water did not generate a splash; position=" + Networking.LocalPlayer.GetPosition()
                + "; grounded=" + Networking.LocalPlayer.IsPlayerGrounded()
                + "; bounds=" + puddle.GetComponent<Collider>().bounds
                + "; emittedUntil=" + splashBacking.GetProgramVariable("_next"));
        }

        [UnityTest]
        public IEnumerator DesktopButtons_TransferPaintAndThrowBalloonAtFeet()
        {
            _optionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            _options = EditorSettings.enterPlayModeOptions;
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
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
            var demo = Object.FindObjectOfType<LiquidContactDemo>();
            var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(demo);
            var button = GameObject.Find("Reset / Run demo").GetComponent<LiquidButton>();
            UdonSharpEditorUtility.GetBackingUdonBehaviour(button).Interact();
            float deadline = Time.realtimeSinceStartup + 20f;
            while ((int)backing.GetProgramVariable("_phase") < 2 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(1, backing.GetProgramVariable("_runs"), "button did not start the demo");
            Assert.AreEqual(2, backing.GetProgramVariable("_phase"), "rub stage did not complete");
            float padBefore = Sum(demo.padCanvas.projectorMaterial);
            Assert.Greater(padBefore, 0.001f, "demo pad did not receive paint");
            LiquidDemoPlayTests.Capture("liquid-contact-desktop.png", new Vector3(0, 2.5f, -4), new Vector3(0, 1.4f, 1), 65f);
            // Clear only the destination texture to prove that subsequent paint is transferred from the pad.
            UdonSharpEditorUtility.GetBackingUdonBehaviour(demo.surface).SendCustomEvent("Clear");
            while ((int)backing.GetProgramVariable("_phase") < 4 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(4, backing.GetProgramVariable("_phase"));
            Assert.Greater(Sum(demo.surface.projectorMaterial), 0.001f, "pad did not transfer paint to the other wall");
            Assert.Less(Sum(demo.padCanvas.projectorMaterial), padBefore, "transfer did not reduce pad paint");
            while ((int)backing.GetProgramVariable("_phase") < 5 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(5, backing.GetProgramVariable("_phase"));
            UdonSharpEditorUtility.GetBackingUdonBehaviour(button).Interact();
            yield return Wait(0.2f);
            Assert.AreEqual(2, backing.GetProgramVariable("_runs"), "reset did not restart the demo");
            var balloon = Object.FindObjectOfType<LiquidWaterBalloon>();
            var balloonBacking = UdonSharpEditorUtility.GetBackingUdonBehaviour(balloon);
            balloonBacking.SetProgramVariable("respawnSeconds", 1f);
            var launch = GameObject.Find("Throw at my feet").GetComponent<LiquidButton>();
            UdonSharpEditorUtility.GetBackingUdonBehaviour(launch).Interact();
            deadline = Time.realtimeSinceStartup + 3f;
            while (!(bool)balloonBacking.GetProgramVariable("_burst") && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue((bool)balloonBacking.GetProgramVariable("_burst"), "launched balloon did not burst");
            Vector3 delta = balloon.transform.position - Networking.LocalPlayer.GetPosition();
            Assert.Less(new Vector2(delta.x, delta.z).magnitude, 0.5f, "balloon missed the player's feet");
            Assert.Greater((int)UdonSharpEditorUtility.GetBackingUdonBehaviour(balloon.splash).GetProgramVariable("_received"), 0);
            yield return Wait(1.2f);
            Assert.IsFalse((bool)balloonBacking.GetProgramVariable("_burst"), "balloon did not respawn");
            // Exercise ClientSim's real pickup/drop path, including its release velocity.
            ClientSimPlayerHand hand = null;
            foreach (var candidate in Resources.FindObjectsOfTypeAll<ClientSimPlayerHand>())
                if (candidate.gameObject.scene.IsValid() && candidate.gameObject.activeInHierarchy)
                { hand = candidate; break; }
            Assert.IsNotNull(hand);
            var pickupMethod = typeof(ClientSimPlayerHand).GetMethod("Pickup",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            pickupMethod.Invoke(hand, new object[] { balloon.GetComponent<ClientSimPickupHelper>() });
            yield return Wait(0.6f);
            Assert.IsTrue(balloon.pickup.IsHeld, "ClientSim pickup failed");
            balloonBacking.OnPickupUseDown();
            yield return null;
            Assert.IsFalse(balloon.pickup.IsHeld, "Use did not drop the balloon");
            deadline = Time.realtimeSinceStartup + 3f;
            while (!(bool)balloonBacking.GetProgramVariable("_burst") && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue((bool)balloonBacking.GetProgramVariable("_burst"), "Use throw did not burst");
            delta = balloon.transform.position - Networking.LocalPlayer.GetPosition();
            Assert.Less(new Vector2(delta.x, delta.z).magnitude, 0.5f, "Use throw missed the player's feet");
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
