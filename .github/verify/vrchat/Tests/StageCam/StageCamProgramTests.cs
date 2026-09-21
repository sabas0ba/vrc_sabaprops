using System.Collections.Generic;
using NUnit.Framework;
using SabaProps.StageCam;
using UdonSharp;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace SabaProps.StageCam.WorldTests
{
    /// <summary>
    /// The gap the offline tier cannot close.
    /// <para>
    /// <c>.github/verify/verify.sh</c> compiles StageCamRig.cs as C#, against the real
    /// VRCSDKBase.dll but a hand-written UdonSharpBehaviour. That proves nothing about
    /// whether UdonSharp will accept the code: UdonSharp rejects generics, interfaces,
    /// user-defined structs, unsupported syntax and unsupported synced types, none of
    /// which a plain C# compile notices.
    /// </para>
    /// <para>
    /// This runs the real compiler and inspects what came out.
    /// </para>
    /// </summary>
    public class StageCamProgramTests
    {
        private GameObject _root;

        /// <summary>
        /// In the editor UdonSharp compiles on assembly reload. A batch mode test run
        /// does not necessarily get one, so the program assets can still be empty by the
        /// time the tests look at them. Ask for the compile explicitly.
        /// </summary>
        [OneTimeSetUp]
        public void CompileUdonSharpPrograms()
        {
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
        }

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("StageCamRigUnderTest");
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void StageCamRig_CompilesToAnUdonProgram()
        {
            IUdonProgram program = CompileRig();

            Assert.IsNotNull(
                program,
                "UdonSharp produced no program for StageCamRig. The Unity console holds the diagnostics.");
        }

        [Test]
        public void StageCamRig_ExportsTheEventsItOverrides()
        {
            IUdonProgram program = CompileRig();
            List<string> exported = new List<string>(program.EntryPoints.GetExportedSymbols());

            // PostLateUpdate is the whole reason the follow reads bones after IK. If it
            // ever stops being an Udon event, the rig silently never updates.
            CollectionAssert.Contains(exported, "_postLateUpdate", "PostLateUpdate is not exported as an Udon event");
            CollectionAssert.Contains(exported, "_interact", "Interact is not exported as an Udon event");
            CollectionAssert.Contains(exported, "_onPlayerLeft", "OnPlayerLeft is not exported as an Udon event");

            // The local-only API. VRChat refuses to fire network events whose names begin
            // with an underscore, which is what keeps these off the network.
            CollectionAssert.Contains(exported, "_TargetLocalPlayer");
            CollectionAssert.Contains(exported, "_TargetNearestPlayer");
            CollectionAssert.Contains(exported, "_ClearTarget");
        }

        [Test]
        public void StageCamRig_ExposesItsInspectorFieldsToUdon()
        {
            IUdonProgram program = CompileRig();
            List<string> symbols = new List<string>(program.SymbolTable.GetExportedSymbols());

            // A public field that Udon does not carry is one the Inspector can set and
            // the running behaviour will never see.
            foreach (string field in new[]
                     {
                         "subject",
                         "followMode",
                         "orbitYaw",
                         "orbitPitch",
                         "orbitDistance",
                         "positionTimeConstant",
                         "rotationTimeConstant",
                         "deadzoneRadius",
                         "autoFraming",
                         "screenFraction",
                         "cameraWork",
                         "cameraWorkPeriod",
                         "cameraWorkYawSweep",
                         "allowPickupAdjust",
                     })
            {
                CollectionAssert.Contains(symbols, field, $"'{field}' is not exposed to Udon");
            }
        }

        [Test]
        public void StageCamRig_DeclaresNoNetworkedState()
        {
            // 0.1.0 is local only, and says so in its sync mode rather than only in the
            // documentation. Adding a [UdonSynced] field without changing the mode is a
            // compile error in UdonSharp, so this asserts the pair stays consistent.
            var attributes = (UdonBehaviourSyncModeAttribute[])typeof(StageCamRig)
                .GetCustomAttributes(typeof(UdonBehaviourSyncModeAttribute), true);

            Assert.AreEqual(1, attributes.Length, "StageCamRig should declare its sync mode explicitly");
            Assert.AreEqual(
                BehaviourSyncMode.None,
                attributes[0].behaviourSyncMode,
                "0.1.0 is local only; syncing arrives in 0.2.0");
        }

        private IUdonProgram CompileRig()
        {
            StageCamRig rig = _root.AddUdonSharpComponent<StageCamRig>();
            Assert.IsNotNull(rig, "AddUdonSharpComponent returned nothing");

            UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(rig);
            Assert.IsNotNull(backing, "the proxy has no backing UdonBehaviour");

            UdonSharpProgramAsset asset = UdonSharpEditorUtility.GetUdonSharpProgramAsset(backing);
            Assert.IsNotNull(asset, "no UdonSharpProgramAsset was created for StageCamRig");
            Assert.IsNotNull(asset.SerializedProgramAsset, "the program asset holds no serialized program");

            return asset.SerializedProgramAsset.RetrieveProgram();
        }
    }
}
