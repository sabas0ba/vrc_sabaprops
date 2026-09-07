using NUnit.Framework;
using SabaProps.StageCam.Editors;
using UdonSharpEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Components;

namespace SabaProps.StageCam.WorldTests
{
    public class StageCamSampleSceneTests
    {
        [Test]
        public void SavedDemo_PreservesRigBindingsAndFacesAudience()
        {
            StageCamSampleScene.Create();
            EditorSceneManager.OpenScene(StageCamSampleScene.ScenePath);

            var rigs = Object.FindObjectsOfType<StageCamRig>();
            Assert.That(rigs.Length, Is.EqualTo(2));
            foreach (var rig in rigs)
            {
                Assert.That(rig.rigRoot, Is.EqualTo(rig.transform));
                Assert.That(rig.targetLocalPlayerOnStart, Is.True);
                Assert.That(rig.framingCamera, Is.Not.Null);
                Assert.That(rig.framingCamera.targetTexture, Is.Not.Null);
                Assert.That(rig.framingCamera.cullingMask & (1 << StageCamAssets.ScreenLayer), Is.Zero);
                var udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(rig);
                Assert.That(udon, Is.Not.Null);
                Assert.That(udon.programSource, Is.Not.Null);
            }

            var crane = GameObject.Find(StageCamSampleScene.CraneRigName).GetComponent<StageCamRig>();
            Assert.That(crane.handle, Is.TypeOf<VRCPickup>());
            Assert.That(crane.cameraWork, Is.True);
            Assert.That(crane.allowPickupAdjust, Is.True);

            foreach (string name in new[] { StageCamSampleScene.FaceScreenName, StageCamSampleScene.CraneScreenName })
            {
                var screen = GameObject.Find(name);
                var normal = screen.transform.TransformDirection(screen.GetComponent<MeshFilter>().sharedMesh.normals[0]);
                var audience = StageCamSampleScene.SpawnPosition - screen.transform.position;
                Assert.That(Vector3.Dot(normal, audience), Is.GreaterThan(0f), name + " must face the audience");
                Assert.That(screen.GetComponent<MeshRenderer>().sharedMaterial.mainTexture, Is.Not.Null);
            }

            var world = Object.FindObjectOfType<VRCSceneDescriptor>();
            Assert.That(world, Is.Not.Null);
            Assert.That(world.spawns.Length, Is.EqualTo(1));
            Assert.That(world.spawns[0].position, Is.EqualTo(StageCamSampleScene.SpawnPosition));
        }
    }
}
