using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SabaProps.Trees.Editors
{
    internal sealed class TreeMeshBuffer
    {
        public readonly List<Vector3> Positions = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<Color> Colors = new List<Color>();
        public readonly List<Vector2> Uv0 = new List<Vector2>();
        public readonly List<Vector4> Uv3 = new List<Vector4>();
        public readonly List<int> Triangles = new List<int>();

        public int VertexCount => Positions.Count;
        public int TriangleCount => Triangles.Count / 3;

        public int AddVertex(
            Vector3 position, Vector3 normal, Color color,
            Vector2 uv0, Vector3 windRoot, float stiffness)
        {
            Positions.Add(position);
            Normals.Add(normal);
            Colors.Add(color);
            Uv0.Add(uv0);
            Uv3.Add(new Vector4(windRoot.x, windRoot.y, windRoot.z, stiffness));
            return Positions.Count - 1;
        }

        public void AddTriangle(int a, int b, int c)
        {
            Triangles.Add(a);
            Triangles.Add(b);
            Triangles.Add(c);
        }

        public void AddQuad(int a, int b, int c, int d)
        {
            AddTriangle(a, b, c);
            AddTriangle(a, c, d);
        }

        public Mesh ToMesh(string name, float boundsPadding)
        {
            var mesh = new Mesh { name = name };
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
}
