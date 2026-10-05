using System;
using NUnit.Framework;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using SabaProps.PutItems.Editors;

namespace SabaProps.PutItems.Tests
{
    public class PlacementIntegrationTests
    {
        [Test]
        public void RuntimePrograms_CompileToUdon()
        {
            PlacementPrograms.Prepare();
            UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
            Assert.IsFalse(UdonSharpProgramAsset.AnyUdonSharpScriptHasError());
            Assert.IsNotNull(UdonSharpProgramAsset.GetProgramAssetForClass(typeof(PlacementSurface)));
            Assert.IsNotNull(UdonSharpProgramAsset.GetProgramAssetForClass(typeof(PlacementSolver)));
            Assert.IsNotNull(UdonSharpProgramAsset.GetProgramAssetForClass(typeof(PlacementFollowState)));
            Assert.IsNotNull(UdonSharpProgramAsset.GetProgramAssetForClass(typeof(ObjectSyncPlacement)));
        }

        [Test]
        public void Demo_CreatesTwoConfiguredAdapters_WithoutLocalOwnerCannotMove()
        {
            // SDK の Editor plugin は asmdef へ自動参照されないため、公式設定処理を参照で呼びます。
            Type layers = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                layers = assembly.GetType("UpdateLayers");
                if (layers != null) break;
            }
            Assert.IsNotNull(layers, "SDK layer setup API not found");
            layers.GetMethod("SetupEditorLayers").Invoke(null, null);
            layers.GetMethod("SetupCollisionLayerMatrix").Invoke(null, null);
            GameObject demo = null;
            try
            {
                PlacementMenu.CreateDemo();
                demo = Selection.activeGameObject;
                Assert.IsNotNull(demo);
                ObjectSyncPlacement[] adapters = demo.GetComponentsInChildren<ObjectSyncPlacement>();
                Assert.AreEqual(2, adapters.Length);
                foreach (ObjectSyncPlacement adapter in adapters)
                {
                    Assert.IsNotNull(adapter.solver);
                    Assert.IsNotNull(adapter.contact);
                    Assert.AreEqual(1, adapter.solver.surfaces.Length);
                    PlacementSurface surface = adapter.solver.surfaces[0];
                    surface.acceptedCategories = 0;
                    UdonSharpEditorUtility.CopyUdonToProxy(surface);
                    Assert.AreNotEqual(0, adapter.category & surface.acceptedCategories, "Udon 側にも面のカテゴリを保存する");
                    Vector3 before = adapter.transform.position;
                    Assert.IsFalse(adapter.TryPlace(), "Editor にはローカル owner がいない");
                    Assert.AreEqual(before, adapter.transform.position);
                    adapter.CancelAndRestorePhysics();
                    Assert.AreEqual(before, adapter.transform.position);
                }
            }
            finally
            {
                if (demo != null) UnityEngine.Object.DestroyImmediate(demo);
            }
        }
    }
}
