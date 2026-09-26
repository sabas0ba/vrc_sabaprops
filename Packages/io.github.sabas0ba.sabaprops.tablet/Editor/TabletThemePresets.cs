using SabaProps.Tablet.Authoring;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Tablet.Editors
{
    public static class TabletThemePresets
    {
        public const string Folder = "Packages/io.github.sabas0ba.sabaprops.tablet/Themes";
        public static readonly string[] Files = { "SakuraRibbon", "MintCat", "HoneyBear", "LilacStars", "PeachBlossom", "SkyCloud" };
        public static readonly string[] Descriptions =
        {
            "桜色・リボン・丸みのあるボタン", "ミント・猫耳・小さな角丸", "はちみつ色・くま耳・丸いボタン",
            "ラベンダー・星・控えめな角丸", "桃色・花・柔らかい角丸", "水色・雲・丸いボタン",
        };

        public static TabletTheme Load(int index)
        {
            return AssetDatabase.LoadAssetAtPath<TabletTheme>(Folder + "/" + Files[index] + ".asset");
        }

        public static void Apply(TabletDefinition definition, TabletTheme theme)
        {
            if (definition == null || theme == null) return;
            Undo.RecordObject(definition, "Apply Tablet Theme");
            definition.theme = theme;
            TabletBuilder.Build(definition);
        }

        public static TabletTheme EditableCopy(TabletTheme source)
        {
            TabletAssets.EnsureFolder(TabletMenu.ThemeFolder);
            TabletTheme copy = Object.Instantiate(source);
            copy.name = source.name + " Custom";
            AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath(TabletMenu.ThemeFolder + "/" + copy.name + ".asset"));
            AssetDatabase.SaveAssets();
            Selection.activeObject = copy;
            EditorGUIUtility.PingObject(copy);
            return copy;
        }
    }

    public class TabletThemeWindow : EditorWindow
    {
        private TabletDefinition definition;
        private Vector2 scroll;

        [MenuItem("Tools/SabaProps/Tablet/Theme Presets", false, 2)]
        public static void Open()
        {
            TabletThemeWindow window = GetWindow<TabletThemeWindow>(false, "Tablet Themes", true);
            window.minSize = new Vector2(440f, 350f);
            if (Selection.activeGameObject != null)
                window.definition = Selection.activeGameObject.GetComponentInParent<TabletDefinition>();
            window.Show();
        }

        private void OnGUI()
        {
            definition = (TabletDefinition)EditorGUILayout.ObjectField("Tablet", definition, typeof(TabletDefinition), true);
            EditorGUILayout.HelpBox("Theme を適用すると Body・Modules・Triggers を再生成します。プリセットを変更するときは編集用コピーを作成してください。", MessageType.Info);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < TabletThemePresets.Files.Length; i++)
            {
                TabletTheme theme = TabletThemePresets.Load(i);
                if (theme == null) continue;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(theme.name, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(TabletThemePresets.Descriptions[i]);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        Color original = GUI.color;
                        foreach (Color color in new[] { theme.bodyColor, theme.screenColor, theme.buttonColor, theme.buttonActiveColor })
                        {
                            GUI.color = color;
                            GUILayout.Box("", GUILayout.Width(36), GUILayout.Height(18));
                        }
                        GUI.color = original;
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(definition == null || Application.isPlaying))
                            if (GUILayout.Button("適用して Build")) TabletThemePresets.Apply(definition, theme);
                        if (GUILayout.Button("編集用コピー")) TabletThemePresets.EditableCopy(theme);
                        if (GUILayout.Button("アセットを表示")) EditorGUIUtility.PingObject(theme);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("6 種の比較シーンを生成")) TabletThemeGallery.CreateAndOpen();
        }
    }
}
