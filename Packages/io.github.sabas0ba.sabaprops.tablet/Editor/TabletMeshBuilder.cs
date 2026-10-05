using System.Collections.Generic;
using UnityEngine;

namespace SabaProps.Tablet.Editors
{
    /// <summary>
    /// 角丸の直方体と角丸の板を生成します。本体、画面、ボタンのキャップ、取っ手に使います。
    /// <para>
    /// 前面は local -Z を向きます。UnityEditor を参照しないため、
    /// .github/verify/offline で Unity なしに実行して形状を検査します。
    /// </para>
    /// </summary>
    public static class TabletMeshBuilder
    {
        /// <summary>生成結果。Unity の Mesh へ渡す前の配列です。</summary>
        public sealed class MeshData
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector3> normals = new List<Vector3>();
            public readonly List<Vector2> uvs = new List<Vector2>();
            public readonly List<int> triangles = new List<int>();
        }

        /// <summary>
        /// 角丸長方形の外周。中心が原点で、反時計回り (+x から +y へ回る向き) に並びます。
        /// 半径は短辺の半分までに制限し、半径 0 では 4 頂点の長方形になります。
        /// </summary>
        public static List<Vector2> Outline(float width, float height, float radius, int segments)
        {
            float r = Mathf.Clamp(radius, 0f, Mathf.Min(width, height) * 0.5f);
            int steps = r <= 1e-6f ? 0 : Mathf.Max(1, segments);
            float hx = width * 0.5f - r;
            float hy = height * 0.5f - r;

            var centers = new[]
            {
                new Vector2(hx, -hy),
                new Vector2(hx, hy),
                new Vector2(-hx, hy),
                new Vector2(-hx, -hy),
            };

            var points = new List<Vector2>();
            for (int corner = 0; corner < 4; corner++)
            {
                float start = -90f + corner * 90f;
                for (int i = 0; i <= steps; i++)
                {
                    float angle = (start + (steps == 0 ? 45f : 90f * i / steps)) * Mathf.Deg2Rad;
                    Vector2 c = centers[corner];
                    if (steps == 0)
                    {
                        points.Add(c);
                        break;
                    }

                    points.Add(new Vector2(c.x + Mathf.Cos(angle) * r, c.y + Mathf.Sin(angle) * r));
                }
            }

            return points;
        }

        /// <summary>角丸長方形を depth だけ押し出した閉じた立体。中心が原点です。</summary>
        public static MeshData RoundedBox(float width, float height, float depth, float radius, int segments)
        {
            var data = new MeshData();
            List<Vector2> ring = Outline(width, height, radius, segments);
            float front = -depth * 0.5f;
            float back = depth * 0.5f;

            AddCap(data, ring, width, height, front, false);
            AddCap(data, ring, width, height, back, true);
            AddSides(data, ring, width, height, front, back, radius);
            return data;
        }

        /// <summary>前面 (-Z 向き) だけの角丸の板。z = 0 に置きます。</summary>
        public static MeshData RoundedPanel(float width, float height, float radius, int segments)
        {
            var data = new MeshData();
            AddCap(data, Outline(width, height, radius, segments), width, height, 0f, false);
            return data;
        }

        private static void AddCap(MeshData data, List<Vector2> ring, float width, float height, float z, bool back)
        {
            Vector3 normal = back ? Vector3.forward : -Vector3.forward;
            int center = data.vertices.Count;
            data.vertices.Add(new Vector3(0f, 0f, z));
            data.normals.Add(normal);
            data.uvs.Add(new Vector2(0.5f, 0.5f));

            for (int i = 0; i < ring.Count; i++)
            {
                Vector2 p = ring[i];
                data.vertices.Add(new Vector3(p.x, p.y, z));
                data.normals.Add(normal);
                data.uvs.Add(new Vector2(
                    width > 0f ? p.x / width + 0.5f : 0.5f,
                    height > 0f ? p.y / height + 0.5f : 0.5f));
            }

            // Unity は視点から見て時計回りの面を表とします。外周は反時計回りなので、
            // -Z から見る前面は順序を反転し、+Z から見る背面は左右が反転するためそのままにします。
            for (int i = 0; i < ring.Count; i++)
            {
                int a = center + 1 + i;
                int b = center + 1 + (i + 1) % ring.Count;
                data.triangles.Add(center);
                data.triangles.Add(back ? a : b);
                data.triangles.Add(back ? b : a);
            }
        }

