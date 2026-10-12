using System;
using UnityEngine;

namespace SabaProps.Llama
{
    public sealed class LlamaModelAsset : ScriptableObject
    {
        public LlamaConfig config;
        public LlamaTokenizer tokenizer;
        public Texture2D weights;
        public Material[] materials;
        public int[] inputA, inputB, targets, widths, heights;
        public int logitsTarget, resultTarget, context;
        public string checkpointSha256, tokenizerSha256, sourceAndLicense;
        public string sourceFormat = "llama2.c-fp32-v0";

        public long WorkingBytes
        {
            get
            {
                long bytes = 0;
                for (int i = 0; i < widths.Length; i++) bytes += (long)widths[i] * heights[i] * 16;
                return bytes;
            }
        }
    }
}
