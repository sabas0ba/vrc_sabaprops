using System.Collections.Generic;
using UnityEngine;

namespace SabaProps.Flock.Editors
{
    internal static partial class FlockBodyBuilder
    {
        private static void BuildSpecialBody(FlockMeshBuffer b, FlockSpecies s, FlockDetail detail)
        {
            float l = s.bodyLength;
            bool silhouette = detail == FlockDetail.Silhouette;
            switch (s.bodyShape)
            {
                case FlockBodyShape.Chicken:
                case FlockBodyShape.Chick:
                    GroundBird(b, s, detail);
                    return;
                case FlockBodyShape.Oyster:
                    SpecialEllipsoid(b, new Vector3(0f, 0.035f * l, 0f), new Vector3(0.65f, 0.12f, 1f) * l, s.primary, detail);
                    SpecialEllipsoid(b, new Vector3(0f, -0.035f * l, 0f), new Vector3(0.65f, 0.12f, 1f) * l, s.secondary, detail);
                    return;
                case FlockBodyShape.Eel:
                case FlockBodyShape.GardenEel:
                    bool upright = s.bodyShape == FlockBodyShape.GardenEel;
                    SpecialTube(b, Curve(detail, u => upright
                        ? new Vector3(0.06f * Mathf.Sin(u * 2.4f), u - 0.5f, 0.04f * u * u) * l
                        : new Vector3(0.08f * Mathf.Sin(u * 5f), 0f, 0.5f - u) * l),
                        0.035f * l, s.primary, detail, 1f);
                    return;
                case FlockBodyShape.Seahorse:
                    SpecialTube(b, Curve(detail, u => new Vector3(0f, 0.42f - u * 0.84f,
                        u < 0.6f ? 0.1f * Mathf.Sin(u * 5f) : 0.13f * Mathf.Sin((u - 0.6f) * 12f)) * l),
                        0.06f * l, s.primary, detail, 0.4f);
                    SpecialEllipsoid(b, new Vector3(0f, 0.33f, 0.16f) * l, new Vector3(0.16f, 0.2f, 0.24f) * l, s.accent, detail);
                    SpecialTube(b, new[] { new Vector3(0f, 0.3f, 0.2f) * l, new Vector3(0f, 0.27f, 0.4f) * l },
                        0.025f * l, s.accent, detail, 0f);
                    return;
                case FlockBodyShape.Urchin:
                    SpecialEllipsoid(b, Vector3.zero, Vector3.one * (0.58f * l), s.primary, detail);
                    int spines = silhouette ? 8 : detail == FlockDetail.Low ? 10 : 24;
                    for (int i = 0; i < spines; i++)
                    {
                        float phi = FlockMotion.TwoPi * i / spines;
                        float y = silhouette ? 0f : (i % 3 - 1) * 0.45f;
                        Vector3 direction = new Vector3(Mathf.Cos(phi), y, Mathf.Sin(phi)).normalized;
                        SpecialTube(b, new[] { direction * (0.22f * l), direction * (0.5f * l) },
                            0.012f * l, s.detail, detail, 0f);
                    }
                    return;
                case FlockBodyShape.Crab:
                    SpecialEllipsoid(b, Vector3.zero, new Vector3(0.65f, 0.18f, 0.45f) * l, s.primary, detail);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            float z = (i - 1.5f) * 0.1f;
                            SpecialTube(b, Curve(detail, u => new Vector3(side * (0.24f + u * 0.3f),
                                -0.06f * u, z + u * (i - 1.5f) * 0.055f) * l), 0.025f * l, s.detail, detail, 0.25f);
                        }
                        SpecialTube(b, new[] { new Vector3(side * 0.2f, 0f, 0.15f) * l,
                            new Vector3(side * 0.32f, 0.02f, 0.4f) * l }, 0.04f * l, s.accent, detail, 0.2f);
                        // Split claw tips, omitted at silhouette to retain the distant budget.
                        if (detail == FlockDetail.High)
                            for (int tip = -1; tip <= 1; tip += 2)
                                SpecialTube(b, new[] { new Vector3(side * 0.32f, 0.02f, 0.37f) * l,
                                    new Vector3(side * (0.32f + tip * 0.045f), 0.02f, 0.49f) * l },
                                    0.018f * l, s.accent, detail, 0f);
                    }
                    return;
            }

