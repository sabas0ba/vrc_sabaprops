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
        [UdonSynced] private bool _burst;
        private bool _shownBurst;
        private float _restoreAt;

        public void OnCollisionEnter(Collision collision)
        {
            if (_burst || !Networking.IsOwner(gameObject) || collision.relativeVelocity.magnitude < impactSpeed) return;
            if (pickup != null && pickup.IsHeld) return;
            if (splash != null) splash.Emit(transform.position);
            _burst = true;
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
