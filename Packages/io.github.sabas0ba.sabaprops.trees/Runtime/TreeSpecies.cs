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

    public enum TreeLeafShape
    {
        None = 0,
        Broad = 1,
        Needle = 2,
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
    }

    [Serializable]
    public sealed class TreeAppearanceParams
    {
        [Header("Bark")]
        public Color barkRootColor = new Color(0.16f, 0.105f, 0.065f, 1f);
        public Color barkTipColor = new Color(0.28f, 0.19f, 0.105f, 1f);

        [Header("Leaves")]
        public TreeLeafShape leafShape = TreeLeafShape.Broad;
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

    /// <summary>
    /// Serializable source parameters for one recursively generated tree.
    /// Generated meshes are editor assets; no C# executes at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "TreeSpecies", menuName = "SabaProps/Trees/Tree Species")]
    public sealed class TreeSpecies : ScriptableObject
    {
        public TreeArchetype archetype = TreeArchetype.Broadleaf;
        public int meshSeed = 101;

        public TreeStructureParams structure = new TreeStructureParams();
        public TreeAppearanceParams appearance = new TreeAppearanceParams();
        public TreeLodParams lod = new TreeLodParams();

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

        public void ApplyArchetypePreset(TreeArchetype value)
        {
            archetype = value;
            structure = new TreeStructureParams();
            appearance = new TreeAppearanceParams();
            lod = new TreeLodParams();

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
                    break;

                case TreeArchetype.Broadleaf:
                default:
                    meshSeed = 101;
                    break;
            }

            ValidateParameters();
        }

        public void ValidateParameters()
        {
            if (structure == null) structure = new TreeStructureParams();
            if (appearance == null) appearance = new TreeAppearanceParams();
            if (lod == null) lod = new TreeLodParams();

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
        }

        private void OnValidate()
        {
            ValidateParameters();
        }
    }
}
