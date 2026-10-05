using NUnit.Framework;
using SabaProps.Capture.Editors;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.Udon;

namespace SabaProps.Capture.WorldTests
{
    /// <summary>
    /// Generates the sample scene, reopens it from disk, and checks that what a user gets
    /// is wired the way the README describes. Reopening matters: references that only
    /// exist on the in-memory objects do not survive the save.
    /// </summary>
    public class CaptureSampleSceneTests
    {
        [Test]
        public void SavedSample_KeepsBothRecordersWiredToTheirInputsAndPanels()
        {
            CaptureSampleScene.Create();
            EditorSceneManager.OpenScene(CaptureSampleScene.ScenePath);

            CaptureRecorder textureRecorder = Recorder(CaptureSampleScene.TextureRecorderName);
            CaptureRecorder cameraRecorder = Recorder(CaptureSampleScene.CameraRecorderName);

            Camera live = GameObject.Find(CaptureSampleScene.LiveCameraName).GetComponent<Camera>();
            // GameObject.Find skips nothing here: the object is active, only the component is disabled.
            Camera timelapse = GameObject.Find(CaptureSampleScene.TimelapseCameraName).GetComponent<Camera>();

            Assert.That(live.enabled, Is.True);
            Assert.That(live.targetTexture, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(live.targetTexture), Is.EqualTo(CaptureSampleScene.LiveTexturePath));
            Assert.That(timelapse.enabled, Is.False, "the timelapse camera must only render when the recorder asks");
            Assert.That(timelapse.targetTexture, Is.Null);

            foreach (Camera camera in new[] { live, timelapse })
            {
                Assert.That(camera.cullingMask & (1 << CapturePanelBuilder.PanelLayer), Is.Zero,
                    $"{camera.name} would film the panels showing its own frames");
            }

            Assert.That(textureRecorder.sourceTexture, Is.EqualTo(live.targetTexture));
            Assert.That(textureRecorder.sourceCamera, Is.Null);
            Assert.That(textureRecorder.fullPolicy, Is.EqualTo(CaptureRecorder.PolicyThin));
            Assert.That(cameraRecorder.sourceCamera, Is.EqualTo(timelapse));
            Assert.That(cameraRecorder.sourceTexture, Is.Null);
            Assert.That(cameraRecorder.fullPolicy, Is.EqualTo(CaptureRecorder.PolicyRing));

            // The frame must match the source's aspect, or the Blit stretches the picture.
            float sourceAspect = (float)live.targetTexture.width / live.targetTexture.height;
            float frameAspect = (float)textureRecorder.frameWidth / textureRecorder.frameHeight;
            Assert.That(frameAspect, Is.EqualTo(sourceAspect).Within(1e-4f));

            foreach (CaptureRecorder recorder in new[] { textureRecorder, cameraRecorder })
            {
                Assert.That(recorder.recordOnStart, Is.True, recorder.name);
                Assert.That(recorder.interval, Is.EqualTo(CaptureSampleScene.SampleInterval), recorder.name);
                Assert.That(recorder.maxFrames, Is.EqualTo(CaptureSampleScene.SampleMaxFrames), recorder.name);

                // What runs in the client is the UdonBehaviour, not the proxy checked above.
                UdonBehaviour udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(recorder);
                Assert.That(udon, Is.Not.Null, recorder.name);
                Assert.That(udon.programSource, Is.Not.Null, recorder.name);
                Assert.That(udon.publicVariables.TryGetVariableValue("recordOnStart", out bool onStart), Is.True, recorder.name);
                Assert.That(onStart, Is.True, $"{recorder.name}: the proxy's settings did not reach the UdonBehaviour");
                Assert.That(udon.publicVariables.TryGetVariableValue("maxFrames", out int maxFrames), Is.True, recorder.name);
                Assert.That(maxFrames, Is.EqualTo(CaptureSampleScene.SampleMaxFrames), recorder.name);
            }

            CapturePlayer texturePanel = GameObject.Find(CaptureSampleScene.TexturePanelName).GetComponent<CapturePlayer>();
            CapturePlayer cameraPanel = GameObject.Find(CaptureSampleScene.CameraPanelName).GetComponent<CapturePlayer>();
            Assert.That(texturePanel.recorder, Is.EqualTo(textureRecorder));
            Assert.That(cameraPanel.recorder, Is.EqualTo(cameraRecorder));

            foreach (CapturePlayer panel in new[] { texturePanel, cameraPanel })
            {
                Assert.That(panel.gameObject.layer, Is.EqualTo(CapturePanelBuilder.PanelLayer), panel.name);
                Assert.That(panel.GetComponentsInChildren<Button>().Length, Is.EqualTo(11), panel.name);

                // The canvas is readable from the side its forward axis points away from.
                Vector3 toPanel = panel.transform.position - CaptureSampleScene.SpawnPosition;
                Assert.That(Vector3.Dot(panel.transform.forward, toPanel), Is.GreaterThan(0f),
                    $"{panel.name} faces away from the spawn");
            }

            Assert.That(texturePanel.transform.Find("Title").GetComponent<Text>().text,
                Is.Not.EqualTo(cameraPanel.transform.Find("Title").GetComponent<Text>().text),
                "the two panels must be told apart by their titles");

            GameObject screen = GameObject.Find(CaptureSampleScene.LiveScreenName);
            Assert.That(screen.layer, Is.EqualTo(CapturePanelBuilder.PanelLayer));
            Assert.That(screen.GetComponent<MeshRenderer>().sharedMaterial.mainTexture, Is.EqualTo(live.targetTexture));

            Animation orbit = GameObject.Find(CaptureSampleScene.SubjectName).GetComponent<Animation>();
            Assert.That(orbit.clip, Is.Not.Null, "without the clip every captured frame is identical");
            Assert.That(orbit.clip.legacy, Is.True);
            Assert.That(orbit.clip.length, Is.EqualTo(CaptureSampleScene.OrbitPeriod).Within(1e-3f));
            Assert.That(orbit.playAutomatically, Is.True);

            VRCSceneDescriptor world = Object.FindObjectOfType<VRCSceneDescriptor>();
            Assert.That(world, Is.Not.Null);
            Assert.That(world.spawns.Length, Is.EqualTo(1));
            Assert.That(world.spawns[0].position, Is.EqualTo(CaptureSampleScene.SpawnPosition));
            Assert.That(world.ReferenceCamera, Is.Not.Null);
        }

        private static CaptureRecorder Recorder(string name)
        {
            GameObject go = GameObject.Find(name);
            Assert.That(go, Is.Not.Null, $"'{name}' is missing from the saved scene");
            return go.GetComponent<CaptureRecorder>();
        }
    }
}
