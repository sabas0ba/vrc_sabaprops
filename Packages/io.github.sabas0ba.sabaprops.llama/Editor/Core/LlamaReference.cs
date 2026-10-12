using System;

namespace SabaProps.Llama
{
    // Deliberately scalar and editor-only; this is an independent numerical oracle.
    public sealed class LlamaReference
    {
        private readonly LlamaCheckpoint model;
        private readonly float[,,] keys, values;
        public int Position { get; private set; }

        public LlamaReference(LlamaCheckpoint model)
        {
            this.model = model;
            var c = model.config;
            keys = new float[c.layers, c.sequenceLength, c.KvDimension];
            values = new float[c.layers, c.sequenceLength, c.KvDimension];
        }

        public void Reset()
        {
            Position = 0;
            Array.Clear(keys, 0, keys.Length);
            Array.Clear(values, 0, values.Length);
        }

        private float[] Dense(float[] input, int offset, int rows)
        {
            var result = new float[rows];
            for (int row = 0; row < rows; row++)
                for (int column = 0; column < input.Length; column++)
                    result[row] += input[column] * model.weights[offset + row * input.Length + column];
            return result;
        }

        private float[] Normalize(float[] input, int offset)
        {
            float sum = 0;
            foreach (float x in input) sum += x * x;
            float scale = 1f / (float)Math.Sqrt(sum / input.Length + 1e-5f);
            var result = new float[input.Length];
            for (int i = 0; i < input.Length; i++) result[i] = input[i] * scale * model.weights[offset + i];
            return result;
        }

        private void Rotate(float[] vector)
        {
            int size = model.config.HeadSize;
            for (int i = 0; i < vector.Length; i += 2)
            {
                double angle = Position / Math.Pow(10000, (double)(i % size) / size);
                float sin = (float)Math.Sin(angle), cos = (float)Math.Cos(angle);
                float a = vector[i], b = vector[i + 1];
                vector[i] = a * cos - b * sin;
                vector[i + 1] = a * sin + b * cos;
            }
        }

        public float[] Forward(int token)
        {
            var c = model.config;
            var w = model.layout;
            int d = c.dimension, k = c.KvDimension, h = c.hiddenDimension;
            if (token < 0 || token >= c.vocabulary || Position >= c.sequenceLength)
                throw new ArgumentOutOfRangeException(nameof(token));
            var x = new float[d];
            Array.Copy(model.weights, w.embedding + token * d, x, 0, d);
            for (int layer = 0; layer < c.layers; layer++)
            {
                float[] n = Normalize(x, w.attentionNorm + layer * d);
                float[] q = Dense(n, w.query + layer * d * d, d);
                float[] key = Dense(n, w.key + layer * k * d, k);
                float[] value = Dense(n, w.value + layer * k * d, k);
                Rotate(q); Rotate(key);
                for (int i = 0; i < k; i++)
                {
                    keys[layer, Position, i] = key[i];
                    values[layer, Position, i] = value[i];
                }
                var attention = new float[d];
                for (int head = 0; head < c.heads; head++)
                {
                    int kvBase = head / (c.heads / c.kvHeads) * c.HeadSize;
                    var scores = new float[Position + 1];
                    float maximum = float.NegativeInfinity;
                    for (int t = 0; t <= Position; t++)
                    {
                        for (int i = 0; i < c.HeadSize; i++)
                            scores[t] += q[head * c.HeadSize + i] * keys[layer, t, kvBase + i];
                        scores[t] /= (float)Math.Sqrt(c.HeadSize);
                        maximum = Math.Max(maximum, scores[t]);
                    }
                    float denominator = 0;
                    for (int t = 0; t <= Position; t++)
                    {
                        scores[t] = (float)Math.Exp(scores[t] - maximum);
                        denominator += scores[t];
                    }
                    for (int i = 0; i < c.HeadSize; i++)
                        for (int t = 0; t <= Position; t++)
                            attention[head * c.HeadSize + i] += scores[t] / denominator * values[layer, t, kvBase + i];
                }
                float[] projected = Dense(attention, w.output + layer * d * d, d);
                for (int i = 0; i < d; i++) x[i] += projected[i];
                n = Normalize(x, w.ffnNorm + layer * d);
                float[] gate = Dense(n, w.gate + layer * h * d, h);
                float[] up = Dense(n, w.up + layer * h * d, h);
                for (int i = 0; i < h; i++) gate[i] = gate[i] / (1f + (float)Math.Exp(-gate[i])) * up[i];
                projected = Dense(gate, w.down + layer * d * h, d);
                for (int i = 0; i < d; i++) x[i] += projected[i];
            }
            Position++;
            return Dense(Normalize(x, w.finalNorm), w.classifier, c.vocabulary);
        }
    }
}
