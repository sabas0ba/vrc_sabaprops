using UnityEngine;

namespace SabaProps.Liquid
{
    /// <summary>
    /// ノズルの設定から放出を決める計算。
    /// <para>
    /// LiquidNozzle の部分クラスで、基底型も VRChat の参照も持たないため単独でコンパイルでき、
    /// .github/verify/offline/OfflineLiquidTests.cs が Unity 無しで実行します。
    /// </para>
    /// <para>
    /// 量はリットル、距離と断面の直径はメートル、速さはメートル毎秒です。1 L の液体を、
    /// 1 回の命中あたり約 0.125 L の塊に分けて光線を放ちます。断面が太いほど 1 つの塊が付く範囲が広く、
    /// 届く距離で直径が広がる分だけ、放つ向きにばらつきを付けます。
    /// </para>
    /// </summary>
    public partial class LiquidNozzle
    {
        /// <summary>1 回に放てる光線の上限。LiquidBodyCanvas が 1 周期に積める付着の数と揃えます。</summary>
        public const int MaxRaysPerShot = 16;

        /// <summary>1 つの塊の量（L）。</summary>
        public const float LitresPerRay = 0.125f;

        /// <summary>量 volume（L）を放つ光線の数。少なくとも 1 本、多くても MaxRaysPerShot 本です。</summary>
        private int RaysFor(float volume)
        {
            int rays = Mathf.CeilToInt(Mathf.Max(volume, 0f) / LitresPerRay);
            return Mathf.Clamp(rays, 1, MaxRaysPerShot);
        }

        /// <summary>
        /// 1 本の光線が運ぶ付着の量。光線の数が上限で頭打ちになる大量の放出では、1 本あたりを増やして
        /// 全体の量を保ちます。1 本あたりは 1.5 を上限とします。
        /// </summary>
        private float AmountPerRay(float volume)
        {
            int rays = RaysFor(volume);
            float perRay = Mathf.Max(volume, 0f) / rays / LitresPerRay;
            return Mathf.Clamp(perRay, 0.15f, 1.5f) * 0.6f;
        }

        /// <summary>1 つの塊が付く範囲の半径（m）。断面の半径に、飛ぶ間の広がりを足します。</summary>
        private float HitRadiusFor(float diameter, float distance)
        {
            return Mathf.Max(diameter * 0.5f, 0.01f) + distance * 0.015f;
        }

        /// <summary>
        /// 放つ向きのばらつき（円錐の半頂角、度）。届く距離で断面の直径ぶん広がる角度です。
        /// </summary>
        private float ConeAngleFor(float diameter, float range)
        {
            if (range <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp(Mathf.Atan2(Mathf.Max(diameter, 0f) * 0.5f, range) * Mathf.Rad2Deg, 0f, 45f);
        }

        /// <summary>命中を判定する放物線の区間の数。</summary>
        public const int ArcSegments = 4;

        /// <summary>
        /// 放物線上の点。origin から初速 velocity（m/s）で放った液の、t 秒後の位置です。重力は 9.81 m/s² です。
        /// </summary>
        private Vector3 ArcPoint(Vector3 origin, Vector3 velocity, float t)
        {
            return origin + velocity * t + new Vector3(0f, -0.5f * 9.81f * t * t, 0f);
        }

        /// <summary>
        /// 命中を判定する飛行時間（秒）。放った向きに range だけ進むまでの時間で、速さ 0 では 0 です。
        /// </summary>
        private float FlightTime(float range, float speed)
        {
            return speed <= 0f ? 0f : Mathf.Max(range, 0f) / speed;
        }

        /// <summary>設定を 1 段変えた値。step 刻みで minimum と maximum の間に収めます。</summary>
        private float Step(float value, float step, int direction, float minimum, float maximum)
        {
            float next = value + step * direction;
            return Mathf.Clamp(Mathf.Round(next / step) * step, minimum, maximum);
        }
    }
}
