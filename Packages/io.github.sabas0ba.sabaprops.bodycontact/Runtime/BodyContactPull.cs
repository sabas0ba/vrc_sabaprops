using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

namespace SabaProps.BodyContact
{
    /// <summary>
    /// 1組の手・前腕のGrabを共有します。位置は同期せず、引かれる側だけが自分の移動量を計算します。
    /// 接触補正と壁判定はBodyContactSystemへ集約します。
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public partial class BodyContactPull : UdonSharpBehaviour
    {
        public BodyContactSystem source;
        [Tooltip("このクライアントが引かれることを許可します。既定はオフです。")]
        public bool allowBeingPulled;
        public float grabMargin = 0.06f;
        public float slack = 0.08f;
        public float responseSeconds = 0.25f;
        public float maxSpeed = 0.8f;
        public float maxAcceleration = 2f;
        public float breakDistance = 1.2f;
        public float leaseSeconds = 2f;

        [UdonSynced] public bool active;
        [UdonSynced] public int grabberId = -1;
        [UdonSynced] public int targetId = -1;
        [UdonSynced] public int grabbingHand;
        [UdonSynced] public int targetArm;
        [UdonSynced] public float anchorFraction;
        [UdonSynced] public float initialDistance;
        [UdonSynced] public double startedAt;
        [UdonSynced] public double heartbeat;

        [HideInInspector] public Vector3 pullStep;
        [HideInInspector] public Vector3 grabPoint;
        [HideInInspector] public Vector3 targetPoint;
        [HideInInspector] public bool pullingLocal;

        private bool _leftHeld;
        private bool _rightHeld;
        private Vector3 _velocity;
        private double _rejectedStart = -1;
        private float _nextHeartbeat;

        public override void InputGrab(bool value, UdonInputEventArgs args)
        {
            int hand = args.handType == HandType.LEFT ? 0 : 1;
            bool wasHeld = hand == 0 ? _leftHeld : _rightHeld;
            if (hand == 0) _leftHeld = value; else _rightHeld = value;
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local)) return;
            if (!value)
            {
                if (active && grabberId == local.playerId && grabbingHand == hand) _ReleasePull();
                return;
            }
            if (!wasHeld && local.IsUserInVR()) TryBegin(local, hand);
        }

        public override void InputJump(bool value, UdonInputEventArgs args)
        {
            if (value) _ReleasePull();
        }

        public void _TogglePullPermission()
        {
            allowBeingPulled = !allowBeingPulled;
            if (!allowBeingPulled) _ReleasePull();
        }

        public void _ReleasePull()
        {
            _velocity = Vector3.zero;
            pullStep = Vector3.zero;
            pullingLocal = false;
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local) || !active) return;
            if (local.playerId != grabberId && local.playerId != targetId) return;
            _rejectedStart = startedAt;
            // 対象側にも所有権移譲を許可し、解除を永続的な同期状態として伝えます。
            if (!Networking.IsOwner(gameObject)) Networking.SetOwner(local, gameObject);
            if (Networking.IsOwner(gameObject)) ClearSession();
        }

        public override bool OnOwnershipRequest(VRCPlayerApi requester, VRCPlayerApi newOwner)
        {
            if (!Utilities.IsValid(requester) || !Utilities.IsValid(newOwner)) return false;
            if (requester.playerId != newOwner.playerId) return false;
            return !HasLiveSession() || newOwner.playerId == targetId || newOwner.playerId == grabberId;
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            _velocity = Vector3.zero;
            if (Utilities.IsValid(player) && active && player.playerId != grabberId && player.isLocal) ClearSession();
        }

        public override void OnDeserialization()
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (Utilities.IsValid(local) && active && targetId == local.playerId
                && (!allowBeingPulled || !CanRun() || startedAt == _rejectedStart)) _ReleasePull();
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (active && (player.playerId == targetId || player.playerId == grabberId))
            {
                _ReleasePull();
                if (Networking.IsOwner(gameObject)) ClearSession();
            }
        }

        public override void OnPlayerRespawn(VRCPlayerApi player)
        {
            if (Utilities.IsValid(player) && player.isLocal) _ReleasePull();
        }

        public override void OnAvatarChanged(VRCPlayerApi player)
        {
            if (Utilities.IsValid(player) && active && (player.playerId == grabberId || player.playerId == targetId)) _ReleasePull();
        }

        public override void OnAvatarEyeHeightChanged(VRCPlayerApi player, float previousEyeHeight)
        {
            if (Utilities.IsValid(player) && active && (player.playerId == grabberId || player.playerId == targetId)) _ReleasePull();
        }

        private void OnDisable() { _ReleasePull(); }

        private bool CanRun()
        {
            return source != null && source.contactEnabled && !source.IsSuspended();
        }

        public bool HasLiveSession()
        {
            VRCPlayerApi owner = Networking.GetOwner(gameObject);
            return active && Utilities.IsValid(owner) && owner.playerId == grabberId && grabberId != targetId
                && (grabbingHand == 0 || grabbingHand == 1) && (targetArm == 0 || targetArm == 1)
                && anchorFraction >= 0f && anchorFraction <= 1f
                && initialDistance >= 0f && initialDistance <= breakDistance
                && LeaseIsValid(Networking.GetServerTimeInSeconds(), heartbeat, leaseSeconds);
        }

        private void ClearSession()
        {
            active = false;
            grabberId = -1;
            targetId = -1;
            _velocity = Vector3.zero;
            pullStep = Vector3.zero;
            pullingLocal = false;
            RequestSerialization();
        }

        private Vector3 HandPosition(VRCPlayerApi player, int hand)
        {
            return player.GetTrackingData(hand == 0 ? VRCPlayerApi.TrackingDataType.LeftHand : VRCPlayerApi.TrackingDataType.RightHand).position;
        }

        private bool ReadTarget(VRCPlayerApi player, int arm, float fraction)
        {
            Vector3 elbow = player.GetBonePosition(arm == 0 ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            Vector3 wrist = player.GetBonePosition(arm == 0 ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            if (elbow.sqrMagnitude < 1e-8f || wrist.sqrMagnitude < 1e-8f) return false;
            targetPoint = Vector3.Lerp(elbow, wrist, fraction);
            return true;
        }

        private void TryBegin(VRCPlayerApi local, int hand)
        {
            if (!CanRun() || HasLiveSession()) return;
            VRC_Pickup held = local.GetPickupInHand(hand == 0 ? VRC_Pickup.PickupHand.Left : VRC_Pickup.PickupHand.Right);
            if (Utilities.IsValid(held)) return;
            Vector3 palm = HandPosition(local, hand);
            VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
            VRCPlayerApi.GetPlayers(players);
            int bestPlayer = -1;
            int bestArm = 0;
            float bestFraction = 0f;
            float nearest = float.MaxValue;
            for (int i = 0; i < players.Length; i++)
            {
                VRCPlayerApi player = players[i];
                if (!Utilities.IsValid(player) || player.isLocal) continue;
                float radius = 0.026f * Mathf.Clamp(player.GetAvatarEyeHeightAsMeters(), 0.2f, 5f) * source.radiusScale;
                for (int arm = 0; arm < 2; arm++)
                {
                    Vector3 elbow = player.GetBonePosition(arm == 0 ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
                    Vector3 wrist = player.GetBonePosition(arm == 0 ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
                    if (elbow.sqrMagnitude < 1e-8f || wrist.sqrMagnitude < 1e-8f) continue;
                    float fraction = SegmentFraction(elbow, wrist, palm);
                    float distance = Vector3.Distance(palm, Vector3.Lerp(elbow, wrist, fraction));
                    if (distance <= radius + Mathf.Max(0f, grabMargin) && distance < nearest)
                    {
                        nearest = distance;
                        bestPlayer = player.playerId;
                        bestArm = arm;
                        bestFraction = fraction;
                    }
                }
            }
            if (bestPlayer < 0) return;
            Networking.SetOwner(local, gameObject);
            if (!Networking.IsOwner(gameObject)) return;
            grabberId = local.playerId;
            targetId = bestPlayer;
            grabbingHand = hand;
            targetArm = bestArm;
            anchorFraction = bestFraction;
            initialDistance = nearest;
            startedAt = Networking.GetServerTimeInSeconds();
            heartbeat = startedAt;
            active = true;
            _nextHeartbeat = Time.time + 0.3f;
            RequestSerialization();
        }

        /// <summary>移動の直前にSystemから呼びます。ネットワーク受信イベント内では移動しません。</summary>
        public void _Tick()
        {
            pullStep = Vector3.zero;
            pullingLocal = false;
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local)) return;
            if (!CanRun() || !HasLiveSession() || startedAt == _rejectedStart)
            {
                _ReleasePull();
                _velocity = Vector3.zero;
                return;
            }
            VRCPlayerApi grabber = VRCPlayerApi.GetPlayerById(grabberId);
            VRCPlayerApi target = VRCPlayerApi.GetPlayerById(targetId);
            if (!Utilities.IsValid(grabber) || !Utilities.IsValid(target) || !ReadTarget(target, targetArm, anchorFraction))
            {
                _ReleasePull();
                return;
            }
            grabPoint = HandPosition(grabber, grabbingHand);
            Vector3 delta = grabPoint - targetPoint;
            if (delta.magnitude > Mathf.Max(0.1f, breakDistance)) { _ReleasePull(); return; }
            if (grabber.isLocal)
            {
                bool held = grabbingHand == 0 ? _leftHeld : _rightHeld;
                if (!held || !local.IsUserInVR()) { _ReleasePull(); return; }
                if (Time.time >= _nextHeartbeat)
                {
                    heartbeat = Networking.GetServerTimeInSeconds();
                    _nextHeartbeat = Time.time + 0.3f;
                    RequestSerialization();
                }
            }
            if (!target.isLocal) return;
            if (!allowBeingPulled) { _ReleasePull(); return; }
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            _velocity = PullVelocity(delta, initialDistance + slack, responseSeconds, maxSpeed, maxAcceleration, _velocity, dt);
            pullStep = _velocity * dt;
            pullingLocal = true;
        }

        public void _BlockedByWorld() { _velocity = Vector3.zero; }

        public int IgnoredHandProbe(int otherPlayerId)
        {
            if (!HasLiveSession()) return -1;
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Utilities.IsValid(local)) return -1;
            if (local.playerId == grabberId && otherPlayerId == targetId) return BodyContactSystem.ProbeLeftHand + grabbingHand;
            if (pullingLocal && otherPlayerId == grabberId) return BodyContactSystem.ProbeLeftHand + targetArm;
            return -1;
        }
    }
}
