using System;
using NUnit.Framework;
using SabaProps.Llama;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SabaProps.Llama.CITests
{
    public sealed class LlamaGpuTests
    {
        private static void RequireGpu()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("A real graphics device is required; -nographics is not numerical validation.");
        }

        private static Color[] Read(RenderTexture target)
        {
            var previous = RenderTexture.active;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBAFloat, false, true);
            try
            {
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                texture.Apply();
                return texture.GetPixels();
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(texture); }
        }

        private static void DestroyModel(LlamaModelAsset model)
        {
            foreach (var material in model.materials) UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(model.weights);
            UnityEngine.Object.DestroyImmediate(model);
        }

        [TestCase(true, 1)]
        [TestCase(false, 1)]
        [TestCase(true, 2)]
        [TestCase(false, 2)]
        public void FullForwardMatchesScalarReference(bool shared, int kvHeads)
        {
            RequireGpu();
            var checkpoint = LlamaCheckpoint.Fixture(shared, kvHeads);
            var model = LlamaProgramBuilder.Build(checkpoint, LlamaTokenizer.Fixture(), 4);
            try
            {
                Assert.IsFalse(ShaderUtil.ShaderHasError(model.materials[0].shader));
                using (var gpu = new LlamaGpuSession(model))
                using (var isolated = new LlamaGpuSession(model))
                {
                    var cpu = new LlamaReference(checkpoint);
                    Action<int> compare = token =>
                    {
                        gpu.Forward(token);
                        float[] expected = cpu.Forward(token);
                        Color[] actual = Read(gpu.Logits);
                        int best = 0;
                        for (int i = 0; i < expected.Length; i++)
                        {
                            Assert.That(actual[i].r, Is.EqualTo(expected[i]).Within(2e-4f), "position=" + cpu.Position + " token=" + i);
                            if (expected[i] > expected[best]) best = i;
                        }
                        Color result = Read(gpu.Result)[0];
                        Assert.That(result.b, Is.EqualTo(1));
                        Assert.That(result.g, Is.EqualTo(best));
                    };
                    compare(1); compare(4);
                    // Interleave another instance with the same imported assets.
                    isolated.Forward(7);
                    compare(6); compare(5);
                    Assert.Throws<ArgumentOutOfRangeException>(() => gpu.Forward(1));
                    gpu.Reset(); cpu.Reset();
                    compare(1); compare(7);
                }
            }
            finally { DestroyModel(model); }
        }

        [Test]
        public void TextureAndMaterialSettingsSurviveAssetImport()
        {
            RequireGpu();
            var checkpoint = LlamaCheckpoint.Fixture();
            var model = LlamaProgramBuilder.Build(checkpoint, LlamaTokenizer.Fixture(), 4);
            string folder = null;
            try
            {
                model.sourceAndLicense = "test fixture / MIT";
                folder = LlamaImportWindow.Save(model, "ImportTest");
                AssetDatabase.ImportAsset(folder + "/Weights.asset", ImportAssetOptions.ForceUpdate);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "/Weights.asset");
                Assert.That(texture.format, Is.EqualTo(TextureFormat.RFloat));
                Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(texture.mipmapCount, Is.EqualTo(1));
                for (int i = 0; i < checkpoint.weights.Length; i++)
                    Assert.That(texture.GetPixel(i % 4096, i / 4096).r, Is.EqualTo(checkpoint.weights[i]));
                var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "/LlamaPass003.mat");
                Assert.That(material.GetInt("_Operation"), Is.EqualTo(3));
                Assert.That(material.GetInt("_Count"), Is.EqualTo(8));
                Assert.That(material.GetInt("_OffsetLo"), Is.EqualTo(checkpoint.layout.query % 65536));
                using (var gpu = new LlamaGpuSession(AssetDatabase.LoadAssetAtPath<LlamaModelAsset>(folder + "/Model.asset")))
                {
                    gpu.Forward(1);
                    var expected = new LlamaReference(checkpoint).Forward(1);
                    var actual = Read(gpu.Logits);
                    for (int i = 0; i < expected.Length; i++) Assert.That(actual[i].r, Is.EqualTo(expected[i]).Within(2e-4));
                }
            }
            finally
            {
                if (folder != null) AssetDatabase.DeleteAsset(folder);
                else DestroyModel(model);
            }
        }
    }
}
