// Structural checks on foliage mesh generation, executed without Unity.
//
// The rest of the offline harness only compiles the package. This runs it: the
// same generators Unity would call, against the shim in UnityEngineShim.cs, so
// every pull request exercises the code that actually produces geometry rather
// than merely proving it parses.
//
// What this can assert: topology, finiteness, determinism, channel invariants,
// budgets, and the wind-joint rules that stop parts of a plant coming apart.
//
// What it cannot: anything that depends on Unity's exact maths, on the asset
// database, or on the renderer. Those stay with the EditMode tests, which need
// a real editor.
using System;
using System.Collections.Generic;
using SabaProps.Foliage;
using SabaProps.Foliage.Editors;
using UnityEngine;

internal static class OfflineMeshTests
{
    private static int _failures;
    private static string _current = "-";

    private static int Main()
    {
        Run("FoliageRandom is deterministic and in range", RandomIsDeterministic);
        Run("FoliageRandom diverges for neighbouring seeds", NeighbouringSeedsDiverge);

        Run("every species builds a well-formed mesh", EverySpeciesIsWellFormed);
        Run("every species is deterministic for a seed", EverySpeciesIsDeterministic);
        Run("mesh seeds actually change the geometry", SeedsChangeGeometry);

        Run("grass topology follows its parameters", GrassTopologyMatchesParameters);
        Run("grass stands tall enough to read as grass", GrassIsTallEnough);

        Run("one plant sways with one wind phase", SinglePlantsShareOneWindPhase);
        Run("the sunflower head does not tear", SunflowerHeadDoesNotTear);

        Run("summer leaves a species exactly as authored", SummerIsIdentity);
        Run("a season leaves the wind joints intact", SeasonsKeepTheWindJointsIntact);
        Run("a dry season stiffens the plant and bends it over", DrySeasonsStiffenAndBendThePlant);
        Run("every season stays well-formed", EverySeasonIsWellFormed);
        Run("autumn warms and winter drains the colour", SeasonsMoveInTheRightDirection);
        Run("a season keeps the root-to-tip gradient", SeasonsPreserveTheGradient);
        Run("season weight holds a colour back", SeasonWeightHoldsColourBack);
        Run("a dormant flower drops its petals", DormantFlowersDropTheirPetals);
        Run("the small flower carries its flowers", SmallFlowerCarriesItsFlowers);
        Run("weed leaves are broad and uneven", WeedLeavesAreBroadAndUneven);

        Run("degenerate parameters stay finite", DegenerateParametersStayFinite);
        Run("merging preserves counts and moves the wind pivots", MergePreservesChannels);

        if (_failures > 0)
        {
            Console.Error.WriteLine($"\n{_failures} offline check(s) failed");
            return 1;
        }

        Console.WriteLine("\nall offline checks passed");
        return 0;
    }

    // ----------------------------------------------------------------------
    // Checks
    // ----------------------------------------------------------------------

    private static void RandomIsDeterministic()
    {
        var a = new FoliageRandom(20260823);
        var b = new FoliageRandom(20260823);

        for (int i = 0; i < 4096; i++)
        {
            float x = a.Value01();
            float y = b.Value01();

            Require(x == y, $"same seed diverged at draw {i}: {x} != {y}");
            Require(x >= 0f && x < 1f, $"Value01 left [0,1) at draw {i}: {x}");
        }

        var ints = new FoliageRandom(7);
        for (int i = 0; i < 1024; i++)
        {
            int v = ints.RangeInt(3, 9);
            Require(v >= 3 && v < 9, $"RangeInt left [3,9): {v}");
        }
    }

    private static void NeighbouringSeedsDiverge()
    {
        // Nearby seeds sharing a first value would make neighbouring fields
        // start identically, which is exactly what the seed mixing prevents.
        var seen = new Dictionary<float, int>();

        for (int seed = 0; seed < 512; seed++)
        {
            var rng = new FoliageRandom(seed);
            float first = rng.Value01();

            Require(!seen.ContainsKey(first),
                $"seeds {seen.GetValueOrDefault(first)} and {seed} start with the same value {first}");

            seen[first] = seed;
        }
    }

