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
                    int first = Mathf.FloorToInt(parent.distanceFromRoot / interval) + 1;
                    int last = Mathf.FloorToInt(node.distanceFromRoot / interval);
                    for (int leafIndex = first; leafIndex <= last; leafIndex++)
                    {
                        float targetDistance = leafIndex * interval;
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
                        if (((leafIndex + node.branchDepth) & 1) != 0)
                        {
                            side = -side;
                        }

                        float length = random.Range(
                            Mathf.Max(0.005f, morphology.minimumLeafLength),
                            Mathf.Max(
                                morphology.minimumLeafLength,
                                morphology.maximumLeafLength));
                        float width = length * morphology.leafWidthRatio;
                        Vector3 direction = (
                            side
                            + tangent * random.Range(-0.22f, 0.22f)
                            + Vector3.down * morphology.leafDroop).normalized;

                        Color color = SelectVineLeafColor(
                            morphology,
                            targetDistance / Mathf.Max(interval, maximumDistance),
                            ref random);
                        AddLeaf(
                            buffer,
                            morphology.leafShape,
                            attach + normal * 0.002f,
                            direction,
                            normal,
                            length,
                            width,
                            color,
                            morphology.leafStiffness,
                            random.Value01());
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
                pivot);
            int b = buffer.AddVertex(
                parent.position + parentSide * halfRoot,
                parent.normal,
                color,
                new Vector2(1f, 0f),
                pivot);
            int c = buffer.AddVertex(
                node.position + nodeSide * halfTip,
                node.normal,
                color,
                new Vector2(1f, 0f),
                pivot);
            int d = buffer.AddVertex(
                node.position - nodeSide * halfTip,
                node.normal,
                color,
                new Vector2(0f, 0f),
                pivot);
            AddOrientedQuad(buffer, a, b, c, d, parent.normal);
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
            color.a = elementSeed;

            Vector3 centrePosition = attach + forward * (length * 0.46f);
            int centre = buffer.AddVertex(
                centrePosition,
                normal,
                color,
                new Vector2(0.5f, 0.48f),
                pivot);
            var outline = new int[profile.Length];
            for (int i = 0; i < profile.Length; i++)
            {
                Vector2 point = profile[i];
                Vector3 position = attach
                    + side * (point.x * width)
                    + forward * (point.y * length);
                Color vertexColor = Color.Lerp(color, Color.white, point.y * 0.035f);
                vertexColor.a = elementSeed;
                outline[i] = buffer.AddVertex(
                    position,
                    normal,
                    vertexColor,
                    new Vector2(point.x + 0.5f, point.y),
                    pivot);
            }

            for (int i = 0; i < outline.Length; i++)
            {
                int next = (i + 1) % outline.Length;
                AddOrientedTriangle(buffer, centre, outline[i], outline[next], normal);
            }
        }

        private static Vector2[] LeafProfile(SurfaceLeafShape shape)
        {
            switch (shape)
            {
                case SurfaceLeafShape.Lobed:
                    return new[]
                    {
                        new Vector2(-0.10f, 0.02f),
                        new Vector2(-0.44f, 0.20f),
                        new Vector2(-0.22f, 0.34f),
                        new Vector2(-0.50f, 0.56f),
                        new Vector2(-0.20f, 0.64f),
                        new Vector2(-0.25f, 0.86f),
                        new Vector2(0f, 1f),
                        new Vector2(0.25f, 0.86f),
                        new Vector2(0.20f, 0.64f),
                        new Vector2(0.50f, 0.56f),
                        new Vector2(0.22f, 0.34f),
                        new Vector2(0.44f, 0.20f),
                        new Vector2(0.10f, 0.02f),
                        new Vector2(0f, 0.12f),
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
