using System;
using UnityEngine;

namespace SabaProps.Foliage
{
    /// <summary>Which procedural mesh generator a species uses.</summary>
    public enum FoliageSpeciesKind
    {
        /// <summary>A clump of curved grass blades ("grass seed").</summary>
        GrassClump = 0,

        /// <summary>Stem, leaves, disc and petals.</summary>
        Sunflower = 1,

        /// <summary>Low ground cover: a short stem carrying heart-shaped leaflets.</summary>
        Clover = 2,

        /// <summary>A tall, near-vertical blade cluster with a seed spike.</summary>
        Reed = 3,
    }

    /// <summary>Parameters for <see cref="FoliageSpeciesKind.GrassClump"/>.</summary>
    [Serializable]
    public class GrassParams
    {
        [Tooltip("生成するブレード（葉）の枚数。1 クランプあたりの三角形数に直結します。")]
        [Range(1, 24)] public int bladeCount = 6;

        [Tooltip("ブレード 1 枚あたりの分割数。曲率の滑らかさとコストのバランス。")]
        [Range(1, 6)] public int segments = 3;

        [Tooltip("クランプの半径 (m)。ブレードの根元がこの円内に散ります。")]
        [Min(0f)] public float clumpRadius = 0.08f;

        [Min(0.01f)] public float height = 0.6f;
        [Range(0f, 0.9f)] public float heightVariance = 0.35f;

        [Min(0.001f)] public float width = 0.022f;
        [Range(0f, 0.9f)] public float widthVariance = 0.25f;

        [Tooltip("先端が根元からどれだけ倒れるか。高さに対する比率。")]
        [Range(0f, 1.5f)] public float bend = 0.45f;

        [Tooltip("先端の細り方。大きいほど鋭く尖ります。")]
        [Range(0.2f, 3f)] public float taper = 0.8f;

        [Tooltip("法線を真上へ寄せる割合。葉らしいフラットなライティングになります。")]
        [Range(0f, 1f)] public float normalUpBlend = 0.7f;

        public Color rootColor = new Color(0.129f, 0.212f, 0.078f, 1f);
        public Color tipColor = new Color(0.408f, 0.573f, 0.180f, 1f);

        [Tooltip("根元を暗くする量。接地感が出ます。")]
        [Range(0f, 1f)] public float rootOcclusion = 0.35f;

        [Tooltip("ブレードごとの色ゆらぎ。シェーダー側の個体差とは別に効きます。")]
        [Range(0f, 0.5f)] public float perBladeTintJitter = 0.08f;

        [Tooltip("風に対する柔らかさ。1 で最大限しなります。")]
        [Range(0f, 1f)] public float stiffness = 1f;
    }

    /// <summary>Parameters for <see cref="FoliageSpeciesKind.Sunflower"/>.</summary>
    [Serializable]
    public class SunflowerParams
    {
        [Header("Stem")]
        [Min(0.05f)] public float height = 1.35f;
        [Range(0f, 0.6f)] public float heightVariance = 0.18f;
        [Min(0.002f)] public float stemWidth = 0.028f;
        [Range(1, 8)] public int stemSegments = 4;

        [Tooltip("茎の傾き (m)。頂点が根元からどれだけ横へずれるか。")]
        [Range(0f, 0.8f)] public float lean = 0.18f;

        public Color stemColor = new Color(0.204f, 0.325f, 0.110f, 1f);

        [Header("Leaves")]
        [Range(0, 6)] public int leafCount = 3;
        [Min(0.01f)] public float leafLength = 0.24f;
        [Min(0.01f)] public float leafWidth = 0.13f;

        [Tooltip("葉の垂れ下がり角度 (度)。")]
        [Range(-60f, 60f)] public float leafDroop = 22f;

        public Color leafColor = new Color(0.243f, 0.404f, 0.129f, 1f);

        [Header("Head")]
        [Min(0.01f)] public float headRadius = 0.085f;
        [Range(5, 16)] public int headSides = 9;
        public Color headColor = new Color(0.243f, 0.145f, 0.063f, 1f);
        public Color headRimColor = new Color(0.361f, 0.227f, 0.086f, 1f);

        [Tooltip("花が正面を向く角度 (度)。0 で真上向き。")]
        [Range(0f, 90f)] public float headTilt = 38f;

