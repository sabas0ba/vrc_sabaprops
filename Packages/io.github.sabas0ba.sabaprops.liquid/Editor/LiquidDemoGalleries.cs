using System.Collections.Generic;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Liquid.Editors
{
    /// <summary>
    /// The comparison rows of the demo world. Everything here runs by itself:
    /// sprayers fire on a schedule, showers cycle and tank levels rise and fall,
    /// all driven by server time, onto mannequins turning on turntables.
    /// </summary>
    public static class LiquidDemoGalleries
    {
        public const string LiquidRowName = "Liquid Comparison";
        public const string SourceRowName = "Source Comparison";

        /// <summary>Distance of each row from the centre line, and the spacing along it.</summary>
        public const float RowOffset = 13f;
        public const float RowSpacing = 3f;
        public const float RowStartZ = -6f;

        public const string LabelMaterialPath = LiquidSampleScene.SampleFolder + "/Label.mat";
        public const string DeviceMaterialPath = LiquidSampleScene.SampleFolder + "/Device.mat";
        public const string GlassMaterialPath = LiquidSampleScene.SampleFolder + "/Glass.mat";

        /// <summary>
        /// One mannequin per liquid preset along x = -13, each hit by an
        /// identical sprayer, so the only difference between them is the liquid.
        /// </summary>
        public static void BuildLiquidRow(LiquidCanvasPool pool, Material update, List<LiquidBodyCanvas> mannequins)
        {
            var row = new GameObject(LiquidRowName);
            Material device = Device();

            string[] presets = LiquidSourceBuilder.PresetNames;
            for (int i = 0; i < presets.Length; i++)
            {
                LiquidProfile profile = LiquidSourceBuilder.GetProfile(presets[i]);
                Vector3 feet = new Vector3(-RowOffset, 0f, RowStartZ + i * RowSpacing);

                var bay = new GameObject(presets[i]);
                bay.transform.SetParent(row.transform, false);
                bay.transform.position = feet;
                bay.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

                LiquidBodyCanvas mannequin = LiquidMannequinBuilder.Create(bay.transform, "Mannequin", mannequins.Count,
                    LiquidMannequinBuilder.LightSkin(), update, true);
                mannequins.Add(mannequin);

                Vector3 nozzle = feet + new Vector3(2.4f, 1.5f, 0.9f);
                LiquidSprayer sprayer = LiquidSourceBuilder.CreateSprayer(bay.transform, "Sprayer", pool, profile,
                    nozzle, feet + new Vector3(0f, 1.25f, 0f), device);
                sprayer.burstInterval = 1.4f;
                sprayer.raysPerBurst = 8;
                sprayer.coneAngle = 9f;
                sprayer.hitRadius = 0.12f + profile.viscosity * 0.08f;
                sprayer.sweepAngle = 10f;
                sprayer.phaseOffset = i * 0.2f;
                UdonSharpEditorUtility.CopyProxyToUdon(sprayer);

                Label(bay.transform, presets[i], feet + new Vector3(0.9f, 2.25f, 0f), Quaternion.Euler(0f, 90f, 0f));
            }
        }

        /// <summary>
        /// One mannequin per kind of Source along x = +13: a cycling shower, a
        /// tank whose level rises and falls, a mud tub, a sweeping water jet and
        /// a slow drip of syrup, then the same spray on a light and a dark body.
        /// </summary>
        public static void BuildSourceRow(LiquidCanvasPool pool, Material update, List<LiquidBodyCanvas> mannequins)
        {
            var row = new GameObject(SourceRowName);
            Material device = Device();
            LiquidProfile water = LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.WaterName);
            LiquidProfile mud = LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.MudName);
            LiquidProfile syrup = LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.SyrupName);
            LiquidProfile paint = LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.BluePaintName);
            Quaternion facing = Quaternion.Euler(0f, -90f, 0f);

            // Shower: 12 s on, 18 s off, so both the run-off and the drying show.
            Transform bay = Bay(row.transform, "Shower", 0, facing);
            mannequins.Add(LiquidMannequinBuilder.Create(bay, "Mannequin", mannequins.Count, LiquidMannequinBuilder.LightSkin(), update, true));
            GameObject shower = LiquidSourceBuilder.CreateShower(pool, water);
            shower.transform.SetParent(bay, false);
            LiquidShower head = shower.GetComponentInChildren<LiquidShower>();
            head.toggleOnInteract = false;
            head.autoOnSeconds = 12f;
            head.autoOffSeconds = 18f;
            UdonSharpEditorUtility.CopyProxyToUdon(head);
            Label(bay, "Shower", bay.position + new Vector3(-0.9f, 2.5f, 0f), facing);

            // Tank: the level swings between the knees and the chest, so the wet line
            // follows it up and drains after it on the way down.
            bay = Bay(row.transform, "Tank", 1, facing);
            mannequins.Add(LiquidMannequinBuilder.Create(bay, "Mannequin", mannequins.Count, LiquidMannequinBuilder.LightSkin(), update, true));
            GameObject tank = Tub(bay, "Water Tank", new Vector3(1.4f, 1.6f, 1.4f), water, pool,
                LiquidAssets.CreateOrLoadTransparentMaterial(LiquidSampleScene.WaterSurfaceMaterialPath, new Color(0.35f, 0.62f, 0.78f, 0.35f), 0.95f));
            LiquidImmersionVolume tankVolume = tank.GetComponentInChildren<LiquidImmersionVolume>();
            tankVolume.waveAmplitude = 0.4f;
            tankVolume.wavePeriod = 20f;
            UdonSharpEditorUtility.CopyProxyToUdon(tankVolume);
            Label(bay, "Immersion (water)", bay.position + new Vector3(-0.9f, 2.5f, 0f), facing);

            // Mud tub: the level rises over the knees and falls back to the shins,
            // so the coat left on the legs shows above the surface.
            bay = Bay(row.transform, "Mud Tub", 2, facing);
            mannequins.Add(LiquidMannequinBuilder.Create(bay, "Mannequin", mannequins.Count, LiquidMannequinBuilder.LightSkin(), update, true));
            GameObject mudTub = Tub(bay, "Mud Tub", new Vector3(1.4f, 0.5f, 1.4f), mud, pool,
                LiquidAssets.CreateOrLoadSurfaceMaterial(LiquidSampleScene.MudSurfaceMaterialPath, new Color(0.3f, 0.21f, 0.13f), 0.45f));
            LiquidImmersionVolume mudVolume = mudTub.GetComponentInChildren<LiquidImmersionVolume>();
            mudVolume.waveAmplitude = 0.25f;
            mudVolume.wavePeriod = 18f;
            UdonSharpEditorUtility.CopyProxyToUdon(mudVolume);
            Label(bay, "Immersion (mud)", bay.position + new Vector3(-0.9f, 2.5f, 0f), facing);

            // Water jet: a thin, fast, sweeping stream, the way a water gun reads.
            bay = Bay(row.transform, "Water Jet", 3, facing);
            mannequins.Add(LiquidMannequinBuilder.Create(bay, "Mannequin", mannequins.Count, LiquidMannequinBuilder.LightSkin(), update, true));
            LiquidSprayer jet = LiquidSourceBuilder.CreateSprayer(bay, "Jet", pool, water,
                bay.position + new Vector3(-2.6f, 1.3f, 0f), bay.position + new Vector3(0f, 1.2f, 0f), device);
            jet.burstInterval = 0.15f;
            jet.raysPerBurst = 1;
            jet.coneAngle = 1.5f;
            jet.hitRadius = 0.07f;
            jet.amountPerHit = 0.35f;
            jet.sweepAngle = 6f;
            jet.sweepPeriod = 5f;
            jet.particlesPerBurst = 12;
            UdonSharpEditorUtility.CopyProxyToUdon(jet);
            Label(bay, "Jet (water gun)", bay.position + new Vector3(-0.9f, 2.5f, 0f), facing);

            // Drip: syrup falling on the head from above.
            bay = Bay(row.transform, "Drip", 4, facing);
            mannequins.Add(LiquidMannequinBuilder.Create(bay, "Mannequin", mannequins.Count, LiquidMannequinBuilder.LightSkin(), update, true));
            LiquidSprayer drip = LiquidSourceBuilder.CreateSprayer(bay, "Drip", pool, syrup,
                bay.position + new Vector3(0.05f, 2.6f, 0f), bay.position + new Vector3(0f, 1.7f, 0f), device);
            drip.burstInterval = 0.8f;
            drip.raysPerBurst = 1;
            drip.coneAngle = 2f;
            drip.hitRadius = 0.08f;
            drip.amountPerHit = 0.6f;
            drip.particlesPerBurst = 4;
            UdonSharpEditorUtility.CopyProxyToUdon(drip);
            Label(bay, "Drip (syrup)", bay.position + new Vector3(-0.9f, 2.9f, 0f), facing);

            // The same water and paint on a light plastic body and a dark cloth one.
            for (int i = 0; i < 2; i++)
            {
                bool dark = i == 1;
                bay = Bay(row.transform, dark ? "Dark Body" : "Light Body", 5 + i, facing);
                mannequins.Add(LiquidMannequinBuilder.Create(bay, "Mannequin", mannequins.Count,
                    dark ? LiquidMannequinBuilder.DarkSkin() : LiquidMannequinBuilder.LightSkin(), update, true));
                LiquidSprayer spray = LiquidSourceBuilder.CreateSprayer(bay, "Sprayer", pool, water,
                    bay.position + new Vector3(-2.4f, 1.5f, 0.9f), bay.position + new Vector3(0f, 1.25f, 0f), device);
                spray.burstInterval = 1.2f;
                spray.raysPerBurst = 5;
                spray.coneAngle = 10f;
                spray.sweepAngle = 12f;
                UdonSharpEditorUtility.CopyProxyToUdon(spray);

                LiquidSprayer paintSpray = LiquidSourceBuilder.CreateSprayer(bay, "Paint Sprayer", pool, paint,
                    bay.position + new Vector3(-2.4f, 1.1f, -0.9f), bay.position + new Vector3(0f, 1f, 0f), device);
                paintSpray.burstInterval = 3f;
                paintSpray.raysPerBurst = 3;
                paintSpray.coneAngle = 8f;
                paintSpray.phaseOffset = 0.7f;
                UdonSharpEditorUtility.CopyProxyToUdon(paintSpray);

                Label(bay, dark ? "Dark cloth" : "Light plastic", bay.position + new Vector3(-0.9f, 2.5f, 0f), facing);
            }
        }

        public const string SurfaceRowName = "Surface Comparison";
        public const string BodyColourRowName = "Body Colour Comparison";
        public const string LiquidColourRowName = "Liquid Colour Comparison";

        /// <summary>Depth of the three rows behind the mirror, facing the spawn.</summary>
        public const float SurfaceRowZ = 12f;
        public const float BodyColourRowZ = 16.5f;
        public const float LiquidColourRowZ = 21f;

        /// <summary>
        /// The same water and paint on each kind of surface: soft cloth, hard
        /// cloth, leather, hair, skin, plastic, and a body with avatar regions
        /// (hair on the head, skin on the face and hands, cloth elsewhere).
        /// </summary>
        public static void BuildSurfaceRow(LiquidCanvasPool pool, Material update, List<LiquidBodyCanvas> mannequins)
        {
            var row = new GameObject(SurfaceRowName);
            Material device = Device();
            LiquidProfile water = LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.WaterName);
            LiquidProfile paint = LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.RedPaintName);

            string[] names = LiquidSurfaceBuilder.PresetNames;
            Color[] colours =
            {
                new Color(0.46f, 0.48f, 0.52f), new Color(0.24f, 0.3f, 0.44f), new Color(0.2f, 0.12f, 0.07f),
                new Color(0.13f, 0.1f, 0.08f), new Color(0.86f, 0.68f, 0.58f), new Color(0.86f, 0.86f, 0.85f),
            };
            float[] smoothness = { 0.05f, 0.2f, 0.55f, 0.4f, 0.35f, 0.75f };

            for (int i = 0; i < names.Length; i++)
            {
                Transform bay = RearBay(row.transform, names[i], i, SurfaceRowZ);
                mannequins.Add(LiquidMannequinBuilder.Create(bay, "Mannequin", mannequins.Count,
                    LiquidMannequinBuilder.Skin(names[i], colours[i], smoothness[i]), update, true,
                    LiquidSurfaceBuilder.GetSurface(names[i]), false, null, null));
                WaterAndPaint(bay, pool, water, paint, device);
                Label(bay, names[i], bay.position + new Vector3(0f, 2.5f, -0.9f), bay.rotation);
            }

            Transform avatar = RearBay(row.transform, "Avatar Regions", names.Length, SurfaceRowZ);
            mannequins.Add(LiquidMannequinBuilder.Create(avatar, "Mannequin", mannequins.Count,
                LiquidMannequinBuilder.Skin(LiquidSurfaceBuilder.SoftClothName, colours[0], smoothness[0]), update, true,
                LiquidSurfaceBuilder.GetSurface(LiquidSurfaceBuilder.SoftClothName), true,
                LiquidMannequinBuilder.Skin(LiquidSurfaceBuilder.HairName, colours[3], smoothness[3]),
                LiquidMannequinBuilder.Skin(LiquidSurfaceBuilder.SkinName, colours[4], smoothness[4])));
            WaterAndPaint(avatar, pool, water, paint, device);
            Label(avatar, "Avatar regions", avatar.position + new Vector3(0f, 2.5f, -0.9f), avatar.rotation);
        }

        /// <summary>The same water and mud on bodies of different colours, all soft cloth.</summary>
        public static void BuildBodyColourRow(LiquidCanvasPool pool, Material update, List<LiquidBodyCanvas> mannequins)
        {
            var row = new GameObject(BodyColourRowName);
            Material device = Device();
            LiquidProfile water = LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.WaterName);
            LiquidProfile mud = LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.MudName);

            string[] names = { "White", "Light Grey", "Grey", "Black", "Red", "Blue", "Skin Tone" };
            Color[] colours =
            {
                new Color(0.92f, 0.92f, 0.9f), new Color(0.68f, 0.68f, 0.68f), new Color(0.4f, 0.4f, 0.4f),
                new Color(0.06f, 0.06f, 0.06f), new Color(0.62f, 0.08f, 0.09f), new Color(0.1f, 0.22f, 0.55f),
                new Color(0.86f, 0.68f, 0.58f),
            };

            for (int i = 0; i < names.Length; i++)
            {
                Transform bay = RearBay(row.transform, names[i], i, BodyColourRowZ);
                mannequins.Add(LiquidMannequinBuilder.Create(bay, "Mannequin", mannequins.Count,
                    LiquidMannequinBuilder.Skin("Colour" + names[i], colours[i], 0.12f), update, true,
                    LiquidSurfaceBuilder.GetSurface(LiquidSurfaceBuilder.SoftClothName), false, null, null));
                WaterAndPaint(bay, pool, water, mud, device);
                Label(bay, names[i] + " body", bay.position + new Vector3(0f, 2.5f, -0.9f), bay.rotation);
            }
        }

        /// <summary>
        /// Paint from black to white on the same pale body, then red, blue, yellow
        /// and white sprayed together onto a pale body and a black one.
        /// </summary>
        public static void BuildLiquidColourRow(LiquidCanvasPool pool, Material update, List<LiquidBodyCanvas> mannequins)
        {
            var row = new GameObject(LiquidColourRowName);
            Material device = Device();

            string[] greys = LiquidSourceBuilder.GreyscalePaintNames;
            for (int i = 0; i < greys.Length; i++)
            {
                Transform bay = RearBay(row.transform, greys[i], i, LiquidColourRowZ);
                mannequins.Add(LiquidMannequinBuilder.Create(bay, "Mannequin", mannequins.Count,
                    LiquidMannequinBuilder.LightSkin(), update, true,
                    LiquidSurfaceBuilder.GetSurface(LiquidSurfaceBuilder.HardClothName), false, null, null));
                LiquidSprayer spray = LiquidSourceBuilder.CreateSprayer(bay, "Sprayer", pool,
                    LiquidSourceBuilder.GetProfile(greys[i]),
                    bay.position + new Vector3(0.9f, 1.5f, -2.4f), bay.position + new Vector3(0f, 1.25f, 0f), device);
                spray.burstInterval = 1.6f;
                spray.sweepAngle = 12f;
                spray.phaseOffset = i * 0.3f;
                UdonSharpEditorUtility.CopyProxyToUdon(spray);
                Label(bay, greys[i], bay.position + new Vector3(0f, 2.5f, -0.9f), bay.rotation);
            }

            string[] mix =
            {
                LiquidSourceBuilder.RedPaintName, LiquidSourceBuilder.BluePaintName,
                LiquidSourceBuilder.YellowPaintName, LiquidSourceBuilder.WhitePaintName,
            };

            for (int b = 0; b < 2; b++)
            {
                bool dark = b == 1;
                Transform bay = RearBay(row.transform, dark ? "Mixed On Black" : "Mixed", greys.Length + b, LiquidColourRowZ);
                mannequins.Add(LiquidMannequinBuilder.Create(bay, "Mannequin", mannequins.Count,
                    dark ? LiquidMannequinBuilder.DarkSkin() : LiquidMannequinBuilder.LightSkin(), update, true,
                    LiquidSurfaceBuilder.GetSurface(LiquidSurfaceBuilder.HardClothName), false, null, null));

                for (int c = 0; c < mix.Length; c++)
                {
                    float angle = (c - 1.5f) * 22f;
                    Vector3 from = bay.position + Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 1.2f + c * 0.15f, -2.4f);
                    LiquidSprayer spray = LiquidSourceBuilder.CreateSprayer(bay, mix[c] + " Sprayer", pool,
                        LiquidSourceBuilder.GetProfile(mix[c]), from, bay.position + new Vector3(0f, 1.2f, 0f), device);
                    spray.burstInterval = 2f;
                    spray.raysPerBurst = 5;
                    spray.phaseOffset = c * 0.5f;
                    spray.sweepAngle = 8f;
                    UdonSharpEditorUtility.CopyProxyToUdon(spray);
                }

                Label(bay, dark ? "Mixed on black" : "Mixed colours", bay.position + new Vector3(0f, 2.5f, -0.9f), bay.rotation);
            }
        }

        /// <summary>Water every 1.2 s from the right and a second liquid every 3 s from the left.</summary>
        private static void WaterAndPaint(Transform bay, LiquidCanvasPool pool, LiquidProfile water, LiquidProfile second,
            Material device)
        {
            LiquidSprayer spray = LiquidSourceBuilder.CreateSprayer(bay, "Water Sprayer", pool, water,
                bay.position + new Vector3(0.9f, 1.5f, -2.4f), bay.position + new Vector3(0f, 1.25f, 0f), device);
            spray.burstInterval = 1.2f;
            spray.coneAngle = 10f;
            spray.sweepAngle = 12f;
            UdonSharpEditorUtility.CopyProxyToUdon(spray);

            LiquidSprayer other = LiquidSourceBuilder.CreateSprayer(bay, "Second Sprayer", pool, second,
                bay.position + new Vector3(-0.9f, 1.1f, -2.4f), bay.position + new Vector3(0f, 1f, 0f), device);
            other.burstInterval = 2f;
            other.raysPerBurst = 5;
            other.coneAngle = 8f;
            other.phaseOffset = 0.7f;
            UdonSharpEditorUtility.CopyProxyToUdon(other);
        }

        /// <summary>A bay in one of the rows behind the mirror, facing the spawn (-Z).</summary>
        private static Transform RearBay(Transform row, string name, int index, float z)
        {
            var bay = new GameObject(name);
            bay.transform.SetParent(row, false);
            bay.transform.position = new Vector3(-9f + index * RowSpacing, 0f, z);
            bay.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            return bay.transform;
        }

        private static Transform Bay(Transform row, string name, int index, Quaternion facing)
        {
            var bay = new GameObject(name);
            bay.transform.SetParent(row, false);
            bay.transform.position = new Vector3(RowOffset, 0f, RowStartZ + index * RowSpacing);
            bay.transform.rotation = facing;
            return bay.transform;
        }

        /// <summary>
        /// A glass-walled tub standing on the ground, with an immersion volume
        /// from the floor to <paramref name="size"/>.y and a surface to match.
        /// </summary>
        private static GameObject Tub(Transform parent, string name, Vector3 size, LiquidProfile profile,
            LiquidCanvasPool pool, Material surfaceMaterial)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            Material glass = LiquidAssets.CreateOrLoadTransparentMaterial(GlassMaterialPath, new Color(0.8f, 0.9f, 0.95f, 0.12f), 0.95f);

            for (int side = 0; side < 4; side++)
            {
                GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = "Wall " + side;
                Object.DestroyImmediate(panel.GetComponent<Collider>());
                panel.transform.SetParent(root.transform, false);
                bool alongX = side < 2;
                float offset = (side % 2 == 0 ? 1f : -1f) * (alongX ? size.z : size.x) * 0.5f;
                panel.transform.localPosition = alongX
                    ? new Vector3(0f, size.y * 0.5f, offset)
                    : new Vector3(offset, size.y * 0.5f, 0f);
                panel.transform.localScale = alongX
                    ? new Vector3(size.x, size.y, 0.02f)
                    : new Vector3(0.02f, size.y, size.z);
                panel.GetComponent<Renderer>().sharedMaterial = glass;
            }

            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
            surface.name = "Surface";
            Object.DestroyImmediate(surface.GetComponent<Collider>());
            surface.transform.SetParent(root.transform, false);
            surface.transform.localPosition = new Vector3(0f, size.y * 0.8f, 0f);
            surface.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            surface.transform.localScale = new Vector3(size.x, size.z, 1f);
            surface.GetComponent<Renderer>().sharedMaterial = surfaceMaterial;

            GameObject volume = LiquidSourceBuilder.CreateImmersionVolume("Volume", pool, profile,
                new Vector3(size.x, size.y * 0.8f + 0.2f, size.z));
            volume.transform.SetParent(root.transform, false);
            volume.transform.localPosition = new Vector3(0f, size.y * 0.8f, 0f);
            LiquidImmersionVolume immersion = volume.GetComponent<LiquidImmersionVolume>();
            immersion.surface = surface.transform;
            UdonSharpEditorUtility.CopyProxyToUdon(immersion);
            return root;
        }

        /// <summary>A world-space text label facing the walkway.</summary>
        public static void Label(Transform parent, string text, Vector3 position, Quaternion facing)
        {
            var label = new GameObject("Label");
            label.transform.SetParent(parent, false);
            label.transform.position = position;
            // TextMesh reads towards its own -Z; turn it to face the walkway.
            label.transform.rotation = facing * Quaternion.Euler(0f, 180f, 0f);

            var mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.characterSize = 0.05f;
            mesh.fontSize = 64;
            mesh.color = new Color(0.95f, 0.95f, 0.92f);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                mesh.font = font;
                label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
        }

        private static Material Device()
        {
            return LiquidAssets.CreateOrLoadSurfaceMaterial(DeviceMaterialPath, new Color(0.3f, 0.32f, 0.35f), 0.6f);
        }
    }
}
