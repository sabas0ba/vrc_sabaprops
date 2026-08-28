using System;
using System.Collections.Generic;
using UnityEngine;

namespace SabaProps.Foliage
{
    /// <summary>How the authoring component creates its surface path.</summary>
    public enum SurfaceGrowthMode
    {
        /// <summary>Interpolates the guide points, then projects the curve onto the collider.</summary>
        ProjectedSpline = 0,

        /// <summary>Walks over the collider using a deterministic random tangent direction.</summary>
        SurfaceCrawl = 1,
    }

    /// <summary>Low-poly profiles based on common climbing and creeping plants.</summary>
    public enum SurfaceLeafShape
    {
        Cordate = 0,
        Lobed = 1,
        Ovate = 2,
        Orbicular = 3,
    }

    /// <summary>How leaves are attached along one surface stem.</summary>
    public enum SurfaceLeafArrangement
    {
        Alternate = 0,
        Opposite = 1,
        Whorled = 2,
        Random = 3,
    }

    /// <summary>Parts of a leaf that receive the secondary pigment colour.</summary>
    public enum SurfaceLeafPigmentPattern
    {
        Solid = 0,
        Edge = 1,
        Vein = 2,
        EdgeAndVein = 3,
        Mottled = 4,
    }

    /// <summary>One projected point in a branching surface-growth graph.</summary>
    [Serializable]
    public struct SurfaceGrowthNode
    {
        public Vector3 position;
        public Vector3 normal;
        public int parentIndex;
        public int branchDepth;
        public float distanceFromRoot;

        public SurfaceGrowthNode(
            Vector3 position,
            Vector3 normal,
            int parentIndex,
            int branchDepth,
            float distanceFromRoot)
        {
            this.position = position;
            this.normal = normal;
            this.parentIndex = parentIndex;
            this.branchDepth = branchDepth;
            this.distanceFromRoot = distanceFromRoot;
        }
    }

    /// <summary>
    /// Serializable result shared by surface vines and rhizomatous patches.
    /// Positions and normals are stored in the authoring component's local space.
    /// </summary>
    [Serializable]
    public sealed class SurfaceGrowthGraph
    {
        [SerializeField] private List<SurfaceGrowthNode> nodes =
            new List<SurfaceGrowthNode>();

        public List<SurfaceGrowthNode> Nodes
        {
            get { return nodes; }
        }

        public void Clear()
        {
            nodes.Clear();
        }
    }

    /// <summary>Path-generation controls shared by wall vines and ground rhizomes.</summary>
    [Serializable]
    public sealed class SurfaceGrowthSettings
    {
        public SurfaceGrowthMode mode = SurfaceGrowthMode.ProjectedSpline;

        [Tooltip("Number of primary paths before coverage is applied.")]
        [Range(1, 32)] public int pathCount = 5;

        [Tooltip("Length of one graph edge in metres.")]
        [Min(0.01f)] public float stepLength = 0.12f;

        [Tooltip("Maximum length of each primary path in metres.")]
        [Min(0.05f)] public float maxPathLength = 2.4f;

        [Tooltip("Scales path count, branch frequency, and leaf or shoot occupancy.")]
        [Range(0.01f, 1f)] public float coverage = 0.65f;

        [Header("Branching")]
        [Tooltip("Expected lateral branch starts per metre of primary path.")]
        [Range(0f, 8f)] public float branchesPerMetre = 0.65f;

        [Tooltip("Number of recursive lateral branch orders. Zero disables branches.")]
        [Range(0, 4)] public int maxBranchDepth = 1;

        [Tooltip("Fraction of the primary path length available to a branch.")]
        [Range(0.05f, 1f)] public float branchLength = 0.38f;

        [Tooltip("Angle in degrees between a lateral branch and its parent stem.")]
        [Range(5f, 85f)] public float branchAngle = 48f;

        [Tooltip("Per-branch random variation applied to Branch Angle in degrees.")]
        [Range(0f, 40f)] public float branchAngleJitter = 14f;

        [Tooltip("Per-branch proportional variation applied to Branch Length.")]
        [Range(0f, 0.75f)] public float branchLengthVariance = 0.22f;

        [Header("Path variation")]
        [Tooltip("Random change of the tangent direction at each step.")]
        [Range(0f, 1f)] public float directionJitter = 0.28f;

        [Tooltip("How strongly a projected spline is attracted to its guide instead of following its own tangent walk.")]
        [Range(0f, 1f)] public float guideAttraction = 0.58f;

        [Tooltip("Maximum root displacement over the surface in metres.")]
        [Min(0f)] public float rootSpread = 0.28f;

        [Tooltip("Per-path variation applied to Maximum Path Length.")]
        [Range(0f, 0.8f)] public float pathLengthVariance = 0.24f;

