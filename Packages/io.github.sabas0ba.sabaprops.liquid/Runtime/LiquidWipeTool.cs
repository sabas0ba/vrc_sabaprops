using UdonSharp;
using UnityEngine;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace SabaProps.Liquid
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [AddComponentMenu("SabaProps/Liquid/Wipe Tool")]
    public class LiquidWipeTool : UdonSharpBehaviour
    {
        public LiquidCanvasPool pool;
        public VRC_Pickup pickup;
        public Transform tip;
        public LiquidBodyCanvas clothCanvas;
        [Range(0.01f, 0.3f)] public float radius = 0.09f;
        [Range(0f, 1f)] public float absorption = 0.85f;
        [Range(0f, 1f)] public float redeposit = 0.04f;
        private float _next;
        private int _target = -1;
        private Vector3 _previous;
        private int _sequence;

        private void Update()
        {
            if (pickup == null || !pickup.IsHeld || !Networking.IsOwner(pickup.gameObject)) return;
            WipeOnce();
        }

        /// <summary>A single wipe, also usable by a world-controlled cleaning prop.</summary>
        public void WipeOnce()
        {
            if (pool == null || tip == null || !Networking.IsOwner(pickup != null ? pickup.gameObject : gameObject)) return;
            if (Time.time < _next) return;
            _next = Time.time + 0.11f;
            int target = pool.CastTargets(tip.position, tip.forward, radius, false);
            if (target == -1) { _target = -1; return; }
            Vector3 point = pool.lastHitPoint;
            Vector3 localPoint = pool.WorldToTarget(target, point);
            Vector3 motion = _target == target ? Vector3.ClampMagnitude(localPoint - _previous, radius) : Vector3.zero;
            _target = target;
            _previous = localPoint;
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ReceiveWipe), target,
                localPoint, pool.WorldToTargetDirection(target, pool.lastHitNormal), motion, ++_sequence);
        }

        [NetworkCallable(10)]
        public void ReceiveWipe(int target, Vector3 localPoint, Vector3 localNormal, Vector3 localMotion, int sequence)
        {
            VRCPlayerApi sender = NetworkCalling.CallingPlayer;
            if (pool == null || tip == null || !Utilities.IsValid(sender) || !Networking.IsOwner(sender, pickup != null ? pickup.gameObject : gameObject)) return;
            Vector3 point = pool.TargetToWorld(target, localPoint);
            if (!(Vector3.Distance(point, tip.position) < radius + 0.6f)) return;
            if (!pool.TryBeginContact()) return;
            LiquidBodyCanvas canvas = pool.CanvasForTarget(target);
            if (target >= 0)
            {
                LiquidBodyCanvas limb = pool.FindLimbAtPoint(target, point);
                if (limb != null) canvas = limb;
            }
            if (canvas == null) return;
            Vector3 normal = pool.TargetToWorldDirection(target, localNormal).normalized;
            Vector3 motion = Vector3.ClampMagnitude(pool.TargetToWorldDirection(target, localMotion), radius);
            if (clothCanvas != null)
                clothCanvas.TransferContact(canvas, tip.position, point, -normal, Vector3.zero, radius, redeposit, 0f, sequence);
            canvas.TransferContact(clothCanvas, point, tip.position, normal, motion, radius, absorption, 0.5f, sequence);
            // The tool also removes dry pigment and liquid after picking up a fraction.
            canvas.QueueShape(point - motion, point, normal, radius, null, LiquidBodyCanvas.ShapeStroke, 0f, absorption);
        }
    }
}