            bool squid = s.bodyShape == FlockBodyShape.Squid;
            bool jelly = s.bodyShape == FlockBodyShape.Jellyfish;
            bool anemone = s.bodyShape == FlockBodyShape.Anemone;
            Vector3 centre = squid ? new Vector3(0f, 0f, 0.15f) * l
                : jelly ? new Vector3(0f, 0.25f, 0f) * l : Vector3.zero;
            Vector3 size = squid ? new Vector3(0.25f, 0.25f, 0.65f) * l
                : jelly ? new Vector3(0.8f, 0.35f, 0.8f) * l
                : anemone ? new Vector3(0.4f, 0.3f, 0.4f) * l : new Vector3(0.35f, 0.3f, 0.4f) * l;
            SpecialEllipsoid(b, centre, size, s.primary, detail, jelly ? 1f : 0f);
            int arms = squid ? 10 : anemone ? (silhouette ? 6 : detail == FlockDetail.Low ? 8 : 16) : 8;
            for (int i = 0; i < arms; i++)
            {
                float angle = FlockMotion.TwoPi * i / arms;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3[] path = Curve(detail, u =>
                {
                    if (jelly) return radial * (0.22f * l) + Vector3.up * ((0.1f - 0.65f * u) * l);
                    if (anemone) return radial * ((0.13f + 0.14f * u) * l) + Vector3.up * ((0.1f + 0.5f * u) * l);
                    if (squid) return new Vector3(radial.x * (0.08f + 0.12f * u), radial.z * 0.08f, -0.12f - u * (i < 2 ? 0.55f : 0.38f)) * l;
                    return radial * ((0.12f + 0.42f * u) * l) + Vector3.up * ((-0.1f + 0.04f * Mathf.Sin(u * 4f)) * l);
                });
                SpecialTube(b, path, (jelly ? 0.008f : 0.024f) * l, squid ? s.primary : s.detail, detail, 1f);
            }
            if (squid && detail == FlockDetail.High)
                for (int side = -1; side <= 1; side += 2)
                    SpecialEllipsoid(b, new Vector3(side * 0.12f, 0f, 0.3f) * l,
                        new Vector3(0.25f, 0.025f, 0.3f) * l, s.primary, detail);
        }

        private static Vector3[] Curve(FlockDetail detail, System.Func<float, Vector3> point)
        {
            int count = detail == FlockDetail.High ? 5 : detail == FlockDetail.Low ? 3 : 2;
            var result = new Vector3[count];
            for (int i = 0; i < count; i++) result[i] = point(i / (float)(count - 1));
            return result;
        }

        private static void SpecialEllipsoid(FlockMeshBuffer b, Vector3 centre, Vector3 size,
            Color color, FlockDetail detail, float weight = 0f, FlockBodyPart part = FlockBodyPart.Body)
        {
            int start = b.VertexCount;
            int steps = detail == FlockDetail.High ? 4 : detail == FlockDetail.Low ? 3 : 2;
            int sides = detail == FlockDetail.High ? 8 : 4;
            var stations = new List<TubeStation>();
            for (int i = 0; i <= steps; i++)
            {
                float angle = Mathf.PI * i / steps;
                float radius = i == 0 || i == steps ? 0f : Mathf.Sin(angle);
                stations.Add(new TubeStation { Z = centre.z + size.z * 0.5f * Mathf.Cos(angle),
                    HalfWidth = size.x * 0.5f * radius, HalfHeight = size.y * 0.5f * radius,
                    YOffset = centre.y, Axial = weight });
            }
            AddTube(b, stations, RingAngles(sides), (u, v) => WithSheen(color, 0f), part);
            for (int i = start; i < b.VertexCount; i++)
            {
                b.Positions[i] += Vector3.right * centre.x;
                if (part == FlockBodyPart.Wing)
                {
                    Vector4 body = b.Body[i];
                    body.x = centre.x < 0f ? -1f : 1f;
                    b.Body[i] = body;
                }
            }
        }

        private static void SpecialTube(FlockMeshBuffer b, IList<Vector3> points, float radius,
            Color color, FlockDetail detail, float weight, FlockBodyPart part = FlockBodyPart.Legs)
        {
            if (detail == FlockDetail.Silhouette)
            {
                Vector3 direction = (points[points.Count - 1] - points[0]).normalized;
                Vector3 ribbonSide = Vector3.Cross(direction, Mathf.Abs(direction.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
                Vector3 ribbonNormal = Vector3.Cross(ribbonSide, direction).normalized;
                AddDoubleGrid(b, points.Count, 2,
                    (i, k) => points[i] + ribbonSide * ((k == 0 ? -1f : 1f) * radius), ribbonNormal,
                    (i, k, top) => WithSheen(color, 0f),
                    (i, k) => new Vector4(0f, weight * i / (points.Count - 1), (float)part, 0f), radius * 0.02f);
                return;
            }
            int sides = detail == FlockDetail.High ? 6 : 3;
            int first = b.VertexCount;
            int firstTriangle = b.Triangles.Count;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 tangent = points[Mathf.Min(i + 1, points.Count - 1)] - points[Mathf.Max(0, i - 1)];
                tangent = tangent.normalized;
                Vector3 axis = Mathf.Abs(tangent.y) > 0.9f ? Vector3.right : Vector3.up;
                Vector3 right = Vector3.Cross(tangent, axis).normalized;
                Vector3 up = Vector3.Cross(right, tangent);
                float taper = 1f - 0.8f * i / (points.Count - 1);
                for (int j = 0; j < sides; j++)
                {
                    float angle = FlockMotion.TwoPi * j / sides;
                    Vector3 normal = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                    b.AddVertex(points[i] + normal * radius * taper, normal, WithSheen(color, 0f),
                        new Vector4(0f, weight * i / (points.Count - 1), (float)part, 0f));
                }
            }
            for (int i = 0; i + 1 < points.Count; i++)
                for (int j = 0; j < sides; j++)
                {
                    int next = (j + 1) % sides;
                    b.AddQuad(first + i * sides + j, first + i * sides + next,
                        first + (i + 1) * sides + next, first + (i + 1) * sides + j);
                }
            // Close both ends with independent centre vertices.
            for (int end = 0; end < 2; end++)
            {
                int row = end == 0 ? 0 : points.Count - 1;
                int centre = b.AddVertex(points[row], Vector3.up, WithSheen(color, 0f),
                    new Vector4(0f, weight * row / (points.Count - 1), (float)part, 0f));
                for (int j = 0; j < sides; j++)
                    if (end == 0) b.AddTriangle(centre, first + row * sides + (j + 1) % sides, first + row * sides + j);
                    else b.AddTriangle(centre, first + row * sides + j, first + row * sides + (j + 1) % sides);
            }
            b.SmoothNormals(first, firstTriangle);
        }

        private static void GroundBird(FlockMeshBuffer b, FlockSpecies s, FlockDetail detail)
        {
            float l = s.bodyLength;
            bool chick = s.bodyShape == FlockBodyShape.Chick;
            SpecialEllipsoid(b, new Vector3(0f, 0.42f, 0f) * l, new Vector3(0.5f, 0.55f, 0.7f) * l, s.primary, detail);
            SpecialEllipsoid(b, new Vector3(0f, 0.75f, 0.23f) * l, new Vector3(0.28f, 0.32f, 0.3f) * l, s.accent, detail);
            SpecialTube(b, new[] { new Vector3(0f, 0.72f, 0.33f) * l, new Vector3(0f, 0.70f, 0.45f) * l },
                0.035f * l, s.extra, detail, 0f, FlockBodyPart.Body);
            for (int side = -1; side <= 1; side += 2)
            {
                SpecialTube(b, new[] { new Vector3(side * 0.14f, 0.28f, 0f) * l, new Vector3(side * 0.14f, 0.025f, 0.08f) * l },
                    0.023f * l, s.extra, detail, 1f);
                SpecialEllipsoid(b, new Vector3(side * 0.21f, 0.43f, -0.05f) * l,
                    new Vector3(0.10f, 0.28f, 0.42f) * l, s.secondary, detail, 0f, FlockBodyPart.Wing);
                if (detail != FlockDetail.Silhouette)
                {
                    SpecialEllipsoid(b, new Vector3(side * 0.13f, 0.79f, 0.28f) * l,
                        Vector3.one * (0.035f * l), new Color(0f, 0f, 0f, 1f), detail);
                }
            }
            if (!chick)
            {
                SpecialEllipsoid(b, new Vector3(0f, 0.94f, 0.23f) * l, new Vector3(0.07f, 0.18f, 0.22f) * l, s.detail, detail);
                SpecialTube(b, new[] { new Vector3(0f, 0.43f, -0.2f) * l, new Vector3(0f, 0.68f, -0.47f) * l },
                    0.07f * l, s.secondary, detail, 0f, FlockBodyPart.Tail);
            }
        }
    }
}
