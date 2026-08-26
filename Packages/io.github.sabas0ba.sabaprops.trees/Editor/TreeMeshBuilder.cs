using System;
using System.Collections.Generic;
using SabaProps.Foliage;
using UnityEngine;

namespace SabaProps.Trees.Editors
{
    /// <summary>
    /// Builds a trunk and recursively split branch subtrees. Each primary
    /// branch owns one wind pivot. Descendants inherit that pivot and their
    /// parent's bend coordinate, so connected joints receive identical wind
    /// displacement instead of opening gaps.
    /// </summary>
    public static class TreeMeshBuilder
    {
        private sealed class BuildSettings
        {
            public int MaxDepth;
            public int BranchCount;
            public int RadialSegments;
            public int SegmentsPerBranch;
            public float LeafFraction;
        }

        private sealed class BuildContext
        {
            public TreeSpecies Species;
            public TreeMeshBuffer Buffer;
            public BuildSettings Settings;
            public FoliageRandom Random;
            public int BranchesBuilt;
        }

        private sealed class BranchPath
        {
            public Vector3[] Points;
            public Vector3[] Directions;

            public Vector3 PointAt(float ratio)
            {
                ratio = Mathf.Clamp01(ratio);
                float scaled = ratio * (Points.Length - 1);
                int index = Mathf.Min(Mathf.FloorToInt(scaled), Points.Length - 2);
                return Vector3.Lerp(Points[index], Points[index + 1], scaled - index);
            }

            public Vector3 DirectionAt(float ratio)
            {
                ratio = Mathf.Clamp01(ratio);
                int index = Mathf.Min(
                    Mathf.FloorToInt(ratio * Directions.Length),
                    Directions.Length - 1);
                return Directions[index];
            }
        }

        public static Mesh Build(TreeSpecies species, int lodLevel)
        {
            if (species == null)
            {
                return null;
            }

            species.ValidateParameters();
            lodLevel = Mathf.Clamp(lodLevel, 0, 2);

            TreeStructureParams structure = species.structure;
            int depthReduction = lodLevel == 1
                ? species.lod.lod1DepthReduction
                : lodLevel == 2 ? species.lod.lod2DepthReduction : 0;

            var settings = new BuildSettings
            {
                MaxDepth = Mathf.Max(1, structure.maxDepth - depthReduction),
                BranchCount = Mathf.Max(1, structure.branchCount - (lodLevel == 2 ? 1 : 0)),
                RadialSegments = Mathf.Max(3, structure.radialSegments - lodLevel * 2),
                SegmentsPerBranch = Mathf.Max(1, structure.segmentsPerBranch - lodLevel),
                LeafFraction = lodLevel == 0 ? 1f : lodLevel == 1 ? 0.55f : 0.22f,
            };

            var context = new BuildContext
            {
                Species = species,
                Buffer = new TreeMeshBuffer(),
                Settings = settings,
                Random = new FoliageRandom(unchecked(species.meshSeed + lodLevel * 104729)),
            };

            BuildTree(context);
            return context.Buffer.ToMesh($"{species.name}_LOD{lodLevel}", 0.45f);
        }

