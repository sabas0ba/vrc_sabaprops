using System;
using System.Collections.Generic;
using UnityEngine;

namespace SabaProps.Foliage
{
    /// <summary>How generated instances are turned into renderers.</summary>
    public enum FoliageOutputMode
    {
        /// <summary>
        /// One GameObject per instance, all sharing a mesh and material so Unity
        /// batches them with GPU instancing. Keeps per-instance frustum culling
        /// and distance shrink, at the cost of one transform per instance.
        /// </summary>
        GpuInstanced = 0,

        /// <summary>
        /// Instances are welded into one mesh per chunk and species. Fewest draw
        /// calls and almost no CPU cost, but the whole chunk is culled as a unit.
        /// </summary>
        MergedChunks = 1,
    }

    /// <summary>Shape of the scatter area, in the field's local XZ plane.</summary>
    public enum FoliageAreaShape
    {
        Rectangle = 0,
        Circle = 1,
    }

    /// <summary>Result summary of the last build, shown in the inspector.</summary>
    [Serializable]
    public class FoliageBuildStats
    {
        public int instanceCount;
        public int rendererCount;
        public int chunkCount;
        public int triangleCount;
        public int vertexCount;
        public float buildSeconds;
        public FoliageOutputMode mode;

        /// <summary>
        /// Rough lower bound on draw calls. In instanced mode Unity packs up to
        /// ~500 instances per batch on most hardware, one batch per mesh.
        /// </summary>
        public int EstimatedDrawCalls
        {
            get
            {
                if (mode == FoliageOutputMode.MergedChunks)
                {
                    return rendererCount;
                }

                return Mathf.Max(1, Mathf.CeilToInt(instanceCount / 500f));
            }
        }
    }

    /// <summary>
    /// Defines an area to scatter foliage over, and owns the hierarchy that the
    /// editor tooling generates underneath it.
    /// <para>
    /// This component intentionally has no runtime logic. VRChat worlds and
    /// avatars cannot execute C#, so everything is baked at edit time and what
    /// ships is plain MeshRenderers driven by an instancing-aware shader.
    /// </para>
    /// </summary>
    [AddComponentMenu("SabaProps/Foliage Field")]
    [DisallowMultipleComponent]
    public class FoliageField : MonoBehaviour
    {
        /// <summary>Name of the generated container object parented to this field.</summary>
        public const string GeneratedRootName = "GeneratedFoliage";

        [Header("Area")]
        public FoliageAreaShape shape = FoliageAreaShape.Rectangle;

        [Tooltip("矩形エリアのサイズ (m)。")]
        public Vector2 size = new Vector2(20f, 20f);

        [Tooltip("円形エリアの半径 (m)。")]
        [Min(0.1f)] public float radius = 10f;

        [Header("Density")]
        [Tooltip("1 平方メートルあたりの個体数。")]
        [Min(0.001f)] public float density = 8f;

        [Tooltip("同じシードなら何度ビルドしても同じ配置になります。")]
        public int seed = 12345;

        [Tooltip("安全弁。この数を超えるとビルドを中断します。")]
        [Min(1)] public int maxInstances = 40000;

        [Header("Ground")]
        [Tooltip("地面として扱うレイヤー。コライダーが必要です。")]
        public LayerMask groundLayers = ~0;

        [Tooltip("レイキャストの開始高さ（エリア原点からの相対 m）。")]
        [Min(0f)] public float raycastHeight = 30f;

        [Tooltip("レイキャストの到達距離 (m)。")]
        [Min(0.1f)] public float raycastDistance = 80f;

        [Tooltip("OFF にすると地面が無い場所ではエリア平面に配置します。")]
        public bool requireGroundHit = true;

        [Tooltip("配置を許可する高度 (m, ワールド Y)。")]
        public Vector2 altitudeLimits = new Vector2(-10000f, 10000f);

        [Tooltip("地面から少し浮かせる／埋める量 (m)。")]
        public float groundOffset = -0.01f;

        [Header("Exclusion")]
        [Tooltip("このレイヤーのコライダー付近には配置しません。Nothing で無効。")]
        public LayerMask exclusionLayers = 0;

