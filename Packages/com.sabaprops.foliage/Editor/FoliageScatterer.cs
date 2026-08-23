using System.Collections.Generic;
using UnityEngine;

namespace SabaProps.Foliage.Editors
{
    /// <summary>A single placement produced by the scatterer, in world space.</summary>
    public struct FoliageInstance
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float Scale;
        public int SpeciesIndex;

        public Matrix4x4 ToMatrix()
        {
            return Matrix4x4.TRS(Position, Rotation, Vector3.one * Scale);
        }
    }

    /// <summary>
    /// Turns a <see cref="FoliageField"/> into a list of placements.
    /// <para>
    /// Uses a jittered grid rather than true Poisson-disc sampling: it is O(n),
    /// deterministic, and for foliage the residual regularity is invisible once
    /// per-instance rotation and scale are applied.
    /// </para>
    /// </summary>
    public static class FoliageScatterer
    {
        /// <summary>
        /// Scatters instances over the field.
        /// </summary>
        /// <param name="field">Field describing the area and filters.</param>
        /// <param name="error">Human readable reason when the result is empty.</param>
        public static List<FoliageInstance> Scatter(FoliageField field, out string error)
        {
            error = null;
            var results = new List<FoliageInstance>();

            if (field == null)
            {
                error = "FoliageField が見つかりません。";
                return results;
            }

            List<FoliageSpecies> validSpecies = CollectValidSpecies(field, out error);
            if (validSpecies.Count == 0)
            {
                return results;
            }

            float[] cumulativeWeights = BuildCumulativeWeights(field, validSpecies);
            if (cumulativeWeights[cumulativeWeights.Length - 1] <= 0f)
            {
                error = "すべての Species の Placement Weight が 0 です。";
                return results;
            }

            float area = field.AreaSquareMeters;
            int targetCount = Mathf.Clamp(Mathf.RoundToInt(area * field.density), 0, field.maxInstances);
            if (targetCount <= 0)
            {
                error = "Density が低すぎて 1 個体も配置されません。";
                return results;
            }

            Color[] maskPixels = TryReadMask(field, out int maskWidth, out int maskHeight);

            using (var baked = new BakedSkinnedGround(field))
            {
                return ScatterOver(
                    field, validSpecies, cumulativeWeights, targetCount,
                    maskPixels, maskWidth, maskHeight, results, ref error);
            }
        }

        private static List<FoliageInstance> ScatterOver(
            FoliageField field, List<FoliageSpecies> validSpecies, float[] cumulativeWeights, int targetCount,
            Color[] maskPixels, int maskWidth, int maskHeight,
            List<FoliageInstance> results, ref string error)
        {
            float area = field.AreaSquareMeters;
            float step = Mathf.Sqrt(area / targetCount);
            Vector2 extents = field.LocalExtents;
            int cellsX = Mathf.Max(1, Mathf.CeilToInt(extents.x * 2f / step));
            int cellsZ = Mathf.Max(1, Mathf.CeilToInt(extents.y * 2f / step));

            // Colliders moved this frame are not reflected in the physics scene
            // until it ticks, which never happens in edit mode.
            Physics.SyncTransforms();

            Transform fieldTransform = field.transform;
            Matrix4x4 localToWorld = fieldTransform.localToWorldMatrix;

            var rng = new FoliageRandom(field.seed);
            float? sunBearing = SunBearing();
            // One grid per species. Min Spacing means "keep this species apart
            // from itself": checking it against every other species as well let
            // dense ground cover crowd out anything sparse, so a sunflower mixed
            // into grass at a low weight would place almost nowhere.
            var spacingGrids = new SpacingGrid[validSpecies.Count];
            for (int i = 0; i < validSpecies.Count; i++)
            {
                spacingGrids[i] = new SpacingGrid(validSpecies[i].minSpacing);
            }

            for (int cz = 0; cz < cellsZ; cz++)
            {
                for (int cx = 0; cx < cellsX; cx++)
                {
                    if (results.Count >= field.maxInstances)
                    {
                        error = $"maxInstances ({field.maxInstances}) に到達したため打ち切りました。";
                        return results;
                    }

                    float localX = -extents.x + (cx + rng.Value01()) * step;
                    float localZ = -extents.y + (cz + rng.Value01()) * step;

                    if (!field.ContainsLocalPoint(localX, localZ))
                    {
                        continue;
                    }

                    if (maskPixels != null &&
                        !PassesMask(field, maskPixels, maskWidth, maskHeight, localX, localZ, ref rng))
                    {
                        continue;
                    }

                    int speciesIndex = PickSpecies(cumulativeWeights, ref rng);
                    FoliageSpecies species = validSpecies[speciesIndex];

                    Vector3 rayOrigin = localToWorld.MultiplyPoint3x4(new Vector3(localX, field.raycastHeight, localZ));

                    Vector3 position;
                    Vector3 groundNormal;

                    if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                            field.raycastDistance, field.groundLayers, QueryTriggerInteraction.Ignore))
                    {
                        position = hit.point;
                        groundNormal = hit.normal;
                    }
                    else if (field.requireGroundHit)
                    {
                        continue;
                    }
                    else
                    {
                        position = localToWorld.MultiplyPoint3x4(new Vector3(localX, 0f, localZ));
                        groundNormal = fieldTransform.up;
                    }

                    if (position.y < field.altitudeLimits.x || position.y > field.altitudeLimits.y)
                    {
                        continue;
                    }

                    float slope = Vector3.Angle(groundNormal, Vector3.up);
                    Vector2 slopeLimits = species.SafeSlopeLimits;
                    if (slope < slopeLimits.x || slope > slopeLimits.y)
                    {
                        continue;
                    }

                    position += Vector3.up * field.groundOffset;

                    if (field.exclusionLayers.value != 0 && field.exclusionRadius > 0f &&
                        Physics.CheckSphere(position, field.exclusionRadius, field.exclusionLayers,
                            QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }

                    if (!spacingGrids[speciesIndex].TryPlace(position, species.minSpacing))
                    {
                        continue;
                    }

                    Vector2 scaleRange = species.SafeScaleRange;

                    results.Add(new FoliageInstance
                    {
                        Position = position,
                        Rotation = BuildRotation(species, groundNormal, sunBearing, ref rng),
                        Scale = rng.Range(scaleRange.x, scaleRange.y),
                        SpeciesIndex = speciesIndex,
                    });
                }
            }

            if (results.Count == 0 && error == null)
            {
                error = field.requireGroundHit
                    ? "1 個体も配置されませんでした。Ground Layers にコライダーがあるか、Raycast Height が足りているか確認してください。"
                    : "1 個体も配置されませんでした。フィルタ条件を緩めてください。";
            }

            return results;
        }

        /// <summary>
        /// Species referenced by the field, minus entries that cannot produce a
        /// renderer. Returns the same order used by <see cref="FoliageInstance.SpeciesIndex"/>.
        /// </summary>
        public static List<FoliageSpecies> CollectValidSpecies(FoliageField field, out string error)
        {
            error = null;
            var valid = new List<FoliageSpecies>();

            if (field == null || field.species == null)
            {
                error = "Species が設定されていません。";
                return valid;
            }

            foreach (FoliageSpecies species in field.species)
            {
                if (species == null)
                {
                    continue;
                }

                if (species.material == null)
                {
                    error = $"Species '{species.name}' に Material が設定されていません。";
                    continue;
                }

                valid.Add(species);
            }

            if (valid.Count == 0 && error == null)
            {
                error = "有効な Species が 1 つもありません。";
            }

            return valid;
        }

        private static float[] BuildCumulativeWeights(FoliageField field, List<FoliageSpecies> species)
        {
            var cumulative = new float[species.Count];
            float total = 0f;

            for (int i = 0; i < species.Count; i++)
            {
                // valid species keep the field's order but may skip entries, so
                // the weight has to be looked up by the species' own slot.
                total += Mathf.Max(0f, field.PlacementWeightAt(field.species.IndexOf(species[i])));
                cumulative[i] = total;
            }

            return cumulative;
        }

        private static int PickSpecies(float[] cumulativeWeights, ref FoliageRandom rng)
        {
            float total = cumulativeWeights[cumulativeWeights.Length - 1];
            float pick = rng.Value01() * total;

            for (int i = 0; i < cumulativeWeights.Length; i++)
            {
                if (pick <= cumulativeWeights[i])
                {
                    return i;
                }
            }

            return cumulativeWeights.Length - 1;
        }

        /// <summary>
        /// Temporary colliders for skinned ground.
        /// <para>
        /// A SkinnedMeshRenderer has no collider that follows the skin, so there
        /// is nothing for the scatterer to raycast against. Baking the current
        /// pose into a throwaway MeshCollider means the existing ground path
        /// works unchanged, and the geometry matches what the player sees rather
        /// than a proxy someone has to keep in sync by hand.
        /// </para>
        /// <para>
        /// The colliders are hidden and never saved, and go away with the build.
        /// </para>
        /// </summary>
        private sealed class BakedSkinnedGround : System.IDisposable
        {
            private readonly List<GameObject> _objects = new List<GameObject>();
            private readonly List<Mesh> _meshes = new List<Mesh>();

            public BakedSkinnedGround(FoliageField field)
            {
                if (field.skinnedGround == null)
                {
                    return;
                }

                foreach (SkinnedMeshRenderer skinned in field.skinnedGround)
                {
                    if (skinned == null || skinned.sharedMesh == null)
                    {
                        continue;
                    }

                    var mesh = new Mesh { name = $"{skinned.name}_BakedGround" };

                    // useScale: the baked vertices then already carry the
                    // renderer's lossy scale, so the proxy needs only its
                    // position and rotation.
                    skinned.BakeMesh(mesh, true);

                    var go = new GameObject($"__SabaFoliageGround_{skinned.name}")
                    {
                        hideFlags = HideFlags.HideAndDontSave,
                        layer = skinned.gameObject.layer,
                    };

                    go.transform.SetPositionAndRotation(
                        skinned.transform.position, skinned.transform.rotation);

                    go.AddComponent<MeshCollider>().sharedMesh = mesh;

                    _objects.Add(go);
                    _meshes.Add(mesh);
                }

                if (_objects.Count > 0)
                {
                    Physics.SyncTransforms();
                }
            }

            public void Dispose()
            {
                foreach (GameObject go in _objects)
                {
                    Object.DestroyImmediate(go);
                }

                foreach (Mesh mesh in _meshes)
                {
                    Object.DestroyImmediate(mesh);
                }

                _objects.Clear();
                _meshes.Clear();
            }
        }

        /// <summary>
        /// Compass bearing of the sun, in degrees, for species that face it.
        /// <para>
        /// Taken from the scene's sun so the flowers agree with the shadows.
        /// Returns null when the scene has no directional light, which leaves
        /// those species on random yaw rather than pointing them all at an
        /// arbitrary default.
        /// </para>
        /// </summary>
        public static float? SunBearing()
        {
            Light sun = RenderSettings.sun;

            if (sun == null || !sun.isActiveAndEnabled || sun.type != LightType.Directional)
            {
                sun = null;
                float brightest = 0f;

                foreach (Light light in Object.FindObjectsOfType<Light>())
                {
                    if (light.type != LightType.Directional || !light.isActiveAndEnabled)
                    {
                        continue;
                    }

                    if (sun == null || light.intensity > brightest)
                    {
                        sun = light;
                        brightest = light.intensity;
                    }
                }
            }

            if (sun == null)
            {
                return null;
            }

            // The light points away from the sun, so the direction to look in is
            // the reverse of its forward, flattened onto the ground.
            Vector3 toSun = -sun.transform.forward;
            var flat = new Vector2(toSun.x, toSun.z);

            if (flat.sqrMagnitude < 1e-6f)
            {
                // Straight overhead: there is no bearing to face.
                return null;
            }

            return Mathf.Atan2(flat.x, flat.y) * Mathf.Rad2Deg;
        }

        private static Quaternion BuildRotation(
            FoliageSpecies species, Vector3 groundNormal, float? sunBearing, ref FoliageRandom rng)
        {
            Vector3 up = Vector3.Slerp(Vector3.up, groundNormal, species.alignToGroundNormal).normalized;
            Quaternion align = Quaternion.FromToRotation(Vector3.up, up);

            // The stock meshes lean and face along object-space +Z, so aiming
            // that axis at the sun aims the flower.
            Quaternion yaw = species.faceSun && sunBearing.HasValue
                ? Quaternion.AngleAxis(
                    sunBearing.Value + rng.Range(-species.faceSunJitter, species.faceSunJitter), Vector3.up)
                : Quaternion.AngleAxis(rng.Range(0f, 360f), Vector3.up);

            Quaternion tilt = Quaternion.identity;
            if (species.maxTilt > 0f)
            {
                float tiltAngle = rng.Range(0f, species.maxTilt);
                float axisAngle = rng.Range(0f, Mathf.PI * 2f);
                var axis = new Vector3(Mathf.Cos(axisAngle), 0f, Mathf.Sin(axisAngle));
                tilt = Quaternion.AngleAxis(tiltAngle, axis);
            }

            return align * yaw * tilt;
        }

        private static Color[] TryReadMask(FoliageField field, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (field.densityMask == null)
            {
                return null;
            }

            if (!field.densityMask.isReadable)
            {
                Debug.LogWarning(
                    $"[SabaProps Foliage] Density Mask '{field.densityMask.name}' は Read/Write Enabled が OFF のため無視されます。",
                    field);
                return null;
            }

            width = field.densityMask.width;
            height = field.densityMask.height;
            return field.densityMask.GetPixels();
        }

        private static bool PassesMask(
            FoliageField field, Color[] pixels, int width, int height,
            float localX, float localZ, ref FoliageRandom rng)
        {
            Vector2 uv = field.LocalPointToMaskUv(localX, localZ);
            int px = Mathf.Clamp(Mathf.FloorToInt(uv.x * width), 0, width - 1);
            int py = Mathf.Clamp(Mathf.FloorToInt(uv.y * height), 0, height - 1);

            float value = pixels[py * width + px].grayscale;
            if (field.invertDensityMask)
            {
                value = 1f - value;
            }

            if (value < field.densityMaskThreshold)
            {
                return false;
            }

            // Above the threshold the mask thins the scatter out probabilistically,
            // so painted gradients produce a gradual falloff rather than a hard edge.
            return rng.Value01() <= value;
        }

        /// <summary>
        /// Uniform-grid neighbour lookup for minimum-spacing rejection. Cheaper
        /// and far simpler than a k-d tree at the densities involved here.
        /// </summary>
        private sealed class SpacingGrid
        {
            private readonly Dictionary<Vector3Int, List<Vector3>> _cells = new Dictionary<Vector3Int, List<Vector3>>();
            private readonly float _cellSize;
            private readonly bool _enabled;

            public SpacingGrid(float spacing)
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

                // The cell size is this species' own spacing, so a single ring
                // of neighbours is guaranteed to cover the search radius.
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
                            float dxx = existing.x - position.x;
                            float dzz = existing.z - position.z;
                            if (dxx * dxx + dzz * dzz < sqrSpacing)
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
}
