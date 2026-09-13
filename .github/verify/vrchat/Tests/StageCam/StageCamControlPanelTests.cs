using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SabaProps.StageCam.Editors;
using UdonSharp.Compiler;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;

namespace SabaProps.StageCam.WorldTests
{
    public class StageCamControlPanelTests
    {
        [OneTimeSetUp]
        public void CompilePrograms()
        {
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
        }

        [SetUp]
        public void CreateDemo()
        {
            StageCamSampleScene.Create();
            EditorSceneManager.OpenScene(StageCamSampleScene.ScenePath);
        }

        [Test]
        public void SavedButtons_TargetExportedUdonEvents()
        {
            var panel = Object.FindObjectOfType<StageCamControlPanel>();
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(panel.GetComponent<VRCUiShape>(), Is.Not.Null);
            Assert.That(panel.GetComponent<GraphicRaycaster>(), Is.Not.Null);
            Assert.That(panel.rigs.Length, Is.EqualTo(2));
            Assert.That(panel.preview.texture, Is.EqualTo(panel.rigs[0].framingCamera.targetTexture));

            var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(panel);
            var asset = UdonSharpEditorUtility.GetUdonSharpProgramAsset(backing);
            Assert.That(asset.SerializedProgramAsset, Is.Not.Null);
            var program = asset.SerializedProgramAsset.RetrieveProgram();
            var exports = new List<string>(program.EntryPoints.GetExportedSymbols());
            var buttons = panel.GetComponentsInChildren<Button>();
            Assert.That(buttons.Length, Is.EqualTo(18));
            foreach (var button in buttons)
            {
                Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1), button.name);
                Assert.That(button.onClick.GetPersistentTarget(0), Is.EqualTo(backing), button.name);
                Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo("SendCustomEvent"));
                var serialized = new SerializedObject(button);
                string eventName = serialized.FindProperty("m_OnClick.m_PersistentCalls.m_Calls.Array.data[0].m_Arguments.m_StringArgument").stringValue;
                CollectionAssert.Contains(exports, eventName, button.name);
            }
        }

        [Test]
        public void Controls_ChangeOnlySelectedRigAndUseFramingWhenEnabled()
        {
            var panel = Object.FindObjectOfType<StageCamControlPanel>();
            StageCamRig face = panel.rigs[0];
            StageCamRig crane = panel.rigs[1];
            float craneYaw = crane.orbitYaw;
            float faceDistance = face.orbitDistance;
            float coverage = face.screenFraction;
            panel._YawRight();
            panel._Closer();
            Assert.That(crane.orbitYaw, Is.EqualTo(craneYaw));
            Assert.That(face.screenFraction, Is.EqualTo(coverage + 0.05f).Within(0.0001f));
            Assert.That(face.orbitDistance, Is.EqualTo(faceDistance));
            float faceYaw = face.orbitYaw;
            panel._NextCamera();
            Assert.That(panel.preview.texture, Is.EqualTo(crane.framingCamera.targetTexture));
            panel._YawLeft();
            Assert.That(face.orbitYaw, Is.EqualTo(faceYaw));
            Assert.That(crane.orbitYaw, Is.EqualTo(craneYaw - 5f));
            panel._NextCamera();
            Assert.That(panel.GetSelectedRig(), Is.EqualTo(face));
            face.subject = StageCamRig.SubjectRightFoot;
            panel._NextSubject();
            Assert.That(face.subject, Is.EqualTo(StageCamRig.SubjectFace));
        }

        [Test]
        public void MissingRigsAndInvalidPlayers_DoNotThrowOrRetarget()
        {
            var panel = Object.FindObjectOfType<StageCamControlPanel>();
            var rig = panel.rigs[0];
            int target = rig.GetTargetPlayerId();
            rig.SetTargetPlayer(-1);
            Assert.That(rig.GetTargetPlayerId(), Is.EqualTo(target));
            panel.rigs = new StageCamRig[0];
            Assert.DoesNotThrow(() => { panel._NextCamera(); panel._Closer(); panel._ApplyPlayer(); panel.RefreshView(); });
            Assert.That(panel.preview.enabled, Is.False);
        }

        [Test]
        public void MenuPlacement_PreservesCanvasScaleAndLayerUnderAParent()
        {
            var parent = new GameObject("Panel parent");
            StageCamControlPanelBuilder.CreateFromMenu(new MenuCommand(parent));
            var panel = Selection.activeGameObject.GetComponent<StageCamControlPanel>();
            Assert.That(panel.transform.parent, Is.EqualTo(parent.transform));
            Assert.That(panel.transform.localScale.x, Is.EqualTo(0.0018f).Within(0.000001f));
            Assert.That(panel.gameObject.layer, Is.EqualTo(StageCamAssets.ScreenLayer));
        }

        [Test]
        public void RenderPanelForVisualReview()
        {
            var panel = Object.FindObjectOfType<StageCamControlPanel>();
            foreach (StageCamRig rig in panel.rigs) rig.framingCamera.Render();
            panel.RefreshView();
            Canvas.ForceUpdateCanvases();
            var cameraObject = new GameObject("Panel review capture");
            var camera = cameraObject.AddComponent<Camera>();
            var texture = new RenderTexture(1000, 1080, 24);
            var pixels = new Texture2D(1000, 1080, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.transform.position = panel.transform.position - Vector3.forward * 3f;
                camera.orthographic = true;
                camera.orthographicSize = 1.02f;
                camera.cullingMask = 1 << StageCamAssets.ScreenLayer;
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0f, 0f, 1000f, 1080f), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory("TestResults");
                File.WriteAllBytes("TestResults/stagecam-panel.png", pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
