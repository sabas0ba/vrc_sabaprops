using System;
using UnityEngine;

namespace SabaProps.Capture
{
    /// <summary>
    /// CaptureRecorder の計算部分。撮影周期、保存枚数、リングバッファの添字、間引きの並べ替えを
    /// 引数だけから決めます。コンポーネント、シーン、VRChat SDK のいずれにも触れません。
    /// <para>
    /// UdonSharp は UdonSharpBehaviour を継承したクラスしかコンパイルしないため、
    /// static なヘルパークラスへ切り出せません。代わりに部分クラスにしています。
    /// この宣言は基底型も VRC の using も持たないので、このファイルだけで単独コンパイルでき、
    /// .github/verify/offline が Unity 無しで実行して検査します。
    /// </para>
    /// <para>
    /// その前提として、このファイルのメソッドは CaptureRecorder のフィールドを参照しては
    /// なりません。必要な値はすべて引数で受け取ります。
    /// </para>
    /// </summary>
    public partial class CaptureRecorder
    {
        /// <summary>8 bit RGBA。1 画素 4 byte。</summary>
        public const int FormatARGB32 = 0;

        /// <summary>16 bit RGB。1 画素 2 byte。ARGB32 の半分の VRAM で保存できますが、階調が粗くなります。</summary>
        public const int FormatRGB565 = 1;

        /// <summary>満杯になったら古いものから上書きします。直近の一定時間を保持します。</summary>
        public const int PolicyRing = 0;

        /// <summary>満杯になったら撮影を止めます。</summary>
        public const int PolicyStop = 1;

        /// <summary>
        /// 満杯になったら 1 枚おきに捨てて撮影間隔を 2 倍にします。枚数の上限を守ったまま、
        /// 撮影開始から現在までを常に均等な間隔で覆います。
        /// </summary>
        public const int PolicyThin = 2;

        /// <summary>設定値にかかわらず保持する枚数の上限。</summary>
        public const int HardFrameLimit = 4096;

        /// <summary>撮影間隔の下限 (s)。これより短い指定はこの値に丸めます。</summary>
        public const float MinInterval = 0.1f;

        /// <summary>1 画素あたりの byte 数。未知の形式は ARGB32 と同じ扱いにします。</summary>
        private int BytesPerPixel(int format)
        {
            return format == FormatRGB565 ? 2 : 4;
        }

        /// <summary>1 枚あたりの VRAM (byte)。int の桁あふれを避けるため double で返します。</summary>
        private double FrameBytes(int width, int height, int format)
        {
            return (double)Mathf.Max(width, 1) * Mathf.Max(height, 1) * BytesPerPixel(format);
        }

        /// <summary>
        /// 保持できる枚数。maxFrames と VRAM の予算の小さい方で、1 以上 HardFrameLimit 以下です。
        /// <para>
        /// budgetMegabytes が 0 以下なら予算は使わず maxFrames だけで決めます。予算が 1 枚分に
        /// 満たない場合も 1 枚は確保します。0 枚の Recorder は状態として扱う意味がないためです。
        /// </para>
        /// </summary>
        private int CapacityFor(int width, int height, int format, int maxFrames, float budgetMegabytes)
        {
            int capacity = Mathf.Clamp(maxFrames, 1, HardFrameLimit);

            if (budgetMegabytes > 0f)
            {
                double byBudget = Math.Floor(budgetMegabytes * 1048576.0 / FrameBytes(width, height, format));
                if (byBudget < capacity)
                {
                    capacity = Mathf.Max((int)byBudget, 1);
                }
            }

            return capacity;
        }

        /// <summary>
        /// 次に撮影する時刻。scheduled を起点とした interval 刻みの格子のうち、now より後で最初のもの。
        /// <para>
        /// 前回撮影した時刻ではなく予定時刻から数えるので、フレームの揺れで間隔がずれていきません。
        /// 処理落ちや一時停止で複数回分を飛ばした場合も、まとめて撮らずに格子へ戻ります。
        /// </para>
        /// </summary>
        private double NextCaptureTime(double scheduled, double interval, double now)
        {
            if (interval < MinInterval)
            {
                interval = MinInterval;
            }

            double next = scheduled + interval;
            if (next > now)
            {
                return next;
            }

            double steps = Math.Floor((now - scheduled) / interval) + 1.0;
            next = scheduled + steps * interval;

            // 浮動小数の丸めで now と等しくなった場合に同じ時刻を 2 回撮らないよう、1 刻み進めます。
            return next > now ? next : next + interval;
        }

        /// <summary>古い方から数えた順番 logical を、保存配列の添字へ変換します。</summary>
        private int PhysicalIndex(int head, int logical, int capacity)
        {
            if (capacity <= 0)
            {
                return 0;
            }

            int index = (head + logical) % capacity;
            return index < 0 ? index + capacity : index;
        }

        /// <summary>
        /// 間引き後の並び。order[k] に、新しい配列の k 番目へ移す旧配列の添字を書きます。
        /// <para>
        /// 先頭には古い方から 0, 2, 4, ... 番目の保持分を順に置き、その後に捨てた分と未使用の枠を
        /// 並べます。捨てた枠の RenderTexture は次の撮影で再利用するため、配列から消しません。
        /// 最古の 1 枚を必ず残すので、タイムラインの起点は間引いても変わりません。
        /// </para>
        /// </summary>
        /// <returns>保持する枚数。</returns>
        private int ThinOrder(int[] order, int head, int count, int capacity)
        {
            int kept = (count + 1) / 2;
            int write = 0;

            for (int logical = 0; logical < count; logical += 2)
            {
                order[write] = PhysicalIndex(head, logical, capacity);
                write++;
            }

            for (int logical = 1; logical < count; logical += 2)
            {
                order[write] = PhysicalIndex(head, logical, capacity);
                write++;
            }

            for (int logical = count; logical < capacity; logical++)
            {
                order[write] = PhysicalIndex(head, logical, capacity);
                write++;
            }

            return kept;
        }

        /// <summary>
        /// 時刻 t に最も近い画像の順番。times は保存配列の並びで、古い方から時刻が単調に増える前提です。
        /// 二分探索なので、数千枚でも呼び出しごとの Udon の命令数が小さく済みます。
        /// </summary>
        /// <returns>見つからなければ -1。</returns>
        private int NearestLogical(double[] times, int head, int count, int capacity, double t)
        {
            if (count <= 0)
            {
                return -1;
            }

            int low = 0;
            int high = count - 1;

            while (low < high)
            {
                int middle = (low + high) / 2;
                if (times[PhysicalIndex(head, middle, capacity)] < t)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            if (low > 0)
            {
                double after = times[PhysicalIndex(head, low, capacity)] - t;
                double before = t - times[PhysicalIndex(head, low - 1, capacity)];
                if (before <= after)
                {
                    return low - 1;
                }
            }

            return low;
        }
    }
}
