using UnityEngine;

namespace SabaProps.Foliage
{
    /// <summary>
    /// Marker left on every generated chunk. Carries just enough information for
    /// the inspector to report statistics and for a rebuild to recognise its own
    /// output, so hand-placed objects parented under a field are never deleted.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class FoliageChunk : MonoBehaviour
    {
        public Vector2Int coordinate;
        public int instanceCount;
        public int triangleCount;

        [Tooltip("このチャンクを生成した FoliageField の BuildId。")]
        public string ownerBuildId;
    }
}
