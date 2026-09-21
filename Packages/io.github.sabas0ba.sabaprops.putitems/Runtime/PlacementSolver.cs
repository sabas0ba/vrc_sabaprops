using UdonSharp;
using UnityEngine;

namespace SabaProps.PutItems
{
    /// <summary>同期と Transform の更新を行わず、静止面への配置候補を計算します。</summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class PlacementSolver : UdonSharpBehaviour
    {
        public PlacementSurface[] surfaces = new PlacementSurface[0];
        [Min(0f)] public float maximumDistance = 0.12f;
        [Min(0f)] public float maximumPenetration = 0.02f;
        [Range(0f, 180f)] public float maximumTilt = 70f;
        [Tooltip("接触点を中心とする占有円の半径 (m)。0 は一点だけで範囲を判定します。")]
        [Min(0f)] public float footprintRadius;

        [HideInInspector] public bool hasResult;
        [HideInInspector] public Vector3 resultPosition;
        [HideInInspector] public Quaternion resultRotation;
        [HideInInspector] public PlacementSurface resultSurface;

        // 結果は次の呼び出しまで有効。呼び出し側が適用直前に所有権を確認します。
        public bool TryFindPose(Transform item, Transform contact, int category)
        {
            hasResult = false;
            resultSurface = null;
            resultPosition = Vector3.zero;
            resultRotation = Quaternion.identity;
            if (item == null || contact == null || surfaces == null || category == 0
                || !isActiveAndEnabled || maximumDistance < 0f || maximumPenetration < 0f
                || footprintRadius < 0f || maximumTilt < 0f || maximumTilt > 180f)
                return false;
            if (contact != item && !contact.IsChildOf(item)) return false;

            // 非一様スケール下での回転による shear は配置計算の対象外です。
            Vector3 itemScale = item.lossyScale;
            if (!IsUniformPositive(itemScale)) return false;

            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < surfaces.Length; i++)
            {
                PlacementSurface surface = surfaces[i];
                if (surface == null || !surface.isActiveAndEnabled
                    || (surface.acceptedCategories & category) == 0) continue;
                Transform plane = surface.transform;
                if (plane == item || plane.IsChildOf(item)) continue;
                Vector3 scale = plane.lossyScale;
                if (!IsUniformPositive(scale) || surface.size.x <= 0f || surface.size.y <= 0f) continue;

                Vector3 normal = plane.up;
                float signedDistance = Vector3.Dot(contact.position - plane.position, normal);
                if (signedDistance < -maximumPenetration || signedDistance > maximumDistance) continue;
                if (Vector3.Angle(contact.up, normal) > maximumTilt) continue;
                Vector3 projected = contact.position - normal * signedDistance;
                Vector3 local = plane.InverseTransformPoint(projected);
                float margin = footprintRadius / scale.x;
                if (Mathf.Abs(local.x) + margin > surface.size.x * 0.5f
                    || Mathf.Abs(local.z) + margin > surface.size.y * 0.5f) continue;
                float distance = Mathf.Abs(signedDistance);
                if (distance >= bestDistance) continue;

                Quaternion correction = Quaternion.FromToRotation(contact.up, normal);
                resultRotation = correction * item.rotation;
                resultPosition = projected - correction * (contact.position - item.position);
                resultSurface = surface;
                bestDistance = distance;
                hasResult = true;
            }
            return hasResult;
        }

        private bool IsUniformPositive(Vector3 scale)
        {
            return scale.x > 0f && Mathf.Abs(scale.x - scale.y) < 0.0001f
                && Mathf.Abs(scale.x - scale.z) < 0.0001f;
        }
    }
}
