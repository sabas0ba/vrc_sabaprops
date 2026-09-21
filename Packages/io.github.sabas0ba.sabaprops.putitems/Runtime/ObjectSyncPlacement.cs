using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace SabaProps.PutItems
{
    /// <summary>同じ GameObject の Pickup / Object Sync を通して配置を適用します。</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [RequireComponent(typeof(VRCObjectSync), typeof(VRCPickup), typeof(Rigidbody))]
    public class ObjectSyncPlacement : UdonSharpBehaviour
    {
        public PlacementSolver solver;
        [Tooltip("底面または背面の接触点。+Y を Prop 内側に向けます。")]
        public Transform contact;
        public int category = 1;
        public bool removeNetworkSmoothing = true;
        [Tooltip("再取得・リセット時に戻す通常の物理状態。")]
        public bool normalKinematic;
        public bool normalGravity = true;
        [Tooltip("移動面に置いた場合の接続状態。別の子 GameObject に配置します。")]
        public PlacementFollowState followState;
        [Tooltip("この Prop に置ける子孫の Prop。入れ子の場合は孫も登録します。")]
        public ObjectSyncPlacement[] carriedItems = new ObjectSyncPlacement[0];

        private VRCObjectSync objectSync;
        private VRCPickup pickup;
        private Rigidbody body;
        private bool pending;
        private int dropFrame;

        private void Start()
        {
            CacheComponents();
            ClaimCarriedItems();
        }

        private void CacheComponents()
        {
            if (objectSync == null) objectSync = GetComponent<VRCObjectSync>();
            if (pickup == null) pickup = GetComponent<VRCPickup>();
            if (body == null) body = GetComponent<Rigidbody>();
        }

        public override void OnDrop()
        {
            pending = Networking.IsOwner(gameObject);
            dropFrame = Time.frameCount;
        }

        private void Update()
        {
            if (!pending || Time.frameCount <= dropFrame) return;
            pending = false;
            TryPlace();
        }

        private void LateUpdate()
        {
            if (!Networking.IsOwner(gameObject) || pickup == null || pickup.IsHeld
                || followState == null || !followState.TryFindPose()) return;
            transform.SetPositionAndRotation(followState.resultPosition, followState.resultRotation);
        }

        public bool TryPlace()
        {
            CacheComponents();
            if (!Networking.IsOwner(gameObject) || pickup == null || pickup.IsHeld
                || body == null || objectSync == null || solver == null) return false;
            if (!solver.TryFindPose(transform, contact, category)) return false;
            if (!Networking.IsOwner(gameObject) || pickup.IsHeld) return false;

            // Drop による投射速度を残さず、保持状態も Object Sync に委ねます。
            if (!body.isKinematic)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            objectSync.SetKinematic(true);
            transform.SetPositionAndRotation(solver.resultPosition, solver.resultRotation);
            if (followState != null) followState.SetSurface(solver.resultSurface);
            if (removeNetworkSmoothing) objectSync.FlagDiscontinuity();
            return true;
        }

        public override void OnPickup()
        {
            CancelAndRestorePhysics();
            ClaimCarriedItems();
        }

        // 既存の Respawn / Pool 制御は位置を戻す前に呼び出してください。
        public void CancelAndRestorePhysics()
        {
            pending = false;
            CacheComponents();
            if (!Networking.IsOwner(gameObject) || objectSync == null) return;
            if (followState != null) followState.SetSurface(null);
            objectSync.SetGravity(normalGravity);
            objectSync.SetKinematic(normalKinematic);
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player)
        {
            pending = false;
            // Pickup と所有権通知の前後関係に依存せず、取得者の通常状態を復元します。
            CacheComponents();
            if (Networking.IsOwner(gameObject) && pickup != null && pickup.IsHeld)
                CancelAndRestorePhysics();
            ClaimCarriedItems();
        }

        private void ClaimCarriedItems()
        {
            if (!Networking.IsOwner(gameObject) || carriedItems == null) return;
            for (int i = 0; i < carriedItems.Length; i++)
            {
                ObjectSyncPlacement item = carriedItems[i];
                if (item == null || item == this || !item.IsCarriedBy(this)) continue;
                VRCPickup childPickup = item.GetComponent<VRCPickup>();
                if (childPickup != null && childPickup.IsHeld) continue;
                if (!Networking.IsOwner(item.gameObject))
                    Networking.SetOwner(Networking.LocalPlayer, item.gameObject);
            }
        }

        public bool IsCarriedBy(ObjectSyncPlacement ancestor)
        {
            ObjectSyncPlacement current = this;
            for (int depth = 0; depth < 16; depth++)
            {
                if (current.followState == null) return false;
                current = current.followState.GetCarrier();
                if (current == null) return false;
                if (current == ancestor) return true;
                if (current == this) return false;
            }
            return false;
        }

        private void OnDisable()
        {
            pending = false;
        }
    }
}
