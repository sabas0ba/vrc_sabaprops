using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace SabaProps.StageCam.Editors
{
    /// <summary>Entry points that get a user from a fresh project to a working rig.</summary>
    public static class StageCamMenu
    {
        [MenuItem("GameObject/SabaProps/Stage Camera Rig", false, 10)]
        public static void CreateRig(MenuCommand command)
        {
            var root = new GameObject("Stage Camera Rig");

            // Interact needs a collider on the object the behaviour is on.
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.3f, 0.3f, 0.4f);

            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(root.transform, false);

            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 40f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 120f;

            StageCamRig rig = root.AddUdonSharpComponent<StageCamRig>();
            rig.rigRoot = root.transform;
            rig.framingCamera = camera;

            GameObjectUtility.SetParentAndAlign(root, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(root, "Create Stage Camera Rig");
            Selection.activeGameObject = root;

            Debug.Log(
                "[SabaProps Stage Cam] リグを作成しました。"
                + "カメラの Target Texture に RenderTexture を割り当て、それを貼ったスクリーンを置いてください。");
        }

        [MenuItem("Tools/SabaProps/Stage Cam/Documentation", false, 100)]
        public static void OpenDocumentation()
        {
            Application.OpenURL(
                "https://github.com/sabas0ba/vrc_sabaprops/blob/main/Packages/io.github.sabas0ba.sabaprops.stagecam/README.md");
        }
    }
}
