using UdonSharp;
using UnityEngine;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace SabaProps.Liquid
{
    /// <summary>
    /// 付着を消すボタンの受け口。自分、マネキン、全員の 3 通りを、全クライアントで同時に消します。
    /// <para>
    /// 消去はイベントで全員へ送ります。自分の消去は送り主と対象が同じ場合だけ受け付けるため、
    /// 他人の付着を勝手に消すことはできません。全員の消去は、everyoneMayClearAll が無効なら
    /// インスタンスのマスターだけが行えます。同期変数は持ちません。
    /// </para>
    /// </summary>
    [AddComponentMenu("SabaProps/Liquid/Reset Panel")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class LiquidResetPanel : UdonSharpBehaviour
    {
        /// <summary>消去イベントの送信上限（回/秒）。</summary>
        public const int MaxClearsPerSecond = 2;

        [Tooltip("付着を消すプール。未設定なら名前で探します。")]
        public LiquidCanvasPool pool;

        [Tooltip("誰でも全員の付着を消せるか。無効ならマスターだけが消せます。")]
        public bool everyoneMayClearAll = true;

        private void Start()
        {
            if (pool == null)
            {
                GameObject found = GameObject.Find(LiquidCanvasPool.DefaultName);
                if (found != null)
                {
                    pool = found.GetComponent<LiquidCanvasPool>();
                }
            }
        }

        /// <summary>自分の付着を、全員の画面で消します。</summary>
        public void ClearMine()
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local))
            {
                return;
            }

            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ReceiveClearPlayer), local.playerId);
        }

        /// <summary>マネキンの付着を、全員の画面で消します。</summary>
        public void ClearMannequins()
        {
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ReceiveClearMannequins));
        }

        /// <summary>全員とマネキンの付着を、全員の画面で消します。</summary>
        public void ClearEveryone()
        {
            if (!everyoneMayClearAll && !Networking.IsMaster)
            {
                return;
            }

            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ReceiveClearEveryone));
        }

        [NetworkCallable(MaxClearsPerSecond)]
        public void ReceiveClearPlayer(int playerId)
        {
            VRCPlayerApi sender = NetworkCalling.CallingPlayer;
            if (pool == null || !Utilities.IsValid(sender) || sender.playerId != playerId)
            {
                return;
            }

            pool.ClearPlayer(playerId);
        }

        [NetworkCallable(MaxClearsPerSecond)]
        public void ReceiveClearMannequins()
        {
            if (pool != null)
            {
                pool.ClearMannequins();
            }
        }

        [NetworkCallable(MaxClearsPerSecond)]
        public void ReceiveClearEveryone()
        {
            VRCPlayerApi sender = NetworkCalling.CallingPlayer;
            if (pool == null || !Utilities.IsValid(sender) || (!everyoneMayClearAll && !sender.isMaster))
            {
                return;
            }

            pool.ClearEveryone();
        }
    }
}