        [Header("Petals")]
        [Range(4, 32)] public int petalCount = 15;
        [Min(0.01f)] public float petalLength = 0.11f;
        [Min(0.005f)] public float petalWidth = 0.042f;

        [Tooltip("花弁の反り (m)。前方向へ持ち上がります。")]
        [Range(-0.1f, 0.1f)] public float petalCurl = 0.018f;

        public Color petalBaseColor = new Color(0.902f, 0.596f, 0.086f, 1f);
        public Color petalTipColor = new Color(0.976f, 0.816f, 0.212f, 1f);

        [Header("Wind")]
        [Tooltip("茎の柔らかさ。ひまわりは草より硬いので小さめが自然です。")]
        [Range(0f, 1f)] public float stemStiffness = 0.5f;

        [Tooltip("花弁の柔らかさ。茎より大きくすると先端がそよぎます。")]
        [Range(0f, 1f)] public float petalStiffness = 0.85f;
    }

    /// <summary>Parameters for <see cref="FoliageSpeciesKind.Clover"/>.</summary>
    [Serializable]
    public class CloverParams
    {
        [Tooltip("1 株あたりの小葉の枚数。")]
        [Range(2, 5)] public int leafletCount = 3;

        [Tooltip("茎の高さ (m)。小葉はこの高さに付きます。")]
        [Min(0.01f)] public float height = 0.11f;

        [Range(0f, 0.9f)] public float heightVariance = 0.3f;

        [Min(0.002f)] public float stemWidth = 0.006f;

        [Min(0.005f)] public float leafLength = 0.055f;
        [Min(0.005f)] public float leafWidth = 0.062f;

        [Tooltip("小葉の垂れ下がり角度 (度)。")]
        [Range(-45f, 45f)] public float leafDroop = 16f;

        [Tooltip("先端の切れ込みの深さ。クローバーらしいハート形になります。")]
        [Range(0f, 0.5f)] public float notch = 0.22f;

        public Color leafColor = new Color(0.208f, 0.361f, 0.145f, 1f);
        public Color leafRimColor = new Color(0.325f, 0.498f, 0.204f, 1f);

        [Range(0f, 1f)] public float rootOcclusion = 0.3f;

        [Tooltip("株ごとの色ゆらぎ。")]
        [Range(0f, 0.5f)] public float perPlantTintJitter = 0.07f;

        [Range(0f, 1f)] public float stiffness = 0.75f;
    }

    /// <summary>Parameters for <see cref="FoliageSpeciesKind.Reed"/>.</summary>
    [Serializable]
    public class ReedParams
    {
        [Range(1, 8)] public int bladeCount = 4;
        [Range(2, 6)] public int segments = 3;

        [Min(0.05f)] public float height = 1.05f;
        [Range(0f, 0.9f)] public float heightVariance = 0.28f;

        [Min(0.002f)] public float width = 0.017f;
        [Range(0f, 0.9f)] public float widthVariance = 0.2f;

        [Tooltip("先端の開き (m)。草より小さくすると直立した葦らしくなります。")]
        [Range(0f, 0.8f)] public float spread = 0.16f;

        [Min(0f)] public float clumpRadius = 0.035f;

        [Range(0.2f, 3f)] public float taper = 1.1f;
        [Range(0f, 1f)] public float normalUpBlend = 0.6f;

        public Color rootColor = new Color(0.243f, 0.298f, 0.129f, 1f);
        public Color tipColor = new Color(0.475f, 0.478f, 0.239f, 1f);

        [Range(0f, 1f)] public float rootOcclusion = 0.3f;

        [Header("Spike")]
        [Tooltip("最も高いブレードの先に穂を付けます。")]
        public bool spike = true;

        [Min(0.01f)] public float spikeLength = 0.14f;
        [Min(0.002f)] public float spikeWidth = 0.024f;

        public Color spikeColor = new Color(0.318f, 0.208f, 0.114f, 1f);

        [Range(0f, 1f)] public float stiffness = 0.55f;
    }

    /// <summary>
    /// A reusable foliage preset: how the mesh is generated and how instances of
    /// it are placed. One species maps to exactly one mesh, and therefore to one
    /// GPU instancing batch.
    /// </summary>
    [CreateAssetMenu(menuName = "SabaProps/Foliage/Species", fileName = "FoliageSpecies", order = 0)]
    public class FoliageSpecies : ScriptableObject
    {
        [Header("Identity")]
        public FoliageSpeciesKind kind = FoliageSpeciesKind.GrassClump;

