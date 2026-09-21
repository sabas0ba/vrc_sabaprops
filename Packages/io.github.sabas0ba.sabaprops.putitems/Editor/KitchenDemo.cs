using System;
using System.IO;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using VRC.SDK3.Components;
using static SabaProps.PutItems.Editors.KitchenDemoGeometry;

namespace SabaProps.PutItems.Editors
{
    public static class KitchenDemo
    {
        public const string GeneratedRoot = "Assets/SabaProps/PutItemsKitchenGenerated";
        public const string SceneName = "PutItemsKitchen.unity";
        private static Material oak, oakLight, cream, sage, dark, brass, steel, terra, paper, blue;
        private static Transform room;
        private static PlacementSurface table, fridge;

        // 配布用の再生成は専用プロジェクトでのみ実行します。
        public static void Generate()
        {
            ConfigureLayers();
            PlacementPrograms.Prepare();
            Directory.CreateDirectory(GeneratedRoot + "/Materials");
            Directory.CreateDirectory(GeneratedRoot + "/Meshes");
            AssetDatabase.Refresh();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            oak = Material("Warm oak", new Color(0.52f, 0.31f, 0.16f));
            oakLight = Material("Light oak", new Color(0.67f, 0.47f, 0.28f));
            cream = Material("Porcelain", new Color(0.91f, 0.88f, 0.78f), 0f, 0.5f);
            sage = Material("Sage enamel", new Color(0.29f, 0.45f, 0.38f), 0.12f, 0.48f);
            dark = Material("Charcoal", new Color(0.055f, 0.085f, 0.075f));
            brass = Material("Brass", new Color(0.77f, 0.53f, 0.19f), 0.7f, 0.65f);
            steel = Material("Cutlery steel", new Color(0.66f, 0.72f, 0.73f), 0.78f, 0.65f);
            terra = Material("Terracotta glaze", new Color(0.65f, 0.25f, 0.13f), 0f, 0.55f);
            paper = Material("Note paper", new Color(0.97f, 0.92f, 0.72f));
            blue = Material("Blue ceramic", new Color(0.18f, 0.37f, 0.51f), 0f, 0.55f);
            room = Group("Put Items / Kitchen Demo", null, Vector3.zero);
            BuildRoom();
            BuildTable();
            BuildFridge();
            BuildTableware();
            BuildServingTray();
            BuildMagnetItems();
            BuildLightingAndWorld();
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), GeneratedRoot + "/" + SceneName);
            Debug.Log("[Put Items] Kitchen demo generated.");
        }

        private static void ConfigureLayers()
        {
            Type layers = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                layers = assembly.GetType("UpdateLayers");
                if (layers != null) break;
            }
            if (layers == null) throw new InvalidOperationException("VRChat SDK layer configuration is unavailable.");
            layers.GetMethod("SetupEditorLayers").Invoke(null, null);
            layers.GetMethod("SetupCollisionLayerMatrix").Invoke(null, null);
            if (LayerMask.NameToLayer("Walkthrough") < 0)
                throw new InvalidOperationException("VRChat Walkthrough layer is unavailable.");
        }

        private static void BuildRoom()
        {
            Transform architecture = Group("Room and kitchen cabinetry", room, Vector3.zero);
            Box("Floor collider", architecture, new Vector3(0f, -0.09f, 0.8f), new Vector3(7.2f, 0.16f, 6f), oak, true);
            for (int x = 0; x < 18; x++)
                Box("Oak floorboard " + x, architecture, new Vector3(-3.4f + x * 0.4f, -0.004f, 0.8f),
                    new Vector3(0.394f, 0.012f, 6f), x % 3 == 0 ? oakLight : oak);
            Box("Back wall", architecture, new Vector3(0f, 1.5f, 3.85f), new Vector3(7.2f, 3f, 0.12f), cream, true);
            Box("Left wall", architecture, new Vector3(-3.65f, 1.5f, 0.8f), new Vector3(0.12f, 3f, 6f), cream, true);
            Box("Back skirting", architecture, new Vector3(0f, 0.08f, 3.75f), new Vector3(7.2f, 0.16f, 0.05f), oakLight);
            Box("Counter cabinet", architecture, new Vector3(-1.45f, 0.43f, 3.31f), new Vector3(3.4f, 0.86f, 0.85f), sage, true);
            Box("Countertop", architecture, new Vector3(-1.45f, 0.89f, 3.28f), new Vector3(3.48f, 0.06f, 0.91f), cream, true);
            for (int i = 0; i < 5; i++)
            {
                float x = -2.79f + i * 0.67f;
                Box("Cabinet door", architecture, new Vector3(x, 0.44f, 2.87f), new Vector3(0.64f, 0.72f, 0.03f), sage);
                Box("Cabinet pull", architecture, new Vector3(x, 0.67f, 2.83f), new Vector3(0.19f, 0.018f, 0.022f), brass);
            }
            Box("Backsplash", architecture, new Vector3(-1.45f, 1.18f, 3.76f), new Vector3(3.5f, 0.5f, 0.03f), sage);
            Box("Sink rim", architecture, new Vector3(-1.6f, 0.926f, 3.23f), new Vector3(0.64f, 0.017f, 0.43f), steel);
            Box("Sink inset", architecture, new Vector3(-1.6f, 0.937f, 3.23f), new Vector3(0.55f, 0.008f, 0.34f), dark);
            Vector3[] faucet = { new Vector3(-1.6f, 0.93f, 3.53f), new Vector3(-1.6f, 1.21f, 3.53f),
                new Vector3(-1.6f, 1.26f, 3.47f), new Vector3(-1.6f, 1.26f, 3.28f), new Vector3(-1.6f, 1.2f, 3.24f) };
            MeshObject("Faucet", architecture, Tube("Faucet", faucet, 0.016f, Vector3.right), steel);
            Transform vase = Group("Herb pot", architecture, new Vector3(-2.8f, 0.93f, 3.26f));
            MeshObject("Pot", vase, Lathe("Herb pot", new[] { new Vector2(0, 0), new Vector2(0.07f, 0),
                new Vector2(0.09f, 0.16f), new Vector2(0.075f, 0.16f), new Vector2(0.06f, 0.025f), new Vector2(0, 0.025f) }), terra);
            for (int i = 0; i < 7; i++)
            {
                float angle = i * 2.4f;
                Primitive("Herb leaf", PrimitiveType.Sphere, vase, new Vector3(Mathf.Sin(angle) * 0.06f, 0.2f + i * 0.01f, Mathf.Cos(angle) * 0.06f),
                    new Vector3(0.085f, 0.16f, 0.04f), sage).transform.localRotation = Quaternion.Euler(15f, i * 51f, i * 13f);
            }
            Box("Kitchen shelf", architecture, new Vector3(-1.3f, 1.75f, 3.55f), new Vector3(3f, 0.05f, 0.4f), oakLight);
            for (int i = 0; i < 3; i++)
                Primitive("Storage jar", PrimitiveType.Cylinder, architecture, new Vector3(-2.3f + i * 0.27f, 1.91f, 3.52f),
                    new Vector3(0.18f, 0.14f, 0.18f), i == 0 ? terra : cream);
            Label("Demo title", architecture, "PUT ITEMS", new Vector3(-1.1f, 2.53f, 3.74f), 0.3f, dark.color);
            Label("Demo subtitle", architecture, "A PLACE FOR EVERYTHING", new Vector3(-1.1f, 2.25f, 3.73f), 0.065f, sage.color);
            BuildInstructions(architecture, "01  /  TABLE", "Pick up a cup, plate or utensil.\nBring it close to the table and release.", new Vector3(-2.8f, 1.35f, 1.9f));
            BuildInstructions(architecture, "02  /  FRIDGE", "Move a magnet note or key charm.\nBring its back close to the door and release.", new Vector3(2.16f, 2.42f, 2.61f));
            BuildInstructions(architecture, "03  /  TRAY", "Lift the tray, then its plate.\nFood and cutlery follow their carrier.", new Vector3(0.1f, 1.6f, 1.9f));
        }

        private static void BuildInstructions(Transform parent, string title, string message, Vector3 position)
        {
            Transform sign = Group(title + " instructions", parent, position);
            if (title.StartsWith("01", StringComparison.Ordinal))
            {
                Box("Sign stand", sign, new Vector3(0, -0.68f, 0.035f), new Vector3(0.035f, 1.34f, 0.035f), dark);
                Box("Sign foot", sign, new Vector3(0, -1.33f, 0.035f), new Vector3(0.45f, 0.035f, 0.32f), dark);
            }
            else
                foreach (float x in new[] { -0.42f, 0.42f })
                    Box("Sign bracket", sign, new Vector3(x, -0.31f, 0.03f), new Vector3(0.025f, 0.24f, 0.025f), brass);
            Box("Sign board", sign, Vector3.zero, new Vector3(1.45f, 0.48f, 0.025f), dark);
            Label("Zone", sign, title, new Vector3(0, 0.135f, -0.019f), 0.1f, new Color(0.91f, 0.76f, 0.44f));
            Label("How to use", sign, message, new Vector3(0, -0.055f, -0.02f), 0.038f, Color.white);
        }

        private static void BuildTable()
        {
            Transform furniture = Group("Dining table", room, new Vector3(-0.9f, 0f, 0.6f));
            furniture.gameObject.layer = LayerMask.NameToLayer("Walkthrough");
            MeshObject("Rounded oak tabletop", furniture, RoundedBlock("Tabletop", 2.2f, 1.2f, 0.055f, 0.12f), oakLight)
                .transform.localPosition = new Vector3(0, 0.705f, 0);
            Box("Tabletop collider", furniture, new Vector3(0, 0.7325f, 0), new Vector3(2.13f, 0.055f, 1.13f), oakLight, true)
                .GetComponent<Renderer>().enabled = false;
            foreach (float x in new[] { -0.91f, 0.91f })
                foreach (float z in new[] { -0.42f, 0.42f })
                    Box("Table leg", furniture, new Vector3(x, 0.35f, z), new Vector3(0.065f, 0.7f, 0.065f), oak, true);
            table = Surface("Tabletop placement area", furniture, new Vector3(0, 0.762f, 0), Quaternion.identity, new Vector2(2.04f, 1.04f), 1);
            SetColliderLayer(furniture, "Walkthrough");
            foreach (float x in new[] { -1.57f, -0.23f })
            {
                Chair(new Vector3(x, 0, -0.4f), Quaternion.identity);
                Chair(new Vector3(x, 0, 1.6f), Quaternion.Euler(0, 180f, 0));
            }
            Transform coaster = Group("Table instruction card", furniture, new Vector3(0, 0.766f, -0.49f));
            Box("Card", coaster, Vector3.zero, new Vector3(0.82f, 0.005f, 0.16f), cream);
            TextMesh label = Label("Instruction", coaster, "PICK UP  >  BRING CLOSE  >  RELEASE", new Vector3(0, 0.004f, 0), 0.028f, dark.color);
            label.transform.localRotation = Quaternion.Euler(90f, 0, 0);
        }

        private static void Chair(Vector3 position, Quaternion rotation)
        {
            Transform chair = Group("Dining chair", room, position);
            chair.localRotation = rotation;
            MeshObject("Seat", chair, RoundedBlock("Chair seat", 0.44f, 0.44f, 0.045f, 0.06f), oakLight)
                .transform.localPosition = new Vector3(0, 0.42f, 0);
            Box("Seat collider", chair, new Vector3(0, 0.44f, 0), new Vector3(0.43f, 0.05f, 0.43f), oakLight, true)
                .GetComponent<Renderer>().enabled = false;
            foreach (float x in new[] { -0.17f, 0.17f })
                foreach (float z in new[] { -0.17f, 0.17f })
                    Box("Chair leg", chair, new Vector3(x, 0.21f, z), new Vector3(0.04f, 0.42f, 0.04f), oak, true);
            foreach (float x in new[] { -0.18f, 0.18f })
                Box("Back upright", chair, new Vector3(x, 0.66f, -0.18f), new Vector3(0.04f, 0.49f, 0.04f), oak, true);
            Box("Backrest", chair, new Vector3(0, 0.8f, -0.185f), new Vector3(0.43f, 0.17f, 0.045f), oakLight, true);
            SetColliderLayer(chair, "Walkthrough");
        }

        private static void SetColliderLayer(Transform root, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                collider.gameObject.layer = layer;
        }

        private static void BuildFridge()
        {
            Transform body = Group("Refrigerator", room, new Vector3(2.15f, 0, 3f));
            Box("Refrigerator body", body, new Vector3(0, 1.03f, 0), new Vector3(1.04f, 2.06f, 0.8f), sage, true);
            Box("Door seal", body, new Vector3(0, 1.04f, -0.41f), new Vector3(1f, 1.98f, 0.035f), dark);
            RoundedDoor("Main door", body, new Vector3(0, 0.75f, -0.44f), 1f, 1.38f);
            RoundedDoor("Freezer door", body, new Vector3(0, 1.78f, -0.44f), 1f, 0.63f);
            Box("Main handle", body, new Vector3(-0.4f, 1.07f, -0.515f), new Vector3(0.026f, 0.45f, 0.036f), brass);
            Box("Freezer handle", body, new Vector3(-0.4f, 1.76f, -0.515f), new Vector3(0.026f, 0.23f, 0.036f), brass);
            Label("Fridge badge", body, "S A B A", new Vector3(0.15f, 1.79f, -0.505f), 0.028f, cream.color);
            fridge = Surface("Fridge door placement area", body, new Vector3(0.07f, 0.85f, -0.502f),
                Quaternion.Euler(-90, 0, 0), new Vector2(0.82f, 1.22f), 2);
        }

        private static void RoundedDoor(string name, Transform parent, Vector3 center, float width, float height)
        {
            GameObject door = MeshObject(name, parent, RoundedBlock(name, width, height, 0.06f, 0.06f), sage);
            door.transform.localPosition = center;
            door.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            Box(name + " collider", parent, center + new Vector3(0, 0, -0.03f), new Vector3(width, height, 0.06f), sage, true)
                .GetComponent<Renderer>().enabled = false;
        }

        private static PlacementSurface Surface(string name, Transform parent, Vector3 position,
            Quaternion rotation, Vector2 size, int category)
        {
            Transform transform = Group(name, parent, position);
            transform.localRotation = rotation;
            PlacementSurface surface = UdonSharpUndo.AddComponent<PlacementSurface>(transform.gameObject);
            surface.size = size;
            surface.acceptedCategories = category;
            UdonSharpEditorUtility.CopyProxyToUdon(surface);
            return surface;
        }

        private static Transform Pickup(string name, Vector3 position, Quaternion rotation, PlacementSurface surface,
            int category, Vector3 colliderSize, Vector3 colliderCenter, float radius)
        {
            Transform item = Group(name, room, position);
            item.localRotation = rotation;
            BoxCollider collider = item.gameObject.AddComponent<BoxCollider>();
            collider.size = colliderSize;
            collider.center = colliderCenter;
            Rigidbody body = item.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            item.gameObject.AddComponent<VRCPickup>();
            VRCObjectSync sync = item.gameObject.AddComponent<VRCObjectSync>();
            sync.AllowCollisionOwnershipTransfer = false;
            PlacementSolver solver = UdonSharpUndo.AddComponent<PlacementSolver>(item.gameObject);
            solver.surfaces = new[] { surface };
            solver.footprintRadius = radius;
            ObjectSyncPlacement placement = UdonSharpUndo.AddComponent<ObjectSyncPlacement>(item.gameObject);
            placement.solver = solver;
            placement.contact = item;
            placement.category = category;
            placement.normalKinematic = true;
            placement.normalGravity = false;
            UdonSharpEditorUtility.CopyProxyToUdon(solver);
            UdonSharpEditorUtility.CopyProxyToUdon(placement);
            return item;
        }

        private static void BuildTableware()
        {
            Mesh cup = Lathe("Hollow mug", new[] { new Vector2(0, 0), new Vector2(0.044f, 0), new Vector2(0.05f, 0.012f),
                new Vector2(0.054f, 0.116f), new Vector2(0.051f, 0.123f), new Vector2(0.047f, 0.123f),
                new Vector2(0.045f, 0.116f), new Vector2(0.041f, 0.016f), new Vector2(0, 0.016f) });
            var arc = new Vector3[25];
            for (int i = 0; i < arc.Length; i++)
            {
                float angle = (-90f + i * 180f / (arc.Length - 1)) * Mathf.Deg2Rad;
                arc[i] = new Vector3(0.047f + 0.042f * Mathf.Cos(angle), 0.066f + 0.04f * Mathf.Sin(angle), 0);
            }
            Mesh handle = Tube("Mug handle", arc, 0.008f, Vector3.forward);
            Mesh plate = Lathe("Dinner plate", new[] { new Vector2(0, 0), new Vector2(0.083f, 0), new Vector2(0.089f, 0.008f),
                new Vector2(0.136f, 0.027f), new Vector2(0.139f, 0.032f), new Vector2(0.136f, 0.036f),
                new Vector2(0.126f, 0.032f), new Vector2(0.085f, 0.015f), new Vector2(0, 0.014f) });
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -1.46f : -0.4f;
                Transform dish = Pickup("Dinner plate " + (i + 1), new Vector3(x, 0.762f, 0.4f), Quaternion.identity, table, 1,
                    new Vector3(0.28f, 0.04f, 0.28f), new Vector3(0, 0.02f, 0), 0.14f);
                MeshObject("Porcelain dish", dish, plate, cream);
                Transform mug = Pickup(i == 0 ? "Terracotta mug - try placing me" : "Blue mug", new Vector3(x + 0.24f, i == 0 ? 0.825f : 0.762f, 0.88f),
                    i == 0 ? Quaternion.Euler(14, -35, 10) : Quaternion.Euler(0, 35, 0), table, 1,
                    new Vector3(0.155f, 0.125f, 0.11f), new Vector3(0.021f, 0.0625f, 0), 0.095f);
                MeshObject("Hollow ceramic body", mug, cup, i == 0 ? terra : blue);
                MeshObject("Handle", mug, handle, i == 0 ? terra : blue);
                Cutlery("Fork " + (i + 1), new Vector3(x - 0.23f, 0.762f, 0.4f), 0);
                Cutlery("Knife " + (i + 1), new Vector3(x + 0.21f, 0.762f, 0.4f), 1);
                Cutlery("Spoon " + (i + 1), new Vector3(x + 0.3f, 0.762f, 0.4f), 2);
            }
        }

        private static void BuildServingTray()
        {
            Vector3 center = new Vector3(-0.85f, 0.762f, 0.88f);
            Transform tray = Pickup("Serving tray - lift me", center, Quaternion.identity, table, 1,
                new Vector3(0.52f, 0.028f, 0.35f), new Vector3(0, 0.014f, 0), 0.24f);
            MeshObject("Oak tray base", tray, RoundedBlock("Serving tray", 0.52f, 0.35f, 0.028f, 0.04f), oakLight);
            foreach (float x in new[] { -0.25f, 0.25f })
                Box("Tray side rim", tray, new Vector3(x, 0.034f, 0), new Vector3(0.012f, 0.02f, 0.31f), oak);
            foreach (float z in new[] { -0.165f, 0.165f })
                Box("Tray end rim", tray, new Vector3(0, 0.034f, z), new Vector3(0.48f, 0.02f, 0.012f), oak);
            ObjectSyncPlacement trayPlacement = tray.GetComponent<ObjectSyncPlacement>();
            PlacementSurface traySurface = Surface("Tray placement area", tray, new Vector3(0, 0.029f, 0),
                Quaternion.identity, new Vector2(0.48f, 0.29f), 1);
            traySurface.carrier = trayPlacement;
            UdonSharpEditorUtility.CopyProxyToUdon(traySurface);

            Transform plate = Pickup("Tray plate - lift me", center + new Vector3(0, 0.029f, 0),
                Quaternion.identity, traySurface, 1, new Vector3(0.18f, 0.027f, 0.18f),
                new Vector3(0, 0.0135f, 0), 0.09f);
            MeshObject("Small porcelain plate", plate, Lathe("Tray plate", new[] {
                new Vector2(0, 0), new Vector2(0.055f, 0), new Vector2(0.063f, 0.012f),
                new Vector2(0.088f, 0.023f), new Vector2(0.09f, 0.027f), new Vector2(0.08f, 0.022f),
                new Vector2(0.056f, 0.015f), new Vector2(0, 0.015f) }), cream);
            ObjectSyncPlacement platePlacement = plate.GetComponent<ObjectSyncPlacement>();
            PlacementSurface plateSurface = Surface("Plate food placement area", plate, new Vector3(0, 0.016f, 0),
                Quaternion.identity, new Vector2(0.11f, 0.11f), 4);
            plateSurface.carrier = platePlacement;
            UdonSharpEditorUtility.CopyProxyToUdon(plateSurface);

            Transform food = Pickup("Tray food - lift me", center + new Vector3(0, 0.045f, 0),
                Quaternion.identity, plateSurface, 4, new Vector3(0.07f, 0.04f, 0.07f),
                new Vector3(0, 0.02f, 0), 0.045f);
            Primitive("Pastry", PrimitiveType.Sphere, food, new Vector3(0, 0.02f, 0),
                new Vector3(0.065f, 0.04f, 0.065f), terra);
            Primitive("Herb garnish", PrimitiveType.Sphere, food, new Vector3(0.013f, 0.042f, 0),
                new Vector3(0.03f, 0.007f, 0.014f), sage);

            Transform fork = Pickup("Tray fork - lift me", center + new Vector3(-0.16f, 0.029f, 0),
                Quaternion.identity, traySurface, 1, new Vector3(0.04f, 0.012f, 0.13f),
                new Vector3(0, 0.006f, 0), 0.07f);
            Box("Fork handle", fork, new Vector3(0, 0.006f, -0.022f), new Vector3(0.013f, 0.008f, 0.082f), steel);
            for (int i = 0; i < 3; i++)
                Box("Fork tine", fork, new Vector3((i - 1) * 0.009f, 0.006f, 0.047f),
                    new Vector3(0.005f, 0.006f, 0.036f), steel);

            Transform spoon = Pickup("Tray spoon - lift me", center + new Vector3(0.16f, 0.029f, 0),
                Quaternion.identity, traySurface, 1, new Vector3(0.04f, 0.012f, 0.13f),
                new Vector3(0, 0.006f, 0), 0.07f);
            Box("Spoon handle", spoon, new Vector3(0, 0.006f, -0.028f),
                new Vector3(0.012f, 0.008f, 0.08f), steel);
            Primitive("Spoon bowl", PrimitiveType.Sphere, spoon, new Vector3(0, 0.006f, 0.047f),
                new Vector3(0.035f, 0.01f, 0.047f), steel);

            ConnectToCarrier(plate, new[] { traySurface, table }, 0);
            ConnectToCarrier(food, new[] { plateSurface }, 0);
            ConnectToCarrier(fork, new[] { traySurface, table }, 0);
            ConnectToCarrier(spoon, new[] { traySurface, table }, 0);
            trayPlacement.carriedItems = new[] { platePlacement, food.GetComponent<ObjectSyncPlacement>(),
                fork.GetComponent<ObjectSyncPlacement>(), spoon.GetComponent<ObjectSyncPlacement>() };
            platePlacement.carriedItems = new[] { food.GetComponent<ObjectSyncPlacement>() };
            UdonSharpEditorUtility.CopyProxyToUdon(trayPlacement);
            UdonSharpEditorUtility.CopyProxyToUdon(platePlacement);
        }

        private static void ConnectToCarrier(Transform item, PlacementSurface[] surfaces, int initialIndex)
        {
            ObjectSyncPlacement placement = item.GetComponent<ObjectSyncPlacement>();
            PlacementSolver solver = item.GetComponent<PlacementSolver>();
            solver.surfaces = surfaces;
            Transform stateObject = Group("Attachment state", item, Vector3.zero);
            PlacementFollowState state = UdonSharpUndo.AddComponent<PlacementFollowState>(stateObject.gameObject);
            state.placement = placement;
            state.surfaceIndex = initialIndex;
            ObjectSyncPlacement carrier = surfaces[initialIndex].carrier;
            state.localPosition = carrier.transform.InverseTransformPoint(item.position);
            state.localRotation = Quaternion.Inverse(carrier.transform.rotation) * item.rotation;
            placement.followState = state;
            UdonSharpEditorUtility.CopyProxyToUdon(solver);
            UdonSharpEditorUtility.CopyProxyToUdon(state);
            UdonSharpEditorUtility.CopyProxyToUdon(placement);
        }

        private static void Cutlery(string name, Vector3 position, int kind)
        {
            Transform item = Pickup(name, position, Quaternion.identity, table, 1, new Vector3(0.055f, 0.018f, 0.225f),
                new Vector3(0, 0.009f, 0), 0.113f);
            GameObject handle = MeshObject("Handle", item, RoundedBlock("Cutlery handle " + kind, 0.017f, 0.135f, 0.006f, 0.008f), steel);
            handle.transform.localPosition = new Vector3(0, 0.002f, -0.04f);
            if (kind == 0)
            {
                Box("Fork shoulder", item, new Vector3(0, 0.005f, 0.044f), new Vector3(0.038f, 0.006f, 0.035f), steel);
                for (int i = 0; i < 4; i++)
                    Box("Fork tine", item, new Vector3((i - 1.5f) * 0.01f, 0.006f, 0.084f), new Vector3(0.005f, 0.005f, 0.048f), steel);
            }
            else if (kind == 1)
            {
                MeshObject("Knife blade", item, RoundedBlock("Knife blade", 0.026f, 0.1f, 0.005f, 0.012f), steel)
                    .transform.localPosition = new Vector3(0.006f, 0.004f, 0.055f);
            }
            else
            {
                Box("Spoon neck", item, new Vector3(0, 0.005f, 0.035f), new Vector3(0.012f, 0.005f, 0.04f), steel);
                Mesh bowl = Lathe("Spoon bowl", new[] { new Vector2(0, 0), new Vector2(0.018f, 0.004f),
                    new Vector2(0.026f, 0.014f), new Vector2(0.024f, 0.015f), new Vector2(0.016f, 0.007f), new Vector2(0, 0.004f) }, 40);
                GameObject spoon = MeshObject("Spoon bowl", item, bowl, steel);
                spoon.transform.localPosition = new Vector3(0, 0.001f, 0.074f);
                spoon.transform.localScale = new Vector3(1, 1, 1.4f);
            }
        }

        private static void BuildMagnetItems()
        {
            Quaternion facing = Quaternion.Euler(-90, 0, 0);
            string[] messages = { "SHOPPING\n\nmilk\nbread\ncoffee", "TODAY\n\nDinner\nat 7:00" };
            for (int i = 0; i < 2; i++)
            {
                Transform note = Pickup("Magnet note " + (i + 1), new Vector3(i == 0 ? 2.04f : 2.38f, 1.16f, 2.498f), facing, fridge, 2,
                    new Vector3(0.22f, 0.028f, 0.3f), new Vector3(0, 0.014f, 0), 0.187f);
                Box("Paper sheet", note, new Vector3(0, 0.003f, 0), new Vector3(0.21f, 0.006f, 0.28f), i == 0 ? paper : cream);
                TextMesh text = Label("Note text", note, messages[i], new Vector3(0, 0.007f, -0.008f), 0.028f, dark.color);
                text.transform.localRotation = Quaternion.Euler(90, 0, 0);
                Primitive("Round magnet", PrimitiveType.Cylinder, note, new Vector3(0, 0.015f, 0.105f), new Vector3(0.044f, 0.01f, 0.044f), i == 0 ? terra : blue);
            }
            for (int i = 0; i < 2; i++)
            {
                Transform keys = Pickup(i == 0 ? "Magnetic key holder" : "Magnetic accessory charm", new Vector3(i == 0 ? 2.02f : 2.39f, 0.66f, 2.498f),
                    facing, fridge, 2, new Vector3(0.12f, 0.06f, 0.22f), new Vector3(0, 0.025f, -0.035f), 0.145f);
                Primitive("Magnet", PrimitiveType.Cylinder, keys, new Vector3(0, 0.019f, 0.045f), new Vector3(0.059f, 0.019f, 0.059f), i == 0 ? brass : blue);
                var ring = new Vector3[36];
                for (int j = 0; j < ring.Length; j++)
                {
                    float angle = j * 2f * Mathf.PI / ring.Length;
                    ring[j] = new Vector3(Mathf.Cos(angle) * 0.026f, 0.032f, Mathf.Sin(angle) * 0.026f - 0.008f);
                }
                MeshObject("Key ring", keys, Tube("Key ring", ring, 0.003f, Vector3.up, true), brass);
                if (i == 0)
                {
                    Box("Key shaft", keys, new Vector3(0, 0.032f, -0.076f), new Vector3(0.012f, 0.008f, 0.08f), brass);
                    Box("Key tooth", keys, new Vector3(0.007f, 0.032f, -0.108f), new Vector3(0.022f, 0.008f, 0.008f), brass);
                    Box("Key tooth", keys, new Vector3(0.009f, 0.032f, -0.09f), new Vector3(0.027f, 0.008f, 0.008f), brass);
                }
                else
                {
                    Primitive("Ceramic charm", PrimitiveType.Sphere, keys, new Vector3(0, 0.032f, -0.083f), new Vector3(0.068f, 0.024f, 0.083f), terra);
                    Primitive("Charm bead", PrimitiveType.Sphere, keys, new Vector3(0, 0.048f, -0.083f), new Vector3(0.023f, 0.013f, 0.023f), cream);
                }
            }
        }

        private static void BuildLightingAndWorld()
        {
            QualitySettings.antiAliasing = 4;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowDistance = 25f;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.73f, 0.78f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.57f, 0.59f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.27f, 0.21f);
            Light sun = new GameObject("Daylight").AddComponent<Light>();
            sun.transform.SetParent(room);
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.color = new Color(1f, 0.94f, 0.82f);
            sun.shadows = LightShadows.Soft;
            sun.shadowBias = 0.02f;
            sun.transform.rotation = Quaternion.Euler(45, -25, 0);
            Transform lamp = Group("Pendant lamp", room, new Vector3(-0.9f, 2.52f, 0.6f));
            MeshObject("Lampshade", lamp, Lathe("Pendant shade", new[] { new Vector2(0.21f, 0), new Vector2(0.21f, 0.012f),
                new Vector2(0.07f, 0.2f), new Vector2(0.025f, 0.22f), new Vector2(0.022f, 0.2f), new Vector2(0.063f, 0.19f), new Vector2(0.20f, 0) }), cream);
            Box("Pendant cord", lamp, new Vector3(0, 0.3f, 0), new Vector3(0.008f, 0.2f, 0.008f), dark);
            Camera camera = new GameObject("Overview camera").AddComponent<Camera>();
            camera.transform.SetParent(room);
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(4.9f, 3.6f, -5.8f);
            camera.transform.LookAt(new Vector3(-0.05f, 1.05f, 1.35f));
            camera.fieldOfView = 43f;
            camera.nearClipPlane = 0.02f;
            camera.farClipPlane = 60f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.78f, 0.80f, 0.75f);
            camera.gameObject.AddComponent<AudioListener>();
            GameObject world = new GameObject("VRCWorld");
            VRCSceneDescriptor descriptor = world.AddComponent<VRCSceneDescriptor>();
            Transform spawn = Group("Spawn - dining area", world.transform, new Vector3(0.35f, 0.08f, -1.55f));
            descriptor.spawns = new[] { spawn };
            descriptor.RespawnHeightY = -5f;
            descriptor.ReferenceCamera = camera.gameObject;
        }

        public static void RenderPreviews()
        {
            Camera camera = Camera.main;
            Directory.CreateDirectory(GeneratedRoot + "/Previews");
            Render(camera, "Overview", camera.transform.position, camera.transform.rotation);
            Vector3 position = new Vector3(-0.65f, 1.9f, -1.12f);
            Render(camera, "Table", position, Quaternion.LookRotation(new Vector3(-0.85f, 0.76f, 0.55f) - position));
            position = new Vector3(2.18f, 1.17f, 0.75f);
            Render(camera, "Fridge", position, Quaternion.LookRotation(new Vector3(2.18f, 1.05f, 2.5f) - position));
        }

        private static void Render(Camera camera, string name, Vector3 position, Quaternion rotation)
        {
            Vector3 previousPosition = camera.transform.position;
            Quaternion previousRotation = camera.transform.rotation;
            RenderTexture target = new RenderTexture(1800, 1200, 24);
            RenderTexture previous = RenderTexture.active;
            Texture2D image = new Texture2D(1800, 1200, TextureFormat.RGB24, false);
            try
            {
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1800, 1200), 0, 0);
                image.Apply();
                File.WriteAllBytes(GeneratedRoot + "/Previews/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                camera.transform.SetPositionAndRotation(previousPosition, previousRotation);
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
