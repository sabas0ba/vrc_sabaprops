using System.Collections.Generic;
using UnityEngine;

namespace SabaProps.Flock.Editors
{
    internal static partial class FlockBodyBuilder
    {
        /// <summary>Planform of a wing shape, all relative to the half span.</summary>
        private struct WingPlanform
        {
            public float RootChord;
            public float TipRatio;
            public float Sweep;
            /// <summary>Span coordinate where separated primaries or scallops begin.</summary>
            public float NotchStart;
            public int NotchCount;
            /// <summary>Fraction of the chord removed at the deepest point of a notch.</summary>
            public float NotchDepth;
        }

        private static WingPlanform Planform(FlockWingShape shape)
        {
            switch (shape)
            {
                case FlockWingShape.Rounded:
                    return new WingPlanform { RootChord = 0.55f, TipRatio = 0.7f, Sweep = 0.08f };
                case FlockWingShape.Slotted:
                    return new WingPlanform
                    {
                        RootChord = 0.5f, TipRatio = 0.85f, Sweep = 0.05f,
                        NotchStart = 0.72f, NotchCount = 5, NotchDepth = 0.45f,
                    };
                case FlockWingShape.Long:
                    return new WingPlanform { RootChord = 0.2f, TipRatio = 0.35f, Sweep = 0.12f };
                case FlockWingShape.Sickle:
                    return new WingPlanform { RootChord = 0.32f, TipRatio = 0.12f, Sweep = 0.75f };
                case FlockWingShape.Membrane:
                    return new WingPlanform
                    {
                        RootChord = 0.5f, TipRatio = 0.45f, Sweep = 0.1f,
                        NotchStart = 0f, NotchCount = 4, NotchDepth = 0.3f,
                    };
                default:
                    return new WingPlanform { RootChord = 0.42f, TipRatio = 0.25f, Sweep = 0.35f };
            }
        }

        /// <summary>Lengths of the bird body along Z, scaled so beak tip to tail tip equals the body length.</summary>
        private struct BirdLayout
        {
            public float Length;
            public float BodyRadius;
            public float BeakTip;
            public float HeadFront;
            public float HeadBack;
            public float NeckBack;
            public float TailRoot;
            public float Torso;
            public float TailLength;
            public float HeadRaise;
        }

        private static BirdLayout LayoutBird(FlockSpecies s)
        {
            float length = s.bodyLength;
            float torso = 0.42f * length;
            float head = 0.13f * length;
            float tail = s.tailLength * length;
            float neck = s.neckLength * length;
            float beak = s.beakLength * length;
            float scale = length / (torso + head + tail + neck + beak);

            var layout = new BirdLayout
            {
                Length = length,
                Torso = torso * scale,
                TailLength = tail * scale,
            };
            layout.BodyRadius = 0.21f * layout.Torso;
            layout.BeakTip = 0.5f * length;
            layout.HeadFront = layout.BeakTip - beak * scale;
            layout.HeadBack = layout.HeadFront - head * scale;
            layout.NeckBack = layout.HeadBack - neck * scale;
            layout.TailRoot = layout.NeckBack - layout.Torso;
            // Short-necked birds carry the head a little above the body axis;
            // long-necked ones fly with the neck stretched straight ahead.
            layout.HeadRaise = s.neckLength > 0.2f ? 0f : 0.25f * layout.BodyRadius;
            return layout;
        }

        private static void BuildBird(FlockMeshBuffer buffer, FlockSpecies s, FlockDetail detail)
        {
            BirdLayout layout = LayoutBird(s);

            if (detail == FlockDetail.Silhouette)
            {
                AddBirdRibbons(buffer, s, layout);
            }
            else
            {
                AddBirdBody(buffer, s, layout, detail);
            }

            AddBirdWing(buffer, s, layout, detail, 1f);
            AddBirdWing(buffer, s, layout, detail, -1f);
            AddBirdTail(buffer, s, layout);

            if (s.trailingLegs)
            {
                AddBirdLegs(buffer, s, layout);
            }
        }

        private static Color BirdBodyColor(FlockSpecies s, BirdLayout layout, float z, float v)
        {
            if (z > layout.HeadFront + 1e-5f)
            {
                return WithSheen(s.extra, 0f);
            }

            if (z >= layout.NeckBack - 0.02f * layout.Length)
            {
                return WithSheen(s.accent, 0f);
            }

            return WithSheen(Color.Lerp(s.secondary, s.primary, SmoothStep(-0.25f, 0.25f, v)), 0f);
        }

