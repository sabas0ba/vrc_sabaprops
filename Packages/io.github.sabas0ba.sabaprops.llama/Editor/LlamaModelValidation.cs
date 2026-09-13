using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SabaProps.Llama
{
    public static class LlamaModelValidation
    {
        // A short, synchronous Editor diagnostic; never runs inside a world.
        public static string Compare(LlamaModelAsset model)
        {
            if (model == null) throw new InvalidOperationException("変換済みモデルを選択してください。");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new NotSupportedException("実GPUが必要です。");
            var layout = new LlamaLayout(model.config); var weights = new float[layout.count];
            var pixels = model.weights.GetPixelData<float>(0);
            for (int i = 0; i < weights.Length; i++) weights[i] = pixels[i];
            var cpu = new LlamaReference(new LlamaCheckpoint(model.config, weights));
            float maximum = 0; int steps = Math.Min(4, model.context), token = 1;
            using (var gpu = new LlamaGpuSession(model))
            {
                for (int position = 0; position < steps; position++)
                {
                    var expected = cpu.Forward(token); gpu.Forward(token);
                    var previous = RenderTexture.active;
                    var read = new Texture2D(gpu.Logits.width, gpu.Logits.height, TextureFormat.RGBAFloat, false, true);
                    try
                    {
                        RenderTexture.active = gpu.Logits;
                        read.ReadPixels(new Rect(0, 0, read.width, read.height), 0, 0); read.Apply();
                        Color[] actual = read.GetPixels(); int cpuBest = 0, gpuBest = 0;
                        for (int i = 0; i < expected.Length; i++)
                        {
                            float error = Math.Abs(actual[i].r - expected[i]); maximum = Math.Max(maximum, error);
                            if (float.IsNaN(actual[i].r) || float.IsInfinity(actual[i].r) || error > 2e-3f + 2e-4f * Math.Abs(expected[i]))
                                throw new InvalidOperationException("CPU/GPU不一致: position=" + position + " logit=" + i + " error=" + error);
                            if (expected[i] > expected[cpuBest]) cpuBest = i;
                            if (actual[i].r > actual[gpuBest].r) gpuBest = i;
                        }
                        if (cpuBest != gpuBest) throw new InvalidOperationException("CPU/GPUのgreedy tokenが一致しません。");
                        token = cpuBest;
                    }
                    finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(read); }
                }
            }
            return steps + " tokensの全logit・argmaxが一致。最大絶対誤差=" + maximum.ToString("G6") + "。VRChat実機・llama.cppとの一致は別途確認してください。";
        }
    }
}