        private static void BuildTree(BuildContext context)
        {
            TreeStructureParams structure = context.Species.structure;
            BranchPath trunk = AddBranch(
                context,
                Vector3.zero,
                Vector3.up,
                structure.trunkLength,
                structure.trunkRadius,
                Vector3.zero,
                0f,
                0f,
                0f,
                true);

            int primaryCount = Mathf.Max(
                context.Settings.BranchCount,
                context.Settings.BranchCount * 2 + context.Settings.MaxDepth - 1);

            for (int i = 0; i < primaryCount && CanAddBranch(context); i++)
            {
                float unit = (i + 0.35f + context.Random.Range(-0.12f, 0.12f)) / primaryCount;
                float attach = Mathf.Lerp(structure.trunkBranchStart, 0.94f, Mathf.Clamp01(unit));
                Vector3 start = trunk.PointAt(attach);

                float azimuth = i * 137.50776f + context.Random.Range(-18f, 18f);
                float angle = structure.branchAngle +
                    context.Random.Range(-structure.branchAngleJitter, structure.branchAngleJitter);
                Vector3 radial = Quaternion.AngleAxis(azimuth, Vector3.up) * Vector3.forward;
                Vector3 direction = (
                    Vector3.up * Mathf.Cos(angle * Mathf.Deg2Rad) +
                    radial * Mathf.Sin(angle * Mathf.Deg2Rad)).normalized;

                float lowerBranchScale = Mathf.Lerp(1.08f, 0.68f, attach);
                float length = structure.trunkLength * structure.lengthDecay * lowerBranchScale *
                    context.Random.Range(0.86f, 1.14f);
                float radius = structure.trunkRadius * structure.radiusDecay *
                    Mathf.Lerp(1f, 0.62f, attach);

                BuildRecursiveBranch(
                    context, start, direction, length, radius, 1,
                    start, 0f, 1f / context.Settings.MaxDepth);
            }
        }

        private static void BuildRecursiveBranch(
            BuildContext context,
            Vector3 start,
            Vector3 direction,
            float length,
            float radius,
            int depth,
            Vector3 windRoot,
            float bendStart,
            float bendEnd)
        {
            if (!CanAddBranch(context) || length < 0.035f || radius < 0.002f)
            {
                return;
            }

            BranchPath path = AddBranch(
                context, start, direction, length, radius, windRoot,
                context.Species.appearance.branchStiffness,
                bendStart, bendEnd, false);

            if (depth >= context.Settings.MaxDepth)
            {
                AddLeaves(context, path.Points[path.Points.Length - 1],
                    path.Directions[path.Directions.Length - 1], windRoot, bendEnd);
                return;
            }

            TreeStructureParams structure = context.Species.structure;
            int remaining = context.Settings.MaxDepth - depth;
            int childCount = Mathf.Max(1, context.Settings.BranchCount - depth / 2);

            for (int i = 0; i < childCount && CanAddBranch(context); i++)
            {
                float attach = childCount == 1
                    ? 0.82f
                    : Mathf.Lerp(0.55f, 0.94f, (i + 0.35f) / childCount);
                attach = Mathf.Clamp(attach + context.Random.Range(-0.07f, 0.07f), 0.45f, 0.97f);

                Vector3 attachDirection = path.DirectionAt(attach);
                Vector3 axis = Perpendicular(attachDirection);
                axis = Quaternion.AngleAxis(
                    i * (360f / childCount) + context.Random.Range(-30f, 30f),
                    attachDirection) * axis;

                float angle = structure.branchAngle * context.Random.Range(0.78f, 1.08f) +
                    context.Random.Range(-structure.branchAngleJitter, structure.branchAngleJitter);
                Vector3 childDirection = (
                    attachDirection * Mathf.Cos(angle * Mathf.Deg2Rad) +
                    axis * Mathf.Sin(angle * Mathf.Deg2Rad)).normalized;

                float childBendStart = Mathf.Lerp(bendStart, bendEnd, attach);
                float childBendEnd = Mathf.Min(
                    1f,
                    childBendStart + (1f - childBendStart) / (remaining + 0.35f));

                BuildRecursiveBranch(
                    context,
                    path.PointAt(attach),
                    childDirection,
                    length * structure.lengthDecay * context.Random.Range(0.86f, 1.12f),
                    radius * structure.radiusDecay,
                    depth + 1,
                    windRoot,
                    childBendStart,
                    childBendEnd);
            }

            if (context.Species.appearance.leafShape != TreeLeafShape.None &&
                depth >= context.Settings.MaxDepth - 1)
            {
                AddLeaves(context, path.Points[path.Points.Length - 1],
                    path.Directions[path.Directions.Length - 1], windRoot, bendEnd);
            }
        }

