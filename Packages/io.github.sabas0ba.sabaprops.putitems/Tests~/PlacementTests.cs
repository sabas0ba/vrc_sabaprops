using NUnit.Framework;
using UdonSharpEditor;
using UnityEngine;

namespace SabaProps.PutItems.Tests
{
    public class PlacementTests
    {
        [OneTimeSetUp]
        public void PreparePrograms() { SabaProps.PutItems.Editors.PlacementPrograms.Prepare(); }

        private GameObject root;
        private Transform item;
        private Transform contact;
        private PlacementSurface surface;
        private PlacementSolver solver;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Test root");
            GameObject plane = new GameObject("Surface");
            plane.transform.SetParent(root.transform);
            surface = UdonSharpUndo.AddComponent<PlacementSurface>(plane);
            solver = UdonSharpUndo.AddComponent<PlacementSolver>(root);
            solver.surfaces = new[] { surface };
            item = new GameObject("Item").transform;
            item.SetParent(root.transform);
            item.position = new Vector3(0.2f, 0.08f, 0.1f);
            contact = new GameObject("Contact").transform;
            contact.SetParent(item, false);
            contact.localPosition = new Vector3(0.03f, -0.02f, 0f);
        }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(root); }

        [TestCase(0f)]
        [TestCase(90f)]
        [TestCase(37f)]
        public void RotatedSurface_AlignsOffsetContactAndPreservesTangentialPosition(float angle)
        {
            Quaternion basis = Quaternion.Euler(angle, 0f, 0f);
            surface.transform.rotation = basis;
            item.position = basis * item.position;
            item.rotation = basis * Quaternion.Euler(20f, 35f, 0f);
            Vector3 before = contact.position;
            Vector3 itemBefore = item.position;
            Quaternion rotationBefore = item.rotation;
            Vector3 expected = before - surface.transform.up * Vector3.Dot(before, surface.transform.up);
            Assert.IsTrue(solver.TryFindPose(item, contact, 1));
            Assert.AreEqual(itemBefore, item.position, "計算だけで Transform を変更しない");
            Assert.AreEqual(rotationBefore, item.rotation);
            item.SetPositionAndRotation(solver.resultPosition, solver.resultRotation);
            Assert.Less(Vector3.Distance(expected, contact.position), 0.00001f);
            Assert.Less(Vector3.Angle(surface.transform.up, contact.up), 0.01f);
        }

        [Test]
        public void FlatItem_PreservesYaw()
        {
            item.rotation = Quaternion.Euler(0f, 123f, 0f);
            Assert.IsTrue(solver.TryFindPose(item, contact, 1));
            Assert.Less(Quaternion.Angle(item.rotation, solver.resultRotation), 0.01f);
        }

        [TestCase(0.3f)]
        [TestCase(-0.1f)]
        public void OutsideDistance_IsRejected(float height)
        {
            item.position = new Vector3(0f, height, 0f);
            Assert.IsFalse(solver.TryFindPose(item, contact, 1));
        }

        [Test]
        public void FootprintOverEdge_IsRejected()
        {
            solver.footprintRadius = 0.1f;
            item.position = new Vector3(0.42f, 0.05f, 0f);
            Assert.IsFalse(solver.TryFindPose(item, contact, 1));
            solver.footprintRadius = 0f;
            Assert.IsTrue(solver.TryFindPose(item, contact, 1));
        }

        [Test]
        public void RejectedQuery_ClearsPreviousResult()
        {
            Assert.IsTrue(solver.TryFindPose(item, contact, 1));
            Assert.IsFalse(solver.TryFindPose(item, contact, 2));
            Assert.IsFalse(solver.hasResult);
            Assert.IsNull(solver.resultSurface);
        }

        [Test]
        public void DisabledSurface_AndExcessTilt_AreRejected()
        {
            surface.enabled = false;
            Assert.IsFalse(solver.TryFindPose(item, contact, 1));
            surface.enabled = true;
            item.rotation = Quaternion.Euler(100f, 0f, 0f);
            Assert.IsFalse(solver.TryFindPose(item, contact, 1));
        }

        [Test]
        public void NonUniformScale_IsRejected()
        {
            item.localScale = new Vector3(1f, 2f, 1f);
            Assert.IsFalse(solver.TryFindPose(item, contact, 1));
        }

        [Test]
        public void NearestCompatibleSurface_Wins()
        {
            GameObject other = new GameObject("Closer surface");
            other.transform.SetParent(root.transform);
            other.transform.position = new Vector3(0f, 0.04f, 0f);
            PlacementSurface closer = UdonSharpUndo.AddComponent<PlacementSurface>(other);
            solver.surfaces = new[] { surface, closer };
            Assert.IsTrue(solver.TryFindPose(item, contact, 1));
            Assert.AreSame(closer, solver.resultSurface);
            closer.acceptedCategories = 2;
            Assert.IsTrue(solver.TryFindPose(item, contact, 1));
            Assert.AreSame(surface, solver.resultSurface);
        }
    }
}
