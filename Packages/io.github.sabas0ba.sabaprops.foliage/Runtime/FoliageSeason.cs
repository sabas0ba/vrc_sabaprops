using System;
using UnityEngine;

namespace SabaProps.Foliage
{
    /// <summary>Which seasonal colouring a species bakes into its mesh.</summary>
    public enum FoliageSeason
    {
        /// <summary>Fresh, yellow-leaning green.</summary>
        Spring = 0,

        /// <summary>The colours as authored on the species. The default.</summary>
        Summer = 1,

        /// <summary>Amber and rust.</summary>
        Autumn = 2,

        /// <summary>Dry straw, desaturated.</summary>
        Winter = 3,
    }

    /// <summary>
    /// How one season shifts a species' vertex colours.
    /// <para>
    /// The shift is applied when the mesh is generated, so it costs nothing at
    /// runtime and is reproducible from the species asset alone. Only RGB is
    /// touched: the alpha channel carries the per-element wind phase.
    /// </para>
    /// </summary>
    [Serializable]
    public class SeasonTint
    {
        [Tooltip("この季節で寄せる色。色相と彩度がこの色へ向かいます。")]
        public Color target = new Color(0.290f, 0.443f, 0.161f, 1f);

        [Tooltip("target へどれだけ寄せるか。0 で Species の色そのままです。")]
        [Range(0f, 1f)] public float blend = 0f;

        [Tooltip("彩度の倍率。1 で変化なし。")]
        [Range(0f, 2f)] public float saturation = 1f;

        [Tooltip("明度の倍率。1 で変化なし。")]
        [Range(0f, 2f)] public float brightness = 1f;

        /// <summary>True when applying this tint would leave every colour unchanged.</summary>
        public bool IsIdentity
        {
            get
            {
                return blend <= 0f
                    && Mathf.Abs(saturation - 1f) < 1e-5f
                    && Mathf.Abs(brightness - 1f) < 1e-5f;
            }
        }
    }

    /// <summary>
    /// A species' four seasonal tints.
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
        public SeasonTint spring = new SeasonTint
        {
            target = new Color(0.478f, 0.678f, 0.243f, 1f),
            blend = 0.35f,
            saturation = 1.1f,
            brightness = 1f,
        };

        [Tooltip("夏。既定では Species の色をそのまま使います。")]
        public SeasonTint summer = new SeasonTint
        {
            target = new Color(0.290f, 0.443f, 0.161f, 1f),
            blend = 0f,
            saturation = 1f,
            brightness = 1f,
        };

        [Tooltip("秋。琥珀色へ寄せます。")]
        public SeasonTint autumn = new SeasonTint
        {
            target = new Color(0.718f, 0.443f, 0.129f, 1f),
            blend = 0.6f,
            saturation = 0.95f,
            brightness = 0.85f,
        };

        [Tooltip("冬。枯草色へ寄せ、彩度を落とします。")]
        public SeasonTint winter = new SeasonTint
        {
            target = new Color(0.573f, 0.510f, 0.376f, 1f),
            blend = 0.8f,
            saturation = 0.5f,
            brightness = 0.8f,
        };

        /// <summary>The tint for one season, or null if that entry is missing.</summary>
        public SeasonTint For(FoliageSeason season)
        {
            switch (season)
            {
                case FoliageSeason.Spring:
                    return spring;

                case FoliageSeason.Autumn:
                    return autumn;

                case FoliageSeason.Winter:
                    return winter;

                case FoliageSeason.Summer:
                default:
                    return summer;
            }
        }
    }
}
