using System;
using UnityEngine;

namespace SabaProps.Flock
{
    /// <summary>Swarm-level parameters baked into the generated mesh.</summary>
    [Serializable]
    public sealed class FlockSwarmSettings
    {
        public FlockPattern pattern = FlockPattern.Cruise;

        [Tooltip("個体数")]
        [Range(1, 1000)]
        public int count = 40;

        [Tooltip("群れが動き回る範囲の半径 (m)。GameObject の位置を中心とする。")]
        public Vector3 area = new Vector3(60f, 15f, 60f);

        [Tooltip("種の巡航速度に掛ける倍率")]
        [Min(0f)]
        public float speedScale = 1f;

        [Tooltip("群れの半径 (m)。0 で個体数と種の大きさから自動で決める。")]
        [Min(0f)]
        public float clusterRadius = 0f;

        [Tooltip("個体の乱数と時刻のずれを決める seed。同じ seed なら同じ mesh になる。")]
        public int seed = 1;

        public FlockDetail detail = FlockDetail.Low;

        public FlockLodMode lodMode = FlockLodMode.LodGroup;

        [Tooltip("LODGroup で各段階へ切り替える、1 個体の大きさの画面占有率 (High, Low, Silhouette)。Silhouette を下回ると描画しない。")]
        public Vector3 lodTransitions = new Vector3(0.03f, 0.006f, 0.0008f);

        public FlockSwarmSettings Clone()
        {
            return (FlockSwarmSettings)MemberwiseClone();
        }
    }
}
