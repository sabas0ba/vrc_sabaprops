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

            float? sunBearing = SunBearing();
            var spacingGrids = new FoliageSpacingGrid[validSpecies.Count];
            for (int i = 0; i < validSpecies.Count; i++)
            {
                spacingGrids[i] = new FoliageSpacingGrid(validSpecies[i].minSpacing);
            }

            FoliageCandidateSelector<int> selectCandidate =
                delegate(ref FoliageRandom random)
                {
                    return PickSpecies(cumulativeWeights, ref random);
                };

            FoliageSurfaceFilter<int> filterSurface =
                delegate(int speciesIndex, Vector3 position, Vector3 groundNormal)
                {
                    Vector2 limits = validSpecies[speciesIndex].SafeSlopeLimits;
                    float slope = Vector3.Angle(groundNormal, Vector3.up);
                    return slope >= limits.x && slope <= limits.y;
                };

            FoliagePlacementFactory<int, FoliageInstance> createPlacement =
                delegate(
                    int speciesIndex, Vector3 position, Vector3 groundNormal,
                    ref FoliageRandom random, out FoliageInstance instance)
                {
                    FoliageSpecies species = validSpecies[speciesIndex];
                    instance = default;
                    if (!spacingGrids[speciesIndex].TryPlace(position, species.minSpacing))
                    {
                        return false;
                    }

                    Vector2 scaleRange = species.SafeScaleRange;
                    Quaternion rotation = BuildRotation(
                        species, groundNormal, sunBearing, ref random);
                    float scale = random.Range(scaleRange.x, scaleRange.y);
                    instance = new FoliageInstance
                    {
                        Position = position,
                        Rotation = rotation,
                        Scale = scale,
                        SpeciesIndex = speciesIndex,
                    };
                    return true;
                };

            using (var baked = new BakedSkinnedGround(field))
            {
                return FoliageSurfaceScatterer.Scatter(
                    field.transform,
                    FoliageSurfaceScatterSettings.From(field),
                    selectCandidate,
                    filterSurface,
                    createPlacement,
                    out error);
            }
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
            // The stock meshes lean and face along object-space +Z, so aiming
            // that axis at the sun aims the flower.
            float yaw = species.faceSun && sunBearing.HasValue
                ? sunBearing.Value + rng.Range(-species.faceSunJitter, species.faceSunJitter)
                : rng.Range(0f, 360f);

            return FoliageSurfaceScatterer.BuildGroundRotation(
                groundNormal,
                species.alignToGroundNormal,
                species.maxTilt,
                yaw,
                ref rng);
        }
    }
}
