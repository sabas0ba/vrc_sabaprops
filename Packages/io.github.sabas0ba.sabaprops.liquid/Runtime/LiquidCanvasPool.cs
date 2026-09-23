using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.Liquid
{
    /// <summary>
    /// Body Canvas をプレイヤーへ割り当てるプール。
    /// <para>
    /// Canvas は RenderTexture を 4 枚持つため、全員分を常に確保するとメモリが足りません。
    /// 付着の入力を受けたプレイヤーにだけ割り当て、足りなくなったら最も長く入力の無い
    /// Canvas を取り上げます。ローカルプレイヤーの Canvas は取り上げの対象から外します。
    /// 自分の体に付いた液体が他人の都合で消えるのは、見え方として最も不自然なためです。
    /// </para>
    /// <para>
    /// 割り当ては各クライアントが独立に決めます。同じプレイヤーでも、クライアントごとに
    /// 別の Canvas が割り当たることがありますが、見た目には影響しません。
    /// </para>
    /// </summary>
    [AddComponentMenu("SabaProps/Liquid/Canvas Pool")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LiquidCanvasPool : UdonSharpBehaviour
    {
        [Tooltip("割り当てに使う Canvas。数がそのまま同時に付着を表示できる人数の上限です。")]
        public LiquidBodyCanvas[] canvases;

        /// <summary>割り当て済みの Canvas を返します。無ければ null。</summary>
        public LiquidBodyCanvas FindCanvas(VRCPlayerApi player)
        {
            if (!Utilities.IsValid(player) || canvases == null)
            {
                return null;
            }

            int id = player.playerId;
            for (int i = 0; i < canvases.Length; i++)
            {
                LiquidBodyCanvas canvas = canvases[i];
                if (canvas != null && canvas.GetPlayerId() == id)
                {
                    return canvas;
                }
            }

            return null;
        }

        /// <summary>
        /// プレイヤーの Canvas を返します。無ければ空きを割り当て、空きも無ければ
        /// 最も長く入力の無い Canvas を取り上げて割り当てます。
        /// </summary>
        public LiquidBodyCanvas AcquireCanvas(VRCPlayerApi player)
        {
            LiquidBodyCanvas existing = FindCanvas(player);
            if (existing != null || !Utilities.IsValid(player) || canvases == null)
            {
                return existing;
            }

            LiquidBodyCanvas chosen = null;
            float oldest = float.MaxValue;
            VRCPlayerApi local = Networking.LocalPlayer;
            int localId = Utilities.IsValid(local) ? local.playerId : -1;

            for (int i = 0; i < canvases.Length; i++)
            {
                LiquidBodyCanvas canvas = canvases[i];
                if (canvas == null)
                {
                    continue;
                }

                int owner = canvas.GetPlayerId();
                if (owner < 0)
                {
                    chosen = canvas;
                    break;
                }

                if (owner == localId)
                {
                    continue;
                }

                float activity = canvas.GetLastActivityTime();
                if (activity < oldest)
                {
                    oldest = activity;
                    chosen = canvas;
                }
            }

            if (chosen != null)
            {
                chosen.Assign(player);
            }

            return chosen;
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            LiquidBodyCanvas canvas = FindCanvas(player);
            if (canvas != null)
            {
                canvas.Release();
            }
        }
    }
}
