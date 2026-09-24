using System.Collections.Generic;
using NUnit.Framework;
using SabaProps.Capture;
using SabaProps.Capture.Editors;
using UdonSharp;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.SDK3.Components;

namespace SabaProps.Capture.WorldTests
{
    /// <summary>
    /// What the offline tier cannot see: whether UdonSharp accepts the two behaviours,
    /// which events and fields reach Udon, and whether the generated panel is wired to
    /// the backing UdonBehaviours rather than to the editor proxies.
    /// </summary>
    public class CaptureProgramTests
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
            _root = new GameObject("CaptureUnderTest");
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
        public void Recorder_CompilesAndExportsItsEvents()
        {
            IUdonProgram program = Compile(_root.AddUdonSharpComponent<CaptureRecorder>());
            var exported = new List<string>(program.EntryPoints.GetExportedSymbols());

            // Without _update nothing is ever captured.
            CollectionAssert.Contains(exported, "_update", "Update is not exported as an Udon event");
            foreach (string name in new[]
                     {
                         "_StartRecording", "_StopRecording", "_ToggleRecording",
                         "_CaptureNow", "_Clear", "_ReleaseFrames",
                     })
            {
                CollectionAssert.Contains(exported, name);
            }
        }

        [Test]
        public void Recorder_ExposesItsInspectorFieldsToUdon()
        {
            IUdonProgram program = Compile(_root.AddUdonSharpComponent<CaptureRecorder>());
            var symbols = new List<string>(program.SymbolTable.GetExportedSymbols());

            foreach (string field in new[]
                     {
                         "sourceTexture", "sourceCamera", "interval", "recordOnStart", "frameWidth",
                         "frameHeight", "pixelFormat", "maxFrames", "memoryBudgetMegabytes", "fullPolicy",
                     })
            {
                CollectionAssert.Contains(symbols, field, $"'{field}' is not exposed to Udon");
            }
        }

        [Test]
        public void Player_CompilesAndExportsItsEvents()
        {
            IUdonProgram program = Compile(_root.AddUdonSharpComponent<CapturePlayer>());
            var exported = new List<string>(program.EntryPoints.GetExportedSymbols());

            CollectionAssert.Contains(exported, "_update", "Update is not exported as an Udon event");
            foreach (string name in new[]
                     {
                         "_Play", "_Pause", "_TogglePlay", "_StepForward", "_StepBackward",
                         "_First", "_Latest", "_Faster", "_Slower", "_OnScrub",
                     })
            {
                CollectionAssert.Contains(exported, name);
            }
        }

        [Test]
        public void BothBehaviours_AreLocalOnly()
        {
            foreach (System.Type type in new[] { typeof(CaptureRecorder), typeof(CapturePlayer) })
            {
                var attributes = (UdonBehaviourSyncModeAttribute[])type
                    .GetCustomAttributes(typeof(UdonBehaviourSyncModeAttribute), true);
                Assert.AreEqual(1, attributes.Length, $"{type.Name} should declare its sync mode explicitly");
                Assert.AreEqual(BehaviourSyncMode.None, attributes[0].behaviourSyncMode, type.Name);
            }
        }

        [Test]
        public void Panel_WiresEveryControlToAnExportedEvent()
        {
            var recorderObject = new GameObject("Recorder");
            recorderObject.transform.SetParent(_root.transform, false);
            CaptureRecorder recorder = recorderObject.AddUdonSharpComponent<CaptureRecorder>();

            CapturePlayer player = CapturePanelBuilder.Create(recorder);
            player.transform.SetParent(_root.transform, false);

            Assert.That(player.recorder, Is.EqualTo(recorder));
            Assert.That(player.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(player.GetComponent<VRCUiShape>(), Is.Not.Null);
            Assert.That(player.display, Is.Not.Null);
            Assert.That(player.timeline, Is.Not.Null);
            Assert.That(player.thumbnails.Length, Is.EqualTo(CapturePanelBuilder.ThumbnailCount));
            Assert.That(player.gameObject.layer, Is.EqualTo(CapturePanelBuilder.PanelLayer));

            UdonBehaviour playerBacking = UdonSharpEditorUtility.GetBackingUdonBehaviour(player);
            UdonBehaviour recorderBacking = UdonSharpEditorUtility.GetBackingUdonBehaviour(recorder);
            var playerExports = new List<string>(Compile(player).EntryPoints.GetExportedSymbols());
            var recorderExports = new List<string>(Compile(recorder).EntryPoints.GetExportedSymbols());

            Button[] buttons = player.GetComponentsInChildren<Button>();
            Assert.That(buttons.Length, Is.EqualTo(11));
            foreach (Button button in buttons)
            {
                Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1), button.name);
                Object target = button.onClick.GetPersistentTarget(0);
                Assert.That(target == playerBacking || target == recorderBacking, Is.True,
                    $"{button.name} must target a backing UdonBehaviour, not a proxy");
                Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo("SendCustomEvent"));

                string eventName = new SerializedObject(button)
                    .FindProperty("m_OnClick.m_PersistentCalls.m_Calls.Array.data[0].m_Arguments.m_StringArgument")
                    .stringValue;
                CollectionAssert.Contains(target == playerBacking ? playerExports : recorderExports, eventName, button.name);
            }

            Slider timeline = player.timeline;
            Assert.That(timeline.onValueChanged.GetPersistentEventCount(), Is.EqualTo(1));
            Assert.That(timeline.onValueChanged.GetPersistentTarget(0), Is.EqualTo(playerBacking));
            string scrub = new SerializedObject(timeline)
                .FindProperty("m_OnValueChanged.m_PersistentCalls.m_Calls.Array.data[0].m_Arguments.m_StringArgument")
                .stringValue;
            Assert.That(scrub, Is.EqualTo("_OnScrub"));
        }

        private static IUdonProgram Compile(UdonSharpBehaviour behaviour)
        {
            Assert.IsNotNull(behaviour, "AddUdonSharpComponent returned nothing");
            UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour);
            Assert.IsNotNull(backing, "the proxy has no backing UdonBehaviour");

            UdonSharpProgramAsset asset = UdonSharpEditorUtility.GetUdonSharpProgramAsset(backing);
            Assert.IsNotNull(asset, $"no UdonSharpProgramAsset was found for {behaviour.GetType().Name}");
            Assert.IsNotNull(asset.SerializedProgramAsset, "the program asset holds no serialized program");

            IUdonProgram program = asset.SerializedProgramAsset.RetrieveProgram();
            Assert.IsNotNull(program, $"UdonSharp produced no program for {behaviour.GetType().Name}");
            return program;
        }
    }
}
