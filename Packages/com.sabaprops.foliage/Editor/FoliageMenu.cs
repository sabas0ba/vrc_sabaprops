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
            EnsureDefaults(out Material material, out FoliageSpecies grass, out FoliageSpecies sunflower);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (material != null)
            {
                Selection.objects = new Object[] { grass, sunflower, material };
                EditorGUIUtility.PingObject(material);
            }

            Debug.Log($"[SabaProps Foliage] デフォルトアセットを {FoliageAssetLibrary.RootFolder} に作成しました。");
        }

        [MenuItem("GameObject/SabaProps/Foliage Field", false, 10)]
        public static void CreateFoliageField(MenuCommand command)
        {
            EnsureDefaults(out Material material, out FoliageSpecies grass, out FoliageSpecies sunflower);
            if (material == null)
            {
                return;
            }

            var go = new GameObject("Foliage Field");
            GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);

            var field = go.AddComponent<FoliageField>();
            field.species.Add(grass);
            field.species.Add(sunflower);

            // Drop the field at the centre of the scene view rather than at the
            // world origin, which is almost never where the user is looking.
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null && command.context == null)
            {
                go.transform.position = sceneView.pivot;
            }

            Undo.RegisterCreatedObjectUndo(go, "Create Foliage Field");
            Selection.activeGameObject = go;
        }

        [MenuItem("Tools/SabaProps/Foliage/Documentation", false, 100)]
        public static void OpenDocumentation()
        {
            Application.OpenURL("https://github.com/sabas0ba/vrc_sabaprops/blob/main/Packages/com.sabaprops.foliage/README.md");
        }

        private static void EnsureDefaults(out Material material, out FoliageSpecies grass, out FoliageSpecies sunflower)
        {
            FoliageAssetLibrary.CreateOrLoadDefaults(out material, out grass, out sunflower);
        }
    }
}
