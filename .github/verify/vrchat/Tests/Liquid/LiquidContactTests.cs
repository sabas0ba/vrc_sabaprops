using NUnit.Framework;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEngine;

namespace SabaProps.Liquid.WorldTests
{
    public class LiquidContactTests
    {
        private Material _material;
        private Texture2D _pigment, _film, _depth;
        private RenderTexture _result;

        [SetUp]
        public void SetUp()
        {
            _material = new Material(Shader.Find("Hidden/SabaProps/Liquid/Contact"));
            _pigment = Solid(new Color(0.8f, 0, 0, 0.8f));
            _film = Solid(new Color(1, 0.9f, 0, 0));
            _depth = Solid(new Color(0, 1, 0, 0));
            _result = new RenderTexture(192, 128, 0, RenderTextureFormat.ARGBFloat);
            _result.Create();
            _material.SetVector("_DonorPoint", Vector3.zero);
            _material.SetVector("_ReceiverPoint", Vector3.zero);
            _material.SetVector("_DonorNormal", Vector3.forward);
            _material.SetVector("_ReceiverNormal", Vector3.back);
            _material.SetVector("_DonorExtent", Vector3.one);
            _material.SetVector("_ReceiverExtent", Vector3.one);
            _material.SetFloat("_DonorInset", 0.02f);
            _material.SetFloat("_ReceiverInset", 0.02f);
            _material.SetVector("_MapX", Vector3.right);
            _material.SetVector("_MapY", Vector3.up);
            _material.SetVector("_MapZ", Vector3.forward);
            _material.SetVector("_Contact", new Vector4(0.15f, 0.5f, 0, 1));
            _material.SetTexture("_DonorPigment", _pigment);
            _material.SetTexture("_DonorFilm", _film);
            _material.SetTexture("_DonorDepth", _depth);
            _material.SetTexture("_DonorTex", _pigment);
        }

        private static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        [TearDown]
        public void TearDown()
        {
            if (RenderTexture.active == _result) RenderTexture.active = null;
            foreach (Object asset in new Object[] { _material, _pigment, _film, _depth, _result }) Object.DestroyImmediate(asset);
        }

        private Color Read(int x, int y)
        {
            RenderTexture previous = RenderTexture.active;
            var read = new Texture2D(192, 128, TextureFormat.RGBAFloat, false, true);
            try
            {
                RenderTexture.active = _result;
                read.ReadPixels(new Rect(0, 0, 192, 128), 0, 0);
                read.Apply();
                return read.GetPixel(x, y);
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(read); }
        }

        [Test]
        public void Transfer_WritesOppositeFace_AndReducesDonor()
        {
            Graphics.Blit(Texture2D.blackTexture, _result, _material, 0);
            float received = Read(160, 96).r;
            Assert.Greater(received, 0.05f);
            Assert.AreEqual(0, Read(96, 96).r, 0.001f);
            Graphics.Blit(_pigment, _result, _material, 1);
            Assert.AreEqual(0.8f, Read(96, 96).r + received, 0.01f);
            Assert.AreEqual(0.8f, Read(160, 96).r, 0.001f);
        }

        [Test]
        public void DryPaint_TransfersLess_AndDistantDepthDoesNotTransfer()
        {
            Graphics.Blit(Texture2D.blackTexture, _result, _material, 0);
            float wet = Read(160, 96).r;
            _film.SetPixel(0, 0, Color.clear); _film.Apply();
            Graphics.Blit(Texture2D.blackTexture, _result, _material, 0);
            Assert.Less(Read(160, 96).r, wet * 0.2f);
            _depth.SetPixel(0, 0, new Color(1, 1, 0, 0)); _depth.Apply();
            Graphics.Blit(Texture2D.blackTexture, _result, _material, 0);
            Assert.AreEqual(0, Read(160, 96).r, 0.001f);
        }

        [Test]
        public void Transfer_RecordsReceiverDepth_AndCarriesGlow()
        {
            _material.SetFloat("_Channel", 2);
            Graphics.Blit(Texture2D.blackTexture, _result, _material, 0);
            Assert.AreEqual(1, Read(160, 96).g, 0.001f);
            _material.SetFloat("_Channel", 3);
            Graphics.Blit(Texture2D.blackTexture, _result, _material, 0);
            Assert.Greater(Read(160, 96).r, 0.05f);
        }

