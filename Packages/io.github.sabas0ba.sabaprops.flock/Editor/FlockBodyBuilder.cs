using System;
using System.Collections.Generic;
using UnityEngine;

namespace SabaProps.Flock.Editors
{
    /// <summary>
    /// Builds the geometry of one individual in body-local space: +Z forward,
    /// +Y up, origin near the centre of the body, lengths in metres.
    /// <para>
    /// Everything is closed or double-sided with explicit winding, so the
    /// shader can cull back faces and still show different colours above and
    /// below a wing. Colour boundaries are made sharp by placing two stations
    /// a small distance apart on each side of the boundary instead of adding a
    /// texture.
    /// </para>
    /// </summary>
    internal static partial class FlockBodyBuilder
    {
        /// <summary>Gap between the two stations placed at a colour boundary, in normalised coordinates.</summary>
        internal const float BoundaryGap = 0.002f;

        public static FlockMeshBuffer Build(FlockSpecies species, FlockDetail detail)
        {
            var buffer = new FlockMeshBuffer();
            if (species.bodyShape != FlockBodyShape.Default && species.bodyShape != FlockBodyShape.FlyingFish)
            {
                BuildSpecialBody(buffer, species, detail);
            }
            else if (species.category == FlockCategory.Bird)
            {
                BuildBird(buffer, species, detail);
            }
            else
            {
                BuildFish(buffer, species, detail);
            }

            return buffer;
        }

        // ------------------------------------------------------------------
        // Shared primitives
        // ------------------------------------------------------------------

        /// <summary>One cross-section of a tube. Zero half extents make an apex vertex.</summary>
        internal struct TubeStation
        {
            public float Z;
            public float HalfWidth;
            public float HalfHeight;
            public float YOffset;
            /// <summary>Axial coordinate written into UV0.y and passed to the colour function.</summary>
            public float Axial;

            public bool IsApex => HalfWidth <= 0f && HalfHeight <= 0f;
        }

        /// <summary>
        /// Closed tube through <paramref name="stations"/>, ordered front to
        /// back. <paramref name="angles"/> are ring angles in radians measured
        /// from the top toward +X; the colour function receives the axial
        /// coordinate and <c>cos(angle)</c> (1 on top, -1 underneath).
        /// </summary>
        internal static void AddTube(
            FlockMeshBuffer buffer,
            IList<TubeStation> stations,
            IList<float> angles,
            Func<float, float, Color> color,
            FlockBodyPart part)
        {
            int firstVertex = buffer.VertexCount;
            int firstIndex = buffer.Triangles.Count;
            int sides = angles.Count;
            var rings = new int[stations.Count];

            for (int i = 0; i < stations.Count; i++)
            {
                TubeStation st = stations[i];
                var body = new Vector4(0f, st.Axial, (float)part, 0f);
                if (st.IsApex)
                {
                    rings[i] = buffer.AddVertex(
                        new Vector3(0f, st.YOffset, st.Z), Vector3.forward,
                        color(st.Axial, 0f), body);
                    continue;
                }

                rings[i] = buffer.VertexCount;
                for (int j = 0; j < sides; j++)
                {
                    float phi = angles[j];
                    float s = Mathf.Sin(phi);
                    float c = Mathf.Cos(phi);
                    buffer.AddVertex(
                        new Vector3(st.HalfWidth * s, st.YOffset + st.HalfHeight * c, st.Z),
                        Vector3.up,
                        color(st.Axial, c),
                        body);
                }
            }

            for (int i = 0; i + 1 < stations.Count; i++)
            {
                bool frontApex = stations[i].IsApex;
                bool backApex = stations[i + 1].IsApex;
                if (frontApex && backApex)
                {
                    continue;
                }

                for (int j = 0; j < sides; j++)
                {
                    int k = (j + 1) % sides;
                    if (frontApex)
                    {
                        buffer.AddTriangle(rings[i], rings[i + 1] + k, rings[i + 1] + j);
                    }
                    else if (backApex)
                    {
                        buffer.AddTriangle(rings[i] + j, rings[i] + k, rings[i + 1]);
                    }
                    else
                    {
                        buffer.AddQuad(rings[i] + j, rings[i] + k, rings[i + 1] + k, rings[i + 1] + j);
                    }
                }
            }

            buffer.SmoothNormals(firstVertex, firstIndex);
        }

