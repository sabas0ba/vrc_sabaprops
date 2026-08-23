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

            switch (species.kind)
            {
                case FoliageSpeciesKind.Sunflower:
                    return BuildSunflower(species.sunflower, species.meshSeed);

                case FoliageSpeciesKind.GrassClump:
                default:
                    return BuildGrassClump(species.grass, species.meshSeed);
            }
        }

        // ------------------------------------------------------------------
        // Grass
        // ------------------------------------------------------------------

        public static Mesh BuildGrassClump(GrassParams p, int seed)
        {
            var rng = new FoliageRandom(seed);
            var buffer = new FoliageMeshBuffer();

            int bladeCount = Mathf.Max(1, p.bladeCount);
            float tallest = 0f;

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

                AddBlade(buffer, p, root, bendDir, height, width, bend, elementSeed, rootColor, tipColor);
                tallest = Mathf.Max(tallest, height);
            }

            return buffer.ToMesh("SabaFoliage_GrassClump", tallest * 0.35f);
        }

        private static void AddBlade(
            FoliageMeshBuffer buffer, GrassParams p,
            Vector3 root, Vector3 bendDir,
            float height, float width, float bend,
            float elementSeed, Color rootColor, Color tipColor)
        {
            var side = new Vector3(-bendDir.z, 0f, bendDir.x);
            int segments = Mathf.Max(1, p.segments);
            var rootData = new Vector4(root.x, root.y, root.z, p.stiffness);

            int previousLeft = -1;
            int previousRight = -1;

            for (int s = 0; s < segments; s++)
            {
                float t = s / (float)segments;
                Vector3 center = BladePoint(root, bendDir, height, bend, t);
                Vector3 normal = BladeNormal(p, side, bendDir, height, bend, t);

                float halfWidth = width * 0.5f * Mathf.Pow(1f - t, p.taper);
                Color color = ShadeBlade(rootColor, tipColor, p.rootOcclusion, t, elementSeed);

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
            Color tipShaded = ShadeBlade(rootColor, tipColor, p.rootOcclusion, 1f, elementSeed);

            int tip = buffer.AddVertex(tipPoint, tipNormal, tipShaded, new Vector2(0.5f, 1f), rootData);
            buffer.AddTriangle(previousLeft, previousRight, tip);
        }

        private static Vector3 BladePoint(Vector3 root, Vector3 bendDir, float height, float bend, float t)
        {
            // Quadratic arc: vertical near the root, leaning hard near the tip.
            return root + Vector3.up * (height * t) + bendDir * (bend * t * t);
        }

        private static Vector3 BladeNormal(GrassParams p, Vector3 side, Vector3 bendDir, float height, float bend, float t)
        {
            Vector3 tangent = (Vector3.up * height + bendDir * (2f * bend * t)).normalized;
            Vector3 face = Vector3.Cross(side, tangent).normalized;

            // Biasing the normal upwards is the standard foliage trick: it makes
            // both faces of a two-sided blade light consistently, so we never
            // need a VFACE flip in the fragment stage.
            return Vector3.Slerp(face, Vector3.up, p.normalUpBlend).normalized;
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
            var rng = new FoliageRandom(seed);
            var buffer = new FoliageMeshBuffer();

            float height = p.height * rng.Range(1f - p.heightVariance, 1f + p.heightVariance);
            float leanAngle = rng.Range(0f, Mathf.PI * 2f);
            var leanDir = new Vector3(Mathf.Cos(leanAngle), 0f, Mathf.Sin(leanAngle));
            float lean = p.lean * rng.Range(0.6f, 1.4f);

            // One seed for the whole plant so stem, leaves and head sway together.
            float plantSeed = rng.Value01();

            AddStem(buffer, p, leanDir, height, lean, plantSeed);
            AddLeaves(buffer, p, ref rng, leanDir, height, lean, plantSeed);
            AddHead(buffer, p, ref rng, leanDir, height, lean, plantSeed);

            return buffer.ToMesh("SabaFoliage_Sunflower", height * 0.3f);
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
            Vector3 leanDir, float height, float lean, float plantSeed)
        {
            Vector3 top = StemPoint(leanDir, height, lean, 1f);

            // Tilt the disc away from vertical, towards whichever way the stem
            // leans, so the flower "looks" somewhere instead of straight up.
            Vector3 tiltAxis = Vector3.Cross(Vector3.up, leanDir);
            if (tiltAxis.sqrMagnitude < 1e-6f)
            {
                tiltAxis = Vector3.forward;
            }

            Vector3 headNormal = (Quaternion.AngleAxis(p.headTilt, tiltAxis.normalized) * Vector3.up).normalized;

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