    private static void EverySpeciesIsWellFormed()
    {
        foreach (FoliageSpeciesKind kind in AllKinds())
        {
            Mesh mesh = Build(kind, 1);
            AssertWellFormed(mesh, kind.ToString());

            int triangles = mesh.triangles.Length / 3;
            Require(triangles < 400, $"{kind} costs {triangles} triangles; mass placement needs it cheap");
        }
    }

    private static void EverySpeciesIsDeterministic()
    {
        foreach (FoliageSpeciesKind kind in AllKinds())
        {
            Mesh first = Build(kind, 4242);
            Mesh second = Build(kind, 4242);

            Require(first.vertexCount == second.vertexCount,
                $"{kind}: vertex count differs between two builds with the same seed");

            for (int i = 0; i < first.vertexCount; i++)
            {
                Vector3 a = first.vertices[i];
                Vector3 b = second.vertices[i];

                Require(a.x == b.x && a.y == b.y && a.z == b.z,
                    $"{kind}: vertex {i} differs between two builds with the same seed");
            }
        }
    }

    private static void SeedsChangeGeometry()
    {
        // A seed that changed nothing would make every clump in a field
        // identical, which the placement jitter would not hide.
        foreach (FoliageSpeciesKind kind in AllKinds())
        {
            Mesh a = Build(kind, 1);
            Mesh b = Build(kind, 2);

            bool differs = a.vertexCount != b.vertexCount;
            for (int i = 0; !differs && i < a.vertexCount; i++)
            {
                differs = a.vertices[i].sqrMagnitude != b.vertices[i].sqrMagnitude;
            }

            Require(differs, $"{kind}: two different mesh seeds produced identical geometry");
        }
    }

    private static void GrassTopologyMatchesParameters()
    {
        var species = new FoliageSpecies { kind = FoliageSpeciesKind.GrassClump, meshSeed = 1 };
        GrassParams p = species.grass;

        Mesh mesh = FoliageMeshBuilder.Build(species);

        int expectedVertices = p.bladeCount * (p.segments * 2 + 1);
        int expectedTriangles = p.bladeCount * ((p.segments - 1) * 2 + 1);

        Require(mesh.vertexCount == expectedVertices,
            $"grass vertex count {mesh.vertexCount} != {expectedVertices}");
        Require(mesh.triangles.Length / 3 == expectedTriangles,
            $"grass triangle count {mesh.triangles.Length / 3} != {expectedTriangles}");
    }

    private static void GrassIsTallEnough()
    {
        Mesh mesh = Build(FoliageSpeciesKind.GrassClump, 1);

        float tallest = 0f;
        foreach (Vector3 v in mesh.vertices)
        {
            tallest = Mathf.Max(tallest, v.y);
        }

        // Bend only displaces a blade sideways, so the tallest vertex is the
        // tallest blade. Ankle-high grass reads as moss from standing height.
        Require(tallest > 0.4f, $"default grass is only {tallest:0.00} m tall");
        Require(tallest < 1.5f, $"default grass is {tallest:0.00} m tall");
    }

    private static void SinglePlantsShareOneWindPhase()
    {
        // A clump is separate blades that may sway out of step. A clover or a
        // sunflower is one plant, and parts of one plant that move out of step
        // come apart at the joints.
        foreach (FoliageSpeciesKind kind in new[] { FoliageSpeciesKind.Clover, FoliageSpeciesKind.Sunflower, FoliageSpeciesKind.SmallFlower })
        {
            Mesh mesh = Build(kind, 3);

            var phases = new HashSet<float>();
            foreach (Color c in mesh.colors)
            {
                phases.Add(c.a);
            }

            Require(phases.Count == 1, $"{kind} sways with {phases.Count} wind phases; it must use one");
        }
    }

