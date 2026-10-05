using System.Collections.Generic;
using UnityEngine;

namespace SabaProps.Foliage.Editors
{
    public sealed class FoliageSurfaceScatterSettings
    {
        public FoliageAreaShape Shape = FoliageAreaShape.Rectangle;
        public Vector2 Size = new Vector2(20f, 20f);
        public float Radius = 10f;
        public float Density = 1f;
        public int Seed = 12345;
        public int MaxInstances = 40000;

        public LayerMask GroundLayers = ~0;
        public float RaycastHeight = 30f;
        public float RaycastDistance = 80f;
        public bool RequireGroundHit = true;
        public Vector2 AltitudeLimits = new Vector2(-10000f, 10000f);
        public float GroundOffset = -0.01f;

        public LayerMask ExclusionLayers = 0;
        public float ExclusionRadius = 0.3f;

        public Texture2D DensityMask;
        public float DensityMaskThreshold = 0.05f;
        public bool InvertDensityMask;
        public Object LogContext;

        public Vector2 LocalExtents =>
            FoliageAreaUtility.LocalExtents(Shape, Size, Radius);

        public float AreaSquareMeters =>
            FoliageAreaUtility.AreaSquareMeters(Shape, Size, Radius);

        public bool ContainsLocalPoint(float x, float z) =>
            FoliageAreaUtility.ContainsLocalPoint(Shape, Size, Radius, x, z);

        public Vector2 LocalPointToMaskUv(float x, float z) =>
            FoliageAreaUtility.LocalPointToMaskUv(Shape, Size, Radius, x, z);

        public static FoliageSurfaceScatterSettings From(FoliageField field)
        {
            return new FoliageSurfaceScatterSettings
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
        }
    }

    public delegate TSelection FoliageCandidateSelector<TSelection>(
        ref FoliageRandom random);

    public delegate bool FoliageSurfaceFilter<TSelection>(
        TSelection selection, Vector3 position, Vector3 groundNormal);

    public delegate bool FoliagePlacementFactory<TSelection, TResult>(
        TSelection selection, Vector3 position, Vector3 groundNormal,
        ref FoliageRandom random, out TResult result);

    /// <summary>
    /// Shared deterministic area sampling and ground filtering. Callers own the
    /// prop-specific selection, slope policy, spacing and final rotation.
    /// Keeping those callbacks inside the same random stream preserves a
    /// field's exact placements when a generator is refactored.
    /// </summary>
    public static class FoliageSurfaceScatterer
    {
        public static List<TResult> Scatter<TSelection, TResult>(
            Transform fieldTransform,
            FoliageSurfaceScatterSettings settings,
            FoliageCandidateSelector<TSelection> selectCandidate,
            FoliageSurfaceFilter<TSelection> filterSurface,
            FoliagePlacementFactory<TSelection, TResult> createPlacement,
            out string error)
        {
            error = null;
            var results = new List<TResult>();

            if (fieldTransform == null || settings == null ||
                selectCandidate == null || createPlacement == null)
            {
                error = "Scatter の設定または callback が不足しています。";
                return results;
            }

            float area = settings.AreaSquareMeters;
            int targetCount = Mathf.Clamp(
                Mathf.RoundToInt(area * settings.Density), 0, settings.MaxInstances);
            if (targetCount <= 0)
            {
                error = "Density が低すぎて 1 個体も配置されません。";
                return results;
            }

            Color[] maskPixels = TryReadMask(
                settings, out int maskWidth, out int maskHeight);

            float step = Mathf.Sqrt(area / targetCount);
            Vector2 extents = settings.LocalExtents;
            int cellsX = Mathf.Max(1, Mathf.CeilToInt(extents.x * 2f / step));
            int cellsZ = Mathf.Max(1, Mathf.CeilToInt(extents.y * 2f / step));

            Physics.SyncTransforms();
            Matrix4x4 localToWorld = fieldTransform.localToWorldMatrix;
            var random = new FoliageRandom(settings.Seed);

            for (int cz = 0; cz < cellsZ; cz++)
            {
                for (int cx = 0; cx < cellsX; cx++)
                {
                    if (results.Count >= settings.MaxInstances)
                    {
                        error = $"maxInstances ({settings.MaxInstances}) に到達したため打ち切りました。";
                        return results;
                    }

                    float localX = -extents.x + (cx + random.Value01()) * step;
                    float localZ = -extents.y + (cz + random.Value01()) * step;
                    if (!settings.ContainsLocalPoint(localX, localZ))
                    {
                        continue;
                    }

                    if (maskPixels != null && !PassesMask(
                        settings, maskPixels, maskWidth, maskHeight,
                        localX, localZ, ref random))
                    {
                        continue;
                    }

                    // Selection intentionally occurs before raycast. Existing
                    // Foliage fields consumed this random value even when a
                    // later ground test rejected the candidate.
                    TSelection selection = selectCandidate(ref random);
                    Vector3 rayOrigin = localToWorld.MultiplyPoint3x4(
                        new Vector3(localX, settings.RaycastHeight, localZ));

                    Vector3 position;
                    Vector3 groundNormal;
                    if (Physics.Raycast(
                        rayOrigin, Vector3.down, out RaycastHit hit,
                        settings.RaycastDistance, settings.GroundLayers,
                        QueryTriggerInteraction.Ignore))
                    {
                        position = hit.point;
                        groundNormal = hit.normal;
                    }
                    else if (settings.RequireGroundHit)
                    {
                        continue;
                    }
                    else
                    {
                        position = localToWorld.MultiplyPoint3x4(
                            new Vector3(localX, 0f, localZ));
                        groundNormal = fieldTransform.up;
                    }

                    if (position.y < settings.AltitudeLimits.x ||
                        position.y > settings.AltitudeLimits.y)
                    {
                        continue;
                    }

                    if (filterSurface != null &&
                        !filterSurface(selection, position, groundNormal))
                    {
                        continue;
                    }

                    position += Vector3.up * settings.GroundOffset;
                    if (settings.ExclusionLayers.value != 0 &&
                        settings.ExclusionRadius > 0f &&
                        Physics.CheckSphere(
                            position, settings.ExclusionRadius,
                            settings.ExclusionLayers,
                            QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }

                    if (createPlacement(
                        selection, position, groundNormal,
                        ref random, out TResult result))
                    {
                        results.Add(result);
                    }
                }
            }

            if (results.Count == 0)
            {
                error = settings.RequireGroundHit
                    ? "1 個体も配置されませんでした。Ground Layers にコライダーがあるか、Raycast Height が足りているか確認してください。"
                    : "1 個体も配置されませんでした。フィルタ条件を緩めてください。";
            }

            return results;
        }

        public static float[] BuildCumulativeWeights(IReadOnlyList<float> weights)
        {
            var cumulative = new float[weights.Count];
            float total = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                total += Mathf.Max(0f, weights[i]);
                cumulative[i] = total;
            }
            return cumulative;
        }