        private static BranchPath AddBranch(
            BuildContext context,
            Vector3 start,
            Vector3 initialDirection,
            float length,
            float radius,
            Vector3 windRoot,
            float stiffness,
            float bendStart,
            float bendEnd,
            bool trunk)
        {
            context.BranchesBuilt++;

            int segments = context.Settings.SegmentsPerBranch;
            int sides = context.Settings.RadialSegments;
            var points = new Vector3[segments + 1];
            var directions = new Vector3[segments];
            points[0] = start;

            Vector3 direction = initialDirection.normalized;
            float step = length / segments;
            for (int i = 0; i < segments; i++)
            {
                Vector3 side = Perpendicular(direction);
                side = Quaternion.AngleAxis(context.Random.Range(0f, 360f), direction) * side;
                direction = (direction + side *
                    context.Species.structure.crookedness * context.Random.Signed()).normalized;
                directions[i] = direction;
                points[i + 1] = points[i] + direction * step;
            }

            float elementSeed = context.Random.Value01();
            int[,] rings = new int[segments + 1, sides];

            for (int ring = 0; ring <= segments; ring++)
            {
                float ratio = ring / (float)segments;
                Vector3 tangent = ring == 0
                    ? directions[0]
                    : ring == segments ? directions[segments - 1]
                    : (directions[ring - 1] + directions[ring]).normalized;
                Vector3 right = Perpendicular(tangent);
                Vector3 up = Vector3.Cross(tangent, right).normalized;
                float ringRadius = Mathf.Lerp(radius, radius * 0.24f, ratio);
                float bend = trunk ? 0f : Mathf.Lerp(bendStart, bendEnd, ratio);
                Color color = Color.Lerp(
                    context.Species.appearance.barkRootColor,
                    context.Species.appearance.barkTipColor,
                    Mathf.Clamp01((points[ring].y / Mathf.Max(0.01f, context.Species.structure.trunkLength)) * 0.8f));
                color.a = elementSeed;

                for (int sideIndex = 0; sideIndex < sides; sideIndex++)
                {
                    float turn = sideIndex / (float)sides;
                    float angle = turn * Mathf.PI * 2f;
                    Vector3 normal = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                    rings[ring, sideIndex] = context.Buffer.AddVertex(
                        points[ring] + normal * ringRadius,
                        normal,
                        color,
                        new Vector2(turn, bend),
                        windRoot,
                        stiffness);
                }
            }

            for (int ring = 0; ring < segments; ring++)
            {
                for (int sideIndex = 0; sideIndex < sides; sideIndex++)
                {
                    int next = (sideIndex + 1) % sides;
                    context.Buffer.AddQuad(
                        rings[ring, sideIndex],
                        rings[ring, next],
                        rings[ring + 1, next],
                        rings[ring + 1, sideIndex]);
                }
            }

            AddCap(context, rings, points, directions, 0, sides, windRoot, stiffness,
                trunk ? 0f : bendStart, elementSeed, true);
            AddCap(context, rings, points, directions, segments, sides, windRoot, stiffness,
                trunk ? 0f : bendEnd, elementSeed, false);

            return new BranchPath { Points = points, Directions = directions };
        }

        private static void AddCap(
            BuildContext context, int[,] rings, Vector3[] points, Vector3[] directions,
            int ring, int sides, Vector3 windRoot, float stiffness, float bend,
            float seed, bool bottom)
        {
            Vector3 normal = bottom ? -directions[0] : directions[directions.Length - 1];
            Color color = bottom
                ? context.Species.appearance.barkRootColor
                : context.Species.appearance.barkTipColor;
            color.a = seed;
            int center = context.Buffer.AddVertex(
                points[ring], normal, color, new Vector2(0.5f, bend), windRoot, stiffness);

            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                if (bottom)
                {
                    context.Buffer.AddTriangle(center, rings[ring, next], rings[ring, side]);
                }
                else
                {
                    context.Buffer.AddTriangle(center, rings[ring, side], rings[ring, next]);
                }
            }
        }

