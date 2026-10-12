using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.Liquid
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
    [AddComponentMenu("SabaProps/Liquid/Water Balloon")]
    public class LiquidWaterBalloon : UdonSharpBehaviour
    {
        public LiquidSplash splash;
        public VRC_Pickup pickup;
        public Rigidbody body;
        public Collider balloonCollider;
        public GameObject visual;
        public Transform respawnPoint;
        [Min(0.1f)] public float impactSpeed = 2f;
        [Min(1f)] public float respawnSeconds = 8f;
        [Tooltip("Use で手放し、押した時点の自分の足元へ投げます。通常の手投げも使えます。")]
        public bool throwAtFeetOnUse = true;
        [Range(0.3f, 1.5f)] public float throwFlightSeconds = 0.7f;
        [UdonSynced] private bool _burst;
        private bool _shownBurst;
        private float _restoreAt;
        private bool _launchPending;
        private Vector3 _launchTarget;

        public override void OnPickupUseDown()
        {
            if (throwAtFeetOnUse && pickup != null && pickup.IsHeld && Networking.IsOwner(gameObject)) ThrowAtFeet();
        }

        /// <summary>Also callable from the sample's button without first picking the balloon up.</summary>
        public void ThrowAtFeet()
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (_burst || _launchPending || body == null || !Utilities.IsValid(local)) return;
            if (pickup != null && pickup.IsHeld && !Networking.IsOwner(gameObject)) return;
            Networking.SetOwner(local, gameObject);
            float height = balloonCollider != null ? balloonCollider.bounds.extents.y : 0.08f;
            _launchTarget = local.GetPosition() + Vector3.up * Mathf.Max(0.02f, height);
            _launchPending = true;
            if (pickup != null && pickup.IsHeld) pickup.Drop();
            // The pickup sets its release velocity while dropping. Apply the throw afterwards.
            SendCustomEventDelayedFrames(nameof(_LaunchAfterDrop), 2);
        }

        public void _LaunchAfterDrop()
        {
            if (!_launchPending) return;
            _launchPending = false;
            if (_burst || !Networking.IsOwner(gameObject) || body == null || (pickup != null && pickup.IsHeld)) return;
            body.isKinematic = false;
            body.useGravity = true;
            body.angularVelocity = Vector3.zero;
            body.velocity = ThrowVelocity(body.position, _launchTarget, Physics.gravity, throwFlightSeconds);
        }

        public static Vector3 ThrowVelocity(Vector3 origin, Vector3 target, Vector3 gravity, float seconds)
        {
            float time = Mathf.Clamp(seconds, 0.3f, 1.5f);
            return (target - origin) / time - gravity * (0.5f * time);
        }

        public void OnCollisionEnter(Collision collision)
        {
            if (_burst || !Networking.IsOwner(gameObject) || collision.relativeVelocity.magnitude < impactSpeed) return;
            if (pickup != null && pickup.IsHeld) return;
            if (splash != null) splash.Emit(transform.position);
            _burst = true;
            _launchPending = false;
            _restoreAt = Time.time + Mathf.Max(respawnSeconds, 1f);
            ApplyState();
        }

        private void Update()
        {
            if (_shownBurst != _burst) ApplyState();
            if (!_burst || !Networking.IsOwner(gameObject)) return;
            if (_restoreAt <= 0f) _restoreAt = Time.time + Mathf.Max(respawnSeconds, 1f);
            if (Time.time < _restoreAt) return;
            if (respawnPoint != null) transform.SetPositionAndRotation(respawnPoint.position, respawnPoint.rotation);
            if (body != null) { body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            _burst = false;
            _restoreAt = 0f;
            ApplyState();
        }

        public override void OnDeserialization() { ApplyState(); }

        private void ApplyState()
        {
            _shownBurst = _burst;
            if (visual != null) visual.SetActive(!_burst);
            if (balloonCollider != null) balloonCollider.enabled = !_burst;
            if (body != null) body.isKinematic = _burst;
        }
    }
}