    private static void SunflowerHeadDoesNotTear()
    {
        var species = new FoliageSpecies { kind = FoliageSpeciesKind.Sunflower, meshSeed = 7 };
        Mesh mesh = FoliageMeshBuilder.Build(species);

        var uv0 = new List<Vector2>();
        var uv3 = new List<Vector4>();
        mesh.GetUVs(0, uv0);
        mesh.GetUVs(3, uv3);

        float top = 0f;
        foreach (Vector3 v in mesh.vertices)
        {
            top = Mathf.Max(top, v.y);
        }

        float headFloor = top - (species.sunflower.headRadius + species.sunflower.petalLength);

        // Mirrors the shader's default _BendPower. Used only to compare sway
        // amplitudes against each other.
        const float bendPower = 2.2f;

        float weakest = float.MaxValue;
        float strongest = 0f;
        int counted = 0;

        for (int i = 0; i < mesh.vertexCount; i++)
        {
            if (mesh.vertices[i].y < headFloor)
            {
                continue;
            }

            float bend = Mathf.Pow(Mathf.Clamp01(uv0[i].y), bendPower) * uv3[i].w;
            weakest = Mathf.Min(weakest, bend);
            strongest = Mathf.Max(strongest, bend);
            counted++;
        }

        Require(counted > 0, "found no sunflower head vertices to check");
        Require(weakest > 0f, "part of the sunflower head does not move with the wind at all");
        Require(strongest / weakest < 1.3f,
            $"the head stretches {strongest / weakest:0.00}x; the petals will tear off the disc");
    }

    private static void SummerIsIdentity()
    {
        // Summer is the season a species is authored in. An asset saved before
        // seasons existed deserialises to it, and must generate the mesh it
        // always did -- byte for byte, not merely close.
        foreach (FoliageSpeciesKind kind in AllKinds())
        {
            Mesh plain = Build(kind, 9);
            Mesh summer = Build(kind, 9, FoliageSeason.Summer);

            Require(plain.vertexCount == summer.vertexCount, $"{kind}: summer changed the vertex count");

            for (int i = 0; i < plain.vertexCount; i++)
            {
                Color a = plain.colors[i];
                Color b = summer.colors[i];

                Require(a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a,
                    $"{kind}: summer altered vertex colour {i}");
            }
        }
    }

    private static void SeasonsKeepTheWindJointsIntact()
    {
        // A season may recolour a plant, stiffen it and bend it over. What it
        // must never do is disturb the relationships the wind reads: parts of
        // one plant that stop agreeing about phase or stiffness come apart at
        // the joints, and none of it is visible until the wind blows.
        foreach (FoliageSpeciesKind kind in AllKinds())
        {
            Mesh summer = Build(kind, 9, FoliageSeason.Summer);

            var summerUv3 = new List<Vector4>();
            summer.GetUVs(3, summerUv3);

            foreach (FoliageSeason season in AllSeasons())
            {
                if (season == FoliageSeason.Summer)
                {
                    continue;
                }

                Mesh mesh = Build(kind, 9, season);

                var uv3 = new List<Vector4>();
                mesh.GetUVs(3, uv3);

                Require(mesh.vertexCount == summer.vertexCount,
                    $"{kind}/{season}: the season changed the vertex count");

                bool anyColourMoved = false;
                float stiffnessRatio = float.NaN;

                for (int i = 0; i < mesh.vertexCount; i++)
                {
                    Require(summer.colors[i].a == mesh.colors[i].a,
                        $"{kind}/{season}: the season changed the wind phase of vertex {i}");

                    // Stiffness may fall, but only by one factor shared across
                    // the whole plant. Anything else rewrites which parts move
                    // further than which.
                    if (summerUv3[i].w > 1e-4f)
                    {
                        float ratio = uv3[i].w / summerUv3[i].w;

                        if (float.IsNaN(stiffnessRatio))
                        {
                            stiffnessRatio = ratio;
                        }

                        Require(Math.Abs(ratio - stiffnessRatio) < 1e-3f,
                            $"{kind}/{season}: vertex {i} was stiffened by {ratio:0.000}, "
                            + $"the rest of the plant by {stiffnessRatio:0.000}");
                    }

                    // Bending is a rotation about the root, so a vertex may move
                    // but must not travel towards or away from it: a stem that
                    // stretched as it dried would be a stem made of rubber.
                    float before = summer.vertices[i].magnitude;
                    float after = mesh.vertices[i].magnitude;

                    Require(Math.Abs(after - before) < 1e-3f,
                        $"{kind}/{season}: vertex {i} changed its distance from the root "
                        + $"({before:0.0000} to {after:0.0000})");

                    anyColourMoved |= Distance(summer.colors[i], mesh.colors[i]) > 1e-3f;
                }

                Require(anyColourMoved, $"{kind}/{season}: the season changed nothing at all");
            }
        }
    }

