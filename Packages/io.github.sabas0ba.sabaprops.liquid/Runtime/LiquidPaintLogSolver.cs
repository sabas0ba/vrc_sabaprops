using UnityEngine;

namespace SabaProps.Liquid
{
    /// <summary>
    /// 描画の履歴の計算。LiquidPaintLog の部分クラスで、基底型も VRChat の参照も持たないため、
    /// このファイルだけで単独コンパイルできます（.github/verify/offline/OfflineLiquidTests.cs）。
    /// ここのメソッドは LiquidPaintLog のフィールドを参照しません。
    /// </summary>
    public partial class LiquidPaintLog
    {
        /// <summary>履歴に載せられるツールの数の上限。</summary>
        public const int MaxTools = 128;

        /// <summary>履歴に載せられるワールドの面の数の上限。</summary>
        public const int MaxSurfaces = 32;

        private const int ToolShift = LiquidPaintTool.StrokeCodeBits;
        private const int SurfaceShift = ToolShift + 7;

        /// <summary>線の符号（法線と回転）に、ツールと面の番号を詰めます。</summary>
        private int PackEntry(int strokeCode, int tool, int surface)
        {
            return (strokeCode & ((1 << ToolShift) - 1)) | (tool << ToolShift) | (surface << SurfaceShift);
        }

        private int EntryStroke(int entry)
        {
            return entry & ((1 << ToolShift) - 1);
        }

        private int EntryTool(int entry)
        {
            return (entry >> ToolShift) & (MaxTools - 1);
        }

        private int EntrySurface(int entry)
        {
            return (entry >> SurfaceShift) & (MaxSurfaces - 1);
        }

        /// <summary>
        /// 環状の配列で、古い順に数えて order 番目の要素の位置。next は次に書く位置、count は入っている数です。
        /// </summary>
        private int RingIndex(int next, int count, int capacity, int order)
        {
            int index = (next - count + order) % capacity;
            return index < 0 ? index + capacity : index;
        }
    }
}
