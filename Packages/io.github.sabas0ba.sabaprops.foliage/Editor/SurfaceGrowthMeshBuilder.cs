using System.Collections.Generic;
using UnityEngine;

namespace SabaProps.Foliage.Editors
{
    /// <summary>Bakes a surface-growth graph into the package's foliage vertex contract.</summary>
    public static class SurfaceGrowthMeshBuilder
    {
        public static Mesh BuildVine(
            SurfaceGrowthGraph graph,
            SurfaceGrowthSettings growth,
            SurfaceVineParams morphology)
        {
            growth = growth ?? new SurfaceGrowthSettings();
            morphology = morphology ?? new SurfaceVineParams();
            var buffer = new FoliageMeshBuffer();
            IReadOnlyList<SurfaceGrowthNode> nodes = graph != null
                ? graph.Nodes
                : new List<SurfaceGrowthNode>();
            var random = new FoliageRandom(growth.seed ^ 0x5A17);

            if (morphology.rootAnchorLength > 0f)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    SurfaceGrowthNode root = nodes[i];
                    if (root.parentIndex >= 0)
                    {
                        continue;
                    }

                    int firstChild = FirstChild(nodes, i);
                    if (firstChild < 0)
                    {
                        continue;
                    }
                    Vector3 tangent = (
                        nodes[firstChild].position - root.position).normalized;
                    if (tangent.sqrMagnitude < 1e-6f)
                    {
                        continue;
                    }
                    var anchor = new SurfaceGrowthNode(
                        root.position - tangent * morphology.rootAnchorLength,
                        root.normal,
                        -1,
                        root.branchDepth,
                        -morphology.rootAnchorLength);
                    AddSurfaceStem(
                        buffer,
                        anchor,
                        root,
                        Mathf.Max(0.001f, morphology.stemWidth)
                            * morphology.rootCollarScale,
                        morphology.stemRootColor,
                        1f,
                        random.Value01());
                }
            }

            float maximumDistance = MaximumDistance(nodes);
            for (int i = 0; i < nodes.Count; i++)
            {
                SurfaceGrowthNode node = nodes[i];
                if (node.parentIndex < 0 || node.parentIndex >= nodes.Count)
                {
                    continue;
                }

                SurfaceGrowthNode parent = nodes[node.parentIndex];
                float age = maximumDistance > 1e-5f
                    ? node.distanceFromRoot / maximumDistance
                    : 0f;
                AddSurfaceStem(
                    buffer,
                    parent,
                    node,
                    Mathf.Max(0.001f, morphology.stemWidth),
                    Color.Lerp(morphology.stemRootColor, morphology.stemTipColor, age),
                    morphology.stemStiffness,
                    random.Value01());
            }

            float density = morphology.leavesPerMetre * growth.coverage;
            float interval = density > 1e-5f ? 1f / density : float.MaxValue;
            if (interval < float.MaxValue)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    SurfaceGrowthNode node = nodes[i];
                    if (node.parentIndex < 0 || node.parentIndex >= nodes.Count)
                    {
                        continue;
                    }

