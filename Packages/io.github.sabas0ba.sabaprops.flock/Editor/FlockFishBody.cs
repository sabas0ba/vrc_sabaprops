using System.Collections.Generic;
using UnityEngine;

namespace SabaProps.Flock.Editors
{
    internal static partial class FlockBodyBuilder
    {
        /// <summary>Side profile of a fish body type.</summary>
        private struct FishProfile
        {
            /// <summary>Axial position of the greatest depth.</summary>
            public float Peak;
            /// <summary>Depth of the tail stock relative to the greatest depth.</summary>
            public float Peduncle;
            /// <summary>Depth just behind the snout relative to the greatest depth.</summary>
            public float Nose;
            public Vector2 Dorsal;
            public Vector2 Anal;
        }

        private static FishProfile Profile(FlockFishBody body)
        {
            switch (body)
            {
                case FlockFishBody.Torpedo:
                    return new FishProfile
                    {
                        Peak = 0.4f, Peduncle = 0.12f, Nose = 0.25f,
                        Dorsal = new Vector2(0.28f, 0.45f), Anal = new Vector2(0.62f, 0.72f),
                    };
                case FlockFishBody.Elongated:
                    return new FishProfile
                    {
                        Peak = 0.45f, Peduncle = 0.3f, Nose = 0.1f,
                        Dorsal = new Vector2(0.38f, 0.55f), Anal = new Vector2(0.62f, 0.78f),
                    };
                case FlockFishBody.Disc:
                    return new FishProfile
                    {
                        Peak = 0.4f, Peduncle = 0.25f, Nose = 0.35f,
                        Dorsal = new Vector2(0.12f, 0.7f), Anal = new Vector2(0.45f, 0.75f),
                    };
                case FlockFishBody.Ray:
                    return new FishProfile
                    {
                        Peak = 0.35f, Peduncle = 0.15f, Nose = 0.4f,
                        Dorsal = new Vector2(0.6f, 0.66f), Anal = Vector2.zero,
                    };
                default:
                    return new FishProfile
                    {
                        Peak = 0.35f, Peduncle = 0.22f, Nose = 0.2f,
                        Dorsal = new Vector2(0.3f, 0.55f), Anal = new Vector2(0.62f, 0.8f),
                    };
            }
        }

        /// <summary>Relative depth at axial coordinate <paramref name="a"/> (0 snout, 1 tail stock).</summary>
        private static float ProfileAt(FishProfile p, float a)
        {
            if (a < p.Peak)
            {
                float t = a / p.Peak;
                float u = 1f - t;
                return p.Nose + (1f - p.Nose) * Mathf.Sqrt(Mathf.Max(1f - u * u, 0f));
            }

            float s = SmoothStep(p.Peak, 1f, a);
            return Mathf.Lerp(1f, p.Peduncle, s);
        }

        private readonly struct FishShape
        {
            public readonly FlockSpecies Species;
            public readonly FishProfile Profile;
            public readonly float Length;
            public readonly float Depth;
            public readonly float Width;

            public FishShape(FlockSpecies species)
            {
                Species = species;
                Profile = FlockBodyBuilder.Profile(species.fishBody);
                Length = species.bodyLength;
                Depth = species.bodyDepth;
                Width = species.fishBody == FlockFishBody.Ray
                    ? Mathf.Min(species.bodyWidth, 0.3f)
                    : species.bodyWidth;
            }

            public float Z(float a) => 0.5f * Length - a * Length;
            public float HalfHeight(float a) => 0.5f * Depth * Length * ProfileAt(Profile, a);
            public float HalfWidth(float a) => 0.5f * Width * Length * ProfileAt(Profile, a);
        }

        private static void BuildFish(FlockMeshBuffer buffer, FlockSpecies s, FlockDetail detail)
        {
            var shape = new FishShape(s);
            bool ray = s.fishBody == FlockFishBody.Ray;

            if (detail == FlockDetail.Silhouette)
            {
                AddFishRibbons(buffer, shape, ray);
            }
            else
            {
                AddFishBody(buffer, shape, detail);
            }

            AddCaudalFin(buffer, shape);

            if (ray)
            {
                AddRayWing(buffer, shape, detail, 1f);
                AddRayWing(buffer, shape, detail, -1f);
                return;
            }

            if (s.dorsalHeight > 0.005f)
            {
                AddMedianFin(buffer, shape, shape.Profile.Dorsal, s.dorsalHeight, 1f);
            }

            if (detail == FlockDetail.High)
            {
                if (s.analHeight > 0.005f && shape.Profile.Anal.y > shape.Profile.Anal.x)
                {
                    AddMedianFin(buffer, shape, shape.Profile.Anal, s.analHeight, -1f);
                }

                if (s.pectoralSize > 0.005f)
                {
                    AddPectoralFin(buffer, shape, 1f);
                    AddPectoralFin(buffer, shape, -1f);
                }
            }
        }

