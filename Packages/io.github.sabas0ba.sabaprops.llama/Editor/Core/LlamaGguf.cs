using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SabaProps.Llama
{
    // Deliberately bounded GGUF subset. All decoding happens in the Editor.
    public sealed class LlamaGguf
    {
        public readonly LlamaCheckpoint checkpoint;
        public readonly LlamaTokenizer tokenizer;
        private LlamaGguf(LlamaCheckpoint model, LlamaTokenizer vocabulary) { checkpoint = model; tokenizer = vocabulary; }
        public static LlamaGguf Read(string path) { using (var s = File.OpenRead(path)) return Read(s); }
        public static LlamaGguf Read(Stream stream)
        {
            try { return new Reader(stream).Read(); }
            catch (EndOfStreamException e) { throw new InvalidDataException("GGUFが途中で切れています。", e); }
            catch (OverflowException e) { throw new InvalidDataException("GGUFのサイズが上限を超えています。", e); }
            catch (DecoderFallbackException e) { throw new InvalidDataException("GGUFに不正なUTF-8があります。", e); }
        }

        private sealed class Tensor
        {
            public string name;
            public int columns, rows, rank;
            public uint type;
            public long offset, bytes;
        }
        private sealed class Reader
        {
            private readonly BinaryReader r;
            private readonly Stream s;
            private readonly Dictionary<string, object> meta = new Dictionary<string, object>(StringComparer.Ordinal);
            private readonly Dictionary<string, Tensor> tensors = new Dictionary<string, Tensor>(StringComparer.Ordinal);
            private long stringBudget = 16 * 1024 * 1024, arrayBudget = 262144, dataStart;
            public Reader(Stream stream)
            {
                if (stream == null || !stream.CanRead || !stream.CanSeek || stream.Length > 512L * 1024 * 1024)
                    throw new InvalidDataException("GGUFはseek可能な512 MiB以下のファイルが必要です。");
                s = stream; s.Position = 0; r = new BinaryReader(s, new UTF8Encoding(false, true), true);
            }
            private static int Bounded(ulong value, int max, string name)
            {
                if (value > (ulong)max) throw new InvalidDataException("GGUF上限: " + name);
                return (int)value;
            }
            private string String()
            {
                int n = Bounded(r.ReadUInt64(), 1024 * 1024, "string");
                stringBudget -= n;
                if (stringBudget < 0 || n > s.Length - s.Position) throw new InvalidDataException("GGUF文字列サイズが不正です。");
                return new UTF8Encoding(false, true).GetString(r.ReadBytes(n));
            }
            private object Value(uint type, bool array = false)
            {
                switch (type)
                {
                    case 0: return r.ReadByte(); case 1: return r.ReadSByte();
                    case 2: return r.ReadUInt16(); case 3: return r.ReadInt16();
                    case 4: return r.ReadUInt32(); case 5: return r.ReadInt32();
                    case 6: return r.ReadSingle();
                    case 7: byte b = r.ReadByte(); if (b > 1) throw new InvalidDataException("GGUF boolが不正です。"); return b != 0;
                    case 8: return String();
                    case 9:
                        if (array) throw new InvalidDataException("GGUF nested arrayは未対応です。");
                        uint itemType = r.ReadUInt32();
                        int count = Bounded(r.ReadUInt64(), 65536, "array");
                        arrayBudget -= count;
                        if (arrayBudget < 0) throw new InvalidDataException("GGUF array総数超過。");
                        var items = new object[count];
                        for (int i = 0; i < count; i++) items[i] = Value(itemType, true);
                        return items;
                    case 10: return r.ReadUInt64(); case 11: return r.ReadInt64(); case 12: return r.ReadDouble();
                    default: throw new InvalidDataException("未対応のGGUF metadata型: " + type);
                }
            }
            private T Get<T>(string key)
            {
                if (!meta.TryGetValue(key, out object value) || !(value is T)) throw new InvalidDataException("GGUF metadata不足/型不一致: " + key);
                return (T)value;
            }
            private int Number(string key, int fallback = -1)
            {
                if (!meta.ContainsKey(key) && fallback >= 0) return fallback;
                return Bounded(Get<uint>(key), int.MaxValue, key);
            }
            private void Expect<T>(string key, T expected)
            {
                if (meta.ContainsKey(key) && !EqualityComparer<T>.Default.Equals(Get<T>(key), expected))
                    throw new InvalidDataException("未対応のGGUF設定: " + key + "（対応値 " + expected + "）");
            }
            public LlamaGguf Read()
            {
                if (r.ReadUInt32() != 0x46554747) throw new InvalidDataException("GGUF magicがありません。");
                uint version = r.ReadUInt32();
                if (version != 2 && version != 3) throw new InvalidDataException("GGUF v2/v3 little-endianのみ対応します。");
                int nt = Bounded(r.ReadUInt64(), 256, "tensor count"), nm = Bounded(r.ReadUInt64(), 512, "metadata count");
                for (int i = 0; i < nm; i++)
                {
                    string key = String(); object value = Value(r.ReadUInt32());
                    if (meta.ContainsKey(key)) throw new InvalidDataException("重複metadata: " + key);
                    meta.Add(key, value);
                }
                if (Get<string>("general.architecture") != "llama" || Get<string>("tokenizer.ggml.model") != "llama")
                    throw new InvalidDataException("Llama / SentencePiece GGUFのみ対応します。");
                Expect("split.count", (ushort)1);
                Expect("llama.rope.freq_base", 10000f);
                Expect("llama.rope.scaling.type", "none");
                Expect("llama.rope.scale_linear", 1f);
                Expect("llama.attention.layer_norm_rms_epsilon", 1e-5f);
                Expect("tokenizer.ggml.bos_token_id", 1u); Expect("tokenizer.ggml.eos_token_id", 2u);
                Expect("tokenizer.ggml.unknown_token_id", 0u);
                Expect("tokenizer.ggml.add_bos_token", true); Expect("tokenizer.ggml.add_eos_token", false);
                Expect("tokenizer.ggml.add_space_prefix", true); Expect("tokenizer.ggml.remove_extra_whitespaces", false);
                var supported = new HashSet<string>(new[] {
                    "llama.context_length", "llama.embedding_length", "llama.feed_forward_length", "llama.block_count",
                    "llama.attention.head_count", "llama.attention.head_count_kv", "llama.attention.layer_norm_rms_epsilon",
                    "llama.attention.key_length", "llama.attention.value_length", "llama.rope.dimension_count",
                    "llama.rope.freq_base", "llama.rope.scaling.type", "llama.rope.scale_linear" }, StringComparer.Ordinal);
                foreach (string key in meta.Keys)
                    if (key.StartsWith("llama.", StringComparison.Ordinal) && !supported.Contains(key))
                        throw new InvalidDataException("未対応のGGUF演算: " + key);
                var tokens = Get<object[]>("tokenizer.ggml.tokens"); var scores = Get<object[]>("tokenizer.ggml.scores");
                var types = Get<object[]>("tokenizer.ggml.token_type");
                if (tokens.Length < 3 || tokens.Length > 32768 || scores.Length != tokens.Length || types.Length != tokens.Length)
                    throw new InvalidDataException("GGUF tokenizer配列長が不正です。");
                var tokenizer = new LlamaTokenizer { pieces = new string[tokens.Length], scores = new float[tokens.Length] };
                for (int i = 0; i < tokens.Length; i++)
                {
                    if (!(tokens[i] is string piece) || !(scores[i] is float score) || !(types[i] is int tokenType) ||
                        piece.Length > 4096 || float.IsNaN(score) || float.IsInfinity(score) || tokenType < 1 || tokenType > 6 || tokenType == 4 ||
                        tokenType == 3 && i != 1 && i != 2)
                        throw new InvalidDataException("未対応/不正なGGUF token: " + i);
                    // Legacy Stories15M GGUF contains one empty NORMAL piece (ID 30143).
                    // Preserve its ID and empty output; do not fabricate a byte or shift vocabulary IDs.
                    tokenizer.pieces[i] = piece.Replace('\u2581', ' '); tokenizer.scores[i] = score;
                }
                tokenizer.Prepare();
                var c = new LlamaConfig { dimension = Number("llama.embedding_length"), hiddenDimension = Number("llama.feed_forward_length"),
                    layers = Number("llama.block_count"), heads = Number("llama.attention.head_count"),
                    vocabulary = tokens.Length, sequenceLength = Number("llama.context_length") };
                c.kvHeads = Number("llama.attention.head_count_kv", c.heads); c.Validate();
                Expect("llama.rope.dimension_count", (uint)c.HeadSize);
                Expect("llama.attention.key_length", (uint)c.HeadSize); Expect("llama.attention.value_length", (uint)c.HeadSize);
                int alignment = Number("general.alignment", 32);
                if (alignment < 1 || alignment > 4096 || (alignment & (alignment - 1)) != 0) throw new InvalidDataException("GGUF alignmentが不正です。");
                for (int i = 0; i < nt; i++)
                {
                    var t = new Tensor { name = String(), rank = Bounded(r.ReadUInt32(), 2, "tensor rank") };
                    if (t.rank < 1) throw new InvalidDataException("GGUF tensor rankが不正です。");
                    t.columns = Bounded(r.ReadUInt64(), 32768, "tensor columns");
                    t.rows = t.rank == 2 ? Bounded(r.ReadUInt64(), 32768, "tensor rows") : 1;
                    t.type = r.ReadUInt32(); ulong offset = r.ReadUInt64();
                    if (offset > (ulong)s.Length || offset % (uint)alignment != 0 || t.columns == 0 || t.rows == 0)
                        throw new InvalidDataException("GGUF tensor offset/shapeが不正です。");
                    t.offset = (long)offset;
                    long count = (long)t.columns * t.rows;
                    if (t.type == 0) t.bytes = count * 4;
                    else if (t.type == 1) t.bytes = count * 2;
                    else if ((t.type == 2 || t.type == 8) && t.columns % 32 == 0) t.bytes = count / 32 * (t.type == 2 ? 18 : 34);
                    else throw new InvalidDataException("未対応GGUF tensor型/ブロック寸法: " + t.name + " type=" + t.type + "。F32/F16/Q4_0/Q8_0のみ対応。");
                    if (tensors.ContainsKey(t.name)) throw new InvalidDataException("重複tensor: " + t.name);
                    tensors.Add(t.name, t);
                }
                dataStart = (s.Position + alignment - 1) / alignment * alignment;
                var ordered = new List<Tensor>(tensors.Values); ordered.Sort((a, b) => a.offset.CompareTo(b.offset));
                long end = 0;
                foreach (var t in ordered)
                {
                    if (t.offset < end || t.bytes > s.Length - dataStart - t.offset) throw new InvalidDataException("GGUF tensorが重複/欠損しています: " + t.name);
                    end = t.offset + t.bytes;
                }
                c.sharedClassifier = !tensors.ContainsKey("output.weight");
                var layout = new LlamaLayout(c); var weights = new float[layout.count];
                Copy("token_embd.weight", c.dimension, c.vocabulary, 2, weights, layout.embedding);
                Copy("output_norm.weight", c.dimension, 1, 1, weights, layout.finalNorm);
                if (!c.sharedClassifier) Copy("output.weight", c.dimension, c.vocabulary, 2, weights, layout.classifier);
                for (int i = 0; i < c.layers; i++)
                {
                    string p = "blk." + i + "."; int d = c.dimension, h = c.hiddenDimension, k = c.KvDimension;
                    Copy(p + "attn_norm.weight", d, 1, 1, weights, layout.attentionNorm + i * d);
                    Copy(p + "attn_q.weight", d, d, 2, weights, layout.query + i * d * d);
                    Copy(p + "attn_k.weight", d, k, 2, weights, layout.key + i * k * d);
                    Copy(p + "attn_v.weight", d, k, 2, weights, layout.value + i * k * d);
                    Copy(p + "attn_output.weight", d, d, 2, weights, layout.output + i * d * d);
                    Copy(p + "ffn_norm.weight", d, 1, 1, weights, layout.ffnNorm + i * d);
                    Copy(p + "ffn_gate.weight", d, h, 2, weights, layout.gate + i * h * d);
                    Copy(p + "ffn_down.weight", h, d, 2, weights, layout.down + i * d * h);
                    Copy(p + "ffn_up.weight", d, h, 2, weights, layout.up + i * h * d);
                }
                // Older converters may store the otherwise implicit, unscaled RoPE frequencies.
                if (tensors.ContainsKey("rope_freqs.weight"))
                {
                    var freq = new float[c.HeadSize / 2]; Copy("rope_freqs.weight", freq.Length, 1, 1, freq, 0);
                    for (int i = 0; i < freq.Length; i++)
                        if (Math.Abs(freq[i] - Math.Pow(10000, -2.0 * i / c.HeadSize)) > 1e-6)
                            throw new InvalidDataException("未対応のRoPE frequency tensorです。");
                }
                if (tensors.Count != 0) throw new InvalidDataException("未対応の追加tensor: " + string.Join(", ", tensors.Keys));
                s.Position = 0; string hash;
                using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(s)).Replace("-", "").ToLowerInvariant();
                return new LlamaGguf(new LlamaCheckpoint(c, weights, hash), tokenizer);
            }
            private void Copy(string name, int columns, int rows, int rank, float[] destination, int offset)
            {
                if (!tensors.TryGetValue(name, out Tensor t) || t.columns != columns || t.rows != rows || t.rank != rank)
                    throw new InvalidDataException("GGUF tensor不足/shape不一致: " + name);
                s.Position = dataStart + t.offset;
                Decode(r, t.type, destination, offset, checked(columns * rows)); tensors.Remove(name);
            }
        }
        // Public for a format-level numerical oracle; no Unity dependency.
        public static void Decode(BinaryReader reader, uint type, float[] output, int offset, int count)
        {
            if (count < 0 || offset < 0 || offset > output.Length - count ||
                !(type == 0 || type == 1 || (type == 2 || type == 8) && count % 32 == 0))
                throw new InvalidDataException("GGUF decode範囲/型が不正です。");
            for (int i = 0; i < count;)
            {
                if (type == 0) output[offset + i++] = reader.ReadSingle();
                else if (type == 1) output[offset + i++] = Half(reader.ReadUInt16());
                else
                {
                    float scale = Half(reader.ReadUInt16());
                    if (type == 2)
                        for (int j = 0; j < 16; j++) { byte q = reader.ReadByte(); output[offset + i + j] = scale * ((q & 15) - 8); output[offset + i + j + 16] = scale * ((q >> 4) - 8); }
                    else for (int j = 0; j < 32; j++) output[offset + i + j] = scale * reader.ReadSByte();
                    i += 32;
                }
            }
            for (int i = offset; i < offset + count; i++)
                if (float.IsNaN(output[i]) || float.IsInfinity(output[i])) throw new InvalidDataException("GGUF重みにNaN/Infinityがあります。");
        }
        public static float Half(ushort bits)
        {
            int e = (bits >> 10) & 31, m = bits & 1023;
            if (e == 31) throw new InvalidDataException("GGUF F16にNaN/Infinityがあります。");
            float value = e == 0 ? m * (1f / 16777216f) : (float)((1024 + m) * Math.Pow(2, e - 25));
            return (bits & 32768) == 0 ? value : -value;
        }
    }
}
