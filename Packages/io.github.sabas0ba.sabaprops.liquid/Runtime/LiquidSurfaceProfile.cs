using UdonSharp;
using UnityEngine;

namespace SabaProps.Liquid
{
    /// <summary>
    /// 液体を受ける側の素材の定義。
    /// <para>
    /// 同じ水でも、柔らかい布は吸って色が深く暗くなり、革や樹脂ははじいて水滴になり、
    /// 肌は薄く塗り広げられます。Projector は受け手の実際のマテリアルを読めないため、
    /// 受け手の素材はこのプロファイルで与えます。Body Canvas は体の部位（髪、肌、衣服）ごとに
    /// プロファイルを持ち、部位は頭と手のボーンの位置から推定します。
    /// </para>
    /// </summary>
    [AddComponentMenu("SabaProps/Liquid/Surface Profile")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LiquidSurfaceProfile : UdonSharpBehaviour
    {
        [Tooltip("吸水性。高いほど液を吸って暗く深い色になり、艶が出にくく、水滴になりません。")]
        [Range(0f, 1f)]
        public float absorbency = 0.8f;

        [Tooltip("撥水性。高いほど水が表面で水滴になり、水滴にハイライトが乗ります。")]
        [Range(0f, 1f)]
        public float repellency = 0.1f;

        [Tooltip("濡れたときに表面全体が帯びる艶。")]
        [Range(0f, 1f)]
        public float sheen = 0.1f;

        [Tooltip("液が流れにくさ。高いほど垂れが遅く、表面に留まって広がります。")]
        [Range(0f, 1f)]
        public float friction = 0.5f;

        [Tooltip("にじみ。高いほど塗料の縁が繊維に沿ってぼやけます。")]
        [Range(0f, 1f)]
        public float bleed = 0.5f;

        [Tooltip("毛束。高いほど液が上下方向の筋にまとまります。髪で使います。")]
        [Range(0f, 1f)]
        public float strands = 0f;

        [Tooltip("水滴の大きさ（m）。")]
        [Range(0.004f, 0.04f)]
        public float beadSize = 0.012f;

        /// <summary>シェーダへ渡す 1 つ目のベクトル。吸水性、撥水性、艶、流れにくさ。</summary>
        public Vector4 GetPrimary()
        {
            return new Vector4(absorbency, repellency, sheen, friction);
        }

        /// <summary>シェーダへ渡す 2 つ目のベクトル。にじみ、毛束、水滴の大きさ。</summary>
        public Vector4 GetSecondary()
        {
            return new Vector4(bleed, strands, beadSize, 0f);
        }
    }
}