        private static List<TubeStation> BirdStations(BirdLayout layout, FlockDetail detail)
        {
            float r = layout.BodyRadius;
            var stations = new List<TubeStation>();

            void Add(float z, float radius, float raise)
            {
                stations.Add(new TubeStation
                {
                    Z = z,
                    HalfWidth = radius,
                    HalfHeight = radius * 0.9f,
                    YOffset = raise,
                    Axial = (layout.BeakTip - z) / layout.Length,
                });
            }

            float beakLength = layout.BeakTip - layout.HeadFront;
            float headLength = layout.HeadFront - layout.HeadBack;
            float neckLength = layout.HeadBack - layout.NeckBack;
            float raise = layout.HeadRaise;

            Add(layout.BeakTip, 0f, raise);
            if (detail == FlockDetail.High && beakLength > 0.02f * layout.Length)
            {
                Add(layout.HeadFront + 0.5f * beakLength, 0.12f * r, raise);
            }

            if (detail != FlockDetail.Silhouette)
            {
                Add(layout.HeadFront, 0.3f * r, raise);
            }

            Add(layout.HeadBack + 0.5f * headLength, 0.55f * r, raise);
            if (detail == FlockDetail.High)
            {
                Add(layout.HeadBack + 0.1f * headLength, 0.5f * r, raise);
                if (neckLength > 0.03f * layout.Length)
                {
                    Add(layout.NeckBack + 0.5f * neckLength, 0.38f * r, raise * 0.5f);
                }
            }

            float[] torso = detail == FlockDetail.High ? new[] { 0.95f, 0.8f, 0.6f, 0.4f, 0.2f, 0.05f }
                : detail == FlockDetail.Low ? new[] { 0.95f, 0.6f, 0.2f }
                : new[] { 0.8f, 0.3f };
            foreach (float u in torso)
            {
                float profile = Mathf.Pow(Mathf.Sin(Mathf.PI * (0.1f + 0.8f * u)), 0.6f);
                Add(layout.TailRoot + u * layout.Torso, r * profile, 0f);
            }

            Add(layout.TailRoot - 0.06f * layout.Torso, 0f, 0f);
            return stations;
        }

        private static void AddBirdBody(FlockMeshBuffer buffer, FlockSpecies s, BirdLayout layout, FlockDetail detail)
        {
            List<TubeStation> stations = BirdStations(layout, detail);
            List<float> angles = RingAngles(detail == FlockDetail.High ? 6 : 4);
            AddTube(
                buffer, stations, angles,
                (axial, v) => BirdBodyColor(s, layout, layout.BeakTip - axial * layout.Length, v),
                FlockBodyPart.Body);
        }

        /// <summary>
        /// Horizontal ribbon standing in for the body at silhouette detail. A
        /// flying bird is read by its planform from below; from the side the
        /// wings already give it depth, so no vertical ribbon is spent on it.
        /// </summary>
        private static void AddBirdRibbons(FlockMeshBuffer buffer, FlockSpecies s, BirdLayout layout)
        {
            List<TubeStation> stations = BirdStations(layout, FlockDetail.Silhouette);
            float thickness = 0.02f * layout.BodyRadius;

            AddDoubleGrid(
                buffer, stations.Count, 2,
                (i, k) => new Vector3((k == 0 ? -1f : 1f) * stations[i].HalfWidth, stations[i].YOffset, stations[i].Z),
                Vector3.up,
                (i, k, top) => BirdBodyColor(s, layout, stations[i].Z, top ? 1f : -1f),
                (i, k) => new Vector4(0f, stations[i].Axial, (float)FlockBodyPart.Body, 0f),
                thickness);
        }

        private static void AddBirdWing(
            FlockMeshBuffer buffer, FlockSpecies s, BirdLayout layout, FlockDetail detail, float side)
        {
            WingPlanform plan = Planform(s.wingShape);
            float shoulder = 0.8f * layout.BodyRadius;
            float halfSpan = Mathf.Max(0.5f * s.bodyLength * s.wingspan - shoulder, 0.1f * s.bodyLength);
            float rootChord = Mathf.Min(plan.RootChord * halfSpan, 0.9f * layout.Torso);
            float leadingRoot = layout.TailRoot + 0.72f * layout.Torso;

            List<float> span;
            List<float> chord;
            if (detail == FlockDetail.Silhouette)
            {
                span = new List<float> { 0f, 0.5f, 1f };
                chord = new List<float> { 0f, 1f };
            }
            else
            {
                span = detail == FlockDetail.High ? Uniform(8) : new List<float> { 0f, 0.35f, 0.7f, 1f };
                if (detail == FlockDetail.High && plan.NotchCount > 0)
                {
                    int samples = plan.NotchCount * 4;
                    for (int i = 1; i < samples; i++)
                    {
                        span.Add(plan.NotchStart + (1f - plan.NotchStart) * i / samples);
                    }
                }

                AddBoundary(span, 1f - s.wingTipFraction);
                SortUnique(span);

                chord = new List<float> { 0f, 1f };
                AddBoundary(chord, 1f - s.flightFeatherFraction);
                SortUnique(chord);
            }

            bool notched = detail == FlockDetail.High && plan.NotchCount > 0;
            float Notch(float u)
            {
                if (!notched || u <= plan.NotchStart)
                {
                    return 0f;
                }

                float t = (u - plan.NotchStart) / (1f - plan.NotchStart);
                return 0.5f - 0.5f * Mathf.Cos(FlockMotion.TwoPi * plan.NotchCount * t);
            }

            Vector3 Position(int i, int k)
            {
                float u = span[i];
                float leading = leadingRoot - plan.Sweep * halfSpan * Mathf.Pow(u, 1.6f);
                float c = rootChord * Mathf.Lerp(1f, plan.TipRatio, Mathf.Pow(u, 1.1f));
                c *= 1f - plan.NotchDepth * Notch(u);
                return new Vector3(side * (shoulder + u * halfSpan), 0f, leading - chord[k] * c);
            }

            Color WingColor(int i, int k, bool top)
            {
                bool tip = span[i] > 1f - s.wingTipFraction && s.wingTipFraction > 0f;
                bool feather = chord[k] > 1f - s.flightFeatherFraction && s.flightFeatherFraction > 0f;
                if (tip || feather)
                {
                    return WithSheen(s.detail, 0f);
                }

                return WithSheen(top ? s.primary : s.secondary, 0f);
            }

            AddDoubleGrid(
                buffer, span.Count, chord.Count, Position, Vector3.up, WingColor,
                (i, k) => new Vector4(
                    side * span[i],
                    (layout.BeakTip - Position(i, k).z) / layout.Length,
                    (float)FlockBodyPart.Wing,
                    shoulder),
                0.02f * layout.BodyRadius);
        }

