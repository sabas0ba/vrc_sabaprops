using UnityEngine;

namespace SabaProps.Liquid
{
    /// <summary>
    /// Source が共通で使う幾何計算。
    /// <para>
    /// LiquidCanvasPool の部分クラスで、基底型も VRChat の参照も持たないため単独でコンパイルでき、
    /// .github/verify/offline/OfflineLiquidTests.cs が Unity 無しで実行します。
    /// メソッドは LiquidCanvasPool のフィールドを参照せず、値はすべて引数で受け取ります。
    /// </para>
    /// </summary>
    public partial class LiquidCanvasPool
    {
        /// <summary>
        /// 光線とカプセル（線分 a-b を半径 r で膨らませた形）の最初の交点までの距離。
        /// 交わらない、または起点がカプセルの内側にある場合は -1 を返します。
        /// <para>
        /// direction は単位ベクトルを前提とします。プレイヤーの体をカプセルで近似し、
        /// Collider の構成に依存せずに命中を判定するために使います。
        /// </para>
        /// </summary>
        private float RayCapsule(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, float radius)
        {
            Vector3 ba = b - a;
            Vector3 oa = origin - a;
            float baba = Vector3.Dot(ba, ba);
            if (baba < 1e-10f)
            {
                return RaySphere(origin, direction, a, radius);
            }

            // 内側から出る光線は当たりとしません。液体は体の外から来るためです。
            float along = Mathf.Clamp01(Vector3.Dot(oa, ba) / baba);
            if ((oa - ba * along).sqrMagnitude <= radius * radius)
            {
                return -1f;
            }

            float bard = Vector3.Dot(ba, direction);
            float baoa = Vector3.Dot(ba, oa);
            float rdoa = Vector3.Dot(direction, oa);
            float oaoa = Vector3.Dot(oa, oa);

            // 円柱部分。軸と平行な光線では式が退化するため、端の球だけで判定します。
            float qa = baba - bard * bard;
            float y;
            if (qa > 1e-8f)
            {
                float qb = baba * rdoa - baoa * bard;
                float qc = baba * oaoa - baoa * baoa - radius * radius * baba;
                float h = qb * qb - qa * qc;
                if (h < 0f)
                {
                    return -1f;
                }

                float t = (-qb - Mathf.Sqrt(h)) / qa;
                y = baoa + t * bard;
                if (y > 0f && y < baba)
                {
                    return t >= 0f ? t : -1f;
                }
            }
            else
            {
                // 軸に沿って進む光線は、進行方向の手前側の端に先に当たります。
                y = bard > 0f ? 0f : baba;
            }

            // 円柱の側面より外で交わった場合は、その側の端の半球と判定し直します。
            return RaySphere(origin, direction, y <= 0f ? a : b, radius);
        }

        private float RaySphere(Vector3 origin, Vector3 direction, Vector3 center, float radius)
        {
            Vector3 oc = origin - center;
            float b = Vector3.Dot(oc, direction);
            float c = Vector3.Dot(oc, oc) - radius * radius;
            float h = b * b - c;
            if (h < 0f || c < 0f)
            {
                return -1f;
            }

            float t = -b - Mathf.Sqrt(h);
            return t >= 0f ? t : -1f;
        }

        /// <summary>カプセル表面の点における外向きの法線。</summary>
        private Vector3 CapsuleNormal(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ba = b - a;
            float baba = Mathf.Max(Vector3.Dot(ba, ba), 1e-8f);
            float h = Mathf.Clamp01(Vector3.Dot(point - a, ba) / baba);
            Vector3 normal = point - (a + ba * h);
            return normal.sqrMagnitude > 1e-12f ? normal.normalized : Vector3.up;
        }

        /// <summary>
        /// 軸まわりの円錐内の方向。u, v は [0, 1) の一様乱数で、面積が一様になるよう
        /// 半径方向は平方根で配ります。angleDegrees は円錐の半頂角です。
        /// </summary>
        private Vector3 ConeDirection(Vector3 axis, float angleDegrees, float u, float v)
        {
            Vector3 w = axis.sqrMagnitude > 1e-12f ? axis.normalized : Vector3.down;
            Vector3 helper = Mathf.Abs(w.y) < 0.9f ? Vector3.up : Vector3.right;
            Vector3 x = Vector3.Cross(helper, w).normalized;
            Vector3 y = Vector3.Cross(w, x);

            float spread = Mathf.Tan(Mathf.Clamp(angleDegrees, 0f, 80f) * Mathf.Deg2Rad) * Mathf.Sqrt(Mathf.Clamp01(u));
            float phi = Mathf.Clamp01(v) * 2f * Mathf.PI;
            return (w + x * (Mathf.Cos(phi) * spread) + y * (Mathf.Sin(phi) * spread)).normalized;
        }

        /// <summary>
        /// 整数から [0, 1) の擬似乱数。Source が評価周期の番号から方向や形状を作るのに使います。
        /// 全クライアントで同じ番号を与えれば同じ値になります。
        /// </summary>
        private float Hash01(int value)
        {
            // 整数の乗算による混合は Udon で桁あふれの扱いが保証されないため、浮動小数で作ります。
            float s = Mathf.Sin(value * 12.9898f + 78.233f) * 43758.5453f;
            return s - Mathf.Floor(s);
        }

        /// <summary>
        /// マネキンの番号をターゲット番号へ変換します。プレイヤー ID（0 以上）と「無し」（-1）と
        /// 重ならないよう、-2 以下を使います。
        /// </summary>
        private int MannequinTarget(int index)
        {
            return -index - 2;
        }

        /// <summary>MannequinTarget の逆変換。マネキンでないターゲットには負の値を返します。</summary>
        private int MannequinIndex(int target)
        {
            return target <= -2 ? -target - 2 : -1;
        }

        /// <summary>
        /// ワールドの点を、プレイヤーの足元と水平の向きを基準にした座標へ変換します。
        /// <para>
        /// 命中を同期するときに使います。プレイヤーの位置はクライアントごとに少しずつ
        /// 異なるため、ワールド座標のまま送ると受信側で体から外れた位置になります。
        /// </para>
        /// </summary>
        private Vector3 ToPlayerLocal(Vector3 world, Vector3 playerPosition, Vector3 playerForward)
        {
            Vector3 forward = HorizontalForward(playerForward);
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            Vector3 d = world - playerPosition;
            return new Vector3(Vector3.Dot(d, right), d.y, Vector3.Dot(d, forward));
        }

        /// <summary>ToPlayerLocal の逆変換。方向の変換には playerPosition に zero を渡します。</summary>
        private Vector3 FromPlayerLocal(Vector3 local, Vector3 playerPosition, Vector3 playerForward)
        {
            Vector3 forward = HorizontalForward(playerForward);
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            return playerPosition + right * local.x + Vector3.up * local.y + forward * local.z;
        }

        private Vector3 HorizontalForward(Vector3 forward)
        {
            Vector3 flat = new Vector3(forward.x, 0f, forward.z);
            return flat.sqrMagnitude > 1e-12f ? flat.normalized : Vector3.forward;
        }
    }
}
