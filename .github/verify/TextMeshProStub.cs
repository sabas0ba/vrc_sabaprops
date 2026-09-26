// Hand-written stand-in for the TextMeshPro assembly (Unity.TextMeshPro).
//
// com.vrchat.worlds depends on com.unity.textmeshpro, which is source inside a
// Unity package and is not published as a reference assembly. This file carries
// only the members io.github.sabas0ba.sabaprops.tablet uses, so a typo in those
// member names fails here. It shares the weakness of UnityUiStub.cs: if the
// stub and the package hold the same wrong belief, this check passes anyway.
// The Unity tests under .github/verify/vrchat compile against the real package.
using UnityEngine;

namespace TMPro
{
    public enum TextAlignmentOptions
    {
        TopLeft = 257,
        Top = 258,
        TopRight = 260,
        Left = 513,
        Center = 514,
        Right = 516,
        BottomLeft = 1025,
        Bottom = 1026,
        BottomRight = 1028,
        Midline = 4610,
    }

    public enum TextOverflowModes
    {
        Overflow = 0,
        Ellipsis = 1,
        Masking = 2,
        Truncate = 3,
    }

    public class TMP_FontAsset : ScriptableObject { }

    public class TMP_Settings : ScriptableObject
    {
        public static TMP_FontAsset defaultFontAsset => null;
    }

    public class TMP_Text : MonoBehaviour
    {
        public string text { get; set; }
        public Color color { get; set; }
        public float fontSize { get; set; }
        public float fontSizeMin { get; set; }
        public float fontSizeMax { get; set; }
        public bool enableAutoSizing { get; set; }
        public bool enableWordWrapping { get; set; }
        public bool richText { get; set; }
        public TMP_FontAsset font { get; set; }
        public TextAlignmentOptions alignment { get; set; }
        public TextOverflowModes overflowMode { get; set; }
        public RectTransform rectTransform => null;
    }

    public class TextMeshPro : TMP_Text { }
}
