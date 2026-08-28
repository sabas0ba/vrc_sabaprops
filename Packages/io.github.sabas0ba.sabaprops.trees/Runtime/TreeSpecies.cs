using System;
using UnityEngine;

namespace SabaProps.Trees
{
    public enum TreeArchetype
    {
        Broadleaf = 0,
        Conifer = 1,
        Deadwood = 2,
        DesertScrub = 3,
    }

    public enum TreeBotanicalPreset
    {
        Custom = 0,
        JapaneseZelkova = 1,
        JapaneseMaple = 2,
        JapaneseCedar = 3,
        JapaneseWhiteBirch = 4,
        JapaneseRedPine = 5,
    }

    public enum TreeCrownShape
    {
        Rounded = 0,
        Vase = 1,
        Layered = 2,
        Pyramidal = 3,
        OpenIrregular = 4,
    }

    public enum TreeBranchArrangement
    {
        Spiral = 0,
        Opposite = 1,
        Whorled = 2,
        Irregular = 3,
    }

    public enum TreeLeafArrangement
    {
        Alternate = 0,
        Opposite = 1,
        Whorled = 2,
        FasciclePairs = 3,
    }

    public enum TreeLeafShape
    {
        None = 0,
        Broad = 1,
        Needle = 2,
        Palmate = 3,
        Scale = 4,
    }

    [Serializable]
    public sealed class TreeStructureParams
    {
        [Header("Trunk")]
        [Min(0.2f)] public float trunkLength = 4.8f;
        [Min(0.01f)] public float trunkRadius = 0.24f;
        [Range(3, 12)] public int radialSegments = 7;
        [Range(1, 8)] public int segmentsPerBranch = 4;

        [Header("Recursion")]
        [Range(1, 6)] public int maxDepth = 4;
        [Range(1, 6)] public int branchCount = 3;
        [Range(5f, 85f)] public float branchAngle = 38f;
        [Range(0f, 35f)] public float branchAngleJitter = 12f;
        [Range(0.35f, 0.85f)] public float lengthDecay = 0.66f;
        [Range(0.25f, 0.8f)] public float radiusDecay = 0.58f;
        [Range(0.15f, 0.8f)] public float trunkBranchStart = 0.32f;
        [Range(0f, 0.5f)] public float crookedness = 0.09f;
        [Range(16, 1024)] public int maxBranches = 384;

        [Header("Growth habit")]
        public TreeCrownShape crownShape = TreeCrownShape.Rounded;
        public TreeBranchArrangement branchArrangement =
            TreeBranchArrangement.Spiral;
        [Range(2, 6)] public int whorlSize = 3;
        [Range(0f, 1f)] public float apicalDominance = 0.5f;
        [Range(0f, 0.8f)] public float branchDroop = 0.04f;
        [Range(0f, 0.8f)] public float tipUpturn = 0.12f;
        [Range(0f, 45f)] public float azimuthJitter = 16f;
        [Range(0f, 0.5f)] public float branchLengthVariance = 0.14f;
    }

    [Serializable]
    public sealed class TreeAppearanceParams
    {
        [Header("Bark")]
        public Color barkRootColor = new Color(0.16f, 0.105f, 0.065f, 1f);
        public Color barkTipColor = new Color(0.28f, 0.19f, 0.105f, 1f);

        [Header("Leaves")]
        public TreeLeafShape leafShape = TreeLeafShape.Broad;
        public TreeLeafArrangement leafArrangement = TreeLeafArrangement.Alternate;
        [Range(1, 12)] public int leavesPerTip = 5;
        [Min(0.01f)] public float leafLength = 0.24f;
        [Min(0.005f)] public float leafWidth = 0.11f;
        public Color leafBaseColor = new Color(0.12f, 0.29f, 0.075f, 1f);
        public Color leafTipColor = new Color(0.32f, 0.52f, 0.14f, 1f);

