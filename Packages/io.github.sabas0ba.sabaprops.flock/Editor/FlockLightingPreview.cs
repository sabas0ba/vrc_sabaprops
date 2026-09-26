using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SabaProps.Flock.Editors
{
    /// <summary>Editor lighting presets for reviewing the sample with standard Unity lights.</summary>
    public static class FlockLightingPreview
    {
        [MenuItem("Tools/SabaProps/Flock/Sample Lighting/Day")]
        public static void Day() { Apply(new Color(0.55f, 0.6f, 0.65f), Color.white, 1f, 50f); }
        [MenuItem("Tools/SabaProps/Flock/Sample Lighting/Evening")]
        public static void Evening() { Apply(new Color(0.10f, 0.075f, 0.08f), new Color(1f, 0.40f, 0.15f), 0.65f, 8f); }
        [MenuItem("Tools/SabaProps/Flock/Sample Lighting/Night")]
        public static void Night() { Apply(new Color(0.008f, 0.012f, 0.024f), new Color(0.25f, 0.35f, 0.65f), 0.08f, 35f); }

        private static void Apply(Color ambient, Color direct, float intensity, float elevation)
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name != "FlockWorldScenarios" && scene.name != "FlockComparisons" && scene.name != "FlockSample")
            {
                Debug.LogWarning("Open a Flock sample scene before applying sample lighting.");
                return;
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            foreach (Light light in Object.FindObjectsOfType<Light>())
            {
                if (light.gameObject.scene != scene || light.type != LightType.Directional) continue;
                Undo.RecordObject(light, "Change Flock sample lighting");
                light.color = direct; light.intensity = intensity;
                light.transform.rotation = Quaternion.Euler(elevation, -25f, 0f);
            }
            foreach (Camera camera in Object.FindObjectsOfType<Camera>())
            {
                if (camera.gameObject.scene != scene) continue;
                Undo.RecordObject(camera, "Change Flock sample background");
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = ambient;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            SceneView.RepaintAll();
        }
    }
}
