using System;
using System.Collections.Generic;
using UnityEngine;

namespace SabaProps.Foliage.Editors
{
    /// <summary>A point returned by a surface projection callback.</summary>
    public struct SurfacePoint
    {
        public Vector3 position;
        public Vector3 normal;

        public SurfacePoint(Vector3 position, Vector3 normal)
        {
            this.position = position;
            this.normal = normal;
        }
    }

    /// <summary>
    /// Projects a local-space candidate onto a surface. This callback boundary
    /// keeps graph generation deterministic and independently testable.
    /// </summary>
    public delegate bool SurfaceProjector(
        Vector3 candidate,
        Vector3 normalHint,
        float maximumDistance,
        out SurfacePoint point);

    /// <summary>
    /// Generates a branching path graph. Collider access is supplied by the
    /// caller, so the path algorithm itself does not depend on Physics globals.
    /// </summary>
    public static class SurfaceGrowthGraphBuilder
    {
        public static SurfaceGrowthGraph Build(
            SurfaceGrowthSettings settings,
            IReadOnlyList<Vector3> guidePoints,
            SurfaceProjector projector)
        {
            settings = settings ?? new SurfaceGrowthSettings();
            var graph = new SurfaceGrowthGraph();
            if (projector == null || settings.nodeBudget < 1)
            {
                return graph;
            }

            var random = new FoliageRandom(settings.seed);
            int primaryPaths = Mathf.Max(
                1,
                Mathf.RoundToInt(
                    Mathf.Max(1, settings.pathCount)
                    * Mathf.Clamp01(settings.coverage)));

            for (int path = 0;
                 path < primaryPaths && graph.Nodes.Count < settings.nodeBudget;
                 path++)
            {
                if (settings.mode == SurfaceGrowthMode.SurfaceCrawl)
                {
                    BuildCrawlPath(graph, settings, guidePoints, path, ref random, projector);
                }
                else
                {
                    BuildSplinePath(graph, settings, guidePoints, path, ref random, projector);
                }
            }

            int primaryNodeCount = graph.Nodes.Count;
            for (int i = 1;
                 i < primaryNodeCount && graph.Nodes.Count < settings.nodeBudget;
                 i++)
            {
                SurfaceGrowthNode node = graph.Nodes[i];
                if (node.parentIndex < 0 || node.branchDepth != 0)
                {
                    continue;
                }

                float probability = Mathf.Clamp01(
                    settings.branchesPerMetre
                    * settings.stepLength
                    * settings.coverage);
                if (settings.maxBranchDepth > 0 && random.Chance(probability))
                {
                    BuildBranch(
                        graph,
                        settings,
                        i,
                        1,
                        settings.maxPathLength * settings.branchLength,
                        ref random,
                        projector);
                }
            }

            return graph;
        }

