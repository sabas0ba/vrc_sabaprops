using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace SabaProps.Tablet
{
    /// <summary>
    /// 頭上など体の近くの決まった位置に手を伸ばしてタブレットを取り出します。VR 専用です。
    /// <para>
    /// requireGrab が有効なときは、その位置で Grab 入力をすると取り出し、表示中なら収納します。
    /// 無効なときは、手を dwellSeconds 秒置き続けると同じ動作をします。
    /// </para>
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public partial class TabletReachTrigger : UdonSharpBehaviour
    {
        public TabletController controller;

        [Tooltip("頭の水平方向の向きを基準にした取り出し位置 (m)。既定は頭上です。")]
        public Vector3 anchorOffset = new Vector3(0f, 0.22f, 0f);

        [Tooltip("取り出し位置の半径 (m)。")]
        public float radius = 0.14f;

        [Tooltip("有効にすると Grab 入力で発火します。無効にすると手を置き続けると発火します。")]
        public bool requireGrab = true;

        public float dwellSeconds = 0.6f;

        private float leftDwell;
        private float rightDwell;

        public override void InputGrab(bool value, UdonInputEventArgs args)
        {
            if (!requireGrab || !value || controller == null)
            {
                return;
            }

            int hand = args.handType == HandType.LEFT ? 0 : 1;
            if (HandAtAnchor(hand))
            {
                Fire(hand);
            }
        }

        private void Update()
        {
            if (requireGrab || controller == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            float next = NextDwell(leftDwell, HandAtAnchor(0), dt);
            if (next >= dwellSeconds)
            {
                Fire(0);
                next = -1f;
            }

            leftDwell = next;

            next = NextDwell(rightDwell, HandAtAnchor(1), dt);
            if (next >= dwellSeconds)
            {
                Fire(1);
                next = -1f;
            }

            rightDwell = next;
        }

        private bool HandAtAnchor(int hand)
        {
            VRCPlayerApi player = Networking.LocalPlayer;
            if (!Utilities.IsValid(player) || !player.IsUserInVR())
            {
                return false;
            }

            VRCPlayerApi.TrackingData head = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            VRCPlayerApi.TrackingData palm = player.GetTrackingData(
                hand == 0 ? VRCPlayerApi.TrackingDataType.LeftHand : VRCPlayerApi.TrackingDataType.RightHand);
            Vector3 anchor = AnchorPosition(head.position, head.rotation * Vector3.forward, anchorOffset);
            return IsWithin(palm.position, anchor, radius);
        }

        private void Fire(int hand)
        {
            if (controller.IsShown())
            {
                controller._Stow();
            }
            else
            {
                controller.SummonNearHand(hand);
            }
        }
    }
}
