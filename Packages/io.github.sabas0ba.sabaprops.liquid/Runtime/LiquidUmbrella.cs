using UdonSharp;
using UnityEngine;

namespace SabaProps.Liquid
{
    /// <summary>
    /// 雨と雪を遮る傘。
    /// <para>
    /// 傘の面（canopy）の下にいる相手には、天候の Source が降らせません。判定は
    /// LiquidCanvasPool.IsUnderUmbrella が、面の中心と向き、半径、覆う深さから行います。
    /// 手に持っても地面に立てても同じで、面の位置と傾きに従います。
    /// </para>
    /// <para>
    /// 状態を持たないため同期しません。持ち運ぶ場合、位置は Pickup の VRCObjectSync が同期します。
    /// 開始時にプールへ自分を登録します。
    /// </para>
    /// </summary>
    [AddComponentMenu("SabaProps/Liquid/Umbrella")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LiquidUmbrella : UdonSharpBehaviour
    {
        [Tooltip("傘の面。上向き（+Y）が空の側です。未設定ならこの Transform です。")]
        public Transform canopy;

        [Tooltip("傘の面の半径（m）。")]
        [Min(0.1f)]
        public float radius = 0.55f;

        [Tooltip("傘の面から下へ覆う深さ（m）。")]
        [Min(0.1f)]
        public float depth = 2.2f;

        [Tooltip("登録するプール。未設定なら名前で探します。")]
        public LiquidCanvasPool pool;

        private void Start()
        {
            if (canopy == null)
            {
                canopy = transform;
            }

            if (pool == null)
            {
                GameObject found = GameObject.Find(LiquidCanvasPool.DefaultName);
                if (found != null)
                {
                    pool = found.GetComponent<LiquidCanvasPool>();
                }
            }

            if (pool != null)
            {
                pool.RegisterUmbrella(this);
            }
        }
    }
}
