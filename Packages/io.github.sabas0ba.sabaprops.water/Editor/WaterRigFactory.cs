using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SabaProps.Water.Editors
{
    /// <summary>Builds VRChat-safe rigs from standard Unity components.</summary>
    public static class WaterRigFactory
    {
        public static GameObject CreateSurface(
            WaterBodyKind bodyKind,
            WaterQuality quality,
            GameObject parent = null)
        {
            WaterSurfaceProfile profile = WaterAssetLibrary.CreateOrLoadProfile(bodyKind, quality);
            Vector3 position = PlacementPosition(parent);

            if (bodyKind == WaterBodyKind.River)
            {
                var river = new GameObject("River " + quality, typeof(MeshFilter), typeof(MeshRenderer));
                ParentAndPosition(river, parent, position);
                var path = river.AddComponent<WaterPath>();
                path.profile = profile;
                path.controlPoints[0] = new Vector3(0f, 0f, -5f);
                path.controlPoints[1] = Vector3.zero;
                path.controlPoints[2] = new Vector3(1f, 0f, 5f);
                path.ApplyProfile();
                WaterPathEditor.Rebuild(path);
                Undo.RegisterCreatedObjectUndo(river, "Create River");
                Selection.activeGameObject = river;
                return river;
            }

            Mesh mesh;
            float boundsPadding;
            switch (bodyKind)
            {
                case WaterBodyKind.Puddle:
                    mesh = WaterMeshBuilder.BuildPuddle(1.25f, 1.3f, 4, 24, 1);
                    boundsPadding = 0.05f;
                    break;
                case WaterBodyKind.Ocean:
                    mesh = WaterMeshBuilder.BuildGrid(100f, 100f, 32, 32);
                    boundsPadding = 0.5f;
                    break;
                case WaterBodyKind.Lake:
                default:
                    mesh = WaterMeshBuilder.BuildGrid(20f, 20f, 16, 16);
                    boundsPadding = 0.2f;
                    break;
            }

            mesh = WaterAssetLibrary.WriteUniqueMesh(
                mesh,
                bodyKind == WaterBodyKind.Puddle
                    ? WaterAssetLibrary.GeneratedPuddlesFolder
                    : WaterAssetLibrary.GeneratedSurfacesFolder,
                bodyKind + "_" + quality);
            ExpandVerticalBounds(mesh, boundsPadding);

            GameObject surface = CreateMeshObject(
                bodyKind + " " + quality,
                mesh,
                profile != null ? profile.material : null,
                parent,
                position);
            GameObjectUtility.SetStaticEditorFlags(surface, StaticEditorFlags.BatchingStatic);
            Undo.RegisterCreatedObjectUndo(surface, "Create Water Surface");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = surface;
            return surface;
        }

        public static GameObject CreateRainRig(GameObject parent = null)
        {
            WaterAssetLibrary.CreateOrLoadDefaults();
            var root = new GameObject("Rain Rig");
            ParentAndPosition(root, parent, PlacementPosition(parent));

            ParticleSystem rain = CreateParticleSystem("Rain", root.transform);
            rain.transform.localPosition = Vector3.up * 12f;
            ConfigureRain(rain);

            ParticleSystem splash = CreateParticleSystem("Collision Splash", rain.transform);
            ConfigureSplash(splash);

            ParticleSystem ripple = CreateParticleSystem("Collision Ripple", rain.transform);
            ConfigureRipple(ripple);

            ParticleSystem.SubEmittersModule subEmitters = rain.subEmitters;
            subEmitters.enabled = true;
            subEmitters.AddSubEmitter(
                splash,
                ParticleSystemSubEmitterType.Collision,
                ParticleSystemSubEmitterProperties.InheritNothing);
            subEmitters.AddSubEmitter(
                ripple,
                ParticleSystemSubEmitterType.Collision,
                ParticleSystemSubEmitterProperties.InheritNothing);

            Undo.RegisterCreatedObjectUndo(root, "Create Rain Rig");
            Selection.activeGameObject = root;
            return root;
        }

        public static GameObject CreateFogVolume(bool highQuality, GameObject parent = null)
        {
            WaterAssetLibrary.CreateOrLoadDefaults();
            GameObject volume = GameObject.CreatePrimitive(PrimitiveType.Cube);
            volume.name = highQuality ? "Fog Volume High" : "Fog Volume Lite";
            Object.DestroyImmediate(volume.GetComponent<Collider>());
            ParentAndPosition(volume, parent, PlacementPosition(parent) + Vector3.up * 1.5f);
            volume.transform.localScale = new Vector3(12f, 3f, 12f);

            string materialName = highQuality
                ? WaterAssetLibrary.FogVolumeHighMaterialName
                : WaterAssetLibrary.FogVolumeLiteMaterialName;
            MeshRenderer renderer = volume.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = WaterAssetLibrary.CreateOrLoadEnvironmentMaterial(materialName);
            ConfigureTransparentRenderer(renderer);

            Undo.RegisterCreatedObjectUndo(volume, "Create Fog Volume");
            Selection.activeGameObject = volume;
            return volume;
        }

        public static GameObject CreateFogParticles(bool clouds, GameObject parent = null)
        {
            WaterAssetLibrary.CreateOrLoadDefaults();
            var root = new GameObject(clouds ? "Cloud Layer" : "Ground Fog Particles");
            Vector3 position = PlacementPosition(parent) + Vector3.up * (clouds ? 30f : 1.5f);
            ParentAndPosition(root, parent, position);

            ParticleSystem particles = CreateParticleSystem(clouds ? "Clouds" : "Fog", root.transform);
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = clouds
                ? new ParticleSystem.MinMaxCurve(35f, 55f)
                : new ParticleSystem.MinMaxCurve(8f, 16f);
            main.startSpeed = clouds
                ? new ParticleSystem.MinMaxCurve(0.4f, 0.9f)
                : new ParticleSystem.MinMaxCurve(0.05f, 0.22f);
            main.startSize = clouds
                ? new ParticleSystem.MinMaxCurve(28f, 55f)
                : new ParticleSystem.MinMaxCurve(4f, 10f);
            main.startColor = clouds
                ? new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.5f))
                : new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.38f));
            main.maxParticles = clouds ? 96 : 256;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = clouds ? 1.4f : 8f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = clouds
                ? new Vector3(120f, 8f, 120f)
                : new Vector3(24f, 2.5f, 24f);

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            noise.strength = clouds ? 2.2f : 0.8f;
            noise.frequency = clouds ? 0.05f : 0.18f;
            noise.scrollSpeed = clouds ? 0.03f : 0.08f;
            noise.damping = true;

            ApplyFadeOut(particles, 0.25f);

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = WaterAssetLibrary.CreateOrLoadEnvironmentMaterial(
                clouds ? WaterAssetLibrary.CloudMaterialName : WaterAssetLibrary.FogParticleMaterialName);
            ConfigureTransparentRenderer(renderer);

            Undo.RegisterCreatedObjectUndo(root, clouds ? "Create Cloud Layer" : "Create Ground Fog");
            Selection.activeGameObject = root;
            return root;
        }

        public static GameObject CreateUnderwaterRig(bool standard, GameObject parent = null)
        {
            WaterAssetLibrary.CreateOrLoadDefaults();
            Vector3 surfacePosition = PlacementPosition(parent);

            var root = new GameObject(standard ? "Underwater Lake Standard" : "Underwater Lake Lite");
            ParentAndPosition(root, parent, surfacePosition);

            GameObject volume = GameObject.CreatePrimitive(PrimitiveType.Cube);
            volume.name = "Underwater Volume";
            Object.DestroyImmediate(volume.GetComponent<Collider>());
            volume.transform.SetParent(root.transform, false);
            volume.transform.localPosition = Vector3.down * 2.5f;
            volume.transform.localScale = new Vector3(20f, 5f, 20f);
            MeshRenderer volumeRenderer = volume.GetComponent<MeshRenderer>();
            volumeRenderer.sharedMaterial = WaterAssetLibrary.CreateOrLoadEnvironmentMaterial(
                standard
                    ? WaterAssetLibrary.UnderwaterStandardMaterialName
                    : WaterAssetLibrary.UnderwaterLiteMaterialName);
            ConfigureTransparentRenderer(volumeRenderer);

            WaterSurfaceProfile surfaceProfile = WaterAssetLibrary.CreateOrLoadProfile(
                WaterBodyKind.Lake,
                standard ? WaterQuality.Standard : WaterQuality.Lite);
            Mesh surfaceMesh = WaterAssetLibrary.WriteUniqueMesh(
                WaterMeshBuilder.BuildGrid(20f, 20f, 16, 16),
                WaterAssetLibrary.GeneratedSurfacesFolder,
                standard ? "UnderwaterLake_Standard" : "UnderwaterLake_Lite");
            ExpandVerticalBounds(surfaceMesh, 0.2f);
            CreateMeshObject(
                "Water Surface",
                surfaceMesh,
                surfaceProfile != null ? surfaceProfile.material : null,
                root,
                surfacePosition);

            Mesh causticsMesh = WaterAssetLibrary.WriteUniqueMesh(
                WaterMeshBuilder.BuildGrid(19f, 19f, 1, 1),
                WaterAssetLibrary.GeneratedSurfacesFolder,
                "UnderwaterCaustics");
            GameObject caustics = CreateMeshObject(
                "Caustics Receiver Overlay",
                causticsMesh,
                WaterAssetLibrary.CreateOrLoadEnvironmentMaterial(WaterAssetLibrary.CausticsMaterialName),
                root,
                surfacePosition + Vector3.down * 4.95f);
            ConfigureTransparentRenderer(caustics.GetComponent<MeshRenderer>());

            Mesh shaftMesh = WaterAssetLibrary.CreateOrLoadLightShaft();
            Material shaftMaterial = WaterAssetLibrary.CreateOrLoadEnvironmentMaterial(
                WaterAssetLibrary.LightShaftMaterialName);
            for (int index = 0; index < 3; index++)
            {
                Vector3 offset = new Vector3((index - 1) * 4f, -0.03f, index % 2 == 0 ? -2f : 2f);
                GameObject shaft = CreateMeshObject(
                    "Light Shaft " + (index + 1),
                    shaftMesh,
                    shaftMaterial,
                    root,
                    surfacePosition + offset);
                shaft.transform.localRotation = Quaternion.Euler(0f, index * 37f, 0f);
                ConfigureTransparentRenderer(shaft.GetComponent<MeshRenderer>());
            }

            Undo.RegisterCreatedObjectUndo(root, "Create Underwater Rig");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            return root;
        }

        private static void ConfigureRain(ParticleSystem rain)
        {
            ParticleSystem.MainModule main = rain.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.75f, 1.15f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.035f);
            main.maxParticles = 6000;

            ParticleSystem.EmissionModule emission = rain.emission;
            emission.enabled = true;
            emission.rateOverTime = 850f;

            ParticleSystem.ShapeModule shape = rain.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(22f, 1f, 22f);

            ParticleSystem.VelocityOverLifetimeModule velocity = rain.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
            velocity.y = new ParticleSystem.MinMaxCurve(-19f, -16f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

            ParticleSystem.CollisionModule collision = rain.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            collision.collidesWith = ~0;
            collision.dampen = 0.12f;
            collision.bounce = 0.08f;
            collision.lifetimeLoss = 1f;
            collision.radiusScale = 0.25f;
            collision.maxCollisionShapes = 128;

            ParticleSystemRenderer renderer = rain.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.4f;
            renderer.velocityScale = 0.06f;
            renderer.cameraVelocityScale = 0f;
            renderer.sharedMaterial = WaterAssetLibrary.CreateOrLoadEnvironmentMaterial(
                WaterAssetLibrary.RainMaterialName);
            ConfigureTransparentRenderer(renderer);
        }

        private static void ConfigureSplash(ParticleSystem splash)
        {
            ParticleSystem.MainModule main = splash.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.34f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 2.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.07f);
            main.gravityModifier = 1.2f;
            main.maxParticles = 1600;

            ParticleSystem.EmissionModule emission = splash.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 3, 6) });

            ParticleSystem.ShapeModule shape = splash.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.025f;

            ApplyFadeOut(splash, 0.05f);
            ParticleSystemRenderer renderer = splash.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = WaterAssetLibrary.CreateOrLoadEnvironmentMaterial(
                WaterAssetLibrary.SplashMaterialName);
            ConfigureTransparentRenderer(renderer);
        }

        private static void ConfigureRipple(ParticleSystem ripple)
        {
            ParticleSystem.MainModule main = ripple.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.9f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
            main.startColor = Color.white;
            main.maxParticles = 1200;

            ParticleSystem.EmissionModule emission = ripple.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1, 1) });

            ApplyFadeOut(ripple, 0f);
            ParticleSystemRenderer renderer = ripple.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = WaterAssetLibrary.CreateOrLoadHorizontalQuad();
            renderer.sharedMaterial = WaterAssetLibrary.CreateOrLoadEnvironmentMaterial(
                WaterAssetLibrary.RippleMaterialName);
            ConfigureTransparentRenderer(renderer);
        }

        private static ParticleSystem CreateParticleSystem(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(ParticleSystem));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<ParticleSystem>();
        }

        private static void ApplyFadeOut(ParticleSystem particles, float fadeInFraction)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(fadeInFraction > 0f ? 0f : 1f, 0f),
                    new GradientAlphaKey(1f, fadeInFraction),
                    new GradientAlphaKey(0f, 1f),
                });
            ParticleSystem.ColorOverLifetimeModule colour = particles.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static GameObject CreateMeshObject(
            string name,
            Mesh mesh,
            Material material,
            GameObject parent,
            Vector3 worldPosition)
        {
            var gameObject = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            ParentAndPosition(gameObject, parent, worldPosition);
            gameObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return gameObject;
        }

        private static void ConfigureTransparentRenderer(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static void ParentAndPosition(
            GameObject gameObject,
            GameObject parent,
            Vector3 worldPosition)
        {
            if (parent != null)
            {
                gameObject.transform.SetParent(parent.transform, true);
            }

            gameObject.transform.position = worldPosition;
        }

        private static Vector3 PlacementPosition(GameObject parent)
        {
            if (parent != null)
            {
                return parent.transform.position;
            }

            SceneView sceneView = SceneView.lastActiveSceneView;
            return sceneView != null ? sceneView.pivot : Vector3.zero;
        }

        private static void ExpandVerticalBounds(Mesh mesh, float padding)
        {
            if (mesh == null || padding <= 0f)
            {
                return;
            }

            Bounds bounds = mesh.bounds;
            bounds.Expand(new Vector3(0f, padding * 2f, 0f));
            mesh.bounds = bounds;
            EditorUtility.SetDirty(mesh);
        }
    }
}