    private static void DrySeasonsStiffenAndBendThePlant()
    {
        // What "カラカラ" has to mean geometrically: a plant that has lost its
        // water bends less in the wind and hangs further over.
        Mesh summer = Build(FoliageSpeciesKind.Reed, 5, FoliageSeason.Summer);
        Mesh autumn = Build(FoliageSpeciesKind.Reed, 5, FoliageSeason.Autumn);

        var summerUv3 = new List<Vector4>();
        var autumnUv3 = new List<Vector4>();
        summer.GetUVs(3, summerUv3);
        autumn.GetUVs(3, autumnUv3);

        float stiffest = 0f;
        for (int i = 0; i < autumnUv3.Count; i++)
        {
            stiffest = Mathf.Max(stiffest, autumnUv3[i].w);
        }

        float summerStiffest = 0f;
        for (int i = 0; i < summerUv3.Count; i++)
        {
            summerStiffest = Mathf.Max(summerStiffest, summerUv3[i].w);
        }

        Require(stiffest < summerStiffest * 0.9f,
            $"autumn barely stiffened the reed ({stiffest:0.000} against {summerStiffest:0.000})");

        // The tip is what moves. Measured on the vertex that was highest in
        // summer, so the two meshes are compared at the same point of the plant.
        int tip = 0;
        for (int i = 1; i < summer.vertexCount; i++)
        {
            if (summer.vertices[i].y > summer.vertices[tip].y)
            {
                tip = i;
            }
        }

        Require(autumn.vertices[tip].z > summer.vertices[tip].z + 0.02f,
            "autumn did not lean the reed over");
        Require(autumn.vertices[tip].y < summer.vertices[tip].y - 0.02f,
            "autumn leaned the reed over without lowering its tip");
    }

    private static void EverySeasonIsWellFormed()
    {
        foreach (FoliageSpeciesKind kind in AllKinds())
        {
            foreach (FoliageSeason season in AllSeasons())
            {
                Mesh mesh = Build(kind, 3, season);
                AssertWellFormed(mesh, $"{kind}/{season}");

                foreach (Color c in mesh.colors)
                {
                    Require(c.r >= 0f && c.r <= 1f && c.g >= 0f && c.g <= 1f && c.b >= 0f && c.b <= 1f,
                        $"{kind}/{season}: vertex colour left [0,1]");
                }
            }
        }
    }

    private static void SeasonsMoveInTheRightDirection()
    {
        Mesh summer = Build(FoliageSpeciesKind.GrassClump, 3, FoliageSeason.Summer);
        Mesh autumn = Build(FoliageSpeciesKind.GrassClump, 3, FoliageSeason.Autumn);
        Mesh winter = Build(FoliageSpeciesKind.GrassClump, 3, FoliageSeason.WinterSnow);

        // Warmth as red minus blue, which needs no hue conversion to read.
        float summerWarmth = MeanWarmth(summer);
        float autumnWarmth = MeanWarmth(autumn);

        Require(autumnWarmth > summerWarmth + 0.05f,
            $"autumn is no warmer than summer ({autumnWarmth:0.000} vs {summerWarmth:0.000})");

        float summerSaturation = MeanSaturation(summer);
        float winterSaturation = MeanSaturation(winter);

        Require(winterSaturation < summerSaturation * 0.75f,
            $"winter is barely less saturated than summer ({winterSaturation:0.000} vs {summerSaturation:0.000})");
    }

    private static void SeasonsPreserveTheGradient()
    {
        // Generators bake a root-to-tip brightness gradient into their colours,
        // and that gradient is what reads as shape. The season pass scales
        // brightness rather than interpolating it precisely so the gradient
        // survives; interpolation would flatten the plant into a silhouette.
        //
        // Grass alone, because every one of its vertices carries the same season
        // weight. Where the weight varies, so does the multiplier, and the
        // ordering across the two groups is allowed to change.
        Mesh summer = Build(FoliageSpeciesKind.GrassClump, 3, FoliageSeason.Summer);
        Mesh winter = Build(FoliageSpeciesKind.GrassClump, 3, FoliageSeason.WinterSnow);

        for (int i = 0; i < summer.vertexCount; i++)
        {
            for (int j = i + 1; j < summer.vertexCount; j++)
            {
                float before = Brightness(summer.colors[i]) - Brightness(summer.colors[j]);
                if (Math.Abs(before) < 0.02f)
                {
                    continue;
                }

                float after = Brightness(winter.colors[i]) - Brightness(winter.colors[j]);

                Require(before * after >= 0f,
                    $"winter inverted the brightness of vertices {i} and {j} "
                    + $"({before:0.000} became {after:0.000})");
            }
        }
    }