        [Tooltip("除外コライダーからの距離 (m)。")]
        [Min(0f)] public float exclusionRadius = 0.3f;

        [Header("Density Mask")]
        [Tooltip("エリアに投影されるグレースケールマスク。Read/Write Enabled が必要です。")]
        public Texture2D densityMask;

        [Tooltip("マスク値がこれ未満の場所には配置しません。")]
        [Range(0f, 1f)] public float densityMaskThreshold = 0.05f;

        public bool invertDensityMask = false;

        [Header("Species")]
        public List<FoliageSpecies> species = new List<FoliageSpecies>();

        [Header("Output")]
        public FoliageOutputMode outputMode = FoliageOutputMode.GpuInstanced;

        [Tooltip("チャンクの一辺 (m)。カリング粒度とドローコール数のトレードオフ。")]
        [Min(1f)] public float chunkSize = 12f;

        [Header("State (read only)")]
        [SerializeField] private string buildId;

        public Transform generatedRoot;
        public FoliageBuildStats lastBuildStats;

        /// <summary>
        /// Stable identifier used to name the folder that merged chunk meshes are
        /// written to. Generated once and then serialised, so renaming the object
        /// or moving it between scenes does not orphan its assets.
        /// </summary>
        public string BuildId
        {
            get
            {
                if (string.IsNullOrEmpty(buildId))
                {
                    buildId = Guid.NewGuid().ToString("N").Substring(0, 12);
                }

                return buildId;
            }
        }

        /// <summary>Area extents in local space, as a half-size on X and Z.</summary>
        public Vector2 LocalExtents
        {
            get
            {
                return shape == FoliageAreaShape.Circle
                    ? new Vector2(radius, radius)
                    : new Vector2(Mathf.Abs(size.x) * 0.5f, Mathf.Abs(size.y) * 0.5f);
            }
        }

        /// <summary>Area in square metres, used to turn density into a count.</summary>
        public float AreaSquareMeters
        {
            get
            {
                return shape == FoliageAreaShape.Circle
                    ? Mathf.PI * radius * radius
                    : Mathf.Abs(size.x) * Mathf.Abs(size.y);
            }
        }

        /// <summary>True when the local point lies inside the configured shape.</summary>
        public bool ContainsLocalPoint(float x, float z)
        {
            if (shape == FoliageAreaShape.Circle)
            {
                return (x * x + z * z) <= radius * radius;
            }

            Vector2 extents = LocalExtents;
            return Mathf.Abs(x) <= extents.x && Mathf.Abs(z) <= extents.y;
        }

        /// <summary>
        /// Maps a local XZ position to [0,1] mask coordinates across the area's
        /// bounding box.
        /// </summary>
        public Vector2 LocalPointToMaskUv(float x, float z)
        {
            Vector2 extents = LocalExtents;
            return new Vector2(
                Mathf.InverseLerp(-extents.x, extents.x, x),
                Mathf.InverseLerp(-extents.y, extents.y, z));
        }

        private void OnValidate()
        {
            size.x = Mathf.Max(0.1f, size.x);
            size.y = Mathf.Max(0.1f, size.y);

            if (altitudeLimits.y < altitudeLimits.x)
            {
                altitudeLimits = new Vector2(altitudeLimits.y, altitudeLimits.x);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.4f, 0.9f, 0.35f, 0.9f);

            if (shape == FoliageAreaShape.Rectangle)
            {
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(Mathf.Abs(size.x), 0f, Mathf.Abs(size.y)));
            }
            else
            {
                const int segments = 48;
                Vector3 previous = new Vector3(radius, 0f, 0f);
                for (int i = 1; i <= segments; i++)
                {
                    float angle = i / (float)segments * Mathf.PI * 2f;
                    Vector3 current = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    Gizmos.DrawLine(previous, current);
                    previous = current;
                }
            }

            // Show where ground rays start so mis-set raycastHeight is obvious.
            Gizmos.color = new Color(0.4f, 0.9f, 0.35f, 0.25f);
            Vector2 extents = LocalExtents;
            Gizmos.DrawWireCube(
                new Vector3(0f, raycastHeight * 0.5f, 0f),
                new Vector3(extents.x * 2f, raycastHeight, extents.y * 2f));
        }
    }
}
