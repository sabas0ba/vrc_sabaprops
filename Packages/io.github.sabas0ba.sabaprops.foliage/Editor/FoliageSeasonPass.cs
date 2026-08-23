using System.Collections.Generic;
using UnityEngine;

namespace SabaProps.Foliage.Editors
{
    /// <summary>
    /// Applies a <see cref="SeasonTint"/> to the vertex colours of a mesh under
    /// construction.
    /// <para>
    /// Runs once, at generation time, at the single point every species passes
    /// through. A species therefore gets its seasons for free: nothing in a
    /// mesh generator needs to know that seasons exist, beyond marking the
    /// vertices that should resist the shift.
    /// </para>
    /// <para>
    /// The conversion is written out here rather than calling
    /// <c>Color.RGBToHSV</c> so that the offline verification tier, which
    /// compiles these sources against a stand-in for UnityEngine, exercises the
    /// same arithmetic the editor does. A stand-in that disagrees with Unity is
    /// worse than no test.
    /// </para>
    /// </summary>
    public static class FoliageSeasonPass
    {
        /// <summary>
        /// Recolours a buffer in place. Vertices keep their alpha, which carries
        /// the per-element wind phase rather than opacity.
        /// </summary>
        internal static void Apply(FoliageMeshBuffer buffer, SeasonTint tint)
        {
            if (buffer == null || tint == null || tint.IsIdentity)
            {
                return;
            }

            List<Color> colors = buffer.Colors;
            List<float> weights = buffer.SeasonWeights;

            float scale = ValueScale(colors, weights, tint);

            for (int i = 0; i < colors.Count; i++)
            {
                float weight = i < weights.Count ? weights[i] : 1f;
                colors[i] = Apply(colors[i], tint, scale, weight);
            }
        }

        /// <summary>
        /// The multiplier that moves the mesh's overall brightness onto the
        /// target's.
        /// <para>
        /// Brightness is scaled rather than interpolated because the root-to-tip
        /// gradient a generator bakes into its colours is what reads as shape.
        /// Lerping every vertex towards one target value flattens that gradient
        /// and the plant turns into a silhouette; scaling by a single factor
        /// moves the average while leaving the relative differences intact.
        /// </para>
        /// </summary>
        private static float ValueScale(List<Color> colors, List<float> weights, SeasonTint tint)
        {
            float sum = 0f;
            float total = 0f;

            for (int i = 0; i < colors.Count; i++)
            {
                float weight = i < weights.Count ? weights[i] : 1f;
                if (weight <= 0f)
                {
                    continue;
                }

                Color color = colors[i];
                sum += weight * Mathf.Max(color.r, Mathf.Max(color.g, color.b));
                total += weight;
            }

            float mean = total > 0f ? sum / total : 0f;
            if (mean < 1e-4f)
            {
                return tint.brightness;
            }

            float targetValue = Mathf.Max(tint.target.r, Mathf.Max(tint.target.g, tint.target.b));
            return Mathf.Lerp(1f, targetValue / mean, tint.blend) * tint.brightness;
        }

        /// <summary>
        /// Shifts one colour. <paramref name="weight"/> scales the whole effect,
        /// so a petal marked 0.3 keeps most of the colour that makes it
        /// recognisable while the leaves around it turn fully.
        /// </summary>
        public static Color Apply(Color source, SeasonTint tint, float valueScale, float weight)
        {
            weight = Mathf.Clamp01(weight);
            if (tint == null || weight <= 0f)
            {
                return source;
            }

            float hue, saturation, value;
            RgbToHsv(source, out hue, out saturation, out value);

            float targetHue, targetSaturation, unusedValue;
            RgbToHsv(tint.target, out targetHue, out targetSaturation, out unusedValue);

            float blend = Mathf.Clamp01(tint.blend) * weight;

            // A grey has no hue to interpolate from -- RgbToHsv reports 0, which
            // is red, and raising the saturation of that would tint dead wood
            // pink. Adopt the target's hue outright instead.
            hue = saturation < 1e-4f
                ? targetHue
                : LerpHue(hue, targetHue, blend);

            saturation = Mathf.Clamp01(
                Mathf.Lerp(saturation, targetSaturation, blend) * Mathf.Lerp(1f, tint.saturation, weight));

            value = Mathf.Clamp01(value * Mathf.Lerp(1f, valueScale, weight));

            Color result = HsvToRgb(hue, saturation, value);

            // Alpha is the wind phase seed. Carrying it through unchanged is the
            // difference between a recoloured plant and one that sways out of
            // step with itself.
            result.a = source.a;
            return result;
        }

        /// <summary>Interpolates around the hue circle by the shorter arc.</summary>
        private static float LerpHue(float from, float to, float t)
        {
            float delta = to - from;

            if (delta > 0.5f)
            {
                delta -= 1f;
            }
            else if (delta < -0.5f)
            {
                delta += 1f;
            }

            return Mathf.Repeat(from + delta * Mathf.Clamp01(t), 1f);
        }

        /// <summary>Hue, saturation and value in [0, 1]. Alpha is ignored.</summary>
        private static void RgbToHsv(Color color, out float hue, out float saturation, out float value)
        {
            float r = Mathf.Clamp01(color.r);
            float g = Mathf.Clamp01(color.g);
            float b = Mathf.Clamp01(color.b);

            float max = Mathf.Max(r, Mathf.Max(g, b));
            float min = Mathf.Min(r, Mathf.Min(g, b));
            float chroma = max - min;

            value = max;
            saturation = max > 0f ? chroma / max : 0f;

            if (chroma < 1e-6f)
            {
                hue = 0f;
                return;
            }

            float sector;
            if (max == r)
            {
                sector = (g - b) / chroma;
            }
            else if (max == g)
            {
                sector = 2f + (b - r) / chroma;
            }
            else
            {
                sector = 4f + (r - g) / chroma;
            }

            hue = Mathf.Repeat(sector / 6f, 1f);
        }

        /// <summary>The inverse of <see cref="RgbToHsv"/>. Alpha is set to 1.</summary>
        private static Color HsvToRgb(float hue, float saturation, float value)
        {
            saturation = Mathf.Clamp01(saturation);
            value = Mathf.Clamp01(value);

            if (saturation < 1e-6f)
            {
                return new Color(value, value, value, 1f);
            }

            float sector = Mathf.Repeat(hue, 1f) * 6f;
            int index = Mathf.FloorToInt(sector);
            float fraction = sector - index;

            float p = value * (1f - saturation);
            float q = value * (1f - saturation * fraction);
            float t = value * (1f - saturation * (1f - fraction));

            switch (index)
            {
                case 0:
                    return new Color(value, t, p, 1f);

                case 1:
                    return new Color(q, value, p, 1f);

                case 2:
                    return new Color(p, value, t, 1f);

                case 3:
                    return new Color(p, q, value, 1f);

                case 4:
                    return new Color(t, p, value, 1f);

                default:
                    return new Color(value, p, q, 1f);
            }
        }
    }
}