        private static void BuildSplinePath(
            SurfaceGrowthGraph graph,
            SurfaceGrowthSettings settings,
            IReadOnlyList<Vector3> guides,
            int pathIndex,
            ref FoliageRandom random,
            SurfaceProjector projector)
        {
            int guideCount = guides != null ? guides.Count : 0;
            Vector3 first = guideCount > 0 ? guides[0] : Vector3.zero;
            Vector3 second = guideCount > 1
                ? guides[1]
                : first + Vector3.up * settings.maxPathLength;

            SurfacePoint firstSurface;
            if (!projector(
                    first,
                    Vector3.forward,
                    settings.projectionDistance,
                    out firstSurface))
            {
                return;
            }

            Vector3 firstDirection = ProjectOnPlane(
                second - first,
                firstSurface.normal).normalized;
            if (firstDirection.sqrMagnitude < 1e-6f)
            {
                firstDirection = AnyTangent(firstSurface.normal);
            }
            Vector3 lateral = Vector3.Cross(
                firstSurface.normal,
                firstDirection).normalized;
            float spread = Mathf.Max(settings.minimumSpacing, settings.rootSpread);
            float rootAngle = pathIndex * 2.39996323f
                + random.Range(-0.35f, 0.35f);
            float rootRadius = spread * Mathf.Sqrt(random.Value01());
            Vector3 offset = lateral * (Mathf.Cos(rootAngle) * rootRadius)
                + firstDirection * (Mathf.Sin(rootAngle) * rootRadius * 0.55f);

            SurfacePoint rootSurface;
            if (!projector(
                    first + offset,
                    firstSurface.normal,
                    settings.projectionDistance,
                    out rootSurface))
            {
                return;
            }

            int parent = AddNode(
                graph,
                rootSurface,
                settings.surfaceOffset,
                -1,
                0,
                0f);
            Vector3 previousPosition = graph.Nodes[parent].position;
            Vector3 previousNormal = rootSurface.normal;
            float distance = 0f;
            float pathLength = settings.maxPathLength * random.Range(
                1f - settings.pathLengthVariance,
                1f + settings.pathLengthVariance);
            int steps = Mathf.Max(
                1,
                Mathf.RoundToInt(
                    pathLength
                    / Mathf.Max(0.01f, settings.stepLength)));

            Vector3 walkDirection = firstDirection;
            float lateralPhase = random.Range(0f, Mathf.PI * 2f);
            float lateralFrequency = random.Range(0.7f, 1.8f);
            float directionBias = random.Signed();

            for (int step = 1;
                 step <= steps && graph.Nodes.Count < settings.nodeBudget;
                 step++)
            {
                float t = step / (float)steps;
                Vector3 guideTarget = SampleGuide(guides, t, pathLength) + offset;

                Vector3 curveTangent = SampleGuide(
                    guides,
                    Mathf.Min(1f, t + 0.01f),
                    pathLength)
                    - SampleGuide(
                        guides,
                        Mathf.Max(0f, t - 0.01f),
                        pathLength);
                Vector3 side = Vector3.Cross(
                    previousNormal,
                    ProjectOnPlane(curveTangent, previousNormal)).normalized;
                if (side.sqrMagnitude < 1e-6f)
                {
                    side = AnyTangent(previousNormal);
                }
                Vector3 tangent = ProjectOnPlane(curveTangent, previousNormal).normalized;
                if (tangent.sqrMagnitude < 1e-6f)
                {
                    tangent = walkDirection;
                }
                float jitter = Mathf.Clamp01(settings.directionJitter);
                float persistence = Mathf.Clamp(
                    settings.directionPersistence,
                    0f,
                    0.98f);
                directionBias = Mathf.Lerp(
                    random.Signed(),
                    directionBias,
                    persistence);
                float wanderAngle = directionBias * jitter * 85f * Mathf.Deg2Rad;
                Vector3 wanderDirection = (
                    tangent * Mathf.Cos(wanderAngle)
                    + side * Mathf.Sin(wanderAngle)).normalized;
                walkDirection = ProjectOnPlane(
                    Vector3.Slerp(
                        walkDirection,
                        wanderDirection,
                        Mathf.Lerp(0.55f, 0.20f, persistence)),
                    previousNormal).normalized;

                Vector3 guideDirection = ProjectOnPlane(
                    guideTarget - previousPosition,
                    previousNormal).normalized;
                if (guideDirection.sqrMagnitude > 1e-6f)
                {
                    walkDirection = Vector3.Slerp(
                        walkDirection,
                        guideDirection,
                        Mathf.Clamp01(settings.guideAttraction) * 0.12f).normalized;
                }
                Vector3 freeCandidate = previousPosition
                    + walkDirection * settings.stepLength
                    + side * Mathf.Sin(t * Mathf.PI * 2f * lateralFrequency + lateralPhase)
                    * jitter * settings.stepLength * 0.25f;
                float guideBlend = Mathf.Clamp01(settings.guideAttraction)
                    * Mathf.Lerp(0.28f, 0.02f, jitter);
                Vector3 candidate = Vector3.Lerp(
                    freeCandidate,
                    guideTarget,
                    guideBlend);

                SurfacePoint projected;
                if (!projector(
                        candidate,
                        previousNormal,
                        settings.projectionDistance,
                        out projected))
                {
                    break;
                }

                // A nearest-point projection can remain on a floor forever
                // when an inclined or vertical surface overlaps it: the free
                // candidate is deliberately kept close to the current plane.
                // Probe the guide itself and accept a different surface only
                // when it is both a substantially better guide fit and close
                // enough to keep the generated stem connected.
                float guideOffPlane = Mathf.Abs(Vector3.Dot(
                    guideTarget - previousPosition,
                    previousNormal));
                if (guideOffPlane > settings.stepLength * 0.35f)
                {
                    SurfacePoint guidedSurface;
                    if (projector(
                            guideTarget,
                            previousNormal,
                            settings.projectionDistance,
                            out guidedSurface))
                    {
                        float normalChange = Vector3.Dot(
                            previousNormal.normalized,
                            guidedSurface.normal.normalized);
                        float currentGuideError = Vector3.Distance(
                            guideTarget,
                            projected.position);
                        float guidedGuideError = Vector3.Distance(
                            guideTarget,
                            guidedSurface.position);
                        float transitionLength = Vector3.Distance(
                            previousPosition,
                            guidedSurface.position);
                        float maximumTransition = Mathf.Min(
                            settings.projectionDistance * 0.75f,
                            settings.stepLength * 3.5f);
                        if (normalChange < 0.96f
                            && guidedGuideError + settings.stepLength * 0.25f
                                < currentGuideError
                            && transitionLength <= maximumTransition)
                        {
                            projected = guidedSurface;
                        }
                    }
                }

                float edgeLength = Vector3.Distance(previousPosition, projected.position);
                if (edgeLength < 1e-5f)
                {
                    continue;
                }
                if (distance + edgeLength > pathLength)
                {
                    break;
                }
                if (!CanPlace(graph, projected.position, parent, settings.minimumSpacing))
                {
                    continue;
                }

                distance += edgeLength;
                parent = AddNode(
                    graph,
                    projected,
                    settings.surfaceOffset,
                    parent,
                    0,
                    distance);
                previousPosition = graph.Nodes[parent].position;
                previousNormal = projected.normal;
            }
        }

