// A runnable stand-in for the small slice of UnityEngine that this package's
// mesh generation touches, so that generation can be executed and inspected
// without a Unity installation.
//
// This is deliberately NOT the same thing as the reference assemblies used by
// verify.sh's compile step. Those are Unity's own and prove the package calls
// real APIs with real signatures; they cannot run. This one can run, at the
// cost of being a reimplementation.
//
// CAVEAT, and the reason the offline tests only assert structural properties:
// Slerp, AngleAxis and friends here are the textbook formulas, not Unity's. A
// test that depended on their exact output would be testing this file. What
// the offline tier asserts instead is topology, finiteness, determinism and
// the channel invariants the shader relies on — none of which change if a
// rotation is a few ulps away from Unity's.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TooltipAttribute : Attribute
    {
        public TooltipAttribute(string tooltip) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RangeAttribute : Attribute
    {
        public RangeAttribute(float min, float max) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class MinAttribute : Attribute
    {
        public MinAttribute(float min) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class HeaderAttribute : Attribute
    {
        public HeaderAttribute(string header) { }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeField : Attribute { }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class HideInInspector : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DisallowMultipleComponent : Attribute { }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RequireComponent : Attribute
    {
        public RequireComponent(Type requiredComponent) { }
        public RequireComponent(Type requiredComponent, Type requiredComponent2) { }
        public RequireComponent(Type requiredComponent, Type requiredComponent2, Type requiredComponent3) { }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string menuName { get; set; }
        public string fileName { get; set; }
        public int order { get; set; }
    }

    public static class Mathf
    {
        public const float PI = 3.14159265358979f;
        public const float Deg2Rad = PI / 180f;
        public const float Rad2Deg = 180f / PI;

        public static float Abs(float v) => Math.Abs(v);
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;

        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        public static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);

        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
        public static float InverseLerp(float a, float b, float v) =>
            Math.Abs(b - a) < 1e-12f ? 0f : Clamp01((v - a) / (b - a));

        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Sin(float v) => (float)Math.Sin(v);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Tan(float v) => (float)Math.Tan(v);
        public static float Acos(float v) => (float)Math.Acos(Clamp(v, -1f, 1f));
        public static float Pow(float v, float p) => (float)Math.Pow(v, p);

        public static float Repeat(float t, float length) =>
            Clamp(t - (float)Math.Floor(t / length) * length, 0f, length);

        public static int FloorToInt(float v) => (int)Math.Floor(v);
        public static int CeilToInt(float v) => (int)Math.Ceiling(v);
        public static int RoundToInt(float v) => (int)Math.Round(v, MidpointRounding.ToEven);
    }

    public struct Vector2
    {
        public float x, y;

        public Vector2(float x, float y) { this.x = x; this.y = y; }

        public static Vector2 zero => new Vector2(0f, 0f);

        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator *(Vector2 a, float s) => new Vector2(a.x * s, a.y * s);

        public override string ToString() => $"({x}, {y})";
    }

    public struct Vector3
    {
        public float x, y, z;

        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }

        public static Vector3 zero => new Vector3(0f, 0f, 0f);
        public static Vector3 one => new Vector3(1f, 1f, 1f);
        public static Vector3 up => new Vector3(0f, 1f, 0f);
        public static Vector3 down => new Vector3(0f, -1f, 0f);
        public static Vector3 forward => new Vector3(0f, 0f, 1f);
        public static Vector3 right => new Vector3(1f, 0f, 0f);

        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => Mathf.Sqrt(sqrMagnitude);

        public Vector3 normalized
        {
            get
            {
                float m = magnitude;
                return m > 1e-9f ? new Vector3(x / m, y / m, z / m) : zero;
            }
        }

        public void Normalize()
        {
            Vector3 n = normalized;
            x = n.x;
            y = n.y;
            z = n.z;
        }

        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;

        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(
            a.y * b.z - a.z * b.y,
            a.z * b.x - a.x * b.z,
            a.x * b.y - a.y * b.x);

        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;

        public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        }

        /// <summary>Great-circle interpolation, falling back to Lerp when degenerate.</summary>
        public static Vector3 Slerp(Vector3 a, Vector3 b, float t)
        {
            float ma = a.magnitude;
            float mb = b.magnitude;

            if (ma < 1e-6f || mb < 1e-6f)
            {
                return Lerp(a, b, t);
            }

            Vector3 na = a.normalized;
            Vector3 nb = b.normalized;
            float dot = Mathf.Clamp(Dot(na, nb), -1f, 1f);

            if (dot > 0.9995f || dot < -0.9995f)
            {
                return Lerp(a, b, t);
            }

            float theta = Mathf.Acos(dot) * Mathf.Clamp01(t);
            Vector3 relative = (nb - na * dot).normalized;

            float magnitude = Mathf.Lerp(ma, mb, Mathf.Clamp01(t));
            return (na * Mathf.Cos(theta) + relative * Mathf.Sin(theta)) * magnitude;
        }

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float s) => new Vector3(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator *(float s, Vector3 a) => a * s;
        public static Vector3 operator /(Vector3 a, float s) => new Vector3(a.x / s, a.y / s, a.z / s);

        public override string ToString() => $"({x}, {y}, {z})";
    }

    public struct Vector4
    {
        public float x, y, z, w;

        public Vector4(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public override string ToString() => $"({x}, {y}, {z}, {w})";
    }

    public struct Color
    {
        public float r, g, b, a;

        public Color(float r, float g, float b, float a = 1f)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public static Color white => new Color(1f, 1f, 1f, 1f);

        public static Color Lerp(Color x, Color y, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color(
                x.r + (y.r - x.r) * t,
                x.g + (y.g - x.g) * t,
                x.b + (y.b - x.b) * t,
                x.a + (y.a - x.a) * t);
        }

        public override string ToString() => $"RGBA({r}, {g}, {b}, {a})";
    }

    public struct Quaternion
    {
        public float x, y, z, w;

        public Quaternion(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public static Quaternion identity => new Quaternion(0f, 0f, 0f, 1f);

        public static Quaternion AngleAxis(float angleDegrees, Vector3 axis)
        {
            Vector3 n = axis.normalized;
            float half = angleDegrees * Mathf.Deg2Rad * 0.5f;
            float s = Mathf.Sin(half);
            return new Quaternion(n.x * s, n.y * s, n.z * s, Mathf.Cos(half));
        }

        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            // v + 2w(q x v) + 2(q x (q x v))
            var u = new Vector3(q.x, q.y, q.z);
            Vector3 uv = Vector3.Cross(u, v);
            Vector3 uuv = Vector3.Cross(u, uv);
            return v + (uv * q.w + uuv) * 2f;
        }

        public static Quaternion operator *(Quaternion a, Quaternion b) => new Quaternion(
            a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
            a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
            a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,
            a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);
    }

    public struct Bounds
    {
        public Vector3 center;
        public Vector3 extents;

        public Bounds(Vector3 center, Vector3 size)
        {
            this.center = center;
            extents = size * 0.5f;
        }

        public Vector3 size
        {
            get => extents * 2f;
            set => extents = value * 0.5f;
        }

        public Vector3 min => center - extents;
        public Vector3 max => center + extents;

        public void Expand(float amount)
        {
            amount *= 0.5f;
            extents = new Vector3(extents.x + amount, extents.y + amount, extents.z + amount);
        }
    }

    public struct Matrix4x4
    {
        // Column-major, as Unity stores it: mRowColumn.
        public float m00, m10, m20, m30;
        public float m01, m11, m21, m31;
        public float m02, m12, m22, m32;
        public float m03, m13, m23, m33;

        public static Matrix4x4 identity
        {
            get
            {
                var m = default(Matrix4x4);
                m.m00 = m.m11 = m.m22 = m.m33 = 1f;
                return m;
            }
        }

        public static Matrix4x4 Translate(Vector3 t)
        {
            Matrix4x4 m = identity;
            m.m03 = t.x;
            m.m13 = t.y;
            m.m23 = t.z;
            return m;
        }

        public static Matrix4x4 Scale(Vector3 s)
        {
            var m = default(Matrix4x4);
            m.m00 = s.x;
            m.m11 = s.y;
            m.m22 = s.z;
            m.m33 = 1f;
            return m;
        }

        public static Matrix4x4 Rotate(Quaternion q)
        {
            Matrix4x4 m = identity;

            Vector3 rx = q * new Vector3(1f, 0f, 0f);
            Vector3 ry = q * new Vector3(0f, 1f, 0f);
            Vector3 rz = q * new Vector3(0f, 0f, 1f);

            m.m00 = rx.x; m.m10 = rx.y; m.m20 = rx.z;
            m.m01 = ry.x; m.m11 = ry.y; m.m21 = ry.z;
            m.m02 = rz.x; m.m12 = rz.y; m.m22 = rz.z;

            return m;
        }

        public static Matrix4x4 TRS(Vector3 t, Quaternion r, Vector3 s) =>
            Translate(t) * Rotate(r) * Scale(s);

        public Vector3 MultiplyPoint3x4(Vector3 v) => new Vector3(
            m00 * v.x + m01 * v.y + m02 * v.z + m03,
            m10 * v.x + m11 * v.y + m12 * v.z + m13,
            m20 * v.x + m21 * v.y + m22 * v.z + m23);

        public Vector3 MultiplyVector(Vector3 v) => new Vector3(
            m00 * v.x + m01 * v.y + m02 * v.z,
            m10 * v.x + m11 * v.y + m12 * v.z,
            m20 * v.x + m21 * v.y + m22 * v.z);

        public Matrix4x4 transpose
        {
            get
            {
                var r = default(Matrix4x4);
                r.m00 = m00; r.m01 = m10; r.m02 = m20; r.m03 = m30;
                r.m10 = m01; r.m11 = m11; r.m12 = m21; r.m13 = m31;
                r.m20 = m02; r.m21 = m12; r.m22 = m22; r.m23 = m32;
                r.m30 = m03; r.m31 = m13; r.m32 = m23; r.m33 = m33;
                return r;
            }
        }

        public Matrix4x4 inverse
        {
            get
            {
                float[,] a = ToArray();
                var inv = new float[4, 4];

                for (int i = 0; i < 4; i++)
                {
                    inv[i, i] = 1f;
                }

                // Gauss-Jordan with partial pivoting. Not fast, but this runs on
                // a handful of matrices in a test.
                for (int col = 0; col < 4; col++)
                {
                    int pivot = col;
                    for (int row = col + 1; row < 4; row++)
                    {
                        if (Math.Abs(a[row, col]) > Math.Abs(a[pivot, col]))
                        {
                            pivot = row;
                        }
                    }

                    if (Math.Abs(a[pivot, col]) < 1e-12f)
                    {
                        return default;
                    }

                    SwapRows(a, col, pivot);
                    SwapRows(inv, col, pivot);

                    float scale = 1f / a[col, col];
                    for (int k = 0; k < 4; k++)
                    {
                        a[col, k] *= scale;
                        inv[col, k] *= scale;
                    }

                    for (int row = 0; row < 4; row++)
                    {
                        if (row == col)
                        {
                            continue;
                        }

                        float factor = a[row, col];
                        for (int k = 0; k < 4; k++)
                        {
                            a[row, k] -= factor * a[col, k];
                            inv[row, k] -= factor * inv[col, k];
                        }
                    }
                }

                return FromArray(inv);
            }
        }

        public static Matrix4x4 operator *(Matrix4x4 lhs, Matrix4x4 rhs)
        {
            float[,] a = lhs.ToArray();
            float[,] b = rhs.ToArray();
            var r = new float[4, 4];

            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    float sum = 0f;
                    for (int k = 0; k < 4; k++)
                    {
                        sum += a[row, k] * b[k, col];
                    }

                    r[row, col] = sum;
                }
            }

            return FromArray(r);
        }

        private static void SwapRows(float[,] m, int a, int b)
        {
            if (a == b)
            {
                return;
            }

            for (int k = 0; k < 4; k++)
            {
                float t = m[a, k];
                m[a, k] = m[b, k];
                m[b, k] = t;
            }
        }

        private float[,] ToArray() => new[,]
        {
            { m00, m01, m02, m03 },
            { m10, m11, m12, m13 },
            { m20, m21, m22, m23 },
            { m30, m31, m32, m33 },
        };

        private static Matrix4x4 FromArray(float[,] v)
        {
            var m = default(Matrix4x4);
            m.m00 = v[0, 0]; m.m01 = v[0, 1]; m.m02 = v[0, 2]; m.m03 = v[0, 3];
            m.m10 = v[1, 0]; m.m11 = v[1, 1]; m.m12 = v[1, 2]; m.m13 = v[1, 3];
            m.m20 = v[2, 0]; m.m21 = v[2, 1]; m.m22 = v[2, 2]; m.m23 = v[2, 3];
            m.m30 = v[3, 0]; m.m31 = v[3, 1]; m.m32 = v[3, 2]; m.m33 = v[3, 3];
            return m;
        }
    }

    public class Object
    {
        public string name { get; set; } = string.Empty;
    }

    public class Material : Object { }

    public class Component : Object { }
    public class Behaviour : Component { }
    public class MonoBehaviour : Behaviour { }
    public class Collider : Component { }
    public class MeshFilter : Component { }
    public class MeshRenderer : Component { }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
    }

    /// <summary>
    /// A plain data holder. The package only ever writes channels into a mesh
    /// and reads them back, so nothing here needs a GPU.
    /// </summary>
    public class Mesh : Object
    {
        private readonly Dictionary<int, List<Vector4>> _uvs = new Dictionary<int, List<Vector4>>();

        public Vector3[] vertices { get; private set; } = new Vector3[0];
        public Vector3[] normals { get; set; } = new Vector3[0];
        public Color[] colors { get; private set; } = new Color[0];
        public int[] triangles { get; private set; } = new int[0];

        public Rendering.IndexFormat indexFormat { get; set; } = Rendering.IndexFormat.UInt16;
        public int vertexCount => vertices.Length;
        public Bounds bounds { get; set; }

        public Vector2[] uv
        {
            get
            {
                var list = new List<Vector2>();
                GetUVs(0, list);
                return list.ToArray();
            }
        }

        public void SetVertices(List<Vector3> value)
        {
            vertices = value.ToArray();
            RecalculateBounds();
        }

        public void SetNormals(List<Vector3> value) => normals = value.ToArray();
        public void SetColors(List<Color> value) => colors = value.ToArray();

        public void SetUVs(int channel, List<Vector2> value)
        {
            var stored = new List<Vector4>(value.Count);
            foreach (Vector2 v in value)
            {
                stored.Add(new Vector4(v.x, v.y, 0f, 0f));
            }

            _uvs[channel] = stored;
        }

        public void SetUVs(int channel, List<Vector4> value) => _uvs[channel] = new List<Vector4>(value);

        public void GetUVs(int channel, List<Vector2> into)
        {
            into.Clear();
            if (!_uvs.TryGetValue(channel, out List<Vector4> stored))
            {
                return;
            }

            foreach (Vector4 v in stored)
            {
                into.Add(new Vector2(v.x, v.y));
            }
        }

        public void GetUVs(int channel, List<Vector4> into)
        {
            into.Clear();
            if (_uvs.TryGetValue(channel, out List<Vector4> stored))
            {
                into.AddRange(stored);
            }
        }

        public void SetTriangles(List<int> value, int submesh, bool calculateBounds)
        {
            triangles = value.ToArray();
            if (calculateBounds)
            {
                RecalculateBounds();
            }
        }

        public int GetIndexCount(int submesh) => triangles.Length;

        public void RecalculateBounds()
        {
            if (vertices.Length == 0)
            {
                bounds = new Bounds(Vector3.zero, Vector3.zero);
                return;
            }

            Vector3 min = vertices[0];
            Vector3 max = vertices[0];

            foreach (Vector3 v in vertices)
            {
                min = new Vector3(Mathf.Min(min.x, v.x), Mathf.Min(min.y, v.y), Mathf.Min(min.z, v.z));
                max = new Vector3(Mathf.Max(max.x, v.x), Mathf.Max(max.y, v.y), Mathf.Max(max.z, v.z));
            }

            bounds = new Bounds((min + max) * 0.5f, max - min);
        }

        /// <summary>Area-weighted vertex normals, as Unity computes them.</summary>
        public void RecalculateNormals()
        {
            var accumulated = new Vector3[vertices.Length];

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];

                Vector3 face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);

                accumulated[a] += face;
                accumulated[b] += face;
                accumulated[c] += face;
            }

            var result = new Vector3[vertices.Length];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = accumulated[i].sqrMagnitude > 0f ? accumulated[i].normalized : Vector3.up;
            }

            normals = result;
        }

        public void UploadMeshData(bool markNoLongerReadable) { }
    }
}

namespace UnityEngine.Rendering
{
    public enum IndexFormat { UInt16 = 0, UInt32 = 1 }
}