        /// <summary>
        /// Body colour at axial coordinate <paramref name="a"/> and height
        /// <paramref name="v"/> (cosine of the ring angle).
        /// <paramref name="side"/> separates left from right for patterns that
        /// should not be mirrored.
        /// </summary>
        private static Color FishBodyColor(FlockSpecies s, float a, float v, float side)
        {
            Color c = Color.Lerp(s.secondary, s.primary, SmoothStep(-0.25f, 0.35f, v));
            float n = Mathf.Max(s.patternCount, 1);
            bool mark = false;

            switch (s.pattern)
            {
                case FlockColorPattern.VerticalBars:
                    if (a > 0.1f && a < 0.9f)
                    {
                        float phase = (a - 0.1f) / 0.8f * n;
                        mark = Mathf.Abs(FlockMotion.Frac(phase) - 0.5f) < 0.18f;
                    }

                    break;
                case FlockColorPattern.BackBars:
                    if (v > 0.05f && a > 0.15f && a < 0.95f)
                    {
                        float phase = (a - 0.15f) / 0.8f * n + 0.15f * v * n;
                        mark = Mathf.Abs(FlockMotion.Frac(phase) - 0.5f) < 0.15f;
                    }

                    break;
                case FlockColorPattern.LateralStripe:
                    mark = Mathf.Abs(v - 0.1f) < 0.12f && a > 0.05f && a < 0.97f;
                    break;
                case FlockColorPattern.HorizontalStripes:
                    if (v > -0.7f && v < 0.7f && a > 0.08f)
                    {
                        float phase = (v + 0.7f) / 1.4f * n;
                        mark = FlockMotion.Frac(phase) < 0.45f;
                    }

                    break;
                case FlockColorPattern.BellyStripes:
                    if (v < -0.05f && a > 0.2f && a < 0.9f)
                    {
                        float phase = -v / 0.9f * n;
                        mark = FlockMotion.Frac(phase) < 0.4f;
                    }

                    break;
                case FlockColorPattern.Spots:
                    if (v > -0.2f && a > 0.12f && a < 0.9f)
                    {
                        int cellA = Mathf.FloorToInt(a * n * 2f);
                        int cellV = Mathf.FloorToInt((v + 1f) * 3f);
                        int cellS = side > 0f ? 1 : 0;
                        mark = FlockRandom.Value(cellA, cellV, cellS) > 0.62f;
                    }

                    break;
                case FlockColorPattern.Patches:
                    if (v > -0.35f)
                    {
                        float noise = Mathf.Sin(a * n * 3.1f + side * 1.7f + 0.4f)
                            * Mathf.Sin(v * 2.3f + a * n * 1.3f + 1.1f);
                        mark = noise > 0.15f;
                    }

                    break;
                case FlockColorPattern.EyeBar:
                    mark = a > 0.1f && a < 0.17f;
                    break;
            }

            if (mark)
            {
                c = s.accent;
            }

            return WithSheen(c, s.sheen);
        }

        private static List<float> FishStations(FlockSpecies s, FlockDetail detail)
        {
            var stations = detail == FlockDetail.High
                ? new List<float>()
                : new List<float> { 0.08f, 0.3f, 0.55f, 0.8f, 1f };

            if (detail == FlockDetail.High)
            {
                const int rings = 14;
                for (int i = 0; i < rings; i++)
                {
                    float t = (float)i / (rings - 1);
                    // Denser toward the head, where the profile curves most.
                    stations.Add(0.03f + 0.97f * (0.35f * t + 0.65f * t * t));
                }

                float n = Mathf.Max(s.patternCount, 1);
                switch (s.pattern)
                {
                    case FlockColorPattern.VerticalBars when n <= 6f:
                        for (int k = 0; k < n; k++)
                        {
                            AddBoundary(stations, 0.1f + (k + 0.32f) / n * 0.8f);
                            AddBoundary(stations, 0.1f + (k + 0.68f) / n * 0.8f);
                        }

                        break;
                    case FlockColorPattern.VerticalBars:
                        for (int k = 0; k < 2 * n; k++)
                        {
                            stations.Add(0.1f + (k + 0.5f) / (2f * n) * 0.8f);
                        }

                        break;
                    case FlockColorPattern.EyeBar:
                        AddBoundary(stations, 0.1f);
                        AddBoundary(stations, 0.17f);
                        break;
                }
            }

            SortUnique(stations);
            return stations;
        }

