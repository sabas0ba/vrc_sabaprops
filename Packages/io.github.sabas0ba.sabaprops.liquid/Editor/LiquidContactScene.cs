using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Components;

namespace SabaProps.Liquid.Editors
{
    public static class LiquidContactScene
    {
        public const string ScenePath = LiquidSampleScene.SampleFolder + "/LiquidContact.unity";
        private const string Materials = LiquidAssets.MaterialFolder + "/Contact";

        [MenuItem("GameObject/SabaProps/Liquid/Contact/Sponge", false, 20)]
        public static void SpongeMenu(MenuCommand command) { PlaceWiper(command, false); }

        [MenuItem("GameObject/SabaProps/Liquid/Contact/Towel", false, 21)]
        public static void TowelMenu(MenuCommand command) { PlaceWiper(command, true); }

        private static void PlaceWiper(MenuCommand command, bool towel)
        {
            GameObject root = CreateWipeTool(LiquidSourceBuilder.FindOrCreatePool(), towel ? "Towel" : "Sponge", Vector3.zero, towel);
            GameObjectUtility.SetParentAndAlign(root, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(root, "Create Liquid Wipe Tool");
            LiquidContactBuilder.EnableContact();
            Selection.activeGameObject = root;
        }

        [MenuItem("GameObject/SabaProps/Liquid/Contact/Water Balloon", false, 22)]
        public static void BalloonMenu(MenuCommand command)
        {
            GameObject root = CreateBalloon(LiquidSourceBuilder.FindOrCreatePool(), Vector3.up);
            Undo.RegisterCreatedObjectUndo(root, "Create Water Balloon");
            Selection.activeGameObject = root;
        }

        [MenuItem("Tools/SabaProps/Liquid/Create Contact Scene")]
        public static void CreateAndOpen()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Create();
        }

