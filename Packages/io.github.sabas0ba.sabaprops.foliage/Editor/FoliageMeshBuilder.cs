using UnityEngine;

namespace SabaProps.Foliage.Editors
{
    /// <summary>
    /// Procedural mesh generation for the built-in species.
    /// <para>
    /// Everything is geometry plus vertex colours: no textures, no alpha test.
    /// That keeps the package free of binary assets and, more importantly, keeps
    /// the fragment shader trivial — thousands of alpha-tested quads are far more
    /// expensive on the GPU than the same silhouette expressed as opaque tris.
    /// </para>
    /// </summary>
    public static class FoliageMeshBuilder
    {
        /// <summary>Builds the mesh described by a species asset.</summary>
        public static Mesh Build(FoliageSpecies species)
        {
            if (species == null)
            {
                return null;
            }

            BuiltMesh built = BuildBuffer(species);

            // Every species reaches the season pass through here, so a new
            // generator gets all four seasons without knowing they exist.
            FoliageSeasonPass.Apply(built.Buffer, species.ActiveSeasonStyle);

            return built.ToMesh();
        }

        private static BuiltMesh BuildBuffer(FoliageSpecies species)
        {
            // Dormant drops the parts of a plant that do not last a year.
            // Species with nothing to drop ignore it and differ by colour alone,
            // which is the correct answer for grass: it browns, it does not
            // disappear.
            bool dormant = species.ActiveAppearance != SeasonAppearance.Full;

            switch (species.kind)
            {
                case FoliageSpeciesKind.Sunflower:
                    return BuildSunflowerBuffer(species.sunflower, species.meshSeed, dormant);

                case FoliageSpeciesKind.Clover:
                    return BuildCloverBuffer(species.clover, species.meshSeed);

                case FoliageSpeciesKind.Reed:
                    return BuildReedBuffer(species.reed, species.meshSeed);

                case FoliageSpeciesKind.SmallFlower:
                    return BuildSmallFlowerBuffer(species.smallFlower, species.meshSeed, dormant);

                case FoliageSpeciesKind.Weed:
                    return BuildWeedBuffer(species.weed, species.meshSeed);

                case FoliageSpeciesKind.GrassClump:
                default:
                    return BuildGrassClumpBuffer(species.grass, species.meshSeed);
            }
        }

        /// <summary>
        /// A finished buffer together with the name and bounds padding it bakes
        /// with. Generators stop one step short of a mesh so that passes which
        /// need per-vertex data the mesh does not carry — the season pass and its
        /// weights — can still run.
        /// </summary>
        private struct BuiltMesh
        {
            public FoliageMeshBuffer Buffer;
            public string Name;
            public float BoundsPadding;

            public Mesh ToMesh()
            {
                return Buffer.ToMesh(Name, BoundsPadding);
            }
        }

        /// <summary>
        /// The parts of a tapered blade that grass and reeds share. Passed
        /// explicitly rather than as a params object so the two species can keep
        /// their own inspector layouts without one dictating the other's.
        /// </summary>
        private struct BladeShape
        {
            public int Segments;
            public float Taper;
            public float NormalUpBlend;
            public float RootOcclusion;
            public float Stiffness;

            /// <summary>
            /// 0 tapers from the base, which is a grass blade. 1 bulges
            /// through the middle, which is a broad leaf. Defaulting to 0
            /// leaves grass and reeds generating exactly what they did.
            /// </summary>
            public float Bulge;
        }

        // ------------------------------------------------------------------
        // Grass
        // ------------------------------------------------------------------

        public static Mesh BuildGrassClump(GrassParams p, int seed)
        {
            return BuildGrassClumpBuffer(p, seed).ToMesh();
        }

        private static BuiltMesh BuildGrassClumpBuffer(GrassParams p, int seed)
        {
            var rng = new FoliageRandom(seed);
            var buffer = new FoliageMeshBuffer();

            int bladeCount = Mathf.Max(1, p.bladeCount);
            float tallest = 0f;

            var shape = new BladeShape
            {
                Segments = p.segments,
                Taper = p.taper,
                NormalUpBlend = p.normalUpBlend,
                RootOcclusion = p.rootOcclusion,
                Stiffness = p.stiffness,
            };

            for (int i = 0; i < bladeCount; i++)
            {
                float angle = rng.Range(0f, Mathf.PI * 2f);

                // sqrt keeps the roots uniformly distributed over the disc
                // instead of bunching them at the centre.
                float distance = Mathf.Sqrt(rng.Value01()) * p.clumpRadius;
                var root = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);

                // Blades mostly lean away from the clump centre, which reads as
                // a tuft rather than a bundle of parallel sticks.
                float facing = angle + rng.Range(-1.1f, 1.1f);
                var bendDir = new Vector3(Mathf.Cos(facing), 0f, Mathf.Sin(facing));

                float height = p.height * rng.Range(1f - p.heightVariance, 1f + p.heightVariance);
                float width = p.width * rng.Range(1f - p.widthVariance, 1f + p.widthVariance);
                float bend = p.bend * height * rng.Range(0.55f, 1.45f);
                float elementSeed = rng.Value01();

                Color rootColor = JitterColor(p.rootColor, ref rng, p.perBladeTintJitter);
                Color tipColor = JitterColor(p.tipColor, ref rng, p.perBladeTintJitter);

                AddBlade(buffer, shape, root, bendDir, height, width, bend, elementSeed, rootColor, tipColor);
                tallest = Mathf.Max(tallest, height);
            }

            return new BuiltMesh
            {
                Buffer = buffer,
                Name = "SabaFoliage_GrassClump",
                BoundsPadding = tallest * 0.35f,
            };
        }

        private static void AddBlade(
            FoliageMeshBuffer buffer, BladeShape p,
            Vector3 root, Vector3 bendDir,
            float height, float width, float bend,
            float elementSeed, Color rootColor, Color tipColor)
        {
            var side = new Vector3(-bendDir.z, 0f, bendDir.x);
            int segments = Mathf.Max(1, p.Segments);
            var rootData = new Vector4(root.x, root.y, root.z, p.Stiffness);

            int previousLeft = -1;
            int previousRight = -1;

            for (int s = 0; s < segments; s++)
            {
                float t = s / (float)segments;
                Vector3 center = BladePoint(root, bendDir, height, bend, t);
                Vector3 normal = BladeNormal(p, side, bendDir, height, bend, t);

                float halfWidth = width * 0.5f * BladeProfile(p, t);
                Color color = ShadeBlade(rootColor, tipColor, p.RootOcclusion, t, elementSeed);

                int left = buffer.AddVertex(center - side * halfWidth, normal, color, new Vector2(0f, t), rootData);
                int right = buffer.AddVertex(center + side * halfWidth, normal, color, new Vector2(1f, t), rootData);

                if (s > 0)
                {
                    buffer.AddQuad(previousLeft, previousRight, right, left);
                }

                previousLeft = left;
                previousRight = right;
            }

            // Converge to a single vertex at the tip: one triangle instead of a
            // degenerate quad, and a cleaner silhouette.
            Vector3 tipPoint = BladePoint(root, bendDir, height, bend, 1f);
            Vector3 tipNormal = BladeNormal(p, side, bendDir, height, bend, 1f);
            Color tipShaded = ShadeBlade(rootColor, tipColor, p.RootOcclusion, 1f, elementSeed);

            int tip = buffer.AddVertex(tipPoint, tipNormal, tipShaded, new Vector2(0.5f, 1f), rootData);
            buffer.AddTriangle(previousLeft, previousRight, tip);
        }