        private static List<float> FishAngles(FlockSpecies s, FlockDetail detail)
        {
            float n = Mathf.Max(s.patternCount, 1);
            bool striped = s.pattern == FlockColorPattern.HorizontalStripes
                || s.pattern == FlockColorPattern.BellyStripes;
            List<float> angles = RingAngles(detail != FlockDetail.High ? 6 : striped ? 8 : 10);
            if (detail != FlockDetail.High)
            {
                return angles;
            }

            switch (s.pattern)
            {
                case FlockColorPattern.LateralStripe:
                    AddHeightBoundary(angles, -0.02f);
                    AddHeightBoundary(angles, 0.22f);
                    break;
                case FlockColorPattern.HorizontalStripes:
                    for (int k = 0; k < n; k++)
                    {
                        AddStripeHeights(angles, -0.7f + k / n * 1.4f, -0.7f + (k + 0.45f) / n * 1.4f, n);
                    }

                    break;
                case FlockColorPattern.BellyStripes:
                    for (int k = 0; k < n; k++)
                    {
                        AddStripeHeights(angles, -((k + 0.4f) / n) * 0.9f, -(k / n) * 0.9f, n);
                    }

                    break;
            }

            for (int i = 0; i < angles.Count; i++)
            {
                angles[i] = Mathf.Repeat(angles[i], FlockMotion.TwoPi);
            }

            angles.Sort();
            for (int i = angles.Count - 1; i > 0; i--)
            {
                if (angles[i] - angles[i - 1] < BoundaryGap)
                {
                    angles.RemoveAt(i);
                }
            }

            return angles;
        }

        /// <summary>
        /// Ring angles for one horizontal stripe between heights
        /// <paramref name="low"/> and <paramref name="high"/>. Up to three
        /// stripes get sharp edges; more would multiply the ring size, so
        /// those are sampled at the stripe and gap centres and blend softly.
        /// </summary>
        private static void AddStripeHeights(List<float> angles, float low, float high, float stripes)
        {
            if (stripes <= 3f)
            {
                AddHeightBoundary(angles, low);
                AddHeightBoundary(angles, high);
                return;
            }

            float width = high - low;
            float centre = 0.5f * (low + high);
            float gap = high + 0.5f * width * (1f / 0.45f - 1f);
            foreach (float v in new[] { centre, gap })
            {
                float phi = Mathf.Acos(Mathf.Clamp(v, -0.98f, 0.98f));
                angles.Add(phi);
                angles.Add(FlockMotion.TwoPi - phi);
            }
        }

        private static void AddFishBody(FlockMeshBuffer buffer, FishShape shape, FlockDetail detail)
        {
            FlockSpecies s = shape.Species;
            var stations = new List<TubeStation>
            {
                new TubeStation { Z = shape.Z(0f), Axial = 0f },
            };

            foreach (float a in FishStations(s, detail))
            {
                stations.Add(new TubeStation
                {
                    Z = shape.Z(a),
                    HalfWidth = shape.HalfWidth(a),
                    HalfHeight = shape.HalfHeight(a),
                    Axial = a,
                });
            }

            stations.Add(new TubeStation { Z = shape.Z(1.02f), Axial = 1.02f });

            List<float> angles = FishAngles(s, detail);
            // The tube hands the colour function cos(angle) only, which is the
            // same on both flanks; the side is recovered from the vertex that
            // was just written.
            int first = buffer.VertexCount;
            AddTube(buffer, stations, angles, (a, v) => FishBodyColor(s, a, v, 1f), FlockBodyPart.Body);

            if (s.pattern == FlockColorPattern.Spots || s.pattern == FlockColorPattern.Patches)
            {
                for (int i = first; i < buffer.VertexCount; i++)
                {
                    Vector3 p = buffer.Positions[i];
                    Vector4 body = buffer.Body[i];
                    float h = Mathf.Max(shape.HalfHeight(body.y), 1e-6f);
                    float v = Mathf.Clamp(p.y / h, -1f, 1f);
                    buffer.Colors[i] = FishBodyColor(s, body.y, v, p.x >= 0f ? 1f : -1f);
                }
            }
        }

