using System.Collections.Generic;
using NUnit.Framework;
using SabaProps.Tablet.Authoring;
using SabaProps.Tablet.Editors;
using UdonSharp;
using UdonSharp.Compiler;
using UdonSharpEditor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.Udon.Common.Interfaces;

namespace SabaProps.Tablet.WorldTests
{
    /// <summary>
    /// What the offline tier cannot see: that UdonSharp accepts every behaviour, and
    /// that the builder wires a saved scene so each button reaches an event Udon
    /// actually exports.
    /// </summary>
    public class TabletSampleSceneTests
    {
        [OneTimeSetUp]
        public void CompilePrograms()
        {
            Assert.That(TMP_Settings.instance, Is.Not.Null, "TMP Essential Resources were not imported");
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
        }

        [SetUp]
        public void CreateSample()
        {
            TabletSampleScene.Create();
            EditorSceneManager.OpenScene(TabletSampleScene.ScenePath);
        }

        [Test]
        public void EveryBehaviour_CompilesToAnUdonProgram()
        {
            foreach (var type in new[]
                     {
                         typeof(TabletController), typeof(TabletButton), typeof(TabletToggle), typeof(TabletTeleport),
                         typeof(TabletKeyTrigger), typeof(TabletReachTrigger), typeof(TabletInteractTrigger),
                         typeof(TabletSlider), typeof(TabletPostEffects),
                     })
            {
                var behaviour = (UdonSharpBehaviour)Object.FindObjectOfType(type, true);
                Assert.That(behaviour, Is.Not.Null, type.Name + " is missing from the sample");
                Assert.That(Program(behaviour), Is.Not.Null, type.Name + " did not compile; see the Unity console");
            }
        }

        [Test]
        public void EveryButton_TargetsAnExportedEvent()
        {
            var controller = Object.FindObjectOfType<TabletController>();
            var buttons = Object.FindObjectsOfType<TabletButton>(true);
            Assert.That(buttons.Length, Is.GreaterThan(0));
            Assert.That(controller.buttons.Length, Is.EqualTo(buttons.Length), "every button is registered for finger presses");

            foreach (TabletButton button in buttons)
            {
                Assert.That(button.target, Is.Not.Null, button.name);
                Assert.That(button.pressZone, Is.Not.Null, button.name);
                Assert.That(button.pressZone.isTrigger, Is.True, button.name + ": the press zone must not block players");
                var exported = new List<string>(Program(button.target).EntryPoints.GetExportedSymbols());
                CollectionAssert.Contains(exported, button.eventName, button.name);
                if (button.useArgument)
                {
                    var symbols = new List<string>(Program(button.target).SymbolTable.GetExportedSymbols());
                    CollectionAssert.Contains(symbols, "tabletArgument", button.name);
                }
            }
        }

        [Test]
        public void ReachTrigger_ExportsInputGrab()
        {
            var reach = Object.FindObjectOfType<TabletReachTrigger>();
            CollectionAssert.Contains(new List<string>(Program(reach).EntryPoints.GetExportedSymbols()), "_inputGrab");
        }

        [Test]
        public void Toggle_DeclaresManualSync()
        {
            var toggle = Object.FindObjectOfType<TabletToggle>();
            var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(toggle);
            Assert.That(backing.SyncMethod, Is.EqualTo(VRC.SDKBase.Networking.SyncType.Manual));
        }

        [Test]
        public void MirrorToggles_AreExclusive()
        {
            TabletToggle high = ToggleFor("Mirror HQ");
            TabletToggle low = ToggleFor("Mirror LQ");
            GameObject highObject = high.objects[0];
            GameObject lowObject = low.objects[0];

            low._TurnOn();
            Assert.That(lowObject.activeSelf, Is.True);
            Assert.That(highObject.activeSelf, Is.False);
            Assert.That(high.IsOn(), Is.False);

            high._Toggle();
            Assert.That(highObject.activeSelf, Is.True);
            Assert.That(lowObject.activeSelf, Is.False);
            Assert.That(high.buttons[0].IsLit(), Is.True);
            Assert.That(low.buttons[0].IsLit(), Is.False);
        }

        [Test]
        public void TeleportButtons_CarryTheirDestinationIndex()
        {
            var teleport = Object.FindObjectOfType<TabletTeleport>();
            Assert.That(teleport.destinations.Length, Is.EqualTo(3));
            foreach (TabletButton button in Object.FindObjectsOfType<TabletButton>(true))
            {
                if (button.eventName != "_TeleportToDestination")
                {
                    continue;
                }

                Assert.That(button.useArgument, Is.True);
                Assert.That(teleport.destinations[button.argument].name, Is.EqualTo(button.name));
            }
        }

        [Test]
        public void Build_ReplacesGeneratedObjectsWithoutDuplicates()
        {
            var definition = Object.FindObjectOfType<TabletDefinition>();
            TabletBuilder.Build(definition);
            TabletBuilder.Build(definition);

            int bodies = 0;
            foreach (Transform child in definition.transform)
            {
                if (child.name == TabletBuilder.BodyName)
                {
                    bodies++;
                }
            }

            Assert.That(bodies, Is.EqualTo(1));
            Assert.That(Object.FindObjectsOfType<TabletController>().Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsOfType<VRCPickup>().Length, Is.EqualTo(1), "only the handle is a pickup");
        }

