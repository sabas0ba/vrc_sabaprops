using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SabaProps.Llama;

internal static class GgufTests
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Reject(Action action)
    {
        try { action(); } catch (InvalidDataException) { return; }
        throw new Exception("Malformed GGUF accepted");
    }
    private static void Text(BinaryWriter w, string text) { byte[] b = Encoding.UTF8.GetBytes(text); w.Write((ulong)b.Length); w.Write(b); }
    private static void Metadata(BinaryWriter w, string key, object value)
    {
        Text(w, key);
        if (value is string text) { w.Write(8u); Text(w, text); }
        else if (value is uint number) { w.Write(4u); w.Write(number); }
        else if (value is float f) { w.Write(6u); w.Write(f); }
        else if (value is string[] pieces) { w.Write(9u); w.Write(8u); w.Write((ulong)pieces.Length); foreach (string p in pieces) Text(w, p); }
        else if (value is float[] scores) { w.Write(9u); w.Write(6u); w.Write((ulong)scores.Length); foreach (float s in scores) w.Write(s); }
        else if (value is int[] types) { w.Write(9u); w.Write(5u); w.Write((ulong)types.Length); foreach (int t in types) w.Write(t); }
        else throw new Exception("fixture metadata type");
    }
    private sealed class Entry { public string name; public int offset, columns, rows, rank; }
    private static MemoryStream Fixture(LlamaCheckpoint model, string architecture = "llama", bool overlap = false, bool missing = false, uint version = 3, bool emptyPiece = false)
    {
        var c = model.config; var l = model.layout; var list = new List<Entry>();
        Action<string, int, int, int, int> add = (n, o, x, y, rank) => list.Add(new Entry { name = n, offset = o, columns = x, rows = y, rank = rank });
        add("token_embd.weight", l.embedding, c.dimension, c.vocabulary, 2);
        if (!missing) add("output_norm.weight", l.finalNorm, c.dimension, 1, 1);
        if (!c.sharedClassifier) add("output.weight", l.classifier, c.dimension, c.vocabulary, 2);
        for (int i = 0; i < c.layers; i++)
        {
            string p = "blk." + i + "."; int d = c.dimension, k = c.KvDimension, h = c.hiddenDimension;
            add(p + "attn_norm.weight", l.attentionNorm + i * d, d, 1, 1);
            add(p + "attn_q.weight", l.query + i * d * d, d, d, 2);
            add(p + "attn_k.weight", l.key + i * k * d, d, k, 2);
            add(p + "attn_v.weight", l.value + i * k * d, d, k, 2);
            add(p + "attn_output.weight", l.output + i * d * d, d, d, 2);
            add(p + "ffn_norm.weight", l.ffnNorm + i * d, d, 1, 1);
            add(p + "ffn_gate.weight", l.gate + i * h * d, d, h, 2);
            add(p + "ffn_down.weight", l.down + i * h * d, h, d, 2);
            add(p + "ffn_up.weight", l.up + i * h * d, d, h, 2);
        }
        var s = new MemoryStream(); var w = new BinaryWriter(s, Encoding.UTF8, true);
        w.Write(0x46554747u); w.Write(version); w.Write((ulong)list.Count); w.Write(12ul);
        Metadata(w, "general.architecture", architecture); Metadata(w, "tokenizer.ggml.model", "llama");
        var tok = LlamaTokenizer.Fixture(); var pieces = (string[])tok.pieces.Clone();
        for (int i = 0; i < pieces.Length; i++) pieces[i] = pieces[i].Replace(' ', '\u2581');
        if (emptyPiece) pieces[5] = "";
        Metadata(w, "tokenizer.ggml.tokens", pieces); Metadata(w, "tokenizer.ggml.scores", tok.scores);
        Metadata(w, "tokenizer.ggml.token_type", new[] { 2, 3, 3, 1, 1, 1, 1, 1 });
        Metadata(w, "llama.embedding_length", (uint)c.dimension); Metadata(w, "llama.feed_forward_length", (uint)c.hiddenDimension);
        Metadata(w, "llama.block_count", (uint)c.layers); Metadata(w, "llama.attention.head_count", (uint)c.heads);
        Metadata(w, "llama.attention.head_count_kv", (uint)c.kvHeads); Metadata(w, "llama.context_length", (uint)c.sequenceLength);
        Metadata(w, "llama.attention.layer_norm_rms_epsilon", 1e-5f);
        ulong offset = 0;
        foreach (var e in list)
        {
            Text(w, e.name); w.Write((uint)e.rank); w.Write((ulong)e.columns); if (e.rank == 2) w.Write((ulong)e.rows);
            w.Write(0u); w.Write(overlap ? 0ul : offset); offset += (ulong)e.columns * (ulong)e.rows * 4;
            offset = (offset + 31) / 32 * 32;
        }
        while (s.Position % 32 != 0) w.Write((byte)0);
        foreach (var e in list)
        {
            for (int i = 0; i < e.columns * e.rows; i++) w.Write(model.weights[e.offset + i]);
            while (s.Position % 32 != 0) w.Write((byte)0);
        }
        s.Position = 0; return s;
    }
    public static void Run()
    {
        foreach (bool shared in new[] { true, false }) foreach (int kv in new[] { 1, 2 }) foreach (uint v in new[] { 2u, 3u })
        {
            var original = LlamaCheckpoint.Fixture(shared, kv);
            using (var s = Fixture(original, version: v))
            {
                var loaded = LlamaGguf.Read(s);
                Check(loaded.checkpoint.config.sharedClassifier == shared, "GGUF tied classifier");
                Check(loaded.tokenizer.Encode("ab")[1] == 7, "GGUF SentencePiece space conversion");
                var a = new LlamaReference(original); var b = new LlamaReference(loaded.checkpoint);
                foreach (int token in new[] { 1, 4, 5, 7 })
                {
                    var x = a.Forward(token); var y = b.Forward(token);
                    for (int i = 0; i < x.Length; i++) Check(x[i] == y[i], "GGUF tensor mapping and GQA logits");
                }
                s.SetLength(s.Length - 33); Reject(() => LlamaGguf.Read(s));
            }
        }
        var model = LlamaCheckpoint.Fixture();
        using (var s = Fixture(model, emptyPiece: true))
        {
            var loaded = LlamaGguf.Read(s);
            Check(loaded.tokenizer.pieces.Length == 8 && loaded.tokenizer.Find("") == 5, "legacy empty piece must preserve vocabulary IDs");
            Check(loaded.tokenizer.Encode("a").Length == 3, "empty piece must not inject prompt tokens");
        }
        using (var s = Fixture(model, architecture: "qwen2")) Reject(() => LlamaGguf.Read(s));
        using (var s = Fixture(model, overlap: true)) Reject(() => LlamaGguf.Read(s));
        using (var s = Fixture(model, missing: true)) Reject(() => LlamaGguf.Read(s));
        using (var s = Fixture(model, version: 0x03000000)) Reject(() => LlamaGguf.Read(s));
        Check(LlamaGguf.Half(0x3c00) == 1 && LlamaGguf.Half(0xc000) == -2 && LlamaGguf.Half(1) == 1f / 16777216 && LlamaGguf.Half(0x7bff) == 65504, "F16 normals and subnormal");
        Reject(() => LlamaGguf.Half(0x7c00)); Reject(() => LlamaGguf.Half(0x7e00));
        foreach (uint type in new[] { 1u, 2u, 8u }) using (var s = new MemoryStream())
        {
            var w = new BinaryWriter(s, Encoding.UTF8, true);
            if (type == 1) for (int i = 0; i < 32; i++) w.Write((ushort)0xbc00);
            else
            {
                w.Write((ushort)0x3800); // scale = 0.5, two independent nibble halves
                if (type == 2) for (int i = 0; i < 16; i++) w.Write((byte)(((15 - i) << 4) | i));
                else for (int i = 0; i < 32; i++) w.Write((sbyte)(i - 16));
            }
            s.Position = 0; var output = new float[32]; LlamaGguf.Decode(new BinaryReader(s), type, output, 0, 32);
            for (int i = 0; i < 32; i++) Check(output[i] == (type == 1 ? -1 : type == 8 ? (i - 16) * .5f : ((i < 16 ? i : 31 - i) - 8) * .5f), "GGUF dequantization oracle");
        }
        Console.WriteLine("GGUF: v2/v3, tensor mapping, GQA, tied output, F16/Q4_0/Q8_0 and malformed fixtures passed.");
    }
    public static void RealModel(string path)
    {
        var model = LlamaGguf.Read(path); var cpu = new LlamaReference(model.checkpoint);
        var tokens = model.tokenizer.Encode("Once upon a time"); float[] logits = null;
        foreach (int token in tokens) logits = cpu.Forward(token);
        var text = new StringBuilder(); var ids = new List<int>();
        for (int step = 0; step < 8; step++)
        {
            int best = 0;
            for (int i = 0; i < logits.Length; i++)
            {
                Check(!float.IsNaN(logits[i]) && !float.IsInfinity(logits[i]), "real model finite logits");
                if (logits[i] > logits[best]) best = i;
            }
            ids.Add(best); if (best == 2) break;
            text.Append(model.tokenizer.pieces[best]); logits = cpu.Forward(best);
        }
        Console.WriteLine("GGUF model SHA256=" + model.checkpoint.sha256 + " floats=" + model.checkpoint.weights.Length);
        Console.WriteLine("Prompt tokens: " + string.Join(",", tokens));
        Console.WriteLine("Greedy tokens: " + string.Join(",", ids)); Console.WriteLine("Completion: " + text);
        Console.WriteLine("CPU smoke test only; not llama.cpp parity or GPU/VRChat validation.");
    }
}
