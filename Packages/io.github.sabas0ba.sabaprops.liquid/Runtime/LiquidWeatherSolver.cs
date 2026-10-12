using UnityEngine;

namespace SabaProps.Liquid
{
    /// <summary>
    /// 天候の周期と地面の状態の計算。
    /// <para>
    /// LiquidWeather の部分クラスで、基底型も VRChat の参照も持たないため単独でコンパイルでき、
    /// .github/verify/offline/OfflineLiquidTests.cs が Unity 無しで実行します。
    /// 時刻はサーバー時刻を渡すため、地面の状態は全クライアントで同じ値になり、
    /// 途中から入ったプレイヤーにも同じ積もり方が見えます。
    /// </para>
    /// </summary>
    public partial class LiquidWeather
    {
        /// <summary>
        /// 降る強さ（0〜1）。onSeconds の間降り、offSeconds の間止む周期を繰り返します。
        /// 降り始めと降り終わりは rampSeconds かけて強さが変わります。offSeconds が 0 以下なら降り続けます。
        /// </summary>
        private float PrecipitationLevel(double time, float onSeconds, float offSeconds, float rampSeconds)
        {
            if (onSeconds <= 0f)
            {
                return 0f;
            }

            if (offSeconds <= 0f)
            {
                return 1f;
            }

            float t = CyclePhase(time, onSeconds + offSeconds);
            if (t >= onSeconds)
            {
                return 0f;
            }

            if (rampSeconds <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp01(Mathf.Min(t, onSeconds - t) / rampSeconds);
        }

        /// <summary>
        /// 地面に積もった量（0〜1）。降っている間は buildSeconds で 1 に達する速さで増え、
        /// 止むと clearSeconds で 1 が消える速さで減ります。
        /// <para>
        /// 前の周期の残りは持ち越しません。止んでいる間に消えきらない設定では、
        /// 次に降り始めた時点で 0 から積もり直します。
        /// </para>
        /// </summary>
        private float GroundCover(double time, float onSeconds, float offSeconds, float buildSeconds, float clearSeconds)
        {
            if (onSeconds <= 0f)
            {
                return 0f;
            }

            if (offSeconds <= 0f)
            {
                return 1f;
            }

            float t = CyclePhase(time, onSeconds + offSeconds);
            if (t < onSeconds)
            {
                return BuildUp(t, buildSeconds);
            }

            float peak = BuildUp(onSeconds, buildSeconds);
            float cleared = clearSeconds <= 0f ? peak : (t - onSeconds) / clearSeconds;
            return Mathf.Max(0f, peak - cleared);
        }

        /// <summary>
        /// 雪が溶けて地面が濡れている度合い（0〜1）。溶けた量だけ濡れ、
        /// 溶けきった後は drySeconds で乾きます。降っている間は濡れません。
        /// </summary>
        private float MeltWetness(double time, float onSeconds, float offSeconds, float buildSeconds,
            float meltSeconds, float drySeconds)
        {
            if (onSeconds <= 0f || offSeconds <= 0f)
            {
                return 0f;
            }

            float t = CyclePhase(time, onSeconds + offSeconds);
            if (t < onSeconds)
            {
                return 0f;
            }

            float peak = BuildUp(onSeconds, buildSeconds);
            float since = t - onSeconds;
            float melted = meltSeconds <= 0f ? peak : Mathf.Min(peak, since / meltSeconds);
            float meltEnd = meltSeconds <= 0f ? 0f : peak * meltSeconds;
            float dried = drySeconds <= 0f ? 1f : Mathf.Max(0f, since - meltEnd) / drySeconds;
            return Mathf.Clamp01(melted - dried);
        }

        /// <summary>範囲の中心から見た点が、半分の大きさ halfSize の箱の中にあるか。</summary>
        private bool InsideArea(Vector3 local, Vector3 halfSize)
        {
            return Mathf.Abs(local.x) <= halfSize.x
                && Mathf.Abs(local.y) <= halfSize.y
                && Mathf.Abs(local.z) <= halfSize.z;
        }

        /// <summary>
        /// 雨粒が落ち始める点。受け手の頭上 height の高さで、半径 radius の円内に一様に散らし、
        /// 風で流される分だけ風上へずらします。u, v は [0, 1) の乱数です。
        /// </summary>
        private Vector3 DropOrigin(Vector3 top, float radius, float height, Vector3 wind, float u, float v)
        {
            float r = radius * Mathf.Sqrt(u);
            float angle = v * 2f * Mathf.PI;
            Vector3 offset = new Vector3(Mathf.Cos(angle) * r, height, Mathf.Sin(angle) * r);
            return top + offset - wind * height;
        }

        /// <summary>雨粒の落ちる向き。真下に風を足した向きです。</summary>
        private Vector3 DropDirection(Vector3 wind)
        {
            return (Vector3.down + wind).normalized;
        }

        private float CyclePhase(double time, float cycle)
        {
            double t = time % cycle;
            if (t < 0.0)
            {
                t += cycle;
            }

            return (float)t;
        }

        private float BuildUp(float seconds, float buildSeconds)
        {
            return buildSeconds <= 0f ? 1f : Mathf.Clamp01(seconds / buildSeconds);
        }
    }
}
