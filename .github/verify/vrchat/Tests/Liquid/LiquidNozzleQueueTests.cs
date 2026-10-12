using System.Reflection;
using NUnit.Framework;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEngine;

namespace SabaProps.Liquid.WorldTests
{
    public class LiquidNozzleQueueTests
    {
        private GameObject _root;
        private LiquidNozzle _nozzle;
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        [OneTimeSetUp]
        public void CompilePrograms()
        {
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
        }

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("NozzleQueueTest");
            _nozzle = _root.AddUdonSharpComponent<LiquidNozzle>();
            var poolObject = new GameObject("Pool");
            poolObject.transform.SetParent(_root.transform);
            _nozzle.pool = poolObject.AddUdonSharpComponent<LiquidCanvasPool>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void Overflow_PreservesAllFutureHitsAndTheirPayloads()
        {
            int capacity = Read<float[]>("_pendingTime").Length;
            float due = Time.time + 100f;
            for (int i = 0; i < capacity; i++) Hold(i, due + i);
            var times = (float[])Read<float[]>("_pendingTime").Clone();
            var points = (Vector3[])Read<Vector3[]>("_pendingPoint").Clone();
            var amounts = (float[])Read<float[]>("_pendingAmount").Clone();
            for (int i = 0; i < capacity * 2; i++) Hold(1000 + i, due + 500f);
            Invoke("DeliverPending");

            Assert.AreEqual(capacity, Read<int>("_pendingCount"));
            Assert.AreEqual(0, Read<int>("_stampsDelivered"));
            CollectionAssert.AreEqual(times, Read<float[]>("_pendingTime"));
            CollectionAssert.AreEqual(points, Read<Vector3[]>("_pendingPoint"));
            CollectionAssert.AreEqual(amounts, Read<float[]>("_pendingAmount"));
        }

        [Test]
        public void Overflow_ReusesOnlyDueSlotsAndKeepsFutureHits()
        {
            int capacity = Read<float[]>("_pendingTime").Length;
            float due = Time.time + 100f;
            for (int i = 0; i < capacity; i++) Hold(i, i == 0 ? Time.time - 1f : due + i);
            Hold(999, due + 500f);

            Assert.AreEqual(capacity, Read<int>("_pendingCount"));
            var points = Read<Vector3[]>("_pendingPoint");
            var times = Read<float[]>("_pendingTime");
            CollectionAssert.DoesNotContain(points, Vector3.zero);
            CollectionAssert.Contains(points, Vector3.right * 999);
            for (int i = 1; i < capacity; i++)
            {
                int slot = System.Array.IndexOf(points, Vector3.right * i);
                Assert.GreaterOrEqual(slot, 0);
                Assert.AreEqual(due + i, times[slot]);
            }
        }

        [Test]
        public void FullQueue_DrainsAfterDueTimesAndAcceptsNewHits()
        {
            var times = Read<float[]>("_pendingTime");
            for (int i = 0; i < times.Length; i++) Hold(i, Time.time + 100f);
            Hold(999, Time.time + 200f);
            // Advance every queued deadline without a wall-clock wait.
            for (int i = 0; i < times.Length; i++) times[i] = Time.time - 1f;
            Invoke("DeliverPending");
            Assert.AreEqual(0, Read<int>("_pendingCount"));
            Hold(1000, Time.time + 100f);
            Assert.AreEqual(1, Read<int>("_pendingCount"));
            Assert.AreEqual(Vector3.right * 1000, Read<Vector3[]>("_pendingPoint")[0]);
        }

        private void Hold(int sample, float due)
        {
            // A missing target tests queue timing independently of canvas rendering.
            Invoke("Hold", -1, Vector3.right * sample, Vector3.up, 0.1f, sample + 1f, 0f, due);
        }

        private T Read<T>(string name) => (T)typeof(LiquidNozzle).GetField(name, PrivateInstance).GetValue(_nozzle);
        private void Invoke(string name, params object[] arguments) =>
            typeof(LiquidNozzle).GetMethod(name, PrivateInstance).Invoke(_nozzle, arguments);
    }
}
