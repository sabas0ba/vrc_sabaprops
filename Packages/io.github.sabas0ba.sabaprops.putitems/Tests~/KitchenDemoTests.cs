using System.IO;
using System.Linq;
using NUnit.Framework;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Components;
using SabaProps.PutItems.Editors;

namespace SabaProps.PutItems.Tests
{
    public class KitchenDemoTests
    {
        private const string TestImportRoot = "Assets/SabaProps/PutItemsKitchenDemoTest";

        [Test]
        public void BundledScene_ImportsWithWorkingTablewareAndFridgeProps()
        {
            Assert.IsTrue(File.Exists(KitchenDemoSample.SampleRoot + "/" + KitchenDemo.SceneName), "配布用 scene が必要です");
            SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                AssetDatabase.DeleteAsset(TestImportRoot);
                KitchenDemoSample.ImportSampleAt(TestImportRoot);
                string path = TestImportRoot + "/" + KitchenDemo.SceneName;
                EditorSceneManager.OpenScene(path);
                ObjectSyncPlacement[] items = Object.FindObjectsOfType<ObjectSyncPlacement>();
                Assert.AreEqual(19, items.Length);
                Assert.AreEqual(14, items.Count(item => item.category == 1));
                Assert.AreEqual(4, items.Count(item => item.category == 2));
                Assert.AreEqual(1, items.Count(item => item.category == 4));
                Assert.AreEqual(4, Object.FindObjectsOfType<Transform>().Count(t => t.name == "Dining chair"));
                int walkthrough = LayerMask.NameToLayer("Walkthrough");
                Assert.AreEqual(17, walkthrough);
                Transform tableRoot = GameObject.Find("Dining table").transform;
                Assert.IsTrue(tableRoot.GetComponentsInChildren<Collider>().All(c => c.gameObject.layer == walkthrough));
                foreach (Transform chair in Object.FindObjectsOfType<Transform>().Where(t => t.name == "Dining chair"))
                    Assert.IsTrue(chair.GetComponentsInChildren<Collider>().All(c => c.gameObject.layer == walkthrough));
                VRCSceneDescriptor world = Object.FindObjectOfType<VRCSceneDescriptor>();
                Assert.IsNotNull(world);
                Assert.AreEqual(1, world.spawns.Length);
                Assert.IsNotNull(world.spawns[0]);
                foreach (ObjectSyncPlacement item in items)
                {
                    UdonSharpEditorUtility.CopyUdonToProxy(item);
                    Assert.IsNotNull(item.contact, item.name);
                    Assert.AreEqual(item.transform, item.contact);
                    Assert.IsNotNull(item.GetComponent<VRCPickup>(), item.name);
                    Assert.IsNotNull(item.GetComponent<VRCObjectSync>(), item.name);
                    Assert.IsTrue(item.GetComponent<Collider>().enabled, item.name);
                    UdonSharpEditorUtility.CopyUdonToProxy(item.solver);
                    Assert.GreaterOrEqual(item.solver.surfaces.Length, 1);
                    foreach (PlacementSurface surface in item.solver.surfaces)
                        UdonSharpEditorUtility.CopyUdonToProxy(surface);
                    Assert.IsTrue(item.solver.TryFindPose(item.transform, item.contact, item.category), item.name + " が初期位置で吸着範囲外です");
                    Assert.IsFalse(item.solver.TryFindPose(item.transform, item.contact, 8), "異なる category への誤吸着を排除する");
                }
                ObjectSyncPlacement tray = items.Single(item => item.name == "Serving tray - lift me");
                ObjectSyncPlacement plate = items.Single(item => item.name == "Tray plate - lift me");
                ObjectSyncPlacement food = items.Single(item => item.name == "Tray food - lift me");
                ObjectSyncPlacement fork = items.Single(item => item.name == "Tray fork - lift me");
                ObjectSyncPlacement spoon = items.Single(item => item.name == "Tray spoon - lift me");
                foreach (ObjectSyncPlacement item in new[] { plate, food, fork, spoon })
                {
                    UdonSharpEditorUtility.CopyUdonToProxy(item.followState);
                    Assert.IsTrue(item.followState.TryFindPose(), item.name);
                    Assert.Less(Vector3.Distance(item.transform.position, item.followState.resultPosition), 0.0001f);
                    Assert.IsTrue(item.IsCarriedBy(tray), item.name);
                }
                Assert.IsTrue(food.IsCarriedBy(plate));
                Vector3 trayPosition = tray.transform.position;
                Quaternion trayRotation = tray.transform.rotation;
                tray.transform.SetPositionAndRotation(trayPosition + new Vector3(0.6f, 0.4f, -0.2f),
                    Quaternion.Euler(0, 45, 0));
                Assert.IsTrue(plate.followState.TryFindPose());
                Assert.IsTrue(food.followState.TryFindPose());
                Vector3 expectedFood = tray.transform.TransformPoint(
                    plate.followState.localPosition + plate.followState.localRotation * food.followState.localPosition);
                Assert.Less(Vector3.Distance(expectedFood, food.followState.resultPosition), 0.0001f);
                Assert.Greater(Vector3.Distance(food.transform.position, food.followState.resultPosition), 0.3f);
                tray.transform.SetPositionAndRotation(trayPosition, trayRotation);
                plate.followState.surfaceIndex = -1;
                Assert.IsFalse(food.IsCarriedBy(tray), "皿をおぼんから外すと料理もおぼんの追従対象から外れる");
                Vector3 platePosition = plate.transform.position;
                plate.transform.position += new Vector3(0.3f, 0.25f, 0.1f);
                Assert.IsTrue(food.followState.TryFindPose());
                Assert.Less(Vector3.Distance(plate.transform.TransformPoint(food.followState.localPosition),
                    food.followState.resultPosition), 0.0001f);
                plate.transform.position = platePosition;
                foreach (MeshFilter mesh in Object.FindObjectsOfType<MeshFilter>())
                    Assert.IsNotNull(mesh.sharedMesh, mesh.name);
                foreach (Renderer renderer in Object.FindObjectsOfType<Renderer>())
                {
                    Assert.IsNotNull(renderer.sharedMaterial, renderer.name);
                    Assert.IsNotNull(renderer.sharedMaterial.shader, renderer.name);
                }
                string[] dependencies = AssetDatabase.GetDependencies(path, true);
                Assert.IsFalse(dependencies.Any(value => value.StartsWith(KitchenDemo.GeneratedRoot + "/")), "開発用 Assets への参照を残さない");
                Assert.IsFalse(dependencies.Any(value => value.Contains("Samples~/")), "非importの sample asset へ依存しない");
                string original = File.ReadAllText(path);
                KitchenDemoSample.ImportSampleAt(TestImportRoot);
                Assert.AreEqual(original, File.ReadAllText(path), "導入済み scene を再importで上書きしない");
            }
            finally
            {
                if (previous != null && previous.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
                AssetDatabase.DeleteAsset(TestImportRoot);
            }
        }
    }
}
