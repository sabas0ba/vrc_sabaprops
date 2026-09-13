using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Foliage.Editors
{
    /// <summary>Entry points that get a user from a fresh project to grass.</summary>
    public static class FoliageMenu
    {
        [MenuItem("Tools/SabaProps/Foliage/Create Default Assets", false, 0)]
        public static void CreateDefaultAssets()
        {
            List<FoliageSpecies> species = FoliageAssetLibrary.CreateOrLoadDefaults(out Material material);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (material == null || species == null)
            {
                return;
            }

            var selection = new List<Object>(species.Count + 1);
            foreach (FoliageSpecies entry in species)
            {
                selection.Add(entry);
            }

            selection.Add(material);

            Selection.objects = selection.ToArray();
            EditorGUIUtility.PingObject(material);

            Debug.Log(
                $"[SabaProps Foliage] {species.Count} 種のデフォルトアセットを {FoliageAssetLibrary.RootFolder} に作成しました。");
        }

        /// <summary>
        /// Copies the demo movement behaviour into the project.
        /// <para>
        /// An opt-in step rather than part of the sample scene, because the file
        /// is an UdonSharp behaviour: importing it is what makes the project's
        /// compilation depend on UdonSharp, and a foliage package has no
        /// business doing that to a project that never asked. It is also why the
        /// source sits under <c>Samples~</c>, which Unity does not import.
        /// </para>
        /// <para>
        /// Unity compiles the file after this returns, so the behaviour can only
        /// be attached to a scene generated after that — run Create Sample Scene
        /// again once the editor has finished compiling.
        /// </para>
        /// </summary>
        [MenuItem("Tools/SabaProps/Debug/Foliage/Import VRChat Demo Movement", false, 30)]
        public static void ImportDemoMovement()
        {
            if (!FoliageVrcWorld.IsSdkPresent)
            {
                EditorUtility.DisplayDialog(
                    "SabaProps Foliage",
                    "VRChat Worlds SDK が見つかりません。移動設定は Udon で動くため、SDK のあるプロジェクトでのみ使えます。",
                    "OK");
                return;
            }

            string source = FoliageAssetLibrary.PackagePath(
                "Samples~/VRChatDemoMovement/FoliageDemoMovement.cs");

            if (source == null || !File.Exists(source))
            {
                Debug.LogError(
                    "[SabaProps Foliage] サンプルのスクリプトが見つかりません: " + (source ?? "(パス不明)"));
                return;
            }

            FoliageAssetLibrary.EnsureFolder(FoliageSampleScene.SampleFolder);
            string destination = FoliageSampleScene.SampleFolder + "/FoliageDemoMovement.cs";

            if (!File.Exists(destination))
            {
                File.Copy(source, destination);
            }

            // Synchronously, because the program asset created below has to
            // reference the MonoScript this import produces.
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);

            if (!FoliageVrcWorld.TryCreateUdonProgramAsset(destination))
            {
                return;
            }

            Debug.Log(
                $"[SabaProps Foliage] {destination} を作成しました。"
                + "コンパイルが終わったら Create Sample Scene を実行し直すと、デモに移動設定が付きます。");
        }

        [MenuItem("GameObject/SabaProps/Placement/Foliage Field...", false, 10)]
        public static void CreateFoliageField(MenuCommand command)
        {
            FoliageFieldWizard.Open(command.context as GameObject);
        }

        [MenuItem("Tools/SabaProps/Foliage/Documentation", false, 100)]
        public static void OpenDocumentation()
        {
            Application.OpenURL("https://github.com/sabas0ba/vrc_sabaprops/blob/main/Packages/io.github.sabas0ba.sabaprops.foliage/README.md");
        }
    }
}
