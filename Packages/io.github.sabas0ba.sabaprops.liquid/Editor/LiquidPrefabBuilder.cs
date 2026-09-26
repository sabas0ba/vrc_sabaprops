using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace SabaProps.Liquid.Editors
{
    /// <summary>
    /// Builds the ready-to-use Source prefabs shipped in the package's Prefabs
    /// folder: cup, bucket, faucet, shower, umbrella, water gun and a nozzle
    /// stand with its control panel.
    /// <para>
    /// Each prefab carries its own liquid (a child "Liquid" profile) and leaves
    /// the pool unset: at runtime every Source looks the pool up by its name,
    /// so a prefab dropped into a scene works once the scene has a pool. The
    /// placement menus create one when it is missing.
    /// </para>
    /// <para>
    /// Materials and textures the prefabs use are written next to them, inside
    /// the package, so the prefabs never point at generated project assets.
    /// </para>
    /// </summary>
    public static class LiquidPrefabBuilder
    {
        public const string PrefabFolder = "Packages/io.github.sabas0ba.sabaprops.liquid/Prefabs";
        public const string MaterialFolder = PrefabFolder + "/Materials";

        public const string CupName = "Liquid Cup";
        public const string BucketName = "Liquid Bucket";
        public const string FaucetName = "Liquid Faucet";
        public const string ShowerName = "Liquid Shower";
        public const string UmbrellaName = "Liquid Umbrella";
        public const string WaterGunName = "Liquid Water Gun";
        public const string NozzleStandName = "Liquid Nozzle Stand";
        public const string SprayGunName = "Liquid Spray Gun";

        public static readonly string[] PrefabNames =
        {
            CupName, BucketName, FaucetName, ShowerName, UmbrellaName, WaterGunName, NozzleStandName, SprayGunName,
        };

        public static string PrefabPath(string name)
        {
            return PrefabFolder + "/" + name + ".prefab";
        }

        // ------------------------------------------------------------------
        // Menus
        // ------------------------------------------------------------------

        /// <summary>
        /// Regenerates every prefab from this builder. For package development;
        /// users place the shipped prefabs.
        /// </summary>
        [MenuItem("Tools/SabaProps/Liquid/Rebuild Prefabs", false, 20)]
        public static void BuildAll()
        {
            LiquidAssets.EnsureFolder(MaterialFolder);
            Save(BuildCup(), CupName);
            Save(BuildBucket(), BucketName);
            Save(BuildFaucet(), FaucetName);
            Save(BuildShower(), ShowerName);
            Save(BuildUmbrella(), UmbrellaName);
            Save(BuildWaterGun(), WaterGunName);
            Save(BuildNozzleStand(LiquidNozzle.ModeContinuous), NozzleStandName);
            Save(BuildSprayGun(), SprayGunName);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("GameObject/SabaProps/Liquid/Prefabs/Cup", false, 40)]
        private static void PlaceCup(MenuCommand command) { PlaceFromMenu(CupName, command); }

        [MenuItem("GameObject/SabaProps/Liquid/Prefabs/Bucket", false, 41)]
        private static void PlaceBucket(MenuCommand command) { PlaceFromMenu(BucketName, command); }

        [MenuItem("GameObject/SabaProps/Liquid/Prefabs/Faucet", false, 42)]
        private static void PlaceFaucet(MenuCommand command) { PlaceFromMenu(FaucetName, command); }

        [MenuItem("GameObject/SabaProps/Liquid/Prefabs/Shower", false, 43)]
        private static void PlaceShower(MenuCommand command) { PlaceFromMenu(ShowerName, command); }

        [MenuItem("GameObject/SabaProps/Liquid/Prefabs/Umbrella", false, 44)]
        private static void PlaceUmbrella(MenuCommand command) { PlaceFromMenu(UmbrellaName, command); }

        [MenuItem("GameObject/SabaProps/Liquid/Prefabs/Water Gun", false, 45)]
        private static void PlaceWaterGun(MenuCommand command) { PlaceFromMenu(WaterGunName, command); }

        [MenuItem("GameObject/SabaProps/Liquid/Prefabs/Nozzle Stand", false, 46)]
        private static void PlaceNozzleStand(MenuCommand command) { PlaceFromMenu(NozzleStandName, command); }

        [MenuItem("GameObject/SabaProps/Liquid/Prefabs/Spray Gun", false, 47)]
        private static void PlaceSprayGun(MenuCommand command) { PlaceFromMenu(SprayGunName, command); }

        /// <summary>
        /// An instance of the named prefab under <paramref name="parent"/>. The
        /// scene gets a pool if it has none, so the instance works on entering play.
        /// </summary>
        public static GameObject Place(string name, Transform parent)
        {
            LiquidSourceBuilder.FindOrCreatePool();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(name));
            if (prefab == null)
            {
                Debug.LogError("[SabaProps Liquid] Prefab " + PrefabPath(name) + " が見つかりません。");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (parent != null)
            {
                instance.transform.SetParent(parent, false);
            }

            return instance;
        }

        private static void PlaceFromMenu(string name, MenuCommand command)
        {
            GameObject instance = Place(name, null);
            if (instance == null)
            {
                return;
            }

            GameObjectUtility.SetParentAndAlign(instance, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(instance, "Place " + name);
            Selection.activeGameObject = instance;
        }

        private static void Save(GameObject root, string name)
        {
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(name));
            Object.DestroyImmediate(root);
        }

        // ------------------------------------------------------------------
        // Prefabs
        // ------------------------------------------------------------------

        /// <summary>A cup of water. Use to throw its contents; it refills at once.</summary>
        public static GameObject BuildCup()
        {
            GameObject root = Vessel(CupName, 0.04f, 0.11f, Surface("CupBody", new Color(0.85f, 0.88f, 0.92f), 0.8f));
            LiquidNozzle nozzle = VesselNozzle(root, 0.11f, LiquidSourceBuilder.WaterName, 0.3f, 2.5f, 5f, 0.1f);
            nozzle.particlesPerLitre = 160f;
            UdonSharpEditorUtility.CopyProxyToUdon(nozzle);
            return root;
        }

        /// <summary>A bucket of water. Use to throw the whole bucket in one go.</summary>
        public static GameObject BuildBucket()
        {
            Material body = Surface("BucketBody", new Color(0.2f, 0.42f, 0.7f), 0.5f);
            GameObject root = Vessel(BucketName, 0.15f, 0.3f, body);
            Part(root.transform, "Handle", PrimitiveType.Cylinder, new Vector3(0f, 0.34f, 0f),
                new Vector3(0.012f, 0.16f, 0.012f), Quaternion.Euler(0f, 0f, 90f), Surface("Metal", new Color(0.6f, 0.6f, 0.62f), 0.8f), false);
            LiquidNozzle nozzle = VesselNozzle(root, 0.3f, LiquidSourceBuilder.WaterName, 5f, 2.5f, 5f, 0.34f);
            nozzle.particlesPerLitre = 50f;
            UdonSharpEditorUtility.CopyProxyToUdon(nozzle);
            return root;
        }

        /// <summary>A sink with a faucet over it. Interact opens and closes it.</summary>
        public static GameObject BuildFaucet()
        {
            LiquidProfile water = OwnProfile(null, LiquidSourceBuilder.WaterName);
            GameObject root = LiquidSourceBuilder.CreateShower(null, water, MaterialFolder);
            root.name = FaucetName;
            water.transform.SetParent(root.transform, false);

            LiquidShower source = root.GetComponentInChildren<LiquidShower>();
            source.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            source.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
            source.nozzle.localPosition = new Vector3(0f, 1.12f, 0f);
            source.coneAngle = 4f;
            source.range = 0.8f;
            source.raysPerEvaluation = 3;
            source.startRunning = false;
            UdonSharpEditorUtility.CopyProxyToUdon(source);

            Material basin = Surface("Basin", new Color(0.92f, 0.93f, 0.94f), 0.7f);
            Part(root.transform, "Sink", PrimitiveType.Cube, new Vector3(0f, 0.8f, 0f), new Vector3(0.6f, 0.1f, 0.45f),
                Quaternion.identity, basin, true);
            Part(root.transform, "Stand", PrimitiveType.Cube, new Vector3(0f, 0.375f, 0f), new Vector3(0.3f, 0.75f, 0.3f),
                Quaternion.identity, basin, true);
            Part(root.transform, "Spout", PrimitiveType.Cylinder, new Vector3(0f, 1.05f, -0.12f), new Vector3(0.03f, 0.12f, 0.03f),
                Quaternion.Euler(-60f, 0f, 0f), Surface("Metal", new Color(0.6f, 0.6f, 0.62f), 0.8f), false);
            return root;
        }

        /// <summary>A fixed shower 2.2 m up. Interact turns it on and off.</summary>
        public static GameObject BuildShower()
        {
            LiquidProfile water = OwnProfile(null, LiquidSourceBuilder.WaterName);
            GameObject root = LiquidSourceBuilder.CreateShower(null, water, MaterialFolder);
            root.name = ShowerName;
            water.transform.SetParent(root.transform, false);

            Material metal = Surface("Metal", new Color(0.6f, 0.6f, 0.62f), 0.8f);
            root.transform.Find("Head").GetComponent<Renderer>().sharedMaterial = metal;
            Part(root.transform, "Pipe", PrimitiveType.Cylinder, new Vector3(0f, 1.2f, 0.25f), new Vector3(0.03f, 1.2f, 0.03f),
                Quaternion.identity, metal, false);
            Part(root.transform, "Arm", PrimitiveType.Cylinder, new Vector3(0f, 2.3f, 0.12f), new Vector3(0.025f, 0.13f, 0.025f),
                Quaternion.Euler(90f, 0f, 0f), metal, false);
            return root;
        }

        /// <summary>
        /// An umbrella to carry. Rain and snow areas do not fall on whoever is
        /// under its canopy, and the canopy stops the falling particles.
        /// </summary>
        public static GameObject BuildUmbrella()
        {
            var root = new GameObject(UmbrellaName);
            Material shaft = Surface("Metal", new Color(0.6f, 0.6f, 0.62f), 0.8f);
            Material cloth = Surface("UmbrellaCloth", new Color(0.12f, 0.2f, 0.45f), 0.55f);

            Part(root.transform, "Shaft", PrimitiveType.Cylinder, new Vector3(0f, 0.4f, 0f), new Vector3(0.015f, 0.45f, 0.015f),
                Quaternion.identity, shaft, false);
            Part(root.transform, "Grip", PrimitiveType.Cylinder, new Vector3(0f, -0.02f, 0f), new Vector3(0.03f, 0.08f, 0.03f),
                Quaternion.identity, Surface("Grip", new Color(0.25f, 0.15f, 0.08f), 0.4f), false);

            var canopy = new GameObject("Canopy");
            canopy.transform.SetParent(root.transform, false);
            canopy.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            Part(canopy.transform, "Cloth", PrimitiveType.Sphere, Vector3.zero, new Vector3(1.1f, 0.3f, 1.1f),
                Quaternion.identity, cloth, false);
            // Stops the rain particles; on the pickup layer, so it never pushes players.
            var cover = canopy.AddComponent<BoxCollider>();
            cover.size = new Vector3(1f, 0.05f, 1f);
            cover.center = new Vector3(0f, 0.05f, 0f);

            LiquidUmbrella umbrella = canopy.AddUdonSharpComponent<LiquidUmbrella>();
            umbrella.canopy = canopy.transform;
            umbrella.radius = 0.55f;
            umbrella.depth = 2.2f;
            UdonSharpEditorUtility.CopyProxyToUdon(umbrella);

            var grip = root.AddComponent<CapsuleCollider>();
            grip.center = new Vector3(0f, 0.3f, 0f);
            grip.height = 0.8f;
            grip.radius = 0.04f;
            MakePickup(root, "Umbrella", null);
            SetLayer(root, 13);
            return root;
        }

        /// <summary>The water gun, carrying its own liquid.</summary>
        public static GameObject BuildWaterGun()
        {
            LiquidProfile water = OwnProfile(null, LiquidSourceBuilder.WaterName);
            GameObject root = LiquidSourceBuilder.CreateWaterGun(null, water, MaterialFolder);
            root.name = WaterGunName;
            water.transform.SetParent(root.transform, false);
            root.transform.Find("Body").GetComponent<Renderer>().sharedMaterial =
                Surface("GunBody", new Color(0.95f, 0.45f, 0.1f), 0.6f);
            return root;
        }

        /// <summary>
        /// A nozzle on a stand with its control panel: amount, range, speed and
        /// nozzle size, and a button to fire or to start and stop.
        /// </summary>
        public static GameObject BuildNozzleStand(int mode)
        {
            var root = new GameObject(NozzleStandName);
            Material device = Surface("Device", new Color(0.3f, 0.32f, 0.35f), 0.6f);
            Part(root.transform, "Post", PrimitiveType.Cylinder, new Vector3(0f, 0.6f, 0f), new Vector3(0.06f, 0.6f, 0.06f),
                Quaternion.identity, device, true);

            LiquidProfile water = OwnProfile(root.transform, LiquidSourceBuilder.WaterName);
            LiquidNozzle nozzle = CreateNozzle(root.transform, "Nozzle", water, new Vector3(0f, 1.25f, 0f), mode, MaterialFolder);
            Part(nozzle.transform, "Head", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.08f, 0.07f, 0.08f),
                Quaternion.Euler(90f, 0f, 0f), device, false);
            // Beside the post, readable from behind the nozzle (TextMesh faces its -Z).
            CreatePanel(root.transform, nozzle, new Vector3(0.35f, 1.1f, -0.1f), Quaternion.identity, MaterialFolder);
            return root;
        }

        /// <summary>A hand-held sprayer of water that fires while the use button is held.</summary>
        public static GameObject BuildSprayGun()
        {
            LiquidProfile water = OwnProfile(null, LiquidSourceBuilder.WaterName);
            GameObject root = CreatePortableNozzle(SprayGunName, LiquidNozzle.ModeHold, water, new Color(0.15f, 0.55f, 0.85f),
                MaterialFolder);
            water.transform.SetParent(root.transform, false);
            return root;
        }

        // ------------------------------------------------------------------
        // Shared parts, also used by the interactive demo scene
        // ------------------------------------------------------------------

        /// <summary>
        /// A nozzle to carry: a synced pickup with the nozzle on a child, so its
        /// Manual-synced firing state does not share a GameObject with
        /// VRCObjectSync. The pickup's use button, release and drop reach the
        /// nozzle through a relay on the root; dropping it stops the flow.
        /// <para>
        /// The mode picks what the use button does: fire once, start and stop a
        /// continuous flow, or flow while held. Hits land on players and
        /// mannequins alike, so people can spray each other.
        /// </para>
        /// </summary>
        public static GameObject CreatePortableNozzle(string name, int mode, LiquidProfile profile, Color bodyColour,
            string materialFolder)
        {
            var root = new GameObject(name);
            Material body = LiquidAssets.CreateOrLoadSurfaceMaterial(
                materialFolder + "/Portable" + mode + ".mat", bodyColour, 0.6f);
            Material metal = LiquidAssets.CreateOrLoadSurfaceMaterial(materialFolder + "/Metal.mat",
                new Color(0.6f, 0.6f, 0.62f), 0.8f);

            Part(root.transform, "Body", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.05f), new Vector3(0.07f, 0.14f, 0.07f),
                Quaternion.Euler(90f, 0f, 0f), body, false);
            Part(root.transform, "Grip", PrimitiveType.Cube, new Vector3(0f, -0.07f, -0.02f), new Vector3(0.035f, 0.11f, 0.05f),
                Quaternion.Euler(-12f, 0f, 0f), body, false);
            Part(root.transform, "Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.22f), new Vector3(0.025f, 0.04f, 0.025f),
                Quaternion.Euler(90f, 0f, 0f), metal, false);

            var collider = root.AddComponent<CapsuleCollider>();
            collider.direction = 2;
            collider.center = new Vector3(0f, -0.02f, 0.06f);
            collider.height = 0.36f;
            collider.radius = 0.05f;

            LiquidNozzle nozzle = CreateNozzle(root.transform, "Nozzle", profile, new Vector3(0f, 0f, 0.27f), mode, materialFolder);
            nozzle.fireOnInteract = false;
            nozzle.range = 8f;
            nozzle.speed = 10f;
            nozzle.diameter = 0.05f;
            nozzle.volume = mode == LiquidNozzle.ModeOneShot ? 1.5f : 0.6f;

            string useText = mode == LiquidNozzle.ModeOneShot ? "Fire" : mode == LiquidNozzle.ModeHold ? "Spray" : "Start / Stop";
            MakePickup(root, useText, nozzle);
            nozzle.pickup = root.GetComponent<VRCPickup>();
            UdonSharpEditorUtility.CopyProxyToUdon(nozzle);

            LiquidButton relay = root.GetComponent<LiquidButton>();
            relay.useUpEventName = nameof(LiquidNozzle.Release);
            relay.dropEventName = nameof(LiquidNozzle.StopFiring);
            UdonSharpEditorUtility.CopyProxyToUdon(relay);

            SetLayer(root, 13);
            return root;
        }

        /// <summary>A nozzle pointing along the parent's +Z, with a stream in the liquid's look.</summary>
        public static LiquidNozzle CreateNozzle(Transform parent, string name, LiquidProfile profile, Vector3 localPosition,
            int mode, string materialFolder)
        {
            var nozzleObject = new GameObject(name);
            nozzleObject.transform.SetParent(parent, false);
            nozzleObject.transform.localPosition = localPosition;

            ParticleSystem stream = LiquidParticleBuilder.CreateLiquidStream(nozzleObject.transform, profile, materialFolder,
                8f, 0.6f, 3f, mode == LiquidNozzle.ModeContinuous ? 300f : 0f);

            LiquidNozzle nozzle = nozzleObject.AddUdonSharpComponent<LiquidNozzle>();
            nozzle.profile = profile;
            nozzle.nozzle = nozzleObject.transform;
            nozzle.stream = stream;
            nozzle.mode = mode;
            UdonSharpEditorUtility.CopyProxyToUdon(nozzle);
            EditorUtility.SetDirty(nozzle);
            return nozzle;
        }

        /// <summary>
        /// A control panel for a nozzle: a readout and +/- buttons for amount,
        /// range, speed and nozzle size, and a fire or start/stop button unless
        /// the nozzle runs on a schedule.
        /// </summary>
        public static GameObject CreatePanel(Transform parent, LiquidNozzle nozzle, Vector3 localPosition, Quaternion localRotation,
            string materialFolder)
        {
            var panel = new GameObject("Control Panel");
            panel.transform.SetParent(parent, false);
            panel.transform.localPosition = localPosition;
            panel.transform.localRotation = localRotation;

            Material board = LiquidAssets.CreateOrLoadSurfaceMaterial(materialFolder + "/PanelBoard.mat", new Color(0.12f, 0.13f, 0.15f), 0.4f);
            Material key = LiquidAssets.CreateOrLoadSurfaceMaterial(materialFolder + "/PanelKey.mat", new Color(0.75f, 0.77f, 0.8f), 0.5f);
            Material fire = LiquidAssets.CreateOrLoadSurfaceMaterial(materialFolder + "/PanelFire.mat", new Color(0.85f, 0.25f, 0.15f), 0.5f);
            Part(panel.transform, "Board", PrimitiveType.Cube, new Vector3(0f, 0f, 0.02f), new Vector3(0.5f, 0.42f, 0.02f),
                Quaternion.identity, board, false);

            nozzle.readout = Readout(panel.transform, new Vector3(-0.12f, 0.06f, -0.005f));

            string[] labels = { "Volume", "Range", "Speed", "Nozzle" };
            string[] up = { nameof(LiquidNozzle.VolumeUp), nameof(LiquidNozzle.RangeUp), nameof(LiquidNozzle.SpeedUp), nameof(LiquidNozzle.DiameterUp) };
            string[] down = { nameof(LiquidNozzle.VolumeDown), nameof(LiquidNozzle.RangeDown), nameof(LiquidNozzle.SpeedDown), nameof(LiquidNozzle.DiameterDown) };
            for (int i = 0; i < labels.Length; i++)
            {
                float y = 0.15f - i * 0.075f;
                Button(panel.transform, labels[i] + " -", "-", new Vector3(0.1f, y, 0f), key, nozzle, down[i]);
                Button(panel.transform, labels[i] + " +", "+", new Vector3(0.18f, y, 0f), key, nozzle, up[i]);
            }

            if (nozzle.mode != LiquidNozzle.ModePeriodic)
            {
                string caption = nozzle.mode == LiquidNozzle.ModeContinuous ? "Start / Stop" : "Fire";
                GameObject trigger = Button(panel.transform, "Trigger", caption, new Vector3(-0.12f, -0.14f, 0f), fire, nozzle,
                    nameof(LiquidNozzle.Trigger));
                trigger.transform.localScale = new Vector3(0.18f, 0.07f, 0.03f);
            }

            UdonSharpEditorUtility.CopyProxyToUdon(nozzle);
            return panel;
        }

        internal static GameObject Button(Transform parent, string name, string caption, Vector3 position, Material material,
            UdonSharpBehaviour target, string eventName)
        {
            GameObject button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = name;
            button.transform.SetParent(parent, false);
            button.transform.localPosition = position;
            button.transform.localScale = new Vector3(0.06f, 0.06f, 0.03f);
            button.GetComponent<Renderer>().sharedMaterial = material;

            LiquidButton relay = button.AddUdonSharpComponent<LiquidButton>();
            relay.target = target;
            relay.eventName = eventName;
            UdonSharpEditorUtility.CopyProxyToUdon(relay);
            UdonSharpEditorUtility.GetBackingUdonBehaviour(relay).interactText = caption == "+" || caption == "-" ? name : caption;

            // The caption sits in front of the key, unscaled.
            var label = Caption(parent, name + " Label", caption, position + new Vector3(0f, 0f, -0.02f), 0.012f, TextAnchor.MiddleCenter);
            label.color = new Color(0.05f, 0.05f, 0.05f);
            return button;
        }

        /// <summary>
        /// The panel's readout: a UI Text on a small world-space canvas. Udon can
        /// change a UI Text but not a TextMesh, so the text that changes at run
        /// time is UI; the fixed captions stay TextMesh.
        /// </summary>
        private static Text Readout(Transform parent, Vector3 position)
        {
            var canvasObject = new GameObject("Readout");
            canvasObject.transform.SetParent(parent, false);
            canvasObject.transform.localPosition = position;
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200f, 240f);
            // 1 canvas unit = 1 mm.
            rect.localScale = new Vector3(0.001f, 0.001f, 0.001f);

            var textObject = new GameObject("Text");
            textObject.transform.SetParent(canvasObject.transform, false);
            var textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 22;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = new Color(0.9f, 0.95f, 0.9f);
            text.raycastTarget = false;
            text.text = "";
            return text;
        }

        private static TextMesh Caption(Transform parent, string name, string text, Vector3 position, float size, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = anchor;
            mesh.alignment = TextAlignment.Left;
            mesh.characterSize = size;
            mesh.fontSize = 64;
            mesh.color = new Color(0.9f, 0.95f, 0.9f);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                mesh.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }

            return mesh;
        }

        /// <summary>A carried vessel: an open cylinder with water showing at the top.</summary>
        private static GameObject Vessel(string name, float radius, float height, Material body)
        {
            var root = new GameObject(name);
            Part(root.transform, "Body", PrimitiveType.Cylinder, new Vector3(0f, height * 0.5f, 0f),
                new Vector3(radius * 2f, height * 0.5f, radius * 2f), Quaternion.identity, body, false);
            Part(root.transform, "Water", PrimitiveType.Cylinder, new Vector3(0f, height * 0.92f, 0f),
                new Vector3(radius * 1.85f, 0.004f, radius * 1.85f), Quaternion.identity,
                Surface("VesselWater", new Color(0.35f, 0.6f, 0.78f), 0.95f), false);

            var collider = root.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, height * 0.5f, 0f);
            collider.height = height;
            collider.radius = radius;
            return root;
        }

        /// <summary>
        /// The nozzle of a vessel: a one-shot throw forward and a little up from
        /// the rim, fired by the pickup's use button through a relay on the root.
        /// </summary>
        private static LiquidNozzle VesselNozzle(GameObject root, float height, string liquid, float volume, float range,
            float speed, float diameter)
        {
            LiquidProfile profile = OwnProfile(root.transform, liquid);
            LiquidNozzle nozzle = CreateNozzle(root.transform, "Nozzle", profile, new Vector3(0f, height, 0.05f),
                LiquidNozzle.ModeOneShot, MaterialFolder);
            // Thrown upward from the rim; the arc comes down on someone in front.
            nozzle.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            nozzle.fireOnInteract = false;
            nozzle.volume = volume;
            nozzle.range = range;
            nozzle.speed = speed;
            nozzle.diameter = diameter;
            UdonSharpEditorUtility.CopyProxyToUdon(nozzle);

            MakePickup(root, "Throw", nozzle);
            // On the pickup layer, so the vessel never blocks its own throw or pushes players.
            SetLayer(root, 13);
            return nozzle;
        }

        /// <summary>
        /// Makes <paramref name="root"/> a synced pickup. With a nozzle, the use
        /// button reaches it through a relay: use events only arrive on the
        /// pickup's own GameObject.
        /// </summary>
        private static void MakePickup(GameObject root, string useText, LiquidNozzle nozzle)
        {
            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            var pickup = root.AddComponent<VRCPickup>();
            pickup.pickupable = true;
            pickup.AutoHold = VRC_Pickup.AutoHoldMode.Yes;
            pickup.UseText = useText;

            VRCObjectSync sync = root.AddComponent<VRCObjectSync>();
            sync.AllowCollisionOwnershipTransfer = false;

            if (nozzle != null)
            {
                LiquidButton relay = root.AddUdonSharpComponent<LiquidButton>();
                relay.target = nozzle;
                relay.eventName = nameof(LiquidNozzle.Trigger);
                relay.relayPickupUse = true;
                UdonSharpEditorUtility.CopyProxyToUdon(relay);
            }
        }

        /// <summary>A liquid profile of its own, so the prefab does not depend on a scene's profiles.</summary>
        private static LiquidProfile OwnProfile(Transform parent, string preset)
        {
            var go = new GameObject("Liquid");
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            LiquidProfile profile = go.AddUdonSharpComponent<LiquidProfile>();
            LiquidSourceBuilder.ApplyPreset(profile, preset);
            UdonSharpEditorUtility.CopyProxyToUdon(profile);
            return profile;
        }

        private static Material Surface(string name, Color colour, float smoothness)
        {
            return LiquidAssets.CreateOrLoadSurfaceMaterial(MaterialFolder + "/" + name + ".mat", colour, smoothness);
        }

        private static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale,
            Quaternion rotation, Material material, bool solid)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            if (!solid)
            {
                Object.DestroyImmediate(part.GetComponent<Collider>());
            }

            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        private static void SetLayer(GameObject root, int layer)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
            }
        }
    }
}
