using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Water.Editors
{
    public static class WaterMenu
    {
        [MenuItem("Tools/SabaProps/Water/Create Default Assets", false, 0)]
        public static void CreateDefaultAssets()
        {
            List<UnityEngine.Object> assets = WaterAssetLibrary.CreateOrLoadDefaults();
            Selection.objects = assets.ToArray();
            if (assets.Count > 0)
            {
                EditorGUIUtility.PingObject(assets[0]);
            }

            Debug.Log(
                $"[SabaProps Water] {assets.Count}個のdefault assetを{WaterAssetLibrary.RootFolder}へ作成または確認しました。");
        }

        [MenuItem("GameObject/SabaProps/Water/Puddle Lite", false, 10)]
        public static void CreatePuddleLite(MenuCommand command) =>
            WaterRigFactory.CreateSurface(WaterBodyKind.Puddle, WaterQuality.Lite, command.context as GameObject);

        [MenuItem("GameObject/SabaProps/Water/Puddle Standard", false, 11)]
        public static void CreatePuddleStandard(MenuCommand command) =>
            WaterRigFactory.CreateSurface(WaterBodyKind.Puddle, WaterQuality.Standard, command.context as GameObject);

        [MenuItem("GameObject/SabaProps/Water/River Lite", false, 20)]
        public static void CreateRiverLite(MenuCommand command) =>
            WaterRigFactory.CreateSurface(WaterBodyKind.River, WaterQuality.Lite, command.context as GameObject);

        [MenuItem("GameObject/SabaProps/Water/River Standard", false, 21)]
        public static void CreateRiverStandard(MenuCommand command) =>
            WaterRigFactory.CreateSurface(WaterBodyKind.River, WaterQuality.Standard, command.context as GameObject);

        [MenuItem("GameObject/SabaProps/Water/Lake Lite", false, 30)]
        public static void CreateLakeLite(MenuCommand command) =>
            WaterRigFactory.CreateSurface(WaterBodyKind.Lake, WaterQuality.Lite, command.context as GameObject);

        [MenuItem("GameObject/SabaProps/Water/Lake Standard", false, 31)]
        public static void CreateLakeStandard(MenuCommand command) =>
            WaterRigFactory.CreateSurface(WaterBodyKind.Lake, WaterQuality.Standard, command.context as GameObject);

        [MenuItem("GameObject/SabaProps/Water/Ocean Lite", false, 40)]
        public static void CreateOceanLite(MenuCommand command) =>
            WaterRigFactory.CreateSurface(WaterBodyKind.Ocean, WaterQuality.Lite, command.context as GameObject);

        [MenuItem("GameObject/SabaProps/Water/Ocean Standard", false, 41)]
        public static void CreateOceanStandard(MenuCommand command) =>
            WaterRigFactory.CreateSurface(WaterBodyKind.Ocean, WaterQuality.Standard, command.context as GameObject);

        [MenuItem("GameObject/SabaProps/Weather/Rain Rig", false, 10)]
        public static void CreateRain(MenuCommand command) =>
            WaterRigFactory.CreateRainRig(command.context as GameObject);

        [MenuItem("GameObject/SabaProps/Weather/Ground Fog Particles", false, 20)]
        public static void CreateGroundFog(MenuCommand command) =>
            WaterRigFactory.CreateFogParticles(false, command.context as GameObject);

        [MenuItem("GameObject/SabaProps/Weather/Cloud Layer", false, 21)]
        public static void CreateClouds(MenuCommand command) =>
            WaterRigFactory.CreateFogParticles(true, command.context as GameObject);

        [MenuItem("GameObject/SabaProps/Weather/Fog Volume Lite", false, 30)]
        public static void CreateFogVolumeLite(MenuCommand command) =>
            WaterRigFactory.CreateFogVolume(false, command.context as GameObject);

        [MenuItem("GameObject/SabaProps/Weather/Fog Volume High", false, 31)]
        public static void CreateFogVolumeHigh(MenuCommand command) =>
            WaterRigFactory.CreateFogVolume(true, command.context as GameObject);

        [MenuItem("GameObject/SabaProps/Water/Underwater Lake Lite", false, 50)]
        public static void CreateUnderwaterLite(MenuCommand command) =>
            WaterRigFactory.CreateUnderwaterRig(false, command.context as GameObject);

        [MenuItem("GameObject/SabaProps/Water/Underwater Lake Standard", false, 51)]
        public static void CreateUnderwaterStandard(MenuCommand command) =>
            WaterRigFactory.CreateUnderwaterRig(true, command.context as GameObject);

        [MenuItem("GameObject/SabaProps/Water/Wet Surface Preview", false, 60)]
        public static void CreateWetSurfacePreview(MenuCommand command) =>
            WaterRigFactory.CreateWetSurfacePreview(command.context as GameObject);

        [MenuItem("Tools/SabaProps/Water/Configure VRChat World Descriptor", false, 3)]
        public static void ConfigureVrcWorldDescriptor()
        {
            GameObject world = WaterVrcWorld.CreateWorld(
                new Vector3(0f, 0.05f, -13f), Quaternion.identity, Camera.main);
            Selection.activeGameObject = world;
            EditorUtility.SetDirty(world);

            if (WaterVrcWorld.IsSdkPresent)
            {
                Debug.Log("[SabaProps Water] VRCSceneDescriptor と Spawn を設定しました。");
            }
            else
            {
                Debug.LogWarning(
                    "[SabaProps Water] VRChat Worlds SDK が見つかりません。" +
                    "VRCWorld と Spawn のみ作成しました。");
            }
        }

        [MenuItem("Tools/SabaProps/Water/Documentation", false, 100)]
        public static void OpenDocumentation()
        {
            Application.OpenURL(
                "https://github.com/sabas0ba/vrc_sabaprops/blob/main/Packages/io.github.sabas0ba.sabaprops.water/README.md");
        }
    }
}