        [Header("Wind")]
        [Range(0f, 1f)] public float branchStiffness = 0.32f;
        [Range(0f, 1f)] public float leafStiffness = 0.78f;
    }

    [Serializable]
    public sealed class TreeLodParams
    {
        [Range(1, 3)] public int lod1DepthReduction = 1;
        [Range(1, 4)] public int lod2DepthReduction = 2;
        [Range(0.2f, 0.9f)] public float lod0ScreenHeight = 0.55f;
        [Range(0.05f, 0.7f)] public float lod1ScreenHeight = 0.25f;
        [Range(0.01f, 0.4f)] public float lod2ScreenHeight = 0.08f;
    }

    [Serializable]
    public sealed class TreePlacementParams
    {
        [Min(0f)] public float placementWeight = 1f;
        [Min(0f)] public float minSpacing = 3f;
        public Vector2 scaleRange = new Vector2(0.85f, 1.2f);
        [Range(0f, 45f)] public float maxTilt = 3f;
        [Range(0f, 1f)] public float alignToGroundNormal = 0.15f;
        public Vector2 slopeLimits = new Vector2(0f, 30f);
    }

    /// <summary>
    /// Serializable source parameters for one recursively generated tree.
    /// Generated meshes are editor assets; no C# executes at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "TreeSpecies", menuName = "SabaProps/Trees/Tree Species")]
    public sealed class TreeSpecies : ScriptableObject
    {
        public TreeArchetype archetype = TreeArchetype.Broadleaf;
        public TreeBotanicalPreset botanicalPreset = TreeBotanicalPreset.Custom;
        public int meshSeed = 101;

        public TreeStructureParams structure = new TreeStructureParams();
        public TreeAppearanceParams appearance = new TreeAppearanceParams();
        public TreeLodParams lod = new TreeLodParams();
        public TreePlacementParams placement = new TreePlacementParams();

        [Header("Generated Assets (read only)")]
        public Mesh lod0Mesh;
        public Mesh lod1Mesh;
        public Mesh lod2Mesh;
        public Material material;

        public Mesh MeshForLod(int lodLevel)
        {
            switch (Mathf.Clamp(lodLevel, 0, 2))
            {
                case 1: return lod1Mesh;
                case 2: return lod2Mesh;
                default: return lod0Mesh;
            }
        }

        public Vector2 SafeScaleRange
        {
            get
            {
                float min = Mathf.Max(0.001f,
                    Mathf.Min(placement.scaleRange.x, placement.scaleRange.y));
                float max = Mathf.Max(min,
                    Mathf.Max(placement.scaleRange.x, placement.scaleRange.y));
                return new Vector2(min, max);
            }
        }

        public Vector2 SafeSlopeLimits
        {
            get
            {
                float min = Mathf.Clamp(
                    Mathf.Min(placement.slopeLimits.x, placement.slopeLimits.y),
                    0f, 90f);
                float max = Mathf.Clamp(
                    Mathf.Max(placement.slopeLimits.x, placement.slopeLimits.y),
                    0f, 90f);
                return new Vector2(min, max);
            }
        }

