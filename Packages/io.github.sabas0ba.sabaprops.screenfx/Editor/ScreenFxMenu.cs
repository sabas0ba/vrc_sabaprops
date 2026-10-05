using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SabaProps.ScreenFx.Editors
{
    public static class ScreenFxMenu
    {
        private const string Standard = "GameObject/SabaProps/Screen FX/";
        private const string Lite = "GameObject/SabaProps/Screen FX Lite/";

        [MenuItem("Tools/SabaProps/Screen FX/Create Default Assets", false, 0)]
        public static void CreateDefaultAssets()
        {
            List<Object> assets = ScreenFxAssetLibrary.CreateOrLoadDefaults();
            Selection.objects = assets.ToArray();
            if (assets.Count > 0)
            {
                EditorGUIUtility.PingObject(assets[0]);
            }

            Debug.Log(
                $"[SabaProps Screen FX] {assets.Count}個のMaterialを{ScreenFxAssetLibrary.MaterialsFolder}へ作成または確認しました。");
        }

        [MenuItem("Tools/SabaProps/Screen FX/Create Gallery Scene", false, 1)]
        public static void CreateGallery() => ScreenFxGallery.Create(false);

        [MenuItem("Tools/SabaProps/Screen FX/Create Gallery Scene (Lite)", false, 2)]
        public static void CreateGalleryLite() => ScreenFxGallery.Create(true);

        [MenuItem("Tools/SabaProps/Screen FX/Documentation", false, 100)]
        public static void OpenDocumentation()
        {
            Application.OpenURL(
                "https://github.com/sabas0ba/vrc_sabaprops/blob/main/Packages/io.github.sabas0ba.sabaprops.screenfx/README.md");
        }

        private static void Create(string id, bool lite, MenuCommand command) =>
            ScreenFxRigFactory.CreateVolume(ScreenFxPresets.Find(id), lite, command.context as GameObject);

        [MenuItem(Standard + "Rain", false, 10)]
        public static void CreateRain(MenuCommand command) => Create("Rain", false, command);

        [MenuItem(Lite + "Rain", false, 10)]
        public static void CreateRainLite(MenuCommand command) => Create("Rain", true, command);

        [MenuItem(Standard + "Storm", false, 11)]
        public static void CreateStorm(MenuCommand command) => Create("Storm", false, command);

        [MenuItem(Lite + "Storm", false, 11)]
        public static void CreateStormLite(MenuCommand command) => Create("Storm", true, command);

        [MenuItem(Standard + "Snow", false, 12)]
        public static void CreateSnow(MenuCommand command) => Create("Snow", false, command);

        [MenuItem(Lite + "Snow", false, 12)]
        public static void CreateSnowLite(MenuCommand command) => Create("Snow", true, command);

        [MenuItem(Standard + "Blizzard", false, 13)]
        public static void CreateBlizzard(MenuCommand command) => Create("Blizzard", false, command);

        [MenuItem(Lite + "Blizzard", false, 13)]
        public static void CreateBlizzardLite(MenuCommand command) => Create("Blizzard", true, command);

        [MenuItem(Standard + "Speed", false, 14)]
        public static void CreateSpeed(MenuCommand command) => Create("Speed", false, command);

        [MenuItem(Lite + "Speed", false, 14)]
        public static void CreateSpeedLite(MenuCommand command) => Create("Speed", true, command);

        [MenuItem(Standard + "Drunk", false, 15)]
        public static void CreateDrunk(MenuCommand command) => Create("Drunk", false, command);

        [MenuItem(Lite + "Drunk", false, 15)]
        public static void CreateDrunkLite(MenuCommand command) => Create("Drunk", true, command);

        [MenuItem(Standard + "Underwater", false, 16)]
        public static void CreateUnderwater(MenuCommand command) => Create("Underwater", false, command);

        [MenuItem(Lite + "Underwater", false, 16)]
        public static void CreateUnderwaterLite(MenuCommand command) => Create("Underwater", true, command);

        [MenuItem(Standard + "Mud", false, 17)]
        public static void CreateMud(MenuCommand command) => Create("Mud", false, command);

        [MenuItem(Lite + "Mud", false, 17)]
        public static void CreateMudLite(MenuCommand command) => Create("Mud", true, command);

        [MenuItem(Standard + "Dark", false, 18)]
        public static void CreateDark(MenuCommand command) => Create("Dark", false, command);

        [MenuItem(Lite + "Dark", false, 18)]
        public static void CreateDarkLite(MenuCommand command) => Create("Dark", true, command);

        [MenuItem(Standard + "Tension", false, 19)]
        public static void CreateTension(MenuCommand command) => Create("Tension", false, command);

        [MenuItem(Lite + "Tension", false, 19)]
        public static void CreateTensionLite(MenuCommand command) => Create("Tension", true, command);

        [MenuItem(Standard + "Heat", false, 20)]
        public static void CreateHeat(MenuCommand command) => Create("Heat", false, command);

        [MenuItem(Lite + "Heat", false, 20)]
        public static void CreateHeatLite(MenuCommand command) => Create("Heat", true, command);

        [MenuItem(Standard + "Cold", false, 21)]
        public static void CreateCold(MenuCommand command) => Create("Cold", false, command);

        [MenuItem(Lite + "Cold", false, 21)]
        public static void CreateColdLite(MenuCommand command) => Create("Cold", true, command);

        [MenuItem(Standard + "Humid", false, 22)]
        public static void CreateHumid(MenuCommand command) => Create("Humid", false, command);

        [MenuItem(Lite + "Humid", false, 22)]
        public static void CreateHumidLite(MenuCommand command) => Create("Humid", true, command);

        [MenuItem(Standard + "Fog", false, 23)]
        public static void CreateFog(MenuCommand command) => Create("Fog", false, command);

        [MenuItem(Lite + "Fog", false, 23)]
        public static void CreateFogLite(MenuCommand command) => Create("Fog", true, command);

        [MenuItem(Standard + "Glare", false, 24)]
        public static void CreateGlare(MenuCommand command) => Create("Glare", false, command);

        [MenuItem(Lite + "Glare", false, 24)]
        public static void CreateGlareLite(MenuCommand command) => Create("Glare", true, command);

        [MenuItem(Standard + "Sandstorm", false, 25)]
        public static void CreateSandstorm(MenuCommand command) => Create("Sandstorm", false, command);

        [MenuItem(Lite + "Sandstorm", false, 25)]
        public static void CreateSandstormLite(MenuCommand command) => Create("Sandstorm", true, command);

        [MenuItem(Standard + "Smoke", false, 26)]
        public static void CreateSmoke(MenuCommand command) => Create("Smoke", false, command);

        [MenuItem(Lite + "Smoke", false, 26)]
        public static void CreateSmokeLite(MenuCommand command) => Create("Smoke", true, command);

        [MenuItem(Standard + "Fire", false, 27)]
        public static void CreateFire(MenuCommand command) => Create("Fire", false, command);

        [MenuItem(Lite + "Fire", false, 27)]
        public static void CreateFireLite(MenuCommand command) => Create("Fire", true, command);

        [MenuItem(Standard + "Drowsy", false, 28)]
        public static void CreateDrowsy(MenuCommand command) => Create("Drowsy", false, command);

        [MenuItem(Lite + "Drowsy", false, 28)]
        public static void CreateDrowsyLite(MenuCommand command) => Create("Drowsy", true, command);

        [MenuItem(Standard + "Dizzy", false, 29)]
        public static void CreateDizzy(MenuCommand command) => Create("Dizzy", false, command);

        [MenuItem(Lite + "Dizzy", false, 29)]
        public static void CreateDizzyLite(MenuCommand command) => Create("Dizzy", true, command);

        [MenuItem(Standard + "Damage", false, 30)]
        public static void CreateDamage(MenuCommand command) => Create("Damage", false, command);

        [MenuItem(Lite + "Damage", false, 30)]
        public static void CreateDamageLite(MenuCommand command) => Create("Damage", true, command);

        [MenuItem(Standard + "Poison", false, 31)]
        public static void CreatePoison(MenuCommand command) => Create("Poison", false, command);

        [MenuItem(Lite + "Poison", false, 31)]
        public static void CreatePoisonLite(MenuCommand command) => Create("Poison", true, command);

        [MenuItem(Standard + "Dream", false, 32)]
        public static void CreateDream(MenuCommand command) => Create("Dream", false, command);

        [MenuItem(Lite + "Dream", false, 32)]
        public static void CreateDreamLite(MenuCommand command) => Create("Dream", true, command);

        [MenuItem(Standard + "Faint", false, 33)]
        public static void CreateFaint(MenuCommand command) => Create("Faint", false, command);

        [MenuItem(Lite + "Faint", false, 33)]
        public static void CreateFaintLite(MenuCommand command) => Create("Faint", true, command);
    }
}
