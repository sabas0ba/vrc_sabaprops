#if UNITY_EDITOR
using NUnit.Framework;
using SabaProps.Flock;
using SabaProps.Flock.Editors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SabaProps.Flock.CITests
{
    public class FlockLightingTests
    {
        [Test]
        public void Render_DistanceColoursFollowAmbientAndPointLight()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("This rendering check requires a graphics device.");
            Color previousAmbient = RenderSettings.ambientLight;
            AmbientMode previousMode = RenderSettings.ambientMode;
            SphericalHarmonicsL2 previousProbe = RenderSettings.ambientProbe;
            RenderTexture previousTarget = RenderTexture.active;
            Material material = null;
            RenderTexture target = null;
            Texture2D pixels = null;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                RenderSettings.fog = false;
                RenderSettings.ambientMode = AmbientMode.Flat;
                FlockSwarm swarm = FlockSwarmBuilder.Create("sardine", null, Vector3.zero);
                swarm.settings.pattern = FlockPattern.Anchored; swarm.settings.count = 1;
                swarm.settings.lodMode = FlockLodMode.Single; swarm.settings.detail = FlockDetail.High;
                FlockSwarmBuilder.Rebuild(swarm);
                material = new Material(Shader.Find(FlockShaderContract.ShaderName));
                material.SetFloat("_SilhouetteStart", 0f); material.SetFloat("_SilhouetteEnd", 0.01f);
                material.SetFloat("_MediumDensity", 2f);
                swarm.GetComponentInChildren<MeshRenderer>().sharedMaterial = material;
                var cameraObject = new GameObject("Lighting test camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(0f, 0f, -0.4f);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.nearClipPlane = 0.01f;
                target = new RenderTexture(128, 128, 24);
                pixels = new Texture2D(128, 128, TextureFormat.RGB24, false);
                camera.targetTexture = target;

                RenderSettings.ambientLight = Color.black;
                RenderSettings.ambientProbe = new SphericalHarmonicsL2();
                float dark = Peak(camera, target, pixels);
                var ambient = new SphericalHarmonicsL2(); ambient.AddAmbientLight(Color.white);
                RenderSettings.ambientLight = Color.white; RenderSettings.ambientProbe = ambient;
                float lit = Peak(camera, target, pixels);
                Assert.Greater(lit, dark + 0.03f, "ambient lighting did not affect distance colours");
                Assert.Less(dark, 0.01f, "distance colour glows in a dark scene");

                RenderSettings.ambientLight = Color.black; RenderSettings.ambientProbe = new SphericalHarmonicsL2();
                material.SetFloat("_MediumDensity", 0f);
                var lamp = new GameObject("Lighting test point light");
                Light light = lamp.AddComponent<Light>(); light.type = LightType.Point;
                light.transform.position = new Vector3(0f, 0.15f, -0.2f);
                light.range = 2f; light.intensity = 3f; light.renderMode = LightRenderMode.ForcePixel;
                Assert.Greater(Peak(camera, target, pixels), dark + 0.03f, "the point light did not illuminate the fish");
                Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));
            }
            finally
            {
                RenderTexture.active = previousTarget;
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                RenderSettings.ambientMode = previousMode; RenderSettings.ambientLight = previousAmbient;
                RenderSettings.ambientProbe = previousProbe;
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                if (pixels != null) Object.DestroyImmediate(pixels);
                if (material != null) Object.DestroyImmediate(material);
                AssetDatabase.DeleteAsset(FlockAssetLibrary.RootFolder);
            }
        }

        private static float Peak(Camera camera, RenderTexture target, Texture2D pixels)
        {
            camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0f, 0f, 128f, 128f), 0, 0); pixels.Apply();
            float maximum = 0f;
            foreach (Color color in pixels.GetPixels()) maximum = Mathf.Max(maximum, color.r, color.g, color.b);
            return maximum;
        }
    }
}
#endif