        public void ApplyArchetypePreset(TreeArchetype value)
        {
            archetype = value;
            botanicalPreset = TreeBotanicalPreset.Custom;
            structure = new TreeStructureParams();
            appearance = new TreeAppearanceParams();
            lod = new TreeLodParams();
            placement = new TreePlacementParams();

            switch (value)
            {
                case TreeArchetype.Conifer:
                    meshSeed = 211;
                    structure.trunkLength = 6.5f;
                    structure.trunkRadius = 0.26f;
                    structure.maxDepth = 4;
                    structure.branchCount = 4;
                    structure.branchAngle = 58f;
                    structure.branchAngleJitter = 7f;
                    structure.lengthDecay = 0.61f;
                    structure.radiusDecay = 0.52f;
                    structure.trunkBranchStart = 0.18f;
                    structure.crookedness = 0.035f;
                    structure.maxBranches = 768;
                    appearance.barkRootColor = new Color(0.12f, 0.085f, 0.055f, 1f);
                    appearance.barkTipColor = new Color(0.24f, 0.16f, 0.08f, 1f);
                    appearance.leafShape = TreeLeafShape.Needle;
                    appearance.leavesPerTip = 8;
                    appearance.leafLength = 0.18f;
                    appearance.leafWidth = 0.018f;
                    appearance.leafBaseColor = new Color(0.055f, 0.19f, 0.09f, 1f);
                    appearance.leafTipColor = new Color(0.13f, 0.31f, 0.14f, 1f);
                    appearance.branchStiffness = 0.2f;
                    placement.minSpacing = 2.6f;
                    placement.scaleRange = new Vector2(0.8f, 1.3f);
                    placement.maxTilt = 2f;
                    placement.slopeLimits = new Vector2(0f, 40f);
                    break;

                case TreeArchetype.Deadwood:
                    meshSeed = 307;
                    structure.trunkLength = 4.2f;
                    structure.trunkRadius = 0.22f;
                    structure.maxDepth = 4;
                    structure.branchCount = 2;
                    structure.branchAngle = 43f;
                    structure.lengthDecay = 0.71f;
                    structure.radiusDecay = 0.61f;
                    structure.crookedness = 0.22f;
                    appearance.barkRootColor = new Color(0.11f, 0.085f, 0.065f, 1f);
                    appearance.barkTipColor = new Color(0.32f, 0.27f, 0.21f, 1f);
                    appearance.leafShape = TreeLeafShape.None;
                    appearance.branchStiffness = 0.12f;
                    placement.minSpacing = 2.2f;
                    placement.scaleRange = new Vector2(0.8f, 1.15f);
                    placement.maxTilt = 4f;
                    placement.alignToGroundNormal = 0.25f;
                    placement.slopeLimits = new Vector2(0f, 50f);
                    break;

                case TreeArchetype.DesertScrub:
                    meshSeed = 401;
                    structure.trunkLength = 1.8f;
                    structure.trunkRadius = 0.13f;
                    structure.radialSegments = 6;
                    structure.maxDepth = 4;
                    structure.branchCount = 3;
                    structure.branchAngle = 57f;
                    structure.branchAngleJitter = 18f;
                    structure.lengthDecay = 0.72f;
                    structure.radiusDecay = 0.63f;
                    structure.trunkBranchStart = 0.12f;
                    structure.crookedness = 0.34f;
                    appearance.barkRootColor = new Color(0.19f, 0.12f, 0.075f, 1f);
                    appearance.barkTipColor = new Color(0.49f, 0.34f, 0.19f, 1f);
                    appearance.leafShape = TreeLeafShape.None;
                    appearance.branchStiffness = 0.18f;
                    placement.minSpacing = 1.4f;
                    placement.scaleRange = new Vector2(0.75f, 1.25f);
                    placement.maxTilt = 6f;
                    placement.alignToGroundNormal = 0.35f;
                    placement.slopeLimits = new Vector2(0f, 55f);
                    break;

                case TreeArchetype.Broadleaf:
                default:
                    meshSeed = 101;
                    break;
            }

            ValidateParameters();
        }

