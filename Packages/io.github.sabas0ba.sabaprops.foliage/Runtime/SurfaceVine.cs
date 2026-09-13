using System.Collections.Generic;
using UnityEngine;

namespace SabaProps.Foliage
{
    /// <summary>
    /// Authoring component for vines that follow a Collider surface. It is
    /// automatically omitted from builds; users do not need to remove it.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SurfaceVine : MonoBehaviour
    {
        [Tooltip("Primary Collider used for surface projection.")]
        public Collider targetSurface;

        [Tooltip("Adjacent Colliders that form one continuous growth surface, such as a floor, slope, and wall.")]
        public List<Collider> additionalSurfaces = new List<Collider>();
        public Material material;
        public SurfaceGrowthSettings growth = new SurfaceGrowthSettings();
        public SurfaceVineParams morphology = new SurfaceVineParams();

        [Tooltip("Local-space guide points. ProjectedSpline follows all points; SurfaceCrawl uses them as seeds.")]
        public List<Vector3> guidePoints = new List<Vector3>
        {
            Vector3.zero,
            new Vector3(0f, 1.2f, 0f),
            new Vector3(0.35f, 2.2f, 0f),
        };

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