        [TestCase(true, false)]
        [TestCase(false, false)]
        [TestCase(true, true)]
        public void Smear_MovesOnlyWetPaintAtContactDepth(bool wet, bool distant)
        {
            var pattern = new Texture2D(192, 128, TextureFormat.RGBAFloat, false, true);
            try
            {
                for (int y = 0; y < 128; y++)
                    for (int x = 0; x < 192; x++)
                        pattern.SetPixel(x, y, new Color(x / 192f, 0, 0, 1));
                pattern.Apply();
                _material.SetTexture("_DonorPigment", pattern);
                _material.SetVector("_Contact", new Vector4(0.3f, 0.5f, 1f, 1f));
                _film.SetPixel(0, 0, new Color(wet ? 1 : 0, 0.9f, 0, 0));
                _film.Apply();
                _depth.SetPixel(0, 0, new Color(distant ? 1 : 0, 1, 0, 0));
                _depth.Apply();
                _material.SetVector("_Motion", Vector3.zero);
                Graphics.Blit(pattern, _result, _material, 1);
                float still = Read(96, 96).r;
                _material.SetVector("_Motion", Vector3.right * 0.2f);
                Graphics.Blit(pattern, _result, _material, 1);
                float moved = Read(96, 96).r;
                if (wet && !distant) Assert.Less(moved, still - 0.001f);
                else Assert.AreEqual(still, moved, 0.0001f);
                Assert.AreEqual(pattern.GetPixel(160, 96).r, Read(160, 96).r, 0.001f,
                    "motion modified another atlas face");
                Assert.AreEqual(pattern.GetPixel(64, 96).r, Read(64, 96).r, 0.001f,
                    "motion modified pixels outside the contact patch");
            }
            finally { Object.DestroyImmediate(pattern); }
        }

        [Test]
        public void Behaviours_CompileToUdonPrograms()
        {
            var root = new GameObject("ContactPrograms");
            try
            {
                var contact = root.AddUdonSharpComponent<LiquidContact>();
                var wipe = root.AddUdonSharpComponent<LiquidWipeTool>();
                var splash = root.AddUdonSharpComponent<LiquidSplash>();
                var balloon = root.AddUdonSharpComponent<LiquidWaterBalloon>();
                var demo = root.AddUdonSharpComponent<LiquidContactDemo>();
                UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
                foreach (var behaviour in new UdonSharp.UdonSharpBehaviour[] { contact, wipe, splash, balloon, demo })
                {
                    var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour);
                    var asset = UdonSharpEditorUtility.GetUdonSharpProgramAsset(backing);
                    Assert.IsNotNull(asset.SerializedProgramAsset.RetrieveProgram(), behaviour.GetType().Name);
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(0.3f)]
        [TestCase(0.7f)]
        [TestCase(1.5f)]
        [TestCase(0f)]
        public void BalloonTrajectory_ReachesFeetFromAnElevatedOrigin(float seconds)
        {
            Vector3 origin = new Vector3(1, 1.4f, -0.5f);
            Vector3 target = new Vector3(-2, 0.08f, -4);
            Vector3 gravity = new Vector3(0, -9.81f, 0);
            Vector3 velocity = LiquidWaterBalloon.ThrowVelocity(origin, target, gravity, seconds);
            float time = Mathf.Clamp(seconds, 0.3f, 1.5f);
            Vector3 landing = origin + velocity * time + gravity * (0.5f * time * time);
            Assert.Less(Vector3.Distance(landing, target), 0.0001f);
        }

        [TestCase(64)]
        [TestCase(160)]
        [TestCase(256)]
        public void MeasureContactPass(int faceResolution)
        {
            var target = new RenderTexture(faceResolution * 3, faceResolution * 2, 0, RenderTextureFormat.ARGB32);
            var fence = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            RenderTexture previous = RenderTexture.active;
            try
            {
                target.Create();
                Graphics.Blit(_pigment, target, _material, 1);
                RenderTexture.active = target;
                fence.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                const int count = 100;
                for (int i = 0; i < count; i++) Graphics.Blit(_pigment, target, _material, 1);
                RenderTexture.active = target;
                fence.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                watch.Stop();
                TestContext.WriteLine("Device={0}; face={1}; passes={2}; batchMs={3:F3}; msPerPass={4:F4}; canvasBytes={5}",
                    SystemInfo.graphicsDeviceName, faceResolution, count, watch.Elapsed.TotalMilliseconds,
                    watch.Elapsed.TotalMilliseconds / count, faceResolution * faceResolution * 6 * 40);
                Assert.IsTrue(target.IsCreated());
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(fence);
            }
        }
    }
}
