using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SabaProps.Tablet.WorldTests
{
    public class TabletTeleportPhysicsTests
    {
        private TabletTeleport teleport;

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            teleport = new GameObject("Teleport").AddComponent<TabletTeleport>();
            teleport.playerCollisionMask = 1;
        }

        private void Box(Vector3 position, Vector3 size)
        {
            var box = new GameObject("Obstacle").AddComponent<BoxCollider>();
            box.transform.position = position;
            box.size = size;
            Physics.SyncTransforms();
        }

        private bool Find()
        {
            return (bool)typeof(TabletTeleport).GetMethod("TryFindPlayerDestination",
                BindingFlags.NonPublic | BindingFlags.Instance).Invoke(teleport,
                new object[] { Vector3.zero, Vector3.forward, 1.8f });
        }

        private Vector3 Position => (Vector3)typeof(TabletTeleport).GetField("safePlayerPosition",
            BindingFlags.NonPublic | BindingFlags.Instance).GetValue(teleport);

        [Test]
        public void ClearFloor_PrefersBehind()
        {
            Box(new Vector3(0, -0.1f, 0), new Vector3(10, 0.2f, 10));
            Assert.That(Find(), Is.True);
            Assert.That(Position.z, Is.EqualTo(-1.2f).Within(0.001f));
            Assert.That(Position.x, Is.EqualTo(0).Within(0.001f));
        }

        [Test]
        public void WallBehind_UsesAnotherDirection()
        {
            Box(new Vector3(0, -0.1f, 0), new Vector3(10, 0.2f, 10));
            Box(new Vector3(0, 1, -1.2f), new Vector3(0.8f, 2, 0.4f));
            Assert.That(Find(), Is.True);
            Assert.That(Mathf.Abs(Position.x), Is.GreaterThan(0.5f));
        }

        [Test]
        public void EnclosedPlayer_RejectsAllDirections()
        {
            Box(new Vector3(0, -0.1f, 0), new Vector3(10, 0.2f, 10));
            Box(new Vector3(0, 1, 0), new Vector3(4, 2, 4));
            Assert.That(Find(), Is.False);
        }

        [Test]
        public void NoFloor_RejectsAllDirections()
        {
            Assert.That(Find(), Is.False);
        }
    }
}
