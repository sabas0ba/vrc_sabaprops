using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SabaProps.PutItems.Editors
{
    // 配布するメッシュはここで生成し、外部モデルやテクスチャへ依存しません。
    public static class KitchenDemoGeometry
    {
        public static Material Material(string name, Color color, float metallic = 0f, float smoothness = 0.35f)
        {
            string path = KitchenDemo.GeneratedRoot + "/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        public static Transform Group(string name, Transform parent, Vector3 position)
        {
            Transform group = new GameObject(name).transform;
            group.SetParent(parent, false);
            group.localPosition = position;
            return group;
        }

        public static GameObject Primitive(string name, PrimitiveType type, Transform parent,
            Vector3 position, Vector3 scale, Material material, bool collision = false)
        {
            GameObject obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) Object.DestroyImmediate(obj.GetComponent<Collider>());
            return obj;
        }

        public static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size,
            Material material, bool collision = false)
        {
            return Primitive(name, PrimitiveType.Cube, parent, position, size, material, collision);
        }

        public static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material material)
        {
            GameObject obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            obj.GetComponent<MeshRenderer>().sharedMaterial = material;
            return obj;
        }

        public static Mesh Lathe(string name, Vector2[] profile, int segments = 64)
        {
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            for (int row = 0; row < profile.Length; row++)
                for (int i = 0; i < segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / segments;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * profile[row].x, profile[row].y,
                        Mathf.Sin(angle) * profile[row].x));
                }
            for (int row = 0; row < profile.Length - 1; row++)
                for (int i = 0; i < segments; i++)
                {
                    int a = row * segments + i;
                    int next = row * segments + (i + 1) % segments;
                    indices.AddRange(new[] { a, a + segments, next, next, a + segments, next + segments });
                }
            return SaveMesh(name, vertices, indices);
        }

        public static Mesh Tube(string name, Vector3[] path, float radius, Vector3 guide, bool closed = false)
        {
            const int sides = 10;
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            for (int i = 0; i < path.Length; i++)
            {
                Vector3 previous = path[closed ? (i + path.Length - 1) % path.Length : Mathf.Max(0, i - 1)];
                Vector3 next = path[closed ? (i + 1) % path.Length : Mathf.Min(path.Length - 1, i + 1)];
                Vector3 tangent = (next - previous).normalized;
                Vector3 second = Vector3.Cross(tangent, guide).normalized;
                for (int j = 0; j < sides; j++)
                {
                    float angle = j * Mathf.PI * 2f / sides;
                    vertices.Add(path[i] + radius * (guide * Mathf.Cos(angle) + second * Mathf.Sin(angle)));
                }
            }
            int joins = closed ? path.Length : path.Length - 1;
            for (int i = 0; i < joins; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a = i * sides + j;
                    int b = ((i + 1) % path.Length) * sides + j;
                    int c = i * sides + (j + 1) % sides;
                    int d = ((i + 1) % path.Length) * sides + (j + 1) % sides;
                    indices.AddRange(new[] { a, c, b, c, d, b });
                }
            return SaveMesh(name, vertices, indices);
        }

        public static Mesh RoundedBlock(string name, float width, float depth, float height, float radius)
        {
            const int corners = 8;
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            float bevel = Mathf.Min(height * 0.25f, 0.012f);
            float[] ys = { 0f, bevel, height - bevel, height };
            for (int row = 0; row < 4; row++)
            {
                float inset = row == 0 || row == 3 ? bevel : 0f;
                for (int corner = 0; corner < 4; corner++)
                    for (int i = 0; i < corners; i++)
                    {
                        float angle = (corner * 90f + i * 90f / (corners - 1)) * Mathf.Deg2Rad;
                        float x = (corner == 0 || corner == 3 ? 1f : -1f) * (width * 0.5f - radius);
                        float z = (corner < 2 ? 1f : -1f) * (depth * 0.5f - radius);
                        vertices.Add(new Vector3(x + Mathf.Cos(angle) * (radius - inset), ys[row],
                            z + Mathf.Sin(angle) * (radius - inset)));
                    }
            }
            int ring = 4 * corners;
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < ring; i++)
                {
                    int a = row * ring + i;
                    int b = row * ring + (i + 1) % ring;
                    indices.AddRange(new[] { a, a + ring, b, b, a + ring, b + ring });
                }
            int bottom = vertices.Count;
            vertices.Add(Vector3.zero);
            int top = vertices.Count;
            vertices.Add(new Vector3(0f, height, 0f));
            for (int i = 0; i < ring; i++)
            {
                int next = (i + 1) % ring;
                indices.AddRange(new[] { bottom, i, next, top, 3 * ring + next, 3 * ring + i });
            }
            return SaveMesh(name, vertices, indices);
        }

        private static Mesh SaveMesh(string name, List<Vector3> vertices, List<int> indices)
        {
            Mesh mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string path = KitchenDemo.GeneratedRoot + "/Meshes/" + name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) AssetDatabase.CreateAsset(mesh, path);
            else
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                mesh = existing;
            }
            return mesh;
        }

        public static TextMesh Label(string name, Transform parent, string text, Vector3 position,
            float height, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            Transform holder = Group(name, parent, position);
            TextMesh label = holder.gameObject.AddComponent<TextMesh>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 72;
            label.characterSize = height * 0.2f;
            label.anchor = anchor;
            label.alignment = anchor == TextAnchor.MiddleLeft ? TextAlignment.Left : TextAlignment.Center;
            label.color = color;
            label.GetComponent<Renderer>().sharedMaterial = label.font.material;
            return label;
        }
    }
}
