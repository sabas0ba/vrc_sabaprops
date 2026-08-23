using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SabaProps.Foliage.Editors
{
    /// <summary>
    /// Growable vertex/index arrays used while building procedural foliage
    /// meshes and while welding instances into merged chunks.
    /// </summary>
    internal sealed class FoliageMeshBuffer
    {
        public readonly List<Vector3> Positions = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<Color> Colors = new List<Color>();
        public readonly List<Vector2> Uv0 = new List<Vector2>();

        /// <summary>xyz = element root in object space, w = wind stiffness.</summary>
        public readonly List<Vector4> Uv3 = new List<Vector4>();

        public readonly List<int> Triangles = new List<int>();

        public int VertexCount
        {
            get { return Positions.Count; }
        }

        public int TriangleCount
        {
            get { return Triangles.Count / 3; }
        }

        public int AddVertex(Vector3 position, Vector3 normal, Color color, Vector2 uv0, Vector4 uv3)
        {
            Positions.Add(position);
            Normals.Add(normal);
            Colors.Add(color);
            Uv0.Add(uv0);
            Uv3.Add(uv3);
            return Positions.Count - 1;
        }

        public void AddTriangle(int a, int b, int c)
        {
            Triangles.Add(a);
            Triangles.Add(b);
            Triangles.Add(c);
        }

        /// <summary>Adds two triangles for a quad given in loop order.</summary>
        public void AddQuad(int a, int b, int c, int d)
        {
            AddTriangle(a, b, c);
            AddTriangle(a, c, d);
        }

        /// <summary>
        /// Appends a transformed copy of a source mesh. Used by the merge path,
        /// where the element roots stored in UV3 have to follow the instance
        /// transform so the shader still finds the correct sway pivot.
        /// </summary>
        public void Append(FoliageSourceMesh source, Matrix4x4 trs)
        {
            int offset = Positions.Count;
            Matrix4x4 normalMatrix = trs.inverse.transpose;

            for (int i = 0; i < source.Positions.Length; i++)
            {
                Positions.Add(trs.MultiplyPoint3x4(source.Positions[i]));
                Normals.Add(normalMatrix.MultiplyVector(source.Normals[i]).normalized);
                Colors.Add(source.Colors[i]);
                Uv0.Add(source.Uv0[i]);

                Vector4 uv3 = source.Uv3[i];
                Vector3 root = trs.MultiplyPoint3x4(new Vector3(uv3.x, uv3.y, uv3.z));
                Uv3.Add(new Vector4(root.x, root.y, root.z, uv3.w));
            }

            for (int i = 0; i < source.Triangles.Length; i++)
            {
                Triangles.Add(source.Triangles[i] + offset);
            }
        }

        /// <summary>
        /// Bakes the buffer into a mesh.
        /// </summary>
        /// <param name="meshName">Asset name for the resulting mesh.</param>
        /// <param name="boundsPadding">
        /// Extra margin added to the bounds. Wind displaces vertices in the
        /// vertex shader, which Unity's culling knows nothing about; without
        /// padding, foliage pops at the edge of the frustum.
        /// </param>
        public Mesh ToMesh(string meshName, float boundsPadding)
        {
            var mesh = new Mesh { name = meshName };

            if (Positions.Count > 65535)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.SetVertices(Positions);
            mesh.SetNormals(Normals);
            mesh.SetColors(Colors);
            mesh.SetUVs(0, Uv0);
            mesh.SetUVs(3, Uv3);
            mesh.SetTriangles(Triangles, 0, true);

            if (boundsPadding > 0f)
            {
                Bounds bounds = mesh.bounds;
                bounds.Expand(boundsPadding * 2f);
                mesh.bounds = bounds;
            }

            mesh.UploadMeshData(false);
            return mesh;
        }
    }

    /// <summary>
    /// Cached, array-form snapshot of a species mesh. Reading
    /// <c>Mesh.vertices</c> allocates a fresh array on every call, so the merge
    /// path pulls the data out exactly once.
    /// </summary>
    internal sealed class FoliageSourceMesh
    {
        public Vector3[] Positions;
        public Vector3[] Normals;
        public Color[] Colors;
        public Vector2[] Uv0;
        public Vector4[] Uv3;
        public int[] Triangles;

        public static FoliageSourceMesh From(Mesh mesh)
        {
            var uv0 = new List<Vector2>();
            var uv3 = new List<Vector4>();
            mesh.GetUVs(0, uv0);
            mesh.GetUVs(3, uv3);

            int vertexCount = mesh.vertexCount;

            // A mesh authored outside this package may not carry our channels.
            // Fall back to neutral values rather than throwing.
            if (uv0.Count != vertexCount)
            {
                uv0.Clear();
                for (int i = 0; i < vertexCount; i++)
                {
                    uv0.Add(Vector2.zero);
                }
            }

            if (uv3.Count != vertexCount)
            {
                uv3.Clear();
                for (int i = 0; i < vertexCount; i++)
                {
                    uv3.Add(new Vector4(0f, 0f, 0f, 1f));
                }
            }

            Color[] colors = mesh.colors;
            if (colors == null || colors.Length != vertexCount)
            {
                colors = new Color[vertexCount];
                for (int i = 0; i < vertexCount; i++)
                {
                    colors[i] = Color.white;
                }
            }

            Vector3[] normals = mesh.normals;
            if (normals == null || normals.Length != vertexCount)
            {
                mesh.RecalculateNormals();
                normals = mesh.normals;
            }

            return new FoliageSourceMesh
            {
                Positions = mesh.vertices,
                Normals = normals,
                Colors = colors,
                Uv0 = uv0.ToArray(),
                Uv3 = uv3.ToArray(),
                Triangles = mesh.triangles,
            };
        }
    }
}
