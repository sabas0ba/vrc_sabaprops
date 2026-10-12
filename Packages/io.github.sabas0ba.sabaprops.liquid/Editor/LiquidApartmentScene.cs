using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Components;

namespace SabaProps.Liquid.Editors
{
    /// <summary>Apartment furnishings and functional liquid props, built without external models.</summary>
    public static class LiquidApartmentScene
    {
        public const string ScenePath = LiquidSampleScene.SampleFolder + "/LiquidApartment.unity";
        private const string Folder = LiquidAssets.MaterialFolder + "/Apartment";
        private static Transform _root;
        private static Material _wall, _wood, _white, _metal, _fabric, _tile;

        [MenuItem("Tools/SabaProps/Liquid/Create Apartment Scene")]
        public static void CreateAndOpen()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Create();
        }

        public static void Create()
        {
            // Reuse independent pickups; leave the previously generated Playroom scene intact.
            LiquidPlayroomScene.CreateContents(false);
            Object.DestroyImmediate(GameObject.Find("Playroom architecture"));
            foreach (TextMesh label in Object.FindObjectsOfType<TextMesh>())
                if (label.transform.parent == null) Object.DestroyImmediate(label.gameObject);
            foreach (Light light in Object.FindObjectsOfType<Light>())
                if (light.name == "Room light") Object.DestroyImmediate(light.gameObject);
            _root = new GameObject("Apartment").transform;
            _wall = Mat("Wallpaper", new Color(0.88f, 0.85f, 0.77f));
            _wood = Mat("Oak", new Color(0.55f, 0.35f, 0.19f));
            _white = Mat("Ceramic", new Color(0.9f, 0.93f, 0.93f));
            _metal = Mat("Metal", new Color(0.18f, 0.22f, 0.24f));
            _fabric = Mat("Sofa", new Color(0.24f, 0.42f, 0.44f));
            _tile = Mat("Tile", new Color(0.55f, 0.67f, 0.70f));
            var pool = Object.FindObjectOfType<LiquidCanvasPool>();
            Architecture();
            Living(pool);
            Kitchen(pool);
            Bathroom(pool);
            Entrance(pool);
            Balcony(pool);
            // A single volume avoids duplicate projections at internal partitions.
            var canvas = pool.surfaces[0];
            canvas.anchor.position = new Vector3(0, 1.5f, 1.1f);
            canvas.halfExtents = new Vector3(6.25f, 1.7f, 6.3f);
            UdonSharpEditorUtility.CopyProxyToUdon(canvas);
            LiquidPaintingBuilder.Register(pool);
            LiquidContactBuilder.EnableContact();
            foreach (LiquidNozzle nozzle in Object.FindObjectsOfType<LiquidNozzle>())
            { nozzle.pool = pool; UdonSharpEditorUtility.CopyProxyToUdon(nozzle); }
            foreach (LiquidShower shower in Object.FindObjectsOfType<LiquidShower>())
            { shower.pool = pool; UdonSharpEditorUtility.CopyProxyToUdon(shower); }
            var descriptor = Object.FindObjectOfType<VRCSceneDescriptor>();
            for (int i = 0; i < descriptor.spawns.Length; i++)
                descriptor.spawns[i].position = new Vector3(-2.7f + i * 0.6f, 0.05f, -3.3f);
            Camera.main.transform.position = new Vector3(-0.3f, 2.4f, -0.5f);
            Camera.main.transform.LookAt(new Vector3(-3, 1, 3));
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
        }

