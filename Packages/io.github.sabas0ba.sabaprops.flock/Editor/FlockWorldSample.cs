using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SabaProps.Flock.Editors
{
    /// <summary>Metre-scale situations for judging a flock as a world prop.</summary>
    public static class FlockWorldSample
    {
        public const string ScenePath = FlockSampleScene.OutputRoot + "/FlockWorldScenarios.unity";
        private const string MaterialsRoot = FlockSampleScene.OutputRoot + "/WorldMaterials";

        public static void GenerateForDistribution()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("Use a clean project to regenerate the world sample.");

            FlockAssetLibrary.EnsureFolder(MaterialsRoot);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            UnityEngine.Object.DestroyImmediate(Camera.main.gameObject);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.65f, 0.69f, 0.73f);
            RenderSettings.fog = false;
            Material ground = Material("Grass", new Color(0.29f, 0.39f, 0.21f));
            Material wood = Material("Wood", new Color(0.34f, 0.23f, 0.13f));
            Material stone = Material("Stone", new Color(0.51f, 0.49f, 0.43f));
            Material sand = Material("Sand", new Color(0.59f, 0.54f, 0.39f));
            Material leaves = Material("Leaves", new Color(0.21f, 0.35f, 0.18f));
            Material wall = Material("Wall", new Color(0.79f, 0.76f, 0.69f));
            Material tankBack = Material("Tank back", new Color(0.035f, 0.15f, 0.18f));
            Material glass = Material("Glass", new Color(0.67f, 0.86f, 0.90f, 0.12f));
            Material water = Material("Water surface", new Color(0.16f, 0.42f, 0.38f, 0.22f));

            Transform sky = Section("01 Open sky", Vector3.zero);
            Box(sky, "Meadow 100 x 100 m", new Vector3(0f, -0.15f, 0f), new Vector3(100f, 0.3f, 100f), ground);
            for (int i = 0; i < 7; i++)
                Tree(sky, new Vector3(-24f + i * 8f, 0f, 22f + (i % 2) * 5f), wood, leaves);
            Bench(sky, new Vector3(-3f, 0f, -4f), wood);
            Swarm(sky, "starling", new Vector3(-8f, 22f, 12f), new Vector3(22f, 7f, 18f), 180, FlockPattern.Murmuration, true, 101);
            Swarm(sky, "goose", new Vector3(10f, 35f, 22f), new Vector3(30f, 8f, 25f), 16, FlockPattern.VFormation, true, 102);
            Swarm(sky, "black-kite", new Vector3(0f, 15f, 5f), new Vector3(10f, 6f, 10f), 4, FlockPattern.Thermal, true, 103);
            View(sky, "Sky - ground eye 1.65 m", new Vector3(0f, 1.65f, -12f), new Vector3(0f, 12f, 18f), true);
            View(sky, "Sky - distant eye 1.65 m", new Vector3(0f, 1.65f, -42f), new Vector3(0f, 15f, 18f));

            Transform small = Section("02 Small aquarium 0.60 x 0.36 x 0.30 m", new Vector3(120f, 0f, 0f));
            Box(small, "Room floor", new Vector3(0f, -0.1f, 1f), new Vector3(5f, 0.2f, 7f), wood);
            Box(small, "Room wall", new Vector3(0f, 1.5f, 3.5f), new Vector3(5f, 3f, 0.15f), wall);
            Box(small, "Cabinet 0.95 m high", new Vector3(0f, 0.475f, 0f), new Vector3(0.85f, 0.95f, 0.5f), wood);
            Tank(small, new Vector3(0f, 1.15f, 0f), new Vector3(0.6f, 0.36f, 0.3f), sand, tankBack, glass, water);
            Plant(small, new Vector3(-0.24f, 0.99f, 0.08f), 0.2f, leaves);
            Plant(small, new Vector3(0.24f, 0.99f, 0.08f), 0.12f, leaves);
            Swarm(small, "neon-tetra", new Vector3(0f, 1.15f, 0f), new Vector3(0.24f, 0.12f, 0.10f), 12, FlockPattern.Wander, false, 201);
            View(small, "Small tank - standing eye 1.65 m", new Vector3(0f, 1.65f, -2.8f), new Vector3(0f, 1.15f, 0.9f));
            View(small, "Small tank - close inspection", new Vector3(0f, 1.2f, -0.55f), new Vector3(0f, 1.15f, 0f));

            Transform large = Section("03 Large aquarium 8 x 3 x 3 m", new Vector3(150f, 0f, 0f));
            Box(large, "Gallery floor", new Vector3(0f, -0.1f, 0f), new Vector3(16f, 0.2f, 14f), stone);
            Box(large, "Gallery wall", new Vector3(0f, 3f, 2f), new Vector3(16f, 6f, 0.2f), wall);
            Box(large, "Tank base", new Vector3(0f, 0.4f, 0f), new Vector3(8.3f, 0.8f, 3.3f), stone);
            Tank(large, new Vector3(0f, 2.3f, 0f), new Vector3(8f, 3f, 3f), sand, tankBack, glass, water);
            Plant(large, new Vector3(-3.7f, 0.86f, 1.1f), 1.2f, leaves);
            Plant(large, new Vector3(3.7f, 0.86f, 1.1f), 0.9f, leaves);
            Bench(large, new Vector3(-4.8f, 0f, -5f), wood);
            Swarm(large, "sardine", new Vector3(0f, 2.5f, 0f), new Vector3(3.6f, 1f, 1.1f), 80, FlockPattern.Stream, true, 301);
            Swarm(large, "anthias", new Vector3(-1.8f, 1.65f, 0f), new Vector3(1.3f, 0.6f, 1f), 24, FlockPattern.Tornado, false, 302);
            View(large, "Large tank - visitor eye 1.65 m", new Vector3(0f, 1.65f, -6.5f), new Vector3(0f, 2.1f, 0f));
            View(large, "Large tank - near glass", new Vector3(1.2f, 1.65f, -2.2f), new Vector3(0f, 2.3f, 0f));
            var lamp = new GameObject("Aquarium lamp - standard point light");
            lamp.transform.SetParent(large, false);
            lamp.transform.localPosition = new Vector3(0f, 3.2f, -0.5f);
            Light aquariumLight = lamp.AddComponent<Light>();
            aquariumLight.type = LightType.Point;
            aquariumLight.color = new Color(0.55f, 0.75f, 1f);
            aquariumLight.intensity = 1.5f;
            aquariumLight.range = 5f;
            aquariumLight.renderMode = LightRenderMode.ForcePixel;

            Transform river = Section("04 River fish through water", new Vector3(200f, 0f, 0f));
            Box(river, "River bed", new Vector3(0f, -1.8f, 0f), new Vector3(28f, 0.2f, 8f), sand);
            Box(river, "Near bank", new Vector3(0f, -0.15f, -6f), new Vector3(28f, 0.3f, 4f), ground);
            Box(river, "Far bank", new Vector3(0f, -0.15f, 6f), new Vector3(28f, 0.3f, 4f), ground);
            Box(river, "Water surface 28 x 8 m", Vector3.zero, new Vector3(28f, 0.015f, 8f), water);
            Box(river, "Footbridge", new Vector3(-9f, 0.35f, 0f), new Vector3(2f, 0.25f, 10f), wood);
            Box(river, "Bridge handrail", new Vector3(-10f, 1.35f, 0f), new Vector3(0.08f, 0.08f, 10f), wood);
            for (int i = 0; i < 5; i++)
            {
                Box(river, "Bridge post", new Vector3(-10f, 0.9f, -4f + i * 2f), new Vector3(0.1f, 1f, 0.1f), wood);
                Sphere(river, "River stone", new Vector3(-8f + i * 4f, -1.5f, 2.8f), new Vector3(0.8f, 0.35f, 0.5f), stone);
            }
            Swarm(river, "koi", new Vector3(2f, -0.85f, 0f), new Vector3(7f, 0.5f, 2.8f), 12, FlockPattern.Wander, true, 401);
            Swarm(river, "medaka-wild", new Vector3(3f, -0.2f, -2.9f), new Vector3(2f, 0.09f, 0.6f), 32, FlockPattern.Wander, false, 402);
            View(river, "River - bank eye 1.65 m", new Vector3(3f, 1.65f, -4.8f), new Vector3(2f, -0.6f, 0f));
            View(river, "River - bridge eye 2.10 m", new Vector3(-9f, 2.1f, -1f), new Vector3(0f, -0.6f, 0f));

            AddSpeciesVariety(sky, small, large, river, wood, stone, sand, tankBack, glass, water);
            EditorSceneManager.SaveScene(scene, ScenePath);
            SetView("Sky - ground eye 1.65 m");
            Debug.Log("[SabaProps Flock] World situations created: " + ScenePath);
        }

        private static void AddSpeciesVariety(Transform sky, Transform small, Transform large, Transform river,
            Material wood, Material stone, Material sand, Material back, Material glass, Material water)
        {
            var present = new HashSet<string>();
            foreach (FlockSwarm swarm in UnityEngine.Object.FindObjectsOfType<FlockSwarm>()) present.Add(swarm.presetId);
            Transform ocean = Section("05 Oceanarium 24 x 10 x 12 m", new Vector3(260f, 0f, 0f));
            Box(ocean, "Observation floor", new Vector3(0f, -0.1f, -8f), new Vector3(28f, 0.2f, 8f), stone);
            Tank(ocean, new Vector3(0f, 5f, 0f), new Vector3(24f, 10f, 12f), sand, back, glass, water);
            Bench(ocean, new Vector3(-8f, 0f, -9f), wood);
            View(ocean, "Oceanarium - visitor eye 1.65 m", new Vector3(0f, 1.65f, -18f), new Vector3(0f, 5f, 0f));
            View(ocean, "Oceanarium - near glass", new Vector3(0f, 1.65f, -7f), new Vector3(0f, 5f, 0f));
            View(sky, "Ground birds - standing eye 1.65 m", new Vector3(0f, 1.65f, -5f), new Vector3(0f, 0.3f, -1f));

            int flying = 0, groundBird = 0, aquarium = 0, marine = 0, giant = 0;
            foreach (FlockPreset preset in FlockSpeciesCatalog.All)
            {
                FlockSpecies species = preset.Species;
                string id = species.id;
                if (species.category == FlockCategory.Bird && !present.Contains(id))
                {
                    if (species.grounded)
                    {
                        Swarm(sky, id, new Vector3(groundBird++ == 0 ? -1f : 1f, 0f, -1f),
                            new Vector3(0.75f, 0.001f, 0.75f), species.defaultCount, FlockPattern.Wander, false, 110 + groundBird);
                    }
                    else
                    {
                        Vector3 position = new Vector3((flying % 5 - 2) * 9f, 7f + flying / 5 * 5f, 10f + flying / 5 * 6f);
                        Swarm(sky, id, position, new Vector3(4f, 2f, 4f), Mathf.Min(species.defaultCount, 6),
                            species.defaultPattern, true, 120 + flying++);
                    }
                }
                else if (preset.Habitat == FlockHabitat.Aquarium && id != "neon-tetra" && id != "koi" && id != "eel")
                {
                    // One species per 60 cm tank keeps the examples readable without implying cohabitation.
                    // Skip the front-row centre, which contains the existing neon tetra tank.
                    int cell = aquarium < 1 ? 0 : aquarium + 1;
                    Vector3 offset = new Vector3((cell % 3 - 1) * 0.9f, 0f, cell / 3 * 0.9f);
                    Box(small, "Cabinet - " + id, offset + Vector3.up * 0.475f, new Vector3(0.8f, 0.95f, 0.5f), wood);
                    Tank(small, offset + Vector3.up * 1.15f, new Vector3(0.6f, 0.36f, 0.3f), sand, back, glass, water);
                    float margin = species.bodyLength * 1.3f;
                    Vector3 area = new Vector3(Mathf.Max(0.3f - margin, 0.005f), Mathf.Max(0.18f - margin, 0.005f),
                        Mathf.Max(0.15f - margin, 0.005f));
                    Swarm(small, id, offset + Vector3.up * 1.15f, area, Mathf.Min(species.defaultCount, 8),
                        FlockPattern.Wander, false, 210 + aquarium++);
                }
                else if (id == "eel" || id == "salmon")
                {
                    Swarm(river, id, new Vector3(id == "eel" ? -3f : 6f, -1f, 0f),
                        new Vector3(3f, 0.3f, 2f), 3, FlockPattern.Wander, true, id == "eel" ? 403 : 404);
                }
                else if ((preset.Habitat == FlockHabitat.Sea || preset.Habitat == FlockHabitat.Reef) && !present.Contains(id))
                {
                    if (species.bodyLength > 0.8f)
                    {
                        Swarm(ocean, id, new Vector3((giant % 3 - 1) * 7f, giant / 3 == 0 ? 4f : 7f, 0f),
                            new Vector3(2.8f, 1.2f, 2.3f), Mathf.Min(species.defaultCount, 3), FlockPattern.Wander, true, 501 + giant++);
                    }
                    else
                    {
                        bool anchored = species.defaultPattern == FlockPattern.Anchored;
                        float floorOffset = 0f;
                        if (anchored)
                            foreach (Vector3 vertex in FlockBodyBuilder.Build(species, FlockDetail.High).Positions)
                                floorOffset = Mathf.Max(floorOffset, -vertex.y);
                        Vector3 position = new Vector3((marine % 6 - 2.5f) * 1.1f, anchored ? 0.825f + floorOffset : 1.65f + marine / 6 * 0.45f,
                            anchored ? -0.4f : 0.35f);
                        float length = species.bodyLength;
                        Swarm(large, id, position, anchored ? Vector3.one * length : new Vector3(length + 0.3f, length + 0.1f, length + 0.2f),
                            anchored ? 1 : Mathf.Min(species.defaultCount, length < 0.15f ? 6 : 3),
                            anchored ? FlockPattern.Anchored : FlockPattern.Wander, false, 310 + marine++);
                    }
                }
            }
        }

        private static Transform Section(string name, Vector3 position)
        {
            var section = new GameObject(name);
            section.transform.position = position;
            return section.transform;
        }

        private static Material Material(string name, Color color)
        {
            var material = new Material(Shader.Find("Standard")) { name = name, color = color };
            material.SetFloat("_Glossiness", color.a < 1f ? 0.7f : 0.1f);
            if (color.a < 1f)
            {
                material.SetFloat("_Mode", 2f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.EnableKeyword("_ALPHABLEND_ON");
                material.renderQueue = 3000;
            }
            AssetDatabase.CreateAsset(material, MaterialsRoot + "/" + name + ".mat");
            return material;
        }

        private static GameObject Shape(Transform parent, string name, Vector3 position, Vector3 size, Material material, PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static void Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        { Shape(parent, name, position, size, material, PrimitiveType.Cube); }

        private static void Sphere(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        { Shape(parent, name, position, size, material, PrimitiveType.Sphere); }

        private static void Tree(Transform parent, Vector3 position, Material wood, Material leaves)
        {
            Box(parent, "Tree trunk", position + Vector3.up * 1.5f, new Vector3(0.35f, 3f, 0.35f), wood);
            Sphere(parent, "Tree crown", position + Vector3.up * 4f, new Vector3(4f, 4.5f, 4f), leaves);
        }

        private static void Bench(Transform parent, Vector3 position, Material wood)
        {
            Box(parent, "Bench seat 0.45 m", position + Vector3.up * 0.45f, new Vector3(1.8f, 0.08f, 0.5f), wood);
            Box(parent, "Bench back", position + new Vector3(0f, 0.75f, 0.2f), new Vector3(1.8f, 0.5f, 0.08f), wood);
            for (int side = -1; side <= 1; side += 2)
                Box(parent, "Bench leg", position + new Vector3(side * 0.65f, 0.2f, 0f), new Vector3(0.1f, 0.4f, 0.4f), wood);
        }

        private static void Plant(Transform parent, Vector3 position, float height, Material leaves)
        {
            for (int i = -1; i <= 1; i++)
                Sphere(parent, "Aquatic plant", position + new Vector3(i * height * 0.16f, height * 0.5f, 0f), new Vector3(height * 0.13f, height, height * 0.13f), leaves);
        }

        private static void Tank(Transform parent, Vector3 centre, Vector3 size, Material sand, Material back, Material glass, Material water)
        {
            Box(parent, "Tank sand bed", centre - Vector3.up * (size.y * 0.5f), new Vector3(size.x, 0.03f, size.z), sand);
            Box(parent, "Tank back", centre + Vector3.forward * (size.z * 0.5f), new Vector3(size.x, size.y, 0.015f), back);
            Box(parent, "Tank front glass", centre - Vector3.forward * (size.z * 0.5f), new Vector3(size.x, size.y, 0.006f), glass);
            for (int side = -1; side <= 1; side += 2)
                Box(parent, "Tank side glass", centre + Vector3.right * (side * size.x * 0.5f), new Vector3(0.006f, size.y, size.z), glass);
            Box(parent, "Tank water surface", centre + Vector3.up * (size.y * 0.5f), new Vector3(size.x, 0.006f, size.z), water);
        }

        private static void Swarm(Transform parent, string preset, Vector3 position, Vector3 area, int count, FlockPattern pattern, bool lod, int seed)
        {
            FlockSwarm swarm = FlockSwarmBuilder.Create(preset, parent.gameObject, position);
            swarm.settings.area = area;
            swarm.settings.count = count;
            swarm.settings.pattern = pattern;
            swarm.settings.seed = seed;
            swarm.settings.lodMode = lod ? FlockLodMode.LodGroup : FlockLodMode.Single;
            swarm.settings.detail = FlockDetail.High;
            if (preset == "starling") swarm.settings.clusterRadius = 5f;
            FlockSwarmBuilder.Rebuild(swarm);
        }

        private static void View(Transform parent, string name, Vector3 position, Vector3 target, bool enabled = false)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = position;
            holder.transform.rotation = Quaternion.LookRotation(parent.TransformPoint(target) - holder.transform.position);
            Camera camera = holder.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 120f;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.enabled = enabled;
            if (enabled) holder.tag = "MainCamera";
        }

        [MenuItem("Tools/SabaProps/Flock/Sample View/Open sky")]
        public static void SkyView() { SetView("Sky - ground eye 1.65 m"); }
        [MenuItem("Tools/SabaProps/Flock/Sample View/Small aquarium")]
        public static void SmallView() { SetView("Small tank - standing eye 1.65 m"); }
        [MenuItem("Tools/SabaProps/Flock/Sample View/Large aquarium")]
        public static void LargeView() { SetView("Large tank - visitor eye 1.65 m"); }
        [MenuItem("Tools/SabaProps/Flock/Sample View/River")]
        public static void RiverView() { SetView("River - bank eye 1.65 m"); }

        [MenuItem("Tools/SabaProps/Flock/Sample View/Oceanarium")]
        public static void OceanView() { SetView("Oceanarium - visitor eye 1.65 m"); }
        [MenuItem("Tools/SabaProps/Flock/Sample View/Ground birds")]
        public static void GroundView() { SetView("Ground birds - standing eye 1.65 m"); }

        public static void SetView(string name)
        {
            Camera selected = null;
            foreach (Camera camera in UnityEngine.Object.FindObjectsOfType<Camera>())
                if (camera.name == name) selected = camera;
            if (selected == null)
            {
                Debug.LogWarning("Open FlockWorldScenarios.unity before selecting a sample view.");
                return;
            }
            foreach (Camera camera in UnityEngine.Object.FindObjectsOfType<Camera>())
            {
                Undo.RecordObject(camera, "Select Flock sample viewpoint");
                camera.enabled = camera == selected;
                camera.tag = camera == selected ? "MainCamera" : "Untagged";
            }
            Selection.activeGameObject = selected.gameObject;
            EditorSceneManager.MarkSceneDirty(selected.gameObject.scene);
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.AlignViewToObject(selected.transform);
        }

        /// <summary>Opens the generated scene in a dedicated review project.</summary>
        public static void OpenForReview()
        {
            EditorSceneManager.OpenScene(ScenePath);
            SkyView();
            EditorApplication.ExecuteMenuItem("Window/General/Game");
        }
    }
}
