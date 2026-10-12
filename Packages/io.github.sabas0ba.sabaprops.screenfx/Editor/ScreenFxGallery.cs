using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SabaProps.ScreenFx.Editors
{
    /// <summary>
    /// Builds a walk-through scene with one booth per preset. Entering a booth
    /// fades its effect in, so every condition can be compared on foot.
    /// </summary>
    public static class ScreenFxGallery
    {
        public const string SamplesFolder = ScreenFxAssetLibrary.RootFolder + "/Samples";
        public const string ScenePath = SamplesFolder + "/ScreenFxGallery.unity";
        public const string RootName = "SabaProps Screen FX Gallery";

        public const int Columns = 6;
        public const float Pitch = 7f;
        public static readonly Vector3 BoothSize = new Vector3(4f, 3.5f, 4f);

        private const string DescriptorTypeName = "VRC.SDK3.Components.VRCSceneDescriptor";

        /// <summary>
        /// Replaces the current scene with the gallery and writes it to
        /// <see cref="ScenePath"/>. Prompt-free for tests.
        /// </summary>
        public static Scene Create(bool lite)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ScreenFxAssetLibrary.EnsureFolder(SamplesFolder);
            var root = new GameObject(RootName);

            int rows = (ScreenFxPresets.All.Length + Columns - 1) / Columns;
            float width = Columns * Pitch;
            float depth = rows * Pitch;

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root.transform, false);
            ground.transform.localPosition = new Vector3(0f, 0f, depth * 0.5f - Pitch * 0.5f);
            ground.transform.localScale = new Vector3(width / 10f + 2f, 1f, depth / 10f + 2f);
            ground.GetComponent<MeshRenderer>().sharedMaterial =
                CreateColourMaterial("Ground", new Color(0.32f, 0.36f, 0.33f));

            Material[] pillarMaterials =
            {
                CreateColourMaterial("Pillar Red", new Color(0.8f, 0.25f, 0.2f)),
                CreateColourMaterial("Pillar Yellow", new Color(0.9f, 0.75f, 0.2f)),
                CreateColourMaterial("Pillar Blue", new Color(0.2f, 0.45f, 0.85f)),
                CreateColourMaterial("Pillar White", new Color(0.9f, 0.9f, 0.9f)),
            };

            for (int index = 0; index < ScreenFxPresets.All.Length; index++)
            {
                ScreenFxPreset preset = ScreenFxPresets.All[index];
                var centre = new Vector3(
                    (index % Columns - (Columns - 1) * 0.5f) * Pitch, 0f, index / Columns * Pitch);
                BuildBooth(root.transform, preset, lite, centre, pillarMaterials);
            }

            var sun = new GameObject("Directional Light", typeof(Light));
            sun.transform.SetParent(root.transform, false);
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light light = sun.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            // Realtime shadows make Unity render the camera depth texture the
            // distance fog reads.
            light.shadows = LightShadows.Soft;

            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(0f, 1.6f, -Pitch);
            Camera camera = cameraObject.GetComponent<Camera>();

            CreateWorld(new Vector3(0f, 0.05f, -Pitch), camera);

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = root;
            Debug.Log("[SabaProps Screen FX] Gallery created at " + ScenePath);
            return scene;
        }

        private static void BuildBooth(
            Transform parent, ScreenFxPreset preset, bool lite, Vector3 centre, Material[] pillarMaterials)
        {
            var booth = new GameObject("Booth - " + preset.displayName);
            booth.transform.SetParent(parent, false);
            booth.transform.localPosition = centre;

            GameObject volume = ScreenFxRigFactory.CreateVolumeObject(
                preset, ScreenFxAssetLibrary.CreateOrLoadMaterial(preset, lite), lite);
            volume.transform.SetParent(booth.transform, false);
            volume.transform.localPosition = new Vector3(0f, BoothSize.y * 0.5f, 0f);
            volume.transform.localScale = BoothSize;

            // Coloured pillars outside the volume give distortion, blur and
            // colour changes something to act on.
            for (int corner = 0; corner < 4; corner++)
            {
                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = "Pillar " + corner;
                pillar.transform.SetParent(booth.transform, false);
                pillar.transform.localPosition = new Vector3(
                    (corner % 2 * 2 - 1) * (BoothSize.x * 0.5f + 0.4f), 1.5f,
                    (corner / 2 * 2 - 1) * (BoothSize.z * 0.5f + 0.4f));
                pillar.transform.localScale = new Vector3(0.3f, 1.5f, 0.3f);
                pillar.GetComponent<MeshRenderer>().sharedMaterial = pillarMaterials[corner];
            }

            var label = new GameObject("Label - " + preset.displayName, typeof(TextMesh));
            label.transform.SetParent(booth.transform, false);
            label.transform.localPosition = new Vector3(0f, 0.02f, -BoothSize.z * 0.5f - 0.6f);
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            TextMesh mesh = label.GetComponent<TextMesh>();
            mesh.text = preset.displayName;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.characterSize = 0.08f;
            mesh.fontSize = 48;
            mesh.color = Color.white;
        }

        private static Material CreateColourMaterial(string name, Color colour)
        {
            string path = SamplesFolder + "/Gallery " + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { name = "Gallery " + name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = colour;
            EditorUtility.SetDirty(material);
            return material;
        }

        // Reflection keeps the Worlds SDK optional: without it the scene still
        // gets VRCWorld and Spawn, so the layout is the same either way.
        private static void CreateWorld(Vector3 spawnPosition, Camera referenceCamera)
        {
            var world = new GameObject("VRCWorld");
            var spawn = new GameObject("Spawn");
            spawn.transform.SetParent(world.transform, false);
            spawn.transform.position = spawnPosition;

            Type descriptorType = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                descriptorType = assembly.GetType(DescriptorTypeName, false);
                if (descriptorType != null)
                {
                    break;
                }
            }

            if (descriptorType == null)
            {
                return;
            }

            Component descriptor = world.AddComponent(descriptorType);
            SetField(descriptor, "spawns", new[] { spawn.transform });
            SetField(descriptor, "ReferenceCamera", referenceCamera.gameObject);
        }

        private static void SetField(Component target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.FieldType.IsInstanceOfType(value))
            {
                field.SetValue(target, value);
                return;
            }

            Debug.LogWarning(
                $"[SabaProps Screen FX] VRCSceneDescriptor の '{name}' を設定できませんでした。Inspector で確認してください。");
        }
    }
}
