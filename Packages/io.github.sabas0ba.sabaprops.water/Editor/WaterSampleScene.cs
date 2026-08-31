using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SabaProps.Water.Editors
{
    /// <summary>
    /// Builds a self-contained gallery for the package's surface, weather and
    /// underwater features. All generated references are copied below the
    /// sample folder so the scene can be distributed as a UPM sample.
    /// </summary>
    public static class WaterSampleScene
    {
        public const string SampleFolder = WaterAssetLibrary.RootFolder + "/Samples/WaterFeatureGallery";
        public const string ScenePath = SampleFolder + "/WaterFeatureGallery.unity";
        public const string MaterialsFolder = SampleFolder + "/Materials";
        public const string MeshesFolder = SampleFolder + "/Meshes";
        public const string ProfilesFolder = SampleFolder + "/Profiles";
        public const string CapturesFolder = SampleFolder + "/Captures";
        public const string OverviewCapturePath = CapturesFolder + "/WaterFeatureGallery.png";
        public const string UnderwaterCapturePath = CapturesFolder + "/UnderwaterStandard.png";

        public const string SurfaceRootName = "1 Water Surfaces";
        public const string RainRootName = "2 Rain and Ripples";
        public const string AtmosphereRootName = "3 Fog and Clouds";
        public const string UnderwaterRootName = "4 Underwater";
        public const string OverviewCameraName = "Documentation Camera - Overview";
        public const string UnderwaterCameraName = "Documentation Camera - Underwater Standard";

        private static readonly Color GroundColour = new Color(0.075f, 0.09f, 0.105f, 1f);
        private static readonly Color PlatformColour = new Color(0.19f, 0.22f, 0.24f, 1f);
        private static readonly Color LiteColour = new Color(0.18f, 0.55f, 0.68f, 1f);
        private static readonly Color StandardColour = new Color(0.42f, 0.75f, 0.9f, 1f);
        private static readonly Color AccentColour = new Color(1f, 0.55f, 0.18f, 1f);

        private static Dictionary<Material, Material> _materialCopies;
        private static Dictionary<Mesh, Mesh> _meshCopies;

        [MenuItem("Tools/SabaProps/Water/Create Feature Gallery", false, 2)]
        public static void CreateAndOpen()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = Create();
            if (!scene.IsValid())
            {
                return;
            }

            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                view.LookAt(new Vector3(0f, 0f, 27f), Quaternion.Euler(48f, 0f, 0f), 72f);
            }
        }

        /// <summary>
        /// Replaces the current scene with the feature gallery and writes it to
        /// <see cref="ScenePath"/>. Prompt-free for tests and distribution builds.
        /// </summary>
        public static Scene Create()
        {
            WaterAssetLibrary.CreateOrLoadDefaults();
            EnsureSampleFolders();
            _materialCopies = new Dictionary<Material, Material>();
            _meshCopies = new Dictionary<Mesh, Mesh>();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var gallery = new GameObject("SabaProps Water Feature Gallery");

            ConfigureEnvironment();
            Material ground = CreateColourMaterial("Gallery Ground", GroundColour, 0f, 0.25f);
            Material platform = CreateColourMaterial("Exhibit Platform", PlatformColour, 0f, 0.4f);
            Material liteAccent = CreateColourMaterial("Lite Accent", LiteColour, 0.05f, 0.55f);
            Material standardAccent = CreateColourMaterial("Standard Accent", StandardColour, 0.15f, 0.75f);
            Material warmAccent = CreateColourMaterial("Warm Accent", AccentColour, 0.05f, 0.55f);

            BuildGround(gallery.transform, ground);
            BuildSurfaceSection(gallery.transform, platform, liteAccent, standardAccent);
            BuildRainSection(gallery.transform, platform, warmAccent);
            BuildAtmosphereSection(gallery.transform, platform, liteAccent, standardAccent);
            BuildUnderwaterSection(gallery.transform, platform, liteAccent, standardAccent, warmAccent);
            CreateOverviewCamera(gallery.transform);
            CreateSun(gallery.transform);

            PersistSceneReferences(gallery);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = gallery;

            Debug.Log(
                "[SabaProps Water] Feature Gallery created at " + ScenePath +
                ". Press Play to animate rain, fog and clouds.");
            return scene;
        }

        /// <summary>Entry point used to build the committed UPM sample.</summary>
        public static void CreateDistributionArtifacts()
        {
            Scene scene = Create();
            if (!scene.IsValid())
            {
                throw new InvalidOperationException("Water Feature Gallery could not be created.");
            }

            CaptureDocumentationImages();
        }

        public static void CaptureDocumentationImages()
        {
            WaterAssetLibrary.EnsureFolder(CapturesFolder);
            SimulateParticles(6f);
            CaptureCamera(OverviewCameraName, OverviewCapturePath, 1600, 900);
            CaptureCamera(UnderwaterCameraName, UnderwaterCapturePath, 1600, 900);
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Validates the currently open gallery after generation or UPM sample
        /// import. This method is also a command-line entry point for release QA.
        /// </summary>
        public static void ValidateOpenGallery()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                throw new InvalidOperationException("No saved Water Feature Gallery scene is open.");
            }

            string[] requiredObjects =
            {
                "SabaProps Water Feature Gallery",
                SurfaceRootName,
                RainRootName,
                AtmosphereRootName,
                UnderwaterRootName,
                OverviewCameraName,
                UnderwaterCameraName,
            };
            foreach (string objectName in requiredObjects)
            {
                if (GameObject.Find(objectName) == null)
                {
                    throw new InvalidOperationException("Gallery object is missing: " + objectName);
                }
            }

            foreach (Renderer renderer in UnityEngine.Object.FindObjectsOfType<Renderer>())
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null)
                    {
                        throw new InvalidOperationException(
                            "Gallery renderer has a missing Material: " + renderer.name);
                    }
                }
            }

            foreach (MeshFilter filter in UnityEngine.Object.FindObjectsOfType<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                {
                    throw new InvalidOperationException(
                        "Gallery MeshFilter has a missing Mesh: " + filter.name);
                }
            }

            foreach (WaterPath path in UnityEngine.Object.FindObjectsOfType<WaterPath>())
            {
                if (path.profile == null || path.profile.material == null || path.generatedMesh == null)
                {
                    throw new InvalidOperationException(
                        "Gallery river has an incomplete authoring reference: " + path.name);
                }
            }

            Debug.Log("[SabaProps Water] Feature Gallery validation passed: " + scene.path);
        }

        /// <summary>
        /// Opens and validates the scene named by SABAPROPS_WATER_GALLERY_SCENE.
        /// Intended for release automation that imports Samples~ into Assets.
        /// </summary>
        public static void ValidateGalleryFromEnvironment()
        {
            string scenePath = Environment.GetEnvironmentVariable("SABAPROPS_WATER_GALLERY_SCENE");
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                throw new InvalidOperationException(
                    "SABAPROPS_WATER_GALLERY_SCENE must name an imported gallery scene.");
            }

            EditorSceneManager.OpenScene(scenePath);
            ValidateOpenGallery();
        }

        private static void BuildSurfaceSection(
            Transform gallery,
            Material platform,
            Material liteAccent,
            Material standardAccent)
        {
            Transform root = CreateRoot(SurfaceRootName, gallery);
            CreateSectionLabel(root, "WATER SURFACES   LITE / STANDARD", new Vector3(0f, 0.5f, -8f), 0.24f);

            WaterBodyKind[] bodyKinds =
            {
                WaterBodyKind.Puddle,
                WaterBodyKind.River,
                WaterBodyKind.Lake,
                WaterBodyKind.Ocean,
            };

            for (int index = 0; index < bodyKinds.Length; index++)
            {
                float x = -27f + index * 18f;
                BuildSurfaceExhibit(root, bodyKinds[index], WaterQuality.Lite,
                    new Vector3(x, 0f, 0f), platform, liteAccent);
                BuildSurfaceExhibit(root, bodyKinds[index], WaterQuality.Standard,
                    new Vector3(x, 0f, 13f), platform, standardAccent);
            }
        }

        private static void BuildSurfaceExhibit(
            Transform parent,
            WaterBodyKind bodyKind,
            WaterQuality quality,
            Vector3 position,
            Material platform,
            Material accent)
        {
            var exhibit = new GameObject(bodyKind + " " + quality + " [Copy Ready]");
            exhibit.transform.SetParent(parent, false);
            exhibit.transform.localPosition = position;

            CreatePlatform(exhibit.transform, new Vector3(15f, 0.4f, 10.5f), platform);
            CreateTrim(exhibit.transform, accent);
            AddComparisonMarkers(exhibit.transform, accent);

            WaterSurfaceProfile profile = CreateSampleProfile(bodyKind, quality);
            Mesh mesh;
            switch (bodyKind)
            {
                case WaterBodyKind.Puddle:
                    mesh = WaterMeshBuilder.BuildPuddle(3.1f, 1.35f, 5, 40, 120 + (int)quality, 0.2f);
                    break;
                case WaterBodyKind.River:
                    mesh = WaterMeshBuilder.BuildRiver(
                        RiverPoints(), 3.8f, 6, 1.8f);
                    break;
                case WaterBodyKind.Ocean:
                    mesh = WaterMeshBuilder.BuildGrid(13.5f, 8.5f, 28, 18);
                    break;
                case WaterBodyKind.Lake:
                default:
                    mesh = WaterMeshBuilder.BuildGrid(13.5f, 8.5f, 18, 12);
                    break;
            }

            mesh = SaveMesh(mesh, bodyKind + "_" + quality);
            GameObject surface = CreateMeshDisplay(
                "Water Surface", exhibit.transform, Vector3.up * 0.08f,
                mesh, profile != null ? profile.material : null);

            if (bodyKind == WaterBodyKind.River)
            {
                WaterPath path = surface.AddComponent<WaterPath>();
                path.controlPoints = RiverPoints();
                path.width = 3.8f;
                path.subdivisions = 6;
                path.uvMetersPerTile = 1.8f;
                path.profile = profile;
                path.generatedMesh = mesh;
            }

            CreateLabel(exhibit.transform, bodyKind + "  " + quality,
                new Vector3(0f, 0.55f, -4.75f), 0.13f, Color.white);
        }

        private static void BuildRainSection(Transform gallery, Material platform, Material accent)
        {
            Transform root = CreateRoot(RainRootName, gallery);
            root.localPosition = new Vector3(-20f, 0f, 31f);
            CreateSectionLabel(root, "RAIN / SPLASH / RIPPLE", new Vector3(0f, 0.6f, -9f), 0.22f);
            CreatePlatform(root, new Vector3(24f, 0.5f, 16f), platform);
            CreateTrim(root, accent, new Vector3(23.5f, 0.12f, 15.5f));

            WaterSurfaceProfile puddleProfile = CreateSampleProfile(WaterBodyKind.Puddle, WaterQuality.Standard);
            Mesh puddle = SaveMesh(
                WaterMeshBuilder.BuildPuddle(5f, 1.3f, 5, 48, 501, 0.16f),
                "RainDemo_Puddle");
            CreateMeshDisplay("Rain Ripple Puddle", root, Vector3.up * 0.1f, puddle, puddleProfile.material);

            GameObject rainRig = WaterRigFactory.CreateRainRig(root.gameObject);
            rainRig.name = "Rain Rig [Copy Ready]";
            ParticleSystem rain = rainRig.transform.Find("Rain").GetComponent<ParticleSystem>();
            rain.transform.localPosition = Vector3.up * 10f;
            ParticleSystem.ShapeModule shape = rain.shape;
            shape.scale = new Vector3(18f, 1f, 13f);
            ParticleSystem.EmissionModule emission = rain.emission;
            emission.rateOverTime = 520f;

            CreateLabel(root, "PRESS PLAY TO SEE COLLISIONS", new Vector3(0f, 0.7f, -7.1f), 0.14f, Color.white);
        }

        private static void BuildAtmosphereSection(
            Transform gallery,
            Material platform,
            Material liteAccent,
            Material standardAccent)
        {
            Transform root = CreateRoot(AtmosphereRootName, gallery);
            root.localPosition = new Vector3(20f, 0f, 31f);
            CreateSectionLabel(root, "FOG / CLOUD / VOLUME", new Vector3(0f, 0.6f, -9f), 0.22f);
            CreatePlatform(root, new Vector3(30f, 0.5f, 16f), platform);

            var liteAnchor = new GameObject("Fog Volume Lite [Copy Ready]");
            liteAnchor.transform.SetParent(root, false);
            liteAnchor.transform.localPosition = new Vector3(-7f, 0f, 0f);
            GameObject lite = WaterRigFactory.CreateFogVolume(false, liteAnchor);
            lite.transform.localScale = new Vector3(9f, 3.5f, 9f);
            CreateTrim(liteAnchor.transform, liteAccent, new Vector3(9.5f, 0.12f, 9.5f));

            var highAnchor = new GameObject("Fog Volume High [Copy Ready]");
            highAnchor.transform.SetParent(root, false);
            highAnchor.transform.localPosition = new Vector3(7f, 0f, 0f);
            GameObject high = WaterRigFactory.CreateFogVolume(true, highAnchor);
            high.transform.localScale = new Vector3(9f, 3.5f, 9f);
            CreateTrim(highAnchor.transform, standardAccent, new Vector3(9.5f, 0.12f, 9.5f));

            GameObject fog = WaterRigFactory.CreateFogParticles(false, root.gameObject);
            fog.name = "Ground Fog Particles [Copy Ready]";
            ParticleSystem.ShapeModule fogShape = fog.GetComponentInChildren<ParticleSystem>().shape;
            fogShape.scale = new Vector3(25f, 2f, 12f);

            GameObject clouds = WaterRigFactory.CreateFogParticles(true, root.gameObject);
            clouds.name = "Cloud Layer [Copy Ready]";
            clouds.transform.localPosition = Vector3.up * 22f;
            ParticleSystem.ShapeModule cloudShape = clouds.GetComponentInChildren<ParticleSystem>().shape;
            cloudShape.scale = new Vector3(65f, 5f, 45f);

            CreateLabel(root, "LITE", new Vector3(-7f, 0.7f, -7.1f), 0.14f, LiteColour);
            CreateLabel(root, "HIGH", new Vector3(7f, 0.7f, -7.1f), 0.14f, StandardColour);
        }

        private static void BuildUnderwaterSection(
            Transform gallery,
            Material platform,
            Material liteAccent,
            Material standardAccent,
            Material warmAccent)
        {
            Transform root = CreateRoot(UnderwaterRootName, gallery);
            root.localPosition = new Vector3(0f, 0f, 57f);
            CreateSectionLabel(root, "UNDERWATER / CAUSTICS / LIGHT SHAFTS",
                new Vector3(0f, 0.7f, -13f), 0.24f);

            BuildUnderwaterPool(root, false, new Vector3(-12f, 0f, 0f), platform, liteAccent, warmAccent);
            GameObject standardPool = BuildUnderwaterPool(
                root, true, new Vector3(12f, 0f, 0f), platform, standardAccent, warmAccent);

            Camera underwaterCamera = CreateCamera(
                UnderwaterCameraName,
                standardPool.transform,
                new Vector3(0f, -2.1f, -7.5f),
                new Vector3(0f, -2f, 3f),
                58f,
                false);
            underwaterCamera.nearClipPlane = 0.05f;
            underwaterCamera.farClipPlane = 60f;
        }

        private static GameObject BuildUnderwaterPool(
            Transform parent,
            bool standard,
            Vector3 position,
            Material platform,
            Material accent,
            Material warmAccent)
        {
            var pool = new GameObject((standard ? "Standard" : "Lite") + " Underwater Pool [Copy Ready]");
            pool.transform.SetParent(parent, false);
            pool.transform.localPosition = position;

            CreateBox("Pool Floor", pool.transform, new Vector3(0f, -5.15f, 0f),
                new Vector3(20f, 0.3f, 20f), platform);
            CreateBox("Pool Wall Left", pool.transform, new Vector3(-10.1f, -2.5f, 0f),
                new Vector3(0.3f, 5f, 20f), platform);
            CreateBox("Pool Wall Right", pool.transform, new Vector3(10.1f, -2.5f, 0f),
                new Vector3(0.3f, 5f, 20f), platform);
            CreateBox("Pool Wall Back", pool.transform, new Vector3(0f, -2.5f, 10.1f),
                new Vector3(20f, 5f, 0.3f), platform);
            CreateTrim(pool.transform, accent, new Vector3(20.5f, 0.12f, 20.5f));

            BuildSampleUnderwaterRig(pool.transform, standard);

            for (int index = 0; index < 5; index++)
            {
                float x = -6f + index * 3f;
                float height = 0.7f + (index % 3) * 0.55f;
                CreateBox("Refraction Marker " + (index + 1), pool.transform,
                    new Vector3(x, -4.75f + height * 0.5f, 3f + (index % 2) * 2f),
                    new Vector3(0.55f, height, 0.55f),
                    index % 2 == 0 ? warmAccent : accent);
            }

            CreateLabel(pool.transform, standard ? "STANDARD" : "LITE",
                new Vector3(0f, 0.7f, -9.3f), 0.16f, standard ? StandardColour : LiteColour);
            return pool;
        }

        private static void BuildSampleUnderwaterRig(Transform parent, bool standard)
        {
            var rig = new GameObject(
                (standard ? "Underwater Standard" : "Underwater Lite") + " [Copy Ready]");
            rig.transform.SetParent(parent, false);

            GameObject volume = GameObject.CreatePrimitive(PrimitiveType.Cube);
            volume.name = "Underwater Volume";
            volume.transform.SetParent(rig.transform, false);
            volume.transform.localPosition = Vector3.down * 2.5f;
            volume.transform.localScale = new Vector3(20f, 5f, 20f);
            UnityEngine.Object.DestroyImmediate(volume.GetComponent<Collider>());
            MeshRenderer volumeRenderer = volume.GetComponent<MeshRenderer>();
            volumeRenderer.sharedMaterial = PersistMaterial(WaterAssetLibrary.CreateOrLoadEnvironmentMaterial(
                standard
                    ? WaterAssetLibrary.UnderwaterStandardMaterialName
                    : WaterAssetLibrary.UnderwaterLiteMaterialName));
            ConfigureTransparentRenderer(volumeRenderer);

            WaterSurfaceProfile profile = CreateSampleProfile(
                WaterBodyKind.Lake,
                standard ? WaterQuality.Standard : WaterQuality.Lite);
            Mesh surface = SaveMesh(
                WaterMeshBuilder.BuildGrid(20f, 20f, 18, 18),
                standard ? "Underwater_Standard_Surface" : "Underwater_Lite_Surface");
            CreateMeshDisplay("Water Surface", rig.transform, Vector3.zero, surface, profile.material);

            Mesh causticsMesh = SaveMesh(
                WaterMeshBuilder.BuildGrid(19f, 19f, 1, 1),
                standard ? "Underwater_Standard_Caustics" : "Underwater_Lite_Caustics");
            GameObject caustics = CreateMeshDisplay(
                "Caustics Receiver Overlay",
                rig.transform,
                Vector3.down * 4.95f,
                causticsMesh,
                PersistMaterial(WaterAssetLibrary.CreateOrLoadEnvironmentMaterial(
                    WaterAssetLibrary.CausticsMaterialName)));
            ConfigureTransparentRenderer(caustics.GetComponent<MeshRenderer>());

            Mesh shaftMesh = SaveMesh(
                WaterMeshBuilder.BuildLightShaft(5f, 0.25f, 2.4f),
                standard ? "Underwater_Standard_LightShaft" : "Underwater_Lite_LightShaft");
            Material shaftMaterial = PersistMaterial(WaterAssetLibrary.CreateOrLoadEnvironmentMaterial(
                WaterAssetLibrary.LightShaftMaterialName));
            for (int index = 0; index < 3; index++)
            {
                GameObject shaft = CreateMeshDisplay(
                    "Light Shaft " + (index + 1),
                    rig.transform,
                    new Vector3((index - 1) * 4f, -0.03f, index % 2 == 0 ? -2f : 2f),
                    shaftMesh,
                    shaftMaterial);
                shaft.transform.localRotation = Quaternion.Euler(0f, index * 37f, 0f);
                ConfigureTransparentRenderer(shaft.GetComponent<MeshRenderer>());
            }
        }

        private static void BuildGround(Transform parent, Material material)
        {
            CreateBox("Gallery Ground", parent, new Vector3(0f, -0.55f, 35f),
                new Vector3(110f, 0.6f, 130f), material);
        }

        private static void ConfigureEnvironment()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.52f, 0.63f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.18f, 0.24f, 0.29f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.07f, 0.08f, 0.09f, 1f);
            RenderSettings.fog = false;
        }

        private static void CreateSun(Transform parent)
        {
            var sun = new GameObject("Gallery Sun", typeof(Light));
            sun.transform.SetParent(parent, false);
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            Light light = sun.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.94f, 0.82f, 1f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            RenderSettings.sun = light;
        }

        private static void CreateOverviewCamera(Transform parent)
        {
            Camera camera = CreateCamera(
                OverviewCameraName,
                parent,
                new Vector3(0f, 64f, -38f),
                new Vector3(0f, 0f, 29f),
                50f,
                true);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 220f;
        }

        private static Camera CreateCamera(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localTarget,
            float fieldOfView,
            bool enabled)
        {
            var cameraObject = new GameObject(name, typeof(Camera));
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.localPosition = localPosition;
            cameraObject.transform.localRotation = Quaternion.LookRotation(localTarget - localPosition, Vector3.up);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.fieldOfView = fieldOfView;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = GroundColour;
            camera.allowHDR = true;
            camera.enabled = enabled;
            if (enabled)
            {
                cameraObject.tag = "MainCamera";
            }

            return camera;
        }

        private static void CreatePlatform(Transform parent, Vector3 size, Material material)
        {
            CreateBox("Platform", parent, new Vector3(0f, -0.25f, 0f), size, material);
        }

        private static void CreateTrim(Transform parent, Material material)
        {
            CreateTrim(parent, material, new Vector3(14.5f, 0.12f, 10f));
        }

        private static void CreateTrim(Transform parent, Material material, Vector3 size)
        {
            const float thickness = 0.12f;
            const float height = 0.12f;
            float halfX = size.x * 0.5f;
            float halfZ = size.z * 0.5f;
            CreateBox("Feature Boundary Front", parent, new Vector3(0f, 0.01f, -halfZ),
                new Vector3(size.x, height, thickness), material);
            CreateBox("Feature Boundary Back", parent, new Vector3(0f, 0.01f, halfZ),
                new Vector3(size.x, height, thickness), material);
            CreateBox("Feature Boundary Left", parent, new Vector3(-halfX, 0.01f, 0f),
                new Vector3(thickness, height, size.z), material);
            CreateBox("Feature Boundary Right", parent, new Vector3(halfX, 0.01f, 0f),
                new Vector3(thickness, height, size.z), material);
        }

        private static void AddComparisonMarkers(Transform parent, Material material)
        {
            for (int index = 0; index < 4; index++)
            {
                CreateBox("Depth Marker " + (index + 1), parent,
                    new Vector3(-4.2f + index * 2.8f, -0.04f, 2.7f),
                    new Vector3(0.5f, 0.14f, 1.8f + index * 0.45f), material);
            }
        }

        private static GameObject CreateBox(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = localScale;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            return box;
        }

        private static GameObject CreateMeshDisplay(
            string name,
            Transform parent,
            Vector3 localPosition,
            Mesh mesh,
            Material material)
        {
            var display = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            display.transform.SetParent(parent, false);
            display.transform.localPosition = localPosition;
            display.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = display.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return display;
        }

        private static void ConfigureTransparentRenderer(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static Transform CreateRoot(string name, Transform parent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            return root.transform;
        }

        private static void CreateSectionLabel(Transform parent, string text, Vector3 position, float size)
        {
            CreateLabel(parent, text, position, size, new Color(0.88f, 0.94f, 1f, 1f));
        }

        private static void CreateLabel(
            Transform parent,
            string text,
            Vector3 localPosition,
            float size,
            Color colour)
        {
            var label = new GameObject("Label - " + text, typeof(TextMesh));
            label.transform.SetParent(parent, false);
            label.transform.localPosition = localPosition;
            label.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
            TextMesh mesh = label.GetComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.characterSize = size;
            mesh.fontSize = 48;
            mesh.color = colour;
        }

        private static List<Vector3> RiverPoints()
        {
            return new List<Vector3>
            {
                new Vector3(-2.8f, 0f, -4f),
                new Vector3(-1.1f, 0f, -1.5f),
                new Vector3(1.8f, 0f, 1.2f),
                new Vector3(0.5f, 0f, 4f),
            };
        }

        private static void EnsureSampleFolders()
        {
            WaterAssetLibrary.EnsureFolder(SampleFolder);
            WaterAssetLibrary.EnsureFolder(MaterialsFolder);
            WaterAssetLibrary.EnsureFolder(MeshesFolder);
            WaterAssetLibrary.EnsureFolder(ProfilesFolder);
            WaterAssetLibrary.EnsureFolder(CapturesFolder);
        }

        private static WaterSurfaceProfile CreateSampleProfile(WaterBodyKind bodyKind, WaterQuality quality)
        {
            WaterSurfaceProfile source = WaterAssetLibrary.CreateOrLoadProfile(bodyKind, quality);
            if (source == null)
            {
                return null;
            }

            string path = ProfilesFolder + "/" + bodyKind + "_" + quality + ".asset";
            WaterSurfaceProfile target = AssetDatabase.LoadAssetAtPath<WaterSurfaceProfile>(path);
            if (target == null)
            {
                target = ScriptableObject.CreateInstance<WaterSurfaceProfile>();
                AssetDatabase.CreateAsset(target, path);
            }

            EditorUtility.CopySerialized(source, target);
            target.name = bodyKind + "_" + quality;
            target.material = PersistMaterial(source.material);
            target.ApplyToMaterial();
            EditorUtility.SetDirty(target);
            return target;
        }

        private static Material CreateColourMaterial(
            string name,
            Color colour,
            float metallic,
            float smoothness)
        {
            string path = MaterialsFolder + "/" + Sanitize(name) + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (shader != null)
            {
                material.shader = shader;
            }

            material.SetColor("_Color", colour);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            _materialCopies[material] = material;
            return material;
        }

        private static Material PersistMaterial(Material source)
        {
            if (source == null)
            {
                return null;
            }

            if (_materialCopies.TryGetValue(source, out Material cached))
            {
                return cached;
            }

            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (sourcePath.StartsWith(SampleFolder + "/", StringComparison.Ordinal))
            {
                _materialCopies[source] = source;
                return source;
            }

            string path = MaterialsFolder + "/" + Sanitize(source.name) + ".mat";
            Material target = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (target == null)
            {
                target = new Material(source) { name = source.name };
                AssetDatabase.CreateAsset(target, path);
            }
            else
            {
                EditorUtility.CopySerialized(source, target);
                target.name = source.name;
                EditorUtility.SetDirty(target);
            }

            _materialCopies[source] = target;
            _materialCopies[target] = target;
            return target;
        }

        private static Mesh SaveMesh(Mesh source, string name)
        {
            if (source == null)
            {
                return null;
            }

            string path = MeshesFolder + "/" + Sanitize(name) + ".asset";
            Mesh target = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (target == null)
            {
                source.name = name;
                AssetDatabase.CreateAsset(source, path);
                target = source;
            }
            else
            {
                EditorUtility.CopySerialized(source, target);
                UnityEngine.Object.DestroyImmediate(source);
                target.name = name;
                EditorUtility.SetDirty(target);
            }

            _meshCopies[target] = target;
            return target;
        }

        private static Mesh PersistMesh(Mesh source)
        {
            if (source == null)
            {
                return null;
            }

            if (_meshCopies.TryGetValue(source, out Mesh cached))
            {
                return cached;
            }

            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (sourcePath.StartsWith(SampleFolder + "/", StringComparison.Ordinal))
            {
                _meshCopies[source] = source;
                return source;
            }

            if (!sourcePath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return source;
            }

            string name = Path.GetFileNameWithoutExtension(sourcePath);
            string path = MeshesFolder + "/" + Sanitize(name) + ".asset";
            Mesh target = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (target == null)
            {
                target = UnityEngine.Object.Instantiate(source);
                target.name = name;
                AssetDatabase.CreateAsset(target, path);
            }
            else
            {
                EditorUtility.CopySerialized(source, target);
                target.name = name;
                EditorUtility.SetDirty(target);
            }

            _meshCopies[source] = target;
            _meshCopies[target] = target;
            return target;
        }

        private static void PersistSceneReferences(GameObject gallery)
        {
            foreach (Renderer renderer in gallery.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                {
                    materials[index] = PersistMaterial(materials[index]);
                }

                renderer.sharedMaterials = materials;
                if (renderer is ParticleSystemRenderer particles)
                {
                    particles.mesh = PersistMesh(particles.mesh);
                }
            }

            foreach (MeshFilter filter in gallery.GetComponentsInChildren<MeshFilter>(true))
            {
                filter.sharedMesh = PersistMesh(filter.sharedMesh);
            }
        }

        private static void SimulateParticles(float seconds)
        {
            GameObject gallery = GameObject.Find("SabaProps Water Feature Gallery");
            if (gallery == null)
            {
                return;
            }

            foreach (ParticleSystem particles in gallery.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.Simulate(seconds, true, true, true);
            }
        }

        private static void CaptureCamera(
            string cameraName,
            string assetPath,
            int width,
            int height)
        {
            GameObject cameraObject = GameObject.Find(cameraName);
            Camera camera = cameraObject != null ? cameraObject.GetComponent<Camera>() : null;
            if (camera == null)
            {
                throw new InvalidOperationException("Camera not found: " + cameraName);
            }

            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4,
            };
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;

            try
            {
                camera.targetTexture = target;
                RenderTexture.active = target;
                camera.Render();
                image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                image.Apply();

                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string absolutePath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
                File.WriteAllBytes(absolutePath, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static string Sanitize(string name)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            return string.IsNullOrWhiteSpace(name) ? "WaterSampleAsset" : name.Replace(' ', '_');
        }
    }
}
