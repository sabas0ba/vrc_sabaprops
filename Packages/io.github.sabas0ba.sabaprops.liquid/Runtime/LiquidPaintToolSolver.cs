using UnityEngine;

namespace SabaProps.Liquid
{
    /// <summary>
    /// 描画ツールの計算。LiquidPaintTool の部分クラスで、基底型も VRChat の参照も持たないため、
    /// このファイルだけで単独コンパイルできます（.github/verify/offline/OfflineLiquidTests.cs）。
    /// ここのメソッドは LiquidPaintTool のフィールドを参照しません。
    /// </summary>
    public partial class LiquidPaintTool
    {
        /// <summary>1 区間の長さの上限（m）。これより離れた点は線でつなぎません。</summary>
        public const float MaxSegment = 0.6f;

        /// <summary>面内の回転を量子化する段数。</summary>
        public const int AngleSteps = 16;

        /// <summary>法線と回転を合わせた符号の bit 数。描画の履歴がこの上にツールと面の番号を詰めます。</summary>
        public const int StrokeCodeBits = 19;

        /// <summary>
        /// 単位ベクトルを 15 bit（各成分 5 bit、-15〜15）へ量子化します。イベントと履歴で法線を小さく持つのに使います。
        /// </summary>
        private int PackDirection(Vector3 direction)
        {
            Vector3 n = direction.sqrMagnitude > 1e-12f ? direction.normalized : Vector3.up;
            int x = Mathf.RoundToInt(n.x * 15f) + 15;
            int y = Mathf.RoundToInt(n.y * 15f) + 15;
            int z = Mathf.RoundToInt(n.z * 15f) + 15;
            return x | (y << 5) | (z << 10);
        }

        /// <summary>PackDirection の逆変換。単位ベクトルを返します。</summary>
        private Vector3 UnpackDirection(int packed)
        {
            Vector3 n = new Vector3((packed & 31) - 15, ((packed >> 5) & 31) - 15, ((packed >> 10) & 31) - 15);
            return n.sqrMagnitude > 0f ? n.normalized : Vector3.up;
        }

        /// <summary>法線と面内の回転（rad）を 1 つの整数（StrokeCodeBits bit）へ詰めます。</summary>
        private int PackStroke(Vector3 normal, float angle)
        {
            float turn = angle / (2f * Mathf.PI);
            int step = Mathf.RoundToInt((turn - Mathf.Floor(turn)) * AngleSteps) % AngleSteps;
            return PackDirection(normal) | (step << 15);
        }

        /// <summary>PackStroke の符号から面内の回転（rad）を取り出します。</summary>
        private float StrokeAngle(int code)
        {
            return ((code >> 15) & (AngleSteps - 1)) * (2f * Mathf.PI / AngleSteps);
        }

        /// <summary>
        /// 形を置く回転（rad）。壁では 0（形の上が面の上）、床や天井では形の上がツールの向く先になるようにします。
        /// normal と forward は面を持つターゲットの座標系の方向です。
        /// </summary>
        private float ShapeAngle(Vector3 normal, Vector3 forward)
        {
            if (Mathf.Abs(normal.y) < 0.7f)
            {
                return 0f;
            }

            Vector2 flat = new Vector2(forward.x, forward.z);
            if (flat.sqrMagnitude < 1e-6f)
            {
                return 0f;
            }

            // 床と天井のタイルの (u, v) は (x, z) で、回転 a のとき形の上は (-sin a, cos a) を向きます。
            return Mathf.Atan2(-flat.x, flat.y);
        }

        /// <summary>
        /// 線の始点。前回の点が同じターゲットにあり、maxSegment 以内ならそこから続け、そうでなければ今の点から始めます。
        /// </summary>
        private Vector3 StrokeStart(bool hasLast, bool sameTarget, Vector3 last, Vector3 current, float maxSegment)
        {
            if (!hasLast || !sameTarget || (current - last).magnitude > maxSegment)
            {
                return current;
            }

            return last;
        }
    }
}
