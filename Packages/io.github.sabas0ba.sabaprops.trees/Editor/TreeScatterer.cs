using System.Collections.Generic;
using SabaProps.Foliage;
using SabaProps.Foliage.Editors;
using UnityEngine;

namespace SabaProps.Trees.Editors
{
    public struct TreeInstance
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float Scale;
        public int SpeciesIndex;

        public Matrix4x4 ToMatrix()
        {
            return Matrix4x4.TRS(
                Position, Rotation, Vector3.one * Scale);
        }
    }

    /// <summary>
    /// Tree-specific policy layered on the shared Foliage surface sampler.
    /// Area iteration, masks, raycasts, altitude and exclusions are not
    /// duplicated in this package.
    /// </summary>
    public static class TreeScatterer
    {
        public static List<TreeInstance> Scatter(
            TreeField field, out string error)
        {
            error = null;
            var empty = new List<TreeInstance>();
            if (field == null)
            {
                error = "TreeField が見つかりません。";
                return empty;
            }

            List<TreeSpecies> species = CollectValidSpecies(field, out error);
            if (species.Count == 0)
            {
                return empty;
            }

            var weights = new List<float>(species.Count);
            foreach (TreeSpecies entry in species)
            {
                weights.Add(field.PlacementWeightAt(field.species.IndexOf(entry)));
            }
            float[] cumulative =
                FoliageSurfaceScatterer.BuildCumulativeWeights(weights);
            if (cumulative[cumulative.Length - 1] <= 0f)
            {
                error = "すべての Tree Species の Placement Weight が 0 です。";
                return empty;
            }

            var spacing = new FoliageSpacingGrid[species.Count];
            for (int i = 0; i < species.Count; i++)
            {
                spacing[i] = new FoliageSpacingGrid(
                    species[i].placement.minSpacing);
            }

            FoliageCandidateSelector<int> selectCandidate =
                delegate(ref FoliageRandom random)
                {
                    return FoliageSurfaceScatterer.PickWeightedIndex(
                        cumulative, ref random);
                };

            FoliageSurfaceFilter<int> filterSurface =
                delegate(int speciesIndex, Vector3 position, Vector3 normal)
                {
                    Vector2 limits = species[speciesIndex].SafeSlopeLimits;
                    float slope = Vector3.Angle(normal, Vector3.up);
                    return slope >= limits.x && slope <= limits.y;
                };

            FoliagePlacementFactory<int, TreeInstance> createPlacement =
                delegate(
                    int speciesIndex, Vector3 position, Vector3 groundNormal,
                    ref FoliageRandom random, out TreeInstance instance)
                {
                    TreeSpecies entry = species[speciesIndex];
                    instance = default;
                    if (!spacing[speciesIndex].TryPlace(
                        position, entry.placement.minSpacing))
                    {
                        return false;
                    }

                    float yaw = random.Range(0f, 360f);
                    Quaternion rotation =
                        FoliageSurfaceScatterer.BuildGroundRotation(
                            groundNormal,
                            entry.placement.alignToGroundNormal,
                            entry.placement.maxTilt,
                            yaw,
                            ref random);
                    Vector2 scaleRange = entry.SafeScaleRange;
                    instance = new TreeInstance
                    {
                        Position = position,
                        Rotation = rotation,
                        Scale = random.Range(scaleRange.x, scaleRange.y),
                        SpeciesIndex = speciesIndex,
                    };
                    return true;
                };

            var settings = new FoliageSurfaceScatterSettings
            {
                Shape = field.shape,
                Size = field.size,
                Radius = field.radius,
                Density = field.density,
                Seed = field.seed,
                MaxInstances = field.maxInstances,
                GroundLayers = field.groundLayers,
                RaycastHeight = field.raycastHeight,
                RaycastDistance = field.raycastDistance,
                RequireGroundHit = field.requireGroundHit,
                AltitudeLimits = field.altitudeLimits,
                GroundOffset = field.groundOffset,
                ExclusionLayers = field.exclusionLayers,
                ExclusionRadius = field.exclusionRadius,
                DensityMask = field.densityMask,
                DensityMaskThreshold = field.densityMaskThreshold,
                InvertDensityMask = field.invertDensityMask,
                LogContext = field,
            };

            return FoliageSurfaceScatterer.Scatter(
                field.transform,
                settings,
                selectCandidate,
                filterSurface,
                createPlacement,
                out error);
        }

        public static List<TreeSpecies> CollectValidSpecies(
            TreeField field, out string error)
        {
            error = null;
            var result = new List<TreeSpecies>();
            if (field == null || field.species == null)
            {
                error = "Tree Species が設定されていません。";
                return result;
            }

            foreach (TreeSpecies species in field.species)
            {
                if (species == null || result.Contains(species))
                {
                    continue;
                }
                species.ValidateParameters();
                result.Add(species);
            }

            if (result.Count == 0)
            {
                error = "有効な Tree Species が 1 つもありません。";
            }
            return result;
        }
    }
}