        /// <summary>
        /// Half-width along a blade, as a fraction of its widest point.
        /// <para>
        /// A grass blade is widest where it leaves the ground and narrows all
        /// the way up. A broad leaf is widest across its middle. The two are the
        /// same strip of geometry with a different profile, which is why they
        /// share a generator instead of having one each.
        /// </para>
        /// </summary>
        private static float BladeProfile(BladeShape p, float t)
        {
            float taper = Mathf.Pow(1f - t, p.Taper);

            if (p.Bulge <= 0f)
            {
                return taper;
            }

            // Never reaches zero at the base: a leaf that pinched to nothing
            // where it meets the crown would be a degenerate quad, and the tip
            // is already a single converged vertex.
            float bulge = Mathf.Lerp(0.45f, 1f, Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)));

            return Mathf.Lerp(taper, bulge, Mathf.Clamp01(p.Bulge));
        }

        private static Vector3 BladePoint(Vector3 root, Vector3 bendDir, float height, float bend, float t)
        {
            // Quadratic arc: vertical near the root, leaning hard near the tip.
            return root + Vector3.up * (height * t) + bendDir * (bend * t * t);
        }

        private static Vector3 BladeNormal(BladeShape p, Vector3 side, Vector3 bendDir, float height, float bend, float t)
        {
            Vector3 tangent = (Vector3.up * height + bendDir * (2f * bend * t)).normalized;
            Vector3 face = Vector3.Cross(side, tangent).normalized;

            // Biasing the normal upwards is the standard foliage trick: it makes
            // both faces of a two-sided blade light consistently, so we never
            // need a VFACE flip in the fragment stage.
            return Vector3.Slerp(face, Vector3.up, p.NormalUpBlend).normalized;
        }

        private static Color ShadeBlade(Color rootColor, Color tipColor, float rootOcclusion, float t, float elementSeed)
        {
            Color color = Color.Lerp(rootColor, tipColor, t);
            float occlusion = Mathf.Lerp(1f - rootOcclusion, 1f, t);
            color.r *= occlusion;
            color.g *= occlusion;
            color.b *= occlusion;

            // Alpha carries the per-element random seed, not opacity.
            color.a = elementSeed;
            return color;
        }

        // ------------------------------------------------------------------
        // Sunflower
        // ------------------------------------------------------------------

        public static Mesh BuildSunflower(SunflowerParams p, int seed)
        {
            return BuildSunflowerBuffer(p, seed, false).ToMesh();
        }

        /// <summary>
        /// <paramref name="dormant"/> builds the plant after its petals have
        /// gone: stem, leaves and the seed head it leaves behind, hanging
        /// further over than a flower in bloom does.
        /// </summary>
        private static BuiltMesh BuildSunflowerBuffer(SunflowerParams p, int seed, bool dormant)
        {
            var rng = new FoliageRandom(seed);
            var buffer = new FoliageMeshBuffer();

            float height = p.height * rng.Range(1f - p.heightVariance, 1f + p.heightVariance);

            // The stem leans, and the head tilts, along object-space +Z. Fixing
            // the direction rather than randomising it is what lets the
            // scatterer aim the flower: with Face Sun off, per-instance yaw is
            // random anyway, so nothing is lost.
            Vector3 leanDir = Vector3.forward;
            float lean = p.lean * rng.Range(0.6f, 1.4f);

            // One seed for the whole plant so stem, leaves and head sway together.
            float plantSeed = rng.Value01();

            AddStem(buffer, p, leanDir, height, lean, plantSeed);
            AddLeaves(buffer, p, ref rng, leanDir, height, lean, plantSeed);
            AddHead(buffer, p, ref rng, leanDir, height, lean, plantSeed, dormant);

            return new BuiltMesh
            {
                Buffer = buffer,
                Name = "SabaFoliage_Sunflower",
                BoundsPadding = height * 0.3f,
            };
        }

        private static Vector3 StemPoint(Vector3 leanDir, float height, float lean, float t)
        {
            return new Vector3(leanDir.x * lean * t * t, height * t, leanDir.z * lean * t * t);
        }

        private static void AddStem(
            FoliageMeshBuffer buffer, SunflowerParams p,
            Vector3 leanDir, float height, float lean, float plantSeed)
        {
            var rootData = new Vector4(0f, 0f, 0f, p.stemStiffness);

            // Two crossed strips read as a round stem from every angle for the
            // price of a handful of triangles.
            var axes = new[]
            {
                new Vector3(-leanDir.z, 0f, leanDir.x),
                leanDir,
            };

            int segments = Mathf.Max(1, p.stemSegments);

            foreach (Vector3 side in axes)
            {
                int previousLeft = -1;
                int previousRight = -1;

                for (int s = 0; s <= segments; s++)
                {
                    float t = s / (float)segments;
                    Vector3 center = StemPoint(leanDir, height, lean, t);

                    // Taper slightly towards the head.
                    float halfWidth = p.stemWidth * 0.5f * Mathf.Lerp(1f, 0.65f, t);

                    Vector3 tangent = (Vector3.up * height + leanDir * (2f * lean * t)).normalized;
                    Vector3 normal = Vector3.Cross(side, tangent).normalized;

                    Color color = p.stemColor;
                    float occlusion = Mathf.Lerp(0.7f, 1f, t);
                    color.r *= occlusion;
                    color.g *= occlusion;
                    color.b *= occlusion;
                    color.a = plantSeed;

                    int left = buffer.AddVertex(center - side * halfWidth, normal, color, new Vector2(0f, t), rootData);
                    int right = buffer.AddVertex(center + side * halfWidth, normal, color, new Vector2(1f, t), rootData);

                    if (s > 0)
                    {
                        buffer.AddQuad(previousLeft, previousRight, right, left);
                    }

                    previousLeft = left;
                    previousRight = right;
                }
            }
        }

        private static void AddLeaves(
            FoliageMeshBuffer buffer, SunflowerParams p, ref FoliageRandom rng,
            Vector3 leanDir, float height, float lean, float plantSeed)
        {
            int leafCount = Mathf.Max(0, p.leafCount);
            if (leafCount == 0)
            {
                return;
            }

            var rootData = new Vector4(0f, 0f, 0f, p.stemStiffness);

            for (int i = 0; i < leafCount; i++)
            {
                float t = leafCount == 1 ? 0.45f : Mathf.Lerp(0.22f, 0.72f, i / (float)(leafCount - 1));
                Vector3 attach = StemPoint(leanDir, height, lean, t);

                // Golden-angle spiral around the stem, like real phyllotaxis.
                float angle = i * 2.399963f + rng.Range(-0.3f, 0.3f);
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                float length = p.leafLength * rng.Range(0.8f, 1.2f);
                float width = p.leafWidth * rng.Range(0.8f, 1.2f);
                float droop = Mathf.Tan(p.leafDroop * Mathf.Deg2Rad) * length;

                Vector3 tip = attach + outward * length - Vector3.up * droop;
                Vector3 mid = attach + outward * (length * 0.42f) - Vector3.up * (droop * 0.25f);
                var side = new Vector3(-outward.z, 0f, outward.x);

                Vector3 spineDir = (tip - attach).normalized;
                Vector3 normal = Vector3.Cross(side, spineDir).normalized;
                if (normal.y < 0f)
                {
                    normal = -normal;
                }

                normal = Vector3.Slerp(normal, Vector3.up, 0.55f).normalized;

                Color color = JitterColor(p.leafColor, ref rng, 0.08f);
                color.a = plantSeed;

                // The base takes the stem's own mask at the attachment point, or
                // the leaf lifts off the stem as the plant sways. Only the part
                // of the leaf that hangs free trails behind.
                int baseIndex = buffer.AddVertex(attach, normal, color, new Vector2(0.5f, t), rootData);
                int leftIndex = buffer.AddVertex(mid - side * (width * 0.5f), normal, color, new Vector2(0f, Mathf.Clamp01(t + 0.06f)), rootData);
                int rightIndex = buffer.AddVertex(mid + side * (width * 0.5f), normal, color, new Vector2(1f, Mathf.Clamp01(t + 0.06f)), rootData);
                int tipIndex = buffer.AddVertex(tip, normal, color, new Vector2(0.5f, Mathf.Clamp01(t + 0.14f)), rootData);

                buffer.AddTriangle(baseIndex, leftIndex, tipIndex);
                buffer.AddTriangle(baseIndex, tipIndex, rightIndex);
            }
        }

        private static void AddHead(
            FoliageMeshBuffer buffer, SunflowerParams p, ref FoliageRandom rng,
            Vector3 leanDir, float height, float lean, float plantSeed, bool dormant)
        {
            Vector3 top = StemPoint(leanDir, height, lean, 1f);

            // Tilt the disc away from vertical, towards whichever way the stem
            // leans, so the flower "looks" somewhere instead of straight up.
            Vector3 tiltAxis = Vector3.Cross(Vector3.up, leanDir);
            if (tiltAxis.sqrMagnitude < 1e-6f)
            {
                tiltAxis = Vector3.forward;
            }

            // A spent head is heavy and no longer tracks anything, so it hangs
            // rather than faces. Tilted further over than the parameter asks
            // for, never less: a species already drooping stays as it was.
            float headTilt = dormant ? Mathf.Max(p.headTilt, 74f) : p.headTilt;

            Vector3 headNormal = (Quaternion.AngleAxis(headTilt, tiltAxis.normalized) * Vector3.up).normalized;

            Vector3 tangentA = Vector3.Cross(headNormal, Vector3.up);
            if (tangentA.sqrMagnitude < 1e-6f)
            {
                tangentA = Vector3.right;
            }

            tangentA.Normalize();
            Vector3 tangentB = Vector3.Cross(headNormal, tangentA).normalized;

            Vector3 center = top + headNormal * (p.headRadius * 0.15f);

            // The head rides on the stem tip as one rigid piece: same wind phase,
            // same bend mask, same stiffness as the stem's last ring. Any
            // mismatch across a joint is relative motion, and relative motion at
            // a joint is a visible tear.
            var headRootData = new Vector4(0f, 0f, 0f, p.stemStiffness);

            // Petal tips may travel a little further than the disc they grow
            // from. That difference is exactly what stretches a petal, and a
            // petal is a few centimetres long, so only a fraction of the
            // requested petal stiffness is spent on it.
            float petalTip = Mathf.Lerp(p.stemStiffness, Mathf.Max(p.stemStiffness, p.petalStiffness), 0.25f);
            float petalMid = Mathf.Lerp(p.stemStiffness, petalTip, 0.6f);

            var petalInnerData = headRootData;
            var petalMidData = new Vector4(0f, 0f, 0f, petalMid);
            var petalTipData = new Vector4(0f, 0f, 0f, petalTip);

            // --- disc -------------------------------------------------------
            // The disc is already a dead brown, so a season has little left to
            // do to it; letting it turn as far as a leaf only muddies the
            // contrast against the petals.
            buffer.SeasonWeight = 0.55f;

            Color centerColor = p.headColor;
            centerColor.a = plantSeed;

            Color rimColor = p.headRimColor;
            rimColor.a = plantSeed;

            int sides = Mathf.Max(5, p.headSides);
            int centerIndex = buffer.AddVertex(center, headNormal, centerColor, new Vector2(0.5f, 1f), headRootData);

            var ring = new int[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = i / (float)sides * Mathf.PI * 2f;
                Vector3 offset = (tangentA * Mathf.Cos(angle) + tangentB * Mathf.Sin(angle)) * p.headRadius;

                // Slight dome so the disc catches a gradient instead of reading flat.
                Vector3 position = center + offset - headNormal * (p.headRadius * 0.12f);
                Vector3 normal = Vector3.Slerp(headNormal, offset.normalized, 0.25f).normalized;

                ring[i] = buffer.AddVertex(position, normal, rimColor, new Vector2(0.5f, 1f), headRootData);
            }

            for (int i = 0; i < sides; i++)
            {
                buffer.AddTriangle(centerIndex, ring[i], ring[(i + 1) % sides]);
            }

            // --- petals -----------------------------------------------------
            if (dormant)
            {
                // Nothing else to add: the seed head above is what a sunflower
                // is once the petals have gone.
                buffer.SeasonWeight = 1f;
                return;
            }

            // Petals hold most of their own colour through the year. A sunflower
            // whose petals bleach to straw along with the grass around it is no
            // longer identifiable as one.
            buffer.SeasonWeight = 0.3f;

            int petalCount = Mathf.Max(4, p.petalCount);
            float innerRadius = p.headRadius * 0.85f;
            float outerRadius = p.headRadius + p.petalLength;

            for (int i = 0; i < petalCount; i++)
            {
                float angle = i / (float)petalCount * Mathf.PI * 2f + rng.Range(-0.05f, 0.05f);
                Vector3 radial = (tangentA * Mathf.Cos(angle) + tangentB * Mathf.Sin(angle)).normalized;
                Vector3 across = Vector3.Cross(headNormal, radial).normalized;

                float halfWidth = p.petalWidth * 0.5f * rng.Range(0.85f, 1.15f);
                float length = outerRadius * rng.Range(0.92f, 1.08f);

                Vector3 inner = center + radial * innerRadius;
                Vector3 outer = center + radial * (length * 0.72f) + headNormal * p.petalCurl;
                Vector3 tip = center + radial * length + headNormal * (p.petalCurl * 1.6f);

                Vector3 normal = Vector3.Slerp(headNormal, radial, 0.15f).normalized;

                Color baseColor = p.petalBaseColor;
                Color tipColor = p.petalTipColor;

                // One phase for the whole plant. A petal is rigidly attached to
                // the disc, so giving it a phase of its own only buys a gap
                // between the two.
                baseColor.a = plantSeed;
                tipColor.a = plantSeed;

                Color midColor = Color.Lerp(baseColor, tipColor, 0.6f);

                // Every head vertex sits at the top of the bend mask; the travel
                // along a petal comes from its stiffness ramp alone, so the disc
                // and the petal roots cannot drift apart.
                int innerLeft = buffer.AddVertex(inner - across * (halfWidth * 0.6f), normal, baseColor, new Vector2(0f, 1f), petalInnerData);
                int innerRight = buffer.AddVertex(inner + across * (halfWidth * 0.6f), normal, baseColor, new Vector2(1f, 1f), petalInnerData);
                int outerLeft = buffer.AddVertex(outer - across * halfWidth, normal, midColor, new Vector2(0f, 1f), petalMidData);
                int outerRight = buffer.AddVertex(outer + across * halfWidth, normal, midColor, new Vector2(1f, 1f), petalMidData);
                int tipIndex = buffer.AddVertex(tip, normal, tipColor, new Vector2(0.5f, 1f), petalTipData);

                buffer.AddQuad(innerLeft, innerRight, outerRight, outerLeft);
                buffer.AddTriangle(outerLeft, outerRight, tipIndex);
            }

            buffer.SeasonWeight = 1f;
        }

        // ------------------------------------------------------------------
        // Clover
        // ------------------------------------------------------------------

        public static Mesh BuildClover(CloverParams p, int seed)
        {
            return BuildCloverBuffer(p, seed).ToMesh();
        }

        private static BuiltMesh BuildCloverBuffer(CloverParams p, int seed)
        {
            var rng = new FoliageRandom(seed);
            var buffer = new FoliageMeshBuffer();

            float height = p.height * rng.Range(1f - p.heightVariance, 1f + p.heightVariance);

            // One phase for the whole plant, as with the sunflower: the leaflets
            // are rigidly attached to the stem tip, so a phase of their own would
            // only pull them off it.
            float plantSeed = rng.Value01();

            float leanAngle = rng.Range(0f, Mathf.PI * 2f);
            var leanDir = new Vector3(Mathf.Cos(leanAngle), 0f, Mathf.Sin(leanAngle));
            float lean = height * 0.18f * rng.Range(0f, 1f);

            Vector3 top = new Vector3(leanDir.x * lean, height, leanDir.z * lean);
            var rootData = new Vector4(0f, 0f, 0f, p.stiffness);

            Color leaf = JitterColor(p.leafColor, ref rng, p.perPlantTintJitter);
            Color rim = JitterColor(p.leafRimColor, ref rng, p.perPlantTintJitter);
            leaf.a = plantSeed;
            rim.a = plantSeed;

            AddCloverStem(buffer, p, leanDir, top, height, leaf, rootData);

            int leaflets = Mathf.Max(2, p.leafletCount);
            float droop = Mathf.Tan(p.leafDroop * Mathf.Deg2Rad) * p.leafLength;

            for (int i = 0; i < leaflets; i++)
            {
                float angle = i / (float)leaflets * Mathf.PI * 2f + rng.Range(-0.12f, 0.12f);
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var side = new Vector3(-outward.z, 0f, outward.x);

                float length = p.leafLength * rng.Range(0.88f, 1.12f);
                float halfWidth = p.leafWidth * 0.5f * rng.Range(0.88f, 1.12f);

                AddCloverLeaflet(buffer, p, top, outward, side, length, halfWidth, droop, leaf, rim, rootData);
            }

            return new BuiltMesh
            {
                Buffer = buffer,
                Name = "SabaFoliage_Clover",
                BoundsPadding = height * 0.4f,
            };
        }

        private static void AddCloverStem(
            FoliageMeshBuffer buffer, CloverParams p,
            Vector3 leanDir, Vector3 top, float height, Color color, Vector4 rootData)
        {
            var side = new Vector3(-leanDir.z, 0f, leanDir.x);
            float halfWidth = p.stemWidth * 0.5f;

            Color rootColor = color;
            float occlusion = 1f - p.rootOcclusion;
            rootColor.r *= occlusion;
            rootColor.g *= occlusion;
            rootColor.b *= occlusion;

            Vector3 normal = Vector3.Cross(side, (top - Vector3.zero).normalized).normalized;
            if (normal.sqrMagnitude < 1e-6f)
            {
                normal = Vector3.up;
            }

            int baseLeft = buffer.AddVertex(-side * halfWidth, normal, rootColor, new Vector2(0f, 0f), rootData);
            int baseRight = buffer.AddVertex(side * halfWidth, normal, rootColor, new Vector2(1f, 0f), rootData);
            int topLeft = buffer.AddVertex(top - side * halfWidth, normal, color, new Vector2(0f, 1f), rootData);
            int topRight = buffer.AddVertex(top + side * halfWidth, normal, color, new Vector2(1f, 1f), rootData);

            buffer.AddQuad(baseLeft, baseRight, topRight, topLeft);
        }

        /// <summary>
        /// One heart-shaped leaflet, fanned from the stem tip. Every vertex sits
        /// at the top of the bend mask and carries the stem's stiffness, so the
        /// leaflet cannot drift away from the stem it grows out of.
        /// </summary>
        private static void AddCloverLeaflet(
            FoliageMeshBuffer buffer, CloverParams p,
            Vector3 top, Vector3 outward, Vector3 side,
            float length, float halfWidth, float droop,
            Color leaf, Color rim, Vector4 rootData)
        {
            Vector3 fall = Vector3.down * droop;

            Vector3 waist = top + outward * (length * 0.45f) + fall * 0.45f;
            Vector3 lobe = top + outward * (length * 0.95f) + fall;
            Vector3 notch = top + outward * (length * (0.95f - p.notch)) + fall * 0.95f;

            Vector3 spine = (lobe - top).normalized;
            Vector3 face = Vector3.Cross(side, spine).normalized;
            if (face.y < 0f)
            {
                face = -face;
            }

            Vector3 normal = Vector3.Slerp(face, Vector3.up, 0.6f).normalized;

            var uv = new Vector2(0.5f, 1f);

            int baseIndex = buffer.AddVertex(top, normal, leaf, uv, rootData);
            int waistLeft = buffer.AddVertex(waist - side * halfWidth, normal, leaf, new Vector2(0f, 1f), rootData);
            int waistRight = buffer.AddVertex(waist + side * halfWidth, normal, leaf, new Vector2(1f, 1f), rootData);
            int lobeLeft = buffer.AddVertex(lobe - side * (halfWidth * 0.62f), normal, rim, new Vector2(0.2f, 1f), rootData);
            int lobeRight = buffer.AddVertex(lobe + side * (halfWidth * 0.62f), normal, rim, new Vector2(0.8f, 1f), rootData);
            int notchIndex = buffer.AddVertex(notch, normal, rim, uv, rootData);

            buffer.AddTriangle(baseIndex, waistLeft, lobeLeft);
            buffer.AddTriangle(baseIndex, lobeLeft, notchIndex);
            buffer.AddTriangle(baseIndex, notchIndex, lobeRight);
            buffer.AddTriangle(baseIndex, lobeRight, waistRight);
        }

        // ------------------------------------------------------------------
        // Reed
        // ------------------------------------------------------------------

        public static Mesh BuildReed(ReedParams p, int seed)
        {
            return BuildReedBuffer(p, seed).ToMesh();
        }

        private static BuiltMesh BuildReedBuffer(ReedParams p, int seed)
        {
            var rng = new FoliageRandom(seed);
            var buffer = new FoliageMeshBuffer();

            var shape = new BladeShape
            {
                Segments = p.segments,
                Taper = p.taper,
                NormalUpBlend = p.normalUpBlend,
                RootOcclusion = p.rootOcclusion,
                Stiffness = p.stiffness,
            };

            int bladeCount = Mathf.Max(1, p.bladeCount);
            float tallest = 0f;

            Vector3 tallestTop = Vector3.zero;
            Vector3 tallestDir = Vector3.forward;
            Vector3 tallestRoot = Vector3.zero;
            float tallestSeed = 0f;

            for (int i = 0; i < bladeCount; i++)
            {
                float angle = rng.Range(0f, Mathf.PI * 2f);
                float distance = Mathf.Sqrt(rng.Value01()) * p.clumpRadius;
                var root = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);

                float facing = angle + rng.Range(-0.7f, 0.7f);
                var bendDir = new Vector3(Mathf.Cos(facing), 0f, Mathf.Sin(facing));

                float height = p.height * rng.Range(1f - p.heightVariance, 1f + p.heightVariance);
                float width = p.width * rng.Range(1f - p.widthVariance, 1f + p.widthVariance);
                float bend = p.spread * height * rng.Range(0.4f, 1.3f);
                float elementSeed = rng.Value01();

                AddBlade(buffer, shape, root, bendDir, height, width, bend, elementSeed, p.rootColor, p.tipColor);

                if (height > tallest)
                {
                    tallest = height;
                    tallestTop = BladePoint(root, bendDir, height, bend, 1f);
                    tallestDir = bendDir;
                    // The spike must sway with the blade it sits on, not with a
                    // phase of its own, or it detaches at the tip.
                    tallestSeed = elementSeed;
                    tallestRoot = root;
                }
            }

            if (p.spike && bladeCount > 0)
            {
                AddReedSpike(buffer, p, tallestTop, tallestDir, tallestRoot, tallestSeed);
            }

            return new BuiltMesh
            {
                Buffer = buffer,
                Name = "SabaFoliage_Reed",
                BoundsPadding = tallest * 0.35f,
            };
        }

        private static void AddReedSpike(
            FoliageMeshBuffer buffer, ReedParams p,
            Vector3 attach, Vector3 bendDir, Vector3 root, float elementSeed)
        {
            // Two crossed strips, the same trick the sunflower stem uses.
            var axes = new[]
            {
                new Vector3(-bendDir.z, 0f, bendDir.x),
                bendDir,
            };

            Vector3 tip = attach + Vector3.up * p.spikeLength + bendDir * (p.spikeLength * 0.18f);
            var rootData = new Vector4(root.x, root.y, root.z, p.stiffness);

            Color color = p.spikeColor;
            color.a = elementSeed;

            // A seed head is the same brown whatever the month.
            buffer.SeasonWeight = 0.5f;

            foreach (Vector3 side in axes)
            {
                Vector3 normal = Vector3.Cross(side, (tip - attach).normalized).normalized;
                if (normal.sqrMagnitude < 1e-6f)
                {
                    normal = Vector3.up;
                }

                normal = Vector3.Slerp(normal, Vector3.up, p.normalUpBlend).normalized;

                float halfWidth = p.spikeWidth * 0.5f;

                // The base sits exactly on the blade tip, which is at the top of
                // the bend mask, so the two move as one.
                int baseLeft = buffer.AddVertex(attach - side * (halfWidth * 0.45f), normal, color, new Vector2(0f, 1f), rootData);
                int baseRight = buffer.AddVertex(attach + side * (halfWidth * 0.45f), normal, color, new Vector2(1f, 1f), rootData);
                int midLeft = buffer.AddVertex(attach + (tip - attach) * 0.45f - side * halfWidth, normal, color, new Vector2(0f, 1f), rootData);
                int midRight = buffer.AddVertex(attach + (tip - attach) * 0.45f + side * halfWidth, normal, color, new Vector2(1f, 1f), rootData);
                int tipIndex = buffer.AddVertex(tip, normal, color, new Vector2(0.5f, 1f), rootData);

                buffer.AddQuad(baseLeft, baseRight, midRight, midLeft);
                buffer.AddTriangle(midLeft, midRight, tipIndex);
            }

            buffer.SeasonWeight = 1f;
        }

        // ------------------------------------------------------------------
        // Small flower
        // ------------------------------------------------------------------

        public static Mesh BuildSmallFlower(SmallFlowerParams p, int seed)
        {
            return BuildSmallFlowerBuffer(p, seed, false).ToMesh();
        }

        /// <summary>
        /// A stem, a few leaves and one or more open flowers.
        /// <para>
        /// Deliberately one generator for the whole family. Nemophila and a
        /// potato flower are both five rounded petals around a pale eye on a
        /// short stem; what separates them is colour and proportion, and a
        /// second generator would only be the first one with different
        /// constants in it.
        /// </para>
        /// <para>
        /// <paramref name="dormant"/> leaves out the flowers. What remains is
        /// the stem and leaves, which is what a small annual looks like before
        /// it opens and after it is spent.
        /// </para>
        /// </summary>
        private static BuiltMesh BuildSmallFlowerBuffer(SmallFlowerParams p, int seed, bool dormant)
        {
            var rng = new FoliageRandom(seed);
            var buffer = new FoliageMeshBuffer();

            float height = p.height * rng.Range(1f - p.heightVariance, 1f + p.heightVariance);

            // Leans along object-space +Z, the convention the sunflower already
            // uses: per-instance yaw is randomised at placement, so fixing the
            // direction here costs nothing and keeps Face Sun usable.
            Vector3 leanDir = Vector3.forward;
            float lean = p.lean * rng.Range(0.4f, 1.6f);

            // One phase for the whole plant. Stem, leaves and flowers are rigidly
            // joined, and parts of one plant that sway out of step come apart.
            float plantSeed = rng.Value01();

            AddSmallFlowerStem(buffer, p, leanDir, height, lean, plantSeed);
            AddSmallFlowerLeaves(buffer, p, ref rng, leanDir, height, lean, plantSeed);

            if (!dormant)
            {
                AddSmallFlowerHeads(buffer, p, ref rng, leanDir, height, lean, plantSeed);
            }

            return new BuiltMesh
            {
                Buffer = buffer,
                Name = "SabaFoliage_SmallFlower",
                BoundsPadding = height * 0.35f,
            };
        }

        private static Vector3 SmallFlowerStemPoint(Vector3 leanDir, float height, float lean, float t)
        {
            return new Vector3(leanDir.x * lean * t * t, height * t, leanDir.z * lean * t * t);
        }

        private static void AddSmallFlowerStem(
            FoliageMeshBuffer buffer, SmallFlowerParams p,
            Vector3 leanDir, float height, float lean, float plantSeed)
        {
            // Two crossed strips, so the stem reads as round from every angle
            // for four triangles. A single strip disappears edge-on, which on a
            // plant this thin is the difference between a flower on a stem and a
            // flower floating in the air.
            var axes = new[]
            {
                new Vector3(-leanDir.z, 0f, leanDir.x),
                leanDir,
            };

            var rootData = new Vector4(0f, 0f, 0f, p.stiffness);
            const int segments = 2;

            foreach (Vector3 side in axes)
            {
                int previousLeft = -1;
                int previousRight = -1;

                for (int s = 0; s <= segments; s++)
                {
                    float t = s / (float)segments;
                    Vector3 center = SmallFlowerStemPoint(leanDir, height, lean, t);

                    float halfWidth = p.stemWidth * 0.5f * Mathf.Lerp(1f, 0.7f, t);

                    Vector3 tangent = (Vector3.up * height + leanDir * (2f * lean * t)).normalized;
                    Vector3 normal = Vector3.Cross(side, tangent).normalized;

                    Color color = p.stemColor;
                    float occlusion = Mathf.Lerp(0.72f, 1f, t);
                    color.r *= occlusion;
                    color.g *= occlusion;
                    color.b *= occlusion;
                    color.a = plantSeed;

                    int left = buffer.AddVertex(center - side * halfWidth, normal, color, new Vector2(0f, t), rootData);
                    int right = buffer.AddVertex(center + side * halfWidth, normal, color, new Vector2(1f, t), rootData);

                    if (s > 0)
                    {
                        buffer.AddQuad(previousLeft, previousRight, right, left);
                    }

                    previousLeft = left;
                    previousRight = right;
                }
            }
        }

        private static void AddSmallFlowerLeaves(
            FoliageMeshBuffer buffer, SmallFlowerParams p, ref FoliageRandom rng,
            Vector3 leanDir, float height, float lean, float plantSeed)
        {
            int leafCount = Mathf.Max(0, p.leafCount);
            if (leafCount == 0)
            {
                return;
            }

            var rootData = new Vector4(0f, 0f, 0f, p.stiffness);

            for (int i = 0; i < leafCount; i++)
            {
                float t = leafCount == 1 ? 0.35f : Mathf.Lerp(0.14f, 0.62f, i / (float)(leafCount - 1));
                Vector3 attach = SmallFlowerStemPoint(leanDir, height, lean, t);

                float angle = i * 2.399963f + rng.Range(-0.35f, 0.35f);
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                float length = p.leafLength * rng.Range(0.8f, 1.2f);
                float width = p.leafWidth * rng.Range(0.8f, 1.2f);
                float droop = Mathf.Tan(p.leafDroop * Mathf.Deg2Rad) * length;

                Vector3 tip = attach + outward * length - Vector3.up * droop;
                Vector3 mid = attach + outward * (length * 0.45f) - Vector3.up * (droop * 0.25f);
                var side = new Vector3(-outward.z, 0f, outward.x);

                Vector3 spine = (tip - attach).normalized;
                Vector3 normal = Vector3.Cross(side, spine).normalized;
                if (normal.y < 0f)
                {
                    normal = -normal;
                }

                normal = Vector3.Slerp(normal, Vector3.up, 0.55f).normalized;

                Color color = JitterColor(p.leafColor, ref rng, 0.09f);
                color.a = plantSeed;

                // The base takes the stem's own mask at the attachment point, or
                // the leaf lifts off the stem as the plant sways.
                int baseIndex = buffer.AddVertex(attach, normal, color, new Vector2(0.5f, t), rootData);
                int leftIndex = buffer.AddVertex(mid - side * (width * 0.5f), normal, color, new Vector2(0f, Mathf.Clamp01(t + 0.05f)), rootData);
                int rightIndex = buffer.AddVertex(mid + side * (width * 0.5f), normal, color, new Vector2(1f, Mathf.Clamp01(t + 0.05f)), rootData);
                int tipIndex = buffer.AddVertex(tip, normal, color, new Vector2(0.5f, Mathf.Clamp01(t + 0.12f)), rootData);

                buffer.AddTriangle(baseIndex, leftIndex, tipIndex);
                buffer.AddTriangle(baseIndex, tipIndex, rightIndex);
            }
        }

        private static void AddSmallFlowerHeads(
            FoliageMeshBuffer buffer, SmallFlowerParams p, ref FoliageRandom rng,
            Vector3 leanDir, float height, float lean, float plantSeed)
        {
            int flowerCount = Mathf.Max(1, p.flowerCount);

            for (int i = 0; i < flowerCount; i++)
            {
                // The first flower crowns the stem; the rest branch off below it
                // on short pedicels, which is how a plant carrying several small
                // flowers actually arranges them.
                bool crown = i == 0;
                float attachT = crown ? 1f : Mathf.Lerp(0.62f, 0.88f, (i - 1) / Mathf.Max(1f, flowerCount - 1f));

                Vector3 attach = SmallFlowerStemPoint(leanDir, height, lean, attachT);

                float angle = i * 2.399963f + rng.Range(-0.4f, 0.4f);
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                Vector3 center = attach;
                float mask = attachT;

                if (!crown)
                {
                    float pedicel = height * rng.Range(0.14f, 0.24f);
                    center = attach + outward * (pedicel * 0.7f) + Vector3.up * (pedicel * 0.7f);

                    // The flower sits at the far end of the pedicel, so it has to
                    // sway by more than its attachment point does. One value for
                    // the pedicel tip and the whole flower, never a gradient
                    // across the joint.
                    mask = Mathf.Clamp01(attachT + 0.1f);

                    AddSmallFlowerPedicel(buffer, p, attach, center, attachT, mask, plantSeed);
                }

                AddSmallFlowerHead(buffer, p, ref rng, center, outward, mask, plantSeed);
            }
        }

        private static void AddSmallFlowerPedicel(
            FoliageMeshBuffer buffer, SmallFlowerParams p,
            Vector3 attach, Vector3 tip, float attachMask, float tipMask, float plantSeed)
        {
            Vector3 along = tip - attach;
            if (along.sqrMagnitude < 1e-8f)
            {
                return;
            }

            var side = Vector3.Cross(along.normalized, Vector3.up);
            if (side.sqrMagnitude < 1e-6f)
            {
                side = Vector3.right;
            }

            side.Normalize();

            Vector3 normal = Vector3.Cross(side, along.normalized).normalized;
            float halfWidth = p.stemWidth * 0.35f;

            Color color = p.stemColor;
            color.a = plantSeed;

            var attachData = new Vector4(0f, 0f, 0f, p.stiffness);

            int baseLeft = buffer.AddVertex(attach - side * halfWidth, normal, color, new Vector2(0f, attachMask), attachData);
            int baseRight = buffer.AddVertex(attach + side * halfWidth, normal, color, new Vector2(1f, attachMask), attachData);
            int tipLeft = buffer.AddVertex(tip - side * halfWidth, normal, color, new Vector2(0f, tipMask), attachData);
            int tipRight = buffer.AddVertex(tip + side * halfWidth, normal, color, new Vector2(1f, tipMask), attachData);

            buffer.AddQuad(baseLeft, baseRight, tipRight, tipLeft);
        }

        /// <summary>
        /// One open flower: a small eye with petals fanned around it.
        /// <para>
        /// Every vertex carries the same bend mask and the same wind phase, so
        /// the flower travels as one rigid piece. Only the petal tips carry a
        /// higher stiffness, which is what lets them move without pulling away
        /// from the eye they grow out of.
        /// </para>
        /// </summary>
        private static void AddSmallFlowerHead(
            FoliageMeshBuffer buffer, SmallFlowerParams p, ref FoliageRandom rng,
            Vector3 center, Vector3 outward, float mask, float plantSeed)
        {
            Vector3 tiltAxis = Vector3.Cross(Vector3.up, outward);
            if (tiltAxis.sqrMagnitude < 1e-6f)
            {
                tiltAxis = Vector3.forward;
            }

            Vector3 faceNormal = (Quaternion.AngleAxis(p.flowerTilt, tiltAxis.normalized) * Vector3.up).normalized;

            Vector3 tangentA = Vector3.Cross(faceNormal, Vector3.up);
            if (tangentA.sqrMagnitude < 1e-6f)
            {
                tangentA = Vector3.right;
            }

            tangentA.Normalize();
            Vector3 tangentB = Vector3.Cross(faceNormal, tangentA).normalized;

            var headData = new Vector4(0f, 0f, 0f, p.stiffness);

            // The same reasoning as the sunflower: a petal tip may travel a
            // little further than the eye it grows from, and that difference is
            // all a petal a centimetre long can afford.
            float petalTip = Mathf.Lerp(p.stiffness, Mathf.Max(p.stiffness, p.petalStiffness), 0.3f);
            var petalTipData = new Vector4(0f, 0f, 0f, petalTip);

            var uv = new Vector2(0.5f, mask);
            int petalCount = Mathf.Max(3, p.petalCount);

            // --- eye ---------------------------------------------------------
            // Already the colour that names the flower, so a season has less to
            // do to it than to a leaf.
            buffer.SeasonWeight = 0.55f;

            Color eyeColor = p.centerColor;
            eyeColor.a = plantSeed;

            int centerIndex = buffer.AddVertex(center, faceNormal, eyeColor, uv, headData);

            var ring = new int[petalCount];
            for (int i = 0; i < petalCount; i++)
            {
                float angle = i / (float)petalCount * Mathf.PI * 2f;
                Vector3 offset = (tangentA * Mathf.Cos(angle) + tangentB * Mathf.Sin(angle)) * p.centerRadius;

                ring[i] = buffer.AddVertex(center + offset, faceNormal, eyeColor, uv, headData);
            }

            for (int i = 0; i < petalCount; i++)
            {
                buffer.AddTriangle(centerIndex, ring[i], ring[(i + 1) % petalCount]);
            }

            // --- petals ------------------------------------------------------
            // A flower recoloured out of its own colour stops being that flower,
            // so the petals resist the season far more than the leaves do.
            buffer.SeasonWeight = 0.3f;

            Color baseColor = p.petalBaseColor;
            Color tipColor = p.petalTipColor;
            baseColor.a = plantSeed;
            tipColor.a = plantSeed;

            float outerRadius = p.centerRadius + p.petalLength;

            for (int i = 0; i < petalCount; i++)
            {
                float angle = i / (float)petalCount * Mathf.PI * 2f + rng.Range(-0.06f, 0.06f);
                Vector3 radial = (tangentA * Mathf.Cos(angle) + tangentB * Mathf.Sin(angle)).normalized;
                Vector3 across = Vector3.Cross(faceNormal, radial).normalized;

                float halfWidth = p.petalWidth * 0.5f * rng.Range(0.88f, 1.12f);
                float length = outerRadius * rng.Range(0.92f, 1.08f);

                Vector3 inner = center + radial * (p.centerRadius * 0.9f);
                Vector3 shoulder = center + radial * (length * 0.62f);
                Vector3 tip = center + radial * length;

                // Rounding widens the petal where a pointed one would already be
                // tapering. At this size that is the whole visible difference
                // between a nemophila and a daisy.
                float shoulderWidth = halfWidth * Mathf.Lerp(0.85f, 1f, p.petalRounding);
                float tipWidth = halfWidth * Mathf.Lerp(0f, 0.55f, p.petalRounding);

                Vector3 normal = Vector3.Slerp(faceNormal, radial, 0.12f).normalized;

                Color midColor = Color.Lerp(baseColor, tipColor, 0.55f);

                int innerLeft = buffer.AddVertex(inner - across * (halfWidth * 0.45f), normal, baseColor, uv, headData);
                int innerRight = buffer.AddVertex(inner + across * (halfWidth * 0.45f), normal, baseColor, uv, headData);
                int shoulderLeft = buffer.AddVertex(shoulder - across * shoulderWidth, normal, midColor, uv, headData);
                int shoulderRight = buffer.AddVertex(shoulder + across * shoulderWidth, normal, midColor, uv, headData);

                buffer.AddQuad(innerLeft, innerRight, shoulderRight, shoulderLeft);

                if (tipWidth > 1e-5f)
                {
                    int tipLeft = buffer.AddVertex(tip - across * tipWidth, normal, tipColor, uv, petalTipData);
                    int tipRight = buffer.AddVertex(tip + across * tipWidth, normal, tipColor, uv, petalTipData);

                    buffer.AddQuad(shoulderLeft, shoulderRight, tipRight, tipLeft);
                }
                else
                {
                    int tipIndex = buffer.AddVertex(tip, normal, tipColor, uv, petalTipData);
                    buffer.AddTriangle(shoulderLeft, shoulderRight, tipIndex);
                }
            }

            buffer.SeasonWeight = 1f;
        }


        // ------------------------------------------------------------------
        // Weed
        // ------------------------------------------------------------------

        public static Mesh BuildWeed(WeedParams p, int seed)
        {
            return BuildWeedBuffer(p, seed).ToMesh();
        }

        /// <summary>
        /// A rosette of broad leaves with a thin shoot or two through it.
        /// <para>
        /// Built from the same blade code as grass and reeds, because that is
        /// what a leaf is here too. Two things make it read as a weed rather
        /// than as coarse grass: the width profile bulges in the middle instead
        /// of tapering from the base, and the length variance is wide enough
        /// that no two leaves match. Uniform height is what a lawn is.
        /// </para>
        /// </summary>
        private static BuiltMesh BuildWeedBuffer(WeedParams p, int seed)
        {
            var rng = new FoliageRandom(seed);
            var buffer = new FoliageMeshBuffer();

            var leafShape = new BladeShape
            {
                Segments = p.segments,
                Taper = p.taper,
                NormalUpBlend = p.normalUpBlend,
                RootOcclusion = p.rootOcclusion,
                Stiffness = p.stiffness,
                Bulge = 1f,
            };

            int leafCount = Mathf.Max(1, p.leafCount);
            float tallest = 0f;

            for (int i = 0; i < leafCount; i++)
            {
                // Fanned rather than scattered: a rosette radiates from one
                // crown, and evenly spaced angles with a little jitter is what
                // that looks like from above.
                float angle = i / (float)leafCount * Mathf.PI * 2f + rng.Range(-0.35f, 0.35f);

                float distance = Mathf.Sqrt(rng.Value01()) * p.clumpRadius;
                var root = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);

                var bendDir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                float height = p.height * rng.Range(1f - p.heightVariance, 1f + p.heightVariance);
                float width = p.width * rng.Range(1f - p.widthVariance, 1f + p.widthVariance);
                float bend = p.bend * height * rng.Range(0.7f, 1.3f);
                float elementSeed = rng.Value01();

                Color rootColor = JitterColor(p.rootColor, ref rng, p.perLeafTintJitter);
                Color tipColor = JitterColor(p.tipColor, ref rng, p.perLeafTintJitter);

                AddBlade(buffer, leafShape, root, bendDir, height, width, bend, elementSeed, rootColor, tipColor);
                tallest = Mathf.Max(tallest, height);
            }

            // The shoots are what give a weed its height. Narrow, barely bent,
            // and built from the same blade code with the grass profile, since
            // a flowering stem does taper from the base.
            var shootShape = new BladeShape
            {
                Segments = Mathf.Max(2, p.segments),
                Taper = 1.4f,
                NormalUpBlend = 0.5f,
                RootOcclusion = p.rootOcclusion,
                Stiffness = p.stiffness,
            };

            int shootCount = Mathf.Max(0, p.shootCount);
            for (int i = 0; i < shootCount; i++)
            {
                float angle = rng.Range(0f, Mathf.PI * 2f);
                float distance = Mathf.Sqrt(rng.Value01()) * (p.clumpRadius * 0.5f);
                var root = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);

                float facing = angle + rng.Range(-1.2f, 1.2f);
                var bendDir = new Vector3(Mathf.Cos(facing), 0f, Mathf.Sin(facing));

                float height = p.shootHeight * rng.Range(0.75f, 1.25f);
                float bend = height * rng.Range(0.08f, 0.28f);
                float elementSeed = rng.Value01();

                Color shoot = JitterColor(p.shootColor, ref rng, p.perLeafTintJitter);

                AddBlade(buffer, shootShape, root, bendDir, height, p.shootWidth, bend, elementSeed, shoot, shoot);
                tallest = Mathf.Max(tallest, height);
            }

            return new BuiltMesh
            {
                Buffer = buffer,
                Name = "SabaFoliage_Weed",
                BoundsPadding = tallest * 0.35f,
            };
        }

        // ------------------------------------------------------------------

        private static Color JitterColor(Color color, ref FoliageRandom rng, float amount)
        {
            if (amount <= 0f)
            {
                return color;
            }

            float scale = Mathf.Max(0f, 1f + rng.Signed() * amount);
            return new Color(
                Mathf.Clamp01(color.r * scale),
                Mathf.Clamp01(color.g * scale),
                Mathf.Clamp01(color.b * scale),
                color.a);
        }
    }
}
