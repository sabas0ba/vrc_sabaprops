using System;
using System.IO;
using SabaProps.Llama;

internal static class CoreTests
{
    private static int assertions;
    private static void Check(bool value, string message)
    {
        assertions++;
        if (!value) throw new Exception(message);
    }
    private static void Reject(Action action, string message)
    {
        bool rejected = false;
        try { action(); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, message);
    }

    public static void Main()
    {
        foreach (bool shared in new[] { true, false })
        foreach (int kvHeads in new[] { 1, 2 })
        {
            var original = LlamaCheckpoint.Fixture(shared, kvHeads);
            using (var stream = new MemoryStream())
            {
                original.Write(stream);
                var loaded = LlamaCheckpoint.Read(stream);
                Check(loaded.config.sharedClassifier == shared, "shared classifier header");
                Check(loaded.config.kvHeads == kvHeads, "GQA header");
                Check(loaded.sha256.Length == 64, "SHA-256 provenance");
                for (int i = 0; i < original.weights.Length; i++) Check(original.weights[i] == loaded.weights[i], "FP32 roundtrip");
                var reference = new LlamaReference(loaded);
                float[] first = reference.Forward(1);
                float[] second = reference.Forward(4);
                var fresh = new LlamaReference(loaded);
                float[] noHistory = fresh.Forward(4);
                bool different = false;
                for (int i = 0; i < second.Length; i++)
                {
                    Check(!float.IsNaN(second[i]) && !float.IsInfinity(second[i]), "finite logits");
                    different |= Math.Abs(second[i] - noHistory[i]) > 1e-5;
                }
                Check(different, "attention must use history");
                reference.Reset();
                float[] reset = reference.Forward(1);
                for (int i = 0; i < first.Length; i++) Check(first[i] == reset[i], "cache reset isolation");
                stream.SetLength(stream.Length - 1);
                Reject(() => LlamaCheckpoint.Read(stream), "truncated input accepted");
            }
        }
        var config = new LlamaConfig { dimension = 2, hiddenDimension = 2, layers = 1, heads = 1,
            kvHeads = 1, vocabulary = 3, sequenceLength = 2, sharedClassifier = false };
        var layout = new LlamaLayout(config);
        var weights = new float[layout.count];
        weights[0] = 1;
        for (int i = 0; i < 2; i++) weights[layout.finalNorm + i] = 1;
        for (int i = 0; i < 6; i++) weights[layout.classifier + i] = i + 1;
        var model = new LlamaCheckpoint(config, weights);
        float[] analytic = new LlamaReference(model).Forward(0);
        for (int i = 0; i < 3; i++)
            Check(Math.Abs(analytic[i] - (2 * i + 1) / Math.Sqrt(0.5 + 1e-5)) < 2e-6, "analytic RMS and classifier");
        weights[0] = float.NaN;
        Reject(() => new LlamaCheckpoint(config, weights), "NaN accepted");
        weights[0] = float.PositiveInfinity;
        Reject(() => new LlamaCheckpoint(config, weights), "Infinity accepted");
        config.heads = 2;
        Reject(() => new LlamaLayout(config), "odd head size accepted");
        config.dimension = int.MaxValue;
        Reject(() => new LlamaLayout(config), "oversized dimension accepted");
        using (var stream = new MemoryStream(new byte[28])) Reject(() => LlamaCheckpoint.Read(stream), "zero dimensions accepted");

        var tokenizer = LlamaTokenizer.Fixture();
        int[] encoded = tokenizer.Encode("ab");
        Check(encoded.Length == 2 && encoded[0] == 1 && encoded[1] == 7, "score-based BPE / BOS / dummy prefix");
        Check(tokenizer.Encode("").Length == 1, "empty prompt");
        Reject(() => tokenizer.Encode("あ"), "missing byte fallback accepted");
        tokenizer.pieces[7] = "ab";
        Reject(() => tokenizer.Prepare(), "duplicate token accepted");
        Console.WriteLine("Llama core: " + assertions + " assertions passed.");
    }
}
