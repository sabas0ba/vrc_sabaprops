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
        foreach (FoliageSpeciesKind kind in new[] { FoliageSpeciesKind.Clover, FoliageSpeciesKind.Sunflower })
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
    };

    private static Mesh Build(FoliageSpeciesKind kind, int seed) =>
        FoliageMeshBuilder.Build(new FoliageSpecies { kind = kind, meshSeed = seed });

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
