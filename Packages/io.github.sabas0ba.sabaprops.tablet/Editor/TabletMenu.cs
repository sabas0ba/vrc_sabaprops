using SabaProps.Tablet.Authoring;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Tablet.Editors
{
    /// <summary>タブレットの配置、Theme の作成、ドキュメントへの入口。</summary>
    public static class TabletMenu
    {
        public const string ThemeFolder = TabletAssets.RootFolder + "/Themes";

        [MenuItem("GameObject/SabaProps/Tablet", false, 20)]
        public static void CreateFromMenu(MenuCommand command)
        {
            TabletDefinition definition = CreateDefinition(command.context as GameObject);
            TabletBuilder.Build(definition);
            Selection.activeGameObject = definition.gameObject;
        }

        /// <summary>空のページを持つ定義を配置します。Build は行いません。</summary>
        public static TabletDefinition CreateDefinition(GameObject parent)
        {
            var go = new GameObject("Tablet");
            if (parent != null)
            {
                GameObjectUtility.SetParentAndAlign(go, parent);
            }

            var definition = go.AddComponent<TabletDefinition>();
            definition.FindOrAddPage(TabletSetupWindow.MirrorPage);
            definition.FindOrAddPage(TabletSetupWindow.ColliderPage);
            definition.FindOrAddPage(TabletSetupWindow.TeleportPage);
            Undo.RegisterCreatedObjectUndo(go, "Create Tablet");
            return definition;
        }

        /// <summary>既定値の Theme を Assets/SabaProps/Tablet/Themes に作成します。</summary>
        public static TabletTheme CreateThemeAsset()
        {
            TabletAssets.EnsureFolder(ThemeFolder);
            var theme = ScriptableObject.CreateInstance<TabletTheme>();
            AssetDatabase.CreateAsset(theme, AssetDatabase.GenerateUniqueAssetPath(ThemeFolder + "/TabletTheme.asset"));
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(theme);
            return theme;
        }

        [MenuItem("Tools/SabaProps/Tablet/Setup Window", false, 0)]
        public static void OpenSetup()
        {
            TabletSetupWindow.Open();
        }

        [MenuItem("Tools/SabaProps/Tablet/Documentation", false, 100)]
        public static void OpenDocumentation()
        {
            Application.OpenURL(
                "https://github.com/sabas0ba/vrc_sabaprops/blob/main/Packages/io.github.sabas0ba.sabaprops.tablet/README.md");
        }
    }
}
