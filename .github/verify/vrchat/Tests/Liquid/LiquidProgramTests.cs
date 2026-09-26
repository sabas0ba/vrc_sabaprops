using System.Collections.Generic;
using NUnit.Framework;
using UdonSharp;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace SabaProps.Liquid.WorldTests
{
    /// <summary>
    /// Whether UdonSharp accepts the liquid behaviours.
    /// <para>
    /// The offline tier compiles them as plain C# against a stub
    /// UdonSharpBehaviour, which says nothing about the rules UdonSharp adds:
    /// no generics, no out parameters, only exposed externs. This runs the real
    /// compiler and inspects the exported entry points.
    /// </para>
    /// </summary>
    public class LiquidProgramTests
    {
        private GameObject _root;

        [OneTimeSetUp]
        public void CompileUdonSharpPrograms()
        {
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
        }

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("LiquidProgramUnderTest");
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
        public void BodyCanvas_ExportsItsEventsAndApi()
        {
            List<string> exported = Exported(Compile<LiquidBodyCanvas>());

            // The frame is read after IK. If PostLateUpdate stops being an Udon
            // event, the canvas never follows the body.
            CollectionAssert.Contains(exported, "_postLateUpdate");

            foreach (string method in new[] { "Assign", "Release", "QueueStamp", "ApplyImmersion", "WashImmersion", "GetPlayerId", "GetLastActivityTime" })
            {
                AssertExportsMethod(exported, method);
            }
        }

        [Test]
        public void CanvasPool_ExportsItsEventsAndApi()
        {
            List<string> exported = Exported(Compile<LiquidCanvasPool>());

            CollectionAssert.Contains(exported, "_onPlayerLeft");
            AssertExportsMethod(exported, "AcquireCanvas");
            AssertExportsMethod(exported, "FindCanvas");
        }

        [Test]
        public void Profile_Compiles()
        {
            Assert.IsNotNull(Compile<LiquidProfile>());
        }

        [Test]
        public void CanvasPool_ExportsTheSourceGeometry()
        {
            List<string> exported = Exported(Compile<LiquidCanvasPool>());

            foreach (string method in new[] { "CastTargets", "CanvasForTarget", "SampleCone", "Random01", "WorldToTarget", "TargetToWorld", "GetMannequinCount" })
            {
                AssertExportsMethod(exported, method);
            }
        }

        [Test]
        public void ImmersionVolume_ExportsItsTriggerEvents()
        {
            List<string> exported = Exported(Compile<LiquidImmersionVolume>());

            // The whole Source is driven by these two; without them nobody is ever inside.
            CollectionAssert.Contains(exported, "_onPlayerTriggerEnter");
            CollectionAssert.Contains(exported, "_onPlayerTriggerExit");
            CollectionAssert.Contains(exported, "_update");
            AssertSyncMode<LiquidImmersionVolume>(BehaviourSyncMode.None);
        }

        [Test]
        public void Shower_ExportsItsControlsAndSyncsOnlyItsState()
        {
            IUdonProgram program = Compile<LiquidShower>();
            List<string> exported = Exported(program);

            CollectionAssert.Contains(exported, "_interact");
            CollectionAssert.Contains(exported, "_onPickupUseDown");
            CollectionAssert.Contains(exported, "_onPickupUseUp");
            CollectionAssert.Contains(exported, "_onDeserialization");
            AssertSyncMode<LiquidShower>(BehaviourSyncMode.Manual);
            AssertSyncedFields(program, "_running");
        }

        [Test]
        public void WaterGun_SendsEventsInsteadOfSyncing()
        {
            IUdonProgram program = Compile<LiquidWaterGun>();
            List<string> exported = Exported(program);

            // Other clients call this over the network. If it stops being exported,
            // hits are sent and silently dropped.
            AssertExportsMethod(exported, "ReceiveHit");
            AssertExportsMethod(exported, "ReceiveFiring");
            CollectionAssert.Contains(exported, "_onPickupUseDown");
            CollectionAssert.Contains(exported, "_onPickupUseUp");

            // The gun sits on a GameObject with VRCObjectSync, which Manual sync interferes with.
            AssertSyncMode<LiquidWaterGun>(BehaviourSyncMode.NoVariableSync);
            AssertSyncedFields(program);
        }

        [Test]
        public void Sprayer_RunsWithoutSyncing()
        {
            List<string> exported = Exported(Compile<LiquidSprayer>());
            CollectionAssert.Contains(exported, "_update");
            AssertSyncMode<LiquidSprayer>(BehaviourSyncMode.None);
        }

        [Test]
        public void Turntable_RunsWithoutSyncing()
        {
            List<string> exported = Exported(Compile<LiquidTurntable>());
            CollectionAssert.Contains(exported, "_update");
            AssertSyncMode<LiquidTurntable>(BehaviourSyncMode.None);
        }

        [Test]
        public void Lighting_ExportsRefresh()
        {
            List<string> exported = Exported(Compile<LiquidLighting>());
            AssertExportsMethod(exported, "Refresh");
            AssertSyncMode<LiquidLighting>(BehaviourSyncMode.None);
        }

        private static void AssertSyncMode<T>(BehaviourSyncMode expected)
        {
            var attributes = (UdonBehaviourSyncModeAttribute[])typeof(T)
                .GetCustomAttributes(typeof(UdonBehaviourSyncModeAttribute), true);
            Assert.AreEqual(1, attributes.Length, typeof(T).Name + " should declare its sync mode explicitly");
            Assert.AreEqual(expected, attributes[0].behaviourSyncMode, typeof(T).Name + " sync mode");
        }

        /// <summary>
        /// Exactly these fields are synced. A Source that synced more than its
        /// on/off state would be sending what every client can compute itself.
        /// </summary>
        private static void AssertSyncedFields(IUdonProgram program, params string[] expected)
        {
            var synced = new List<string>();
            foreach (IUdonSyncMetadata metadata in program.SyncMetadataTable.GetAllSyncMetadata())
            {
                synced.Add(metadata.Name);
            }

            CollectionAssert.AreEquivalent(expected, synced);
        }

        private IUdonProgram Compile<T>() where T : UdonSharpBehaviour
        {
            T behaviour = _root.AddUdonSharpComponent<T>();
            Assert.IsNotNull(behaviour, "AddUdonSharpComponent returned nothing for " + typeof(T).Name);

            UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour);
            Assert.IsNotNull(backing, "the proxy has no backing UdonBehaviour");

            UdonSharpProgramAsset asset = UdonSharpEditorUtility.GetUdonSharpProgramAsset(backing);
            Assert.IsNotNull(asset, "no UdonSharpProgramAsset for " + typeof(T).Name);
            Assert.IsNotNull(asset.SerializedProgramAsset,
                "UdonSharp produced no program for " + typeof(T).Name + ". The Unity console holds the diagnostics.");

            return asset.SerializedProgramAsset.RetrieveProgram();
        }

        /// <summary>
        /// UdonSharp exports a method without parameters under its own name and a
        /// method with parameters under a mangled one ("__0_Assign"). Either means
        /// another behaviour can call it.
        /// </summary>
        private static void AssertExportsMethod(List<string> exported, string method)
        {
            bool found = exported.Exists(name => name == method || name.EndsWith("_" + method));
            Assert.IsTrue(found, method + " is not callable from other behaviours. Exported: " + string.Join(", ", exported));
        }

        private static List<string> Exported(IUdonProgram program)
        {
            Assert.IsNotNull(program);
            return new List<string>(program.EntryPoints.GetExportedSymbols());
        }
    }
}
