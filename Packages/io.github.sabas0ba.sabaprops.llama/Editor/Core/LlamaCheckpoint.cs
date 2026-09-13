using System;
using System.IO;
using System.Security.Cryptography;

namespace SabaProps.Llama
{
    [Serializable]
    public sealed class LlamaConfig
    {
        public int dimension, hiddenDimension, layers, heads, kvHeads, vocabulary, sequenceLength;
        public bool sharedClassifier;
        public int HeadSize => dimension / heads;
        public int KvDimension => HeadSize * kvHeads;

        public void Validate()
        {
            if (dimension < 2 || dimension > 1024 || hiddenDimension < 1 || hiddenDimension > 4096 ||
                layers < 1 || layers > 16 || heads < 1 || heads > dimension ||
                kvHeads < 1 || kvHeads > heads || vocabulary < 3 || vocabulary > 32768 ||
                sequenceLength < 1 || sequenceLength > 2048)
                throw new InvalidDataException("モデルの寸法が対応範囲外です。");
            if (dimension % heads != 0 || heads % kvHeads != 0 || HeadSize % 2 != 0)
                throw new InvalidDataException("head寸法は偶数、query head数はKV head数の倍数である必要があります。");
        }
    }

    // Offsets are float indices, never byte offsets or floating point uniforms.
    public sealed class LlamaLayout
    {
        public readonly int embedding, attentionNorm, query, key, value, output, ffnNorm;
        public readonly int gate, down, up, finalNorm, legacyRope, classifier, count;
        private long cursor;
        private int Take(long length)
        {
            long start = cursor;
            cursor += length;
            if (cursor > 64L * 1024 * 1024)
                throw new InvalidDataException("重みが256 MiBの上限を超えています。");
            return (int)start;
        }

        public LlamaLayout(LlamaConfig c)
        {
            c.Validate();
            long d = c.dimension, l = c.layers, h = c.hiddenDimension, k = c.KvDimension;
            embedding = Take(c.vocabulary * d);
            attentionNorm = Take(l * d);
            query = Take(l * d * d);
            key = Take(l * k * d);
            value = Take(l * k * d);
            output = Take(l * d * d);
            ffnNorm = Take(l * d);
            gate = Take(l * h * d);
            down = Take(l * d * h);
            up = Take(l * h * d);
            finalNorm = Take(d);
            legacyRope = Take((long)c.sequenceLength * c.HeadSize);
            classifier = c.sharedClassifier ? embedding : Take(c.vocabulary * d);
            count = (int)cursor;
        }
    }

    public sealed class LlamaCheckpoint
    {
        public readonly LlamaConfig config;
        public readonly LlamaLayout layout;
        public readonly float[] weights;
        public readonly string sha256;

        public LlamaCheckpoint(LlamaConfig config, float[] weights, string sha256 = "generated")
        {
            this.config = config;
            layout = new LlamaLayout(config);
            if (weights == null || weights.Length != layout.count)
                throw new InvalidDataException("重みの要素数が一致しません。");
            foreach (float value in weights)
                if (float.IsNaN(value) || float.IsInfinity(value))
                    throw new InvalidDataException("重みにNaNまたはInfinityが含まれます。");
            this.weights = weights;
            this.sha256 = sha256;
        }

        public static LlamaCheckpoint Read(string path)
        {
            using (var file = File.OpenRead(path)) return Read(file);
        }

        public static LlamaCheckpoint Read(Stream stream)
        {
            if (!stream.CanSeek || stream.Length < 28)
                throw new InvalidDataException("FP32 v0 checkpointのヘッダーがありません。");
            stream.Position = 0;
            using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
            {
                var c = new LlamaConfig
                {
                    dimension = reader.ReadInt32(), hiddenDimension = reader.ReadInt32(),
                    layers = reader.ReadInt32(), heads = reader.ReadInt32(), kvHeads = reader.ReadInt32()
                };
                int signedVocabulary = reader.ReadInt32();
                if (signedVocabulary == int.MinValue) throw new InvalidDataException("語彙数が不正です。");
                c.vocabulary = Math.Abs(signedVocabulary);
                c.sharedClassifier = signedVocabulary > 0;
                c.sequenceLength = reader.ReadInt32();
                var layout = new LlamaLayout(c);
                if (stream.Length != 28L + 4L * layout.count)
                    throw new InvalidDataException("ファイル長がFP32 v0形式と一致しません。GGUF・量子化形式は未対応です。");
                var weights = new float[layout.count];
                for (int i = 0; i < weights.Length; i++) weights[i] = reader.ReadSingle();
                stream.Position = 0;
                string hash;
                using (var sha = SHA256.Create())
                    hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                return new LlamaCheckpoint(c, weights, hash);
            }
        }

        public void Write(Stream stream)
        {
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                writer.Write(config.dimension); writer.Write(config.hiddenDimension);
                writer.Write(config.layers); writer.Write(config.heads); writer.Write(config.kvHeads);
                writer.Write(config.sharedClassifier ? config.vocabulary : -config.vocabulary);
                writer.Write(config.sequenceLength);
                foreach (float weight in weights) writer.Write(weight);
            }
        }

        // A numerical fixture, not a trained language model. No third-party weights are bundled.
        public static LlamaCheckpoint Fixture(bool shared = true, int kvHeads = 1)
        {
            var c = new LlamaConfig { dimension = 8, hiddenDimension = 12, layers = 2,
                heads = 2, kvHeads = kvHeads, vocabulary = 8, sequenceLength = 8, sharedClassifier = shared };
            var layout = new LlamaLayout(c);
            var weights = new float[layout.count];
            uint seed = 12345;
            for (int i = 0; i < weights.Length; i++)
            {
                seed = unchecked(seed * 1664525u + 1013904223u);
                weights[i] = ((seed >> 8) / 16777216f - 0.5f) * 0.6f;
            }
            for (int i = 0; i < c.layers * c.dimension; i++)
                weights[layout.attentionNorm + i] = weights[layout.ffnNorm + i] = 1;
            for (int i = 0; i < c.dimension; i++) weights[layout.finalNorm + i] = 1;
            return new LlamaCheckpoint(c, weights);
        }
    }
}