    private static void SeasonWeightHoldsColourBack()
    {
        // What keeps a sunflower's petals yellow while the leaves around them
        // turn. Without it the flower bleaches to straw and stops being one.
        var tint = new SeasonPalette().winterSnow;
        var petal = new Color(0.902f, 0.596f, 0.086f, 0.5f);

        Color full = FoliageSeasonPass.Apply(petal, tint, 1f, 1f);
        Color held = FoliageSeasonPass.Apply(petal, tint, 1f, 0.3f);
        Color none = FoliageSeasonPass.Apply(petal, tint, 1f, 0f);

        Require(Distance(none, petal) < 1e-6f, "a weight of zero still changed the colour");
        Require(Distance(held, petal) < Distance(full, petal),
            "a held-back vertex moved as far as a fully exposed one");
        Require(Distance(held, petal) > 1e-4f, "a held-back vertex did not move at all");

        Require(full.a == petal.a && held.a == petal.a, "the wind phase did not survive the recolour");
    }

    private static void DormantFlowersDropTheirPetals()
    {
        // Recolouring a flower in full bloom to straw produces a thing that
        // does not exist. A sunflower in winter is a seed head on a dry stalk,
        // which means the petals have to actually not be built.
        Mesh bloom = Build(FoliageSpeciesKind.Sunflower, 7);

        var dormant = new FoliageSpecies
        {
            kind = FoliageSpeciesKind.Sunflower,
            meshSeed = 7,
            season = FoliageSeason.WinterSnow,
        };
        dormant.seasonPalette.winterSnow.appearance = SeasonAppearance.Dormant;

        Mesh spent = FoliageMeshBuilder.Build(dormant);
        AssertWellFormed(spent, "dormant sunflower");

        // Each petal is a quad and a tip triangle. Everything else -- stem,
        // leaves, seed head -- has to survive, so the difference is exact.
        int petals = Math.Max(4, dormant.sunflower.petalCount);
        int dropped = bloom.triangles.Length / 3 - spent.triangles.Length / 3;

        Require(dropped == petals * 3,
            $"a dormant sunflower dropped {dropped} triangles, expected {petals * 3} (the petals)");

        // The appearance is what gates this, not the season: a species told to
        // stay in bloom through the winter keeps its petals and only recolours.
        var blooming = new FoliageSpecies
        {
            kind = FoliageSpeciesKind.Sunflower,
            meshSeed = 7,
            season = FoliageSeason.WinterSnow,
        };
        blooming.seasonPalette.winterSnow.appearance = SeasonAppearance.Full;

        Require(FoliageMeshBuilder.Build(blooming).triangles.Length == bloom.triangles.Length,
            "a winter sunflower left as Full lost geometry anyway");
    }