        private static void Architecture()
        {
            Box("Floor", new Vector3(0, -0.1f, 0), new Vector3(12.4f, 0.2f, 10.4f), _wood);
            Box("Ceiling", new Vector3(0, 3.1f, 0), new Vector3(12.4f, 0.2f, 10.4f), _wall);
            Box("South wall", new Vector3(0, 1.5f, -5.1f), new Vector3(12.4f, 3, 0.2f), _wall);
            Box("West wall", new Vector3(-6.1f, 1.5f, 0), new Vector3(0.2f, 3, 10), _wall);
            Box("East wall", new Vector3(6.1f, 1.5f, 0), new Vector3(0.2f, 3, 10), _wall);
            // Open passages are 1.4 m or wider; lintels leave 2.3 m of headroom.
            Partition("Balcony doorway", true, 5.1f, -6, 6, -2.3f, -0.7f);
            Partition("Living doorway", true, -1, -6, 1, -2.7f, -1.1f);
            Partition("Bathroom doorway", false, 1, -5, 1, -3.5f, -1.9f);
            Box("Kitchen partition", new Vector3(3.5f, 1.5f, 1), new Vector3(5, 3, 0.15f), _wall);
            Box("Entry tiles", new Vector3(-2.5f, 0.005f, -3.7f), new Vector3(6.8f, 0.01f, 2.5f), _tile);
            Box("Bathroom tiles", new Vector3(3.5f, 0.008f, -2), new Vector3(4.8f, 0.016f, 5.8f), _tile);
            for (int i = 0; i < 23; i++)
                Box("Floorboard joint", new Vector3(-5.75f + i * 0.5f, 0.003f, 2.9f), new Vector3(0.008f, 0.006f, 4.1f), _wood);
            Box("Front door", new Vector3(-2, 1.1f, -4.98f), new Vector3(1.1f, 2.2f, 0.06f), _wood);
            Box("Door handle", new Vector3(-1.6f, 1, -4.89f), new Vector3(0.16f, 0.04f, 0.08f), _metal);
            Box("Living window", new Vector3(-4.25f, 1.8f, 4.96f), new Vector3(2.5f, 1.5f, 0.04f), Mat("Window", new Color(0.48f, 0.7f, 0.84f)));
            for (int side = -1; side <= 1; side += 2)
                Box("Curtain", new Vector3(-4.25f + side * 1.25f, 1.65f, 4.83f), new Vector3(0.35f, 2.3f, 0.12f), _white);
        }

