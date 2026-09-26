using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SabaProps.Flock.Editors
{
    /// <summary>
    /// Copies one individual's geometry once per individual and writes the
    /// per-individual and per-swarm channels described in
    /// <see cref="FlockShaderContract"/>. No UnityEditor API is used here, so
    /// the offline checks can run it without Unity.
    /// </summary>
    public static class FlockSwarmMeshBuilder
    {
        /// <summary>Clock offset range in seconds derived from the seed.</summary>
        public const float TimeOffsetRange = 600f;

        public static float Speed(FlockSpecies species, FlockSwarmSettings settings)
        {
            return Mathf.Max(species.cruiseSpeed * settings.speedScale, 0f);
        }

        public static float TimeOffset(FlockSwarmSettings settings)
        {
            return FlockRandom.Value(settings.seed, -1, 7) * TimeOffsetRange;
        }

        public static Vector3 Area(FlockSwarmSettings settings)
        {
            Vector3 a = settings.area;
            return new Vector3(Mathf.Max(Mathf.Abs(a.x), 0.01f), Mathf.Max(Mathf.Abs(a.y), 0.01f), Mathf.Max(Mathf.Abs(a.z), 0.01f));
        }

        public static int Count(FlockSwarmSettings settings)
        {
            return Mathf.Clamp(settings.count, 1, 1000);
        }

        /// <summary>The cluster radius the mesh is baked with: the setting, or the automatic value, clamped to fit the area.</summary>
        public static float ClusterRadius(FlockSpecies species, FlockSwarmSettings settings)
        {
            float radius = settings.clusterRadius > 0f
                ? settings.clusterRadius
                : FlockMotion.AutoClusterRadius(settings.pattern, Count(settings), species.Span);
            return FlockMotion.ClampClusterRadius(radius, Area(settings), species.bodyLength);
        }

        public static float BankGain(FlockSpecies species)
        {
            if (species.grounded) return 0f;
            return species.category == FlockCategory.Bird ? 0.06f : 0.02f;
        }

        public static float MaxPitch(FlockSpecies species)
        {
            if (species.grounded) return 0f;
            if (species.category == FlockCategory.Bird)
            {
                return 0.6f;
            }

            return species.fishBody == FlockFishBody.Ray ? 0.3f : 0.35f;
        }

        /// <summary>Beat amplitude as the shader reads it: radians for wings, a fraction of body length otherwise.</summary>
        public static float Amplitude(FlockSpecies species)
        {
            return species.animation == FlockAnimation.Flap
                ? species.beatAmplitude * Mathf.Deg2Rad
                : species.beatAmplitude;
        }

        public static Vector3 IndividualRandom(int seed, int index)
        {
            return new Vector3(
                FlockRandom.Value(seed, index, 0),
                FlockRandom.Value(seed, index, 1),
                FlockRandom.Value(seed, index, 2));
        }

        /// <summary>The motion constants the shader sees for one individual.</summary>
        public static FlockMotionInput MotionInput(FlockSpecies species, FlockSwarmSettings settings, int index)
        {
            return new FlockMotionInput
            {
                Pattern = settings.pattern,
                Speed = Speed(species, settings),
                TimeOffset = TimeOffset(settings),
                ClusterRadius = ClusterRadius(species, settings),
                Area = Area(settings),
                BodyLength = species.bodyLength,
                Count = Count(settings),
                Index = index,
                Random = IndividualRandom(settings.seed, index),
                MaxPitch = MaxPitch(species),
                BankGain = BankGain(species),
            };
        }

        /// <summary>Half extents the renderer bounds must cover, including the body of an individual at the wall.</summary>
        public static Vector3 BoundsExtents(FlockSpecies species, FlockSwarmSettings settings)
        {
            return Area(settings) + Vector3.one * BodyReach(species);
        }

        /// <summary>
        /// Largest distance any vertex of one individual can reach from the
        /// individual's position once the shader has animated and scaled it.
        /// Measured on the High tier, which contains every part of the lower
        /// tiers.
        /// </summary>
        public static float BodyReach(FlockSpecies species)
        {
            FlockMeshBuffer body = FlockBodyBuilder.Build(species, FlockDetail.High);
            float radius = 0f;
            float shoulder = 0f;
            foreach (Vector3 p in body.Positions)
            {
                radius = Mathf.Max(radius, p.magnitude);
            }

            foreach (Vector4 b in body.Body)
            {
                shoulder = Mathf.Max(shoulder, Mathf.Abs(b.w));
            }

            // A wing rotates about its shoulder, which can move a vertex at
            // most two shoulder offsets further out, and the body bobs by 4 %
            // of its length. Undulation and ray waves displace by the
            // amplitude times the body length.
            float wave = species.animation == FlockAnimation.Flap
                ? 0.04f * species.bodyLength
                : Amplitude(species) * species.bodyLength;
            if (species.animation == FlockAnimation.Tentacles) wave *= 1.12f;
            if (species.animation == FlockAnimation.Walk) wave += 0.05f * species.bodyLength;
            return (radius + 2f * shoulder + wave) * (1f + Mathf.Clamp(species.sizeVariance, 0f, 0.5f)) + 0.01f;
        }

        public static Mesh Build(FlockSpecies species, FlockSwarmSettings settings, FlockDetail detail, string name)
        {
            var mesh = new Mesh { name = name };
            Fill(mesh, species, settings, detail);
            return mesh;
        }

        /// <summary>
        /// Replaces the contents of <paramref name="mesh"/>. Rebuilding into the
        /// existing mesh asset keeps its GUID, so scene references survive.
        /// </summary>
        public static void Fill(Mesh mesh, FlockSpecies species, FlockSwarmSettings settings, FlockDetail detail)
        {
            FlockMeshBuffer body = FlockBodyBuilder.Build(species, detail);
            int count = Count(settings);
            int perBody = body.VertexCount;
            int total = perBody * count;

            var positions = new List<Vector3>(total);
            var normals = new List<Vector3>(total);
            var colors = new List<Color>(total);
            var uvBody = new List<Vector4>(total);
            var uvIndividual = new List<Vector4>(total);
            var uvSwarm = new List<Vector4>(total);
            var uvArea = new List<Vector4>(total);
            var uvAnimation = new List<Vector4>(total);
            var uvExtra = new List<Vector4>(total);
            var triangles = new List<int>(body.Triangles.Count * count);

            Vector3 area = Area(settings);
            var swarm = new Vector4(
                (float)settings.pattern,
                Speed(species, settings),
                TimeOffset(settings),
                ClusterRadius(species, settings));
            var areaChannel = new Vector4(area.x, area.y, area.z, species.bodyLength);
            var animation = new Vector4(
                (float)species.animation,
                Mathf.Max(species.beatFrequency, 0f),
                Amplitude(species),
                Mathf.Clamp01(species.glide));
            var extra = new Vector4(count, BankGain(species), MaxPitch(species), Mathf.Clamp(species.sizeVariance, 0f, 0.5f));

            for (int i = 0; i < count; i++)
            {
                int offset = positions.Count;
                Vector3 r = IndividualRandom(settings.seed, i);
                var individual = new Vector4(i, r.x, r.y, r.z);

                positions.AddRange(body.Positions);
                normals.AddRange(body.Normals);
                colors.AddRange(body.Colors);
                uvBody.AddRange(body.Body);
                for (int v = 0; v < perBody; v++)
                {
                    uvIndividual.Add(individual);
                    uvSwarm.Add(swarm);
                    uvArea.Add(areaChannel);
                    uvAnimation.Add(animation);
                    uvExtra.Add(extra);
                }

                foreach (int index in body.Triangles)
                {
                    triangles.Add(index + offset);
                }
            }

            mesh.Clear();
            mesh.indexFormat = total > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;

            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetUVs(FlockShaderContract.BodyChannel, uvBody);
            mesh.SetUVs(FlockShaderContract.IndividualChannel, uvIndividual);
            mesh.SetUVs(FlockShaderContract.SwarmChannel, uvSwarm);
            mesh.SetUVs(FlockShaderContract.AreaChannel, uvArea);
            mesh.SetUVs(FlockShaderContract.AnimationChannel, uvAnimation);
            mesh.SetUVs(FlockShaderContract.ExtraChannel, uvExtra);
            mesh.SetTriangles(triangles, 0, false);

            // The vertices hold one body at the origin; the shader spreads the
            // individuals over the area, which Unity's culling cannot know.
            Vector3 extents = BoundsExtents(species, settings);
            mesh.bounds = new Bounds(Vector3.zero, extents * 2f);
        }

        /// <summary>Vertex and triangle count of one individual at a detail tier.</summary>
        public static void BodyCost(FlockSpecies species, FlockDetail detail, out int vertices, out int triangles)
        {
            FlockMeshBuffer body = FlockBodyBuilder.Build(species, detail);
            vertices = body.VertexCount;
            triangles = body.TriangleCount;
        }
    }
}