        [Test]
        public void BedMirrors_HaveFiveInwardFacesAndSeparateColliderSwitch()
        {
            var definition = Object.FindObjectOfType<TabletDefinition>();
            TabletPage page = definition.FindOrAddPage("Bed Mirrors");
            Assert.That(page.bedDiagram, Is.True);
            Assert.That(page.entries.Count, Is.EqualTo(6));
            Vector3 eye = new Vector3(6f, 0.8f, 3f);
            for (int i = 0; i < 5; i++)
            {
                GameObject mirror = page.entries[i].objects[0];
                Assert.That(mirror.GetComponent<VRCMirrorReflection>(), Is.Not.Null);
                Assert.That(mirror.GetComponent<Renderer>().sharedMaterial.shader.name, Is.EqualTo("FX/MirrorReflection"));
                Assert.That(Vector3.Dot(-mirror.transform.forward, (eye - mirror.transform.position).normalized), Is.GreaterThan(0.99f));
                Assert.That(page.entries[i].startOn, Is.False);
                Assert.That(page.entries[i].exclusiveGroup, Is.Empty, "the bed faces can be enabled independently");
            }
            Assert.That(page.entries[5].colliders.Length, Is.EqualTo(2));
            Assert.That(page.entries[5].objects, Is.Empty, "collider switch must keep the bed visible");
        }

        [Test]
        public void PostEffects_HaveThreeSlidersAndVolumesSeparateFromReferenceCamera()
        {
            var module = Object.FindObjectOfType<TabletPostEffects>();
            Assert.That(module, Is.Not.Null);
            Assert.That(module.animator.runtimeAnimatorController, Is.Not.Null);
            Assert.That(module.volumes.Length, Is.EqualTo(5));
            foreach (Behaviour volume in module.volumes)
                Assert.That(volume.gameObject, Is.Not.EqualTo(Camera.main.gameObject));
            Assert.That(Object.FindObjectsOfType<TabletSlider>(true).Length, Is.EqualTo(3));
        }

        [Test]
        public void SampleDefinition_HasNoErrors()
        {
            var definition = Object.FindObjectOfType<TabletDefinition>();
            foreach (TabletIssue issue in TabletValidation.Validate(definition))
            {
                Assert.That(issue.type, Is.Not.EqualTo(MessageType.Error), issue.message);
            }
        }

        [Test]
        public void InteractItem_SummonsTheTablet()
        {
            GameObject stand = GameObject.Find(TabletSampleScene.StandName);
            var trigger = stand.GetComponent<TabletInteractTrigger>();
            Assert.That(trigger, Is.Not.Null);
            Assert.That(trigger.controller, Is.EqualTo(Object.FindObjectOfType<TabletController>()));
        }

        [Test]
        public void RemovedInteractItem_LosesItsTrigger()
        {
            var definition = Object.FindObjectOfType<TabletDefinition>();
            GameObject stand = GameObject.Find(TabletSampleScene.StandName);
            definition.interactItems.Remove(stand);
            TabletBuilder.Build(definition);
            Assert.That(stand.GetComponent<TabletInteractTrigger>(), Is.Null);
            Assert.That(stand.GetComponent<VRC.Udon.UdonBehaviour>(), Is.Null);
        }

        [Test]
        public void DuplicatedTablet_GetsItsOwnGeneratedFolder()
        {
            var original = Object.FindObjectOfType<TabletDefinition>();
            string folder = original.generatedFolder;
            GameObject copy = Object.Instantiate(original.gameObject);
            var duplicate = copy.GetComponent<TabletDefinition>();
            Assert.That(duplicate.generatedFolder, Is.EqualTo(folder), "the copy starts with the original's folder");

            TabletBuilder.Build(duplicate);
            Assert.That(duplicate.generatedFolder, Is.Not.EqualTo(folder));
            Assert.That(original.generatedFolder, Is.EqualTo(folder));
        }

        private static TabletToggle ToggleFor(string objectName)
        {
            foreach (TabletToggle toggle in Object.FindObjectsOfType<TabletToggle>())
            {
                if (toggle.objects.Length > 0 && toggle.objects[0].name == objectName)
                {
                    return toggle;
                }
            }

            Assert.Fail("no toggle for " + objectName);
            return null;
        }

        private static IUdonProgram Program(UdonSharpBehaviour behaviour)
        {
            var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour);
            var asset = UdonSharpEditorUtility.GetUdonSharpProgramAsset(backing);
            Assert.That(asset, Is.Not.Null, behaviour.GetType().Name + " has no program asset");
            Assert.That(asset.SerializedProgramAsset, Is.Not.Null, behaviour.GetType().Name + " has no compiled program");
            return asset.SerializedProgramAsset.RetrieveProgram();
        }
    }
}