                    SurfaceGrowthNode parent = nodes[node.parentIndex];
                    int first = Mathf.Max(
                        0,
                        Mathf.FloorToInt(parent.distanceFromRoot / interval) - 1);
                    int last = Mathf.CeilToInt(node.distanceFromRoot / interval) + 1;
                    for (int leafIndex = first; leafIndex <= last; leafIndex++)
                    {
                        float spacingOffset = 0.5f
                            + (Hash01(
                                leafIndex,
                                node.branchDepth,
                                growth.seed) - 0.5f)
                            * morphology.leafSpacingJitter;
                        float targetDistance = (leafIndex + spacingOffset) * interval;
                        if (targetDistance <= parent.distanceFromRoot
                            || targetDistance > node.distanceFromRoot)
                        {
                            continue;
                        }
                        float edgeDistance = node.distanceFromRoot - parent.distanceFromRoot;
                        float t = edgeDistance > 1e-5f
                            ? Mathf.Clamp01(
                                (targetDistance - parent.distanceFromRoot) / edgeDistance)
                            : 1f;
                        Vector3 attach = Vector3.Lerp(parent.position, node.position, t);
                        Vector3 normal = Vector3.Lerp(parent.normal, node.normal, t).normalized;
                        Vector3 tangent = (node.position - parent.position).normalized;
                        Vector3 side = Vector3.Cross(normal, tangent).normalized;
                        if (side.sqrMagnitude < 1e-6f)
                        {
                            side = AnyTangent(normal);
                        }
                        float length = random.Range(
                            Mathf.Max(0.005f, morphology.minimumLeafLength),
                            Mathf.Max(
                                morphology.minimumLeafLength,
                                morphology.maximumLeafLength));
                        float width = length * morphology.leafWidthRatio;
                        Color color = SelectVineLeafColor(
                            morphology,
                            targetDistance / Mathf.Max(interval, maximumDistance),
                            ref random);
                        int leavesAtNode = LeavesAtNode(morphology.leafArrangement);
                        for (int leaf = 0; leaf < leavesAtNode; leaf++)
                        {
                            float baseAngle = ArrangementAngle(
                                morphology.leafArrangement,
                                leafIndex,
                                leaf,
                                leavesAtNode,
                                ref random);
                            float angle = baseAngle + random.Range(
                                -morphology.leafAngleJitter,
                                morphology.leafAngleJitter);
                            Vector3 radial = Quaternion.AngleAxis(angle, normal) * side;
                            Vector3 direction = (
                                radial
                                + tangent * random.Range(-0.18f, 0.18f)
                                + Vector3.down * morphology.leafDroop).normalized;
                            AddVineLeaf(
                                buffer,
                                morphology,
                                attach + normal * 0.002f,
                                direction,
                                normal,
                                length * random.Range(0.94f, 1.06f),
                                width * random.Range(0.94f, 1.06f),
                                color,
                                ref random);
                        }
                    }
                }
            }

            return buffer.ToMesh(
                "SabaFoliage_SurfaceVine",
                Mathf.Max(morphology.maximumLeafLength, morphology.stemWidth) * 1.5f);
        }

        public static Mesh BuildRhizomePatch(
            SurfaceGrowthGraph graph,
            SurfaceGrowthSettings growth,
            RhizomePatchParams morphology)
        {
            growth = growth ?? new SurfaceGrowthSettings();
            morphology = morphology ?? new RhizomePatchParams();
            var buffer = new FoliageMeshBuffer();
            IReadOnlyList<SurfaceGrowthNode> nodes = graph != null
                ? graph.Nodes
                : new List<SurfaceGrowthNode>();
            var random = new FoliageRandom(growth.seed ^ 0x31D4);

            if (morphology.renderRhizomes)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    SurfaceGrowthNode node = nodes[i];
                    if (node.parentIndex < 0 || node.parentIndex >= nodes.Count)
                    {
                        continue;
                    }
                    AddSurfaceStem(
                        buffer,
                        nodes[node.parentIndex],
                        node,
                        morphology.rhizomeWidth,
                        morphology.rhizomeColor,
                        1f,
                        random.Value01());
                }
            }

            float density = morphology.shootsPerMetre * growth.coverage;
            float interval = density > 1e-5f ? 1f / density : float.MaxValue;
            for (int i = 0; i < nodes.Count; i++)
            {
                SurfaceGrowthNode node = nodes[i];
                bool isRoot = node.parentIndex < 0;
                bool crossedInterval = false;
                if (!isRoot && node.parentIndex < nodes.Count && interval < float.MaxValue)
                {
                    SurfaceGrowthNode parent = nodes[node.parentIndex];
                    crossedInterval = Mathf.FloorToInt(node.distanceFromRoot / interval)
                        > Mathf.FloorToInt(parent.distanceFromRoot / interval);
                }
                if (!isRoot && !crossedInterval)
                {
                    continue;
                }
                if (isRoot && i > 0 && !random.Chance(growth.coverage))
                {
                    continue;
                }

                AddRhizomeShoot(buffer, node, morphology, ref random);
            }

            float padding = Mathf.Max(
                morphology.shootHeight.x,
                morphology.shootHeight.y)
                + Mathf.Max(morphology.leafLength.x, morphology.leafLength.y);
            return buffer.ToMesh("SabaFoliage_RhizomePatch", padding * 0.25f);
        }

        private static void AddSurfaceStem(
            FoliageMeshBuffer buffer,
            SurfaceGrowthNode parent,
            SurfaceGrowthNode node,
            float width,
            Color color,
            float stiffness,
            float elementSeed)
        {
            Vector3 tangent = (node.position - parent.position).normalized;
            if (tangent.sqrMagnitude < 1e-6f)
            {
                return;
            }
            Vector3 parentSide = Vector3.Cross(parent.normal, tangent).normalized;
            Vector3 nodeSide = Vector3.Cross(node.normal, tangent).normalized;
            if (parentSide.sqrMagnitude < 1e-6f)
            {
                parentSide = AnyTangent(parent.normal);
            }
            if (nodeSide.sqrMagnitude < 1e-6f)
            {
                nodeSide = parentSide;
            }

            float halfRoot = Mathf.Max(0.0005f, width * 0.5f);
            float halfTip = halfRoot * 0.82f;
            Vector4 pivot = new Vector4(
                parent.position.x,
                parent.position.y,
                parent.position.z,
                Mathf.Clamp01(stiffness));
            color.a = elementSeed;
            int a = buffer.AddVertex(
                parent.position - parentSide * halfRoot,
                parent.normal,
                color,
                new Vector2(0f, 0f),
                pivot,
                true);
            int b = buffer.AddVertex(
                parent.position + parentSide * halfRoot,
                parent.normal,
                color,
                new Vector2(1f, 0f),
                pivot,
                true);
            int c = buffer.AddVertex(
                node.position + nodeSide * halfTip,
                node.normal,
                color,
                new Vector2(1f, 0f),
                pivot,
                true);
            int d = buffer.AddVertex(
                node.position - nodeSide * halfTip,
                node.normal,
                color,
                new Vector2(0f, 0f),
                pivot,
                true);
            AddOrientedQuad(buffer, a, b, c, d, parent.normal);
        }

        private static int FirstChild(
            IReadOnlyList<SurfaceGrowthNode> nodes,
            int parentIndex)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].parentIndex == parentIndex)
                {
                    return i;
                }
            }
            return -1;
        }

        private static void AddRhizomeShoot(
            FoliageMeshBuffer buffer,
            SurfaceGrowthNode node,
            RhizomePatchParams p,
            ref FoliageRandom random)
        {
            Vector3 normal = node.normal.normalized;
            Vector3 sideA = RandomTangent(normal, ref random);
            Vector3 sideB = Vector3.Cross(normal, sideA).normalized;
            float minimumHeight = Mathf.Min(p.shootHeight.x, p.shootHeight.y);
            float maximumHeight = Mathf.Max(p.shootHeight.x, p.shootHeight.y);
            float height = Mathf.Max(0.01f, random.Range(minimumHeight, maximumHeight));
            Vector3 top = node.position + normal * height;
            Vector4 root = new Vector4(
                node.position.x,
                node.position.y,
                node.position.z,
                Mathf.Clamp01(p.stiffness));
            Color stem = p.stemColor;
            stem.a = random.Value01();

            AddCrossedShootStem(
                buffer,
                node.position,
                top,
                sideA,
                sideB,
                p.stemWidth,
                normal,
                stem,
                root);

            int leafCount = Mathf.Max(1, p.leavesPerShoot);
            for (int leaf = 0; leaf < leafCount; leaf++)
            {
                float t = (leaf + 1f) / (leafCount + 1.35f);
                Vector3 attach = Vector3.Lerp(node.position, top, t);
                float angle = leaf / (float)leafCount * Mathf.PI * 2f
                    + random.Range(-0.45f, 0.45f);
                Vector3 direction = (
                    sideA * Mathf.Cos(angle)
                    + sideB * Mathf.Sin(angle)).normalized;
                float length = random.Range(
                    Mathf.Min(p.leafLength.x, p.leafLength.y),
                    Mathf.Max(p.leafLength.x, p.leafLength.y));
                Color leafColor = random.Chance(p.accentAmount)
                    ? Color.Lerp(p.leafColor, p.leafAccentColor, random.Range(0.55f, 1f))
                    : p.leafColor;
                leafColor = JitterColour(leafColor, ref random, 0.08f);
                AddLeaf(
                    buffer,
                    p.leafShape,
                    attach,
                    direction,
                    normal,
                    Mathf.Max(0.005f, length),
                    Mathf.Max(0.004f, length * p.leafWidthRatio),
                    leafColor,
                    p.stiffness,
                    random.Value01());
            }

            if (random.Chance(p.flowerChance))
            {
                AddHouttuyniaFlower(
                    buffer,
                    top,
                    normal,
                    sideA,
                    sideB,
                    p,
                    root,
                    random.Value01());
            }
        }

        private static void AddCrossedShootStem(
            FoliageMeshBuffer buffer,
            Vector3 root,
            Vector3 top,
            Vector3 sideA,
            Vector3 sideB,
            float width,
            Vector3 normal,
            Color color,
            Vector4 rootData)
        {
            Vector3[] sides = { sideA, sideB };
            float half = Mathf.Max(0.0005f, width * 0.5f);
            foreach (Vector3 side in sides)
            {
                Vector3 faceNormal = Vector3.Cross(side, normal).normalized;
                int a = buffer.AddVertex(root - side * half, faceNormal, color, new Vector2(0f, 0f), rootData);
                int b = buffer.AddVertex(root + side * half, faceNormal, color, new Vector2(1f, 0f), rootData);
                int c = buffer.AddVertex(top + side * half * 0.7f, faceNormal, color, new Vector2(1f, 1f), rootData);
                int d = buffer.AddVertex(top - side * half * 0.7f, faceNormal, color, new Vector2(0f, 1f), rootData);
                AddOrientedQuad(buffer, a, b, c, d, faceNormal);
            }
        }

        private static void AddVineLeaf(
            FoliageMeshBuffer buffer,
            SurfaceVineParams p,
            Vector3 attach,
            Vector3 forward,
            Vector3 normal,
            float length,
            float width,
            Color color,
            ref FoliageRandom random)
        {
            float petioleLength = length * p.petioleLengthRatio;
            Vector3 leafBase = attach + forward * petioleLength;
            Color petiole = Color.Lerp(color, p.petioleColor, p.pigmentAmount);
            var root = new SurfaceGrowthNode(attach, normal, -1, 0, 0f);
            var tip = new SurfaceGrowthNode(leafBase, normal, 0, 0, petioleLength);
            AddSurfaceStem(
                buffer,
                root,
                tip,
                Mathf.Max(0.0006f, length * p.petioleWidthRatio),
                petiole,
                p.leafStiffness,
                random.Value01());

            AddLeaf(
                buffer,
                p.leafShape,
                leafBase,
                forward,
                normal,
                length,
                width,
                color,
                Color.Lerp(color, p.edgeColor, p.pigmentAmount),
                Color.Lerp(color, p.veinColor, p.pigmentAmount * 0.42f),
                p.pigmentPattern,
                p.edgeWidth,
                p.leafStiffness,
                random.Value01());
        }

        private static void AddLeaf(
            FoliageMeshBuffer buffer,
            SurfaceLeafShape shape,
            Vector3 attach,
            Vector3 forward,
            Vector3 normal,
            float length,
            float width,
            Color color,
            float stiffness,
            float elementSeed)
        {
            AddLeaf(
                buffer,
                shape,
                attach,
                forward,
                normal,
                length,
                width,
                color,
                color,
                color,
                SurfaceLeafPigmentPattern.Solid,
                0.1f,
                stiffness,
                elementSeed);
        }

        private static void AddLeaf(
            FoliageMeshBuffer buffer,
            SurfaceLeafShape shape,
            Vector3 attach,
            Vector3 forward,
            Vector3 normal,
            float length,
            float width,
            Color color,
            Color edgeColor,
            Color veinColor,
            SurfaceLeafPigmentPattern pigmentPattern,
            float edgeWidth,
            float stiffness,
            float elementSeed)
        {
            normal = normal.normalized;
            forward = ProjectOnPlane(forward, normal).normalized;
            if (forward.sqrMagnitude < 1e-6f)
            {
                forward = AnyTangent(normal);
            }
            Vector3 side = Vector3.Cross(normal, forward).normalized;
            Vector2[] profile = LeafProfile(shape);
            Vector4 pivot = new Vector4(
                attach.x,
                attach.y,
                attach.z,
                Mathf.Clamp01(stiffness));
            bool hasEdge = pigmentPattern == SurfaceLeafPigmentPattern.Edge
                || pigmentPattern == SurfaceLeafPigmentPattern.EdgeAndVein;
            bool hasVein = pigmentPattern == SurfaceLeafPigmentPattern.Vein
                || pigmentPattern == SurfaceLeafPigmentPattern.EdgeAndVein;
            if (pigmentPattern == SurfaceLeafPigmentPattern.Solid)
            {
                color = edgeColor;
            }
            color.a = elementSeed;
            edgeColor.a = elementSeed;
            veinColor.a = elementSeed;

            Vector2 profileCentre = new Vector2(0f, 0.46f);
            Vector3 centrePosition = attach + forward * (length * profileCentre.y);
            int centre = buffer.AddVertex(
                centrePosition,
                normal,
                color,
                new Vector2(0.5f, profileCentre.y),
                pivot,
                true);
            var inner = new int[profile.Length];
            var outline = hasEdge ? new int[profile.Length] : inner;
            float innerScale = 1f - Mathf.Clamp(edgeWidth, 0.02f, 0.4f);
            for (int i = 0; i < profile.Length; i++)
            {
                Vector2 point = profile[i];
                Vector2 innerPoint = hasEdge
                    ? profileCentre + (point - profileCentre) * innerScale
                    : point;
                Color innerColor = color;
                if (pigmentPattern == SurfaceLeafPigmentPattern.Mottled
                    && (i % 3) == 0)
                {
                    innerColor = Color.Lerp(color, edgeColor, 0.55f);
                    innerColor.a = elementSeed;
                }
                inner[i] = buffer.AddVertex(
                    ProfilePosition(attach, side, forward, innerPoint, width, length),
                    normal,
                    innerColor,
                    new Vector2(innerPoint.x + 0.5f, innerPoint.y),
                    pivot,
                    true);
                if (hasEdge)
                {
                    outline[i] = buffer.AddVertex(
                        ProfilePosition(attach, side, forward, point, width, length),
                        normal,
                        edgeColor,
                        new Vector2(point.x + 0.5f, point.y),
                        pivot,
                        true);
                }
            }

            for (int i = 0; i < inner.Length; i++)
            {
                int next = (i + 1) % inner.Length;
                AddOrientedTriangle(buffer, centre, inner[i], inner[next], normal);
                if (hasEdge)
                {
                    AddOrientedQuad(
                        buffer,
                        inner[i],
                        outline[i],
                        outline[next],
                        inner[next],
                        normal);
                }
            }

            if (hasVein)
            {
                AddLeafVein(
                    buffer,
                    attach,
                    forward,
                    side,
                    normal,
                    length,
                    Mathf.Max(0.00025f, width * 0.014f),
                    veinColor,
                    pivot);
            }
        }

        private static Vector3 ProfilePosition(
            Vector3 attach,
            Vector3 side,
            Vector3 forward,
            Vector2 point,
            float width,
            float length)
        {
            return attach + side * (point.x * width) + forward * (point.y * length);
        }

        private static void AddLeafVein(
            FoliageMeshBuffer buffer,
            Vector3 attach,
            Vector3 forward,
            Vector3 side,
            Vector3 normal,
            float length,
            float halfWidth,
            Color color,
            Vector4 pivot)
        {
            Vector3 lift = normal * 0.0006f;
            Vector3 start = attach + forward * (length * 0.08f) + lift;
            Vector3 end = attach + forward * (length * 0.88f) + lift;
            int a = buffer.AddVertex(
                start - side * halfWidth, normal, color,
                new Vector2(0f, 0f), pivot, true);
            int b = buffer.AddVertex(
                start + side * halfWidth, normal, color,
                new Vector2(1f, 0f), pivot, true);
            int c = buffer.AddVertex(
                end + side * halfWidth * 0.35f, normal, color,
                new Vector2(1f, 1f), pivot, true);
            int d = buffer.AddVertex(
                end - side * halfWidth * 0.35f, normal, color,
                new Vector2(0f, 1f), pivot, true);
            AddOrientedQuad(buffer, a, b, c, d, normal);
        }

        private static Vector2[] LeafProfile(SurfaceLeafShape shape)
        {
            switch (shape)
            {
                case SurfaceLeafShape.Lobed:
                    return new[]
                    {
                        new Vector2(-0.08f, 0.02f),
                        new Vector2(-0.25f, 0.10f),
                        new Vector2(-0.39f, 0.20f),
                        new Vector2(-0.36f, 0.30f),
                        new Vector2(-0.28f, 0.37f),
                        new Vector2(-0.43f, 0.47f),
                        new Vector2(-0.50f, 0.57f),
                        new Vector2(-0.43f, 0.65f),
                        new Vector2(-0.29f, 0.70f),
                        new Vector2(-0.31f, 0.82f),
                        new Vector2(-0.22f, 0.91f),
                        new Vector2(0f, 1f),
                        new Vector2(0.22f, 0.91f),
                        new Vector2(0.31f, 0.82f),
                        new Vector2(0.29f, 0.70f),
                        new Vector2(0.43f, 0.65f),
                        new Vector2(0.50f, 0.57f),
                        new Vector2(0.43f, 0.47f),
                        new Vector2(0.28f, 0.37f),
                        new Vector2(0.36f, 0.30f),
                        new Vector2(0.39f, 0.20f),
                        new Vector2(0.25f, 0.10f),
                        new Vector2(0.08f, 0.02f),
                        new Vector2(0f, 0.09f),
                    };
                case SurfaceLeafShape.Ovate:
                    return new[]
                    {
                        new Vector2(0f, 0f),
                        new Vector2(-0.34f, 0.16f),
                        new Vector2(-0.50f, 0.42f),
                        new Vector2(-0.40f, 0.70f),
                        new Vector2(-0.18f, 0.90f),
                        new Vector2(0f, 1f),
                        new Vector2(0.18f, 0.90f),
                        new Vector2(0.40f, 0.70f),
                        new Vector2(0.50f, 0.42f),
                        new Vector2(0.34f, 0.16f),
                    };
                case SurfaceLeafShape.Orbicular:
                    return new[]
                    {
                        new Vector2(0f, 0f),
                        new Vector2(-0.35f, 0.12f),
                        new Vector2(-0.50f, 0.38f),
                        new Vector2(-0.46f, 0.70f),
                        new Vector2(-0.22f, 0.94f),
                        new Vector2(0f, 1f),
                        new Vector2(0.22f, 0.94f),
                        new Vector2(0.46f, 0.70f),
                        new Vector2(0.50f, 0.38f),
                        new Vector2(0.35f, 0.12f),
                    };
                case SurfaceLeafShape.Cordate:
                default:
                    return new[]
                    {
                        new Vector2(0f, 0.13f),
                        new Vector2(-0.18f, 0.01f),
                        new Vector2(-0.45f, 0.13f),
                        new Vector2(-0.50f, 0.39f),
                        new Vector2(-0.37f, 0.65f),
                        new Vector2(-0.17f, 0.86f),
                        new Vector2(0f, 1f),
                        new Vector2(0.17f, 0.86f),
                        new Vector2(0.37f, 0.65f),
                        new Vector2(0.50f, 0.39f),
                        new Vector2(0.45f, 0.13f),
                        new Vector2(0.18f, 0.01f),
                    };
            }
        }

        private static void AddHouttuyniaFlower(
            FoliageMeshBuffer buffer,
            Vector3 centre,
            Vector3 normal,
            Vector3 axisA,
            Vector3 axisB,
            RhizomePatchParams p,
            Vector4 root,
            float elementSeed)
        {
            float radius = Mathf.Max(0.003f, p.flowerRadius);
            Color bract = p.bractColor;
            bract.a = elementSeed;
            for (int petal = 0; petal < 4; petal++)
            {
                float angle = petal * Mathf.PI * 0.5f;
                Vector3 outward = axisA * Mathf.Cos(angle) + axisB * Mathf.Sin(angle);
                Vector3 across = Vector3.Cross(normal, outward).normalized;
                int a = buffer.AddVertex(centre, normal, bract, new Vector2(0.5f, 0f), root);
                int b = buffer.AddVertex(centre + outward * radius + across * radius * 0.48f, normal, bract, new Vector2(0f, 0.65f), root);
                int c = buffer.AddVertex(centre + outward * radius * 1.65f, normal, bract, new Vector2(0.5f, 1f), root);
                int d = buffer.AddVertex(centre + outward * radius - across * radius * 0.48f, normal, bract, new Vector2(1f, 0.65f), root);
                AddOrientedQuad(buffer, a, b, c, d, normal);
            }

            Color spike = p.spikeColor;
            spike.a = elementSeed;
            Vector3 spikeTop = centre + normal * radius * 1.15f;
            AddCrossedShootStem(
                buffer,
                centre,
                spikeTop,
                axisA,
                axisB,
                radius * 0.32f,
                normal,
                spike,
                root);
        }

        private static Color SelectVineLeafColor(
            SurfaceVineParams p,
            float age,
            ref FoliageRandom random)
        {
            Color colour;
            float choice = random.Value01();
            if (choice < p.dryAmount)
            {
                colour = p.dryColor;
            }
            else if (choice < p.dryAmount + p.autumnAmount)
            {
                colour = p.autumnColor;
            }
            else
            {
                colour = Color.Lerp(p.youngColor, p.matureColor, Mathf.Clamp01(age));
            }
            return JitterColour(colour, ref random, p.colourJitter);
        }

        private static int LeavesAtNode(SurfaceLeafArrangement arrangement)
        {
            switch (arrangement)
            {
                case SurfaceLeafArrangement.Opposite: return 2;
                case SurfaceLeafArrangement.Whorled: return 3;
                default: return 1;
            }
        }

        private static float ArrangementAngle(
            SurfaceLeafArrangement arrangement,
            int nodeIndex,
            int leafIndex,
            int leavesAtNode,
            ref FoliageRandom random)
        {
            switch (arrangement)
            {
                case SurfaceLeafArrangement.Opposite:
                case SurfaceLeafArrangement.Whorled:
                    return nodeIndex * 137.50776f
                        + leafIndex * (360f / leavesAtNode);
                case SurfaceLeafArrangement.Random:
                    return random.Range(0f, 360f);
                case SurfaceLeafArrangement.Alternate:
                default:
                    return (nodeIndex & 1) == 0 ? 0f : 180f;
            }
        }

        private static float Hash01(int a, int b, int c)
        {
            unchecked
            {
                uint value = (uint)a * 0x9E3779B9u;
                value ^= (uint)b * 0x85EBCA6Bu;
                value ^= (uint)c * 0xC2B2AE35u;
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                return (value & 0xFFFFFFu) / 16777216f;
            }
        }

        private static Color JitterColour(
            Color colour,
            ref FoliageRandom random,
            float amount)
        {
            float scale = Mathf.Max(0f, 1f + random.Signed() * amount);
            return new Color(
                Mathf.Clamp01(colour.r * scale),
                Mathf.Clamp01(colour.g * scale),
                Mathf.Clamp01(colour.b * scale),
                colour.a);
        }

        private static float MaximumDistance(IReadOnlyList<SurfaceGrowthNode> nodes)
        {
            float maximum = 0f;
            for (int i = 0; i < nodes.Count; i++)
            {
                maximum = Mathf.Max(maximum, nodes[i].distanceFromRoot);
            }
            return maximum;
        }

        private static Vector3 RandomTangent(
            Vector3 normal,
            ref FoliageRandom random)
        {
            Vector3 axisA = AnyTangent(normal);
            Vector3 axisB = Vector3.Cross(normal.normalized, axisA).normalized;
            float angle = random.Range(0f, Mathf.PI * 2f);
            return (axisA * Mathf.Cos(angle) + axisB * Mathf.Sin(angle)).normalized;
        }

        private static Vector3 AnyTangent(Vector3 normal)
        {
            normal = normal.normalized;
            Vector3 axis = Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right;
            Vector3 tangent = Vector3.Cross(axis, normal).normalized;
            return tangent.sqrMagnitude > 1e-6f ? tangent : Vector3.forward;
        }

        private static Vector3 ProjectOnPlane(Vector3 value, Vector3 normal)
        {
            float denominator = normal.sqrMagnitude;
            return denominator > 1e-8f
                ? value - normal * (Vector3.Dot(value, normal) / denominator)
                : value;
        }

        private static void AddOrientedTriangle(
            FoliageMeshBuffer buffer,
            int a,
            int b,
            int c,
            Vector3 expectedNormal)
        {
            Vector3 actual = Vector3.Cross(
                buffer.Positions[b] - buffer.Positions[a],
                buffer.Positions[c] - buffer.Positions[a]);
            if (Vector3.Dot(actual, expectedNormal) >= 0f)
            {
                buffer.AddTriangle(a, b, c);
            }
            else
            {
                buffer.AddTriangle(a, c, b);
            }
        }

        private static void AddOrientedQuad(
            FoliageMeshBuffer buffer,
            int a,
            int b,
            int c,
            int d,
            Vector3 expectedNormal)
        {
            AddOrientedTriangle(buffer, a, b, c, expectedNormal);
            AddOrientedTriangle(buffer, a, c, d, expectedNormal);
        }
    }
}
