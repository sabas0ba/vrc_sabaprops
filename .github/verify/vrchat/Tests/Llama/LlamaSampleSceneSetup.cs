using System;
using UdonSharp.Compiler;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Llama.WorldTests
{
    /// <summary>手動のBuild &amp; Test用に、モデルの変換からサンプルシーンの生成までを行います。</summary>
    public static class LlamaSampleSceneSetup
    {
        /// <summary>
        /// -executeMethod の入口です。-batchmode と -quit は指定せず、Editor を開いたままにします。
        /// <see cref="LlamaWorldRunTests.GgufVariable"/> が GGUF を指していればそのモデル（Context 64）、
        /// なければ未学習の検証用モデルを使います。
        /// </summary>
        public static void Open()
        {
            try
            {
                string path = Environment.GetEnvironmentVariable(LlamaWorldRunTests.GgufVariable);
                LlamaModelAsset model;
                if (string.IsNullOrEmpty(path))
                {
                    model = LlamaProgramBuilder.Build(LlamaCheckpoint.Fixture(), LlamaTokenizer.Fixture(), 8);
                    model.sourceAndLicense = "SabaProps numerical fixture / untrained";
                    model.tokenizerSha256 = "generated";
                }
                else
                {
                    LlamaGguf gguf = LlamaGguf.Read(path);
                    model = LlamaProgramBuilder.Build(gguf.checkpoint, gguf.tokenizer, 64);
                    model.sourceFormat = "gguf-llama";
                    model.tokenizerSha256 = gguf.checkpoint.sha256;
                    model.sourceAndLicense = "manual trial: " + System.IO.Path.GetFileName(path);
                }
                LlamaImportWindow.Save(model, "SampleModel");
                UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
                LlamaSampleScene.Create(model);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
