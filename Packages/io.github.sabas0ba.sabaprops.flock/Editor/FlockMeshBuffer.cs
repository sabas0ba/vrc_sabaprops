using System.Collections.Generic;
using UnityEngine;

namespace SabaProps.Flock.Editors
{
    /// <summary>
    /// Vertex and index lists of one individual in body-local space. The swarm
    /// builder copies it once per individual and adds the per-individual and
    /// per-swarm channels.
    /// </summary>
    internal sealed class FlockMeshBuffer
    {
        public readonly List<Vector3> Positions = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<Color> Colors = new List<Color>();

        /// <summary>See <see cref="FlockShaderContract"/>: span, axial, part, shoulder.</summary>
        public readonly List<Vector4> Body = new List<Vector4>();

        public readonly List<int> Triangles = new List<int>();

        public int VertexCount => Positions.Count;

        public int TriangleCount => Triangles.Count / 3;

        public int AddVertex(Vector3 position, Vector3 normal, Color color, Vector4 body)
        {
            Positions.Add(position);
            Normals.Add(normal);
            Colors.Add(color);
            Body.Add(body);
            return Positions.Count - 1;
        }

        public void AddTriangle(int a, int b, int c)
        {
            Triangles.Add(a);
            Triangles.Add(b);
            Triangles.Add(c);
        }

        /// <summary>Two triangles for a quad given in loop order.</summary>
        public void AddQuad(int a, int b, int c, int d)
        {
            AddTriangle(a, b, c);
            AddTriangle(a, c, d);
        }

        /// <summary>
        /// Replaces the normals of the vertices from <paramref name="firstVertex"/>
        /// with area-weighted face normals of the triangles from
        /// <paramref name="firstIndex"/>. Used for closed tubes, whose shared
        /// ring vertices need smooth normals.
        /// </summary>
        public void SmoothNormals(int firstVertex, int firstIndex)
        {
            int count = Positions.Count - firstVertex;
            var accumulated = new Vector3[count];

            for (int i = firstIndex; i + 2 < Triangles.Count; i += 3)
            {
                int a = Triangles[i];
                int b = Triangles[i + 1];
                int c = Triangles[i + 2];
                if (a < firstVertex || b < firstVertex || c < firstVertex)
                {
                    continue;
                }

                Vector3 face = Vector3.Cross(Positions[b] - Positions[a], Positions[c] - Positions[a]);
                accumulated[a - firstVertex] += face;
                accumulated[b - firstVertex] += face;
                accumulated[c - firstVertex] += face;
            }

            for (int i = 0; i < count; i++)
            {
                Vector3 n = accumulated[i];
                Normals[firstVertex + i] = n.sqrMagnitude > 1e-20f ? n.normalized : Vector3.up;
            }
        }
    }
}
