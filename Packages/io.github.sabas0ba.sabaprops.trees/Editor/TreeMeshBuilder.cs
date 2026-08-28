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
                0,
                true);

            int primaryCount = PrimaryBranchCount(context);

            for (int i = 0; i < primaryCount && CanAddBranch(context); i++)
            {
                float attach;
                float azimuth;
                PrimaryPlacement(
                    context,
                    i,
                    primaryCount,
                    out attach,
                    out azimuth);
                Vector3 start = trunk.PointAt(attach);

                float angle = structure.branchAngle +
                    context.Random.Range(-structure.branchAngleJitter, structure.branchAngleJitter);
                Vector3 radial = Quaternion.AngleAxis(azimuth, Vector3.up) * Vector3.forward;
                Vector3 direction = (
                    Vector3.up * Mathf.Cos(angle * Mathf.Deg2Rad) +
                    radial * Mathf.Sin(angle * Mathf.Deg2Rad)).normalized;

                float crownPosition = Mathf.InverseLerp(
                    structure.trunkBranchStart,
                    0.94f,
                    attach);
                float crownScale = CrownRadiusScale(
                    structure.crownShape,
                    crownPosition);
                float dominanceScale = Mathf.Lerp(
                    1.08f,
                    0.78f,
                    structure.apicalDominance);
                float length = structure.trunkLength * structure.lengthDecay
                    * crownScale * dominanceScale
                    * context.Random.Range(
                        1f - structure.branchLengthVariance,
                        1f + structure.branchLengthVariance);
                float radius = structure.trunkRadius * structure.radiusDecay *
                    Mathf.Lerp(0.78f, 0.35f, attach);

                BuildRecursiveBranch(
                    context, start, direction, length, radius, 1,
                    start, 0f, 1f / context.Settings.MaxDepth);
            }

            if (context.Species.appearance.leafShape != TreeLeafShape.None
                && structure.apicalDominance > 0.65f)
            {
                AddLeaves(context, trunk, Vector3.zero, 0f);
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
                EffectiveWindResponse(
                    context.Species.appearance,
                    context.Species.appearance.branchStiffness),
                bendStart, bendEnd, depth, false);

            if (depth >= context.Settings.MaxDepth)
            {
                AddLeaves(context, path, windRoot, bendEnd);
                return;
            }

            TreeStructureParams structure = context.Species.structure;
            int remaining = context.Settings.MaxDepth - depth;
            int childCount = ChildBranchCount(context, depth);

            for (int i = 0; i < childCount && CanAddBranch(context); i++)
            {
                bool continuation = i == 0;
                float attach;
                float turn;
                ChildPlacement(
                    context,
                    depth,
                    i,
                    childCount,
                    out attach,
                    out turn);

                Vector3 attachDirection = path.DirectionAt(attach);
                Vector3 axis = Perpendicular(attachDirection);
                axis = Quaternion.AngleAxis(
                    turn,
                    attachDirection) * axis;

                float angle = continuation
                    ? structure.branchAngle
                        * Mathf.Lerp(0.46f, 0.22f, structure.apicalDominance)
                        + context.Random.Range(
                            -structure.branchAngleJitter * 0.3f,
                            structure.branchAngleJitter * 0.3f)
                    : structure.branchAngle * context.Random.Range(0.78f, 1.08f)
                        + context.Random.Range(
                            -structure.branchAngleJitter,
                            structure.branchAngleJitter);
                Vector3 childDirection = (
                    attachDirection * Mathf.Cos(angle * Mathf.Deg2Rad) +
                    axis * Mathf.Sin(angle * Mathf.Deg2Rad)).normalized;
                if (!continuation)
                {
                    float architecturalLift = Mathf.Lerp(
                        0.18f,
                        0.055f,
                        structure.apicalDominance);
                    architecturalLift *= Mathf.Lerp(
                        1f,
                        0.45f,
                        depth / (float)context.Settings.MaxDepth);
                    childDirection = (
                        childDirection + Vector3.up * architecturalLift).normalized;
                }
                childDirection = ConstrainBranchElevation(
                    childDirection,
                    depth + 1,
                    context.Settings.MaxDepth,
                    structure.branchDroop);

                float childBendStart = Mathf.Lerp(bendStart, bendEnd, attach);
                float childBendEnd = Mathf.Min(
                    1f,
                    childBendStart + (1f - childBendStart) / (remaining + 0.35f));

                BuildRecursiveBranch(
                    context,
                    path.PointAt(attach),
                    childDirection,
                    length * structure.lengthDecay
                        * (continuation ? 0.98f : 0.76f)
                        * Mathf.Lerp(
                            0.94f,
                            0.68f,
                            depth / (float)context.Settings.MaxDepth)
                        * context.Random.Range(0.90f, 1.10f),
                    radius * (continuation
                        ? Mathf.Min(0.72f, structure.radiusDecay)
                        : Mathf.Min(
                            structure.radiusDecay,
                            0.88f / Mathf.Sqrt(Mathf.Max(1, childCount - 1)))),
                    depth + 1,
                    windRoot,
                    childBendStart,
                    childBendEnd);
            }

            TreeLeafShape leafShape = context.Species.appearance.leafShape;
            int foliageDepth = Mathf.Min(
                context.Species.appearance.foliageDepth,
                context.Settings.MaxDepth);
            int foliageStartDepth = Mathf.Max(
                1,
                context.Settings.MaxDepth - foliageDepth + 1);
            if (leafShape != TreeLeafShape.None && depth >= foliageStartDepth)
            {
                bool compoundLeafGeometry = leafShape == TreeLeafShape.Palmate
                    || leafShape == TreeLeafShape.Blossom
                    || leafShape == TreeLeafShape.Fan;
                float densityScale = depth == foliageStartDepth
                    && foliageDepth > 2
                        ? compoundLeafGeometry ? 0.2f : 0.5f
                        : 1f;
                AddLeaves(context, path, windRoot, bendEnd, densityScale);
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
            int branchOrder,
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
                float pathRatio = (i + 1f) / segments;
                TreeStructureParams structure = context.Species.structure;
                float terminalRatio = trunk
                    ? 0f
                    : Mathf.Clamp01(
                        (branchOrder - Mathf.Max(1f, context.Settings.MaxDepth - 1f))
                        / Mathf.Max(1f, context.Settings.MaxDepth * 0.35f));
                terminalRatio *= terminalRatio;
                Vector3 tropism = trunk
                    ? Vector3.up * structure.apicalDominance * 0.08f
                    : Vector3.down * structure.branchDroop * terminalRatio
                        * pathRatio * pathRatio * 0.08f
                        + Vector3.up * structure.tipUpturn
                        * (0.03f + pathRatio * pathRatio * 0.11f);
                direction = (direction
                    + side * structure.crookedness * context.Random.Signed()
                    + tropism).normalized;
                if (!trunk)
                {
                    direction = ConstrainBranchElevation(
                        direction,
                        branchOrder,
                        context.Settings.MaxDepth,
                        structure.branchDroop);
                }
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
                bool terminal = branchOrder >= context.Settings.MaxDepth;
                float tipRatio = trunk || terminal
                    ? 0.06f
                    : Mathf.Min(
                        0.72f,
                        context.Species.structure.radiusDecay);
                float taperRatio = trunk ? Mathf.Pow(ratio, 2.4f) : ratio;
                float ringRadius = Mathf.Lerp(
                    radius,
                    radius * tipRatio,
                    taperRatio);
                float bend = trunk ? 0f : Mathf.Lerp(bendStart, bendEnd, ratio);
                float age = trunk
                    ? ratio * 0.35f
                    : Mathf.Clamp01(0.42f + branchOrder * 0.13f + ratio * 0.16f);
                Color color = Color.Lerp(
                    context.Species.appearance.barkRootColor,
                    context.Species.appearance.barkTipColor,
                    age);
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
            if (trunk || branchOrder >= context.Settings.MaxDepth)
            {
                AddCap(
                    context, rings, points, directions, segments, sides,
                    windRoot, stiffness, trunk ? 0f : bendEnd,
                    elementSeed, false);
            }

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
            BuildContext context,
            BranchPath path,
            Vector3 windRoot,
            float bend,
            float densityScale = 1f)
        {
            TreeAppearanceParams appearance = context.Species.appearance;
            if (appearance.leafShape == TreeLeafShape.None)
            {
                return;
            }

            int count = Mathf.Max(
                1,
                Mathf.RoundToInt(
                    appearance.leavesPerTip
                    * context.Settings.LeafFraction
                    * Mathf.Max(0.05f, densityScale)));
            int leavesPerNode = LeavesPerNode(appearance.leafArrangement);
            for (int i = 0; i < count; i++)
            {
                int node = i / leavesPerNode;
                int nodeCount = Mathf.Max(1, Mathf.CeilToInt(count / (float)leavesPerNode));
                int slot = i % leavesPerNode;
                float firstLeaf = appearance.leafArrangement ==
                    TreeLeafArrangement.FasciclePairs ? 0.72f
                    : appearance.leafArrangement == TreeLeafArrangement.Clustered
                        ? 0.80f
                        : 0.42f;
                float along = nodeCount == 1
                    ? 0.9f
                    : Mathf.Lerp(firstLeaf, 0.98f, node / (float)(nodeCount - 1));
                Vector3 origin = path.PointAt(along);
                Vector3 branchDirection = path.DirectionAt(along);
                float azimuth = LeafAzimuth(
                    appearance.leafArrangement,
                    node,
                    slot,
                    leavesPerNode,
                    ref context.Random);
                Vector3 radial = Quaternion.AngleAxis(azimuth, branchDirection) * Perpendicular(branchDirection);
                float forwardBias = appearance.leafShape == TreeLeafShape.Scale
                    ? 0.72f
                    : appearance.leafShape == TreeLeafShape.Needle ? 0.56f : 0.32f;
                Vector3 leafDirection = (branchDirection * forwardBias + radial).normalized;

                float length = appearance.leafLength * context.Random.Range(0.82f, 1.18f);
                float width = appearance.leafWidth * context.Random.Range(0.82f, 1.18f);
                if (appearance.leafShape == TreeLeafShape.Needle)
                {
                    width *= 0.65f;
                }
                else if (appearance.leafShape == TreeLeafShape.Scale)
                {
                    length *= 0.90f;
                    width *= 0.90f;
                }

                Vector3 basePosition = origin + leafDirection * 0.01f;
                if (appearance.leafShape == TreeLeafShape.Palmate)
                {
                    AddPalmateLeaf(
                        context,
                        basePosition,
                        leafDirection,
                        branchDirection,
                        length,
                        width,
                        windRoot,
                        Mathf.Lerp(bend, 1f, along));
                    continue;
                }
                if (appearance.leafShape == TreeLeafShape.Blossom)
                {
                    AddBlossom(
                        context,
                        basePosition,
                        branchDirection,
                        length,
                        width,
                        windRoot,
                        Mathf.Lerp(bend, 1f, along));
                    continue;
                }
                if (appearance.leafShape == TreeLeafShape.Fan)
                {
                    AddFanLeaf(
                        context,
                        basePosition,
                        leafDirection,
                        branchDirection,
                        length,
                        width,
                        windRoot,
                        Mathf.Lerp(bend, 1f, along));
                    continue;
                }
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

        private static void AddPalmateLeaf(
            BuildContext context,
            Vector3 basePosition,
            Vector3 leafDirection,
            Vector3 branchDirection,
            float length,
            float width,
            Vector3 windRoot,
            float bend)
        {
            TreeAppearanceParams appearance = context.Species.appearance;
            Vector3 across = Vector3.Cross(leafDirection, branchDirection).normalized;
            if (across.sqrMagnitude < 1e-6f)
            {
                across = Perpendicular(leafDirection);
            }
            Vector3 normal = Vector3.Cross(across, leafDirection).normalized;
            for (int lobe = 0; lobe < 5; lobe++)
            {
                float fan = Mathf.Lerp(-52f, 52f, lobe / 4f);
                Vector3 direction = Quaternion.AngleAxis(fan, normal) * leafDirection;
                float lobeScale = 1f - Mathf.Abs(lobe - 2) * 0.12f;
                Vector3 tip = basePosition + direction * length * lobeScale;
                Vector3 lobeAcross = Vector3.Cross(normal, direction).normalized;
                Vector3 middle = Vector3.Lerp(basePosition, tip, 0.58f);
                float lobeWidth = width * 0.22f;
                var positions = new[]
                {
                    basePosition,
                    middle - lobeAcross * lobeWidth,
                    tip,
                    middle + lobeAcross * lobeWidth,
                };
                Color baseColor = appearance.leafBaseColor;
                Color tipColor = appearance.leafTipColor;
                float seed = context.Random.Value01();
                baseColor.a = seed;
                tipColor.a = seed;
                var colors = new[] { baseColor, tipColor, tipColor, tipColor };
                var stiffness = new[]
                {
                    appearance.branchStiffness,
                    appearance.leafStiffness,
                    appearance.leafStiffness,
                    appearance.leafStiffness,
                };
                AddLeafFace(context, positions, colors, stiffness, normal, windRoot, bend, false);
                AddLeafFace(context, positions, colors, stiffness, -normal, windRoot, bend, true);
            }
        }

        private static void AddBlossom(
            BuildContext context,
            Vector3 centre,
            Vector3 branchDirection,
            float length,
            float width,
            Vector3 windRoot,
            float bend)
        {
            TreeAppearanceParams appearance = context.Species.appearance;
            Vector3 right = Perpendicular(branchDirection);
            Vector3 up = Vector3.Cross(branchDirection, right).normalized;
            Color centreColor = appearance.leafBaseColor;
            Color petalColor = appearance.leafTipColor;
            float seed = context.Random.Value01();
            centreColor.a = seed;
            petalColor.a = seed;

            for (int petal = 0; petal < 5; petal++)
            {
                float angle = (petal + 0.5f) * 72f
                    + context.Random.Range(-4f, 4f);
                Vector3 direction = right * Mathf.Cos(angle * Mathf.Deg2Rad)
                    + up * Mathf.Sin(angle * Mathf.Deg2Rad);
                Vector3 across = Vector3.Cross(branchDirection, direction).normalized;
                Vector3 tip = centre + direction * length;
                Vector3 middle = Vector3.Lerp(centre, tip, 0.58f);
                var positions = new[]
                {
                    centre,
                    middle - across * width,
                    tip,
                    middle + across * width,
                };
                var colors = new[]
                {
                    centreColor,
                    petalColor,
                    petalColor,
                    petalColor,
                };
                var stiffness = new[]
                {
                    appearance.branchStiffness,
                    appearance.leafStiffness,
                    appearance.leafStiffness,
                    appearance.leafStiffness,
                };
                AddLeafFace(
                    context, positions, colors, stiffness,
                    branchDirection, windRoot, bend, false);
                AddLeafFace(
                    context, positions, colors, stiffness,
                    -branchDirection, windRoot, bend, true);
            }
        }

        private static void AddFanLeaf(
            BuildContext context,
            Vector3 basePosition,
            Vector3 leafDirection,
            Vector3 branchDirection,
            float length,
            float width,
            Vector3 windRoot,
            float bend)
        {
            TreeAppearanceParams appearance = context.Species.appearance;
            Vector3 across = Vector3.Cross(leafDirection, branchDirection).normalized;
            if (across.sqrMagnitude < 1e-6f)
            {
                across = Perpendicular(leafDirection);
            }
            Vector3 normal = Vector3.Cross(across, leafDirection).normalized;
            Vector3 fanBase = basePosition + leafDirection * length * 0.24f;
            Color baseColor = appearance.leafBaseColor;
            Color tipColor = appearance.leafTipColor;
            float seed = context.Random.Value01();
            baseColor.a = seed;
            tipColor.a = seed;

            const int segments = 5;
            for (int segment = 0; segment < segments; segment++)
            {
                float leftAngle = Mathf.Lerp(-68f, 68f, segment / (float)segments);
                float rightAngle = Mathf.Lerp(-68f, 68f, (segment + 1f) / segments);
                float centreAngle = (leftAngle + rightAngle) * 0.5f;
                Vector3 leftDirection = Quaternion.AngleAxis(leftAngle, normal) * leafDirection;
                Vector3 centreDirection = Quaternion.AngleAxis(centreAngle, normal) * leafDirection;
                Vector3 rightDirection = Quaternion.AngleAxis(rightAngle, normal) * leafDirection;
                float radius = length * 0.76f;
                var positions = new[]
                {
                    basePosition,
                    fanBase + leftDirection * radius + across * width * 0.08f,
                    fanBase + centreDirection * radius,
                    fanBase + rightDirection * radius - across * width * 0.08f,
                };
                var colors = new[] { baseColor, tipColor, tipColor, tipColor };
                var stiffness = new[]
                {
                    appearance.branchStiffness,
                    appearance.leafStiffness,
                    appearance.leafStiffness,
                    appearance.leafStiffness,
                };
                AddLeafFace(
                    context, positions, colors, stiffness,
                    normal, windRoot, bend, false);
                AddLeafFace(
                    context, positions, colors, stiffness,
                    -normal, windRoot, bend, true);
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
                    positions[i], normal, colors[i], uv[i], windRoot,
                    EffectiveWindResponse(context.Species.appearance, stiffness[i]));
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

        private static int PrimaryBranchCount(BuildContext context)
        {
            TreeStructureParams structure = context.Species.structure;
            int tiers = Mathf.Max(2, context.Settings.MaxDepth + 1);
            int denseTiers = Mathf.Max(
                2,
                Mathf.RoundToInt(tiers * structure.crownDensity));
            switch (structure.branchArrangement)
            {
                case TreeBranchArrangement.Opposite:
                    return denseTiers * 2;
                case TreeBranchArrangement.Whorled:
                    return denseTiers * structure.whorlSize;
                default:
                    return Mathf.Max(
                        context.Settings.BranchCount,
                        Mathf.RoundToInt(
                            (context.Settings.BranchCount * 2
                                + context.Settings.MaxDepth - 1)
                            * structure.crownDensity));
            }
        }

        private static float EffectiveWindResponse(
            TreeAppearanceParams appearance,
            float stiffness)
        {
            if (!appearance.windEnabled)
            {
                return 0f;
            }

            return Mathf.Clamp01(stiffness * appearance.windResponse);
        }

        private static void PrimaryPlacement(
            BuildContext context,
            int index,
            int count,
            out float attach,
            out float azimuth)
        {
            TreeStructureParams structure = context.Species.structure;
            int groupSize = structure.branchArrangement == TreeBranchArrangement.Opposite
                ? 2
                : structure.branchArrangement == TreeBranchArrangement.Whorled
                    ? structure.whorlSize
                    : 1;
            int group = index / groupSize;
            int slot = index % groupSize;
            int groupCount = Mathf.Max(1, Mathf.CeilToInt(count / (float)groupSize));
            float unit = (group + 0.42f
                + context.Random.Range(-0.10f, 0.10f)) / groupCount;
            attach = Mathf.Lerp(
                structure.trunkBranchStart,
                0.94f,
                Mathf.Clamp01(unit));

            switch (structure.branchArrangement)
            {
                case TreeBranchArrangement.Opposite:
                    azimuth = group * 97f + slot * 180f;
                    break;
                case TreeBranchArrangement.Whorled:
                    azimuth = group * 23f + slot * (360f / groupSize);
                    break;
                case TreeBranchArrangement.Irregular:
                    azimuth = context.Random.Range(0f, 360f);
                    attach = Mathf.Clamp(
                        attach + context.Random.Range(-0.09f, 0.09f),
                        structure.trunkBranchStart,
                        0.96f);
                    break;
                case TreeBranchArrangement.Spiral:
                default:
                    azimuth = index * 137.50776f;
                    break;
            }
            azimuth += context.Random.Range(
                -structure.azimuthJitter,
                structure.azimuthJitter);
        }

        private static int ChildBranchCount(BuildContext context, int depth)
        {
            TreeStructureParams structure = context.Species.structure;
            if (structure.branchArrangement == TreeBranchArrangement.Opposite)
            {
                return depth == 1 ? 3 : 2;
            }
            if (structure.branchArrangement == TreeBranchArrangement.Whorled
                && depth == 1)
            {
                return 1 + Mathf.Max(
                    2,
                    Mathf.Min(structure.whorlSize, context.Settings.BranchCount));
            }
            return Mathf.Max(2, context.Settings.BranchCount - depth / 2);
        }

        private static void ChildPlacement(
            BuildContext context,
            int depth,
            int index,
            int count,
            out float attach,
            out float turn)
        {
            TreeStructureParams structure = context.Species.structure;
            if (index == 0)
            {
                attach = 1f;
                turn = depth * 137.50776f
                    + context.Random.Range(
                        -structure.azimuthJitter,
                        structure.azimuthJitter);
                return;
            }

            int lateralIndex = index - 1;
            int lateralCount = Mathf.Max(1, count - 1);
            bool grouped = structure.branchArrangement == TreeBranchArrangement.Opposite
                || structure.branchArrangement == TreeBranchArrangement.Whorled;
            attach = grouped
                ? Mathf.Lerp(0.68f, 0.88f, depth / (float)context.Settings.MaxDepth)
                : count == 1
                    ? 0.82f
                    : Mathf.Lerp(
                        0.52f,
                        0.88f,
                        (lateralIndex + 0.35f) / lateralCount);
            attach = Mathf.Clamp(
                attach + context.Random.Range(-0.055f, 0.055f),
                0.45f,
                0.97f);

            if (structure.branchArrangement == TreeBranchArrangement.Opposite)
            {
                turn = depth * 83f + index * 180f;
            }
            else if (structure.branchArrangement == TreeBranchArrangement.Whorled)
            {
                turn = depth * 31f
                    + lateralIndex * (360f / lateralCount);
            }
            else if (structure.branchArrangement == TreeBranchArrangement.Irregular)
            {
                turn = context.Random.Range(0f, 360f);
            }
            else
            {
                turn = lateralIndex * 137.50776f + depth * 47f;
            }
            turn += context.Random.Range(
                -structure.azimuthJitter,
                structure.azimuthJitter);
        }

        private static float CrownRadiusScale(TreeCrownShape shape, float height)
        {
            height = Mathf.Clamp01(height);
            switch (shape)
            {
                case TreeCrownShape.Vase:
                    return Mathf.Lerp(0.55f, 1.12f, height)
                        * Mathf.Lerp(1f, 0.72f, Mathf.InverseLerp(0.82f, 1f, height));
                case TreeCrownShape.Layered:
                    return (0.72f + Mathf.Sin(height * Mathf.PI) * 0.32f)
                        * (0.90f + Mathf.Sin(height * Mathf.PI * 4f) * 0.10f);
                case TreeCrownShape.Pyramidal:
                    return Mathf.Lerp(1.12f, 0.24f, height);
                case TreeCrownShape.OpenIrregular:
                    return 0.68f + Mathf.Sin(height * Mathf.PI) * 0.34f;
                case TreeCrownShape.Rounded:
                default:
                    return 0.62f + Mathf.Sin(height * Mathf.PI) * 0.48f;
            }
        }

        private static int LeavesPerNode(TreeLeafArrangement arrangement)
        {
            switch (arrangement)
            {
                case TreeLeafArrangement.Opposite:
                case TreeLeafArrangement.FasciclePairs:
                    return 2;
                case TreeLeafArrangement.Whorled:
                    return 3;
                case TreeLeafArrangement.Clustered:
                    return 4;
                default:
                    return 1;
            }
        }

        private static float LeafAzimuth(
            TreeLeafArrangement arrangement,
            int node,
            int slot,
            int leavesPerNode,
            ref FoliageRandom random)
        {
            float jitter = random.Range(-12f, 12f);
            switch (arrangement)
            {
                case TreeLeafArrangement.Opposite:
                    return node * 90f + slot * 180f + jitter;
                case TreeLeafArrangement.Whorled:
                    return node * 47f + slot * (360f / leavesPerNode) + jitter;
                case TreeLeafArrangement.FasciclePairs:
                    return node * 137.50776f + (slot == 0 ? -9f : 9f) + jitter * 0.35f;
                case TreeLeafArrangement.Clustered:
                    return node * 31f + slot * (360f / leavesPerNode) + jitter * 0.45f;
                case TreeLeafArrangement.Alternate:
                default:
                    return node * 137.50776f + jitter;
            }
        }

        private static bool CanAddBranch(BuildContext context)
        {
            return context.BranchesBuilt < context.Species.structure.maxBranches;
        }

        private static Vector3 ConstrainBranchElevation(
            Vector3 direction,
            int branchOrder,
            int maximumDepth,
            float terminalDroop)
        {
            direction = direction.normalized;
            float terminalRatio = maximumDepth <= 1
                ? 1f
                : Mathf.Clamp01(
                    (branchOrder - (maximumDepth - 1f))
                    / Mathf.Max(1f, maximumDepth * 0.35f));
            float minimumY = Mathf.Lerp(
                0.025f,
                -0.04f * Mathf.Clamp01(terminalDroop),
                terminalRatio * terminalRatio);
            if (direction.y >= minimumY)
            {
                return direction;
            }

            Vector3 horizontal = new Vector3(direction.x, 0f, direction.z);
            if (horizontal.sqrMagnitude < 1e-6f)
            {
                horizontal = Vector3.forward;
            }
            horizontal.Normalize();
            float horizontalScale = Mathf.Sqrt(
                Mathf.Max(0f, 1f - minimumY * minimumY));
            return (horizontal * horizontalScale + Vector3.up * minimumY).normalized;
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
