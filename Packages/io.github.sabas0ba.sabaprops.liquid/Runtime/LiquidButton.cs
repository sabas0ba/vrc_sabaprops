using UdonSharp;
using UnityEngine;

namespace SabaProps.Liquid
{
    /// <summary>
    /// Interact で別の behaviour のイベントを呼ぶボタン。
    /// <para>
    /// 操作盤のボタン（量を増やす、放つ、明かりを切り替えるなど）に使います。
    /// 状態は呼び出し先が持ち、同期も呼び出し先が行うため、このボタンは同期しません。
    /// </para>
    /// <para>
    /// relayPickupUse を有効にすると、Pickup の使用ボタンでも同じイベントを呼びます。使用ボタンの
    /// イベントは Pickup の GameObject にしか届かないため、子に置いた Source へ中継するのに使います。
    /// 使用ボタンを離したときと手放したときにも、それぞれ別のイベントを中継できます。
    /// VRCObjectSync と同じ GameObject に置けるよう、同期変数を持たない設定にしています。
    /// </para>
    /// </summary>
    [AddComponentMenu("SabaProps/Liquid/Button")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class LiquidButton : UdonSharpBehaviour
    {
        [Tooltip("呼び出し先。")]
        public UdonSharpBehaviour target;

        [Tooltip("呼び出すイベント（public メソッドの名前）。")]
        public string eventName = "";

        [Tooltip("Pickup の使用ボタンでも呼ぶか。")]
        public bool relayPickupUse;

        [Tooltip("Pickup の使用ボタンを離したときに呼ぶイベント。空なら呼びません。")]
        public string useUpEventName = "";

        [Tooltip("Pickup を手放したときに呼ぶイベント。空なら呼びません。")]
        public string dropEventName = "";

        public override void Interact()
        {
            Send();
        }

        public override void OnPickupUseDown()
        {
            if (relayPickupUse)
            {
                Send();
            }
        }

        public override void OnPickupUseUp()
        {
            SendNamed(useUpEventName);
        }

        public override void OnDrop()
        {
            SendNamed(dropEventName);
        }

        private void Send()
        {
            SendNamed(eventName);
        }

        private void SendNamed(string name)
        {
            if (target != null && !string.IsNullOrEmpty(name))
            {
                target.SendCustomEvent(name);
            }
        }
    }
}
