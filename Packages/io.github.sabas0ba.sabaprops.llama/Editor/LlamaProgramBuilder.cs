using System;
using System.Collections.Generic;
using UnityEngine;

namespace SabaProps.Llama
{
    public static class LlamaProgramBuilder
    {
        public static LlamaModelAsset Build(LlamaCheckpoint checkpoint, LlamaTokenizer tokenizer, int context)
        {
            var c = checkpoint.config;
            var w = checkpoint.layout;
            if (context < 1 || context > Math.Min(c.sequenceLength, 256))
                throw new ArgumentOutOfRangeException(nameof(context), "contextはモデルの上限以内、最大256です。");
            if (tokenizer.pieces.Length != c.vocabulary) throw new ArgumentException("語彙数が一致しません。");
            Shader shader = Shader.Find("SabaProps/Llama/Inference");
            if (shader == null) throw new InvalidOperationException("推論shaderが見つかりません。");
            var model = ScriptableObject.CreateInstance<LlamaModelAsset>();
            model.config = c;
            model.tokenizer = tokenizer;
            model.context = context;
            model.checkpointSha256 = checkpoint.sha256;
            var widths = new List<int>();
            var heights = new List<int>();
            var materials = new List<Material>();
            var inputA = new List<int>();
            var inputB = new List<int>();
            var targets = new List<int>();
            try
            {
                int height = (checkpoint.weights.Length + 4095) / 4096;
                model.weights = new Texture2D(4096, height, TextureFormat.RFloat, false, true)
                {
                    name = "LlamaWeights", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                    anisoLevel = 0
                };
                var padded = new float[4096 * height];
                Array.Copy(checkpoint.weights, padded, checkpoint.weights.Length);
                model.weights.SetPixelData(padded, 0);
                model.weights.Apply(false, false);

                int Buffer(int width, int rows = 1)
                {
                    widths.Add(width); heights.Add(rows); return widths.Count - 1;
                }
                void Step(int op, int a, int b, int target, int count, int inner = 0, int offset = 0, int rope = 0)
                {
                    if (a == target || b == target) throw new InvalidOperationException("読み書き先が重複しています。");
                    var material = new Material(shader) { name = "LlamaPass" + materials.Count.ToString("D3") };
                    material.SetTexture("_Weights", model.weights);
                    material.SetInt("_Operation", op);
                    // Legacy Material.SetInt serializes through float; split large offsets exactly.
                    material.SetInt("_OffsetLo", offset % 65536); material.SetInt("_OffsetHi", offset / 65536);
                    material.SetInt("_Count", count); material.SetInt("_Inner", inner);
                    material.SetInt("_Width", widths[target]);
                    material.SetInt("_AWidth", a < 0 ? 1 : widths[a]);
                    material.SetInt("_HeadSize", c.HeadSize); material.SetInt("_KvMultiple", c.heads / c.kvHeads);
                    material.SetInt("_Rope", rope);
                    materials.Add(material); inputA.Add(a); inputB.Add(b); targets.Add(target);
                }
                int d = c.dimension, k = c.KvDimension, h = c.hiddenDimension;
                int x = Buffer(d), residual = Buffer(d), normal = Buffer(d), statistic = Buffer(1);
                int q0 = Buffer(d), q = Buffer(d), k0 = Buffer(k), v0 = Buffer(k);
                int scores = Buffer(context, c.heads), probabilities = Buffer(context, c.heads);
                int attention = Buffer(d), projected = Buffer(d), gate = Buffer(h), up = Buffer(h), hidden = Buffer(h);
                int keyScratch = Buffer(k, context), valueScratch = Buffer(k, context);
                void Norm(int source, int offset)
                {
                    Step(1, source, -1, statistic, 1, d);
                    Step(2, source, statistic, normal, d, 0, offset);
                }
                Step(0, -1, -1, x, d, 0, w.embedding);
                for (int layer = 0; layer < c.layers; layer++)
                {
                    int keys = Buffer(k, context), values = Buffer(k, context);
                    Norm(x, w.attentionNorm + layer * d);
                    Step(3, normal, -1, q0, d, d, w.query + layer * d * d);
                    Step(3, normal, -1, k0, k, d, w.key + layer * k * d);
                    Step(3, normal, -1, v0, k, d, w.value + layer * k * d);
                    Step(4, q0, -1, q, d);
                    Step(5, k0, keys, keyScratch, k * context, 0, 0, 1);
                    Step(11, keyScratch, -1, keys, k * context);
                    Step(5, v0, values, valueScratch, k * context);
                    Step(11, valueScratch, -1, values, k * context);
                    Step(6, q, keys, scores, context * c.heads);
                    Step(7, scores, -1, probabilities, context * c.heads);
                    Step(8, probabilities, values, attention, d);
                    Step(3, attention, -1, projected, d, d, w.output + layer * d * d);
                    Step(9, x, projected, residual, d);
                    Norm(residual, w.ffnNorm + layer * d);
                    Step(3, normal, -1, gate, h, d, w.gate + layer * h * d);
                    Step(3, normal, -1, up, h, d, w.up + layer * h * d);
                    Step(10, gate, up, hidden, h);
                    Step(3, hidden, -1, projected, d, h, w.down + layer * d * h);
                    Step(9, residual, projected, x, d);
                }
                Norm(x, w.finalNorm);
                model.logitsTarget = Buffer(256, (c.vocabulary + 255) / 256);
                Step(3, normal, -1, model.logitsTarget, c.vocabulary, d, w.classifier);
                int groups = (c.vocabulary + 255) / 256;
                int reduced = Buffer(groups);
                Step(12, model.logitsTarget, -1, reduced, c.vocabulary);
                model.resultTarget = Buffer(1);
                Step(13, reduced, -1, model.resultTarget, groups);
                model.materials = materials.ToArray();
                model.inputA = inputA.ToArray(); model.inputB = inputB.ToArray(); model.targets = targets.ToArray();
                model.widths = widths.ToArray(); model.heights = heights.ToArray();
                return model;
            }
            catch
            {
                foreach (var material in materials) UnityEngine.Object.DestroyImmediate(material);
                if (model.weights != null) UnityEngine.Object.DestroyImmediate(model.weights);
                UnityEngine.Object.DestroyImmediate(model);
                throw;
            }
        }
    }
}
