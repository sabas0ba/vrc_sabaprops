using System;
using System.Collections.Generic;
using SabaProps.Foliage;
using UnityEngine;

namespace SabaProps.Trees
{
    [Serializable]
    public sealed class TreeBuildStats
    {
        public int instanceCount;
        public int rendererCount;
        public int lod0TriangleCount;
        public int lod0VertexCount;
        public float buildSeconds;
    }

    /// <summary>
    /// Area that bakes TreeSpecies into ordinary LODGroups at edit time.
    /// No C# executes in a built VRChat world.
    /// The authoring component is automatically omitted from builds; users do
    /// not need to remove it before building or uploading a world.
    /// </summary>
    [AddComponentMenu("SabaProps/Tree Field")]
    [DisallowMultipleComponent]
    public sealed class TreeField : MonoBehaviour
    {
        public const string GeneratedRootName = "GeneratedTrees";

        [Header("Area")]
        public FoliageAreaShape shape = FoliageAreaShape.Rectangle;
        public Vector2 size = new Vector2(30f, 30f);
        [Min(0.1f)] public float radius = 15f;

        [Header("Density")]
        [Min(0.0001f)] public float density = 0.08f;
        public int seed = 24680;
        [Min(1)] public int maxInstances = 2000;

        [Header("Ground")]
        public LayerMask groundLayers = ~0;
        [Min(0f)] public float raycastHeight = 40f;
        [Min(0.1f)] public float raycastDistance = 100f;
        public bool requireGroundHit = true;
        public Vector2 altitudeLimits = new Vector2(-10000f, 10000f);
        public float groundOffset;

        [Header("Exclusion")]
        public LayerMask exclusionLayers;
        [Min(0f)] public float exclusionRadius = 0.5f;

        [Header("Density Mask")]
        public Texture2D densityMask;
        [Range(0f, 1f)] public float densityMaskThreshold = 0.05f;
        public bool invertDensityMask;

        [Header("Species")]
        public List<TreeSpecies> species = new List<TreeSpecies>();
        public List<float> speciesWeights = new List<float>();

        [HideInInspector] public bool autoRebuild = true;

        [Header("State (read only)")]
        public Transform generatedRoot;
        public TreeBuildStats lastBuildStats;

        public Vector2 LocalExtents =>
            FoliageAreaUtility.LocalExtents(shape, size, radius);

        public float AreaSquareMeters =>
            FoliageAreaUtility.AreaSquareMeters(shape, size, radius);

        public float PlacementWeightAt(int index)
        {
            if (species == null || index < 0 || index >= species.Count ||
                species[index] == null)
            {
                return 0f;
            }

            if (speciesWeights != null && index < speciesWeights.Count &&
                speciesWeights[index] > 0f)
            {
                return speciesWeights[index];
            }
            return Mathf.Max(0f, species[index].placement.placementWeight);
        }

        private void OnValidate()
        {
            ExcludeAuthoringComponentFromBuild();
            size.x = Mathf.Max(0.1f, size.x);
            size.y = Mathf.Max(0.1f, size.y);
            radius = Mathf.Max(0.1f, radius);
            density = Mathf.Max(0.0001f, density);
            maxInstances = Mathf.Max(1, maxInstances);
            raycastHeight = Mathf.Max(0f, raycastHeight);
            raycastDistance = Mathf.Max(0.1f, raycastDistance);
            exclusionRadius = Mathf.Max(0f, exclusionRadius);
            densityMaskThreshold = Mathf.Clamp01(densityMaskThreshold);
            if (altitudeLimits.y < altitudeLimits.x)
            {
                altitudeLimits = new Vector2(
                    altitudeLimits.y, altitudeLimits.x);
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
            Gizmos.color = new Color(0.22f, 0.72f, 0.95f, 0.9f);
            if (shape == FoliageAreaShape.Rectangle)
            {
                Gizmos.DrawWireCube(
                    Vector3.zero,
                    new Vector3(Mathf.Abs(size.x), 0f, Mathf.Abs(size.y)));
                return;
            }

            const int segments = 48;
            Vector3 previous = new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vector3 current = new Vector3(
                    Mathf.Cos(angle) * radius, 0f,
                    Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previous, current);
                previous = current;
            }
        }
    }
}