        private static void BuildCrawlPath(
            SurfaceGrowthGraph graph,
            SurfaceGrowthSettings settings,
            IReadOnlyList<Vector3> guides,
            int pathIndex,
            ref FoliageRandom random,
            SurfaceProjector projector)
        {
            int guideCount = guides != null ? guides.Count : 0;
            Vector3 seed = guideCount > 0
                ? guides[pathIndex % guideCount]
                : Vector3.zero;
            Vector3 seedDirection = new Vector3(
                random.Signed(), random.Signed(), random.Signed()).normalized;
            seed += seedDirection * settings.rootSpread * Mathf.Sqrt(random.Value01());

            SurfacePoint rootSurface;
            if (!projector(
                    seed,
                    Vector3.up,
                    settings.projectionDistance,
                    out rootSurface))
            {
                return;
            }

            int parent = AddNode(
                graph,
                rootSurface,
                settings.surfaceOffset,
                -1,
                0,
                0f);
            Vector3 direction;
            if (guideCount > 1)
            {
                direction = ProjectOnPlane(
                    guides[(pathIndex + 1) % guideCount] - seed,
                    rootSurface.normal).normalized;
            }
            else
            {
                direction = RandomTangent(rootSurface.normal, ref random);
            }
            if (direction.sqrMagnitude < 1e-6f)
            {
                direction = AnyTangent(rootSurface.normal);
            }

            float pathLength = settings.maxPathLength * random.Range(
                1f - settings.pathLengthVariance,
                1f + settings.pathLengthVariance);
            GrowWalk(
                graph,
                settings,
                parent,
                0,
                direction,
                pathLength,
                ref random,
                projector);
        }

