using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Components;

namespace SabaProps.Liquid.Editors
{
    /// <summary>Enclosed, shared painting room with independently owned tools.</summary>
    public static class LiquidPlayroomScene
    {
        public const string ScenePath = LiquidSampleScene.SampleFolder + "/LiquidPlayroom.unity";
        private const string Materials = LiquidAssets.MaterialFolder + "/Playroom";

        [MenuItem("Tools/SabaProps/Liquid/Create Playroom Scene")]
        public static void CreateAndOpen()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Create();
        }

        public static void Create()
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            LiquidSampleScene.ConfigureLight();
            LiquidAssets.EnsureFolder(LiquidSampleScene.SampleFolder);
            var pool = LiquidCanvasPoolBuilder.CreateCanvasPool(8).GetComponent<LiquidCanvasPool>();
            Material wall = Material("Wall", new Color(0.78f, 0.79f, 0.74f));
            Material floor = Material("Floor", new Color(0.40f, 0.49f, 0.51f));
            Material trim = Material("Trim", new Color(0.12f, 0.20f, 0.23f));
            Material bench = Material("Bench", new Color(0.59f, 0.43f, 0.28f));
            var room = new GameObject("Playroom architecture").transform;
            Box(room, "Floor", new Vector3(0, -0.1f, 0), new Vector3(12.4f, 0.2f, 10.4f), floor);
            Box(room, "Ceiling", new Vector3(0, 3.7f, 0), new Vector3(12.4f, 0.2f, 10.4f), wall);
            Box(room, "North wall", new Vector3(0, 1.8f, 5.1f), new Vector3(12.4f, 3.6f, 0.2f), wall);
            Box(room, "South wall", new Vector3(0, 1.8f, -5.1f), new Vector3(12.4f, 3.6f, 0.2f), wall);
            Box(room, "West wall", new Vector3(-6.1f, 1.8f, 0), new Vector3(0.2f, 3.6f, 10), wall);
            Box(room, "East wall", new Vector3(6.1f, 1.8f, 0), new Vector3(0.2f, 3.6f, 10), wall);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(room, "Skirting", new Vector3(0, 0.12f, side * 4.97f), new Vector3(12, 0.24f, 0.06f), trim);
                Box(room, "Skirting", new Vector3(side * 5.97f, 0.12f, 0), new Vector3(0.06f, 0.24f, 10), trim);
                Box(room, "Ceiling beam", new Vector3(side * 3, 3.45f, 0), new Vector3(0.16f, 0.3f, 10), trim);
            }
            LiquidPaintingBuilder.CreateSurfaceCanvas(null, "Room Surfaces", new Vector3(0, 1.8f, 0),
                new Vector3(12.4f, 3.8f, 10.4f), LiquidSurfaceBuilder.GetSurface(LiquidSurfaceBuilder.PaintedWallName),
                LiquidSampleScene.CanvasUpdateMaterial(), 950, 256);

            string[] profiles = { LiquidSourceBuilder.RedPaintName, LiquidSourceBuilder.BluePaintName,
                LiquidSourceBuilder.YellowPaintName, LiquidSourceBuilder.FluorescentGreenName };
            Color[] colours = { new Color(0.85f, 0.12f, 0.1f), new Color(0.12f, 0.4f, 0.9f),
                new Color(0.95f, 0.75f, 0.12f), new Color(0.3f, 0.8f, 0.2f) };
            for (int i = 0; i < profiles.Length; i++)
            {
                float x = -4.5f + i * 3;
                string folder = Materials + "/Station" + i;
                Box(room, "Workbench " + i, new Vector3(x, 0.8f, 4.25f), new Vector3(2.5f, 0.12f, 0.85f), bench);
                for (int leg = -1; leg <= 1; leg += 2)
                    Box(room, "Bench leg", new Vector3(x + leg, 0.38f, 4.25f), new Vector3(0.12f, 0.76f, 0.65f), trim);
                Box(room, "Station colour", new Vector3(x, 1.7f, 4.96f), new Vector3(2.4f, 0.12f, 0.03f), Material("Colour" + i, colours[i]));
                LiquidProfile profile = LiquidSourceBuilder.GetProfile(profiles[i]);
                Spray(pool, profiles[i] + " sprayer", profile, colours[i], new Vector3(x - 0.7f, 1.05f, 4.2f), folder);
                GameObject pen = LiquidPaintingBuilder.CreatePaintTool("Pen " + i, LiquidPaintTool.ModePen, profile,
                    LiquidBodyCanvas.ShapeStroke, 0.035f, folder);
                pen.transform.position = new Vector3(x - 0.15f, 0.94f, 4.2f);
                GameObject stamp = LiquidPaintingBuilder.CreatePaintTool("Star stamp " + i, LiquidPaintTool.ModeStamp, profile,
                    LiquidBodyCanvas.ShapeStar, 0.12f, folder);
                stamp.transform.position = new Vector3(x + 0.25f, 0.94f, 4.2f);
                LiquidContactScene.CreateWipeTool(pool, "Sponge " + i, new Vector3(x + 0.8f, 0.94f, 4.2f), false);
                Label(profiles[i] + "\nSpray / Pen / Stamp / Sponge", new Vector3(x, 2.15f, 4.94f), 0.028f);
            }
            Box(room, "Wash bench", new Vector3(-4.1f, 0.8f, -3.5f), new Vector3(3, 0.12f, 1), bench);
            Box(room, "Wash cabinet", new Vector3(-4.1f, 0.38f, -3.5f), new Vector3(2.8f, 0.76f, 0.8f), trim);
            for (int i = 0; i < 2; i++)
            {
                GameObject gun = LiquidSourceBuilder.CreateWaterGun(pool, LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.WaterName), Materials + "/Wash" + i);
                gun.name = "Water gun " + i;
                gun.transform.position = new Vector3(-5 + i * 0.6f, 1.05f, -3.5f);
                LiquidPrefabBuilder.SetLayer(gun, 13);
                LiquidContactScene.CreateWipeTool(pool, "Towel " + i, new Vector3(-3.7f + i * 0.55f, 0.94f, -3.5f), true);
                GameObject balloon = LiquidContactScene.CreateBalloon(pool, new Vector3(3.6f + i * 0.65f, 1.05f, -3.5f));
                balloon.name = "Water Balloon " + i;
            }
            Box(room, "Splash bench", new Vector3(4.1f, 0.8f, -3.5f), new Vector3(3, 0.12f, 1), bench);
            Box(room, "Splash cabinet", new Vector3(4.1f, 0.38f, -3.5f), new Vector3(2.8f, 0.76f, 0.8f), trim);
            Spray(pool, "Mud sprayer", LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.MudName), new Color(0.38f, 0.24f, 0.12f), new Vector3(3, 1.05f, -3.5f), Materials + "/Mud");
            Spray(pool, "Slime sprayer", LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.SlimeName), Color.green, new Vector3(5.15f, 1.05f, -3.5f), Materials + "/Slime");
            LiquidPrefabBuilder.CreateResetPanel(null, pool, Materials).transform.position = new Vector3(0, 0, 4.5f);
            Label("LIQUID PLAYROOM\nPick a colour. Paint each other and the room.", new Vector3(0, 3.05f, 4.94f), 0.042f);
            Label("WATER + TOWELS", new Vector3(-4.1f, 1.6f, -3.45f), 0.035f);
            Label("MUD / BALLOONS / SLIME", new Vector3(4.1f, 1.6f, -3.45f), 0.032f);
            for (int i = 0; i < 4; i++)
            {
                Vector3 position = new Vector3(i % 2 == 0 ? -3 : 3, 3.45f, i < 2 ? -2.5f : 2.5f);
                Box(room, "Ceiling light", position, new Vector3(1.6f, 0.08f, 0.35f), Material("Lamp", Color.white));
                var lamp = new GameObject("Room light").AddComponent<Light>();
                lamp.transform.position = position - Vector3.up * 0.25f;
                lamp.type = LightType.Point;
                lamp.range = 10;
                lamp.intensity = 1.3f;
                lamp.shadows = LightShadows.None;
            }
            LiquidPaintingBuilder.Register(pool);
            LiquidContactBuilder.EnableContact();
            LiquidSampleScene.BuildWorld(new Vector3(0, 0.05f, -3));
            var movement = Object.FindObjectOfType<LiquidDemoMovement>();
            movement.walkSpeed = 2;
            movement.runSpeed = 4;
            movement.strafeSpeed = 2;
            UdonSharpEditorUtility.CopyProxyToUdon(movement);
            var descriptor = Object.FindObjectOfType<VRCSceneDescriptor>();
            descriptor.spawns = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                var spawn = new GameObject("Room spawn " + i).transform;
                spawn.SetParent(descriptor.transform);
                spawn.position = new Vector3(-1.5f + i, 0.05f, -2.5f);
                descriptor.spawns[i] = spawn;
            }
            Camera.main.transform.position = new Vector3(0, 2, -4.5f);
            Camera.main.transform.LookAt(new Vector3(0, 1.3f, 3));
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
        }

        private static void Spray(LiquidCanvasPool pool, string name, LiquidProfile profile, Color colour, Vector3 position, string folder)
        {
            GameObject root = LiquidPrefabBuilder.CreatePortableNozzle(name, LiquidNozzle.ModeHold, profile, colour, folder);
            root.transform.position = position;
            var nozzle = root.GetComponentInChildren<LiquidNozzle>();
            nozzle.pool = pool;
            UdonSharpEditorUtility.CopyProxyToUdon(nozzle);
        }

        private static Material Material(string name, Color colour)
        {
            return LiquidAssets.CreateOrLoadSurfaceMaterial(Materials + "/" + name + ".mat", colour, 0.25f);
        }

        private static void Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            LiquidPrefabBuilder.Part(parent, name, PrimitiveType.Cube, position, size, Quaternion.identity, material, true);
        }

        private static void Label(string text, Vector3 position, float size)
        {
            var label = new GameObject(text).AddComponent<TextMesh>();
            label.transform.position = position;
            if (position.z < 0) label.transform.rotation = Quaternion.Euler(0, 180, 0);
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 48;
            label.characterSize = size;
            label.color = new Color(0.1f, 0.16f, 0.18f);
        }
    }
}
