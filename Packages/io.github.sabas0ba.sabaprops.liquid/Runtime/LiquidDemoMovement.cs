using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.Liquid
{
    /// <summary>
    /// ローカルプレイヤーの移動速度を上げ、ジャンプできるようにします。サンプルのワールド用です。
    /// <para>
    /// VRChat の既定は歩行 2 m/s、走行 4 m/s、ジャンプの初速 0（ジャンプ不可）です。サンプルは区画が
    /// 数十 m に散らばっているため、既定のままでは見て回るのに時間がかかります。
    /// </para>
    /// <para>
    /// VRCSceneDescriptor は移動の設定を持たず、速度は実行時に VRCPlayerApi で与えます。
    /// 各クライアントが自分のプレイヤーに設定するため、同期はしません。
    /// </para>
    /// </summary>
    [AddComponentMenu("SabaProps/Liquid/Demo Movement")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class LiquidDemoMovement : UdonSharpBehaviour
    {
        [Tooltip("歩行速度（m/s）。VRChat の既定は 2 です。")]
        [Min(0f)]
        public float walkSpeed = 4f;

        [Tooltip("走行速度（m/s）。VRChat の既定は 4 です。")]
        [Min(0f)]
        public float runSpeed = 8f;

        [Tooltip("横移動の速度（m/s）。VRChat の既定は 2 です。")]
        [Min(0f)]
        public float strafeSpeed = 4f;

        [Tooltip("ジャンプの初速。VRChat の既定は 0 で、0 のままだとジャンプできません。")]
        [Min(0f)]
        public float jumpImpulse = 3f;

        private void Start()
        {
            ApplySoon();
        }

        public override void OnPlayerJoined(VRCPlayerApi player)
        {
            // 開始時にローカルプレイヤーがまだ用意できていない場合に備えて、入った時点でも設定します。
            if (Utilities.IsValid(player) && player.isLocal)
            {
                ApplySoon();
            }
        }

        public override void OnPlayerRespawn(VRCPlayerApi player)
        {
            if (Utilities.IsValid(player) && player.isLocal)
            {
                Apply();
            }
        }

        /// <summary>
        /// すぐに設定し、少し後にも設定し直します。入った直後はプレイヤーの初期化が続いており、
        /// 先に設定した値が既定値で上書きされることがあるためです。
        /// </summary>
        private void ApplySoon()
        {
            Apply();
            SendCustomEventDelayedSeconds(nameof(Apply), 1f);
            SendCustomEventDelayedSeconds(nameof(Apply), 5f);
        }

        /// <summary>ローカルプレイヤーに速度とジャンプを設定します。値を実行中に変えたときにも呼べます。</summary>
        public void Apply()
        {
            VRCPlayerApi player = Networking.LocalPlayer;
            if (!Utilities.IsValid(player))
            {
                return;
            }

            player.SetWalkSpeed(walkSpeed);
            player.SetRunSpeed(runSpeed);
            player.SetStrafeSpeed(strafeSpeed);
            player.SetJumpImpulse(jumpImpulse);
        }
    }
}