        /// <summary>
        /// Double-sided grid sheet. The side facing <paramref name="normal"/>
        /// takes <c>color(i, k, true)</c>, the other side
        /// <c>color(i, k, false)</c>. The two layers are separated by
        /// <paramref name="thickness"/> so they do not z-fight.
        /// </summary>
        internal static void AddDoubleGrid(
            FlockMeshBuffer buffer,
            int rows,
            int columns,
            Func<int, int, Vector3> position,
            Vector3 normal,
            Func<int, int, bool, Color> color,
            Func<int, int, Vector4> body,
            float thickness)
        {
            if (rows < 2 || columns < 2)
            {
                return;
            }

            for (int layer = 0; layer < 2; layer++)
            {
                bool top = layer == 0;
                Vector3 n = top ? normal : -normal;
                Vector3 shift = n * (thickness * 0.5f);
                int first = buffer.VertexCount;

                for (int i = 0; i < rows; i++)
                {
                    for (int k = 0; k < columns; k++)
                    {
                        buffer.AddVertex(position(i, k) + shift, n, color(i, k, top), body(i, k));
                    }
                }

                for (int i = 0; i + 1 < rows; i++)
                {
                    for (int k = 0; k + 1 < columns; k++)
                    {
                        int a = first + i * columns + k;
                        int b = first + (i + 1) * columns + k;
                        int c = first + (i + 1) * columns + k + 1;
                        int d = first + i * columns + k + 1;
                        AddFacing(buffer, a, b, c, n);
                        AddFacing(buffer, a, c, d, n);
                    }
                }
            }
        }

        /// <summary>
        /// Double-sided polygon, triangulated as a fan from
        /// <paramref name="centre"/>. The outline must be star-shaped around the
        /// centre, which holds for every fin and tail outline in this package.
        /// </summary>
        internal static void AddDoubleFan(
            FlockMeshBuffer buffer,
            Vector3 centre,
            IList<Vector3> outline,
            Vector3 normal,
            Color topColor,
            Color bottomColor,
            Func<Vector3, Vector4> body,
            float thickness)
        {
            for (int layer = 0; layer < 2; layer++)
            {
                bool top = layer == 0;
                Vector3 n = top ? normal : -normal;
                Vector3 shift = n * (thickness * 0.5f);
                Color c = top ? topColor : bottomColor;

                int hub = buffer.AddVertex(centre + shift, n, c, body(centre));
                int first = buffer.VertexCount;
                for (int i = 0; i < outline.Count; i++)
                {
                    buffer.AddVertex(outline[i] + shift, n, c, body(outline[i]));
                }

                for (int i = 0; i + 1 < outline.Count; i++)
                {
                    AddFacing(buffer, hub, first + i, first + i + 1, n);
                }
            }
        }

        /// <summary>
        /// Adds a triangle wound so that its face normal points along
        /// <paramref name="facing"/>. Triangles collapsed to zero area, where a
        /// sheet narrows to a point, are dropped.
        /// </summary>
        private static void AddFacing(FlockMeshBuffer buffer, int a, int b, int c, Vector3 facing)
        {
            Vector3 pa = buffer.Positions[a];
            Vector3 face = Vector3.Cross(buffer.Positions[b] - pa, buffer.Positions[c] - pa);
            if (face.sqrMagnitude <= 0f)
            {
                return;
            }

            if (Vector3.Dot(face, facing) >= 0f)
            {
                buffer.AddTriangle(a, b, c);
            }
            else
            {
                buffer.AddTriangle(a, c, b);
            }
        }

        /// <summary>Evenly spaced ring angles, starting at the top.</summary>
        internal static List<float> RingAngles(int sides)
        {
            var angles = new List<float>(sides);
            for (int j = 0; j < sides; j++)
            {
                angles.Add(FlockMotion.TwoPi * j / sides);
            }

            return angles;
        }

        /// <summary>
        /// Adds a pair of ring angles on each side of the height
        /// <paramref name="v"/> (cosine of the angle), mirrored left and right.
        /// </summary>
        internal static void AddHeightBoundary(List<float> angles, float v)
        {
            float clamped = Mathf.Clamp(v, -0.98f, 0.98f);
            float phi = Mathf.Acos(clamped);
            float gap = BoundaryGap * 4f;
            angles.Add(phi - gap);
            angles.Add(phi + gap);
            angles.Add(FlockMotion.TwoPi - phi - gap);
            angles.Add(FlockMotion.TwoPi - phi + gap);
        }

        /// <summary>Adds a pair of values straddling <paramref name="boundary"/> when it lies inside (0, 1).</summary>
        internal static void AddBoundary(List<float> values, float boundary)
        {
            if (boundary <= BoundaryGap || boundary >= 1f - BoundaryGap)
            {
                return;
            }

            values.Add(boundary - BoundaryGap);
            values.Add(boundary + BoundaryGap);
        }

        /// <summary>Sorts in place and removes values closer than a tenth of the boundary gap.</summary>
        internal static void SortUnique(List<float> values)
        {
            values.Sort();
            for (int i = values.Count - 1; i > 0; i--)
            {
                if (values[i] - values[i - 1] < BoundaryGap * 0.1f)
                {
                    values.RemoveAt(i);
                }
            }
        }

        internal static List<float> Uniform(int segments)
        {
            var values = new List<float>(segments + 1);
            for (int i = 0; i <= segments; i++)
            {
                values.Add((float)i / segments);
            }

            return values;
        }

        internal static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        internal static Color WithSheen(Color c, float sheen)
        {
            return new Color(c.r, c.g, c.b, sheen);
        }
    }
}