        public static int PickWeightedIndex(
            float[] cumulativeWeights, ref FoliageRandom random)
        {
            float total = cumulativeWeights[cumulativeWeights.Length - 1];
            float pick = random.Value01() * total;
            for (int i = 0; i < cumulativeWeights.Length; i++)
            {
                if (pick <= cumulativeWeights[i])
                {
                    return i;
                }
            }
            return cumulativeWeights.Length - 1;
        }

        public static Quaternion BuildGroundRotation(
            Vector3 groundNormal, float alignToGroundNormal,
            float maxTilt, float yawDegrees, ref FoliageRandom random)
        {
            Vector3 up = Vector3.Slerp(
                Vector3.up, groundNormal, alignToGroundNormal).normalized;
            Quaternion align = Quaternion.FromToRotation(Vector3.up, up);
            Quaternion yaw = Quaternion.AngleAxis(yawDegrees, Vector3.up);

            Quaternion tilt = Quaternion.identity;
            if (maxTilt > 0f)
            {
                float tiltAngle = random.Range(0f, maxTilt);
                float axisAngle = random.Range(0f, Mathf.PI * 2f);
                var axis = new Vector3(
                    Mathf.Cos(axisAngle), 0f, Mathf.Sin(axisAngle));
                tilt = Quaternion.AngleAxis(tiltAngle, axis);
            }

            return align * yaw * tilt;
        }

        private static Color[] TryReadMask(
            FoliageSurfaceScatterSettings settings,
            out int width, out int height)
        {
            width = 0;
            height = 0;
            if (settings.DensityMask == null)
            {
                return null;
            }

            if (!settings.DensityMask.isReadable)
            {
                Debug.LogWarning(
                    $"[SabaProps Foliage] Density Mask '{settings.DensityMask.name}' は Read/Write Enabled が OFF のため無視されます。",
                    settings.LogContext);
                return null;
            }

            width = settings.DensityMask.width;
            height = settings.DensityMask.height;
            return settings.DensityMask.GetPixels();
        }

        private static bool PassesMask(
            FoliageSurfaceScatterSettings settings,
            Color[] pixels, int width, int height,
            float localX, float localZ, ref FoliageRandom random)
        {
            Vector2 uv = settings.LocalPointToMaskUv(localX, localZ);
            int px = Mathf.Clamp(Mathf.FloorToInt(uv.x * width), 0, width - 1);
            int py = Mathf.Clamp(Mathf.FloorToInt(uv.y * height), 0, height - 1);

            float value = pixels[py * width + px].grayscale;
            if (settings.InvertDensityMask)
            {
                value = 1f - value;
            }
            if (value < settings.DensityMaskThreshold)
            {
                return false;
            }
            return random.Value01() <= value;
        }
    }

    /// <summary>Uniform-grid minimum-spacing rejection shared by prop types.</summary>
    public sealed class FoliageSpacingGrid
    {
        private readonly Dictionary<Vector3Int, List<Vector3>> _cells =
            new Dictionary<Vector3Int, List<Vector3>>();
        private readonly float _cellSize;
        private readonly bool _enabled;

        public FoliageSpacingGrid(float spacing)
        {
            _enabled = spacing > 0f;
            _cellSize = Mathf.Max(0.01f, spacing);
        }

        public bool TryPlace(Vector3 position, float minSpacing)
        {
            if (!_enabled || minSpacing <= 0f)
            {
                return true;
            }

            Vector3Int cell = ToCell(position);
            float sqrSpacing = minSpacing * minSpacing;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    var key = new Vector3Int(cell.x + dx, 0, cell.z + dz);
                    if (!_cells.TryGetValue(key, out List<Vector3> bucket))
                    {
                        continue;
                    }
                    foreach (Vector3 existing in bucket)
                    {
                        float deltaX = existing.x - position.x;
                        float deltaZ = existing.z - position.z;
                        if (deltaX * deltaX + deltaZ * deltaZ < sqrSpacing)
                        {
                            return false;
                        }
                    }
                }
            }

            if (!_cells.TryGetValue(cell, out List<Vector3> target))
            {
                target = new List<Vector3>();
                _cells[cell] = target;
            }
            target.Add(position);
            return true;
        }

        private Vector3Int ToCell(Vector3 position)
        {
            return new Vector3Int(
                Mathf.FloorToInt(position.x / _cellSize),
                0,
                Mathf.FloorToInt(position.z / _cellSize));
        }
    }
}
