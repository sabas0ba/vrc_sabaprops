using System.Collections.Generic;
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

        [MenuItem("GameObject/SabaProps/Foliage Field", false, 10)]
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
