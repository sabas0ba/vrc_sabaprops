using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SabaProps.ScreenFx.Editors
{
    public static class ScreenFxRigFactory
    {
        public static readonly Vector3 DefaultVolumeSize = new Vector3(6f, 4f, 6f);

        /// <summary>
        /// Creates the volume a camera has to be inside for the effect to show.
        /// The shader reads the normals and UVs of the built-in cube, so the
        /// mesh must not be replaced.
        /// </summary>
        public static GameObject CreateVolume(ScreenFxPreset preset, bool lite, GameObject parent = null)
        {
            GameObject volume = CreateVolumeObject(
                preset, ScreenFxAssetLibrary.CreateOrLoadMaterial(preset, lite), lite);
            GameObjectUtility.SetParentAndAlign(volume, parent);
            if (parent == null)
            {
                volume.transform.position = new Vector3(0f, DefaultVolumeSize.y * 0.5f, 0f);
            }

            Undo.RegisterCreatedObjectUndo(volume, "Create Screen FX Volume");
            Selection.activeGameObject = volume;
            return volume;
        }

        public static GameObject CreateVolumeObject(ScreenFxPreset preset, Material material, bool lite)
        {
            GameObject volume = GameObject.CreatePrimitive(PrimitiveType.Cube);
            volume.name = "Screen FX Volume - " + preset.displayName + (lite ? " Lite" : string.Empty);
            Object.DestroyImmediate(volume.GetComponent<Collider>());
            volume.transform.localScale = DefaultVolumeSize;

            MeshRenderer renderer = volume.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            // The overlay colours follow the light probe sampled at the volume.
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            return volume;
        }
    }
}
