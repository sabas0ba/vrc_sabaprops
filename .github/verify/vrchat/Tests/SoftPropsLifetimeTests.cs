using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.SoftProps.WorldTests
{
    public class SoftPropsLifetimeTests
    {
        private static Type FindType(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(name)).First(t => t != null);

        [Test]
        public void DemoProgramMigration_PreservesGuids_AndFurnitureSurvivesDemoRemoval()
        {
            Type demo = FindType("SabaProps.SoftProps.Editors.SoftPropsDemo");
            Type generator = FindType("SabaProps.SoftProps.Editors.SoftPropGenerator");
            string importRoot = (string)demo.GetField("ImportRoot").GetRawConstantValue();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            demo.GetMethod("ImportSample").Invoke(null, null);
            string shared = (string)generator.GetProperty("ProgramAssetPath").GetValue(null);
            string guid = AssetDatabase.AssetPathToGUID(shared);
            var program = AssetDatabase.LoadAssetAtPath<ScriptableObject>(shared);
            var compiled = new SerializedObject(program).FindProperty("serializedUdonProgramAsset").objectReferenceValue;
            string compiledGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(compiled));

            // 旧版でdemo内にprogramが作られた状態を再現する。
            Assert.IsEmpty(AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(compiled), importRoot + "/LegacyCompiled.asset"));
            Assert.IsEmpty(AssetDatabase.MoveAsset(shared, importRoot + "/LegacyController.asset"));
            generator.GetMethod("GenerateAll").Invoke(null, null);
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(shared));
            Assert.IsFalse(AssetDatabase.GUIDToAssetPath(compiledGuid).StartsWith(importRoot + "/", StringComparison.Ordinal));

            string prefabPath = "Assets/SabaProps/SoftPropsGenerated/Prefabs/Futon.prefab";
            foreach (string dependency in AssetDatabase.GetDependencies(prefabPath, true))
                Assert.IsFalse(dependency.StartsWith(importRoot + "/", StringComparison.Ordinal), dependency);
            Assert.IsTrue(AssetDatabase.DeleteAsset(importRoot));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            foreach (var behaviour in prefab.GetComponentsInChildren<Component>()
                .Where(c => c.GetType().Name == "UdonBehaviour"))
            {
                var serialized = new SerializedObject(behaviour);
                Assert.IsNotNull(serialized.FindProperty("programSource").objectReferenceValue);
                Assert.IsNotNull(serialized.FindProperty("serializedProgramAsset").objectReferenceValue);
            }
            demo.GetMethod("ImportSample").Invoke(null, null);
            Assert.AreEqual(1, AssetDatabase.FindAssets("SoftSurfaceContactController t:UdonSharpProgramAsset", new[] { "Assets" }).Length);
        }

        [Test]
        public void AssignSender_ClearsPreviousPlayerOwnership()
        {
            Type controllerType = FindType("SabaProps.SoftProps.SoftSurfaceContactController");
            var root = new GameObject("Slot ownership test");
            try
            {
                var controller = root.AddComponent(controllerType);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var players = (VRCPlayerApi[])controllerType.GetField("_players", flags).GetValue(controller);
                var seen = (float[])controllerType.GetField("_playerSeen", flags).GetValue(controller);
                // SDK APIを呼ばず、所有者識別用の参照だけを作る。
                players[3] = (VRCPlayerApi)FormatterServices.GetUninitializedObject(typeof(VRCPlayerApi));
                seen[3] = 10f;
                var senders = (Array)controllerType.GetField("_senders", flags).GetValue(controller);
                object sender = FormatterServices.GetUninitializedObject(senders.GetType().GetElementType());
                controllerType.GetMethod("AssignSender", flags).Invoke(controller, new object[] { 3, sender });
                Assert.AreSame(sender, senders.GetValue(3));
                Assert.IsNull(players[3]);
                Assert.AreEqual(0f, seen[3]);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
