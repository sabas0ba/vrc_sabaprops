using NUnit.Framework;
using SabaProps.BodyContact.Editors;
using UdonSharpEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.Udon;

namespace SabaProps.BodyContact.WorldTests
{
    /// <summary>
    /// The generated scene is what a person opens to try the package in the client, so
    /// what matters is the state it is in after a save and a reload, not the state the
    /// generator left in memory.
    /// </summary>
    public class BodyContactSampleSceneTests
    {
        [Test]
        public void SavedDemo_KeepsTheSystemWiredAndTheDummiesRegistered()
        {
            BodyContactSampleScene.Create();
            EditorSceneManager.OpenScene(BodyContactSampleScene.ScenePath);

            BodyContactSystem[] systems = Object.FindObjectsOfType<BodyContactSystem>();
            Assert.That(systems.Length, Is.EqualTo(1), "the demo must hold exactly one system");
            BodyContactSystem system = systems[0];

            UdonBehaviour systemUdon = UdonSharpEditorUtility.GetBackingUdonBehaviour(system);
            Assert.That(systemUdon, Is.Not.Null);
            Assert.That(systemUdon.programSource, Is.Not.Null, "the system lost its Udon program");

            BodyContactDebugView view = system.debugView;
            Assert.That(view, Is.Not.Null, "the system lost its debug view");
            Assert.That(view.source, Is.SameAs(system));
            Assert.That(view.lineFilter, Is.Not.Null);
            Assert.That(view.lineRenderer, Is.Not.Null);
            Assert.That(view.lineRenderer.sharedMaterial, Is.Not.Null, "the line material was not saved as an asset");
            Assert.That(view.statusLabel, Is.Not.Null);
            Assert.That(view.hudRoot, Is.Not.Null);
            Assert.That(view.lineFilter.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(view.lineFilter.transform.lossyScale, Is.EqualTo(Vector3.one));

            UdonBehaviour viewUdon = UdonSharpEditorUtility.GetBackingUdonBehaviour(view);
            Assert.That(viewUdon, Is.Not.Null);
            Assert.That(viewUdon.programSource, Is.Not.Null, "the debug view lost its Udon program");

            Assert.That(system.dummies.Length, Is.EqualTo(3));
            AssertDummy(system, 0, BodyContactSampleScene.OpenDummyName, BodyContactSampleScene.OpenDummyPosition, 1f);
            AssertDummy(system, 1, BodyContactSampleScene.WallDummyName, BodyContactSampleScene.WallDummyPosition, 1f);
            AssertDummy(
                system, 2, BodyContactSampleScene.SmallDummyName, BodyContactSampleScene.SmallDummyPosition,
                BodyContactSampleScene.SmallDummyScale);
        }

        [Test]
        public void SavedDemo_HasAWallTheSystemWillStopAt()
        {
            BodyContactSampleScene.Create();
            EditorSceneManager.OpenScene(BodyContactSampleScene.ScenePath);

            BodyContactSystem system = Object.FindObjectOfType<BodyContactSystem>();
            GameObject wall = GameObject.Find(BodyContactSampleScene.WallName);
            Assert.That(wall, Is.Not.Null);
            Assert.That(wall.GetComponent<Collider>(), Is.Not.Null, "the wall has nothing to raycast against");
            Assert.That(
                system.worldCollisionMask.value & (1 << wall.layer), Is.Not.Zero,
                "the wall is on a layer the system does not test, so a player would be pushed through it");

            // The wall has to stand between the spawn and its dummy, with room for a
            // player in the gap. Otherwise the wall check in the README cannot be done.
            float wallZ = wall.transform.position.z;
            float dummyZ = BodyContactSampleScene.WallDummyPosition.z;
            Assert.That(wallZ, Is.LessThan(dummyZ));
            Assert.That(wallZ, Is.GreaterThan(BodyContactSampleScene.SpawnPosition.z));
            Assert.That(dummyZ - wallZ, Is.InRange(0.8f, 1.5f), "the gap between the wall and the dummy");
        }

        [Test]
        public void SavedDemo_IsAVrchatWorldWithAFloorUnderTheSpawn()
        {
            BodyContactSampleScene.Create();
            EditorSceneManager.OpenScene(BodyContactSampleScene.ScenePath);

            VRCSceneDescriptor world = Object.FindObjectOfType<VRCSceneDescriptor>();
            Assert.That(world, Is.Not.Null);
            Assert.That(world.spawns.Length, Is.EqualTo(1));
            Assert.That(world.spawns[0].position, Is.EqualTo(BodyContactSampleScene.SpawnPosition));

            GameObject floor = GameObject.Find(BodyContactSampleScene.FloorName);
            Assert.That(floor, Is.Not.Null);
            Bounds bounds = floor.GetComponent<Collider>().bounds;
            Vector3 spawn = BodyContactSampleScene.SpawnPosition;
            Assert.That(bounds.max.y, Is.EqualTo(0f).Within(1e-4f), "the floor surface must be at height 0");
            Assert.That(spawn.x, Is.InRange(bounds.min.x, bounds.max.x));
            Assert.That(spawn.z, Is.InRange(bounds.min.z, bounds.max.z));
        }

        private static void AssertDummy(BodyContactSystem system, int index, string name, Vector3 position, float scale)
        {
            Transform dummy = system.dummies[index];
            Assert.That(dummy, Is.Not.Null, name + " is missing from the system");
            Assert.That(dummy.name, Is.EqualTo(name));
            Assert.That(Vector3.Distance(dummy.position, position), Is.LessThan(1e-4f), name + " position");
            Assert.That(dummy.lossyScale.y, Is.EqualTo(scale).Within(1e-4f), name + " scale");
        }
    }
}