        [Tooltip("World-gravity preference projected into the surface tangent plane.")]
        [Range(-1f, 1f)] public float gravityBias = -0.08f;

        [Tooltip("Distance kept between unrelated graph nodes.")]
        [Min(0f)] public float minimumSpacing = 0.035f;

        [Tooltip("Distance above the collider used for the rendered path.")]
        [Min(0f)] public float surfaceOffset = 0.006f;

        [Tooltip("Maximum ray distance used to find the target surface.")]
        [Min(0.01f)] public float projectionDistance = 0.35f;

        [Tooltip("Hard safety limit for generated nodes.")]
        [Range(8, 8192)] public int nodeBudget = 1024;

        public int seed = 1;
    }

    /// <summary>Stem, leaf morphology, density, and colour controls for surface vines.</summary>
    [Serializable]
    public sealed class SurfaceVineParams
    {
        [Header("Stem")]
        [Min(0.001f)] public float stemWidth = 0.012f;
        [Tooltip("Short tapered stem behind each generated root so the vine does not start at a cut edge.")]
        [Min(0f)] public float rootAnchorLength = 0.08f;
        [Range(1f, 3f)] public float rootCollarScale = 1.55f;
        public Color stemRootColor = new Color(0.10f, 0.19f, 0.06f, 1f);
        public Color stemTipColor = new Color(0.23f, 0.36f, 0.11f, 1f);

        [Header("Leaves")]
        public SurfaceLeafShape leafShape = SurfaceLeafShape.Cordate;
        public SurfaceLeafArrangement leafArrangement = SurfaceLeafArrangement.Alternate;

        [Tooltip("Target leaves per metre before coverage is applied.")]
        [Range(0f, 40f)] public float leavesPerMetre = 7f;

        [Min(0.005f)] public float minimumLeafLength = 0.07f;
        [Min(0.005f)] public float maximumLeafLength = 0.16f;
        [Range(0.2f, 1.4f)] public float leafWidthRatio = 0.72f;
        [Range(0f, 1f)] public float leafDroop = 0.12f;

        [Tooltip("Variation of the interval between adjacent leaf nodes.")]
        [Range(0f, 0.9f)] public float leafSpacingJitter = 0.42f;

        [Tooltip("Random rotation of a leaf around its attachment node in degrees.")]
        [Range(0f, 90f)] public float leafAngleJitter = 24f;

        [Range(0f, 0.5f)] public float petioleLengthRatio = 0.12f;
        [Range(0.01f, 0.25f)] public float petioleWidthRatio = 0.045f;

        [Header("Leaf palette")]
        public Color youngColor = new Color(0.34f, 0.54f, 0.17f, 1f);
        public Color matureColor = new Color(0.12f, 0.31f, 0.07f, 1f);
        public Color autumnColor = new Color(0.42f, 0.12f, 0.24f, 1f);
        public Color dryColor = new Color(0.40f, 0.29f, 0.12f, 1f);

        [Tooltip("Probability that an entire leaf uses the autumn palette.")]
        [Range(0f, 1f)] public float autumnAmount = 0f;

        [Tooltip("Probability that a leaf uses the dry palette.")]
        [Range(0f, 1f)] public float dryAmount = 0f;

        [Tooltip("Per-leaf brightness variation.")]
        [Range(0f, 0.5f)] public float colourJitter = 0.08f;

        [Header("Local pigment")]
        public SurfaceLeafPigmentPattern pigmentPattern =
            SurfaceLeafPigmentPattern.EdgeAndVein;
        public Color edgeColor = new Color(0.12f, 0.075f, 0.09f, 1f);
        public Color veinColor = new Color(0.16f, 0.08f, 0.11f, 1f);
        public Color petioleColor = new Color(0.14f, 0.075f, 0.09f, 1f);

        [Tooltip("Width of the coloured edge as a fraction of the leaf radius.")]
        [Range(0.02f, 0.4f)] public float edgeWidth = 0.12f;

        [Tooltip("Blend strength of edge, vein, petiole, and mottled pigment.")]
        [Range(0f, 1f)] public float pigmentAmount = 0.42f;

        [Header("Wind")]
        [Tooltip("Surface stems should normally remain close to 1. Leaves keep their own attachment pivot.")]
        [Range(0f, 1f)] public float stemStiffness = 0.96f;
        [Range(0f, 1f)] public float leafStiffness = 0.72f;

