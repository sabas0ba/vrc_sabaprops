using NUnit.Framework;
using SabaProps.Liquid.Editors;
using UdonSharp.Compiler;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Components;

namespace SabaProps.Liquid.WorldTests
{
    public class LiquidPlayroomSceneTests
    {
        [OneTimeSetUp]
        public void CreateScene()
        {
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
            LiquidPlayroomScene.Create();
            EditorSceneManager.OpenScene(LiquidPlayroomScene.ScenePath);
            Physics.SyncTransforms();
        }

        [OneTimeTearDown]
        public void CloseScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void Room_IsEnclosedAndCoveredByOneRegisteredCanvas()
        {
            var pool = Object.FindObjectOfType<LiquidCanvasPool>();
            Assert.AreEqual(8, pool.canvases.Length);
            Assert.AreEqual(1, pool.surfaces.Length);
            Vector3 origin = new Vector3(0, 1.8f, 0);
            foreach (Vector3 direction in new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back })
            {
                Assert.IsTrue(Physics.Raycast(origin, direction, out RaycastHit hit, 7, 1));
                var surface = pool.surfaces[0];
                Vector3 local = surface.transform.InverseTransformPoint(hit.point);
                Assert.LessOrEqual(Mathf.Abs(local.x), surface.halfExtents.x);
                Assert.LessOrEqual(Mathf.Abs(local.y), surface.halfExtents.y);
                Assert.LessOrEqual(Mathf.Abs(local.z), surface.halfExtents.z);
            }
            foreach (Transform spawn in Object.FindObjectOfType<VRCSceneDescriptor>().spawns)
                Assert.IsTrue(Physics.Raycast(spawn.position + Vector3.up, Vector3.down, 2, 1));
        }

        [Test]
        public void SharedRoom_HasIndependentToolsAndContact()
        {
            var pool = Object.FindObjectOfType<LiquidCanvasPool>();
            Assert.IsNotNull(pool.GetComponent<LiquidContact>());
            Assert.AreEqual(6, Object.FindObjectsOfType<LiquidNozzle>().Length);
            Assert.AreEqual(8, Object.FindObjectsOfType<LiquidPaintTool>().Length);
            Assert.AreEqual(6, Object.FindObjectsOfType<LiquidWipeTool>().Length);
            Assert.AreEqual(2, Object.FindObjectsOfType<LiquidWaterBalloon>().Length);
            foreach (var nozzle in Object.FindObjectsOfType<LiquidNozzle>())
            {
                Assert.AreEqual(pool, nozzle.pool);
                Assert.IsNotNull(nozzle.pickup);
                Assert.IsNotNull(nozzle.pickup.GetComponent<VRCObjectSync>());
                Assert.AreNotEqual(nozzle.gameObject, nozzle.pickup.gameObject);
            }
            LiquidSampleSceneTests.AssertEveryProgramLoads();
            LiquidDemoPlayTests.Capture("liquid-playroom-overview.png", new Vector3(0, 2.8f, -4.85f), new Vector3(0, 1.1f, 2), 85);
        }
    }
}
