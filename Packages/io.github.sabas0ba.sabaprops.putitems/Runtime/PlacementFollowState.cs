using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.PutItems
{
    /// <summary>移動面との接続と相対姿勢を、Object Sync とは別の GameObject で同期します。</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PlacementFollowState : UdonSharpBehaviour
    {
        public ObjectSyncPlacement placement;
        [UdonSynced] public int surfaceIndex = -1;
        [UdonSynced] public Vector3 localPosition;
        [UdonSynced] public Quaternion localRotation = Quaternion.identity;

        [HideInInspector] public Vector3 resultPosition;
        [HideInInspector] public Quaternion resultRotation;
        private bool pendingSerialization;
        private int pendingSurfaceIndex;
        private Vector3 pendingLocalPosition;
        private Quaternion pendingLocalRotation;

        public ObjectSyncPlacement GetCarrier()
        {
            if (placement == null || placement.solver == null || placement.solver.surfaces == null
                || surfaceIndex < 0 || surfaceIndex >= placement.solver.surfaces.Length) return null;
            PlacementSurface surface = placement.solver.surfaces[surfaceIndex];
            if (surface == null || surface.carrier == placement) return null;
            return surface.carrier;
        }

        // 親も別の面に接続されている場合、相対姿勢を上へ合成して更新順による1フレームの遅れを防ぎます。
        public bool TryFindPose()
        {
            ObjectSyncPlacement carrier = GetCarrier();
            if (carrier == null) return false;
            Vector3 position = localPosition;
            Quaternion rotation = localRotation;
            for (int depth = 0; depth < 16; depth++)
            {
                PlacementFollowState parent = carrier.followState;
                ObjectSyncPlacement next = parent == null ? null : parent.GetCarrier();
                if (next == null)
                {
                    resultPosition = carrier.transform.TransformPoint(position);
                    resultRotation = carrier.transform.rotation * rotation;
                    return true;
                }
                position = parent.localPosition + parent.localRotation * position;
                rotation = parent.localRotation * rotation;
                carrier = next;
                if (carrier == placement) return false;
            }
            return false;
        }

        public void SetSurface(PlacementSurface surface)
        {
            if (placement == null || !Networking.IsOwner(placement.gameObject)) return;
            surfaceIndex = -1;
            if (surface != null && surface.carrier != null && surface.carrier != placement
                && placement.solver != null && placement.solver.surfaces != null)
            {
                for (int i = 0; i < placement.solver.surfaces.Length; i++)
                {
                    if (placement.solver.surfaces[i] != surface) continue;
                    surfaceIndex = i;
                    localPosition = surface.carrier.transform.InverseTransformPoint(placement.transform.position);
                    localRotation = Quaternion.Inverse(surface.carrier.transform.rotation) * placement.transform.rotation;
                    break;
                }
            }
            pendingSurfaceIndex = surfaceIndex;
            pendingLocalPosition = localPosition;
            pendingLocalRotation = localRotation;
            pendingSerialization = true;
            if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
            FlushPending();
        }

        private void Update()
        {
            FlushPending();
        }

        private void FlushPending()
        {
            if (!pendingSerialization) return;
            // 所有権移行中に古い同期値を受信しても、確定したドロップ結果を保持します。
            surfaceIndex = pendingSurfaceIndex;
            localPosition = pendingLocalPosition;
            localRotation = pendingLocalRotation;
            if (!Networking.IsOwner(gameObject)) return;
            RequestSerialization();
            pendingSerialization = false;
        }
    }
}
