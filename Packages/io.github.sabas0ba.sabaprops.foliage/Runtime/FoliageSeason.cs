using System;
using UnityEngine;

namespace SabaProps.Foliage
{
    /// <summary>
    /// Which seasonal state a species bakes into its mesh. Five, not four:
    /// winter looks nothing like itself between a field under snow and the same
    /// field on a bare cold day.
    /// </summary>
    public enum FoliageSeason
    {
        /// <summary>Fresh, yellow-leaning green.</summary>
        Spring = 0,

        /// <summary>The colours as authored on the species. The default.</summary>
        Summer = 1,

        /// <summary>Brown and dry. What is left stands stiffly and hangs over.</summary>
        Autumn = 2,

        /// <summary>Pale straw under snow light: washed out rather than dark.</summary>
        WinterSnow = 3,

        /// <summary>
        /// A cold day with no snow to lift it: dark browns and near-blacks, the
        /// colour of wet dead wood.
        /// </summary>
        WinterBare = 4,
    }

    /// <summary>What is left of a plant in a given season.</summary>
    public enum SeasonAppearance
    {
        /// <summary>The whole plant. Only its colours change.</summary>
        Full = 0,

        /// <summary>
        /// The parts that do not last a year are not generated: petals go, the
        /// stem, leaves and seed head stay. A flower recoloured to straw but
        /// still in bloom is a thing that does not exist.
        /// </summary>
        Dormant = 1,

        /// <summary>
        /// Not placed at all. An annual is simply gone for part of the year, and
        /// a dead stalk would be a worse answer than nothing.
        /// </summary>
        Absent = 2,
    }

    /// <summary>
    /// How one season changes a species: what of the plant is there, and what
    /// colour it is.
    /// <para>
    /// Both are resolved when the mesh is generated, so they cost nothing at
    /// runtime and are reproducible from the species asset alone. Of the
    /// colours, only RGB is touched: the alpha channel carries the per-element
    /// wind phase.
    /// </para>
    /// </summary>
    [Serializable]
    public class SeasonStyle
    {
        [Tooltip(
            "この季節の姿。Dormant は花弁など一年で落ちる部位を生成しません。"
            + "Absent はその季節に配置しません。")]
        public SeasonAppearance appearance = SeasonAppearance.Full;

        [Tooltip("この季節で寄せる色。色相と彩度がこの色へ向かいます。")]
        public Color target = new Color(0.290f, 0.443f, 0.161f, 1f);

        [Tooltip("target へどれだけ寄せるか。0 で Species の色そのままです。")]
        [Range(0f, 1f)] public float blend = 0f;

        [Tooltip("彩度の倍率。1 で変化なし。")]
        [Range(0f, 2f)] public float saturation = 1f;

        [Tooltip("明度の倍率。1 で変化なし。")]
        [Range(0f, 2f)] public float brightness = 1f;

        [Tooltip(
            "風の効き方の倍率。1 で変化なし。"
            + "水分の抜けた植物は硬くなり、青いときほどしなりません。")]
        [Range(0f, 1f)] public float windScale = 1f;

        [Tooltip(
            "枯れて頭を垂れる量。株の根元を軸に、先端ほど大きく倒れます。"
            + "角度で効くので、背の高い植物ほど見た目の変化が大きくなります。")]
        [Range(0f, 1f)] public float droop = 0f;

        /// <summary>
        /// True when applying this style would leave the mesh exactly as the
        /// generator produced it.
        /// </summary>
        public bool IsIdentity
        {
            get
            {
                return blend <= 0f
                    && droop <= 0f
                    && Mathf.Abs(saturation - 1f) < 1e-5f
                    && Mathf.Abs(brightness - 1f) < 1e-5f
                    && Mathf.Abs(windScale - 1f) < 1e-5f;
            }
        }
    }

    /// <summary>
    /// A species' seasonal styles.
    /// <para>
    /// Held per species rather than globally so that plants can differ in how
    /// they turn: a maple going red and a birch going yellow is the whole point
    /// of autumn, and a single global hue rotation cannot express it.
    /// </para>
    /// </summary>
    [Serializable]
    public class SeasonPalette
    {
        [Tooltip("春。若葉寄りの明るい緑。")]
        public SeasonStyle spring = new SeasonStyle
        {
            target = new Color(0.478f, 0.678f, 0.243f, 1f),
            blend = 0.35f,
            saturation = 1.1f,
            brightness = 1f,
        };

        [Tooltip("夏。既定では Species の色をそのまま使います。")]
        public SeasonStyle summer = new SeasonStyle
        {
            target = new Color(0.290f, 0.443f, 0.161f, 1f),
            blend = 0f,
            saturation = 1f,
            brightness = 1f,
        };

        [Tooltip("秋。枯れ茶へ寄せ、水分の抜けた硬い株にします。")]
        public SeasonStyle autumn = new SeasonStyle
        {
            target = new Color(0.478f, 0.290f, 0.106f, 1f),
            blend = 0.78f,
            saturation = 0.8f,
            brightness = 0.78f,
            windScale = 0.45f,
            droop = 0.35f,
        };

        [Tooltip("冬（雪）。雪明かりに晒された色。彩度を大きく落とします。")]
        public SeasonStyle winterSnow = new SeasonStyle
        {
            target = new Color(0.573f, 0.510f, 0.376f, 1f),
            blend = 0.8f,
            saturation = 0.45f,
            brightness = 0.8f,
            windScale = 0.3f,
            droop = 0.5f,
        };

        [Tooltip("冬（晴れ間なし）。濡れた枯れ木の色。暗い茶から黒へ寄せます。")]
        public SeasonStyle winterBare = new SeasonStyle
        {
            target = new Color(0.235f, 0.180f, 0.129f, 1f),
            blend = 0.85f,
            saturation = 0.5f,
            brightness = 0.62f,
            windScale = 0.3f,
            droop = 0.55f,
        };

        /// <summary>The tint for one season, or null if that entry is missing.</summary>
        public SeasonStyle For(FoliageSeason season)
        {
            switch (season)
            {
                case FoliageSeason.Spring:
                    return spring;

                case FoliageSeason.Autumn:
                    return autumn;

                case FoliageSeason.WinterSnow:
                    return winterSnow;

                case FoliageSeason.WinterBare:
                    return winterBare;

                case FoliageSeason.Summer:
                default:
                    return summer;
            }
        }
    }
}
