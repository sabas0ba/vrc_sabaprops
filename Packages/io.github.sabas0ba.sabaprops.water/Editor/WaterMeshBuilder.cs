using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SabaProps.Water.Editors
{
    /// <summary>Deterministic geometry used by water authoring tools.</summary>
    public static class WaterMeshBuilder
    {
        public static Mesh BuildGrid(float width, float length, int xSegments, int zSegments)
        {
            width = Mathf.Max(0.01f, width);
            length = Mathf.Max(0.01f, length);
            xSegments = Mathf.Clamp(xSegments, 1, 256);
            zSegments = Mathf.Clamp(zSegments, 1, 256);

            int columns = xSegments + 1;
            int rows = zSegments + 1;
            var vertices = new Vector3[columns * rows];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];

            for (int z = 0; z < rows; z++)
            {
                float v = z / (float)zSegments;
                for (int x = 0; x < columns; x++)
                {
                    float u = x / (float)xSegments;
                    int index = z * columns + x;
                    vertices[index] = new Vector3((u - 0.5f) * width, 0f, (v - 0.5f) * length);
                    normals[index] = Vector3.up;
                    uv[index] = new Vector2(u, v);
                }
            }

            var triangles = new int[xSegments * zSegments * 6];
            int triangleIndex = 0;
            for (int z = 0; z < zSegments; z++)
            {
                for (int x = 0; x < xSegments; x++)
                {
                    int current = z * columns + x;
                    int nextRow = current + columns;
                    triangles[triangleIndex++] = current;
                    triangles[triangleIndex++] = nextRow;
                    triangles[triangleIndex++] = current + 1;
                    triangles[triangleIndex++] = current + 1;
                    triangles[triangleIndex++] = nextRow;
                    triangles[triangleIndex++] = nextRow + 1;
                }
            }

            return CreateMesh("SabaWater_Grid", vertices, normals, uv, triangles);
        }

        public static Mesh BuildPuddle(
            float radius,
            float aspect,
            int rings,
            int radialSegments,
            int seed,
            float irregularity = 0.18f)
        {
            radius = Mathf.Max(0.05f, radius);
            aspect = Mathf.Clamp(aspect, 0.2f, 5f);
            rings = Mathf.Clamp(rings, 1, 16);
            radialSegments = Mathf.Clamp(radialSegments, 8, 128);
            irregularity = Mathf.Clamp(irregularity, 0f, 0.45f);

            int vertexCount = 1 + rings * radialSegments;
            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uv = new Vector2[vertexCount];
            var triangles = new int[radialSegments * 3 + (rings - 1) * radialSegments * 6];

            vertices[0] = Vector3.zero;
            normals[0] = Vector3.up;
            uv[0] = new Vector2(0.5f, 0.5f);

            var boundary = new float[radialSegments];
            for (int segment = 0; segment < radialSegments; segment++)
            {
                float broad = SignedHash(seed, segment / 3);
                float detail = SignedHash(seed + 7919, segment);
                boundary[segment] = 1f + irregularity * (broad * 0.65f + detail * 0.35f);
            }

            for (int ring = 1; ring <= rings; ring++)
            {
                float ringFraction = ring / (float)rings;
                for (int segment = 0; segment < radialSegments; segment++)
                {
                    float angle = segment / (float)radialSegments * Mathf.PI * 2f;
                    float distance = radius * ringFraction * Mathf.Lerp(1f, boundary[segment], ringFraction);
                    float x = Mathf.Cos(angle) * distance * aspect;
                    float z = Mathf.Sin(angle) * distance;
                    int index = 1 + (ring - 1) * radialSegments + segment;
                    vertices[index] = new Vector3(x, 0f, z);
                    normals[index] = Vector3.up;
                    uv[index] = new Vector2(
                        x / (radius * aspect * 2f) + 0.5f,
                        z / (radius * 2f) + 0.5f);
                }
            }

            int triangleIndex = 0;
            for (int segment = 0; segment < radialSegments; segment++)
            {
                int current = 1 + segment;
                int next = 1 + (segment + 1) % radialSegments;
                triangles[triangleIndex++] = 0;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = current;
            }

            for (int ring = 2; ring <= rings; ring++)
            {
                int innerStart = 1 + (ring - 2) * radialSegments;
                int outerStart = 1 + (ring - 1) * radialSegments;
                for (int segment = 0; segment < radialSegments; segment++)
                {
                    int nextSegment = (segment + 1) % radialSegments;
                    int innerCurrent = innerStart + segment;
                    int innerNext = innerStart + nextSegment;
                    int outerCurrent = outerStart + segment;
                    int outerNext = outerStart + nextSegment;

                    triangles[triangleIndex++] = innerCurrent;
                    triangles[triangleIndex++] = outerNext;
                    triangles[triangleIndex++] = outerCurrent;
                    triangles[triangleIndex++] = innerCurrent;
                    triangles[triangleIndex++] = innerNext;
                    triangles[triangleIndex++] = outerNext;
                }
            }

            return CreateMesh("SabaWater_Puddle", vertices, normals, uv, triangles);
        }

        public static Mesh BuildRiver(
            IReadOnlyList<Vector3> controlPoints,
            float width,
            int subdivisions,
            float uvMetersPerTile)
        {
            if (controlPoints == null || controlPoints.Count < 2)
            {
                return null;
            }

            width = Mathf.Max(0.05f, width);
            subdivisions = Mathf.Clamp(subdivisions, 1, 16);
            uvMetersPerTile = Mathf.Max(0.01f, uvMetersPerTile);

            int spans = controlPoints.Count - 1;
            int samples = spans * subdivisions + 1;
            var vertices = new Vector3[samples * 2];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[(samples - 1) * 6];

            Vector3 previousCentre = Vector3.zero;
            float distance = 0f;

            for (int sample = 0; sample < samples; sample++)
            {
                int span = Mathf.Min(sample / subdivisions, spans - 1);
                float t = sample == samples - 1 ? 1f : (sample % subdivisions) / (float)subdivisions;

                Vector3 p0 = controlPoints[Mathf.Max(0, span - 1)];
                Vector3 p1 = controlPoints[span];
                Vector3 p2 = controlPoints[span + 1];
                Vector3 p3 = controlPoints[Mathf.Min(controlPoints.Count - 1, span + 2)];

                Vector3 centre = CatmullRom(p0, p1, p2, p3, t);
                Vector3 tangent = CatmullRomTangent(p0, p1, p2, p3, t);
                tangent.y = 0f;
                if (tangent.sqrMagnitude < 1e-6f)
                {
                    tangent = Vector3.forward;
                }

                tangent.Normalize();
                Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;

                if (sample > 0)
                {
                    distance += Vector3.Distance(previousCentre, centre);
                }

                previousCentre = centre;
                int left = sample * 2;
                int right = left + 1;
                vertices[left] = centre - side * (width * 0.5f);
                vertices[right] = centre + side * (width * 0.5f);
                normals[left] = Vector3.up;
                normals[right] = Vector3.up;
                uv[left] = new Vector2(0f, distance / uvMetersPerTile);
                uv[right] = new Vector2(1f, distance / uvMetersPerTile);
            }

            int triangleIndex = 0;
            for (int sample = 0; sample < samples - 1; sample++)
            {
                int left = sample * 2;
                int right = left + 1;
                int nextLeft = left + 2;
                int nextRight = left + 3;
                triangles[triangleIndex++] = left;
                triangles[triangleIndex++] = nextLeft;
                triangles[triangleIndex++] = right;
                triangles[triangleIndex++] = right;
                triangles[triangleIndex++] = nextLeft;
                triangles[triangleIndex++] = nextRight;
            }

            return CreateMesh("SabaWater_River", vertices, normals, uv, triangles);
        }

        public static Mesh BuildHorizontalQuad()
        {
            var vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f),
            };
            var normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            var uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
            };
            var triangles = new[] { 0, 2, 1, 1, 2, 3 };
            return CreateMesh("SabaWater_HorizontalQuad", vertices, normals, uv, triangles);
        }

        public static Mesh BuildLightShaft(float height, float topWidth, float bottomWidth)
        {
            height = Mathf.Max(0.1f, height);
            topWidth = Mathf.Max(0.01f, topWidth);
            bottomWidth = Mathf.Max(topWidth, bottomWidth);

            var vertices = new List<Vector3>(8);
            var normals = new List<Vector3>(8);
            var uv = new List<Vector2>(8);
            var triangles = new List<int>(12);

            AddShaftPlane(vertices, normals, uv, triangles, Vector3.right, Vector3.forward, height, topWidth, bottomWidth);
            AddShaftPlane(vertices, normals, uv, triangles, Vector3.forward, Vector3.right, height, topWidth, bottomWidth);
            return CreateMesh(
                "SabaWater_LightShaft",
                vertices.ToArray(),
                normals.ToArray(),
                uv.ToArray(),
                triangles.ToArray());
        }

        private static void AddShaftPlane(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uv,
            List<int> triangles,
            Vector3 side,
            Vector3 normal,
            float height,
            float topWidth,
            float bottomWidth)
        {
            int start = vertices.Count;
            vertices.Add(side * (-topWidth * 0.5f));
            vertices.Add(side * (topWidth * 0.5f));
            vertices.Add(Vector3.down * height + side * (-bottomWidth * 0.5f));
            vertices.Add(Vector3.down * height + side * (bottomWidth * 0.5f));
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            uv.Add(new Vector2(0f, 0f));
            uv.Add(new Vector2(1f, 0f));
            uv.Add(new Vector2(0f, 1f));
            uv.Add(new Vector2(1f, 1f));
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 1);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private static Vector3 CatmullRom(
            Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                2f * p1 +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static Vector3 CatmullRomTangent(
            Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            return 0.5f * (
                (-p0 + p2) +
                2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * t +
                3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t2);
        }

        private static float SignedHash(int seed, int index)
        {
            unchecked
            {
                uint value = (uint)seed;
                value ^= (uint)index * 0x9E3779B9u;
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return (value / (float)uint.MaxValue) * 2f - 1f;
            }
        }

        private static Mesh CreateMesh(
            string name,
            Vector3[] vertices,
            Vector3[] normals,
            Vector2[] uv,
            int[] triangles)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Length > ushort.MaxValue)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