        private static void AddFishRibbons(FlockMeshBuffer buffer, FishShape shape, bool ray)
        {
            FlockSpecies s = shape.Species;
            var stations = new List<float> { 0f, 0.15f, 0.4f, 0.7f, 1f };
            float thickness = 0.01f * shape.Depth * shape.Length;

            Vector4 Body(int i, int k) => new Vector4(0f, stations[i], (float)FlockBodyPart.Body, 0f);

            AddDoubleGrid(
                buffer, stations.Count, 2,
                (i, k) => new Vector3((k == 0 ? -1f : 1f) * shape.HalfWidth(stations[i]), 0f, shape.Z(stations[i])),
                Vector3.up,
                (i, k, top) => FishBodyColor(s, stations[i], top ? 1f : -1f, 1f),
                Body, thickness);

            if (ray)
            {
                return;
            }

            AddDoubleGrid(
                buffer, stations.Count, 2,
                (i, k) => new Vector3(0f, (k == 0 ? -1f : 1f) * shape.HalfHeight(stations[i]), shape.Z(stations[i])),
                Vector3.right,
                (i, k, top) => FishBodyColor(s, stations[i], k == 0 ? -0.8f : 0.8f, top ? 1f : -1f),
                Body, thickness);
        }

        private static void AddCaudalFin(FlockMeshBuffer buffer, FishShape shape)
        {
            FlockSpecies s = shape.Species;
            float cl = Mathf.Max(s.caudalSize, 0.02f) * shape.Length;
            float h1 = Mathf.Max(shape.HalfHeight(1f), 0.02f * shape.Length) * 1.1f;
            float root = shape.Z(1f);

            float[] ds;
            float[] ys;
            switch (s.caudalFin)
            {
                case FlockCaudalFin.Lunate:
                    ds = new[] { 0f, 0.6f, 0.9f, 0.35f, 0.9f, 0.6f, 0f };
                    ys = new[] { 1f, 0.9f, 1f, 0f, -1f, -0.9f, -1f };
                    break;
                case FlockCaudalFin.Truncate:
                    ds = new[] { 0f, 1f, 1f, 0f };
                    ys = new[] { 1f, 0.55f, -0.55f, -1f };
                    break;
                case FlockCaudalFin.Rounded:
                    ds = new[] { 0f, 0.6f, 1f, 1.05f, 1f, 0.6f, 0f };
                    ys = new[] { 1f, 0.5f, 0.25f, 0f, -0.25f, -0.5f, -1f };
                    break;
                case FlockCaudalFin.Veil:
                    ds = new[] { 0f, 0.7f, 1f, 0.85f, 1f, 0.7f, 0f };
                    ys = new[] { 1f, 0.6f, 0.45f, 0.05f, -0.45f, -0.6f, -1f };
                    break;
                case FlockCaudalFin.Heterocercal:
                    ds = new[] { 0f, 1f, 0.8f, 0.3f, 0.45f, 0f };
                    ys = new[] { 1f, 0.8f, 0.55f, 0f, -0.4f, -1f };
                    break;
                case FlockCaudalFin.Whip:
                    ds = new[] { 0f, 1f, 0f };
                    ys = new[] { 1f, 0f, -1f };
                    h1 = 0.015f * shape.Length;
                    break;
                default:
                    ds = new[] { 0f, 1f, 0.4f, 1f, 0f };
                    ys = new[] { 1f, 0.75f, 0f, -0.75f, -1f };
                    break;
            }

            var outline = new List<Vector3>(ds.Length);
            for (int i = 0; i < ds.Length; i++)
            {
                // The root edge spans the tail stock; the lobes scale with the fin length.
                float y = ds[i] <= 0f ? ys[i] * h1 : ys[i] * Mathf.Max(cl, h1);
                if (s.caudalFin == FlockCaudalFin.Whip)
                {
                    y = ys[i] * h1;
                }

                outline.Add(new Vector3(0f, y, root - ds[i] * cl));
            }

            AddDoubleFan(
                buffer, new Vector3(0f, 0f, root - 0.05f * cl), outline, Vector3.right,
                WithSheen(s.extra, 0f), WithSheen(s.extra, 0f),
                p => new Vector4(0f, (0.5f * shape.Length - p.z) / shape.Length, (float)FlockBodyPart.Tail, 0f),
                0.004f * shape.Length);
        }

