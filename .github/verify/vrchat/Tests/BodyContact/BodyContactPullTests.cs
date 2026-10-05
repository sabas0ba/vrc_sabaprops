using NUnit.Framework;
using SabaProps.BodyContact.Editors;
using UdonSharpEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SabaProps.BodyContact.WorldTests
{
    public class BodyContactPullTests
    {
        private GameObject _root;
        private BodyContactSystem _system;
        private BodyContactPull _pull;

        [SetUp]
        public void SetUp()
        {
            _root = BodyContactMenu.CreateSystem();
            _system = _root.GetComponent<BodyContactSystem>();
            _pull = _system.pull;
        }

        [TearDown]
        public void TearDown() { if (_root != null) Object.DestroyImmediate(_root); }

        [Test]
        public void Permission_DefaultsOffAndCanBeRevoked()
        {
            Assert.That(_pull.allowBeingPulled, Is.False);
            _pull._TogglePullPermission();
            Assert.That(_pull.allowBeingPulled, Is.True);
            _pull.pullStep = Vector3.right;
            _pull.pullingLocal = true;
            _pull._TogglePullPermission();
            Assert.That(_pull.allowBeingPulled, Is.False);
            Assert.That(_pull.pullStep, Is.EqualTo(Vector3.zero));
            Assert.That(_pull.pullingLocal, Is.False);
        }

        [TestCase(10d, 9d, true)]
        [TestCase(10d, 7d, false)]
        [TestCase(10d, 11d, false)]
        [TestCase(10d, double.NaN, false)]
        public void Lease_RejectsExpiredAndInvalidUpdates(double now, double heartbeat, bool valid)
        {
            Assert.That(_pull.LeaseIsValid(now, heartbeat, 2f), Is.EqualTo(valid));
        }

        [Test]
        public void Pull_LeavesSlackAndNeverMovesVertically()
        {
            Assert.That(_pull.PullVelocity(new Vector3(0.05f, 1f, 0f), 0.08f, 0.25f, 0.8f, 2f, Vector3.zero, 0.02f), Is.EqualTo(Vector3.zero));
            Vector3 velocity = _pull.PullVelocity(new Vector3(1f, 2f, 0f), 0.08f, 0.25f, 0.8f, 2f, Vector3.zero, 0.02f);
            Assert.That(velocity.y, Is.Zero);
            Assert.That(velocity.x, Is.EqualTo(0.04f).Within(1e-5f));
        }

        [TestCase(30)]
        [TestCase(90)]
        [TestCase(144)]
        public void Pull_SettlesWithoutOvershootAndRespectsSpeedAndAcceleration(int fps)
        {
            float dt = 1f / fps;
            Vector3 position = Vector3.zero;
            Vector3 velocity = Vector3.zero;
            Vector3 target = Vector3.right;
            for (int frame = 0; frame < fps * 5; frame++)
            {
                Vector3 next = _pull.PullVelocity(target - position, 0.08f, 0.25f, 0.8f, 2f, velocity, dt);
                Assert.That(next.magnitude, Is.LessThanOrEqualTo(0.80001f));
                // 遊びに入ったときの即時停止を除き、加速度を制限します。
                if (next.sqrMagnitude > 0f) Assert.That((next - velocity).magnitude, Is.LessThanOrEqualTo(2f * dt + 1e-4f));
                position += next * dt;
                Assert.That(position.x, Is.LessThanOrEqualTo(0.92001f));
                velocity = next;
            }
            Assert.That(position.x, Is.GreaterThan(0.91f));
        }

        [Test]
        public void ContactCorrection_TakesPriorityOverOpposingPull()
        {
            Vector3 result = _pull.CombineSteps(Vector3.right * 0.02f, new Vector3(-0.1f, 2f, 0.01f));
            Assert.That(result.x, Is.EqualTo(0.02f).Within(1e-6f));
            Assert.That(result.y, Is.Zero);
            Assert.That(result.z, Is.EqualTo(0.01f).Within(1e-6f));
        }

        [Test]
        public void StationSuspension_DoesNotUndoManualSuspension()
        {
            _system._Suspend();
            _system._StationEntered();
            _system._StationExited();
            Assert.That(_system.IsSuspended(), Is.True);
            _system._Resume();
            Assert.That(_system.IsSuspended(), Is.False);
            _system._StationEntered();
            _system._Resume();
            Assert.That(_system.IsSuspended(), Is.True);
            _system._StationExited();
            Assert.That(_system.IsSuspended(), Is.False);
        }

        [Test]
        public void LimbContact_PreservesShallowTouchAndOnlyCorrectsDeepVRProbes()
        {
            var positions = new Vector3[BodyContactSystem.ProbeCount];
            var radii = new float[BodyContactSystem.ProbeCount];
            var a = new Vector3[BodyContactSystem.PartCount];
            var b = new Vector3[BodyContactSystem.PartCount];
            var parts = new float[BodyContactSystem.PartCount];
            int hand = BodyContactSystem.ProbeLeftHand;
            radii[hand] = 0.05f;
            parts[BodyContactSystem.PartLeftForearm] = 0.05f;
            positions[hand] = Vector3.right * 0.06f;
            var depths = new float[BodyContactSystem.ProbeCount];
            var contacts = new Vector3[BodyContactSystem.ProbeCount];
            Vector3 shallow = _system.SolveBodyWithLimbs(positions, radii, 7, a, b, parts, 0,
                true, 1f, 0.03f, Vector3.right, Vector3.zero, depths, contacts, true, 0.05f, -1);
            Assert.That(shallow, Is.EqualTo(Vector3.zero));
            positions[hand] = Vector3.right * 0.02f;
            Vector3 deep = _system.SolveBodyWithLimbs(positions, radii, 7, a, b, parts, 0,
                true, 1f, 0.03f, Vector3.right, Vector3.zero, depths, contacts, true, 0.05f, -1);
            Assert.That(deep.x, Is.EqualTo(0.03f).Within(1e-5f));
            Vector3 desktop = _system.SolveBodyWithLimbs(positions, radii, 3, a, b, parts, 0,
                true, 1f, 0.03f, Vector3.right, Vector3.zero, depths, contacts, true, 0.05f, -1);
            Assert.That(desktop, Is.EqualTo(Vector3.zero));
            Vector3 grasp = _system.SolveBodyWithLimbs(positions, radii, 7, a, b, parts, 0,
                true, 1f, 0.03f, Vector3.right, Vector3.zero, depths, contacts, true, 0.05f, hand);
            Assert.That(grasp, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void PullWallCheck_IncludesBodyWidthAndHonorsExcludedLayers()
        {
            Vector3 root = new Vector3(100f, 0f, 100f);
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.transform.position = root + new Vector3(0.12f, 0.9f, 0.22f);
                wall.transform.localScale = new Vector3(0.04f, 1.8f, 0.04f);
                Physics.SyncTransforms();
                Vector3 step = Vector3.forward * 0.1f;
                Assert.That(_system.HitsWorld(root, step, 1.6f), Is.False, "center rays miss the wall edge");
                _pull.pullingLocal = true;
                Assert.That(_system.HitsWorld(root, step, 1.6f), Is.True, "body width should hit the wall edge");
                wall.layer = 9;
                Physics.SyncTransforms();
                Assert.That(_system.HitsWorld(root, step, 1.6f), Is.False, "player layer must remain excluded");
            }
            finally { Object.DestroyImmediate(wall); }
        }

        [Test]
        public void GrabAnchor_ClampsToTheForearmAndHandlesCoincidentJoints()
        {
            Assert.That(_pull.SegmentFraction(Vector3.zero, Vector3.right, Vector3.left), Is.Zero);
            Assert.That(_pull.SegmentFraction(Vector3.zero, Vector3.right, Vector3.right * 2f), Is.EqualTo(1f));
            Assert.That(_pull.SegmentFraction(Vector3.zero, Vector3.right, new Vector3(0.25f, 1f, 0f)), Is.EqualTo(0.25f));
            Assert.That(_pull.SegmentFraction(Vector3.one, Vector3.one, Vector3.zero), Is.EqualTo(1f));
        }

        [Test]
        public void SavedScene_PreservesPullControlsAndStationWiring()
        {
            Object.DestroyImmediate(_root);
            BodyContactSampleScene.Create();
            EditorSceneManager.OpenScene(BodyContactSampleScene.ScenePath);
            BodyContactSystem system = Object.FindObjectOfType<BodyContactSystem>();
            Assert.That(system.pull, Is.Not.Null);
            Assert.That(system.pull.source, Is.SameAs(system));
            Assert.That(system.pull.gameObject, Is.Not.SameAs(system.gameObject));
            Assert.That(system.pull.allowBeingPulled, Is.False);
            BodyContactPullControl[] controls = Object.FindObjectsOfType<BodyContactPullControl>();
            Assert.That(controls.Length, Is.EqualTo(2));
            foreach (BodyContactPullControl control in controls)
            {
                Assert.That(control.pull, Is.SameAs(system.pull));
                Assert.That(control.GetComponent<Collider>(), Is.Not.Null);
                Assert.That(control.label, Is.Not.Null);
                Assert.That(UdonSharpEditorUtility.GetBackingUdonBehaviour(control).programSource, Is.Not.Null);
            }
            BodyContactStationRelay relay = Object.FindObjectOfType<BodyContactStationRelay>();
            Assert.That(relay.source, Is.SameAs(system));
            Assert.That(relay.GetComponent<VRC.SDK3.Components.VRCStation>(), Is.Not.Null);
        }
    }
}
