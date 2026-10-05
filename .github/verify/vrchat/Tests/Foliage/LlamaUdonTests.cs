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
            MethodInfo compile = asset.GetType().GetMethod("CompileAllCsPrograms", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(compile);
            compile.Invoke(null, new object[] { true, false });
            MethodInfo program = asset.GetType().GetMethod("GetRealProgram", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(program);
            Assert.IsNotNull(program.Invoke(asset, null), "Client Udon program was not produced.");
        }
    }
}
