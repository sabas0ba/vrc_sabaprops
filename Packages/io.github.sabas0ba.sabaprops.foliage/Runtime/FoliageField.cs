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
    /// The authoring component is automatically omitted from builds; users do
    /// not need to remove it before building or uploading a world.
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

        [Tooltip(
            "地面として使う SkinnedMeshRenderer。生成時だけ現在のポーズをベイクして"
            + "一時的な MeshCollider を作り、それに対してレイキャストします。"
            + "Collider は生成後に破棄され、シーンには残りません。")]
        public List<SkinnedMeshRenderer> skinnedGround = new List<SkinnedMeshRenderer>();

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

        [Tooltip("Species と同じ並びの出現比率。0 以下、または未設定の要素は Species 側の Placement Weight を使います。")]
        public List<float> speciesWeights = new List<float>();

        [Header("Output")]
        public FoliageOutputMode outputMode = FoliageOutputMode.GpuInstanced;

        [Tooltip("チャンクの一辺 (m)。カリング粒度とドローコール数のトレードオフ。")]
        [Min(1f)] public float chunkSize = 12f;

        [HideInInspector] public bool autoRebuild = true;

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

        /// <summary>
        /// Placement weight for the species at <paramref name="index"/>, taken
        /// from this field when it sets one and from the species asset when it
        /// does not. A field that never touches these keeps behaving exactly as
        /// it did before per-field weights existed.
        /// </summary>
        public float PlacementWeightAt(int index)
        {
            if (species == null || index < 0 || index >= species.Count || species[index] == null)
            {
                return 0f;
            }

            // An annual is simply not there for part of the year. Handled as a
            // weight of zero rather than as a special case in the scatterer, so
            // the remaining species share the field's density between them the
            // way they would if it had never been listed.
            if (species[index].ActiveAppearance == SeasonAppearance.Absent)
            {
                return 0f;
            }

            if (speciesWeights != null && index < speciesWeights.Count && speciesWeights[index] > 0f)
            {
                return speciesWeights[index];
            }

            return Mathf.Max(0f, species[index].placementWeight);
        }

        /// <summary>Area extents in local space, as a half-size on X and Z.</summary>
        public Vector2 LocalExtents
        {
            get
            {
                return FoliageAreaUtility.LocalExtents(shape, size, radius);
            }
        }

        /// <summary>Area in square metres, used to turn density into a count.</summary>
        public float AreaSquareMeters
        {
            get
            {
                return FoliageAreaUtility.AreaSquareMeters(shape, size, radius);
            }
        }

        /// <summary>True when the local point lies inside the configured shape.</summary>
        public bool ContainsLocalPoint(float x, float z)
        {
            return FoliageAreaUtility.ContainsLocalPoint(shape, size, radius, x, z);
        }

        /// <summary>
        /// Maps a local XZ position to [0,1] mask coordinates across the area's
        /// bounding box.
        /// </summary>
        public Vector2 LocalPointToMaskUv(float x, float z)
        {
            return FoliageAreaUtility.LocalPointToMaskUv(shape, size, radius, x, z);
        }

        private void OnValidate()
        {
            ExcludeAuthoringComponentFromBuild();
            size.x = Mathf.Max(0.1f, size.x);
            size.y = Mathf.Max(0.1f, size.y);

            if (altitudeLimits.y < altitudeLimits.x)
            {
                altitudeLimits = new Vector2(altitudeLimits.y, altitudeLimits.x);
            }
        }

        private void Reset()
        {
            ExcludeAuthoringComponentFromBuild();
        }

        private void ExcludeAuthoringComponentFromBuild()
        {
            hideFlags |= HideFlags.DontSaveInBuild;
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
