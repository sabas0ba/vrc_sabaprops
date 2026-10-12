using UdonSharp;
using UnityEngine;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace SabaProps.Liquid
{
    /// <summary>Local bone probes produce bounded contact events for all clients.</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [AddComponentMenu("SabaProps/Liquid/Contact")]
    public class LiquidContact : UdonSharpBehaviour
    {
        public LiquidCanvasPool pool;
        [Range(0.02f, 0.2f)] public float radius = 0.07f;
        [Range(0f, 1f)] public float transferFraction = 0.25f;
        [Range(0f, 1f)] public float smear = 0.6f;
        public bool includeFeet = true;
        public bool includeBody = true;
        private int[] _bones = { (int)HumanBodyBones.LeftHand, (int)HumanBodyBones.RightHand,
            (int)HumanBodyBones.LeftFoot, (int)HumanBodyBones.RightFoot, (int)HumanBodyBones.Chest, (int)HumanBodyBones.Head };
        private Vector3[] _previous = new Vector3[6];
        private int[] _targets = { -1, -1, -1, -1, -1, -1 };
        private int[] _probeOrder = { 0, 1, 0, 1, 2, 3, 0, 1, 4, 5 };
        private int _probe;
        private float _next;
        private int _sequence;

        private void Update()
        {
            if (pool == null || Time.time < _next) return;
            _next = Time.time + 0.06f;
            VRCPlayerApi player = Networking.LocalPlayer;
            if (!Utilities.IsValid(player)) return;
            int index = _probeOrder[_probe];
            _probe = (_probe + 1) % _probeOrder.Length;
            if ((index >= 2 && index <= 3 && !includeFeet) || (index >= 4 && !includeBody)) return;
            Vector3 position = player.GetBonePosition((HumanBodyBones)_bones[index]);
            if (position.sqrMagnitude < 1e-12f) { _targets[index] = -1; return; }
            float reach = radius * Mathf.Clamp(player.GetAvatarEyeHeightAsMeters() / 1.6f, 0.25f, 3f);
            // Six short rays also detect a stationary hand placed against a surface.
            for (int axis = 0; axis < 6; axis++)
            {
                Vector3 direction = axis < 2 ? Vector3.right : (axis < 4 ? Vector3.up : Vector3.forward);
                if (axis % 2 == 1) direction = -direction;
                int target = pool.CastTargets(position, direction, reach, false);
                if (target == -1) continue;
                Vector3 point = pool.lastHitPoint;
                Vector3 localPoint = pool.WorldToTarget(target, point);
                Vector3 motion = _targets[index] == target ? Vector3.ClampMagnitude(localPoint - _previous[index], reach) : Vector3.zero;
                _previous[index] = localPoint;
                _targets[index] = target;
                SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ReceiveContact), target, index,
                    localPoint, pool.WorldToTargetDirection(target, pool.lastHitNormal), motion, ++_sequence);
                return;
            }
            _targets[index] = -1;
        }

        [NetworkCallable(20)]
        public void ReceiveContact(int target, int probe, Vector3 localPoint, Vector3 localNormal, Vector3 localMotion, int sequence)
        {
            VRCPlayerApi player = NetworkCalling.CallingPlayer;
            if (!Utilities.IsValid(player) || pool == null || probe < 0 || probe >= _bones.Length || target == player.playerId) return;
            Vector3 point = pool.TargetToWorld(target, localPoint);
            Vector3 normal = pool.TargetToWorldDirection(target, localNormal).normalized;
            Vector3 bonePoint = player.GetBonePosition((HumanBodyBones)_bones[probe]);
            if (bonePoint.sqrMagnitude < 1e-12f || normal.sqrMagnitude < 0.5f) return;
            float size = radius * Mathf.Clamp(player.GetAvatarEyeHeightAsMeters() / 1.6f, 0.25f, 3f);
            // Allow network pose latency, but reject remote arbitrary surface edits.
            if (!(Vector3.Distance(point, bonePoint) <= size + 0.6f)) return;
            if (!pool.TryBeginContact()) return;
            LiquidBodyCanvas source = pool.CanvasForTarget(target);
            if (target >= 0)
            {
                LiquidBodyCanvas limb = pool.FindLimbAtPoint(target, point);
                if (limb != null) source = limb;
            }
            LiquidBodyCanvas receiver = probe == 4 ? pool.AcquireCanvas(player) : pool.LimbForPlayer(player, (HumanBodyBones)_bones[probe]);
            if (source == null || receiver == null) return;
            Vector3 contactPoint = bonePoint - normal * size * 0.5f;
            Vector3 motion = Vector3.ClampMagnitude(pool.TargetToWorldDirection(target, localMotion), size);
            receiver.TransferContact(source, contactPoint, point, -normal, Vector3.zero, size, transferFraction, 0f, sequence);
            source.TransferContact(receiver, point, contactPoint, normal, motion, size, transferFraction, smear, sequence);
        }

        public void HideLiquid() { if (pool != null) pool.SetLocalDisplay(false); }
        public void ShowLiquid() { if (pool != null) pool.SetLocalDisplay(true); }
    }
}