        private static void AddSides(MeshData data, List<Vector2> ring, float width, float height, float front, float back, float radius)
        {
            float r = Mathf.Clamp(radius, 0f, Mathf.Min(width, height) * 0.5f);
            float hx = width * 0.5f - r;
            float hy = height * 0.5f - r;
            float perimeter = 0f;
            float total = 0f;
            for (int i = 0; i < ring.Count; i++)
            {
                total += (ring[(i + 1) % ring.Count] - ring[i]).magnitude;
            }

            for (int i = 0; i < ring.Count; i++)
            {
                Vector2 p = ring[i];
                Vector2 q = ring[(i + 1) % ring.Count];
                float length = (q - p).magnitude;
                if (length <= 1e-7f)
                {
                    continue;
                }

                // 角の円弧では円の中心からの向き、直線部では辺に垂直な向きを法線にします。
                Vector3 np = SideNormal(p, q, hx, hy, r, true);
                Vector3 nq = SideNormal(p, q, hx, hy, r, false);
                float u0 = total > 0f ? perimeter / total : 0f;
                perimeter += length;
                float u1 = total > 0f ? perimeter / total : 0f;

                int start = data.vertices.Count;
                data.vertices.Add(new Vector3(p.x, p.y, front));
                data.vertices.Add(new Vector3(q.x, q.y, front));
                data.vertices.Add(new Vector3(q.x, q.y, back));
                data.vertices.Add(new Vector3(p.x, p.y, back));
                data.normals.Add(np);
                data.normals.Add(nq);
                data.normals.Add(nq);
                data.normals.Add(np);
                data.uvs.Add(new Vector2(u0, 0f));
                data.uvs.Add(new Vector2(u1, 0f));
                data.uvs.Add(new Vector2(u1, 1f));
                data.uvs.Add(new Vector2(u0, 1f));

                data.triangles.Add(start);
                data.triangles.Add(start + 1);
                data.triangles.Add(start + 3);
                data.triangles.Add(start + 1);
                data.triangles.Add(start + 2);
                data.triangles.Add(start + 3);
            }
        }

        private static Vector3 SideNormal(Vector2 p, Vector2 q, float hx, float hy, float r, bool first)
        {
            Vector2 point = first ? p : q;
            Vector2 edge = q - p;
            // 外周は反時計回りなので、辺の右側 (dy, -dx) が外向きです。
            var flat = new Vector3(edge.y, -edge.x, 0f).normalized;
            if (r <= 1e-6f)
            {
                return flat;
            }

            // 最寄りの円弧中心からの向き。円弧の端点は直線部の端点でもあり、そこでは辺の法線と一致するため、
            // 直線部の側面は平らに、円弧の側面は滑らかに陰影が付きます。
            var center = new Vector2(Mathf.Clamp(point.x, -hx, hx), Mathf.Clamp(point.y, -hy, hy));
            Vector2 radial = point - center;
            if (radial.sqrMagnitude <= 1e-12f)
            {
                return flat;
            }

            return new Vector3(radial.x, radial.y, 0f).normalized;
        }

        /// <summary>生成結果を Unity の Mesh に変換します。</summary>
        public static Mesh ToMesh(MeshData data, string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(data.vertices);
            mesh.SetNormals(data.normals);
            mesh.SetUVs(0, data.uvs);
            mesh.SetTriangles(data.triangles, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