        public void ApplyBotanicalPreset(TreeBotanicalPreset value)
        {
            TreeArchetype baseArchetype = value == TreeBotanicalPreset.JapaneseCedar
                || value == TreeBotanicalPreset.JapaneseRedPine
                ? TreeArchetype.Conifer
                : TreeArchetype.Broadleaf;
            ApplyArchetypePreset(baseArchetype);
            botanicalPreset = value;

            switch (value)
            {
                case TreeBotanicalPreset.JapaneseZelkova:
                    meshSeed = 1103;
                    structure.trunkLength = 6.2f;
                    structure.trunkRadius = 0.31f;
                    structure.maxDepth = 4;
                    structure.branchCount = 3;
                    structure.branchAngle = 34f;
                    structure.branchAngleJitter = 8f;
                    structure.lengthDecay = 0.69f;
                    structure.radiusDecay = 0.58f;
                    structure.trunkBranchStart = 0.36f;
                    structure.crookedness = 0.055f;
                    structure.maxBranches = 620;
                    structure.crownShape = TreeCrownShape.Vase;
                    structure.branchArrangement = TreeBranchArrangement.Spiral;
                    structure.apicalDominance = 0.38f;
                    structure.branchDroop = 0.02f;
                    structure.tipUpturn = 0.32f;
                    structure.branchLengthVariance = 0.12f;
                    appearance.barkRootColor = new Color(0.19f, 0.17f, 0.14f, 1f);
                    appearance.barkTipColor = new Color(0.37f, 0.29f, 0.20f, 1f);
                    appearance.leafShape = TreeLeafShape.Broad;
                    appearance.leafArrangement = TreeLeafArrangement.Alternate;
                    appearance.leavesPerTip = 8;
                    appearance.leafLength = 0.22f;
                    appearance.leafWidth = 0.09f;
                    appearance.leafBaseColor = new Color(0.10f, 0.28f, 0.07f, 1f);
                    appearance.leafTipColor = new Color(0.28f, 0.48f, 0.12f, 1f);
                    break;

                case TreeBotanicalPreset.JapaneseMaple:
                    meshSeed = 1201;
                    structure.trunkLength = 4.3f;
                    structure.trunkRadius = 0.23f;
                    structure.maxDepth = 5;
                    structure.branchCount = 2;
                    structure.branchAngle = 51f;
                    structure.branchAngleJitter = 11f;
                    structure.lengthDecay = 0.72f;
                    structure.radiusDecay = 0.64f;
                    structure.trunkBranchStart = 0.20f;
                    structure.crookedness = 0.11f;
                    structure.maxBranches = 560;
                    structure.crownShape = TreeCrownShape.Layered;
                    structure.branchArrangement = TreeBranchArrangement.Opposite;
                    structure.apicalDominance = 0.27f;
                    structure.branchDroop = 0.09f;
                    structure.tipUpturn = 0.17f;
                    structure.azimuthJitter = 10f;
                    appearance.barkRootColor = new Color(0.16f, 0.13f, 0.105f, 1f);
                    appearance.barkTipColor = new Color(0.25f, 0.20f, 0.14f, 1f);
                    appearance.leafShape = TreeLeafShape.Palmate;
                    appearance.leafArrangement = TreeLeafArrangement.Opposite;
                    appearance.leavesPerTip = 4;
                    appearance.leafLength = 0.22f;
                    appearance.leafWidth = 0.18f;
                    appearance.leafBaseColor = new Color(0.10f, 0.27f, 0.07f, 1f);
                    appearance.leafTipColor = new Color(0.31f, 0.49f, 0.13f, 1f);
                    break;

                case TreeBotanicalPreset.JapaneseCedar:
                    meshSeed = 1301;
                    structure.trunkLength = 7.2f;
                    structure.trunkRadius = 0.30f;
                    structure.maxDepth = 4;
                    structure.branchCount = 3;
                    structure.branchAngle = 73f;
                    structure.branchAngleJitter = 6f;
                    structure.lengthDecay = 0.59f;
                    structure.radiusDecay = 0.54f;
                    structure.trunkBranchStart = 0.14f;
                    structure.crookedness = 0.025f;
                    structure.maxBranches = 820;
                    structure.crownShape = TreeCrownShape.Pyramidal;
                    structure.branchArrangement = TreeBranchArrangement.Whorled;
                    structure.whorlSize = 4;
                    structure.apicalDominance = 0.94f;
                    structure.branchDroop = 0.17f;
                    structure.tipUpturn = 0.04f;
                    structure.azimuthJitter = 5f;
                    structure.branchLengthVariance = 0.08f;
                    appearance.barkRootColor = new Color(0.19f, 0.085f, 0.045f, 1f);
                    appearance.barkTipColor = new Color(0.35f, 0.16f, 0.075f, 1f);
                    appearance.leafShape = TreeLeafShape.Scale;
                    appearance.leafArrangement = TreeLeafArrangement.Whorled;
                    appearance.leavesPerTip = 10;
                    appearance.leafLength = 0.28f;
                    appearance.leafWidth = 0.07f;
                    appearance.leafBaseColor = new Color(0.045f, 0.18f, 0.075f, 1f);
                    appearance.leafTipColor = new Color(0.12f, 0.31f, 0.12f, 1f);
                    break;

                case TreeBotanicalPreset.JapaneseWhiteBirch:
                    meshSeed = 1409;
                    structure.trunkLength = 6.8f;
                    structure.trunkRadius = 0.25f;
                    structure.maxDepth = 4;
                    structure.branchCount = 2;
                    structure.branchAngle = 49f;
                    structure.branchAngleJitter = 9f;
                    structure.lengthDecay = 0.67f;
                    structure.radiusDecay = 0.61f;
                    structure.trunkBranchStart = 0.23f;
                    structure.crookedness = 0.045f;
                    structure.maxBranches = 520;
                    structure.crownShape = TreeCrownShape.Pyramidal;
                    structure.branchArrangement = TreeBranchArrangement.Spiral;
                    structure.apicalDominance = 0.88f;
                    structure.branchDroop = 0.38f;
                    structure.tipUpturn = 0.06f;
                    structure.branchLengthVariance = 0.16f;
                    appearance.barkRootColor = new Color(0.48f, 0.47f, 0.43f, 1f);
                    appearance.barkTipColor = new Color(0.25f, 0.18f, 0.12f, 1f);
                    appearance.leafShape = TreeLeafShape.Broad;
                    appearance.leafArrangement = TreeLeafArrangement.Alternate;
                    appearance.leavesPerTip = 7;
                    appearance.leafLength = 0.20f;
                    appearance.leafWidth = 0.09f;
                    appearance.leafBaseColor = new Color(0.10f, 0.29f, 0.075f, 1f);
                    appearance.leafTipColor = new Color(0.35f, 0.51f, 0.15f, 1f);
                    break;

                case TreeBotanicalPreset.JapaneseRedPine:
                    meshSeed = 1501;
                    structure.trunkLength = 6.4f;
                    structure.trunkRadius = 0.29f;
                    structure.maxDepth = 4;
                    structure.branchCount = 2;
                    structure.branchAngle = 68f;
                    structure.branchAngleJitter = 14f;
                    structure.lengthDecay = 0.68f;
                    structure.radiusDecay = 0.62f;
                    structure.trunkBranchStart = 0.43f;
                    structure.crookedness = 0.17f;
                    structure.maxBranches = 430;
                    structure.crownShape = TreeCrownShape.OpenIrregular;
                    structure.branchArrangement = TreeBranchArrangement.Whorled;
                    structure.whorlSize = 3;
                    structure.apicalDominance = 0.72f;
                    structure.branchDroop = 0.13f;
                    structure.tipUpturn = 0.24f;
                    structure.azimuthJitter = 18f;
                    structure.branchLengthVariance = 0.22f;
                    appearance.barkRootColor = new Color(0.20f, 0.17f, 0.14f, 1f);
                    appearance.barkTipColor = new Color(0.55f, 0.22f, 0.075f, 1f);
                    appearance.leafShape = TreeLeafShape.Needle;
                    appearance.leafArrangement = TreeLeafArrangement.FasciclePairs;
                    appearance.leavesPerTip = 10;
                    appearance.leafLength = 0.38f;
                    appearance.leafWidth = 0.025f;
                    appearance.leafBaseColor = new Color(0.08f, 0.24f, 0.09f, 1f);
                    appearance.leafTipColor = new Color(0.20f, 0.39f, 0.13f, 1f);
                    break;

                case TreeBotanicalPreset.Custom:
                default:
                    break;
            }

            ValidateParameters();
        }

