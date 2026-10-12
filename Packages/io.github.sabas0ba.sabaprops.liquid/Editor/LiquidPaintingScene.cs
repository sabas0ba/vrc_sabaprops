using System.Collections.Generic;
using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SabaProps.Liquid.Editors
{
    /// <summary>
    /// Generates the painting sample world: liquid on the walls and floors of
    /// the world rather than on bodies, and the pens, stamps and erasers that
    /// draw on whatever they point at.
    /// <para>
    /// Every area is covered by a Surface Canvas. The studio is for drawing; the
    /// dark studio shows fluorescent and luminous ink under its lamp and
    /// blacklight; the wall test runs the same sprayers against glazed tile and
    /// bare concrete; the rain patio takes rain on its floor and furniture.
    /// </para>
    /// </summary>
    public static class LiquidPaintingScene
    {
        public const string ScenePath = LiquidSampleScene.SampleFolder + "/LiquidPainting.unity";

        /// <summary>First mannequin number here, apart from the other scenes' materials.</summary>
        public const int IndexBase = 300;

        public const string StudioName = "Studio";
        public const string ToolTableName = "Tool Table";
        public const string DarkStudioName = "Dark Studio";
        public const string WallTestName = "Wall Test";
        public const string RainPatioName = "Rain Patio";
        public const string SprayRackName = "Spray Rack";

        public const string StudioWallMaterialPath = LiquidSampleScene.SampleFolder + "/StudioWall.mat";
        public const string DarkStudioWallMaterialPath = LiquidSampleScene.SampleFolder + "/DarkStudioWall.mat";
        public const string TileWallMaterialPath = LiquidSampleScene.SampleFolder + "/TileWall.mat";
        public const string ConcreteWallMaterialPath = LiquidSampleScene.SampleFolder + "/ConcreteWall.mat";

        public static readonly Vector3 SpawnPosition = new Vector3(0f, 0.05f, -6f);

        public static readonly Vector3 StudioCentre = new Vector3(0f, 0f, 3f);
        public static readonly Vector3 StudioSize = new Vector3(6f, 3f, 4f);
        public static readonly Vector3 DarkStudioCentre = new Vector3(8.5f, 0f, 3.5f);
        public static readonly Vector3 DarkStudioSize = new Vector3(5f, 3f, 5f);
        public static readonly Vector3 WallTestCentre = new Vector3(-8.5f, 0f, 3.5f);
        public static readonly Vector3 RainPatioCentre = new Vector3(-8f, 0f, -4f);
        public static readonly Vector3 RainPatioSize = new Vector3(4f, 3f, 4f);

        /// <summary>The stamps on the tool table: shape, ink and caption.</summary>
        public static readonly int[] StampShapes =
        {
            LiquidBodyCanvas.ShapeStar, LiquidBodyCanvas.ShapeHeart, LiquidBodyCanvas.ShapeSquare, LiquidBodyCanvas.ShapeRing,
            LiquidBodyCanvas.ShapeStroke,
        };

        private static readonly string[] StampInks =
        {
            LiquidSourceBuilder.YellowInkName, LiquidSourceBuilder.RedInkName, LiquidSourceBuilder.BlueInkName,
            LiquidSourceBuilder.BlackInkName, LiquidSourceBuilder.WhiteInkName,
        };

        private static readonly string[] StampCaptions = { "Star", "Heart", "Square", "Ring", "Dot" };

        /// <summary>The pens on the tool table, one per ordinary ink.</summary>
        public static readonly string[] PenInks =
        {
            LiquidSourceBuilder.BlackInkName, LiquidSourceBuilder.RedInkName, LiquidSourceBuilder.BlueInkName,
            LiquidSourceBuilder.YellowInkName, LiquidSourceBuilder.WhiteInkName,
        };

        /// <summary>The pens in the dark studio: the inks that glow, and white to compare.</summary>
        public static readonly string[] GlowInks =
        {
            LiquidSourceBuilder.FluorescentPinkInkName, LiquidSourceBuilder.FluorescentGreenInkName,
            LiquidSourceBuilder.LuminousInkName, LiquidSourceBuilder.WhiteInkName,
        };

        /// <summary>The carried sprayers beside the studio.</summary>
        public static readonly string[] RackLiquids =
        {
            LiquidSourceBuilder.WaterName, LiquidSourceBuilder.RedPaintName, LiquidSourceBuilder.BluePaintName,
            LiquidSourceBuilder.YellowPaintName, LiquidSourceBuilder.FluorescentGreenName, LiquidSourceBuilder.SlimeName,
        };

        /// <summary>The liquids sprayed at each wall of the wall test.</summary>
        public static readonly string[] WallTestLiquids =
        {
            LiquidSourceBuilder.WaterName, LiquidSourceBuilder.RedPaintName, LiquidSourceBuilder.SlimeName,
        };

        [MenuItem("Tools/SabaProps/Liquid/Create Painting Scene", false, 4)]
        public static void CreateAndOpen()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = Create();
            SceneView view = SceneView.lastActiveSceneView;
            if (scene.IsValid() && view != null)
            {
                view.LookAt(new Vector3(0f, 0.5f, 1f), Quaternion.Euler(35f, 0f, 0f), 20f);
            }
        }

        /// <summary>Replaces the open scene with the painting demo and saves it.</summary>
        public static Scene Create()
        {
            LiquidAssets.EnsureFolder(LiquidSampleScene.SampleFolder);
            LiquidSampleScene.EnsurePrefabs();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            LiquidSampleScene.ConfigureLight();
            // A dimmer sky, as in the interactive scene, so the closed studio is dark inside.
            RenderSettings.ambientSkyColor *= 0.45f;
            RenderSettings.ambientEquatorColor *= 0.45f;
            RenderSettings.ambientGroundColor *= 0.45f;

            Material ground = LiquidAssets.CreateOrLoadSurfaceMaterial(LiquidSampleScene.GroundMaterialPath,
                new Color(0.55f, 0.56f, 0.52f), 0.1f);
            var groundRoot = new GameObject(LiquidSampleScene.GroundRootName);
            float edge = LiquidSampleScene.GroundHalfSize;
            LiquidSampleScene.Slab(groundRoot.transform, "Ground", -edge, edge, -edge, edge, 0f,
                LiquidSampleScene.GroundThickness, ground);

            LiquidCanvasPool pool = LiquidSourceBuilder.FindOrCreatePool();
            Material update = LiquidSampleScene.CanvasUpdateMaterial();
            var mannequins = new List<LiquidBodyCanvas>();

            BuildStudio(pool, update, mannequins);
            BuildToolTable(pool);
            BuildSprayRack(pool);
            BuildDarkStudio(pool, update);
            BuildWallTest(pool, update);
            BuildRainPatio(pool, update);

            GameObject panel = LiquidPrefabBuilder.Place(LiquidPrefabBuilder.ResetPanelName, null);
            panel.transform.SetPositionAndRotation(SpawnPosition + new Vector3(-2.2f, 0f, 1.2f), Quaternion.identity);

            LiquidSampleScene.AssignMannequins(pool, mannequins);
            LiquidPaintingBuilder.Register(pool);
            LiquidSampleScene.BuildWorld(SpawnPosition);

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log(Summarise());
            return scene;
        }

        // ------------------------------------------------------------------
        // Studio
        // ------------------------------------------------------------------

        /// <summary>
        /// Three light walls open to the sky and to the spawn, a low plinth to
        /// draw on from above, and a mannequin, which the tools draw on as well.
        /// </summary>
        private static void BuildStudio(LiquidCanvasPool pool, Material update, List<LiquidBodyCanvas> mannequins)
        {
            var studio = new GameObject(StudioName);
            Material wall = LiquidAssets.CreateOrLoadSurfaceMaterial(StudioWallMaterialPath, new Color(0.86f, 0.86f, 0.84f), 0.15f);
            const float t = 0.15f;
            float x0 = StudioCentre.x - StudioSize.x * 0.5f;
            float x1 = StudioCentre.x + StudioSize.x * 0.5f;
            float z0 = StudioCentre.z - StudioSize.z * 0.5f;
            float z1 = StudioCentre.z + StudioSize.z * 0.5f;
            float h = StudioSize.y;

            LiquidSampleScene.Slab(studio.transform, "Back Wall", x0, x1, z1 - t, z1, h, h, wall);
            LiquidSampleScene.Slab(studio.transform, "Left Wall", x0, x0 + t, z0, z1, h, h, wall);
            LiquidSampleScene.Slab(studio.transform, "Right Wall", x1 - t, x1, z0, z1, h, h, wall);
            LiquidSampleScene.Slab(studio.transform, "Floor", x0, x1, z0, z1, 0.02f, 0.02f, wall);
            LiquidSampleScene.Slab(studio.transform, "Plinth", -2.4f, -1.4f, 2.2f, 3.2f, 0.5f, 0.5f, wall);

            LiquidPaintingBuilder.CreateSurfaceCanvas(studio.transform, "Surface Canvas",
                StudioCentre + new Vector3(0f, h * 0.5f, 0f), StudioSize,
                LiquidSurfaceBuilder.GetSurface(LiquidSurfaceBuilder.PaintedWallName), update, 0, 384);

            var spot = new GameObject("Mannequin Spot");
            spot.transform.SetParent(studio.transform, false);
            spot.transform.SetPositionAndRotation(new Vector3(2f, 0.02f, 3.6f), Quaternion.Euler(0f, 200f, 0f));
            mannequins.Add(LiquidMannequinBuilder.Create(spot.transform, "Mannequin", IndexBase + mannequins.Count,
                LiquidMannequinBuilder.LightSkin(), update, false,
                LiquidSurfaceBuilder.GetSurface(LiquidSurfaceBuilder.HardClothName), false, null, null));

            LiquidDemoGalleries.Label(studio.transform, "Studio: draw, stamp and spray on the walls, the floor and the mannequin",
                new Vector3(StudioCentre.x, h + 0.4f, z1), Quaternion.Euler(0f, 180f, 0f));
        }

        /// <summary>The pens, stamps and erasers, on a table between the spawn and the studio.</summary>
        private static void BuildToolTable(LiquidCanvasPool pool)
        {
            var table = new GameObject(ToolTableName);
            Material furniture = LiquidAssets.CreateOrLoadSurfaceMaterial(LiquidSampleScene.FurnitureMaterialPath,
                new Color(0.42f, 0.33f, 0.25f), 0.2f);
            const float spacing = 0.36f;
            int count = PenInks.Length + StampShapes.Length + 2;
            float width = spacing * count;
            Vector3 centre = new Vector3(0f, 0f, -0.6f);
            LiquidSampleScene.Slab(table.transform, "Top", centre.x - width * 0.5f, centre.x + width * 0.5f,
                centre.z - 0.3f, centre.z + 0.3f, 0.9f, 0.9f, furniture);

            int slot = 0;
            foreach (string ink in PenInks)
            {
                Place(table.transform, LiquidPaintingBuilder.CreatePaintTool(ink + " Pen", LiquidPaintTool.ModePen,
                    LiquidSourceBuilder.GetProfile(ink), LiquidBodyCanvas.ShapeStroke, 0.02f, LiquidAssets.MaterialFolder),
                    pool, Slot(centre, width, spacing, slot++), ink.Replace(" Ink", "") + "\npen");
            }

            for (int i = 0; i < StampShapes.Length; i++)
            {
                Place(table.transform, LiquidPaintingBuilder.CreatePaintTool(StampCaptions[i] + " Stamp", LiquidPaintTool.ModeStamp,
                    LiquidSourceBuilder.GetProfile(StampInks[i]), StampShapes[i], 0.12f, LiquidAssets.MaterialFolder),
                    pool, Slot(centre, width, spacing, slot++), StampCaptions[i] + "\nstamp");
            }

            Place(table.transform, LiquidPaintingBuilder.CreatePaintTool("Eraser", LiquidPaintTool.ModeEraser, null,
                LiquidBodyCanvas.ShapeStroke, 0.05f, LiquidAssets.MaterialFolder),
                pool, Slot(centre, width, spacing, slot++), "Eraser");
            Place(table.transform, LiquidPaintingBuilder.CreatePaintTool("Wide Eraser", LiquidPaintTool.ModeEraser, null,
                LiquidBodyCanvas.ShapeStroke, 0.15f, LiquidAssets.MaterialFolder),
                pool, Slot(centre, width, spacing, slot), "Wide\neraser");

            LiquidDemoGalleries.Label(table.transform, "Pick one up, point it at a wall or the floor, and hold use",
                centre + new Vector3(0f, 1.7f, -0.2f), Quaternion.Euler(0f, 180f, 0f)).characterSize = 0.025f;
        }

        /// <summary>Carried sprayers, to wet and paint the studio with liquid that runs.</summary>
        private static void BuildSprayRack(LiquidCanvasPool pool)
        {
            var rack = new GameObject(SprayRackName);
            Material furniture = LiquidAssets.CreateOrLoadSurfaceMaterial(LiquidSampleScene.FurnitureMaterialPath,
                new Color(0.42f, 0.33f, 0.25f), 0.2f);
            const float spacing = 0.6f;
            float width = spacing * RackLiquids.Length;
            Vector3 centre = new Vector3(4.6f, 0f, -0.6f);
            LiquidSampleScene.Slab(rack.transform, "Shelf", centre.x - 0.3f, centre.x + 0.3f,
                centre.z - width * 0.5f, centre.z + width * 0.5f, 0.9f, 0.9f, furniture);

            for (int i = 0; i < RackLiquids.Length; i++)
            {
                LiquidProfile profile = LiquidSourceBuilder.GetProfile(RackLiquids[i]);
                float z = centre.z - width * 0.5f + spacing * (i + 0.5f);
                GameObject carried = LiquidPrefabBuilder.CreatePortableNozzle(RackLiquids[i] + " Sprayer", LiquidNozzle.ModeHold,
                    profile, new Color(0.22f, 0.24f, 0.27f), LiquidAssets.MaterialFolder);
                carried.transform.SetParent(rack.transform, false);
                carried.transform.SetPositionAndRotation(new Vector3(centre.x, 0.99f, z), Quaternion.Euler(0f, -90f, 0f));
                LiquidNozzle nozzle = carried.GetComponentInChildren<LiquidNozzle>();
                nozzle.pool = pool;
                UdonSharpEditorUtility.CopyProxyToUdon(nozzle);
            }

            LiquidDemoGalleries.Label(rack.transform, "Sprayers: liquid runs down the walls",
                centre + new Vector3(0f, 1.5f, 0f), Quaternion.Euler(0f, -90f, 0f)).characterSize = 0.025f;
        }

        // ------------------------------------------------------------------
        // Dark studio
        // ------------------------------------------------------------------

        /// <summary>
        /// A closed room with a lamp and a blacklight that cycle (or follow the
        /// switches by the door), pens in the inks that glow, and two sprayers
        /// that keep fluorescent and luminous paint on the back wall.
        /// </summary>
        private static void BuildDarkStudio(LiquidCanvasPool pool, Material update)
        {
            Vector3 centre = DarkStudioCentre;
            Vector3 size = DarkStudioSize;
            Material wall = LiquidAssets.CreateOrLoadSurfaceMaterial(DarkStudioWallMaterialPath, new Color(0.3f, 0.3f, 0.32f), 0.1f);
            GameObject room = LiquidInteractiveScene.Room(DarkStudioName, centre, size, wall);
            Material device = LiquidInteractiveScene.Device();

            var lampObject = new GameObject("Lamp");
            lampObject.transform.SetParent(room.transform, false);
            lampObject.transform.position = centre + new Vector3(0f, size.y - 0.3f, 0f);
            Light lamp = lampObject.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 7f;
            lamp.intensity = 1.4f;
            lamp.color = new Color(1f, 0.92f, 0.8f);

            var blacklightObject = new GameObject("Blacklight");
            blacklightObject.transform.SetParent(room.transform, false);
            blacklightObject.transform.position = centre + new Vector3(0f, size.y - 0.35f, -1f);
            Light blacklight = blacklightObject.AddComponent<Light>();
            blacklight.type = LightType.Point;
            blacklight.range = 7f;
            blacklight.intensity = 0.5f;
            blacklight.color = new Color(0.45f, 0.1f, 1f);
            blacklight.enabled = false;

            var zoneObject = new GameObject("Light Zone");
            zoneObject.transform.SetParent(room.transform, false);
            zoneObject.transform.position = centre + new Vector3(0f, size.y * 0.5f, 0f);
            LiquidLightZone zone = zoneObject.AddUdonSharpComponent<LiquidLightZone>();
            zone.pool = pool;
            zone.areaSize = size;
            zone.lamp = lamp;
            zone.blacklight = blacklight;
            UdonSharpEditorUtility.CopyProxyToUdon(zone);

            LiquidPaintingBuilder.CreateSurfaceCanvas(room.transform, "Surface Canvas",
                centre + new Vector3(0f, size.y * 0.5f, 0f), size,
                LiquidSurfaceBuilder.GetSurface(LiquidSurfaceBuilder.PaintedWallName), update, 1, 320);

            // Paint that is already on the wall, so the room shows its point before anyone draws.
            string[] paints = { LiquidSourceBuilder.FluorescentPinkName, LiquidSourceBuilder.LuminousPaintName };
            for (int i = 0; i < paints.Length; i++)
            {
                float x = centre.x - 1.2f + i * 2.4f;
                LiquidSprayer sprayer = LiquidSourceBuilder.CreateSprayer(room.transform, paints[i] + " Sprayer", pool,
                    LiquidSourceBuilder.GetProfile(paints[i]), new Vector3(x, 2.6f, centre.z + 0.6f),
                    new Vector3(x, 2.1f, centre.z + size.z * 0.5f), device);
                sprayer.burstInterval = 6f;
                sprayer.raysPerBurst = 4;
                sprayer.coneAngle = 9f;
                sprayer.phaseOffset = i * 2f;
                UdonSharpEditorUtility.CopyProxyToUdon(sprayer);
            }

            Material furniture = LiquidAssets.CreateOrLoadSurfaceMaterial(LiquidSampleScene.FurnitureMaterialPath,
                new Color(0.42f, 0.33f, 0.25f), 0.2f);
            const float spacing = 0.4f;
            float width = spacing * (GlowInks.Length + 1);
            Vector3 shelf = centre + new Vector3(0f, 0f, -1.2f);
            LiquidSampleScene.Slab(room.transform, "Shelf", shelf.x - width * 0.5f, shelf.x + width * 0.5f,
                shelf.z - 0.2f, shelf.z + 0.2f, 0.9f, 0.9f, furniture);
            for (int i = 0; i < GlowInks.Length; i++)
            {
                Place(room.transform, LiquidPaintingBuilder.CreatePaintTool(GlowInks[i] + " Pen", LiquidPaintTool.ModePen,
                    LiquidSourceBuilder.GetProfile(GlowInks[i]), LiquidBodyCanvas.ShapeStroke, 0.03f, LiquidAssets.MaterialFolder),
                    pool, Slot(shelf, width, spacing, i), null);
            }

            Place(room.transform, LiquidPaintingBuilder.CreatePaintTool("Dark Studio Eraser", LiquidPaintTool.ModeEraser, null,
                LiquidBodyCanvas.ShapeStroke, 0.08f, LiquidAssets.MaterialFolder),
                pool, Slot(shelf, width, spacing, GlowInks.Length), null);

            Material key = LiquidAssets.CreateOrLoadSurfaceMaterial(LiquidAssets.MaterialFolder + "/PanelKey.mat",
                new Color(0.75f, 0.77f, 0.8f), 0.5f);
            var switches = new GameObject("Switches");
            switches.transform.SetParent(room.transform, false);
            switches.transform.position = centre + new Vector3(1.2f, 1.3f, -size.z * 0.5f - 0.1f);
            LiquidPrefabBuilder.Button(switches.transform, "Lamp", "Lamp", new Vector3(0f, 0f, 0f), key, zone,
                nameof(LiquidLightZone.ToggleLamp));
            LiquidPrefabBuilder.Button(switches.transform, "Blacklight", "UV", new Vector3(0.12f, 0f, 0f), key, zone,
                nameof(LiquidLightZone.ToggleBlacklight));
            LiquidPrefabBuilder.Button(switches.transform, "Automatic", "Auto", new Vector3(0.24f, 0f, 0f), key, zone,
                nameof(LiquidLightZone.ResumeAutomatic));

            LiquidDemoGalleries.Label(room.transform, "Dark studio: fluorescent and luminous ink (lit / UV only / dark)",
                centre + new Vector3(0f, 3.4f, -size.z * 0.5f), Quaternion.Euler(0f, 180f, 0f));
        }

        // ------------------------------------------------------------------
        // Wall test
        // ------------------------------------------------------------------

        /// <summary>
        /// Two walls side by side, glazed tile and bare concrete, each with its
        /// own Surface Canvas and the same three sprayers: water, paint and
        /// slime. The liquid runs down the wall and collects on the floor.
        /// </summary>
        private static void BuildWallTest(LiquidCanvasPool pool, Material update)
        {
            var root = new GameObject(WallTestName);
            Material device = LiquidInteractiveScene.Device();
            string[] surfaces = { LiquidSurfaceBuilder.GlazedTileName, LiquidSurfaceBuilder.ConcreteName };
            Material[] walls =
            {
                LiquidAssets.CreateOrLoadSurfaceMaterial(TileWallMaterialPath, new Color(0.82f, 0.88f, 0.9f), 0.85f),
                LiquidAssets.CreateOrLoadSurfaceMaterial(ConcreteWallMaterialPath, new Color(0.58f, 0.57f, 0.55f), 0.05f),
            };

            const float bayWidth = 3f;
            const float depth = 2.4f;
            const float height = 3f;
            for (int b = 0; b < surfaces.Length; b++)
            {
                var bay = new GameObject(surfaces[b]);
                bay.transform.SetParent(root.transform, false);
                float x0 = WallTestCentre.x - bayWidth + b * bayWidth;
                float x1 = x0 + bayWidth;
                float z1 = WallTestCentre.z + depth * 0.5f;
                float z0 = z1 - depth;
                LiquidSampleScene.Slab(bay.transform, "Wall", x0 + 0.05f, x1 - 0.05f, z1 - 0.15f, z1, height, height, walls[b]);
                LiquidSampleScene.Slab(bay.transform, "Floor", x0 + 0.05f, x1 - 0.05f, z0, z1, 0.02f, 0.02f, walls[b]);

                var size = new Vector3(bayWidth - 0.5f, height, depth);
                LiquidPaintingBuilder.CreateSurfaceCanvas(bay.transform, "Surface Canvas",
                    new Vector3((x0 + x1) * 0.5f, height * 0.5f, (z0 + z1) * 0.5f), size,
                    LiquidSurfaceBuilder.GetSurface(surfaces[b]), update, 2 + b, 192);

                for (int i = 0; i < WallTestLiquids.Length; i++)
                {
                    float x = x0 + 0.55f + i * 0.95f;
                    LiquidSprayer sprayer = LiquidSourceBuilder.CreateSprayer(bay.transform, WallTestLiquids[i] + " Sprayer", pool,
                        LiquidSourceBuilder.GetProfile(WallTestLiquids[i]), new Vector3(x, 2.5f, z0 + 0.3f),
                        new Vector3(x, 2.3f, z1 - 0.15f), device);
                    sprayer.burstInterval = 5f;
                    sprayer.raysPerBurst = 4;
                    sprayer.coneAngle = 6f;
                    sprayer.hitRadius = 0.12f;
                    sprayer.phaseOffset = i * 1.5f + b * 0.7f;
                    UdonSharpEditorUtility.CopyProxyToUdon(sprayer);
                }

                LiquidDemoGalleries.Label(bay.transform, surfaces[b], new Vector3((x0 + x1) * 0.5f, height + 0.25f, z1),
                    Quaternion.Euler(0f, 180f, 0f)).characterSize = 0.035f;
            }

            LiquidDemoGalleries.Label(root.transform, "Wall test: water, paint and slime on tile and on concrete",
                WallTestCentre + new Vector3(0f, height + 0.6f, depth * 0.5f), Quaternion.Euler(0f, 180f, 0f));
        }

        // ------------------------------------------------------------------
        // Rain patio
        // ------------------------------------------------------------------

        /// <summary>
        /// A paved patch under rain that comes and goes, with a bench and a
        /// canopy. Drops land on the paving and the bench and dry between
        /// showers; under the canopy the paving stays dry.
        /// </summary>
        private static void BuildRainPatio(LiquidCanvasPool pool, Material update)
        {
            var patio = new GameObject(RainPatioName);
            Vector3 centre = RainPatioCentre;
            Vector3 size = RainPatioSize;
            Material paving = LiquidAssets.CreateOrLoadSurfaceMaterial(ConcreteWallMaterialPath, new Color(0.58f, 0.57f, 0.55f), 0.05f);
            Material furniture = LiquidAssets.CreateOrLoadSurfaceMaterial(LiquidSampleScene.FurnitureMaterialPath,
                new Color(0.42f, 0.33f, 0.25f), 0.2f);
            float x0 = centre.x - size.x * 0.5f;
            float x1 = centre.x + size.x * 0.5f;
            float z0 = centre.z - size.z * 0.5f;
            float z1 = centre.z + size.z * 0.5f;

            LiquidSampleScene.Slab(patio.transform, "Paving", x0, x1, z0, z1, 0.02f, 0.02f, paving);
            LiquidSampleScene.Slab(patio.transform, "Bench", centre.x - 0.8f, centre.x + 0.8f, centre.z + 0.4f, centre.z + 0.9f,
                0.45f, 0.45f, furniture);
            // A canopy over one corner, on two posts.
            LiquidSampleScene.Slab(patio.transform, "Canopy", x0, x0 + 1.6f, z0, z0 + 1.6f, 2.3f, 0.08f, furniture);
            LiquidSampleScene.Slab(patio.transform, "Canopy Post A", x0, x0 + 0.1f, z0, z0 + 0.1f, 2.22f, 2.22f, furniture);
            LiquidSampleScene.Slab(patio.transform, "Canopy Post B", x0 + 1.5f, x0 + 1.6f, z0 + 1.5f, z0 + 1.6f, 2.22f, 2.22f, furniture);

            LiquidPaintingBuilder.CreateSurfaceCanvas(patio.transform, "Surface Canvas",
                centre + new Vector3(0f, size.y * 0.5f, 0f), size,
                LiquidSurfaceBuilder.GetSurface(LiquidSurfaceBuilder.ConcreteName), update, 4, 192);

            var weatherRoot = new GameObject("Rain");
            weatherRoot.transform.SetParent(patio.transform, false);
            weatherRoot.transform.position = centre;
            LiquidWeather rain = LiquidWeatherBuilder.CreateWeather(weatherRoot.transform, "Weather", pool,
                LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.WaterName), false, size);
            rain.precipitationSeconds = 35f;
            rain.clearSeconds = 40f;
            UdonSharpEditorUtility.CopyProxyToUdon(rain);

            LiquidDemoGalleries.Label(patio.transform, "Rain patio: drops on the paving and the bench; dry under the canopy",
                centre + new Vector3(0f, 3.3f, size.z * 0.5f), Quaternion.Euler(0f, 180f, 0f));
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static Vector3 Slot(Vector3 centre, float width, float spacing, int slot)
        {
            return new Vector3(centre.x - width * 0.5f + spacing * (slot + 0.5f), 0.96f, centre.z);
        }

        /// <summary>Lays a tool on a table, pointing away from the spawn, with a caption above it.</summary>
        private static void Place(Transform parent, GameObject tool, LiquidCanvasPool pool, Vector3 position, string caption)
        {
            tool.transform.SetParent(parent, false);
            tool.transform.SetPositionAndRotation(position, Quaternion.identity);
            LiquidPaintTool behaviour = tool.GetComponentInChildren<LiquidPaintTool>();
            behaviour.pool = pool;
            UdonSharpEditorUtility.CopyProxyToUdon(behaviour);

            if (!string.IsNullOrEmpty(caption))
            {
                LiquidDemoGalleries.Label(parent, caption, position + new Vector3(0f, 0.32f, -0.2f),
                    Quaternion.Euler(0f, 180f, 0f)).characterSize = 0.016f;
            }
        }

        private static string Summarise()
        {
            var text = new StringBuilder();
            text.AppendLine($"[SabaProps Liquid] 壁と床に描くシーンを {ScenePath} に作成しました。");
            text.AppendLine($"・{StudioName}（正面）: 壁、床、台、マネキンに、ペン、スタンプ、噴射で描きます。消しゴムで消せます。");
            text.AppendLine($"・{ToolTableName}（スポーンの前）: ペン 5 色、スタンプ 5 種、消しゴム 2 種。{SprayRackName}（右）に噴射器。");
            text.AppendLine($"・{DarkStudioName}（右奥）: 蛍光と蓄光のインク。点灯、紫外線のみ、消灯が切り替わります。");
            text.AppendLine($"・{WallTestName}（左奥）: タイルとコンクリートの壁に、水、塗料、スライムを吹き付けて比べます。");
            text.AppendLine($"・{RainPatioName}（左手前）: 雨が敷石と腰掛けに落ち、屋根の下は乾いたままです。");
            return text.ToString();
        }
    }
}
