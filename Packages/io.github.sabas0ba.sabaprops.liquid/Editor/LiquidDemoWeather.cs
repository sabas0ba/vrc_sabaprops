using System.Collections.Generic;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Liquid.Editors
{
    /// <summary>
    /// The clothed figures and the two weather yards of the demo world.
    /// <para>
    /// The yards stand side by side behind the mirror: rain on the left, snow
    /// on the right. Each has plain and clothed mannequins in the open and one
    /// under a roof, and ground that gets wet or snowed on, so the effect of
    /// shelter shows next to the effect of the weather.
    /// </para>
    /// </summary>
    public static class LiquidDemoWeather
    {
        public const string ClothedRowName = "Clothed Figures";
        public const string RainYardName = "Rain Yard";
        public const string SnowYardName = "Snow Yard";

        public const string RainGroundMaterialPath = LiquidSampleScene.SampleFolder + "/RainGround.mat";
        public const string SnowGroundMaterialPath = LiquidSampleScene.SampleFolder + "/SnowGround.mat";
        public const string RoofMaterialPath = LiquidSampleScene.SampleFolder + "/SnowRoof.mat";
        public const string RainRoofMaterialPath = LiquidSampleScene.SampleFolder + "/RainRoof.mat";

        public static readonly Vector3 RainYardCentre = new Vector3(-6.2f, 0f, 18.5f);
        public static readonly Vector3 SnowYardCentre = new Vector3(6.2f, 0f, 18.5f);
        public static readonly Vector3 YardSize = new Vector3(11f, 7f, 11f);

        /// <summary>Rain: 40 s on, 50 s off. Snow: 60 s on, 60 s off, long enough to cover and melt.</summary>
        public const float RainOnSeconds = 40f;
        public const float RainOffSeconds = 50f;
        public const float SnowOnSeconds = 60f;
        public const float SnowOffSeconds = 60f;

        private static readonly LiquidMannequinBuilder.Outfit[] Outfits =
        {
            LiquidMannequinBuilder.Casual, LiquidMannequinBuilder.RainGear, LiquidMannequinBuilder.Knit,
        };

        /// <summary>
        /// Clothed mannequins along x = -13, facing +x: each outfit once with
        /// water and mud, and once with water and paint.
        /// </summary>
        public static void BuildClothedRow(LiquidCanvasPool pool, Material update, List<LiquidBodyCanvas> mannequins)
        {
            var row = new GameObject(ClothedRowName);
            Material device = LiquidAssets.CreateOrLoadSurfaceMaterial(
                LiquidDemoGalleries.DeviceMaterialPath, new Color(0.3f, 0.32f, 0.35f), 0.6f);
            LiquidProfile water = LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.WaterName);
            LiquidProfile[] seconds =
            {
                LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.MudName),
                LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.RedPaintName),
            };
            string[] secondNames = { "mud", "paint" };
            Quaternion facing = Quaternion.Euler(0f, 90f, 0f);

            for (int s = 0; s < seconds.Length; s++)
            {
                for (int o = 0; o < Outfits.Length; o++)
                {
                    int index = s * Outfits.Length + o;
                    LiquidMannequinBuilder.Outfit outfit = Outfits[o];
                    Vector3 feet = new Vector3(-LiquidDemoGalleries.RowOffset, 0f,
                        LiquidDemoGalleries.RowStartZ + index * LiquidDemoGalleries.RowSpacing);

                    var bay = new GameObject(outfit.Name + " + " + secondNames[s]);
                    bay.transform.SetParent(row.transform, false);
                    bay.transform.SetPositionAndRotation(feet, facing);

                    mannequins.Add(LiquidMannequinBuilder.CreateClothed(bay.transform, "Mannequin", mannequins.Count,
                        update, true, outfit));

                    LiquidSprayer spray = LiquidSourceBuilder.CreateSprayer(bay.transform, "Water Sprayer", pool, water,
                        feet + new Vector3(2.4f, 1.5f, 0.9f), feet + new Vector3(0f, 1.25f, 0f), device);
                    spray.burstInterval = 1.3f;
                    spray.coneAngle = 10f;
                    spray.sweepAngle = 12f;
                    spray.phaseOffset = index * 0.2f;
                    UdonSharpEditorUtility.CopyProxyToUdon(spray);

                    LiquidSprayer other = LiquidSourceBuilder.CreateSprayer(bay.transform, "Second Sprayer", pool,
                        seconds[s], feet + new Vector3(2.4f, 1.1f, -0.9f), feet + new Vector3(0f, 1f, 0f), device);
                    other.burstInterval = 2.5f;
                    other.raysPerBurst = 5;
                    other.coneAngle = 8f;
                    other.phaseOffset = 0.7f + index * 0.2f;
                    UdonSharpEditorUtility.CopyProxyToUdon(other);

                    LiquidDemoGalleries.Label(bay.transform, outfit.Name + ", water + " + secondNames[s],
                        feet + new Vector3(0.9f, 2.25f, 0f), facing);
                }
            }
        }

        /// <summary>The rain yard and the snow yard, with their mannequins.</summary>
        public static void BuildWeatherYards(LiquidCanvasPool pool, Material update, List<LiquidBodyCanvas> mannequins)
        {
            BuildYard(pool, update, mannequins, false);
            BuildYard(pool, update, mannequins, true);
        }

        private static void BuildYard(LiquidCanvasPool pool, Material update, List<LiquidBodyCanvas> mannequins, bool snow)
        {
            Vector3 centre = snow ? SnowYardCentre : RainYardCentre;
            var yard = new GameObject(snow ? SnowYardName : RainYardName);
            yard.transform.position = centre;

            Material ground = LiquidWeatherBuilder.CreateOrLoadGroundMaterial(
                snow ? SnowGroundMaterialPath : RainGroundMaterialPath, new Color(0.5f, 0.5f, 0.47f), 0.1f);
            Material roof = LiquidWeatherBuilder.CreateOrLoadGroundMaterial(
                snow ? RoofMaterialPath : RainRoofMaterialPath, new Color(0.45f, 0.3f, 0.22f), 0.25f);

            // The yard's floor: a thin plate over the ground, drawn with the weather material.
            Box(yard.transform, "Floor", new Vector3(0f, 0.01f, 0f), new Vector3(YardSize.x, 0.02f, YardSize.z), ground);

            LiquidWeather weather = LiquidWeatherBuilder.CreateWeather(yard.transform, snow ? "Snowfall Area" : "Rainfall Area",
                pool, snow ? null : LiquidSourceBuilder.GetProfile(LiquidSourceBuilder.WaterName), snow, YardSize);
            weather.precipitationSeconds = snow ? SnowOnSeconds : RainOnSeconds;
            weather.clearSeconds = snow ? SnowOffSeconds : RainOffSeconds;
            weather.groundBuildSeconds = snow ? 30f : 15f;
            weather.groundClearSeconds = 45f;
            weather.groundDrySeconds = 20f;
            weather.groundMaterials = new[] { ground, roof };
            UdonSharpEditorUtility.CopyProxyToUdon(weather);
            EditorUtility.SetDirty(weather);

            // Four in the open, facing the spawn: a plain body and the three outfits.
            Quaternion facing = Quaternion.Euler(0f, 180f, 0f);
            for (int i = 0; i < 4; i++)
            {
                var spot = new GameObject("Spot " + (i + 1));
                spot.transform.SetParent(yard.transform, false);
                spot.transform.localPosition = new Vector3(-3.9f + i * 2.6f, 0.02f, -2f);
                spot.transform.localRotation = facing;

                LiquidBodyCanvas mannequin = i == 0
                    ? LiquidMannequinBuilder.Create(spot.transform, "Mannequin", mannequins.Count,
                        LiquidMannequinBuilder.LightSkin(), update, true)
                    : LiquidMannequinBuilder.CreateClothed(spot.transform, "Mannequin", mannequins.Count, update, true,
                        Outfits[i - 1]);
                mannequins.Add(mannequin);
            }

            // One under a roof, which stays dry. The roof is drawn with the weather material, so it
            // gets wet or snowed on itself.
            var shelter = new GameObject("Shelter");
            shelter.transform.SetParent(yard.transform, false);
            shelter.transform.localPosition = new Vector3(0f, 0.02f, 2.8f);
            shelter.transform.localRotation = facing;
            mannequins.Add(LiquidMannequinBuilder.CreateClothed(shelter.transform, "Mannequin", mannequins.Count, update,
                true, LiquidMannequinBuilder.Casual));

            Material post = LiquidAssets.CreateOrLoadSurfaceMaterial(
                LiquidDemoGalleries.DeviceMaterialPath, new Color(0.3f, 0.32f, 0.35f), 0.6f);
            for (int corner = 0; corner < 4; corner++)
            {
                float x = corner % 2 == 0 ? -1.2f : 1.2f;
                float z = corner < 2 ? -1.2f : 1.2f;
                Box(shelter.transform, "Post " + (corner + 1), new Vector3(x, 1.25f, z), new Vector3(0.08f, 2.5f, 0.08f), post);
            }

            Box(shelter.transform, "Roof", new Vector3(0f, 2.54f, 0f), new Vector3(2.8f, 0.08f, 2.8f), roof);

            string cycle = snow
                ? $"Snow: {SnowOnSeconds:0} s on, {SnowOffSeconds:0} s off (covers, then melts)"
                : $"Rain: {RainOnSeconds:0} s on, {RainOffSeconds:0} s off";
            LiquidDemoGalleries.Label(yard.transform, cycle, centre + new Vector3(0f, 3.2f, -YardSize.z * 0.5f), facing);
            LiquidDemoGalleries.Label(shelter.transform, "Under a roof", shelter.transform.position + new Vector3(0f, 2.9f, -1.5f),
                facing);
        }

        private static GameObject Box(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }
    }
}
