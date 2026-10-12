using UnityEngine;

namespace SabaProps.BodyContact
{
    // BodyContactDebugView の計算部分。BodyContactSolver.cs と同じく、基底型も VRChat の参照も持たず、
    // フィールドを参照しません。オフライン検査は .github/verify/offline/OfflineBodyContactTests.cs です。
    public partial class BodyContactDebugView
    {
        /// <summary>円を近似する線分の数。</summary>
        public const int CircleSegments = 8;

        /// <summary>
        /// axis に直交する単位ベクトル。axis が鉛直に近い場合と長さが 0 の場合も有限の値を返します。
        /// </summary>
        private Vector3 Perpendicular(Vector3 axis)
        {
            Vector3 reference = Mathf.Abs(axis.y) < 0.9f * axis.magnitude ? Vector3.up : Vector3.right;
            Vector3 side = Vector3.Cross(axis, reference);
            if (side.sqrMagnitude < 1e-10f)
            {
                return Vector3.right;
            }

            return side.normalized;
        }

        /// <summary>u と v が張る平面上の、center を中心とする円周上の点。index は CircleSegments で 1 周します。</summary>
        private Vector3 CirclePoint(Vector3 center, Vector3 u, Vector3 v, float radius, int index)
        {
            // 固定8分割の単位円。Udon内で頂点ごとに三角関数を呼ばないようにします。
            int i = index % CircleSegments;
            float x = 0f;
            float y = 0f;
            const float diagonal = 0.70710678f;
            if (i == 0) x = 1f;
            else if (i == 1) { x = diagonal; y = diagonal; }
            else if (i == 2) y = 1f;
            else if (i == 3) { x = -diagonal; y = diagonal; }
            else if (i == 4) x = -1f;
            else if (i == 5) { x = -diagonal; y = -diagonal; }
            else if (i == 6) y = -1f;
            else { x = diagonal; y = -diagonal; }
            return center + (u * x + v * y) * radius;
        }
    }
}
