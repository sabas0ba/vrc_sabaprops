using System;
using System.Reflection;
using NUnit.Framework;
using SabaProps.Llama;
using UnityEditor;

namespace SabaProps.Foliage.WorldTests
{
    public sealed class LlamaUdonTests
    {
        [Test]
        public void RunnerCompilesToClientUdonProgram()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.ScriptableObject>(
                System.IO.Path.ChangeExtension(LlamaWorldBuilder.RuntimePath, ".asset"));
            Assert.IsNotNull(asset, "Run the world project's setup session before running tests.");

            // UdonSharpProgramAsset.CompileAllCsPrograms only starts a compile,
            // so the program has to be read after CompileSync returns.
            Compile(asset.GetType().Assembly, false);
            try
            {
                PropertyInfo serialized = asset.GetType().GetProperty("SerializedProgramAsset", BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotNull(serialized);
                object programAsset = serialized.GetValue(asset);
                Assert.IsNotNull(programAsset, "The program asset holds no serialized program.");
                MethodInfo retrieve = programAsset.GetType().GetMethod("RetrieveProgram", BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotNull(retrieve);
                Assert.IsNotNull(retrieve.Invoke(programAsset, null), "Client Udon program was not produced.");
            }
            finally
            {
                // ClientSim runs editor builds; leave them in place for the
                // fixtures that follow.
                Compile(asset.GetType().Assembly, true);
            }
        }

        private static void Compile(Assembly udonSharpEditor, bool editorBuild)
        {
            Type optionsType = udonSharpEditor.GetType("UdonSharp.Compiler.UdonSharpCompileOptions");
            Type compilerType = udonSharpEditor.GetType("UdonSharp.Compiler.UdonSharpCompilerV1");
            Assert.IsNotNull(optionsType);
            Assert.IsNotNull(compilerType);
            object options = Activator.CreateInstance(optionsType);
            PropertyInfo isEditorBuild = optionsType.GetProperty("IsEditorBuild", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(isEditorBuild);
            isEditorBuild.SetValue(options, editorBuild);
            MethodInfo compileSync = compilerType.GetMethod("CompileSync", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(compileSync);
            compileSync.Invoke(null, new[] { options });
        }
    }
}