        private static void AddBirdTail(FlockMeshBuffer buffer, FlockSpecies s, BirdLayout layout)
        {
            float lt = Mathf.Max(layout.TailLength, 0.05f * layout.Length);
            float w = Mathf.Max(1.2f * layout.BodyRadius, 0.45f * lt);
            float root = layout.TailRoot + 0.05f * layout.Torso;

            float[] xs;
            float[] ds;
            switch (s.tailShape)
            {
                case FlockTailShape.Square:
                    xs = new[] { -0.5f, -0.6f, 0.6f, 0.5f };
                    ds = new[] { 0f, 1f, 1f, 0f };
                    break;
                case FlockTailShape.Forked:
                    xs = new[] { -0.5f, -0.8f, 0f, 0.8f, 0.5f };
                    ds = new[] { 0f, 1f, 0.7f, 1f, 0f };
                    break;
                case FlockTailShape.DeepFork:
                    xs = new[] { -0.4f, -0.6f, 0f, 0.6f, 0.4f };
                    ds = new[] { 0f, 1f, 0.35f, 1f, 0f };
                    break;
                case FlockTailShape.Wedge:
                    xs = new[] { -0.5f, -0.3f, 0f, 0.3f, 0.5f };
                    ds = new[] { 0f, 0.85f, 1f, 0.85f, 0f };
                    break;
                case FlockTailShape.Fan:
                    xs = new[] { -0.5f, -1f, -0.6f, 0f, 0.6f, 1f, 0.5f };
                    ds = new[] { 0f, 0.8f, 1f, 1.05f, 1f, 0.8f, 0f };
                    break;
                case FlockTailShape.Long:
                    xs = new[] { -0.3f, -0.15f, 0.15f, 0.3f };
                    ds = new[] { 0f, 1f, 1f, 0f };
                    break;
                default:
                    xs = new[] { -0.35f, -0.3f, 0f, 0.3f, 0.35f };
                    ds = new[] { 0f, 0.9f, 1f, 0.9f, 0f };
                    break;
            }

            var outline = new List<Vector3>(xs.Length);
            for (int i = 0; i < xs.Length; i++)
            {
                outline.Add(new Vector3(xs[i] * w, 0f, root - ds[i] * (lt + 0.05f * layout.Torso)));
            }

            AddDoubleFan(
                buffer, new Vector3(0f, 0f, root - 0.02f * lt), outline, Vector3.up,
                WithSheen(s.primary, 0f), WithSheen(s.secondary, 0f),
                p => new Vector4(0f, (layout.BeakTip - p.z) / layout.Length, (float)FlockBodyPart.Tail, 0f),
                0.02f * layout.BodyRadius);
        }

        private static void AddBirdLegs(FlockMeshBuffer buffer, FlockSpecies s, BirdLayout layout)
        {
            float start = layout.TailRoot + 0.15f * layout.Torso;
            float end = layout.TailRoot - layout.TailLength - 0.35f * layout.Length;
            float halfWidth = 0.12f * layout.BodyRadius;
            float y = -0.4f * layout.BodyRadius;

            AddDoubleGrid(
                buffer, 2, 2,
                (i, k) => new Vector3((k == 0 ? -1f : 1f) * halfWidth, y, i == 0 ? start : end),
                Vector3.up,
                (i, k, top) => WithSheen(s.extra, 0f),
                (i, k) => new Vector4(
                    0f,
                    (layout.BeakTip - (i == 0 ? start : end)) / layout.Length,
                    (float)FlockBodyPart.Legs,
                    0f),
                0.02f * layout.BodyRadius);
        }
    }
}