        private static void BuildBranch(
            SurfaceGrowthGraph graph,
            SurfaceGrowthSettings settings,
            int parent,
            int depth,
            float length,
            ref FoliageRandom random,
            SurfaceProjector projector)
        {
            if (depth > settings.maxBranchDepth
                || length < settings.stepLength
                || graph.Nodes.Count >= settings.nodeBudget)
            {
                return;
            }

            SurfaceGrowthNode parentNode = graph.Nodes[parent];
            Vector3 incoming = AnyTangent(parentNode.normal);
            if (parentNode.parentIndex >= 0)
            {
                incoming = (parentNode.position
                    - graph.Nodes[parentNode.parentIndex].position).normalized;
            }
            incoming = ProjectOnPlane(incoming, parentNode.normal).normalized;
            if (incoming.sqrMagnitude < 1e-6f)
            {
                incoming = AnyTangent(parentNode.normal);
            }
            Vector3 side = Vector3.Cross(parentNode.normal, incoming).normalized;
            float angle = Mathf.Clamp(
                settings.branchAngle
                + random.Range(
                    -settings.branchAngleJitter,
                    settings.branchAngleJitter),
                0f,
                89f) * Mathf.Deg2Rad;
            float sideSign = random.Chance(0.5f) ? 1f : -1f;
            Vector3 direction = (
                incoming * Mathf.Cos(angle)
                + side * (Mathf.Sin(angle) * sideSign)).normalized;
            float variedLength = length * random.Range(
                1f - settings.branchLengthVariance,
                1f + settings.branchLengthVariance);

            int before = graph.Nodes.Count;
            GrowWalk(
                graph,
                settings,
                parent,
                depth,
                direction,
                variedLength,
                ref random,
                projector);

            if (depth < settings.maxBranchDepth && graph.Nodes.Count > before)
            {
                int last = graph.Nodes.Count - 1;
                BuildBranch(
                    graph,
                    settings,
                    last,
                    depth + 1,
                    variedLength * settings.branchLength,
                    ref random,
                    projector);
            }
        }

        private static void GrowWalk(
            SurfaceGrowthGraph graph,
            SurfaceGrowthSettings settings,
            int parent,
            int depth,
            Vector3 direction,
            float maximumLength,
            ref FoliageRandom random,
            SurfaceProjector projector)
        {
            float travelled = 0f;
            int attempts = 0;
            float directionBias = random.Signed();
            int maximumAttempts = Mathf.Max(
                8,
                Mathf.CeilToInt(
                    maximumLength
                    / Mathf.Max(0.01f, settings.stepLength)) * 8);
            while (travelled < maximumLength
                   && graph.Nodes.Count < settings.nodeBudget
                   && attempts++ < maximumAttempts)
            {
                SurfaceGrowthNode parentNode = graph.Nodes[parent];
                Vector3 normal = parentNode.normal.normalized;
                Vector3 gravity = ProjectOnPlane(Vector3.down, normal).normalized;
                direction = ProjectOnPlane(direction, normal).normalized;
                if (direction.sqrMagnitude < 1e-6f)
                {
                    direction = AnyTangent(normal);
                }
                float jitter = Mathf.Clamp01(settings.directionJitter);
                float persistence = Mathf.Clamp(
                    settings.directionPersistence,
                    0f,
                    0.98f);
                directionBias = Mathf.Lerp(
                    random.Signed(),
                    directionBias,
                    persistence);
                Vector3 side = Vector3.Cross(normal, direction).normalized;
                if (side.sqrMagnitude < 1e-6f)
                {
                    side = AnyTangent(normal);
                }
                float turnAngle = directionBias * jitter * 14f * Mathf.Deg2Rad;
                direction = (
                    direction * Mathf.Cos(turnAngle)
                    + side * Mathf.Sin(turnAngle)
                    + gravity * settings.gravityBias).normalized;
                if (direction.sqrMagnitude < 1e-6f)
                {
                    direction = AnyTangent(normal);
                }

                float step = Mathf.Min(
                    Mathf.Max(0.01f, settings.stepLength),
                    maximumLength - travelled);
                Vector3 candidate = parentNode.position + direction * step;

                SurfacePoint projected;
                if (!projector(
                        candidate,
                        normal,
                        settings.projectionDistance,
                        out projected))
                {
                    break;
                }

                if (!CanPlace(graph, projected.position, parent, settings.minimumSpacing))
                {
                    direction = RandomTangent(normal, ref random);
                    continue;
                }

                float edgeLength = Vector3.Distance(parentNode.position, projected.position);
                if (edgeLength < 1e-5f)
                {
                    break;
                }

                travelled += edgeLength;
                parent = AddNode(
                    graph,
                    projected,
                    settings.surfaceOffset,
                    parent,
                    depth,
                    parentNode.distanceFromRoot + edgeLength);
            }
        }

