using NUnit.Framework;
using SabaProps.Liquid.Editors;
using UdonSharp.Compiler;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Components;

namespace SabaProps.Liquid.WorldTests
{
    public class LiquidApartmentPlayTests : LiquidPlayroomPlayTests
    {
        protected override void CreateScene() { LiquidApartmentScene.Create(); }
        protected override void CaptureSurface(string phase)
        {
            bool asynchronous = UnityEditor.ShaderUtil.allowAsyncCompilation;
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
            try
            {
                LiquidDemoPlayTests.Capture("liquid-apartment-" + phase + ".png", new Vector3(2.7f, 1.8f, -1.5f), new Vector3(5, 0.55f, 0), 75);
            }
            finally { UnityEditor.ShaderUtil.allowAsyncCompilation = asynchronous; }
        }
    }

    public class LiquidApartmentSceneTests
    {
        [OneTimeSetUp]
        public void CreateScene()
        {
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
            LiquidApartmentScene.Create();
            EditorSceneManager.OpenScene(LiquidApartmentScene.ScenePath);
            Physics.SyncTransforms();
        }

        [OneTimeTearDown]
        public void CloseScene() { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); }

        [Test]
        public void Doorways_ArePassableAndRoomsHavePaintableFloors()
        {
            foreach (Vector3 point in new[] { new Vector3(-1.9f, 1.1f, -1), new Vector3(1, 1.1f, -2.7f), new Vector3(-1.5f, 1.1f, 5.1f) })
                Assert.IsFalse(Physics.CheckSphere(point, 0.35f, 1, QueryTriggerInteraction.Ignore), "blocked passage " + point);
            var surface = Object.FindObjectOfType<LiquidCanvasPool>().surfaces[0];
            foreach (Vector3 point in new[] { new Vector3(-1, 1, 1), new Vector3(4, 1, 3), new Vector3(3, 1, -2), new Vector3(-2, 1, -3), new Vector3(-2, 1, 6) })
            {
                Assert.IsTrue(Physics.Raycast(point, Vector3.down, out RaycastHit hit, 2, 1, QueryTriggerInteraction.Ignore));
                Vector3 local = surface.anchor.InverseTransformPoint(hit.point);
                Assert.LessOrEqual(Mathf.Abs(local.x), surface.halfExtents.x);
                Assert.LessOrEqual(Mathf.Abs(local.y), surface.halfExtents.y);
                Assert.LessOrEqual(Mathf.Abs(local.z), surface.halfExtents.z);
            }
        }

        [Test]
        public void RoundedFurniture_HasOutwardFacesAndMetricUVs()
        {
            Mesh mesh = LiquidApartmentMesh.Build(new Vector3(2, 1, 3), 0.05f);
            try
            {
                Assert.AreEqual(new Vector3(2, 1, 3), mesh.bounds.size);
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                int[] indices = mesh.triangles;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                    Vector3 cross = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                    Assert.Greater(Vector3.Dot(cross, normals[a] + normals[b] + normals[c]), 0);
                }
                Assert.Greater(mesh.uv[35].y - mesh.uv[0].y, 2.9f);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void HouseholdProps_AreConnectedAndBalloonRespawnsStayInBathroom()
        {
            var pool = Object.FindObjectOfType<LiquidCanvasPool>();
            Assert.AreEqual(8, pool.canvases.Length);
            Assert.AreEqual(3, Object.FindObjectsOfType<LiquidShower>().Length);
            Assert.AreEqual(2, Object.FindObjectsOfType<LiquidImmersionVolume>().Length);
            Assert.AreEqual(2, Object.FindObjectsOfType<LiquidUmbrella>().Length);
            Assert.AreEqual(4, Object.FindObjectsOfType<LiquidLightZone>().Length);
            Assert.IsNotNull(Object.FindObjectOfType<LiquidWeather>());
            Assert.IsNotNull(Object.FindObjectOfType<LiquidHumidity>().linkedShower);
            foreach (var shower in Object.FindObjectsOfType<LiquidShower>()) Assert.AreEqual(pool, shower.pool);
            foreach (var balloon in Object.FindObjectsOfType<LiquidWaterBalloon>())
                Assert.Less(Vector3.Distance(balloon.transform.position, balloon.respawnPoint.position), 0.001f);
            foreach (var nozzle in Object.FindObjectsOfType<LiquidNozzle>()) Assert.AreEqual(pool, nozzle.pool);
            LiquidSampleSceneTests.AssertEveryProgramLoads();
            bool asynchronous = UnityEditor.ShaderUtil.allowAsyncCompilation;
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
            try
            {
            LiquidDemoPlayTests.Capture("liquid-apartment-living.png", new Vector3(-0.2f, 2.6f, -0.7f), new Vector3(-2.8f, 1.1f, 2.5f), 85);
            LiquidDemoPlayTests.Capture("liquid-apartment-bath.png", new Vector3(1.25f, 2.5f, -1.2f), new Vector3(4.4f, 0.9f, -2.5f), 90);
            LiquidDemoPlayTests.Capture("liquid-apartment-kitchen.png", new Vector3(0.5f, 2.4f, 1.3f), new Vector3(3.7f, 1.2f, 3.5f), 85);
            AssertNoErrorMaterial("liquid-apartment-living.png");
            AssertNoErrorMaterial("liquid-apartment-kitchen.png");
            }
            finally { UnityEditor.ShaderUtil.allowAsyncCompilation = asynchronous; }
        }

        private static void AssertNoErrorMaterial(string file)
        {
            var image = new Texture2D(2, 2);
            try
            {
                image.LoadImage(System.IO.File.ReadAllBytes(System.IO.Path.Combine("TestResults", file)));
                int pink = 0;
                foreach (Color32 pixel in image.GetPixels32())
                    if (pixel.r > 245 && pixel.b > 245 && pixel.g < 10) pink++;
                Assert.Less(pink, image.width * image.height / 1000, "error-material pixels in " + file);
            }
            finally { Object.DestroyImmediate(image); }
        }
    }
}
