using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace SabaProps.Liquid.Editors
{
    /// <summary>
    /// Builds liquid profiles and Sources, wired to the Canvas Pool in the scene.
    /// <para>
    /// Every Source needs a pool and a profile. The menu entries find the pool
    /// already in the scene, or create one, and reuse the profiles under a single
    /// "Liquid Profiles" object, so placing several Sources does not multiply
    /// either.
    /// </para>
    /// </summary>
    public static class LiquidSourceBuilder
    {
        public const string ProfilesName = "Liquid Profiles";
        public const string WaterName = "Water";
        public const string JuiceName = "Juice";
        public const string RedPaintName = "Red Paint";
        public const string BluePaintName = "Blue Paint";
        public const string MudName = "Mud";
        public const string SlimeName = "Slime";
        public const string SyrupName = "Syrup";
        public const string YellowPaintName = "Yellow Paint";
        public const string BlackPaintName = "Black Paint";
        public const string DarkGreyPaintName = "Dark Grey Paint";
        public const string GreyPaintName = "Grey Paint";
        public const string LightGreyPaintName = "Light Grey Paint";
        public const string WhitePaintName = "White Paint";

        /// <summary>Paint from black to white, for seeing how pigment lightness reads on a body.</summary>
        public static readonly string[] GreyscalePaintNames =
        {
            BlackPaintName, DarkGreyPaintName, GreyPaintName, LightGreyPaintName, WhitePaintName,
        };

        public const string StreamMaterialPath = LiquidAssets.MaterialFolder + "/LiquidStream.mat";

        [MenuItem("GameObject/SabaProps/Liquid/Pool Volume (Water)", false, 21)]
        public static void CreateWaterVolumeFromMenu(MenuCommand command)
        {
            LiquidCanvasPool pool = FindOrCreatePool();
            GameObject root = CreateImmersionVolume("Water Volume", pool, GetProfile(WaterName), new Vector3(4f, 1.2f, 4f));
            Place(root, command, "Create Liquid Pool Volume");
        }

        [MenuItem("GameObject/SabaProps/Liquid/Mud Volume", false, 22)]
        public static void CreateMudVolumeFromMenu(MenuCommand command)
        {
            LiquidCanvasPool pool = FindOrCreatePool();
            GameObject root = CreateImmersionVolume("Mud Volume", pool, GetProfile(MudName), new Vector3(3f, 0.5f, 3f));
            Place(root, command, "Create Liquid Mud Volume");
        }

        [MenuItem("GameObject/SabaProps/Liquid/Shower", false, 23)]
        public static void CreateShowerFromMenu(MenuCommand command)
        {
            LiquidCanvasPool pool = FindOrCreatePool();
            GameObject root = CreateShower(pool, GetProfile(WaterName));
            Place(root, command, "Create Liquid Shower");
        }

        [MenuItem("GameObject/SabaProps/Liquid/Water Gun", false, 24)]
        public static void CreateWaterGunFromMenu(MenuCommand command)
        {
            LiquidCanvasPool pool = FindOrCreatePool();
            GameObject root = CreateWaterGun(pool, GetProfile(WaterName));
            Place(root, command, "Create Liquid Water Gun");
        }

        /// <summary>The pool in the open scene, or a new one when there is none.</summary>
        public static LiquidCanvasPool FindOrCreatePool()
        {
            LiquidCanvasPool pool = Object.FindObjectOfType<LiquidCanvasPool>();
            if (pool != null)
            {
                return pool;
            }

            GameObject root = LiquidCanvasPoolBuilder.CreateCanvasPool(LiquidCanvasPoolBuilder.DefaultCanvasCount);
            Undo.RegisterCreatedObjectUndo(root, "Create Liquid Canvas Pool");
            return root.GetComponent<LiquidCanvasPool>();
        }

        /// <summary>
        /// The named profile under "Liquid Profiles", created with its preset
        /// values when missing.
        /// </summary>
        public static LiquidProfile GetProfile(string name)
        {
            GameObject profiles = GameObject.Find(ProfilesName);
            if (profiles == null)
            {
                profiles = new GameObject(ProfilesName);
                Undo.RegisterCreatedObjectUndo(profiles, "Create Liquid Profiles");
            }

            Transform existing = profiles.transform.Find(name);
            if (existing != null)
            {
                LiquidProfile found = existing.GetComponent<LiquidProfile>();
                if (found != null)
                {
                    return found;
                }
            }

            var profileObject = new GameObject(name);
            profileObject.transform.SetParent(profiles.transform, false);
            LiquidProfile profile = profileObject.AddUdonSharpComponent<LiquidProfile>();
            ApplyPreset(profile, name);
            UdonSharpEditorUtility.CopyProxyToUdon(profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        /// <summary>Every built-in preset, in the order the comparison demo lines them up.</summary>
        public static readonly string[] PresetNames =
        {
            WaterName, JuiceName, RedPaintName, BluePaintName, MudName, SlimeName, SyrupName,
        };

        /// <summary>
        /// Preset values for the built-in profiles.
        /// <para>
        /// The presets vary along the axes the canvas can show: how much pigment
        /// covers the surface, how thick and glossy the film is, how freely it
        /// runs, whether it dries, and whether it washes other liquids away.
        /// </para>
        /// <list type="bullet">
        /// <item>Water: film only. Darkens and shines, runs quickly, dries in a minute and a half, washes pigment away.</item>
        /// <item>Juice: water with a thin tint. Runs like water and dries to a faint stain.</item>
        /// <item>Paint: opaque pigment in a thin, glossy film. Runs a little, dries to a satin finish, stays until scrubbed.</item>
        /// <item>Mud: dense pigment in a thick film that barely runs; dries lighter and matte.</item>
        /// <item>Slime: translucent pigment in a thick, very glossy film that never dries and hangs in long drips.</item>
        /// <item>Syrup: translucent amber, the most viscous and glossy; never dries.</item>
        /// </list>
        /// </summary>
        public static void ApplyPreset(LiquidProfile profile, string name)
        {
            switch (name)
            {
                case JuiceName:
                    Set(profile, new Color(0.95f, 0.42f, 0.04f), 0.3f, 1f, 0.9f, 0.15f, 120f, 0.2f, 0.6f);
                    return;
                case RedPaintName:
                    Set(profile, new Color(0.72f, 0.04f, 0.05f), 1f, 0.5f, 0.85f, 0.6f, 240f, 0f, 0.3f);
                    return;
                case BluePaintName:
                    Set(profile, new Color(0.04f, 0.18f, 0.72f), 1f, 0.5f, 0.85f, 0.6f, 240f, 0f, 0.3f);
                    return;
                case YellowPaintName:
                    Set(profile, new Color(0.95f, 0.78f, 0.08f), 1f, 0.5f, 0.85f, 0.6f, 240f, 0f, 0.3f);
                    return;
                case BlackPaintName:
                    Set(profile, new Color(0.03f, 0.03f, 0.03f), 1f, 0.5f, 0.85f, 0.6f, 240f, 0f, 0.3f);
                    return;
                case DarkGreyPaintName:
                    Set(profile, new Color(0.25f, 0.25f, 0.25f), 1f, 0.5f, 0.85f, 0.6f, 240f, 0f, 0.3f);
                    return;
                case GreyPaintName:
                    Set(profile, new Color(0.5f, 0.5f, 0.5f), 1f, 0.5f, 0.85f, 0.6f, 240f, 0f, 0.3f);
                    return;
                case LightGreyPaintName:
                    Set(profile, new Color(0.75f, 0.75f, 0.75f), 1f, 0.5f, 0.85f, 0.6f, 240f, 0f, 0.3f);
                    return;
                case WhitePaintName:
                    Set(profile, new Color(0.96f, 0.96f, 0.95f), 1f, 0.5f, 0.85f, 0.6f, 240f, 0f, 0.3f);
                    return;
                case MudName:
                    Set(profile, new Color(0.44f, 0.31f, 0.19f), 0.9f, 0.8f, 0.6f, 0.85f, 180f, 0f, 0.35f);
                    return;
                case SlimeName:
                    Set(profile, new Color(0.22f, 0.8f, 0.3f), 0.45f, 1f, 1f, 0.92f, 0f, 0f, 0.2f);
                    return;
                case SyrupName:
                    Set(profile, new Color(0.7f, 0.36f, 0.05f), 0.55f, 1f, 1f, 0.97f, 0f, 0f, 0.15f);
                    return;
                default:
                    Set(profile, Color.white, 0f, 1f, 0.92f, 0.1f, 90f, 0.5f, 0.6f);
                    return;
            }
        }

        private static void Set(LiquidProfile profile, Color colour, float pigment, float film, float smoothness,
            float viscosity, float dryingSeconds, float wash, float irregularity)
        {
            profile.pigmentColor = colour;
            profile.pigmentAmount = pigment;
            profile.filmAmount = film;
            profile.smoothness = smoothness;
            profile.viscosity = viscosity;
            profile.dryingSeconds = dryingSeconds;
            profile.washStrength = wash;
            profile.edgeIrregularity = irregularity;
        }

        /// <summary>
        /// A trigger box whose top face is the liquid surface.
        /// <para>
        /// The box extends below the surface by its full height, so a player
        /// standing on the floor under it is inside the trigger. Raycasts ignore
        /// triggers, so the box does not block other Sources.
        /// </para>
        /// </summary>
        public static GameObject CreateImmersionVolume(string name, LiquidCanvasPool pool, LiquidProfile profile, Vector3 size)
        {
            var root = new GameObject(name);
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = size;
            trigger.center = new Vector3(0f, -size.y * 0.5f, 0f);

            LiquidImmersionVolume volume = root.AddUdonSharpComponent<LiquidImmersionVolume>();
            volume.pool = pool;
            volume.profile = profile;
            UdonSharpEditorUtility.CopyProxyToUdon(volume);
            EditorUtility.SetDirty(volume);
            return root;
        }

        /// <summary>
        /// A fixed shower head 2.2 m up, pointing down, toggled by Interact.
        /// </summary>
        public static GameObject CreateShower(LiquidCanvasPool pool, LiquidProfile profile)
        {
            var root = new GameObject("Shower");

            var head = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            head.transform.localScale = new Vector3(0.18f, 0.02f, 0.18f);

            var nozzle = new GameObject("Nozzle");
            nozzle.transform.SetParent(root.transform, false);
            nozzle.transform.localPosition = new Vector3(0f, 2.18f, 0f);
            nozzle.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);

            ParticleSystem stream = CreateStream(nozzle.transform, 12f, 4f, 1.2f);

            LiquidShower shower = head.AddUdonSharpComponent<LiquidShower>();
            shower.pool = pool;
            shower.profile = profile;
            shower.nozzle = nozzle.transform;
            shower.stream = stream;
            shower.coneAngle = 12f;
            shower.range = 2.5f;
            shower.toggleOnInteract = true;
            UdonSharpEditorUtility.CopyProxyToUdon(shower);
            EditorUtility.SetDirty(shower);
            return root;
        }

        /// <summary>
        /// A pickup water gun: collider, kinematic rigidbody, VRCPickup, object
        /// sync, a muzzle at the front and a stream effect.
        /// </summary>
        public static GameObject CreateWaterGun(LiquidCanvasPool pool, LiquidProfile profile)
        {
            var root = new GameObject("Water Gun");

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.05f, 0.08f, 0.24f);

            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.06f, 0.1f, 0.26f);

            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            var pickup = root.AddComponent<VRCPickup>();
            pickup.pickupable = true;
            pickup.AutoHold = VRC_Pickup.AutoHoldMode.Yes;
            pickup.UseText = "Spray";

            VRCObjectSync sync = root.AddComponent<VRCObjectSync>();
            sync.AllowCollisionOwnershipTransfer = false;

            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0.02f, 0.14f);

            ParticleSystem stream = CreateStream(muzzle.transform, 1.5f, 9f, 0.6f);

            LiquidWaterGun gun = root.AddUdonSharpComponent<LiquidWaterGun>();
            gun.pool = pool;
            gun.profile = profile;
            gun.muzzle = muzzle.transform;
            gun.stream = stream;
            UdonSharpEditorUtility.CopyProxyToUdon(gun);
            EditorUtility.SetDirty(gun);
            return root;
        }

        /// <summary>
        /// A stopped particle stream along the parent's +Z. Visual only: hits are
        /// decided by the Source's rays, never by particle collisions, because
        /// Udon cannot read where a particle hit.
        /// </summary>
        private static ParticleSystem CreateStream(Transform parent, float coneAngle, float speed, float lifetime)
        {
            return CreateStream(parent, coneAngle, speed, lifetime, new Color(0.85f, 0.93f, 1f, 0.6f), 250f, 0.02f, 1f);
        }

        /// <summary>
        /// The stream with a given colour, emission rate and droplet size. A rate
        /// of zero makes a stream that only emits when a Source calls Emit.
        /// </summary>
        public static ParticleSystem CreateStream(Transform parent, float coneAngle, float speed, float lifetime,
            Color colour, float rate, float size, float gravity)
        {
            var streamObject = new GameObject("Stream");
            streamObject.transform.SetParent(parent, false);

            var stream = streamObject.AddComponent<ParticleSystem>();
            stream.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = stream.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = colour;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;

            ParticleSystem.EmissionModule emission = stream.emission;
            emission.rateOverTime = rate;

            ParticleSystem.ShapeModule shape = stream.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = coneAngle;
            shape.radius = 0.01f;

            ParticleSystem.CollisionModule collision = stream.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.bounce = 0.05f;
            collision.lifetimeLoss = 0.6f;

            var renderer = streamObject.GetComponent<ParticleSystemRenderer>();
            Material material = LiquidAssets.StreamMaterial(StreamMaterialPath);
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            return stream;
        }

        /// <summary>
        /// An automatic sprayer on a post, aimed at <paramref name="aimAt"/> (world).
        /// Its droplets take the liquid's colour, so a row of sprayers reads at a glance.
        /// </summary>
        public static LiquidSprayer CreateSprayer(Transform parent, string name, LiquidCanvasPool pool,
            LiquidProfile profile, Vector3 position, Vector3 aimAt, Material postMaterial)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;

            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Post";
            Object.DestroyImmediate(post.GetComponent<Collider>());
            post.transform.SetParent(root.transform, false);
            post.transform.localScale = new Vector3(0.06f, position.y * 0.5f, 0.06f);
            post.transform.position = new Vector3(position.x, position.y * 0.5f, position.z);
            post.GetComponent<Renderer>().sharedMaterial = postMaterial;

            var nozzle = new GameObject("Nozzle");
            nozzle.transform.SetParent(root.transform, false);
            nozzle.transform.LookAt(aimAt);

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            head.name = "Head";
            Object.DestroyImmediate(head.GetComponent<Collider>());
            head.transform.SetParent(nozzle.transform, false);
            head.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            head.transform.localScale = new Vector3(0.08f, 0.06f, 0.08f);
            head.GetComponent<Renderer>().sharedMaterial = postMaterial;

            Color droplet = profile.pigmentAmount > 0f
                ? new Color(profile.pigmentColor.r, profile.pigmentColor.g, profile.pigmentColor.b, 0.9f)
                : new Color(0.85f, 0.93f, 1f, 0.6f);
            float distance = Vector3.Distance(position, aimAt);
            // Droplets reach the target in about a third of a second. Gravity is
            // reduced so they arrive near where the rays land rather than short of it.
            ParticleSystem stream = CreateStream(nozzle.transform, 6f, distance / 0.35f, 0.45f, droplet, 0f,
                0.012f + profile.viscosity * 0.05f, 0.3f);

            LiquidSprayer sprayer = root.AddUdonSharpComponent<LiquidSprayer>();
            sprayer.pool = pool;
            sprayer.profile = profile;
            sprayer.nozzle = nozzle.transform;
            sprayer.stream = stream;
            sprayer.range = distance + 1f;
            UdonSharpEditorUtility.CopyProxyToUdon(sprayer);
            EditorUtility.SetDirty(sprayer);
            return sprayer;
        }

        private static void Place(GameObject root, MenuCommand command, string undoName)
        {
            GameObjectUtility.SetParentAndAlign(root, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(root, undoName);
            Selection.activeGameObject = root;
        }
    }
}