        private static int AddNode(
            SurfaceGrowthGraph graph,
            SurfacePoint point,
            float surfaceOffset,
            int parent,
            int depth,
            float distance)
        {
            Vector3 normal = point.normal.normalized;
            graph.Nodes.Add(new SurfaceGrowthNode(
                point.position + normal * surfaceOffset,
                normal,
                parent,
                depth,
                distance));
            return graph.Nodes.Count - 1;
        }

        private static bool CanPlace(
            SurfaceGrowthGraph graph,
            Vector3 position,
            int parent,
            float minimumSpacing)
        {
            if (minimumSpacing <= 0f)
            {
                return true;
            }

            float minimumSquared = minimumSpacing * minimumSpacing;
            int grandParent = parent >= 0 ? graph.Nodes[parent].parentIndex : -1;
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                if (i == parent || i == grandParent)
                {
                    continue;
                }
                if ((graph.Nodes[i].position - position).sqrMagnitude < minimumSquared)
                {
                    return false;
                }
            }
            return true;
        }

        private static Vector3 SampleGuide(
            IReadOnlyList<Vector3> guides,
            float t,
            float fallbackLength)
        {
            int count = guides != null ? guides.Count : 0;
            if (count == 0)
            {
                return Vector3.up * fallbackLength * t;
            }
            if (count == 1)
            {
                return guides[0] + Vector3.up * fallbackLength * t;
            }

            float scaled = Mathf.Clamp01(t) * (count - 1);
            int segment = Mathf.Min(count - 2, Mathf.FloorToInt(scaled));
            float localT = scaled - segment;
            Vector3 p0 = guides[Mathf.Max(0, segment - 1)];
            Vector3 p1 = guides[segment];
            Vector3 p2 = guides[segment + 1];
            Vector3 p3 = guides[Mathf.Min(count - 1, segment + 2)];
            return CatmullRom(p0, p1, p2, p3, localT);
        }

        private static Vector3 CatmullRom(
            Vector3 p0,
            Vector3 p1,
            Vector3 p2,
            Vector3 p3,
            float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                2f * p1
                + (p2 - p0) * t
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static Vector3 RandomTangent(
            Vector3 normal,
            ref FoliageRandom random)
        {
            Vector3 tangent = AnyTangent(normal);
            Vector3 bitangent = Vector3.Cross(normal.normalized, tangent).normalized;
            float angle = random.Range(0f, Mathf.PI * 2f);
            return (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)).normalized;
        }

        private static Vector3 AnyTangent(Vector3 normal)
        {
            normal = normal.normalized;
            Vector3 axis = Mathf.Abs(normal.y) < 0.9f
                ? Vector3.up
                : Vector3.right;
            Vector3 tangent = Vector3.Cross(axis, normal).normalized;
            return tangent.sqrMagnitude > 1e-6f ? tangent : Vector3.forward;
        }

        private static Vector3 ProjectOnPlane(Vector3 value, Vector3 normal)
        {
            float denominator = normal.sqrMagnitude;
            if (denominator < 1e-8f)
            {
                return value;
            }
            return value - normal * (Vector3.Dot(value, normal) / denominator);
        }
    }
}