        private static void Living(LiquidCanvasPool pool)
        {
            Box("Sofa seat", new Vector3(-4.8f, 0.42f, 2.2f), new Vector3(1.35f, 0.45f, 2.8f), _fabric);
            Box("Sofa back", new Vector3(-5.4f, 0.85f, 2.2f), new Vector3(0.25f, 1.1f, 2.8f), _fabric);
            for (int side = -1; side <= 1; side += 2)
                Box("Sofa arm", new Vector3(-4.8f, 0.7f, 2.2f + side * 1.3f), new Vector3(1.35f, 0.7f, 0.18f), _fabric);
            Box("Rug", new Vector3(-3, 0.015f, 2.2f), new Vector3(3.5f, 0.03f, 3.6f), Mat("Rug", new Color(0.7f, 0.61f, 0.48f)));
            Table("Coffee table", new Vector3(-3, 0.55f, 2.1f), new Vector2(1.2f, 2));
            Box("TV cabinet", new Vector3(0.45f, 0.35f, 2.8f), new Vector3(0.7f, 0.7f, 2.5f), _wood);
            Box("TV", new Vector3(0.6f, 1.2f, 2.8f), new Vector3(0.08f, 1, 1.8f), _metal);
            Table("Art desk", new Vector3(-4, 0.8f, -0.45f), new Vector2(3.3f, 0.8f));
            for (int i = 0; i < 4; i++)
            {
                float x = -5.3f + i * 0.85f;
                string[] names = { "Red Paint", "Blue Paint", "Yellow Paint", "Fluorescent Green Paint" };
                Move(names[i] + " sprayer", new Vector3(x, 1.02f, -0.5f));
                Move("Pen " + i, new Vector3(x, 0.93f, -0.25f));
                Move("Star stamp " + i, new Vector3(-3.2f, 0.67f, 1.4f + i * 0.42f));
                Move("Sponge " + i, new Vector3(-2.8f, 0.67f, 1.4f + i * 0.42f));
            }
            var luminous = LiquidPaintingBuilder.CreatePaintTool("Luminous pen", LiquidPaintTool.ModePen,
                LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.LuminousPaintName), LiquidBodyCanvas.ShapeStroke, 0.035f, Folder);
            luminous.transform.position = new Vector3(-4, 0.94f, -0.18f);
            var eraser = LiquidPrefabBuilder.BuildEraser();
            eraser.transform.position = new Vector3(-4.5f, 0.94f, -0.18f);
            Lighting(pool, "Living", new Vector3(-2.5f, 1.5f, 2), new Vector3(6.8f, 3, 5.8f), true);
            Label("LIVING / draw, rub, transfer", new Vector3(-4, 1.5f, -0.86f), 180);
        }

        private static void Kitchen(LiquidCanvasPool pool)
        {
            Box("Kitchen units", new Vector3(3.5f, 0.45f, 4.5f), new Vector3(4.6f, 0.9f, 0.85f), _white);
            Box("Countertop", new Vector3(3.5f, 0.93f, 4.5f), new Vector3(4.7f, 0.06f, 0.9f), _wood);
            Box("Upper cupboards", new Vector3(3.5f, 2.25f, 4.7f), new Vector3(4.6f, 0.8f, 0.5f), _white);
            for (int i = 0; i < 4; i++)
                Box("Cabinet handle", new Vector3(1.7f + i * 1.15f, 0.7f, 4.04f), new Vector3(0.24f, 0.035f, 0.035f), _metal);
            Box("Refrigerator", new Vector3(5.5f, 1, 2), new Vector3(0.9f, 2, 0.8f), _white);
            Box("Fridge handle", new Vector3(5.18f, 1.2f, 1.56f), new Vector3(0.04f, 0.55f, 0.05f), _metal);
            Box("Hob", new Vector3(4.3f, 0.97f, 4.4f), new Vector3(0.9f, 0.025f, 0.6f), _metal);
            var faucet = LiquidPrefabBuilder.BuildFaucet();
            faucet.name = "Kitchen faucet";
            faucet.transform.position = new Vector3(2, 0.17f, 4.35f);
            Table("Dining table", new Vector3(3, 0.78f, 2.5f), new Vector2(1.8f, 1.1f));
            for (int i = 0; i < 2; i++)
            {
                Box("Dining chair", new Vector3(2.5f + i, 0.25f, 1.65f), new Vector3(0.5f, 0.5f, 0.5f), _wood);
                Box("Chair back", new Vector3(2.5f + i, 0.75f, 1.4f), new Vector3(0.5f, 0.65f, 0.08f), _wood);
                GameObject cup = LiquidPrefabBuilder.BuildCup();
                cup.name = "Cup " + i;
                cup.transform.position = new Vector3(2.5f + i, 0.83f, 2.5f);
                if (i == 1)
                {
                    var nozzle = cup.GetComponentInChildren<LiquidNozzle>();
                    LiquidSourceBuilder.ApplyPreset(nozzle.profile, LiquidSourceBuilder.JuiceName);
                    UdonSharpEditorUtility.CopyProxyToUdon(nozzle.profile);
                }
            }
            Move("Slime sprayer", new Vector3(3.3f, 1.1f, 4.4f));
            Lighting(pool, "Kitchen", new Vector3(3.5f, 1.5f, 3), new Vector3(4.8f, 3, 3.8f), false);
            Label("KITCHEN / faucet, water + juice cups", new Vector3(3.4f, 1.65f, 4.94f));
        }

        private static void Bathroom(LiquidCanvasPool pool)
        {
            Box("Tub base", new Vector3(4.65f, 0.1f, -3.75f), new Vector3(2.2f, 0.2f, 1.5f), _white);
            foreach (float z in new[] { -4.5f, -3f })
                Box("Tub rim", new Vector3(4.65f, 0.35f, z), new Vector3(2.3f, 0.7f, 0.16f), _white);
            foreach (float x in new[] { 3.5f, 5.8f })
                Box("Tub end", new Vector3(x, 0.35f, -3.75f), new Vector3(0.16f, 0.7f, 1.5f), _white);
            var water = LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.WaterName);
            var bath = LiquidSourceBuilder.CreateImmersionVolume("Bath water", pool, water, new Vector3(2.1f, 0.35f, 1.3f));
            bath.transform.position = new Vector3(4.65f, 0.56f, -3.75f);
            Box("Water surface", new Vector3(4.65f, 0.55f, -3.75f), new Vector3(2.1f, 0.015f, 1.3f), Mat("Water", new Color(0.3f, 0.67f, 0.75f)), false);
            GameObject showerRoot = LiquidPrefabBuilder.BuildShower();
            showerRoot.name = "Bathroom shower";
            showerRoot.transform.position = new Vector3(5, 0, -1.3f);
            var shower = showerRoot.GetComponentInChildren<LiquidShower>();
            shower.startRunning = false;
            var humidity = new GameObject("Bathroom humidity").AddUdonSharpComponent<LiquidHumidity>();
            humidity.transform.position = new Vector3(3.5f, 1.5f, -2);
            humidity.pool = pool;
            humidity.dripProfile = water;
            humidity.areaSize = new Vector3(4.8f, 3, 5.8f);
            humidity.humidity = 0.45f;
            humidity.linkedShower = shower;
            UdonSharpEditorUtility.CopyProxyToUdon(humidity);
            Switch("Shower on/off", new Vector3(5.85f, 1.2f, -0.6f), shower, nameof(LiquidShower.Toggle));
            GameObject sink = LiquidPrefabBuilder.BuildFaucet();
            sink.name = "Washbasin";
            sink.transform.position = new Vector3(2, 0, 0.3f);
            var mirror = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mirror.name = "Bathroom mirror";
            mirror.transform.SetParent(_root, false);
            mirror.transform.position = new Vector3(2, 1.65f, 0.9f);
            mirror.transform.localScale = new Vector3(0.8f, 1, 1);
            Object.DestroyImmediate(mirror.GetComponent<Collider>());
            mirror.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(LiquidSampleScene.MirrorMaterialPath);
            mirror.AddComponent<VRCMirrorReflection>();
            // Keep the mirror outside the liquid projector's environment layer.
            mirror.layer = 18;
            Box("Washing machine", new Vector3(3.1f, 0.45f, 0.45f), new Vector3(0.85f, 0.9f, 0.8f), _white);
            Box("Washer door", new Vector3(3.1f, 0.42f, 0.03f), new Vector3(0.58f, 0.55f, 0.03f), _metal);
            Box("Towel shelf", new Vector3(4.35f, 0.8f, 0.45f), new Vector3(1.4f, 0.1f, 0.8f), _wood);
            Box("Bath stool", new Vector3(2.25f, 0.12f, -4), new Vector3(1.1f, 0.24f, 0.7f), _white);
            for (int i = 0; i < 2; i++)
            {
                Move("Towel " + i, new Vector3(4 + i * 0.6f, 0.9f, 0.4f));
                Move("Water gun " + i, new Vector3(4 + i * 0.6f, 1.05f, 0.15f));
                Move("Water Balloon " + i, new Vector3(2 + i * 0.5f, 0.35f, -4));
                // The existing balloon spawn marker must move with its pickup.
                var balloon = GameObject.Find("Water Balloon " + i).GetComponent<LiquidWaterBalloon>();
                balloon.respawnPoint.position = balloon.transform.position;
                UdonSharpEditorUtility.CopyProxyToUdon(balloon);
            }
            Lighting(pool, "Bath", new Vector3(3.5f, 1.5f, -2), new Vector3(4.8f, 3, 5.8f), false);
            Label("BATH / wash, soak, wipe", new Vector3(3.5f, 2.45f, 0.89f));
        }

        private static void Entrance(LiquidCanvasPool pool)
        {
            Box("Shoe cabinet", new Vector3(-5.2f, 0.55f, -3.5f), new Vector3(1.1f, 1.1f, 2), _white);
            Box("Doormat", new Vector3(-2, 0.02f, -4.2f), new Vector3(1.4f, 0.04f, 0.8f), _fabric);
            Move("Mud sprayer", new Vector3(-5.2f, 1.3f, -3));
            var bucket = LiquidPrefabBuilder.BuildBucket();
            bucket.transform.position = new Vector3(-4, 0.05f, -4.2f);
            for (int i = 0; i < 2; i++)
            {
                var umbrella = LiquidPrefabBuilder.BuildUmbrella();
                umbrella.name = "Entry umbrella " + i;
                umbrella.transform.position = new Vector3(-5.2f, 1.2f, -4 + i * 0.55f);
            }
            var puddle = LiquidSourceBuilder.CreateImmersionVolume("Entry puddle", pool,
                LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.WaterName), new Vector3(1.4f, 0.08f, 0.8f));
            puddle.transform.position = new Vector3(-2, 0.08f, -4.2f);
            var splash = puddle.AddUdonSharpComponent<LiquidSplash>();
            splash.pool = pool; splash.profile = LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.WaterName);
            splash.emissionVolume = puddle.GetComponent<Collider>();
            puddle.GetComponent<LiquidImmersionVolume>().stepSplash = splash;
            UdonSharpEditorUtility.CopyProxyToUdon(splash);
            UdonSharpEditorUtility.CopyProxyToUdon(puddle.GetComponent<LiquidImmersionVolume>());
            var reset = Object.FindObjectOfType<LiquidResetPanel>();
            reset.transform.position = new Vector3(0, 0, -1.6f);
            Lighting(pool, "Entry", new Vector3(-2.5f, 1.5f, -3), new Vector3(6.8f, 3, 3.8f), false);
            Label("ENTRY / mud, bucket, umbrellas\nWet mat: step splash", new Vector3(-4.6f, 1.8f, -4.94f), 180);
        }

        private static void Balcony(LiquidCanvasPool pool)
        {
            Box("Balcony floor", new Vector3(-2.5f, -0.1f, 6.1f), new Vector3(7, 0.2f, 2.2f), _tile);
            Box("Balcony railing", new Vector3(-2.5f, 0.65f, 7.15f), new Vector3(7, 1.3f, 0.1f), _white);
            foreach (float x in new[] { -6f, 1f })
                Box("Balcony side", new Vector3(x, 0.65f, 6.1f), new Vector3(0.1f, 1.3f, 2.2f), _white);
            var rain = LiquidWeatherBuilder.CreateWeather(null, "Balcony rain", pool,
                LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.WaterName), false, new Vector3(6.8f, 5, 2));
            rain.transform.parent.position = new Vector3(-2.5f, 0, 6.1f);
            rain.clearSeconds = 0;
            UdonSharpEditorUtility.CopyProxyToUdon(rain);
        }

        private static void Lighting(LiquidCanvasPool pool, string name, Vector3 centre, Vector3 size, bool uv)
        {
            var zone = new GameObject(name + " lighting").AddUdonSharpComponent<LiquidLightZone>();
            zone.transform.position = centre;
            zone.pool = pool; zone.areaSize = size; zone.automatic = false;
            var light = new GameObject(name + " lamp").AddComponent<Light>();
            light.transform.position = new Vector3(centre.x, 2.75f, centre.z);
            light.type = LightType.Point; light.range = 9; light.intensity = 1.3f;
            zone.lamp = light;
            Box("Ceiling fixture", light.transform.position + Vector3.up * 0.15f, new Vector3(0.8f, 0.1f, 0.8f), _white);
            if (uv)
            {
                var blacklight = new GameObject("Living UV").AddComponent<Light>();
                blacklight.transform.position = light.transform.position;
                blacklight.type = LightType.Point; blacklight.range = 8; blacklight.color = new Color(0.3f, 0.05f, 1);
                zone.blacklight = blacklight;
                Switch("Room light", new Vector3(-0.7f, 1.3f, -0.86f), zone, nameof(LiquidLightZone.ToggleLamp));
                Switch("UV light", new Vector3(-0.7f, 1.1f, -0.86f), zone, nameof(LiquidLightZone.ToggleBlacklight));
            }
            UdonSharpEditorUtility.CopyProxyToUdon(zone);
        }

        private static void Partition(string name, bool alongX, float fixedAxis, float min, float max, float doorMin, float doorMax)
        {
            foreach (Vector2 interval in new[] { new Vector2(min, doorMin), new Vector2(doorMax, max) })
            {
                float mid = (interval.x + interval.y) * 0.5f, length = interval.y - interval.x;
                Box(name, alongX ? new Vector3(mid, 1.5f, fixedAxis) : new Vector3(fixedAxis, 1.5f, mid),
                    alongX ? new Vector3(length, 3, 0.15f) : new Vector3(0.15f, 3, length), _wall);
            }
            float centre = (doorMin + doorMax) * 0.5f;
            Box(name + " lintel", alongX ? new Vector3(centre, 2.65f, fixedAxis) : new Vector3(fixedAxis, 2.65f, centre),
                alongX ? new Vector3(doorMax - doorMin, 0.7f, 0.15f) : new Vector3(0.15f, 0.7f, doorMax - doorMin), _wall);
        }

        private static void Switch(string caption, Vector3 position, UdonSharpBehaviour target, string method)
        {
            var button = LiquidPrefabBuilder.Button(_root, caption, caption, position, _white, target, method);
            button.transform.localScale = new Vector3(0.2f, 0.14f, 0.08f);
        }

        private static void Table(string name, Vector3 top, Vector2 size)
        {
            Box(name, top, new Vector3(size.x, 0.08f, size.y), _wood);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Box(name + " leg", new Vector3(top.x + x * (size.x * 0.5f - 0.1f), top.y * 0.5f, top.z + z * (size.y * 0.5f - 0.1f)), new Vector3(0.09f, top.y, 0.09f), _wood);
        }

        private static void Move(string name, Vector3 position) { GameObject.Find(name).transform.position = position; }
        private static Material Mat(string name, Color colour) { return LiquidAssets.CreateOrLoadSurfaceMaterial(Folder + "/" + name + ".mat", colour, 0.3f); }
        private static void Box(string name, Vector3 position, Vector3 size, Material material, bool solid = true)
        { LiquidPrefabBuilder.Part(_root, name, PrimitiveType.Cube, position, size, Quaternion.identity, material, solid); }
        private static void Label(string text, Vector3 position, float yaw = 0)
        {
            var label = new GameObject(text).AddComponent<TextMesh>();
            label.transform.SetParent(_root, false); label.transform.position = position;
            label.transform.rotation = Quaternion.Euler(0, yaw, 0);
            label.text = text; label.fontSize = 48; label.characterSize = 0.023f;
            label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center; label.color = new Color(0.18f, 0.2f, 0.22f);
        }
    }
}
