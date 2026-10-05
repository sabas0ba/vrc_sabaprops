using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Liquid.Editors
{
    /// <summary>
    /// Builds rain and snow areas: a <see cref="LiquidWeather"/> with a
    /// particle system covering its box, and the ground material that shows
    /// the ground getting wet or snowed on.
    /// </summary>
    public static class LiquidWeatherBuilder
    {
        public const string WeatherSurfaceShader = "SabaProps/Liquid/Weather Surface";
        public const string RainMaterialPath = LiquidAssets.MaterialFolder + "/LiquidRain.mat";
        public const string SnowMaterialPath = LiquidAssets.MaterialFolder + "/LiquidSnow.mat";

        [MenuItem("GameObject/SabaProps/Liquid/Rain Area", false, 25)]
        public static void CreateRainFromMenu(MenuCommand command)
        {
            LiquidCanvasPool pool = LiquidSourceBuilder.FindOrCreatePool();
            LiquidWeather weather = CreateWeather(null, "Rain Area", pool,
                LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.WaterName), false, new Vector3(10f, 6f, 10f));
            Place(weather.gameObject, command, "Create Liquid Rain Area");
        }

        [MenuItem("GameObject/SabaProps/Liquid/Snow Area", false, 26)]
        public static void CreateSnowFromMenu(MenuCommand command)
        {
            LiquidCanvasPool pool = LiquidSourceBuilder.FindOrCreatePool();
            LiquidWeather weather = CreateWeather(null, "Snow Area", pool, null, true, new Vector3(10f, 6f, 10f));
            Place(weather.gameObject, command, "Create Liquid Snow Area");
        }

        /// <summary>
        /// A weather area centred on the parent's origin, with its box
        /// <paramref name="areaSize"/> standing on the ground: the origin is the
        /// centre of the floor, and the box reaches up to <paramref name="areaSize"/>.y.
        /// </summary>
        public static LiquidWeather CreateWeather(Transform parent, string name, LiquidCanvasPool pool,
            LiquidProfile profile, bool snow, Vector3 areaSize)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);

            var area = new GameObject("Area");
            area.transform.SetParent(root.transform, false);
            area.transform.localPosition = new Vector3(0f, areaSize.y * 0.5f, 0f);

            ParticleSystem particles = CreatePrecipitation(root.transform, snow, areaSize);

            LiquidWeather weather = area.AddUdonSharpComponent<LiquidWeather>();
            weather.pool = pool;
            weather.profile = profile;
            weather.snow = snow;
            weather.precipitation = particles;
            weather.areaSize = areaSize;
            UdonSharpEditorUtility.CopyProxyToUdon(weather);
            EditorUtility.SetDirty(weather);
            return weather;
        }

        /// <summary>
        /// A material for ground under the sky that gets wet in rain and white in
        /// snow. Each weather area needs its own, since the area writes its state
        /// into the material.
        /// </summary>
        public static Material CreateOrLoadGroundMaterial(string assetPath, Color colour, float smoothness)
        {
            Material material = LiquidAssets.CreateOrLoadMaterial(assetPath, WeatherSurfaceShader);
            if (material == null)
            {
                return null;
            }

            material.SetColor("_Color", colour);
            material.SetFloat("_Glossiness", smoothness);
            material.SetVector("_WeatherState", Vector4.zero);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Falling rain or snow over the whole box, emitted from its top face.
        /// Visual only, like the other streams: bodies are hit by the area's rays.
        /// </summary>
        private static ParticleSystem CreatePrecipitation(Transform parent, bool snow, Vector3 areaSize)
        {
            var emitter = new GameObject(snow ? "Snowfall" : "Rainfall");
            emitter.transform.SetParent(parent, false);
            emitter.transform.localPosition = new Vector3(0f, areaSize.y, 0f);
            // The box shape emits along its local +Z; turn that to face down.
            emitter.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var particles = emitter.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            float speed = snow ? 1.1f : 9f;
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startSpeed = speed;
            main.startLifetime = areaSize.y / speed;
            main.startSize = snow ? new ParticleSystem.MinMaxCurve(0.03f, 0.06f) : new ParticleSystem.MinMaxCurve(0.012f);
            main.startColor = snow ? new Color(1f, 1f, 1f, 0.9f) : new Color(0.8f, 0.88f, 0.95f, 0.35f);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = snow ? 4000 : 3000;

            float area = areaSize.x * areaSize.z;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = (snow ? 5f : 12f) * area;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(areaSize.x, areaSize.z, 0f);

            if (snow)
            {
                // Flakes drift sideways as they fall.
                ParticleSystem.NoiseModule noise = particles.noise;
                noise.enabled = true;
                noise.strength = 0.4f;
                noise.frequency = 0.3f;
            }

            ParticleSystem.CollisionModule collision = particles.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.bounce = 0f;
            collision.lifetimeLoss = 1f;

            var renderer = emitter.GetComponent<ParticleSystemRenderer>();
            if (!snow)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.04f;
                renderer.lengthScale = 1f;
            }

            Material material = LiquidAssets.StreamMaterial(snow ? SnowMaterialPath : RainMaterialPath);
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            return particles;
        }

        private static void Place(GameObject root, MenuCommand command, string undoName)
        {
            GameObjectUtility.SetParentAndAlign(root, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(root, undoName);
            Selection.activeGameObject = root;
        }
    }
}
