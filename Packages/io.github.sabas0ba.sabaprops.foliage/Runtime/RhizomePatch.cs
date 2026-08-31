using System.Collections.Generic;
using UnityEngine;

namespace SabaProps.Foliage
{
    /// <summary>
    /// Authoring component for rhizome-connected ground-cover shoots. It is
    /// automatically omitted from builds; users do not need to remove it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class RhizomePatch : MonoBehaviour
    {
        [Tooltip("Primary Collider used for surface projection.")]
        public Collider targetSurface;

        [Tooltip("Adjacent Colliders that belong to the same ground surface.")]
        public List<Collider> additionalSurfaces = new List<Collider>();
        public Material material;
        public SurfaceGrowthSettings growth = new SurfaceGrowthSettings
        {
            mode = SurfaceGrowthMode.SurfaceCrawl,
            pathCount = 7,
            maxPathLength = 1.6f,
            coverage = 0.72f,
            branchesPerMetre = 1.1f,
            maxBranchDepth = 2,
            branchLength = 0.45f,
            gravityBias = 0f,
        };
        public RhizomePatchParams morphology = new RhizomePatchParams();

        [Tooltip("Local-space seed points for the underground graph.")]
        public List<Vector3> guidePoints = new List<Vector3> { Vector3.zero };

        [HideInInspector] public SurfaceGrowthGraph generatedGraph =
            new SurfaceGrowthGraph();
        [HideInInspector] public Mesh generatedMesh;
        [HideInInspector] public bool autoRebuild = true;

        private void Reset()
        {
            ExcludeAuthoringComponentFromBuild();
        }

        private void OnValidate()
        {
            ExcludeAuthoringComponentFromBuild();
        }

        private void ExcludeAuthoringComponentFromBuild()
        {
            hideFlags |= HideFlags.DontSaveInBuild;
        }
    }
}
