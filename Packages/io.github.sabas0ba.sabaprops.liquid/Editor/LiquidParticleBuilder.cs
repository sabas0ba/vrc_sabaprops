using UnityEditor;
using UnityEngine;

namespace SabaProps.Liquid.Editors
{
    /// <summary>
    /// Builds the visual particles for liquids: streams whose look follows
    /// the liquid's viscosity, splashes where they land, and steam.
    /// <para>
    /// Visual only. Where liquid lands on a body is decided by the Source's
    /// rays; the particles never touch the canvas, because Udon cannot read
    /// where a particle hit.
    /// </para>
    /// <list type="bullet">
    /// <item>Thin liquids (viscosity below 0.3): small fast drops drawn stretched
    /// along their motion, bursting into a spray of fine droplets on impact.</item>
    /// <item>Medium liquids (paint): larger, less stretched drops with a smaller
    /// splash.</item>
    /// <item>Thick liquids (0.7 and up): heavy blobs. Glossy ones (slime, syrup)
    /// trail strings behind them; matte ones (mud) land as a few dull splats.</item>
    /// </list>
    /// </summary>
    public static class LiquidParticleBuilder
    {
        public const float ThinViscosity = 0.3f;
        public const float ThickViscosity = 0.7f;

        /// <summary>
        /// A stream along the parent's +Z in the look of <paramref name="profile"/>.
        /// With <paramref name="rate"/> 0 it only emits when a Source calls Emit.
        /// Materials go to <paramref name="materialFolder"/>, so prefabs can keep
        /// theirs inside the package.
        /// </summary>
        public static ParticleSystem CreateLiquidStream(Transform parent, LiquidProfile profile, string materialFolder,
            float speed, float lifetime, float coneAngle, float rate)
        {
            float viscosity = profile != null ? profile.viscosity : 0.1f;
            bool thick = viscosity >= ThickViscosity;
            bool thin = viscosity < ThinViscosity;
            bool stringy = thick && profile != null && profile.smoothness >= 0.9f;
            Color colour = DropletColour(profile);

            var streamObject = new GameObject("Stream");
            streamObject.transform.SetParent(parent, false);
            var stream = streamObject.AddComponent<ParticleSystem>();
            stream.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = stream.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = lifetime;
            main.startSpeed = thick ? new ParticleSystem.MinMaxCurve(speed * 0.8f, speed) : new ParticleSystem.MinMaxCurve(speed * 0.9f, speed * 1.05f);
            main.startSize = thin
                ? new ParticleSystem.MinMaxCurve(0.01f, 0.024f)
                : thick ? new ParticleSystem.MinMaxCurve(0.04f, 0.08f) : new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
            main.startColor = colour;
            main.gravityModifier = thick ? 1.1f : 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1500;

            ParticleSystem.EmissionModule emission = stream.emission;
            emission.rateOverTime = rate;

            ParticleSystem.ShapeModule shape = stream.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = coneAngle;
            shape.radius = 0.01f;

            ParticleSystem.CollisionModule collision = stream.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.bounce = 0f;
            collision.lifetimeLoss = 1f;
            collision.quality = ParticleSystemCollisionQuality.Medium;

            var renderer = streamObject.GetComponent<ParticleSystemRenderer>();
            Material material = LiquidAssets.StreamMaterial(materialFolder + "/LiquidStream.mat", materialFolder);
            renderer.sharedMaterial = material;
            if (!thick)
            {
                // Drops in flight read as short streaks, the faster the longer.
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = thin ? 0.03f : 0.015f;
                renderer.lengthScale = thin ? 1.6f : 1.2f;
            }

            if (stringy)
            {
                ParticleSystem.TrailModule trails = stream.trails;
                trails.enabled = true;
                trails.mode = ParticleSystemTrailMode.PerParticle;
                trails.lifetime = new ParticleSystem.MinMaxCurve(0.12f);
                trails.minVertexDistance = 0.02f;
                trails.inheritParticleColor = true;
                trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.25f));
                renderer.trailMaterial = material;
            }

            AddSplash(stream, colour, material, thin ? 6 : thick ? (stringy ? 0 : 3) : 4, thin, thick);
            return stream;
        }

        /// <summary>
        /// Rising steam over a floor of <paramref name="size"/>: large soft puffs
        /// that grow and fade as they climb. Stopped until a Source plays it.
        /// </summary>
        public static ParticleSystem CreateSteam(Transform parent, Vector2 size, string materialFolder)
        {
            var steamObject = new GameObject("Steam");
            steamObject.transform.SetParent(parent, false);
            steamObject.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var steam = steamObject.AddComponent<ParticleSystem>();
            steam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = steam.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.95f, 0.96f, 0.98f, 0.14f);
            main.gravityModifier = -0.01f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;

            ParticleSystem.EmissionModule emission = steam.emission;
            emission.rateOverTime = 8f * Mathf.Max(size.x * size.y, 1f);

            ParticleSystem.ShapeModule shape = steam.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(size.x, size.y, 0f);

            ParticleSystem.SizeOverLifetimeModule growth = steam.sizeOverLifetime;
            growth.enabled = true;
            growth.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));

            ParticleSystem.ColorOverLifetimeModule fade = steam.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystem.NoiseModule noise = steam.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.4f;
            noise.scrollSpeed = 0.2f;

            // Puffs stop at the ceiling and walls rather than rising through the roof.
            ParticleSystem.CollisionModule collision = steam.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.bounce = 0f;
            collision.dampen = 1f;
            collision.lifetimeLoss = 0.3f;
            collision.radiusScale = 0.5f;

            var renderer = steamObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = LiquidAssets.PuffMaterial(materialFolder + "/LiquidSteam.mat", materialFolder);
            renderer.sortingFudge = 10f;
            return steam;
        }

        /// <summary>
        /// Droplets burst from where the stream lands: fine spray for thin liquids,
        /// a few heavy splats for thick ones.
        /// </summary>
        private static void AddSplash(ParticleSystem stream, Color colour, Material material, int count, bool thin, bool thick)
        {
            if (count <= 0)
            {
                return;
            }

            var splashObject = new GameObject("Splash");
            splashObject.transform.SetParent(stream.transform, false);
            var splash = splashObject.AddComponent<ParticleSystem>();
            splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = splash.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, thin ? 0.45f : 0.3f);
            main.startSpeed = thick ? new ParticleSystem.MinMaxCurve(0.3f, 0.8f) : new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            main.startSize = thin
                ? new ParticleSystem.MinMaxCurve(0.005f, 0.012f)
                : thick ? new ParticleSystem.MinMaxCurve(0.03f, 0.05f) : new ParticleSystem.MinMaxCurve(0.01f, 0.02f);
            main.startColor = colour;
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1500;

            ParticleSystem.EmissionModule emission = splash.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            ParticleSystem.ShapeModule shape = splash.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.02f;

            var renderer = splashObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;

            ParticleSystem.SubEmittersModule sub = stream.subEmitters;
            sub.enabled = true;
            sub.AddSubEmitter(splash, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritColor);
        }

        /// <summary>The droplet colour: the pigment for coloured liquids, a pale blue tint for water.</summary>
        public static Color DropletColour(LiquidProfile profile)
        {
            if (profile == null || profile.pigmentAmount <= 0f)
            {
                return new Color(0.85f, 0.93f, 1f, 0.6f);
            }

            Color c = profile.pigmentColor;
            float alpha = Mathf.Lerp(0.55f, 0.95f, profile.pigmentAmount);
            return new Color(c.r, c.g, c.b, alpha);
        }
    }
}