        public void ValidateParameters()
        {
            if (structure == null) structure = new TreeStructureParams();
            if (appearance == null) appearance = new TreeAppearanceParams();
            if (lod == null) lod = new TreeLodParams();
            if (placement == null) placement = new TreePlacementParams();

            structure.trunkLength = Mathf.Max(0.2f, structure.trunkLength);
            structure.trunkRadius = Mathf.Max(0.01f, structure.trunkRadius);
            structure.radialSegments = Mathf.Clamp(structure.radialSegments, 3, 12);
            structure.segmentsPerBranch = Mathf.Clamp(structure.segmentsPerBranch, 1, 8);
            structure.maxDepth = Mathf.Clamp(structure.maxDepth, 1, 6);
            structure.branchCount = Mathf.Clamp(structure.branchCount, 1, 6);
            structure.branchAngle = Mathf.Clamp(structure.branchAngle, 5f, 85f);
            structure.branchAngleJitter = Mathf.Clamp(structure.branchAngleJitter, 0f, 35f);
            structure.lengthDecay = Mathf.Clamp(structure.lengthDecay, 0.35f, 0.85f);
            structure.radiusDecay = Mathf.Clamp(structure.radiusDecay, 0.25f, 0.8f);
            structure.trunkBranchStart = Mathf.Clamp(structure.trunkBranchStart, 0.15f, 0.8f);
            structure.crookedness = Mathf.Clamp(structure.crookedness, 0f, 0.5f);
            structure.maxBranches = Mathf.Clamp(structure.maxBranches, 16, 1024);
            structure.whorlSize = Mathf.Clamp(structure.whorlSize, 2, 6);
            structure.apicalDominance = Mathf.Clamp01(structure.apicalDominance);
            structure.branchDroop = Mathf.Clamp(structure.branchDroop, 0f, 0.8f);
            structure.tipUpturn = Mathf.Clamp(structure.tipUpturn, 0f, 0.8f);
            structure.azimuthJitter = Mathf.Clamp(structure.azimuthJitter, 0f, 45f);
            structure.branchLengthVariance = Mathf.Clamp(
                structure.branchLengthVariance, 0f, 0.5f);
            appearance.leavesPerTip = Mathf.Clamp(appearance.leavesPerTip, 1, 12);
            appearance.leafLength = Mathf.Max(0.01f, appearance.leafLength);
            appearance.leafWidth = Mathf.Max(0.005f, appearance.leafWidth);
            appearance.branchStiffness = Mathf.Clamp01(appearance.branchStiffness);
            appearance.leafStiffness = Mathf.Clamp01(appearance.leafStiffness);

            lod.lod1DepthReduction = Mathf.Clamp(lod.lod1DepthReduction, 1, 3);
            lod.lod2DepthReduction = Mathf.Clamp(lod.lod2DepthReduction, 1, 4);

            lod.lod0ScreenHeight = Mathf.Clamp(lod.lod0ScreenHeight, 0.03f, 1f);
            lod.lod1ScreenHeight = Mathf.Clamp(
                lod.lod1ScreenHeight, 0.02f, lod.lod0ScreenHeight - 0.01f);
            lod.lod2ScreenHeight = Mathf.Clamp(
                lod.lod2ScreenHeight, 0.01f, lod.lod1ScreenHeight - 0.01f);

            placement.placementWeight = Mathf.Max(0f, placement.placementWeight);
            placement.minSpacing = Mathf.Max(0f, placement.minSpacing);
            placement.scaleRange = SafeScaleRange;
            placement.maxTilt = Mathf.Clamp(placement.maxTilt, 0f, 45f);
            placement.alignToGroundNormal = Mathf.Clamp01(placement.alignToGroundNormal);
            placement.slopeLimits = SafeSlopeLimits;
        }

        private void OnValidate()
        {
            ValidateParameters();
        }
    }
}