    private static void SmallFlowerCarriesItsFlowers()
    {
        // The species exists to fill a field with flowers, so "it has flowers"
        // is the property worth pinning down rather than a vertex count.
        var species = new FoliageSpecies { kind = FoliageSpeciesKind.SmallFlower, meshSeed = 31 };
        SmallFlowerParams p = species.smallFlower;

        Mesh mesh = FoliageMeshBuilder.Build(species);
        AssertWellFormed(mesh, "small flower");

        // Petal colours are what a flower is recognised by, and they are the
        // one thing in this mesh that is not some shade of green.
        int petalish = 0;
        foreach (Color c in mesh.colors)
        {
            if (c.b > c.g)
            {
                petalish++;
            }
        }

        Require(petalish > 0, "the small flower grew no petals in its petal colours");

        // Several flowers per plant is what stops a field of these reading as a
        // grid of single dots.
        var species2 = new FoliageSpecies { kind = FoliageSpeciesKind.SmallFlower, meshSeed = 31 };
        species2.smallFlower.flowerCount = 1;

        Mesh single = FoliageMeshBuilder.Build(species2);

        Require(mesh.triangles.Length > single.triangles.Length,
            $"flowerCount {p.flowerCount} produced no more geometry than a single flower");

        // Dormant is the same plant with the flowers left out: stem and leaves
        // survive, so it is still a plant rather than nothing.
        var dormant = new FoliageSpecies
        {
            kind = FoliageSpeciesKind.SmallFlower,
            meshSeed = 31,
            season = FoliageSeason.Autumn,
        };
        dormant.seasonPalette.autumn.appearance = SeasonAppearance.Dormant;

        Mesh spent = FoliageMeshBuilder.Build(dormant);
        AssertWellFormed(spent, "dormant small flower");

        Require(spent.triangles.Length < mesh.triangles.Length,
            "a dormant small flower kept its flowers");

        foreach (Color c in spent.colors)
        {
            Require(c.b <= c.g + 1e-4f,
                "a dormant small flower still carries a petal colour");
        }
    }

    private static void WeedLeavesAreBroadAndUneven()
    {
        // The two properties that separate a weed from coarse grass, asserted
        // rather than eyeballed, because both are easy to lose to a tweak of
        // the shared blade code.
        Mesh weed = Build(FoliageSpeciesKind.Weed, 44);
        Mesh grass = Build(FoliageSpeciesKind.GrassClump, 44);

        Require(WidestSpan(weed) > WidestSpan(grass),
            $"weed leaves ({WidestSpan(weed):0.000} m) are no broader than grass blades "
            + $"({WidestSpan(grass):0.000} m)");

        // Uneven length is the other half. Measured as the spread of how far
        // each vertex reaches from the crown, which needs no knowledge of which
        // vertices belong to which leaf.
        Require(ReachSpread(weed) > ReachSpread(grass),
            "weed leaves come out as even in length as grass blades");
    }

    /// <summary>
    /// Widest gap between any two vertices that share a height band. Stands in
    /// for "how broad is a leaf" without the mesh saying where its leaves are.
    /// </summary>
    private static float WidestSpan(Mesh mesh)
    {
        var uv0 = new List<Vector2>();
        mesh.GetUVs(0, uv0);

        float widest = 0f;

        for (int i = 0; i < mesh.vertexCount; i++)
        {
            for (int j = i + 1; j < mesh.vertexCount; j++)
            {
                // Same point along a blade, and close enough together to be the
                // two sides of one -- not two different leaves.
                if (Math.Abs(uv0[i].y - uv0[j].y) > 1e-4f)
                {
                    continue;
                }

                float span = (mesh.vertices[i] - mesh.vertices[j]).magnitude;
                if (span < 0.2f)
                {
                    widest = Mathf.Max(widest, span);
                }
            }
        }

        return widest;
    }

    /// <summary>Difference between the furthest and the nearest vertex reach.</summary>
    private static float ReachSpread(Mesh mesh)
    {
        float nearest = float.MaxValue;
        float furthest = 0f;

        var uv0 = new List<Vector2>();
        mesh.GetUVs(0, uv0);

        for (int i = 0; i < mesh.vertexCount; i++)
        {
            // Tips only: the bases all sit on the crown whatever the species.
            if (uv0[i].y < 0.99f)
            {
                continue;
            }

            float reach = mesh.vertices[i].magnitude;
            nearest = Mathf.Min(nearest, reach);
            furthest = Mathf.Max(furthest, reach);
        }

        return nearest < float.MaxValue ? furthest - nearest : 0f;
    }