        /// <summary>Dorsal (<paramref name="up"/> = 1) or anal (-1) fin along <paramref name="range"/>.</summary>
        private static void AddMedianFin(FlockMeshBuffer buffer, FishShape shape, Vector2 range, float height, float up)
        {
            FlockSpecies s = shape.Species;
            float a0 = range.x;
            float a1 = range.y;
            float span = a1 - a0;
            float fin = height * shape.Length;

            Vector3 Point(float a, float lift)
            {
                return new Vector3(0f, up * (shape.HalfHeight(a) * 0.9f + lift), shape.Z(a));
            }

            var outline = new List<Vector3>
            {
                Point(a0, 0f),
                Point(a0 + 0.25f * span, fin),
                Point(a1, 0.35f * fin),
                Point(a1, 0f),
            };
            float mid = 0.5f * (a0 + a1);
            var centre = new Vector3(0f, up * shape.HalfHeight(mid) * 0.5f, shape.Z(mid));

            AddDoubleFan(
                buffer, centre, outline, Vector3.right,
                WithSheen(s.detail, 0f), WithSheen(s.detail, 0f),
                p => new Vector4(0f, (0.5f * shape.Length - p.z) / shape.Length, (float)FlockBodyPart.Fin, 0f),
                0.004f * shape.Length);
        }

        private static void AddPectoralFin(FlockMeshBuffer buffer, FishShape shape, float side)
        {
            FlockSpecies s = shape.Species;
            float p = s.pectoralSize * shape.Length;
            float a0 = 0.22f;
            float a1 = 0.28f;
            float y = -0.35f * shape.HalfHeight(0.25f);

            Vector3 Base(float a) => new Vector3(side * shape.HalfWidth(a) * 0.9f, y, shape.Z(a));

            var outline = new List<Vector3>
            {
                Base(a0),
                new Vector3(side * (shape.HalfWidth(0.25f) + 0.6f * p), y - 0.3f * p, shape.Z(0.25f) - 0.8f * p),
                Base(a1),
            };
            Vector3 centre = (Base(a0) + Base(a1)) * 0.5f;
            Vector3 normal = Vector3.Cross(outline[1] - outline[0], outline[2] - outline[0]).normalized;
            if (normal.y < 0f)
            {
                normal = -normal;
            }

            AddDoubleFan(
                buffer, centre, outline, normal,
                WithSheen(s.detail, 0f), WithSheen(s.detail, 0f),
                q => new Vector4(0f, (0.5f * shape.Length - q.z) / shape.Length, (float)FlockBodyPart.Fin, 0f),
                0.004f * shape.Length);
        }

        /// <summary>Pectoral disc of a ray, waved by span in the shader.</summary>
        private static void AddRayWing(FlockMeshBuffer buffer, FishShape shape, FlockDetail detail, float side)
        {
            FlockSpecies s = shape.Species;
            float root = shape.HalfWidth(0.4f) * 0.9f;
            float halfSpan = Mathf.Max(s.pectoralSize * shape.Length - root, 0.05f * shape.Length);

            List<float> span = detail == FlockDetail.High ? Uniform(7)
                : detail == FlockDetail.Low ? Uniform(3) : Uniform(2);
            List<float> chord = detail == FlockDetail.High ? Uniform(3) : Uniform(1);

            Vector3 Position(int i, int k)
            {
                float u = span[i];
                float leading = Mathf.Lerp(shape.Z(0.08f), shape.Z(0.42f), Mathf.Pow(u, 0.7f));
                float trailing = Mathf.Lerp(shape.Z(0.82f), shape.Z(0.45f), Mathf.Pow(u, 1.4f));
                return new Vector3(
                    side * (root + u * halfSpan),
                    -0.05f * halfSpan * u,
                    Mathf.Lerp(leading, trailing, chord[k]));
            }

            Color WingColor(int i, int k, bool top)
            {
                if (!top)
                {
                    return WithSheen(s.secondary, 0f);
                }

                if (s.pattern == FlockColorPattern.Spots
                    && FlockRandom.Value(i, k, side > 0f ? 3 : 4) > 0.6f)
                {
                    return WithSheen(s.accent, 0f);
                }

                return WithSheen(s.primary, 0f);
            }

            AddDoubleGrid(
                buffer, span.Count, chord.Count, Position, Vector3.up, WingColor,
                (i, k) => new Vector4(
                    side * span[i],
                    (0.5f * shape.Length - Position(i, k).z) / shape.Length,
                    (float)FlockBodyPart.Wing,
                    root),
                0.01f * shape.Length);
        }
    }
}
