using NUnit.Framework;
using UdonSharp;
using UdonSharp.Compiler;
using UnityEditor;

namespace SabaProps.Llama.WorldTests
{
    public sealed class LlamaUdonTests
    {
        [Test]
        public void RunnerCompilesToClientUdonProgram()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(
                System.IO.Path.ChangeExtension(LlamaWorldBuilder.RuntimePath, ".asset"));
            Assert.IsNotNull(asset, "Run the world project's setup session before running tests.");

            // UdonSharpProgramAsset.CompileAllCsPrograms only starts a compile,
            // so the program has to be read after CompileSync returns.
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = false });
            try
            {
                Assert.IsNotNull(asset.SerializedProgramAsset, "The program asset holds no serialized program.");
                Assert.IsNotNull(asset.SerializedProgramAsset.RetrieveProgram(), "Client Udon program was not produced.");
            }
            finally
            {
                // ClientSim runs editor builds; leave them in place for the
                // fixtures that follow.
                UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
            }
        }
    }
}
