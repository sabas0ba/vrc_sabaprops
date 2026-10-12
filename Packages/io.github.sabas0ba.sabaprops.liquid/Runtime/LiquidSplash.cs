using UdonSharp;
using UnityEngine;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace SabaProps.Liquid
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [AddComponentMenu("SabaProps/Liquid/Splash")]
    public class LiquidSplash : UdonSharpBehaviour
    {
        public LiquidCanvasPool pool;
        public LiquidProfile profile;
        public ParticleSystem particles;
        public Collider emissionVolume;
        public GameObject authority;
        [Range(1, 48)] public int rays = 16;
        [Min(0.05f)] public float range = 1.2f;
        [Range(0.01f, 0.3f)] public float markRadius = 0.06f;
        [Range(0.01f, 2f)] public float amount = 0.2f;
        private float _next;
        private int _received;
        public int GetReceivedSplashes() { return _received; }

        public void Emit(Vector3 centre)
        {
            if (Time.time < _next || pool == null || profile == null) return;
            _next = Time.time + 0.3f;
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ReceiveSplash), centre, Networking.GetServerTimeInMilliseconds());
        }

        [NetworkCallable(4)]
        public void ReceiveSplash(Vector3 centre, int seed)
        {
            VRCPlayerApi sender = NetworkCalling.CallingPlayer;
            if (pool == null || profile == null || !Utilities.IsValid(sender)) return;
            if (authority != null && !Networking.IsOwner(sender, authority)) return;
            if (emissionVolume != null)
            {
                Bounds bounds = emissionVolume.bounds;
                bounds.Expand(0.1f);
                if (!bounds.Contains(centre) || !(Vector3.Distance(centre, sender.GetPosition()) < 1f)) return;
            }
            else if (!(Vector3.Distance(centre, transform.position) < Mathf.Max(range, 2f))) return;
            int count = Mathf.Clamp(rays, 1, 48);
            _received++;
            float phase = (seed & 1023) * (Mathf.PI * 2f / 1024f);
            for (int i = 0; i < count; i++)
            {
                float y = 1f - 2f * (i + 0.5f) / count;
                float ring = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                float angle = phase + i * 2.39996323f;
                Vector3 direction = new Vector3(Mathf.Cos(angle) * ring, y, Mathf.Sin(angle) * ring);
                int target = pool.CastTargets(centre, direction, range, true);
                LiquidBodyCanvas canvas = pool.CanvasForTarget(target);
                if (canvas != null) canvas.QueueStamp(pool.lastHitPoint, pool.lastHitNormal, markRadius, profile, amount / count, seed + i);
            }
            if (particles != null) { particles.transform.position = centre; particles.Emit(count); }
        }
    }
}
