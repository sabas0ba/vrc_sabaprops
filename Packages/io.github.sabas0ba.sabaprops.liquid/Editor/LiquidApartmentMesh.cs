using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Liquid.Editors
{
    /// <summary>Metric UVs and rounded edges for the apartment's manufactured surfaces.</summary>
    public static class LiquidApartmentMesh
    {
        private const string Folder = LiquidAssets.MaterialFolder + "/Apartment/Meshes";

        public static void RoundBox(GameObject target, float radius)
        {
            Vector3 size = target.transform.localScale;
            radius = Mathf.Min(radius, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.45f);
            string key = Mathf.RoundToInt(size.x * 1000) + "_" + Mathf.RoundToInt(size.y * 1000) + "_"
                + Mathf.RoundToInt(size.z * 1000) + "_" + Mathf.RoundToInt(radius * 1000);
            LiquidAssets.EnsureFolder(Folder);
            string path = Folder + "/Box_" + key + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = Build(size, radius);
                AssetDatabase.CreateAsset(mesh, path);
            }
            target.GetComponent<MeshFilter>().sharedMesh = mesh;
            var collider = target.GetComponent<BoxCollider>();
            if (collider != null) collider.size = size;
            target.transform.localScale = Vector3.one;
        }

        public static Mesh Build(Vector3 size, float radius)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            Vector3 half = size * 0.5f;
            Vector3 inner = half - Vector3.one * radius;
            Vector3[] directions = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (Vector3 normal in directions)
            {
                Vector3 axisU = Mathf.Abs(normal.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 axisV = Vector3.Cross(normal, axisU);
                float extentU = Vector3.Dot(Abs(axisU), half);
                float extentV = Vector3.Dot(Abs(axisV), half);
                float extentN = Vector3.Dot(Abs(normal), half);
                int start = vertices.Count;
                for (int y = 0; y < 6; y++)
                    for (int x = 0; x < 6; x++)
                    {
                        float u = Coordinate(x, extentU, radius), v = Coordinate(y, extentV, radius);
                        Vector3 point = normal * extentN + axisU * u + axisV * v;
                        Vector3 centre = new Vector3(Mathf.Clamp(point.x, -inner.x, inner.x),
                            Mathf.Clamp(point.y, -inner.y, inner.y), Mathf.Clamp(point.z, -inner.z, inner.z));
                        Vector3 roundedNormal = (point - centre).normalized;
                        vertices.Add(centre + roundedNormal * radius);
                        normals.Add(roundedNormal);
                        uv.Add(new Vector2(u, v));
                    }
                for (int y = 0; y < 5; y++)
                    for (int x = 0; x < 5; x++)
                    {
                        int a = start + y * 6 + x;
                        triangles.Add(a); triangles.Add(a + 1); triangles.Add(a + 7);
                        triangles.Add(a); triangles.Add(a + 7); triangles.Add(a + 6);
                    }
            }
            var mesh = new Mesh { name = "Rounded metric box" };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            return mesh;
        }

        public static void Drape(GameObject target, Vector3 size)
        {
            string path = Folder + "/Curtain.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                const int columns = 48;
                var vertices = new List<Vector3>();
                var uv = new List<Vector2>();
                var triangles = new List<int>();
                for (int side = 0; side < 2; side++)
                {
                    int start = vertices.Count;
                    for (int x = 0; x <= columns; x++)
                    {
                        float u = x / (float)columns;
                        float z = Mathf.Cos(u * Mathf.PI * 12) * size.z * 0.5f;
                        vertices.Add(new Vector3((u - 0.5f) * size.x, -size.y * 0.5f + 0.012f * Mathf.Sin(u * 25), z));
                        vertices.Add(new Vector3((u - 0.5f) * size.x, size.y * 0.5f, z * 0.7f));
                        uv.Add(new Vector2(u * size.x, 0)); uv.Add(new Vector2(u * size.x, size.y));
                    }
                    for (int x = 0; x < columns; x++)
                    {
                        int a = start + x * 2;
                        int[] face = side == 0 ? new[] { a, a + 1, a + 2, a + 2, a + 1, a + 3 }
                            : new[] { a, a + 2, a + 1, a + 2, a + 3, a + 1 };
                        triangles.AddRange(face);
                    }
                }
                mesh = new Mesh { name = "Pleated curtain" };
                mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, path);
            }
            target.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        private static float Coordinate(int index, float half, float radius)
        {
            if (index == 0) return -half;
            if (index == 1) return -half + radius * 0.293f;
            if (index == 2) return -half + radius;
            if (index == 3) return half - radius;
            if (index == 4) return half - radius * 0.293f;
            return half;
        }

        private static Vector3 Abs(Vector3 v) { return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z)); }
    }
}
