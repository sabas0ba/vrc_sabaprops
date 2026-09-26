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
    /// Generates the interactive sample world: the nozzle bench with its
    /// control panels, the particle comparison by viscosity, humid rooms
    /// (sauna, bathroom, damp room), the dark room for fluorescent and
    /// luminous paint, and the prefabs laid out to pick up and try.
    /// </summary>
    public static class LiquidInteractiveScene
    {
        public const string ScenePath = LiquidSampleScene.SampleFolder + "/LiquidInteractive.unity";

        /// <summary>First mannequin number here, apart from the other two scenes' materials.</summary>
        public const int IndexBase = 200;

        public const string NozzleBenchName = "Nozzle Bench";
        public const string ViscosityRowName = "Viscosity Comparison";
        public const string SaunaName = "Sauna";
        public const string BathroomName = "Bathroom";
        public const string DampRoomName = "Damp Air";
        public const string DarkRoomName = "Dark Room";
        public const string PrefabCornerName = "Prefab Corner";

        public const string SaunaWallMaterialPath = LiquidSampleScene.SampleFolder + "/SaunaWall.mat";
        public const string BathroomWallMaterialPath = LiquidSampleScene.SampleFolder + "/BathroomWall.mat";
        public const string SaunaFogMaterialPath = LiquidSampleScene.SampleFolder + "/SaunaFog.mat";
        public const string BathroomFogMaterialPath = LiquidSampleScene.SampleFolder + "/BathroomFog.mat";
        public const string FoggedGlassMaterialPath = LiquidSampleScene.SampleFolder + "/FoggedGlass.mat";
        public const string DarkWallMaterialPath = LiquidSampleScene.SampleFolder + "/DarkWall.mat";

        public static readonly Vector3 SpawnPosition = new Vector3(0f, 0.05f, -10f);

        /// <summary>Viscosity row presets, thin to thick.</summary>
        public static readonly string[] ViscosityPresets =
        {
            LiquidSourceBuilder.WaterName, LiquidSourceBuilder.JuiceName, LiquidSourceBuilder.RedPaintName,
            LiquidSourceBuilder.MudName, LiquidSourceBuilder.SlimeName, LiquidSourceBuilder.SyrupName,
        };

        /// <summary>Dark room paints: ordinary, fluorescent and luminous.</summary>
        public static readonly string[] DarkRoomPaints =
        {
            LiquidSourceBuilder.RedPaintName, LiquidSourceBuilder.FluorescentPinkName,
            LiquidSourceBuilder.FluorescentGreenName, LiquidSourceBuilder.LuminousPaintName,
        };

        /// <summary>Short captions for the dark room, which has little room between the mannequins.</summary>
        private static readonly string[] DarkRoomCaptions =
        {
            "Ordinary\n(red)", "Fluorescent\n(pink)", "Fluorescent\n(green)", "Luminous",
        };

        [MenuItem("Tools/SabaProps/Liquid/Create Interactive Scene", false, 3)]
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
                view.LookAt(new Vector3(0f, 0.5f, 3f), Quaternion.Euler(35f, 0f, 0f), 22f);
            }
        }

        /// <summary>Replaces the open scene with the interactive demo and saves it.</summary>
        public static Scene Create()
        {
            LiquidAssets.EnsureFolder(LiquidSampleScene.SampleFolder);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            LiquidSampleScene.ConfigureLight();
            // A dimmer sky than the other scenes, so the closed rooms are dark inside
            // while the sun still lights the open ground.
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

            BuildNozzleBench(pool, update, mannequins);
            BuildViscosityRow(pool, update, mannequins);
            BuildSauna(pool, update, mannequins);
            BuildBathroom(pool, update, mannequins);
            BuildDampRoom(pool, update, mannequins);
            BuildDarkRoom(pool, update, mannequins);
            BuildPrefabCorner(pool, update, mannequins);

            LiquidSampleScene.AssignMannequins(pool, mannequins);
            LiquidSampleScene.BuildWorld(SpawnPosition);

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log(Summarise());
            return scene;
        }

        // ------------------------------------------------------------------
        // Nozzle bench
        // ------------------------------------------------------------------

        /// <summary>
        /// Three nozzles, one per way of firing, each aimed at a mannequin 4 m
        /// away and with its own control panel.
        /// </summary>
        private static void BuildNozzleBench(LiquidCanvasPool pool, Material update, List<LiquidBodyCanvas> mannequins)
        {
            var bench = new GameObject(NozzleBenchName);
            Material device = Device();
            LiquidProfile water = LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.WaterName);

            int[] modes = { LiquidNozzle.ModeOneShot, LiquidNozzle.ModePeriodic, LiquidNozzle.ModeContinuous };
            string[] titles = { "One shot (Fire)", "Periodic (every 3 s)", "Continuous (Start / Stop)" };
            float[] volumes = { 1.5f, 0.8f, 0.6f };
            for (int i = 0; i < modes.Length; i++)
            {
                var bay = new GameObject(titles[i]);
                bay.transform.SetParent(bench.transform, false);
                bay.transform.position = new Vector3(-10f + i * 3.2f, 0f, -3f);

                LiquidSampleScene.Slab(bay.transform, "Post", -0.04f, 0.04f, -0.04f, 0.04f, 1.2f, 1.2f, device);
                LiquidNozzle nozzle = LiquidPrefabBuilder.CreateNozzle(bay.transform, "Nozzle", water,
                    new Vector3(0f, 1.3f, 0f), modes[i], LiquidAssets.MaterialFolder);
                nozzle.pool = pool;
                // Tilted up so the arc comes down on the mannequin's middle 4 m away.
                nozzle.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
                nozzle.volume = volumes[i];
                nozzle.range = 5f;
                nozzle.speed = 9f;
                nozzle.diameter = 0.08f;
                nozzle.period = 3f;
                nozzle.phaseOffset = i * 0.7f;
                UdonSharpEditorUtility.CopyProxyToUdon(nozzle);
                LiquidPrefabBuilder.CreatePanel(bay.transform, nozzle, new Vector3(0.55f, 1.1f, -0.2f), Quaternion.identity,
                    LiquidAssets.MaterialFolder);

                var spot = new GameObject("Target");
                spot.transform.SetParent(bay.transform, false);
                spot.transform.localPosition = new Vector3(0f, 0f, 4f);
                spot.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                mannequins.Add(LiquidMannequinBuilder.CreateClothed(spot.transform, "Mannequin", NextIndex(mannequins),
                    update, true, LiquidMannequinBuilder.Casual));

                LiquidDemoGalleries.Label(bay.transform, titles[i], bay.transform.position + new Vector3(0f, 2f, 0f),
                    Quaternion.Euler(0f, 180f, 0f));
            }
        }

        // ------------------------------------------------------------------
        // Viscosity row
        // ------------------------------------------------------------------

        /// <summary>
        /// The same sprayer with liquids from thin to thick, so the particles
        /// (streaks and spray, splats, strings) can be compared side by side.
        /// </summary>
        private static void BuildViscosityRow(LiquidCanvasPool pool, Material update, List<LiquidBodyCanvas> mannequins)
        {
            var row = new GameObject(ViscosityRowName);
            Material device = Device();
            for (int i = 0; i < ViscosityPresets.Length; i++)
            {
                LiquidProfile profile = LiquidSourceBuilder.GetProfile(ViscosityPresets[i]);
                var bay = new GameObject(ViscosityPresets[i]);
                bay.transform.SetParent(row.transform, false);
                bay.transform.position = new Vector3(2.5f + i * 2.3f, 0f, 2.5f);
                bay.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

                mannequins.Add(LiquidMannequinBuilder.Create(bay.transform, "Mannequin", NextIndex(mannequins),
                    LiquidMannequinBuilder.LightSkin(), update, true));
                Vector3 feet = bay.transform.position;
                LiquidSprayer sprayer = LiquidSourceBuilder.CreateSprayer(bay.transform, "Sprayer", pool, profile,
                    feet + new Vector3(0.9f, 1.5f, -2.2f), feet + new Vector3(0f, 1.2f, 0f), device);
                sprayer.burstInterval = 1.8f;
                sprayer.raysPerBurst = 6;
                sprayer.coneAngle = 7f;
                sprayer.hitRadius = 0.1f + profile.viscosity * 0.06f;
                sprayer.phaseOffset = i * 0.25f;
                UdonSharpEditorUtility.CopyProxyToUdon(sprayer);

                LiquidDemoGalleries.Label(bay.transform, ViscosityPresets[i], feet + new Vector3(0f, 2.25f, -0.4f),
                    Quaternion.Euler(0f, 180f, 0f));
            }
        }

        // ------------------------------------------------------------------
        // Humid rooms
        // ------------------------------------------------------------------

        /// <summary>
        /// A wooden sauna at saturation: steam, haze, walls running with dew, and
        /// mannequins that break out in droplets. Bare skin beads; cloth soaks.
        /// </summary>
        private static void BuildSauna(LiquidCanvasPool pool, Material update, List<LiquidBodyCanvas> mannequins)
        {
            Vector3 centre = new Vector3(-12f, 0f, 13f);
            Vector3 size = new Vector3(4.5f, 3f, 4.5f);
            Material wall = LiquidWeatherBuilder.CreateOrLoadGroundMaterial(SaunaWallMaterialPath, new Color(0.55f, 0.36f, 0.2f), 0.2f);
            GameObject room = Room(SaunaName, centre, size, wall);

            Material bench = LiquidAssets.CreateOrLoadSurfaceMaterial(LiquidSampleScene.SampleFolder + "/SaunaBench.mat",
                new Color(0.62f, 0.44f, 0.26f), 0.25f);
            LiquidSampleScene.Slab(room.transform, "Bench", centre.x - 2f, centre.x + 2f, centre.z + 1.2f, centre.z + 2f,
                0.45f, 0.45f, bench);

            var skin = LiquidMannequinBuilder.Skin("Skin", new Color(0.86f, 0.68f, 0.58f), 0.35f);
            AddMannequin(room.transform, "Bare Skin", new Vector3(centre.x - 0.9f, 0f, centre.z), mannequins,
                LiquidMannequinBuilder.Create(null, "Mannequin", NextIndex(mannequins), skin, update, true,
                    LiquidSurfaceBuilder.GetSurface(LiquidSurfaceBuilder.SkinName), false, null, null));
            AddMannequin(room.transform, "Clothed", new Vector3(centre.x + 0.9f, 0f, centre.z), mannequins,
                LiquidMannequinBuilder.CreateClothed(null, "Mannequin", NextIndex(mannequins), update, true,
                    LiquidMannequinBuilder.Casual));

            LiquidHumidity humidity = Humidity(room.transform, "Humidity", pool, centre, size, 0.97f, 0.7f);
            humidity.steam = LiquidParticleBuilder.CreateSteam(room.transform, new Vector2(size.x - 0.6f, size.z - 0.6f),
                LiquidAssets.MaterialFolder);
            humidity.steam.transform.position = centre + new Vector3(0f, 0.2f, 0f);
            humidity.fogMaterial = FogVolume(room.transform, centre, size, SaunaFogMaterialPath, new Color(0.9f, 0.88f, 0.85f));
            humidity.surfaceMaterials = new[] { wall };
            UdonSharpEditorUtility.CopyProxyToUdon(humidity);
            RoomLight(room.transform, pool, centre, size, new Color(1f, 0.78f, 0.55f), 1.3f);

            LiquidDemoGalleries.Label(room.transform, "Sauna (humidity 97 %)", centre + new Vector3(0f, 3.4f, -size.z * 0.5f),
                Quaternion.Euler(0f, 180f, 0f));
        }

        /// <summary>
        /// A bathroom whose humidity follows its shower: steam rises while it
        /// runs, the mirror fogs over and clears again after it stops.
        /// </summary>
        private static void BuildBathroom(LiquidCanvasPool pool, Material update, List<LiquidBodyCanvas> mannequins)
        {
            Vector3 centre = new Vector3(-4f, 0f, 13f);
            Vector3 size = new Vector3(4.5f, 3f, 4.5f);
            Material wall = LiquidWeatherBuilder.CreateOrLoadGroundMaterial(BathroomWallMaterialPath, new Color(0.8f, 0.85f, 0.87f), 0.55f);
            GameObject room = Room(BathroomName, centre, size, wall);

            LiquidProfile water = LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.WaterName);
            GameObject shower = LiquidSourceBuilder.CreateShower(pool, water);
            shower.transform.SetParent(room.transform, false);
            shower.transform.position = centre + new Vector3(-1.1f, 0f, 1f);
            LiquidShower head = shower.GetComponentInChildren<LiquidShower>();
            head.toggleOnInteract = true;
            head.autoOnSeconds = 25f;
            head.autoOffSeconds = 35f;
            UdonSharpEditorUtility.CopyProxyToUdon(head);

            AddMannequin(room.transform, "Under Shower", centre + new Vector3(-1.1f, 0f, 1f), mannequins,
                LiquidMannequinBuilder.Create(null, "Mannequin", NextIndex(mannequins),
                    LiquidMannequinBuilder.Skin("Skin", new Color(0.86f, 0.68f, 0.58f), 0.35f), update, true,
                    LiquidSurfaceBuilder.GetSurface(LiquidSurfaceBuilder.SkinName), false, null, null));
            AddMannequin(room.transform, "By The Mirror", centre + new Vector3(0.9f, 0f, 0.6f), mannequins,
                LiquidMannequinBuilder.CreateClothed(null, "Mannequin", NextIndex(mannequins), update, true,
                    LiquidMannequinBuilder.Knit));

            // A mirror on the back wall with a sheet of glass over it that fogs.
            Material mirrorMaterial = LiquidAssets.CreateOrLoadSurfaceMaterial(LiquidSampleScene.SampleFolder + "/BathroomMirror.mat",
                new Color(0.75f, 0.78f, 0.8f), 0.97f);
            mirrorMaterial.SetFloat("_Metallic", 1f);
            Quad(room.transform, "Mirror", centre + new Vector3(0.9f, 1.5f, size.z * 0.5f - 0.1f), new Vector2(1.4f, 1.1f),
                mirrorMaterial);
            Material glass = LiquidAssets.CreateOrLoadMaterial(FoggedGlassMaterialPath, "SabaProps/Liquid/Fogged Glass");
            Quad(room.transform, "Fogged Glass", centre + new Vector3(0.9f, 1.5f, size.z * 0.5f - 0.11f), new Vector2(1.4f, 1.1f), glass);

            LiquidHumidity humidity = Humidity(room.transform, "Humidity", pool, centre, size, 0.55f, 0.75f);
            humidity.linkedShower = head;
            humidity.showerHumidity = 1f;
            humidity.steam = LiquidParticleBuilder.CreateSteam(room.transform, new Vector2(1.2f, 1.2f), LiquidAssets.MaterialFolder);
            humidity.steam.transform.position = centre + new Vector3(-1.1f, 0.1f, 1f);
            humidity.fogMaterial = FogVolume(room.transform, centre, size, BathroomFogMaterialPath, new Color(0.9f, 0.92f, 0.95f));
            humidity.fogDensity = 0.25f;
            humidity.surfaceMaterials = new[] { wall, glass };
            UdonSharpEditorUtility.CopyProxyToUdon(humidity);
            RoomLight(room.transform, pool, centre, size, new Color(1f, 0.97f, 0.92f), 1.4f);

            LiquidDemoGalleries.Label(room.transform, "Bathroom (humidity follows the shower)",
                centre + new Vector3(0f, 3.4f, -size.z * 0.5f), Quaternion.Euler(0f, 180f, 0f));
        }

        /// <summary>
        /// The same water sprayed on two bodies, one in dry air and one in damp
        /// air below the dew point: the damp one stays wet much longer.
        /// </summary>
        private static void BuildDampRoom(LiquidCanvasPool pool, Material update, List<LiquidBodyCanvas> mannequins)
        {
            var root = new GameObject(DampRoomName);
            Material device = Device();
            LiquidProfile water = LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.WaterName);
            Vector3[] spots = { new Vector3(3.5f, 0f, 13f), new Vector3(7f, 0f, 13f) };
            string[] titles = { "Dry air", "Damp air (humidity 70 %)" };

            for (int i = 0; i < spots.Length; i++)
            {
                var bay = new GameObject(titles[i]);
                bay.transform.SetParent(root.transform, false);
                bay.transform.SetPositionAndRotation(spots[i], Quaternion.Euler(0f, 180f, 0f));
                mannequins.Add(LiquidMannequinBuilder.CreateClothed(bay.transform, "Mannequin", NextIndex(mannequins), update,
                    true, LiquidMannequinBuilder.Casual));

                LiquidSprayer sprayer = LiquidSourceBuilder.CreateSprayer(bay.transform, "Sprayer", pool, water,
                    spots[i] + new Vector3(0.9f, 1.5f, -2.2f), spots[i] + new Vector3(0f, 1.2f, 0f), device);
                // One burst a minute: long enough to watch the two dry at different rates.
                sprayer.burstInterval = 60f;
                sprayer.raysPerBurst = 12;
                sprayer.coneAngle = 12f;
                UdonSharpEditorUtility.CopyProxyToUdon(sprayer);

                LiquidDemoGalleries.Label(bay.transform, titles[i], spots[i] + new Vector3(0f, 2.3f, -0.4f),
                    Quaternion.Euler(0f, 180f, 0f));
            }

            LiquidHumidity damp = Humidity(root.transform, "Humidity", pool, spots[1], new Vector3(2.4f, 3f, 2.4f), 0.7f, 0.8f);
            damp.dripProfile = null;
            UdonSharpEditorUtility.CopyProxyToUdon(damp);
        }

        // ------------------------------------------------------------------
        // Dark room
        // ------------------------------------------------------------------

        /// <summary>
        /// A closed room with a ceiling lamp and a blacklight. Ordinary paint
        /// disappears in the dark; fluorescent paint glows under the blacklight;
        /// luminous paint keeps glowing after the lights go out.
        /// </summary>
        private static void BuildDarkRoom(LiquidCanvasPool pool, Material update, List<LiquidBodyCanvas> mannequins)
        {
            Vector3 centre = new Vector3(14f, 0f, 13f);
            Vector3 size = new Vector3(7f, 3f, 5f);
            Material wall = LiquidAssets.CreateOrLoadSurfaceMaterial(DarkWallMaterialPath, new Color(0.18f, 0.18f, 0.2f), 0.1f);
            GameObject room = Room(DarkRoomName, centre, size, wall);
            Material device = Device();

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
            blacklightObject.transform.position = centre + new Vector3(0f, size.y - 0.35f, -1.4f);
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

            for (int i = 0; i < DarkRoomPaints.Length; i++)
            {
                Vector3 feet = centre + new Vector3(-2.4f + i * 1.6f, 0f, 0.8f);
                var bay = new GameObject(DarkRoomPaints[i]);
                bay.transform.SetParent(room.transform, false);
                bay.transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, 180f, 0f));
                mannequins.Add(LiquidMannequinBuilder.Create(bay.transform, "Mannequin", NextIndex(mannequins),
                    LiquidMannequinBuilder.LightSkin(), update, true,
                    LiquidSurfaceBuilder.GetSurface(LiquidSurfaceBuilder.HardClothName), false, null, null));

                LiquidSprayer sprayer = LiquidSourceBuilder.CreateSprayer(bay.transform, "Sprayer", pool,
                    LiquidSourceBuilder.GetProfile(DarkRoomPaints[i]), feet + new Vector3(0.5f, 1.4f, -1.6f),
                    feet + new Vector3(0f, 1.2f, 0f), device);
                sprayer.burstInterval = 4f;
                sprayer.raysPerBurst = 5;
                sprayer.coneAngle = 10f;
                sprayer.phaseOffset = i * 0.6f;
                UdonSharpEditorUtility.CopyProxyToUdon(sprayer);

                LiquidDemoGalleries.Label(bay.transform, DarkRoomCaptions[i], feet + new Vector3(0f, 2.15f, -0.3f),
                    Quaternion.Euler(0f, 180f, 0f)).characterSize = 0.035f;
            }

            // Switches beside the door, outside.
            Material key = LiquidAssets.CreateOrLoadSurfaceMaterial(LiquidAssets.MaterialFolder + "/PanelKey.mat",
                new Color(0.75f, 0.77f, 0.8f), 0.5f);
            var switches = new GameObject("Switches");
            switches.transform.SetParent(room.transform, false);
            switches.transform.position = centre + new Vector3(1.4f, 1.3f, -size.z * 0.5f - 0.1f);
            LiquidPrefabBuilder.Button(switches.transform, "Lamp", "Lamp", new Vector3(0f, 0f, 0f), key, zone,
                nameof(LiquidLightZone.ToggleLamp));
            LiquidPrefabBuilder.Button(switches.transform, "Blacklight", "UV", new Vector3(0.12f, 0f, 0f), key, zone,
                nameof(LiquidLightZone.ToggleBlacklight));
            LiquidPrefabBuilder.Button(switches.transform, "Automatic", "Auto", new Vector3(0.24f, 0f, 0f), key, zone,
                nameof(LiquidLightZone.ResumeAutomatic));

            LiquidDemoGalleries.Label(room.transform, "Dark room (lit / UV only / dark, or use the switches)",
                centre + new Vector3(0f, 3.4f, -size.z * 0.5f), Quaternion.Euler(0f, 180f, 0f));
        }

        // ------------------------------------------------------------------
        // Prefab corner
        // ------------------------------------------------------------------

        /// <summary>
        /// The shipped prefabs, placed as a user would: cup, bucket and water gun
        /// on a table facing a target, a faucet, a shower, and umbrellas in a
        /// patch of steady rain, one held over a mannequin.
        /// </summary>
        private static void BuildPrefabCorner(LiquidCanvasPool pool, Material update, List<LiquidBodyCanvas> mannequins)
        {
            var corner = new GameObject(PrefabCornerName);
            Material furniture = LiquidAssets.CreateOrLoadSurfaceMaterial(LiquidSampleScene.FurnitureMaterialPath,
                new Color(0.42f, 0.33f, 0.25f), 0.2f);

            Vector3 table = new Vector3(4f, 0f, -6f);
            LiquidSampleScene.Slab(corner.transform, "Table", table.x - 0.8f, table.x + 0.8f, table.z - 0.4f, table.z + 0.4f,
                0.8f, 0.8f, furniture);
            Put(corner.transform, LiquidPrefabBuilder.CupName, table + new Vector3(-0.5f, 0.8f, 0f), 0f);
            Put(corner.transform, LiquidPrefabBuilder.BucketName, table + new Vector3(0f, 0.8f, 0f), 0f);
            Put(corner.transform, LiquidPrefabBuilder.WaterGunName, table + new Vector3(0.5f, 0.86f, 0f), 0f);

            AddMannequin(corner.transform, "Target", table + new Vector3(0f, 0f, 2.3f), mannequins,
                LiquidMannequinBuilder.CreateClothed(null, "Mannequin", NextIndex(mannequins), update, true,
                    LiquidMannequinBuilder.RainGear), 180f);

            Put(corner.transform, LiquidPrefabBuilder.FaucetName, new Vector3(7f, 0f, -7f), 0f);
            Put(corner.transform, LiquidPrefabBuilder.ShowerName, new Vector3(9f, 0f, -6.5f), 0f);
            Put(corner.transform, LiquidPrefabBuilder.NozzleStandName, new Vector3(-3f, 0f, -7f), 0f);

            // Steady rain over two mannequins; one has an umbrella held over it.
            Vector3 rain = new Vector3(13f, 0f, -6f);
            LiquidWeather weather = LiquidWeatherBuilder.CreateWeather(corner.transform, "Rain Patch", pool,
                LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.WaterName), false, new Vector3(5f, 5f, 4f));
            weather.transform.parent.position = rain;
            weather.clearSeconds = 0f;
            UdonSharpEditorUtility.CopyProxyToUdon(weather);

            AddMannequin(corner.transform, "In The Rain", rain + new Vector3(-1.3f, 0f, 0f), mannequins,
                LiquidMannequinBuilder.CreateClothed(null, "Mannequin", NextIndex(mannequins), update, true,
                    LiquidMannequinBuilder.Casual), 180f);
            AddMannequin(corner.transform, "Under An Umbrella", rain + new Vector3(1.3f, 0f, 0f), mannequins,
                LiquidMannequinBuilder.CreateClothed(null, "Mannequin", NextIndex(mannequins), update, true,
                    LiquidMannequinBuilder.Casual), 180f);
            Put(corner.transform, LiquidPrefabBuilder.UmbrellaName, rain + new Vector3(1.3f, 1.25f, 0.15f), 0f);
            Put(corner.transform, LiquidPrefabBuilder.UmbrellaName, rain + new Vector3(0f, 0f, -2.6f), 0f);

            LiquidDemoGalleries.Label(corner.transform, "Prefabs: pick up the cup, bucket, water gun and umbrellas",
                table + new Vector3(0f, 1.9f, 0f), Quaternion.Euler(0f, 180f, 0f));
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static int NextIndex(List<LiquidBodyCanvas> mannequins)
        {
            return IndexBase + mannequins.Count;
        }

        private static void AddMannequin(Transform parent, string name, Vector3 feet, List<LiquidBodyCanvas> mannequins,
            LiquidBodyCanvas mannequin, float yaw = 180f)
        {
            var spot = new GameObject(name);
            spot.transform.SetParent(parent, false);
            spot.transform.SetPositionAndRotation(feet, Quaternion.Euler(0f, yaw, 0f));
            mannequin.transform.parent.SetParent(spot.transform, false);
            mannequins.Add(mannequin);
        }

        private static GameObject Put(Transform parent, string prefab, Vector3 position, float yaw)
        {
            GameObject instance = LiquidPrefabBuilder.Place(prefab, parent);
            instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            return instance;
        }

        /// <summary>
        /// A closed room standing on the ground: four walls and a roof, with a
        /// doorway in the wall facing -Z. Walls and roof are solid, so they cast
        /// shadows and block rain, spray and sunlight.
        /// </summary>
        private static GameObject Room(string name, Vector3 centre, Vector3 size, Material wall)
        {
            var room = new GameObject(name);
            const float t = 0.15f;
            const float door = 1.4f;
            float x0 = centre.x - size.x * 0.5f;
            float x1 = centre.x + size.x * 0.5f;
            float z0 = centre.z - size.z * 0.5f;
            float z1 = centre.z + size.z * 0.5f;
            float h = size.y;

            LiquidSampleScene.Slab(room.transform, "Back Wall", x0, x1, z1 - t, z1, h, h, wall);
            LiquidSampleScene.Slab(room.transform, "Left Wall", x0, x0 + t, z0, z1, h, h, wall);
            LiquidSampleScene.Slab(room.transform, "Right Wall", x1 - t, x1, z0, z1, h, h, wall);
            LiquidSampleScene.Slab(room.transform, "Front Wall Left", x0, centre.x - door * 0.5f, z0, z0 + t, h, h, wall);
            LiquidSampleScene.Slab(room.transform, "Front Wall Right", centre.x + door * 0.5f, x1, z0, z0 + t, h, h, wall);
            LiquidSampleScene.Slab(room.transform, "Lintel", centre.x - door * 0.5f, centre.x + door * 0.5f, z0, z0 + t, h, 0.8f, wall);
            LiquidSampleScene.Slab(room.transform, "Roof", x0, x1, z0, z1, h + t, t, wall);
            return room;
        }

        /// <summary>
        /// A ceiling light for a closed room, and a light zone that hands it to
        /// the liquid, which would otherwise be lit by the sun through the roof.
        /// </summary>
        private static void RoomLight(Transform parent, LiquidCanvasPool pool, Vector3 floorCentre, Vector3 size,
            Color colour, float intensity)
        {
            var lampObject = new GameObject("Ceiling Light");
            lampObject.transform.SetParent(parent, false);
            lampObject.transform.position = floorCentre + new Vector3(0f, size.y - 0.3f, 0f);
            Light lamp = lampObject.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = Mathf.Max(size.x, size.z) * 1.4f;
            lamp.intensity = intensity;
            lamp.color = colour;

            var zoneObject = new GameObject("Light Zone");
            zoneObject.transform.SetParent(parent, false);
            zoneObject.transform.position = floorCentre + new Vector3(0f, size.y * 0.5f, 0f);
            LiquidLightZone zone = zoneObject.AddUdonSharpComponent<LiquidLightZone>();
            zone.pool = pool;
            zone.areaSize = size;
            zone.lamp = lamp;
            zone.automatic = false;
            UdonSharpEditorUtility.CopyProxyToUdon(zone);
        }

        private static LiquidHumidity Humidity(Transform parent, string name, LiquidCanvasPool pool, Vector3 floorCentre,
            Vector3 size, float humidity, float threshold)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = floorCentre + new Vector3(0f, size.y * 0.5f, 0f);
            LiquidHumidity source = go.AddUdonSharpComponent<LiquidHumidity>();
            source.pool = pool;
            source.dripProfile = LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.WaterName);
            source.areaSize = size;
            source.humidity = humidity;
            source.condensationThreshold = threshold;
            UdonSharpEditorUtility.CopyProxyToUdon(source);
            return source;
        }

        /// <summary>A haze filling the room: a unit cube scaled to the room, drawn with the fog shader.</summary>
        private static Material FogVolume(Transform parent, Vector3 floorCentre, Vector3 size, string materialPath, Color colour)
        {
            Material material = LiquidAssets.CreateOrLoadMaterial(materialPath, "SabaProps/Liquid/Fog Volume");
            material.SetColor("_Color", colour);
            material.SetFloat("_Density", 0f);
            EditorUtility.SetDirty(material);

            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Fog Volume";
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.transform.SetParent(parent, false);
            cube.transform.position = floorCentre + new Vector3(0f, size.y * 0.5f, 0f);
            cube.transform.localScale = size - new Vector3(0.3f, 0.02f, 0.3f);
            var renderer = cube.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return material;
        }

        private static void Quad(Transform parent, string name, Vector3 position, Vector2 size, Material material)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.position = position;
            // A quad faces -Z: towards the room from the back wall.
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            quad.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Material Device()
        {
            return LiquidAssets.CreateOrLoadSurfaceMaterial(LiquidDemoGalleries.DeviceMaterialPath,
                new Color(0.3f, 0.32f, 0.35f), 0.6f);
        }

        private static string Summarise()
        {
            var text = new StringBuilder();
            text.AppendLine($"[SabaProps Liquid] 操作と環境のシーンを {ScenePath} に作成しました。");
            text.AppendLine($"・{NozzleBenchName}（左手前）: 1 回、定期、連続のノズル。操作盤で量、距離、速さ、断面を変えられます。");
            text.AppendLine($"・{ViscosityRowName}（右手前）: 水からシロップまで、粘性ごとのパーティクル（飛沫、塊、糸）を比べます。");
            text.AppendLine($"・{SaunaName}、{BathroomName}（奥の左）: 湯気と霧、結露、体から垂れる水滴。浴室はシャワーに合わせて湿度が変わり、鏡が曇ります。");
            text.AppendLine($"・{DampRoomName}（奥の中央）: 乾いた空気と湿った空気で、同じ水の乾き方を比べます。");
            text.AppendLine($"・{DarkRoomName}（奥の右）: 普通の塗料、蛍光塗料、蓄光塗料を、点灯、紫外線のみ、消灯で比べます。");
            text.AppendLine($"・{PrefabCornerName}（手前）: コップ、バケツ、水鉄砲、水道、シャワー、ノズル台、傘の Prefab。");
            return text.ToString();
        }
    }
}