    private static void DegenerateParametersStayFinite()
    {
        // Values a user can dial in from the inspector that collapse a basis
        // vector. These used to be the shortest path to a NaN.
        var grass = new FoliageSpecies { kind = FoliageSpeciesKind.GrassClump, meshSeed = 5 };
        grass.grass.clumpRadius = 0f;
        grass.grass.bend = 0f;
        grass.grass.widthVariance = 0f;
        grass.grass.heightVariance = 0f;
        AssertWellFormed(FoliageMeshBuilder.Build(grass), "grass with a collapsed clump");

        var sunflower = new FoliageSpecies { kind = FoliageSpeciesKind.Sunflower, meshSeed = 5 };
        sunflower.sunflower.lean = 0f;
        sunflower.sunflower.headTilt = 0f;
        sunflower.sunflower.leafCount = 0;
        sunflower.sunflower.petalCurl = 0f;
        AssertWellFormed(FoliageMeshBuilder.Build(sunflower), "upright sunflower");

        var clover = new FoliageSpecies { kind = FoliageSpeciesKind.Clover, meshSeed = 5 };
        clover.clover.notch = 0f;
        clover.clover.leafDroop = 0f;
        clover.clover.heightVariance = 0f;
        AssertWellFormed(FoliageMeshBuilder.Build(clover), "clover with no notch");

        var reed = new FoliageSpecies { kind = FoliageSpeciesKind.Reed, meshSeed = 5 };
        reed.reed.spread = 0f;
        reed.reed.clumpRadius = 0f;
        reed.reed.spike = false;
        reed.reed.bladeCount = 1;
        AssertWellFormed(FoliageMeshBuilder.Build(reed), "single upright reed");
    }