        public static void Create()
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            LiquidSampleScene.ConfigureLight();
            LiquidAssets.EnsureFolder(LiquidSampleScene.SampleFolder);
            var pool = LiquidCanvasPoolBuilder.CreateCanvasPool(4).GetComponent<LiquidCanvasPool>();
            Material wall = LiquidAssets.CreateOrLoadSurfaceMaterial(Materials + "/Wall.mat", new Color(0.7f, 0.72f, 0.75f), 0.2f);
            Part("Floor", new Vector3(0, -0.1f, 0), new Vector3(12, 0.2f, 12), wall);
            Part("Paint here", new Vector3(-1.7f, 1.4f, 2), new Vector3(3, 2.8f, 0.15f), wall);
            Part("Transfer here", new Vector3(1.7f, 1.4f, 2), new Vector3(3, 2.8f, 0.15f), wall);
            LiquidPaintingBuilder.CreateSurfaceCanvas(null, "Contact Surfaces", new Vector3(0, 1.4f, 0),
                new Vector3(12, 3, 12), LiquidSurfaceBuilder.GetSurface(LiquidSurfaceBuilder.PaintedWallName),
                LiquidSampleScene.CanvasUpdateMaterial(), 900, 256);
            Part("Tool Table", new Vector3(0, 0.7f, -0.5f), new Vector3(3, 0.1f, 0.7f), wall);
            LiquidProfile paint = LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.RedPaintName);
            LiquidProfile glow = LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.FluorescentGreenName);
            GameObject pen = LiquidPaintingBuilder.CreatePaintTool("Wet red pen", LiquidPaintTool.ModePen, paint,
                LiquidBodyCanvas.ShapeStroke, 0.035f, Materials);
            pen.transform.position = new Vector3(-1, 0.8f, -0.5f);
            GameObject glowPen = LiquidPaintingBuilder.CreatePaintTool("Fluorescent pen", LiquidPaintTool.ModePen, glow,
                LiquidBodyCanvas.ShapeStroke, 0.035f, Materials);
            glowPen.transform.position = new Vector3(-0.6f, 0.8f, -0.5f);
            CreateWipeTool(pool, "Sponge", new Vector3(0, 0.8f, -0.5f), false);
            CreateWipeTool(pool, "Towel", new Vector3(0.5f, 0.8f, -0.5f), true);
            CreateBalloon(pool, new Vector3(1, 1, -0.5f));
            GameObject puddle = LiquidSourceBuilder.CreateImmersionVolume("Step Splash", pool,
                LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.WaterName), new Vector3(2, 0.15f, 2));
            puddle.transform.position = new Vector3(-3, 0.12f, -2);
            Material water = LiquidAssets.CreateOrLoadSurfaceMaterial(Materials + "/Puddle.mat", new Color(0.15f, 0.45f, 0.65f), 0.8f);
            LiquidPrefabBuilder.Part(null, "Shallow water marker", PrimitiveType.Cube, new Vector3(-3, 0.01f, -2),
                new Vector3(2, 0.01f, 2), Quaternion.identity, water, false);
            Label("Walk here: shallow water", new Vector3(-4, 0.35f, -2), 0.025f);
            var splash = puddle.AddUdonSharpComponent<LiquidSplash>();
            splash.pool = pool;
            splash.profile = LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.WaterName);
            splash.emissionVolume = puddle.GetComponent<Collider>();
            puddle.GetComponent<LiquidImmersionVolume>().stepSplash = splash;
            UdonSharpEditorUtility.CopyProxyToUdon(puddle.GetComponent<LiquidImmersionVolume>());
            UdonSharpEditorUtility.CopyProxyToUdon(splash);
            LiquidPaintingBuilder.Register(pool);
            LiquidContactBuilder.EnableContact();
            var zoneObject = new GameObject("Contact light cycle");
            zoneObject.transform.position = new Vector3(0, 1.4f, 0);
            var zone = zoneObject.AddUdonSharpComponent<LiquidLightZone>();
            zone.pool = pool;
            zone.areaSize = new Vector3(12, 6, 12);
            var lamp = new GameObject("Lamp");
            lamp.transform.SetParent(zoneObject.transform, false);
            lamp.transform.localPosition = Vector3.up * 2;
            zone.lamp = lamp.AddComponent<Light>();
            zone.lamp.type = LightType.Point;
            zone.lamp.range = 12;
            var ultraviolet = new GameObject("UV");
            ultraviolet.transform.SetParent(lamp.transform, false);
            zone.blacklight = ultraviolet.AddComponent<Light>();
            zone.blacklight.type = LightType.Point;
            zone.blacklight.range = 12;
            zone.blacklight.color = new Color(0.2f, 0, 1);
            UdonSharpEditorUtility.CopyProxyToUdon(zone);
            var contact = pool.GetComponent<LiquidContact>();
            Button("Hide liquid locally", new Vector3(-1, 1, -2), contact, nameof(LiquidContact.HideLiquid), wall);
            Button("Show liquid locally", new Vector3(1, 1, -2), contact, nameof(LiquidContact.ShowLiquid), wall);
            Label("Draw, rub with a hand, then touch the other wall.\nSponge / towel: wipe. Balloon: throw.\nCheck ordinary and glowing paint in both eyes.", new Vector3(-3, 3, 1.8f));
            LiquidSampleScene.BuildWorld(new Vector3(0, 0.05f, -4));
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
        }

        public static GameObject CreateWipeTool(LiquidCanvasPool pool, string name, Vector3 position, bool towel)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Cloth";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = towel ? new Vector3(0.22f, 0.03f, 0.16f) : new Vector3(0.1f, 0.05f, 0.08f);
            visual.GetComponent<Renderer>().sharedMaterial = LiquidAssets.CreateOrLoadSurfaceMaterial(
                Materials + "/" + name + ".mat", towel ? Color.white : Color.yellow, 0.1f);
            LiquidPrefabBuilder.SetLayer(root, 13);
            LiquidPrefabBuilder.MakePickup(root, "Wipe", null, "");
            var tip = new GameObject("Contact tip");
            tip.transform.SetParent(root.transform, false);
            tip.transform.localPosition = new Vector3(0, 0, towel ? 0.09f : 0.05f);
            var child = new GameObject("Wipe");
            child.transform.SetParent(root.transform, false);
            var tool = child.AddUdonSharpComponent<LiquidWipeTool>();
            tool.pool = pool;
            tool.pickup = root.GetComponent<VRCPickup>();
            tool.tip = tip.transform;
            tool.radius = towel ? 0.11f : 0.055f;
            tool.redeposit = towel ? 0.02f : 0.08f;
            var canvasObject = new GameObject("Cloth Canvas");
            canvasObject.transform.SetParent(root.transform, false);
            var canvas = canvasObject.AddUdonSharpComponent<LiquidBodyCanvas>();
            canvas.anchor = root.transform;
            canvas.canvasRoot = canvasObject.transform;
            canvas.centerOffset = Vector3.zero;
            canvas.halfExtents = Vector3.one * 0.18f;
            canvas.faceResolution = 64;
            canvas.estimateRegions = false;
            canvas.bodySurface = LiquidSurfaceBuilder.GetSurface(LiquidSurfaceBuilder.SoftClothName);
            canvas.updateMaterial = LiquidSampleScene.CanvasUpdateMaterial();
            canvas.contactMaterial = LiquidAssets.CreateOrLoadMaterial(LiquidAssets.MaterialFolder + "/LiquidContact.mat", "Hidden/SabaProps/Liquid/Contact");
            // A separate material per tool instance is necessary for projector state.
            string path = AssetDatabase.GenerateUniqueAssetPath(Materials + "/" + name + "Projector.mat");
            canvas.projectorMaterial = LiquidAssets.CreateOrLoadMaterial(path, LiquidAssets.BodyProjectorShader);
            canvas.projectorObject = new GameObject("Projector");
            canvas.projectorObject.transform.SetParent(canvasObject.transform, false);
            LiquidCanvasPoolBuilder.ConfigureProjector(canvas.projectorObject, canvas.halfExtents, canvas.projectorMaterial, 13, ~(1 << 13));
            tool.clothCanvas = canvas;
            UdonSharpEditorUtility.CopyProxyToUdon(canvas);
            UdonSharpEditorUtility.CopyProxyToUdon(tool);
            return root;
        }

        public static GameObject CreateBalloon(LiquidCanvasPool pool, Vector3 position)
        {
            var root = new GameObject("Water Balloon");
            root.transform.position = position;
            root.AddComponent<SphereCollider>().radius = 0.08f;
            LiquidPrefabBuilder.MakePickup(root, "Throw", null, "");
            var body = root.GetComponent<Rigidbody>();
            body.isKinematic = false;
            body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = Vector3.one * 0.16f;
            visual.GetComponent<Renderer>().sharedMaterial = LiquidAssets.CreateOrLoadSurfaceMaterial(Materials + "/Balloon.mat", Color.cyan, 0.6f);
            var child = new GameObject("Splash");
            child.transform.SetParent(root.transform, false);
            var splash = child.AddUdonSharpComponent<LiquidSplash>();
            splash.pool = pool;
            splash.profile = LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.WaterName);
            splash.authority = root;
            var balloon = root.AddUdonSharpComponent<LiquidWaterBalloon>();
            balloon.splash = splash;
            balloon.pickup = root.GetComponent<VRCPickup>();
            balloon.body = body;
            balloon.balloonCollider = root.GetComponent<Collider>();
            balloon.visual = visual;
            var spawn = new GameObject("Balloon respawn");
            spawn.transform.position = position;
            balloon.respawnPoint = spawn.transform;
            UdonSharpEditorUtility.CopyProxyToUdon(splash);
            UdonSharpEditorUtility.CopyProxyToUdon(balloon);
            LiquidPrefabBuilder.SetLayer(root, 13);
            return root;
        }

        private static GameObject Part(string name, Vector3 position, Vector3 scale, Material material)
        {
            return LiquidPrefabBuilder.Part(null, name, PrimitiveType.Cube, position, scale, Quaternion.identity, material, true);
        }

        private static void Label(string text, Vector3 position, float characterSize = 0.07f)
        {
            var label = new GameObject("Instructions");
            label.transform.position = position;
            var mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.characterSize = characterSize;
            mesh.fontSize = 48;
        }

        private static void Button(string name, Vector3 position, LiquidContact contact, string method, Material material)
        {
            GameObject root = Part(name, position, new Vector3(0.6f, 0.2f, 0.15f), material);
            var button = root.AddUdonSharpComponent<LiquidButton>();
            button.target = contact;
            button.eventName = method;
            UdonSharpEditorUtility.CopyProxyToUdon(button);
            Label(name.Replace(" locally", "\n(local only)"), position + new Vector3(-0.3f, 0.25f, -0.1f), 0.02f);
        }
    }
}
