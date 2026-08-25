using System.Collections.Generic;
using UnityEngine;

namespace SabaProps.Foliage.Editors
{
    /// <summary>
    /// Applies a <see cref="SeasonStyle"/> to the vertex colours of a mesh under
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
        /// <summary>Applies a season to a buffer in place.</summary>
        internal static void Apply(FoliageMeshBuffer buffer, SeasonStyle style)
        {
            if (buffer == null || style == null || style.IsIdentity)
            {
                return;
            }

            Recolour(buffer, style);
            Stiffen(buffer, style);
            Bend(buffer, style);
        }

        /// <summary>
        /// Recolours a buffer in place. Vertices keep their alpha, which carries
        /// the per-element wind phase rather than opacity.
        /// </summary>
        private static void Recolour(FoliageMeshBuffer buffer, SeasonStyle tint)
        {
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
        /// Scales how far the wind moves the plant.
        /// <para>
        /// A dried stem has lost the water that let it bend, and reads wrong if
        /// it keeps waving like a green one. One factor across every vertex, not
        /// a per-part adjustment: the wind-joint rule is that rigidly joined
        /// geometry shares its bend inputs, and a uniform scale is the only kind
        /// of change that leaves every one of those relationships intact.
        /// </para>
        /// </summary>
        private static void Stiffen(FoliageMeshBuffer buffer, SeasonStyle style)
        {
            float scale = Mathf.Clamp01(style.windScale);
            if (Mathf.Abs(scale - 1f) < 1e-5f)
            {
                return;
            }

            List<Vector4> uv3 = buffer.Uv3;
            for (int i = 0; i < uv3.Count; i++)
            {
                Vector4 entry = uv3[i];
                entry.w *= scale;
                uv3[i] = entry;
            }
        }

        /// <summary>
        /// Bends the plant over from its base, as a stalk that has dried out and
        /// can no longer hold its own weight does.
        /// <para>
        /// A rotation about the root rather than a sideways offset: rotating
        /// preserves the length of the stem, and the same rotation applied to
        /// the normal keeps the lighting correct without recalculating anything.
        /// </para>
        /// <para>
        /// The angle grows with UV0.y, the bend mask the wind already uses, so
        /// the base stays planted and the tip travels furthest. That also makes
        /// it free for any generator that fills the channel correctly — the same
        /// reason the recolour needs no per-species knowledge.
        /// </para>
        /// </summary>
        private static void Bend(FoliageMeshBuffer buffer, SeasonStyle style)
        {
            float droop = Mathf.Clamp01(style.droop);
            if (droop <= 0f)
            {
                return;
            }

            List<Vector3> positions = buffer.Positions;
            List<Vector3> normals = buffer.Normals;
            List<Vector2> uv0 = buffer.Uv0;

            // Towards object-space +Z, the direction the sunflower already leans
            // and tilts. Every instance is yawed at random when it is placed, so
            // a field does not end up bending as one.
            for (int i = 0; i < positions.Count; i++)
            {
                float t = i < uv0.Count ? Mathf.Clamp01(uv0[i].y) : 0f;
                if (t <= 0f)
                {
                    continue;
                }

                Quaternion bend = Quaternion.AngleAxis(droop * MaxDroopDegrees * t * t, Vector3.right);

                positions[i] = bend * positions[i];

                if (i < normals.Count)
                {
                    normals[i] = bend * normals[i];
                }
            }
        }

        /// <summary>How far over a fully drooping plant leans at its tip.</summary>
        private const float MaxDroopDegrees = 80f;

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
        private static float ValueScale(List<Color> colors, List<float> weights, SeasonStyle tint)
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
        public static Color Apply(Color source, SeasonStyle tint, float valueScale, float weight)
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
