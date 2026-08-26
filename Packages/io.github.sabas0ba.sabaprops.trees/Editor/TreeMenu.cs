using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Trees.Editors
{
    public static class TreeMenu
    {
        [MenuItem("Tools/SabaProps/Trees/Create Default Assets", false, 0)]
        public static void CreateDefaultAssets()
        {
            List<TreeSpecies> species = TreeAssetLibrary.CreateOrLoadDefaults(out Material material);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var selection = new List<UnityEngine.Object>(species.Count + 1);
            selection.AddRange(species);
            if (material != null)
            {
                selection.Add(material);
            }
            Selection.objects = selection.ToArray();

            Debug.Log(
                $"[SabaProps Trees] {species.Count} 種の Tree Species と LOD Mesh を " +
                $"{TreeAssetLibrary.RootFolder} に作成しました。");
        }

        [MenuItem("GameObject/SabaProps/Tree LOD Group", false, 11)]
        public static void CreateTree(MenuCommand command)
        {
            TreeSpecies species = Selection.activeObject as TreeSpecies;
            if (species == null)
            {
                species = TreeAssetLibrary.CreateOrLoadSpecies(TreeArchetype.Broadleaf);
            }

            GameObject parentObject = command.context as GameObject;
            TreeAssetLibrary.CreateLodGroup(species, parentObject != null ? parentObject.transform : null);
        }

        [MenuItem("Tools/SabaProps/Trees/Documentation", false, 100)]
        public static void OpenDocumentation()
        {
            Application.OpenURL(
                "https://github.com/sabas0ba/vrc_sabaprops/blob/main/" +
                "Packages/io.github.sabas0ba.sabaprops.trees/README.md");
        }
    }
}
