using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SabaProps.SoftProps.Editors
{
    /// <summary>
    /// 変形面用のsubdivided rounded boxを決定的に生成する。
    /// 上面のCOLOR.rだけを可動maskとし、sideとの境界に亀裂を作らない。
    /// </summary>
    internal static class SoftPropMeshBuilder
    {
        public static Mesh BuildRoundedBox(
            string meshName,
            Vector3 size,
            float cornerRadius,
            int xSegments,
            int zSegments)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uv = new List<Vector2>();
            var colors = new List<Color>();
            var triangles = new List<int>();

            Vector3 half = size * 0.5f;
            float radius = Mathf.Clamp(
                cornerRadius,
                0.001f,
                Mathf.Min(half.x, Mathf.Min(half.y, half.z)) * 0.98f);

            int sideY = Mathf.Clamp(Mathf.RoundToInt(size.y / 0.025f), 3, 10);
            int bottomX = Mathf.Max(4, xSegments / 4);
            int bottomZ = Mathf.Max(4, zSegments / 4);

            AddFace(vertices, normals, uv, colors, triangles, half, radius,
                Vector3.up, Vector3.right, Vector3.forward, xSegments, zSegments, true);
            AddFace(vertices, normals, uv, colors, triangles, half, radius,
                Vector3.down, Vector3.right, Vector3.back, bottomX, bottomZ, false);
            AddFace(vertices, normals, uv, colors, triangles, half, radius,
                Vector3.forward, Vector3.right, Vector3.down, xSegments, sideY, false);
            AddFace(vertices, normals, uv, colors, triangles, half, radius,
                Vector3.back, Vector3.left, Vector3.down, xSegments, sideY, false);
            AddFace(vertices, normals, uv, colors, triangles, half, radius,
                Vector3.right, Vector3.back, Vector3.down, zSegments, sideY, false);
            AddFace(vertices, normals, uv, colors, triangles, half, radius,
                Vector3.left, Vector3.forward, Vector3.down, zSegments, sideY, false);

            var mesh = new Mesh
            {
                name = meshName,
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16,
            };

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddFace(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<Color> colors,
            List<int> triangles,
            Vector3 half,
            float radius,
            Vector3 faceNormal,
            Vector3 axisU,
            Vector3 axisV,
            int segmentsU,
            int segmentsV,
            bool deformable)
        {
            int baseIndex = vertices.Count;
            Vector3 center = Vector3.Scale(faceNormal, half);
            float extentU = Vector3.Dot(Abs(axisU), half);
            float extentV = Vector3.Dot(Abs(axisV), half);

            for (int v = 0; v <= segmentsV; v++)
            {
                float tv = v / (float)segmentsV;
                for (int u = 0; u <= segmentsU; u++)
                {
                    float tu = u / (float)segmentsU;
                    Vector3 raw = center
                        + axisU * Mathf.Lerp(-extentU, extentU, tu)
                        + axisV * Mathf.Lerp(-extentV, extentV, tv);

                    Vector3 rounded = RoundedPoint(raw, half, radius, faceNormal, out Vector3 roundedNormal);
                    float softness = deformable ? TopSoftness(raw, half, radius) : 0f;

                    vertices.Add(rounded);
                    normals.Add(roundedNormal);
                    uv.Add(new Vector2(tu, tv));
                    colors.Add(new Color(softness, Mathf.Lerp(0.84f, 1f, softness), 1f, 1f));
                }
            }

            bool forwardWinding = Vector3.Dot(Vector3.Cross(axisU, axisV), faceNormal) > 0f;
            int stride = segmentsU + 1;

            for (int v = 0; v < segmentsV; v++)
            {
                for (int u = 0; u < segmentsU; u++)
                {
                    int a = baseIndex + v * stride + u;
                    int b = a + 1;
                    int c = a + stride;
                    int d = c + 1;

                    if (forwardWinding)
                    {
                        triangles.Add(a);
                        triangles.Add(b);
                        triangles.Add(c);
                        triangles.Add(b);
                        triangles.Add(d);
                        triangles.Add(c);
                    }
                    else
                    {
                        triangles.Add(a);
                        triangles.Add(c);
                        triangles.Add(b);
                        triangles.Add(c);
                        triangles.Add(d);
                        triangles.Add(b);
                    }
                }
            }
        }

        private static Vector3 RoundedPoint(
            Vector3 point,
            Vector3 half,
            float radius,
            Vector3 fallbackNormal,
            out Vector3 normal)
        {
            Vector3 inner = new Vector3(
                Mathf.Max(0f, half.x - radius),
                Mathf.Max(0f, half.y - radius),
                Mathf.Max(0f, half.z - radius));

            Vector3 closest = new Vector3(
                Mathf.Clamp(point.x, -inner.x, inner.x),
                Mathf.Clamp(point.y, -inner.y, inner.y),
                Mathf.Clamp(point.z, -inner.z, inner.z));

            Vector3 delta = point - closest;
            if (delta.sqrMagnitude < 0.0000001f)
            {
                normal = fallbackNormal;
                return point;
            }

            normal = delta.normalized;
            return closest + normal * radius;
        }

        private static float TopSoftness(Vector3 point, Vector3 half, float radius)
        {
            float edgeDistance = Mathf.Min(half.x - Mathf.Abs(point.x), half.z - Mathf.Abs(point.z));
            float t = Mathf.Clamp01(edgeDistance / Mathf.Max(radius * 1.35f, 0.001f));
            return t * t * (3f - 2f * t);
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }
    }
}
