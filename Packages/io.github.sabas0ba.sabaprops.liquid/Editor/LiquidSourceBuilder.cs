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
        public const string MudName = "Mud";

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

        /// <summary>
        /// Preset values for the built-in profiles.
        /// <para>
        /// Water is film only: it darkens and shines, runs quickly, dries in about
        /// a minute and a half, and washes pigment away. Mud carries pigment and a
        /// thick film that barely runs; the film dries and leaves the lighter,
        /// matte pigment behind, which only water removes.
        /// </para>
        /// </summary>
        public static void ApplyPreset(LiquidProfile profile, string name)
        {
            if (name == MudName)
            {
                profile.pigmentColor = new Color(0.32f, 0.22f, 0.13f, 1f);
                profile.pigmentAmount = 0.85f;
                profile.filmAmount = 0.8f;
                profile.smoothness = 0.55f;
                profile.viscosity = 0.85f;
                profile.dryingSeconds = 180f;
                profile.washStrength = 0f;
                profile.edgeIrregularity = 0.35f;
                return;
            }

            profile.pigmentColor = Color.white;
            profile.pigmentAmount = 0f;
            profile.filmAmount = 1f;
            profile.smoothness = 0.92f;
            profile.viscosity = 0.1f;
            profile.dryingSeconds = 90f;
            profile.washStrength = 0.5f;
            profile.edgeIrregularity = 0.6f;
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
            var streamObject = new GameObject("Stream");
            streamObject.transform.SetParent(parent, false);

            var stream = streamObject.AddComponent<ParticleSystem>();
            stream.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = stream.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = 0.02f;
            main.startColor = new Color(0.85f, 0.93f, 1f, 0.6f);
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;

            ParticleSystem.EmissionModule emission = stream.emission;
            emission.rateOverTime = 250f;

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
            Material material = LiquidAssets.CreateOrLoadMaterial(StreamMaterialPath, "Particles/Standard Unlit");
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            return stream;
        }

        private static void Place(GameObject root, MenuCommand command, string undoName)
        {
            GameObjectUtility.SetParentAndAlign(root, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(root, undoName);
            Selection.activeGameObject = root;
        }
    }
}