        public void ApplyCreepingFigPreset()
        {
            leafShape = SurfaceLeafShape.Cordate;
            leafArrangement = SurfaceLeafArrangement.Alternate;
            leavesPerMetre = 11f;
            minimumLeafLength = 0.045f;
            maximumLeafLength = 0.09f;
            leafWidthRatio = 0.78f;
            autumnAmount = 0f;
            dryAmount = 0f;
            youngColor = new Color(0.34f, 0.55f, 0.20f, 1f);
            matureColor = new Color(0.10f, 0.29f, 0.08f, 1f);
            pigmentPattern = SurfaceLeafPigmentPattern.Vein;
            pigmentAmount = 0.18f;
            edgeColor = new Color(0.11f, 0.20f, 0.08f, 1f);
            veinColor = new Color(0.12f, 0.23f, 0.09f, 1f);
            petioleColor = new Color(0.15f, 0.25f, 0.09f, 1f);
        }

        public void ApplyEnglishIvyPreset()
        {
            leafShape = SurfaceLeafShape.Lobed;
            leafArrangement = SurfaceLeafArrangement.Alternate;
            leavesPerMetre = 7f;
            minimumLeafLength = 0.08f;
            maximumLeafLength = 0.17f;
            leafWidthRatio = 0.95f;
            autumnAmount = 0f;
            dryAmount = 0f;
            youngColor = new Color(0.25f, 0.44f, 0.14f, 1f);
            matureColor = new Color(0.07f, 0.22f, 0.07f, 1f);
            pigmentPattern = SurfaceLeafPigmentPattern.Vein;
            pigmentAmount = 0.34f;
            veinColor = new Color(0.24f, 0.31f, 0.17f, 1f);
            petioleColor = new Color(0.13f, 0.18f, 0.08f, 1f);
        }

        public void ApplyBostonIvyPreset()
        {
            leafShape = SurfaceLeafShape.Lobed;
            leafArrangement = SurfaceLeafArrangement.Alternate;
            leavesPerMetre = 5.5f;
            minimumLeafLength = 0.10f;
            maximumLeafLength = 0.22f;
            leafWidthRatio = 1.02f;
            autumnAmount = 0.12f;
            dryAmount = 0.035f;
            youngColor = new Color(0.24f, 0.45f, 0.13f, 1f);
            matureColor = new Color(0.075f, 0.27f, 0.075f, 1f);
            autumnColor = new Color(0.36f, 0.10f, 0.15f, 1f);
            pigmentPattern = SurfaceLeafPigmentPattern.EdgeAndVein;
            pigmentAmount = 0.48f;
            edgeColor = new Color(0.16f, 0.055f, 0.075f, 1f);
            veinColor = new Color(0.20f, 0.06f, 0.085f, 1f);
            petioleColor = new Color(0.18f, 0.055f, 0.07f, 1f);
            stemRootColor = new Color(0.11f, 0.055f, 0.065f, 1f);
            stemTipColor = new Color(0.20f, 0.075f, 0.085f, 1f);
        }
    }

    /// <summary>Shoot morphology for a rhizome-connected ground patch.</summary>
    [Serializable]
    public sealed class RhizomePatchParams
    {
        [Tooltip("Target above-ground shoots per metre of rhizome before coverage is applied.")]
        [Range(0.1f, 30f)] public float shootsPerMetre = 5f;

        public Vector2 shootHeight = new Vector2(0.12f, 0.28f);
        [Min(0.001f)] public float stemWidth = 0.006f;
        public Color stemColor = new Color(0.25f, 0.39f, 0.16f, 1f);

        [Header("Leaves")]
        public SurfaceLeafShape leafShape = SurfaceLeafShape.Cordate;
        [Range(1, 4)] public int leavesPerShoot = 2;
        public Vector2 leafLength = new Vector2(0.055f, 0.13f);
        [Range(0.2f, 1.4f)] public float leafWidthRatio = 0.9f;
        public Color leafColor = new Color(0.16f, 0.40f, 0.20f, 1f);
        public Color leafAccentColor = new Color(0.43f, 0.16f, 0.28f, 1f);
        [Range(0f, 1f)] public float accentAmount = 0.12f;

        [Header("Flowers")]
        [Range(0f, 1f)] public float flowerChance = 0.16f;
        [Min(0.003f)] public float flowerRadius = 0.026f;
        public Color bractColor = new Color(0.92f, 0.94f, 0.87f, 1f);
        public Color spikeColor = new Color(0.68f, 0.74f, 0.38f, 1f);

        [Header("Rhizome preview")]
        public bool renderRhizomes = false;
        [Min(0.001f)] public float rhizomeWidth = 0.008f;
        public Color rhizomeColor = new Color(0.38f, 0.27f, 0.13f, 1f);
        [Range(0f, 1f)] public float stiffness = 0.86f;
    }

    /// <summary>Authoring component for vines that follow a Collider surface.</summary>
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
    }

    /// <summary>Authoring component for rhizome-connected ground-cover shoots.</summary>
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
    }
}
