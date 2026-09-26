using SabaProps.Tablet.Authoring;
using UnityEngine;

namespace SabaProps.Tablet.Editors
{
    /// <summary>Theme の装飾を通常のメッシュとして生成します。Collider は付けません。</summary>
    public static class TabletThemeDecorations
    {
        public static void Build(Transform body, TabletTheme theme, string folder)
        {
            if (theme.skeletonFrame) BuildFrame(body, theme, folder);
            if (theme.decoration == TabletDecoration.None) return;
            var root = new GameObject("Theme Decoration").transform;
            root.SetParent(body, false);
            Material outer = TabletAssets.CreateOrReplace(TabletAssets.ColorMaterial(theme.shader,
                theme.decorationColor, theme.smoothness, "Decoration"), folder + "/Decoration.mat");
            Material inner = TabletAssets.CreateOrReplace(TabletAssets.ColorMaterial(theme.shader,
                theme.decorationInnerColor, theme.smoothness, "Decoration Inner"), folder + "/DecorationInner.mat");
            float x = theme.bodySize.x * 0.39f;
            float y = theme.bodySize.y * 0.5f + 0.01f;
            float z = -theme.bodyThickness * 0.35f;
            switch (theme.decoration)
            {
                case TabletDecoration.Ribbon:
                    Blob(root, "Bow Left", new Vector3(x - 0.015f, y, z), new Vector3(0.035f, 0.023f, 0.014f), outer, -25f);
                    Blob(root, "Bow Right", new Vector3(x + 0.015f, y, z), new Vector3(0.035f, 0.023f, 0.014f), outer, 25f);
                    Blob(root, "Knot", new Vector3(x, y, z - 0.006f), Vector3.one * 0.017f, inner, 0f);
                    break;
                case TabletDecoration.CatEars:
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Polygon(root, "Ear", new Vector3(side * x, y, z),
                            new[] { new Vector2(-0.022f, -0.015f), new Vector2(0.022f, -0.015f), new Vector2(side * 0.008f, 0.022f) },
                            outer, folder + "/Ear" + side + ".asset");
                        Polygon(root, "Inner Ear", new Vector3(side * x, y, z - 0.005f),
                            new[] { new Vector2(-0.012f, -0.010f), new Vector2(0.012f, -0.010f), new Vector2(side * 0.005f, 0.012f) },
                            inner, folder + "/InnerEar" + side + ".asset");
                    }
                    break;
                case TabletDecoration.BearEars:
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Blob(root, "Ear", new Vector3(side * x, y, z), new Vector3(0.042f, 0.042f, 0.015f), outer, 0f);
                        Blob(root, "Inner Ear", new Vector3(side * x, y, z - 0.007f), new Vector3(0.024f, 0.024f, 0.010f), inner, 0f);
                    }
                    break;
                case TabletDecoration.Stars:
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var outline = new Vector2[10];
                        for (int i = 0; i < outline.Length; i++)
                        {
                            float angle = (90f + i * 36f) * Mathf.Deg2Rad;
                            float radius = i % 2 == 0 ? 0.022f : 0.010f;
                            outline[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                        }
                        Polygon(root, "Star", new Vector3(side * x, y, z), outline,
                            side < 0 ? outer : inner, folder + "/Star" + side + ".asset");
                    }
                    break;
                case TabletDecoration.Flower:
                    for (int i = 0; i < 5; i++)
                    {
                        float angle = i * 72f * Mathf.Deg2Rad;
                        Blob(root, "Petal", new Vector3(-x + Mathf.Cos(angle) * 0.014f,
                            y + Mathf.Sin(angle) * 0.014f, z), new Vector3(0.025f, 0.025f, 0.012f), outer, 0f);
                    }
                    Blob(root, "Flower Center", new Vector3(-x, y, z - 0.006f), Vector3.one * 0.017f, inner, 0f);
                    break;
                case TabletDecoration.Clouds:
                    for (int side = -1; side <= 1; side += 2)
                        for (int i = -1; i <= 1; i++)
                            Blob(root, "Cloud", new Vector3(side * x + i * 0.014f, y + (i == 0 ? 0.006f : 0f), z),
                                new Vector3(0.033f, 0.025f, 0.013f), i == 0 ? inner : outer, 0f);
                    break;
            }
        }

        private static void BuildFrame(Transform body, TabletTheme theme, string folder)
        {
            var root = new GameObject("Internal Frame").transform;
            root.SetParent(body, false);
            Material material = TabletAssets.CreateOrReplace(TabletAssets.ColorMaterial(theme.shader,
                theme.frameColor, theme.smoothness, "Frame"), folder + "/Frame.mat");
            for (int side = -1; side <= 1; side += 2)
            {
                FrameRail(root, new Vector3(side * theme.bodySize.x * 0.46f, 0f, 0f),
                    new Vector3(0.006f, theme.bodySize.y * 0.88f, theme.bodyThickness * 0.7f), material);
                FrameRail(root, new Vector3(0f, side * theme.bodySize.y * 0.44f, 0f),
                    new Vector3(theme.bodySize.x * 0.94f, 0.006f, theme.bodyThickness * 0.7f), material);
            }
        }

        private static void FrameRail(Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Support Rail";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void Blob(Transform parent, string name, Vector3 position, Vector3 scale, Material material, float angle)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.AngleAxis(angle, Vector3.forward);
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        // 中心からの三角形分割が可能な輪郭を、厚み 4 mm の閉じた立体にします。
        private static void Polygon(Transform parent, string name, Vector3 position, Vector2[] outline, Material material, string path)
        {
            var vertices = new Vector3[outline.Length * 2 + 2];
            int count = outline.Length;
            vertices[count * 2] = new Vector3(0f, 0f, -0.002f);
            vertices[count * 2 + 1] = new Vector3(0f, 0f, 0.002f);
            var triangles = new int[count * 12];
            for (int i = 0; i < count; i++)
            {
                vertices[i] = new Vector3(outline[i].x, outline[i].y, -0.002f);
                vertices[i + count] = new Vector3(outline[i].x, outline[i].y, 0.002f);
                int next = (i + 1) % count;
                int offset = i * 12;
                triangles[offset] = count * 2; triangles[offset + 1] = next; triangles[offset + 2] = i;
                triangles[offset + 3] = count * 2 + 1; triangles[offset + 4] = i + count; triangles[offset + 5] = next + count;
                triangles[offset + 6] = i; triangles[offset + 7] = next; triangles[offset + 8] = next + count;
                triangles[offset + 9] = i; triangles[offset + 10] = next + count; triangles[offset + 11] = i + count;
            }
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh = TabletAssets.CreateOrReplace(mesh, path);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
