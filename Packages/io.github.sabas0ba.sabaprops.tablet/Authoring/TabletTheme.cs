using TMPro;
using UnityEngine;

namespace SabaProps.Tablet.Authoring
{
    /// <summary>
    /// タブレットの外観。Build 時にメッシュとマテリアルへ変換して焼き込みます。
    /// Udon はこのアセットを実行時に参照しません。Theme を差し替えて Build し直すと外観が変わります。
    /// </summary>
    [CreateAssetMenu(menuName = "SabaProps/Tablet Theme", fileName = "TabletTheme")]
    public class TabletTheme : ScriptableObject
    {
        [Header("本体 (m)")]
        public Vector2 bodySize = new Vector2(0.30f, 0.21f);
        public float bodyThickness = 0.012f;
        public float cornerRadius = 0.016f;

        [Tooltip("本体の縁から画面までの幅。")]
        public float bezel = 0.011f;

        [Tooltip("画面上部のタイトルとページ操作の高さ。")]
        public float headerHeight = 0.03f;

        [Header("ボタン")]
        [Min(1)]
        public int columns = 4;

        [Min(1)]
        public int rows = 3;

        public float spacing = 0.006f;

        [Tooltip("キャップの厚み (m)。")]
        public float buttonHeight = 0.005f;

        public float buttonCornerRadius = 0.006f;

        [Tooltip("押下時にキャップが沈む量 (m)。")]
        public float pressTravel = 0.003f;

        [Tooltip("キャップ前方の押下判定領域の奥行き (m)。")]
        public float pokeDepth = 0.025f;

        [Header("取っ手")]
        public bool handle = true;
        public Vector3 handleSize = new Vector3(0.11f, 0.016f, 0.014f);

        [Header("色")]
        public Color bodyColor = new Color(0.16f, 0.17f, 0.19f, 1f);
        public Color screenColor = new Color(0.05f, 0.06f, 0.08f, 1f);
        public Color buttonColor = new Color(0.22f, 0.25f, 0.30f, 1f);
        public Color buttonActiveColor = new Color(0.20f, 0.52f, 0.78f, 1f);
        public Color buttonPressedColor = new Color(0.42f, 0.66f, 0.88f, 1f);
        public Color headerButtonColor = new Color(0.14f, 0.16f, 0.20f, 1f);
        public Color labelColor = new Color(0.92f, 0.95f, 1f, 1f);

        [Range(0f, 1f)]
        public float smoothness = 0.35f;

        [Header("素材の差し替え (任意)")]
        [Tooltip("生成するマテリアルのシェーダー。未設定なら Standard です。色は _Color に設定します。")]
        public Shader shader;

        public Material bodyMaterial;
        public Material screenMaterial;
        public Material buttonMaterial;
        public Material buttonActiveMaterial;
        public Material buttonPressedMaterial;
        public Mesh bodyMesh;
        public Mesh buttonMesh;

        [Header("文字")]
        [Tooltip("未設定なら TextMeshPro の既定フォントです。日本語を表示する場合は日本語を含むフォントアセットを指定してください。")]
        public TMP_FontAsset font;
    }
}
