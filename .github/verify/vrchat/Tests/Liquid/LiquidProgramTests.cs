using System.Collections.Generic;
using NUnit.Framework;
using UdonSharp;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace SabaProps.Liquid.WorldTests
{
    /// <summary>
    /// Whether UdonSharp accepts the liquid behaviours.
    /// <para>
    /// The offline tier compiles them as plain C# against a stub
    /// UdonSharpBehaviour, which says nothing about the rules UdonSharp adds:
    /// no generics, no out parameters, only exposed externs. This runs the real
    /// compiler and inspects the exported entry points.
    /// </para>
    /// </summary>
    public class LiquidProgramTests
    {
        private GameObject _root;

        [OneTimeSetUp]
        public void CompileUdonSharpPrograms()
        {
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
        }

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("LiquidProgramUnderTest");
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void BodyCanvas_ExportsItsEventsAndApi()
        {
            List<string> exported = Exported(Compile<LiquidBodyCanvas>());

            // The frame is read after IK. If PostLateUpdate stops being an Udon
            // event, the canvas never follows the body.
            CollectionAssert.Contains(exported, "_postLateUpdate");

            foreach (string method in new[] { "Assign", "Release", "QueueStamp", "ApplyImmersion", "WashImmersion", "GetPlayerId", "GetLastActivityTime" })
            {
                CollectionAssert.Contains(exported, method, method + " is not callable from other behaviours");
            }
        }

        [Test]
        public void CanvasPool_ExportsItsEventsAndApi()
        {
            List<string> exported = Exported(Compile<LiquidCanvasPool>());

            CollectionAssert.Contains(exported, "_onPlayerLeft");
            CollectionAssert.Contains(exported, "AcquireCanvas");
            CollectionAssert.Contains(exported, "FindCanvas");
        }

        [Test]
        public void Profile_Compiles()
        {
            Assert.IsNotNull(Compile<LiquidProfile>());
        }

        private IUdonProgram Compile<T>() where T : UdonSharpBehaviour
        {
            T behaviour = _root.AddUdonSharpComponent<T>();
            Assert.IsNotNull(behaviour, "AddUdonSharpComponent returned nothing for " + typeof(T).Name);

            UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour);
            Assert.IsNotNull(backing, "the proxy has no backing UdonBehaviour");

            UdonSharpProgramAsset asset = UdonSharpEditorUtility.GetUdonSharpProgramAsset(backing);
            Assert.IsNotNull(asset, "no UdonSharpProgramAsset for " + typeof(T).Name);
            Assert.IsNotNull(asset.SerializedProgramAsset,
                "UdonSharp produced no program for " + typeof(T).Name + ". The Unity console holds the diagnostics.");

            return asset.SerializedProgramAsset.RetrieveProgram();
        }

        private static List<string> Exported(IUdonProgram program)
        {
            Assert.IsNotNull(program);
            return new List<string>(program.EntryPoints.GetExportedSymbols());
        }
    }
}
