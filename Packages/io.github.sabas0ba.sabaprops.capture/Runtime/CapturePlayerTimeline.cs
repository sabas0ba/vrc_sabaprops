using System;
using UnityEngine;

namespace SabaProps.Capture
{
    /// <summary>
    /// CapturePlayer の計算部分。再生位置の進め方、スライダーとの対応、サムネイルの選び方、
    /// 時刻の表示を引数だけから決めます。
    /// <para>
    /// CaptureRecorderSchedule.cs と同じく、基底型も VRC の using も持たない部分クラスです。
    /// このファイルのメソッドは CapturePlayer のフィールドを参照してはなりません。
    /// </para>
    /// </summary>
    public partial class CapturePlayer
    {
        /// <summary>再生速度の下限と上限 (枚/s)。</summary>
        public const float MinPlaybackRate = 0.5f;

        public const float MaxPlaybackRate = 60f;

        /// <summary>
        /// 再生位置を dt 秒分進めます。位置は [0, count) の連続値で、表示する画像は切り捨てで決めます。
        /// loop が偽なら最後の画像で止まります。
        /// </summary>
        private float AdvancePlayhead(float playhead, float rate, float deltaTime, int count, bool loop)
        {
            if (count <= 0)
            {
                return 0f;
            }

            float next = playhead + Mathf.Max(rate, 0f) * Mathf.Max(deltaTime, 0f);

            if (loop)
            {
                return Mathf.Repeat(next, count);
            }

            return Mathf.Min(next, count - 1);
        }

        /// <summary>再生位置に対応する画像の順番。空なら -1。</summary>
        private int PlayheadFrame(float playhead, int count)
        {
            if (count <= 0)
            {
                return -1;
            }

            return Mathf.Clamp(Mathf.FloorToInt(playhead), 0, count - 1);
        }

        /// <summary>
        /// 1 枚ずつ送ります。loop が真なら端で反対側へ回り、偽なら端で止まります。
        /// </summary>
        private int StepFrame(int frame, int delta, int count, bool loop)
        {
            if (count <= 0)
            {
                return -1;
            }

            int next = frame + delta;
            if (loop)
            {
                next %= count;
                return next < 0 ? next + count : next;
            }

            return Mathf.Clamp(next, 0, count - 1);
        }

        /// <summary>0 から 1 のスライダー値を画像の順番へ変換します。</summary>
        private int SliderToFrame(float value, int count)
        {
            if (count <= 1)
            {
                return count == 1 ? 0 : -1;
            }

            return Mathf.RoundToInt(Mathf.Clamp(value, 0f, 1f) * (count - 1));
        }

        /// <summary>画像の順番を 0 から 1 のスライダー値へ変換します。SliderToFrame の逆です。</summary>
        private float FrameToSlider(int frame, int count)
        {
            if (count <= 1)
            {
                return 0f;
            }

            return Mathf.Clamp((float)frame / (count - 1), 0f, 1f);
        }

        /// <summary>
        /// thumbnailCount 個のサムネイルの index 番目に出す画像の順番。
        /// <para>
        /// 枚数がサムネイル数以下なら先頭から 1 枚ずつ並べ、残りは -1 (空欄) にします。
        /// それより多ければ最古と最新を両端に含む等間隔で選びます。
        /// </para>
        /// </summary>
        private int ThumbnailFrame(int index, int thumbnailCount, int count)
        {
            if (count <= 0 || index < 0 || index >= thumbnailCount)
            {
                return -1;
            }

            if (count <= thumbnailCount)
            {
                return index < count ? index : -1;
            }

            if (thumbnailCount == 1)
            {
                return count - 1;
            }

            return Mathf.RoundToInt((float)index * (count - 1) / (thumbnailCount - 1));
        }

        /// <summary>経過秒を "m:ss" または "h:mm:ss" で表します。負の値は 0 とします。</summary>
        private string FormatClock(double seconds)
        {
            // NaN も含めて弾くため、否定形で比較します。
            if (!(seconds >= 0.0))
            {
                seconds = 0.0;
            }

            // int で数えられる範囲 (約 68 年) を超える値は表示上の意味がないので飽和させます。
            int total = seconds >= int.MaxValue ? int.MaxValue : (int)Math.Floor(seconds);
            int hours = total / 3600;
            int minutes = total / 60 % 60;
            int secs = total % 60;

            if (hours > 0)
            {
                return hours + ":" + Pad2(minutes) + ":" + Pad2(secs);
            }

            return minutes + ":" + Pad2(secs);
        }

        private string Pad2(int value)
        {
            return value < 10 ? "0" + value : value.ToString();
        }
    }
}
