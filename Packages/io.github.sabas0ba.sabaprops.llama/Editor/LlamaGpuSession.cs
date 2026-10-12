using System;
using UnityEngine;

namespace SabaProps.Llama
{
    // Editor preview and numerical tests. Worlds use the UdonSharp sample instead.
    public sealed class LlamaGpuSession : IDisposable
    {
        private readonly LlamaModelAsset model;
        private readonly RenderTexture[] buffers;
        private readonly Material[] materials;
        public int Position { get; private set; }
        public RenderTexture Logits => buffers[model.logitsTarget];
        public RenderTexture Result => buffers[model.resultTarget];

        public LlamaGpuSession(LlamaModelAsset model)
        {
            this.model = model;
            if (!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat) ||
                !SystemInfo.SupportsTextureFormat(TextureFormat.RFloat) || !model.materials[0].shader.isSupported)
                throw new NotSupportedException("FP32 textureまたは推論shaderを利用できません。");
            if (model.weights.width > SystemInfo.maxTextureSize || model.weights.height > SystemInfo.maxTextureSize)
                throw new NotSupportedException("重みtextureがGPUの最大サイズを超えています。");
            buffers = new RenderTexture[model.widths.Length];
            materials = new Material[model.materials.Length];
            try
            {
                for (int i = 0; i < buffers.Length; i++)
                {
                    buffers[i] = new RenderTexture(model.widths[i], model.heights[i], 0,
                        RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear)
                    { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, useMipMap = false, antiAliasing = 1 };
                    if (!buffers[i].Create()) throw new InvalidOperationException("RenderTextureの確保に失敗しました。");
                }
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = new Material(model.materials[i]);
                    if (model.inputA[i] >= 0) materials[i].SetTexture("_A", buffers[model.inputA[i]]);
                    if (model.inputB[i] >= 0) materials[i].SetTexture("_B", buffers[model.inputB[i]]);
                }
            }
            catch { Dispose(); throw; }
        }

        public void Reset() { Position = 0; }

        public void Forward(int token)
        {
            if (token < 0 || token >= model.config.vocabulary || Position >= model.context)
                throw new ArgumentOutOfRangeException(nameof(token));
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i].SetInt("_Position", Position);
                materials[i].SetInt("_Token", token);
                Graphics.Blit(Texture2D.blackTexture, buffers[model.targets[i]], materials[i]);
            }
            Position++;
        }

        public void Dispose()
        {
            if (materials != null) foreach (var material in materials)
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
            if (buffers != null) foreach (var buffer in buffers)
                if (buffer != null) { buffer.Release(); UnityEngine.Object.DestroyImmediate(buffer); }
        }
    }
}