        [Tooltip("GPU インスタンシングを有効にしたマテリアル。未設定だと生成できません。")]
        public Material material;

        [Tooltip("メッシュ生成のシード。変えると形状バリエーションが変わります。")]
        public int meshSeed = 1;

        [Header("Season")]
        [Tooltip("生成時に頂点カラーへ焼き込む季節。Summer は Species の色をそのまま使います。")]
        public FoliageSeason season = FoliageSeason.Summer;

        [Tooltip("季節ごとの色の寄せ方。種ごとに枯れ方を変えられます。")]
        public SeasonPalette seasonPalette = new SeasonPalette();

        [Header("Placement")]
        [Tooltip("複数種を混ぜたときの出現比率。")]
        [Min(0f)] public float placementWeight = 1f;

        [Tooltip("同じ種の個体どうしをこれ以上近づけない距離 (m)。0 で無効。他の種との距離には影響しません。")]
        [Min(0f)] public float minSpacing = 0.05f;

        [Tooltip("個体ごとのスケール範囲（等倍スケールのみ）。")]
        public Vector2 scaleRange = new Vector2(0.85f, 1.2f);

        [Tooltip("鉛直からのランダムな傾き (度)。")]
        [Range(0f, 45f)] public float maxTilt = 7f;

        [Tooltip(
            "個体の向きをランダムにせず、太陽（Directional Light）の方位へ揃えます。"
            + "ひまわりのように向きが揃う植物向けです。")]
        public bool faceSun = false;

        [Tooltip("Face Sun のときの向きのばらつき (度)。0 で完全に揃います。")]
        [Range(0f, 180f)] public float faceSunJitter = 18f;

        [Tooltip("地面の法線にどれだけ倣うか。0 で常に鉛直。")]
        [Range(0f, 1f)] public float alignToGroundNormal = 0.3f;

        [Tooltip("この傾斜 (度) を超える地面には配置しません。")]
        public Vector2 slopeLimits = new Vector2(0f, 40f);

        [Header("Rendering")]
        [Tooltip("草は影を落とさない方が圧倒的に軽くなります。")]
        public bool castShadows = false;

        public bool receiveShadows = true;

        [Tooltip("ライトプローブを使う。ライトマップ非対応の代わりにこちらで陰影を取ります。")]
        public bool useLightProbes = true;

        [Header("Mesh Parameters")]
        public GrassParams grass = new GrassParams();
        public SunflowerParams sunflower = new SunflowerParams();
        public CloverParams clover = new CloverParams();
        public ReedParams reed = new ReedParams();

        [Header("Generated (read only)")]
        [Tooltip("ビルド時に自動生成・上書きされるメッシュアセット。")]
        public Mesh generatedMesh;

        /// <summary>
        /// The tint for the selected season, or null when the asset predates the
        /// palette. A null tint means "leave the colours alone", which is what an
        /// asset authored before seasons existed expects.
        /// </summary>
        public SeasonTint ActiveSeasonTint
        {
            get { return seasonPalette != null ? seasonPalette.For(season) : null; }
        }

        /// <summary>
        /// Clamped, always-valid scale range. Guards against a user inverting the
        /// min/max fields in the inspector.
        /// </summary>
        public Vector2 SafeScaleRange
        {
            get
            {
                float min = Mathf.Max(0.001f, Mathf.Min(scaleRange.x, scaleRange.y));
                float max = Mathf.Max(min, Mathf.Max(scaleRange.x, scaleRange.y));
                return new Vector2(min, max);
            }
        }

        /// <summary>Slope filter in degrees, ordered and clamped to [0, 90].</summary>
        public Vector2 SafeSlopeLimits
        {
            get
            {
                float min = Mathf.Clamp(Mathf.Min(slopeLimits.x, slopeLimits.y), 0f, 90f);
                float max = Mathf.Clamp(Mathf.Max(slopeLimits.x, slopeLimits.y), 0f, 90f);
                return new Vector2(min, max);
            }
        }

        private void OnValidate()
        {
            placementWeight = Mathf.Max(0f, placementWeight);
            minSpacing = Mathf.Max(0f, minSpacing);
        }
    }
}