    private static void MergePreservesChannels()
    {
        Mesh source = Build(FoliageSpeciesKind.GrassClump, 11);
        FoliageSourceMesh snapshot = FoliageSourceMesh.From(source);

        var buffer = new FoliageMeshBuffer();
        var offset = new Vector3(3f, 0f, -2f);

        buffer.Append(snapshot, Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one));
        buffer.Append(snapshot, Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one));

        Require(buffer.VertexCount == source.vertexCount * 2,
            $"merging two copies gave {buffer.VertexCount} vertices, expected {source.vertexCount * 2}");
        Require(buffer.TriangleCount == source.triangles.Length / 3 * 2,
            "merging two copies did not double the triangle count");

        foreach (int index in buffer.Triangles)
        {
            Require(index >= 0 && index < buffer.VertexCount,
                $"merged triangle index {index} is out of range");
        }

        // The shader sways around the root stored in UV3, so the merge has to
        // carry those pivots with the instance. Leaving them behind is invisible
        // until the wind blows.
        int half = source.vertexCount;
        for (int i = 0; i < half; i++)
        {
            Vector4 a = buffer.Uv3[i];
            Vector4 b = buffer.Uv3[half + i];

            Require(Math.Abs(b.x - (a.x + offset.x)) < 1e-3f
                    && Math.Abs(b.z - (a.z + offset.z)) < 1e-3f,
                $"merged wind pivot {i} did not follow the instance transform");

            Require(Math.Abs(b.w - a.w) < 1e-6f, $"merged stiffness {i} changed");
        }
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    private static FoliageSpeciesKind[] AllKinds() => new[]
    {
        FoliageSpeciesKind.GrassClump,
        FoliageSpeciesKind.Clover,
        FoliageSpeciesKind.Sunflower,
        FoliageSpeciesKind.Reed,
        FoliageSpeciesKind.SmallFlower,
        FoliageSpeciesKind.Weed,
    };

    private static FoliageSeason[] AllSeasons() => new[]
    {
        FoliageSeason.Spring,
        FoliageSeason.Summer,
        FoliageSeason.Autumn,
        FoliageSeason.WinterSnow,
        FoliageSeason.WinterBare,
    };

    private static Mesh Build(FoliageSpeciesKind kind, int seed) =>
        FoliageMeshBuilder.Build(new FoliageSpecies { kind = kind, meshSeed = seed });

    private static Mesh Build(FoliageSpeciesKind kind, int seed, FoliageSeason season) =>
        FoliageMeshBuilder.Build(
            new FoliageSpecies { kind = kind, meshSeed = seed, season = season });

    private static float Brightness(Color c) => Mathf.Max(c.r, Mathf.Max(c.g, c.b));

    private static float Distance(Color a, Color b) =>
        Math.Abs(a.r - b.r) + Math.Abs(a.g - b.g) + Math.Abs(a.b - b.b);

    private static float MeanWarmth(Mesh mesh)
    {
        float sum = 0f;
        foreach (Color c in mesh.colors)
        {
            sum += c.r - c.b;
        }

        return mesh.vertexCount > 0 ? sum / mesh.vertexCount : 0f;
    }

    private static float MeanSaturation(Mesh mesh)
    {
        float sum = 0f;
        foreach (Color c in mesh.colors)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            sum += max > 0f ? (max - min) / max : 0f;
        }

        return mesh.vertexCount > 0 ? sum / mesh.vertexCount : 0f;
    }

    private static void AssertWellFormed(Mesh mesh, string label)
    {
        Require(mesh != null, $"{label}: mesh is null");
        Require(mesh.vertexCount > 0, $"{label}: no vertices");
        Require(mesh.triangles.Length > 0, $"{label}: no triangles");
        Require(mesh.triangles.Length % 3 == 0, $"{label}: index count is not a multiple of three");

        foreach (Vector3 p in mesh.vertices)
        {
            Require(IsFinite(p.x) && IsFinite(p.y) && IsFinite(p.z), $"{label}: non-finite vertex position");
        }

        Require(mesh.normals.Length == mesh.vertexCount, $"{label}: normals are missing");
        foreach (Vector3 n in mesh.normals)
        {
            Require(Math.Abs(n.magnitude - 1f) < 1e-3f, $"{label}: normal is not unit length ({n.magnitude})");
        }

        Require(mesh.colors.Length == mesh.vertexCount, $"{label}: vertex colours are missing");

        var uv0 = new List<Vector2>();
        var uv3 = new List<Vector4>();
        mesh.GetUVs(0, uv0);
        mesh.GetUVs(3, uv3);

        Require(uv0.Count == mesh.vertexCount, $"{label}: UV0 channel is missing");
        Require(uv3.Count == mesh.vertexCount, $"{label}: UV3 channel is missing");

        foreach (Vector2 uv in uv0)
        {
            Require(uv.y >= -1e-4f && uv.y <= 1f + 1e-4f, $"{label}: bend mask {uv.y} is outside [0,1]");
        }

        foreach (Vector4 uv in uv3)
        {
            Require(IsFinite(uv.x) && IsFinite(uv.y) && IsFinite(uv.z), $"{label}: non-finite wind pivot");
            Require(uv.w >= 0f && uv.w <= 1f, $"{label}: stiffness {uv.w} is outside [0,1]");
        }

        for (int i = 0; i < mesh.triangles.Length; i += 3)
        {
            int a = mesh.triangles[i];
            int b = mesh.triangles[i + 1];
            int c = mesh.triangles[i + 2];

            Require(a >= 0 && a < mesh.vertexCount && b >= 0 && b < mesh.vertexCount
                    && c >= 0 && c < mesh.vertexCount,
                $"{label}: triangle index out of range");

            Require(a != b && b != c && a != c, $"{label}: degenerate triangle {a}/{b}/{c}");

            Vector3 area = Vector3.Cross(
                mesh.vertices[b] - mesh.vertices[a],
                mesh.vertices[c] - mesh.vertices[a]);

            Require(area.magnitude > 1e-9f, $"{label}: zero-area triangle {a}/{b}/{c}");
        }

        // Bounds are padded for wind, so they must cover the raw geometry.
        Bounds bounds = mesh.bounds;
        Require(bounds.size.y > 0f, $"{label}: degenerate bounds");
    }

    private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

    private static void Run(string name, Action check)
    {
        _current = name;

        try
        {
            check();
            Console.WriteLine($"  ok   {name}");
        }
        catch (CheckFailed failure)
        {
            _failures++;
            Console.WriteLine($"  FAIL {name}");
            Console.WriteLine($"       {failure.Message}");
        }
        catch (Exception error)
        {
            _failures++;
            Console.WriteLine($"  FAIL {name}");
            Console.WriteLine($"       threw {error.GetType().Name}: {error.Message}");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new CheckFailed(message);
        }
    }

    private sealed class CheckFailed : Exception
    {
        public CheckFailed(string message) : base(message) { }
    }
}

internal static class DictionaryExtensions
{
    public static TValue GetValueOrDefault<TKey, TValue>(
        this Dictionary<TKey, TValue> source, TKey key)
    {
        return source.TryGetValue(key, out TValue value) ? value : default;
    }
}