        private static void AddLeaves(
            BuildContext context, Vector3 origin, Vector3 branchDirection,
            Vector3 windRoot, float bend)
        {
            TreeAppearanceParams appearance = context.Species.appearance;
            if (appearance.leafShape == TreeLeafShape.None)
            {
                return;
            }

            int count = Mathf.Max(1, Mathf.RoundToInt(appearance.leavesPerTip * context.Settings.LeafFraction));
            for (int i = 0; i < count; i++)
            {
                float azimuth = i * (360f / count) + context.Random.Range(-18f, 18f);
                Vector3 radial = Quaternion.AngleAxis(azimuth, branchDirection) * Perpendicular(branchDirection);
                Vector3 leafDirection = (branchDirection * 0.45f + radial).normalized;

                float length = appearance.leafLength * context.Random.Range(0.82f, 1.18f);
                float width = appearance.leafWidth * context.Random.Range(0.82f, 1.18f);
                if (appearance.leafShape == TreeLeafShape.Needle)
                {
                    width *= 0.65f;
                }

                Vector3 basePosition = origin + leafDirection * 0.01f;
                Vector3 tip = basePosition + leafDirection * length;
                Vector3 across = Vector3.Cross(leafDirection, branchDirection).normalized;
                if (across.sqrMagnitude < 1e-6f)
                {
                    across = Perpendicular(leafDirection);
                }

                Color baseColor = appearance.leafBaseColor;
                Color tipColor = appearance.leafTipColor;
                float seed = context.Random.Value01();
                baseColor.a = seed;
                tipColor.a = seed;

                Vector3 middle = Vector3.Lerp(basePosition, tip, 0.55f);
                var positions = new[]
                {
                    basePosition,
                    middle - across * width,
                    tip,
                    middle + across * width,
                };
                var colors = new[] { baseColor, tipColor, tipColor, tipColor };
                var stiffness = new[]
                {
                    appearance.branchStiffness,
                    appearance.leafStiffness,
                    appearance.leafStiffness,
                    appearance.leafStiffness,
                };

                Vector3 normal = Vector3.Cross(positions[1] - positions[0], positions[2] - positions[0]).normalized;
                AddLeafFace(context, positions, colors, stiffness, normal, windRoot, bend, false);
                AddLeafFace(context, positions, colors, stiffness, -normal, windRoot, bend, true);
            }
        }

        private static void AddLeafFace(
            BuildContext context, Vector3[] positions, Color[] colors, float[] stiffness,
            Vector3 normal, Vector3 windRoot, float bend, bool reverse)
        {
            int[] indices = new int[4];
            Vector2[] uv =
            {
                new Vector2(0.5f, bend),
                new Vector2(0f, Mathf.Lerp(bend, 1f, 0.6f)),
                new Vector2(0.5f, 1f),
                new Vector2(1f, Mathf.Lerp(bend, 1f, 0.6f)),
            };

            for (int i = 0; i < 4; i++)
            {
                indices[i] = context.Buffer.AddVertex(
                    positions[i], normal, colors[i], uv[i], windRoot, stiffness[i]);
            }

            if (reverse)
            {
                context.Buffer.AddQuad(indices[0], indices[3], indices[2], indices[1]);
            }
            else
            {
                context.Buffer.AddQuad(indices[0], indices[1], indices[2], indices[3]);
            }
        }

        private static bool CanAddBranch(BuildContext context)
        {
            return context.BranchesBuilt < context.Species.structure.maxBranches;
        }

        private static Vector3 Perpendicular(Vector3 direction)
        {
            Vector3 result = Vector3.Cross(direction, Vector3.up);
            if (result.sqrMagnitude < 1e-6f)
            {
                result = Vector3.Cross(direction, Vector3.right);
            }

            return result.normalized;
        }
    }
}
