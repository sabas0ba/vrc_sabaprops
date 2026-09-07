using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SabaProps.SoftProps.WorldTests
{
    public class SoftPropsDemoTests
    {
        [Test]
        public void BundledDemo_ImportsWithoutGeneratorAndHasReviewStations()
        {
            Type demo = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("SabaProps.SoftProps.Editors.SoftPropsDemo"))
                .First(type => type != null);
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                demo.GetMethod("ImportSample", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
                demo.GetMethod("ImportSample", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
                Assert.AreEqual(1, AssetDatabase.FindAssets("SoftSurfaceContactController t:UdonSharpProgramAsset",
                    new[] { "Assets" }).Length, "Demo import must share the controller program");
                var scene = EditorSceneManager.OpenScene("Assets/SabaProps/SoftPropsDemoMotion/SoftPropsDemo.unity");
                var roots = scene.GetRootGameObjects();
                foreach (string name in new[] { "Futon", "Bed", "Sofa", "Cushion", "ContactProbeTest",
                    "Static Finger", "Static Rod", "Static Plate", "Floor", "VRCWorld" })
                    Assert.IsTrue(roots.Any(root => root.name == name), "Missing demo station: " + name);

                var components = roots.SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToArray();
                Assert.IsFalse(components.Any(component => component == null), "Missing script in bundled scene");
                foreach (var filter in components.OfType<MeshFilter>())
                    Assert.IsNotNull(filter.sharedMesh, "Missing mesh: " + filter.name);
                foreach (var renderer in components.OfType<Renderer>())
                    foreach (var material in renderer.sharedMaterials)
                    {
                        Assert.IsNotNull(material, "Missing material: " + renderer.name);
                        Assert.IsNotNull(material.shader, "Missing shader: " + renderer.name);
                    }
                foreach (var label in components.OfType<TextMesh>())
                    Assert.IsNotNull(label.font, "Missing label font");

                var descriptor = components.First(component => component.GetType().Name == "VRCSceneDescriptor");
                var spawns = (Transform[])descriptor.GetType().GetField("spawns").GetValue(descriptor);
                Assert.AreEqual(1, spawns.Length);
                Assert.IsNotNull(spawns[0]);
                Assert.Greater(spawns[0].position.y, 0f);
                Assert.AreEqual(3, components.Count(component => component.GetType().Name == "VRCPickup"));
                Assert.AreEqual(13, components.Count(component => component.GetType().Name == "SoftSurfaceContactController"));
                for (int profile = 0; profile < 3; profile++)
                {
                    var comparison = roots.Single(root => root.name == "Automatic comparison " + profile);
                    var controller = comparison.GetComponentsInChildren<Component>()
                        .Single(component => component.GetType().Name == "SoftSurfaceContactController");
                    Type type = controller.GetType();
                    Assert.IsTrue((bool)type.GetField("automaticProbe").GetValue(controller));
                    Assert.AreEqual(profile == 0 ? 0.15f : profile == 1 ? 0.45f : 0.80f,
                        (float)type.GetField("hardness").GetValue(controller));
                    var probes = (Collider[])type.GetField("probeColliders").GetValue(controller);
                    Assert.AreEqual(3, probes.Length);
                    foreach (var probe in probes)
                    {
                        Assert.IsNotNull(probe);
                        Assert.IsTrue(probe.GetComponent<Rigidbody>().isKinematic);
                    }
                }
                foreach (var behaviour in components.Where(component => component.GetType().Name == "UdonBehaviour"))
                {
                    var serialized = new SerializedObject(behaviour);
                    var program = serialized.FindProperty("serializedProgramAsset");
                    Assert.IsNotNull(program, "Udon serialized program field missing");
                    Assert.IsNotNull(program.objectReferenceValue, "Bundled Udon program missing");
                }
                foreach (var root in roots.Where(root => root.name.StartsWith("Static ", StringComparison.Ordinal)))
                {
                    var material = root.GetComponent<Renderer>().sharedMaterial;
                    Assert.Greater(material.GetVector("_Contact0").w, 0f);
                    Assert.IsFalse(root.GetComponents<Component>().Any(component => component.GetType().Name == "SoftSurfaceContactController"));
                }
            }
            finally
            {
                if (previous.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(previous);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
    }
}
