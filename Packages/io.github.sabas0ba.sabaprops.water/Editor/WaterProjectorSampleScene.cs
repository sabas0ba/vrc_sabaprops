using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SabaProps.Water.Editors
{
    /// <summary>Projector comparison on unchanged Standard materials.</summary>
    public static class WaterProjectorSampleScene
    {
        public const string Folder = WaterSampleScene.SampleFolder + "/Projector";
        public const string ScenePath = WaterSampleScene.SampleFolder + "/WaterDropletProjectorGallery.unity";

        [MenuItem("Tools/SabaProps/Water/Create Droplet Projector Gallery", false, 4)]
        public static void CreateAndOpen()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Create();
            }
        }

        public static void Create()
        {
            WaterAssetLibrary.EnsureFolder(Folder);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.6f);
            var root = new GameObject("Droplet Projector Gallery");
            Material receiver = SaveMaterial("Receiver", Shader.Find("Standard"));
            receiver.color = new Color(0.15f, 0.22f, 0.28f);
            receiver.SetFloat("_Glossiness", 0.25f);
            foreach (float x in new[] { -0.85f, 0.85f, 3.5f })
            {
                GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.name = x > 3f ? "Outside Projection - Dry" : "Standard Receiver";
                capsule.transform.SetParent(root.transform);
                capsule.transform.position = new Vector3(x, 1.05f, 0f);
                capsule.GetComponent<Renderer>().sharedMaterial = receiver;
            }
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(root.transform);
            floor.transform.position = new Vector3(1f, -0.15f, 0f);
            floor.transform.localScale = new Vector3(8f, 0.2f, 5f);
            floor.GetComponent<Renderer>().sharedMaterial = receiver;

            GameObject rig = WaterRigFactory.CreateDropletProjector(root);
            rig.transform.position = new Vector3(0f, 1.3f, -2f);
            Material projected = SaveMaterial("DropletProjector",
                WaterAssetLibrary.LoadShader(WaterAssetLibrary.DropletProjectorShaderName));
            rig.GetComponent<Projector>().material = projected;
            var sun = new GameObject("Directional Light", typeof(Light));
            sun.transform.SetParent(root.transform);
            sun.transform.rotation = Quaternion.Euler(40f, -25f, 0f);
            sun.GetComponent<Light>().type = LightType.Directional;
            sun.GetComponent<Light>().intensity = 0.8f;
            var cameraObject = new GameObject("Projector Preview Camera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform);
            cameraObject.transform.position = new Vector3(1.2f, 2f, -6f);
            cameraObject.transform.LookAt(new Vector3(1.2f, 1f, 0f));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.08f, 0.1f);
            camera.nearClipPlane = 0.1f;
            WaterVrcWorld.CreateWorld(new Vector3(0f, 0f, -1f), Quaternion.identity, camera);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = rig;
        }

        private static Material SaveMaterial(string name, Shader shader)
        {
            string path = Folder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        /// <summary>Capture both states with a graphics device enabled.</summary>
        public static void CreateAndCapture()
        {
            Create();
            Camera camera = GameObject.Find("Projector Preview Camera").GetComponent<Camera>();
            Projector projector = Object.FindObjectOfType<Projector>();
            var target = new RenderTexture(1200, 720, 24);
            var image = new Texture2D(1200, 720, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                foreach (bool enabled in new[] { false, true })
                {
                    projector.enabled = enabled;
                    camera.Render();
                    RenderTexture.active = target;
                    image.ReadPixels(new Rect(0, 0, 1200, 720), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Folder + (enabled ? "/ProjectorOn.png" : "/ProjectorOff.png"),
                        image.EncodeToPNG());
                }
            }
            finally
            {
                projector.enabled = true;
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(target);
            }
            AssetDatabase.Refresh();
        }
    }
}
