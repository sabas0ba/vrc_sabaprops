using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.Tablet
{
    /// <summary>VR の指で横に動かすスライダー。Desktop は両端のボタンで調整します。</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class TabletSlider : UdonSharpBehaviour
    {
        public UdonSharpBehaviour target;
        public string eventName;
        public float minimum;
        public float maximum = 1f;
        public float value;
        public float step = 0.05f;
        public BoxCollider pressZone;
        public Transform thumb;
        public TextMeshPro valueLabel;
        private int activeHand = -1;
        private bool leftArmed;
        private bool rightArmed;

        private void Start() { Apply(); }
        private void OnDisable() { activeHand = -1; leftArmed = false; rightArmed = false; }

        public void _Increase() { value += (maximum - minimum) * step; Apply(); }
        public void _Decrease() { value -= (maximum - minimum) * step; Apply(); }
        public void _SetValue() { Apply(); }

        public override void Interact()
        {
            VRCPlayerApi player = Networking.LocalPlayer;
            if (!Utilities.IsValid(player) || pressZone == null || player.IsUserInVR()) return;
            VRCPlayerApi.TrackingData head = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            Vector3 origin = pressZone.transform.InverseTransformPoint(head.position);
            Vector3 direction = pressZone.transform.InverseTransformVector(head.rotation * Vector3.forward);
            if (Mathf.Abs(direction.z) < 0.0001f) return;
            float distance = (pressZone.center.z - origin.z) / direction.z;
            if (distance < 0f) return;
            float x = origin.x + direction.x * distance - pressZone.center.x;
            value = Mathf.Lerp(minimum, maximum, Mathf.InverseLerp(-pressZone.size.x * 0.5f, pressZone.size.x * 0.5f, x));
            Apply();
        }

        private void Update()
        {
            VRCPlayerApi player = Networking.LocalPlayer;
            if (!Utilities.IsValid(player) || !player.IsUserInVR() || pressZone == null) return;
            Vector3 left = player.GetBonePosition(HumanBodyBones.LeftIndexDistal);
            Vector3 right = player.GetBonePosition(HumanBodyBones.RightIndexDistal);
            Vector3 leftMiddle = player.GetBonePosition(HumanBodyBones.LeftIndexIntermediate);
            Vector3 rightMiddle = player.GetBonePosition(HumanBodyBones.RightIndexIntermediate);
            if (left != Vector3.zero && leftMiddle != Vector3.zero)
                left += (left - leftMiddle) * 0.8f;
            if (right != Vector3.zero && rightMiddle != Vector3.zero)
                right += (right - rightMiddle) * 0.8f;
            Poll(left, 0);
            Poll(right, 1);
        }

        private void Poll(Vector3 point, int hand)
        {
            if (point == Vector3.zero)
            {
                if (activeHand == hand) activeHand = -1;
                if (hand == 0) leftArmed = false; else rightArmed = false;
                return;
            }
            Vector3 p = pressZone.transform.InverseTransformPoint(point) - pressZone.center;
            Vector3 half = pressZone.size * 0.5f;
            bool inside = Mathf.Abs(p.y) <= half.y && Mathf.Abs(p.x) <= half.x + 0.01f;
            bool approach = inside && p.z < -half.z && p.z > -half.z - 0.025f;
            if (!inside || p.z > half.z + 0.008f || p.z < -half.z - 0.025f)
            {
                if (hand == 0) leftArmed = false; else rightArmed = false;
            }
            if (hand == 0 && approach) leftArmed = true;
            if (hand == 1 && approach) rightArmed = true;
            bool armed = hand == 0 ? leftArmed : rightArmed;
            if (activeHand < 0 && armed && inside && p.z >= -half.z && p.z <= half.z)
                activeHand = hand;
            if (activeHand == hand)
            {
                if (!inside || p.z < -half.z - 0.008f || p.z > half.z + 0.008f)
                {
                    activeHand = -1;
                    if (hand == 0) leftArmed = false; else rightArmed = false;
                    return;
                }
                float next = Mathf.Lerp(minimum, maximum, Mathf.InverseLerp(-half.x, half.x, p.x));
                if (Mathf.Abs(next - value) > 0.00001f)
                {
                    value = next;
                    Apply();
                }
            }
            else if (!inside)
            {
                if (hand == 0) leftArmed = false; else rightArmed = false;
            }
        }

        private void Apply()
        {
            value = Mathf.Clamp(value, minimum, maximum);
            if (thumb != null && pressZone != null)
            {
                Vector3 p = thumb.localPosition;
                p.x = pressZone.center.x + (Mathf.InverseLerp(minimum, maximum, value) - 0.5f) * pressZone.size.x;
                thumb.localPosition = p;
            }
            if (valueLabel != null) valueLabel.text = value.ToString("0.00");
            if (target == null || string.IsNullOrEmpty(eventName)) return;
            target.SetProgramVariable("tabletValue", value);
            target.SendCustomEvent(eventName);
        }
    }
}
