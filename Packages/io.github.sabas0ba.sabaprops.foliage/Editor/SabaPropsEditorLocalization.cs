using UnityEditor;

namespace SabaProps.Foliage.Editors
{
    /// <summary>Languages supported by SabaProps scene-authoring windows.</summary>
    public enum SabaPropsEditorLanguage
    {
        Japanese = 0,
        English = 1,
    }

    /// <summary>Shared, persistent localization state for placement workflows.</summary>
    public static class SabaPropsEditorLocalization
    {
        private const string LanguagePreferenceKey = "SabaProps.Editor.Language";

        public static SabaPropsEditorLanguage Language
        {
            get
            {
                int stored = EditorPrefs.GetInt(
                    LanguagePreferenceKey,
                    (int)SabaPropsEditorLanguage.Japanese);
                return stored == (int)SabaPropsEditorLanguage.English
                    ? SabaPropsEditorLanguage.English
                    : SabaPropsEditorLanguage.Japanese;
            }
            set => EditorPrefs.SetInt(LanguagePreferenceKey, (int)value);
        }

        public static bool IsJapanese => Language == SabaPropsEditorLanguage.Japanese;

        public static string Text(string japanese, string english)
        {
            return IsJapanese ? japanese : english;
        }

        public static void DrawLanguageSelector()
        {
            int selected = (int)Language;
            int next = EditorGUILayout.Popup(
                Text("表示言語", "UI Language"),
                selected,
                IsJapanese
                    ? new[] { "日本語", "英語" }
                    : new[] { "Japanese", "English" });
            if (next != selected)
            {
                Language = next == (int)SabaPropsEditorLanguage.English
                    ? SabaPropsEditorLanguage.English
                    : SabaPropsEditorLanguage.Japanese;
            }
        }

        public static int Popup(
            string japaneseLabel,
            string englishLabel,
            int selected,
            string[] japaneseOptions,
            string[] englishOptions)
        {
            return EditorGUILayout.Popup(
                Text(japaneseLabel, englishLabel),
                selected,
                IsJapanese ? japaneseOptions : englishOptions);
        }
    }
}
