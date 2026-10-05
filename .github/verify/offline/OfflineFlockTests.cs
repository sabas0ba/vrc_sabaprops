// Structural checks on the flock package, executed without Unity.
//
// Runs the real body generators and swarm mesh builder against the shim in
// UnityEngineShim.cs, and the C# reference of the motion that
// SabaFlockMotion.cginc evaluates on the GPU.
//
// What this can assert: topology, finiteness, determinism, the channel
// contract the shader reads, triangle budgets per detail tier, that every
// pattern keeps every individual inside its area and its renderer bounds, and
// that headings turn smoothly.
//
// What it cannot: that the HLSL port matches the C# reference number for
// number, how the swarm looks, or anything that needs the asset database.
// Those stay with the Unity EditMode tests and a visual check in the editor.
using System;
using System.Collections.Generic;
using SabaProps.Flock;
using SabaProps.Flock.Editors;
using UnityEngine;

internal static class OfflineFlockTests
{
    private static int _failures;
    private static string _current = "-";

    private static readonly FlockDetail[] Details = { FlockDetail.Silhouette, FlockDetail.Low, FlockDetail.High };

    private static readonly FlockPattern[] Patterns =
    {
        FlockPattern.Cruise,
        FlockPattern.Murmuration,
        FlockPattern.VFormation,
        FlockPattern.Thermal,
        FlockPattern.Stream,
        FlockPattern.BaitBall,
        FlockPattern.Tornado,
        FlockPattern.Wander,
        FlockPattern.Anchored,
        FlockPattern.Jet,
        FlockPattern.Float,
        FlockPattern.FreeFlight,
        FlockPattern.FloorGlide,
    };

    private static string _csharpMotionPath;
    private static string _shaderMotionPath;
    private static string _elementsPath;

    /// <summary>
    /// Functions that exist in both FlockMotion.cs and SabaFlockMotion.cginc
    /// (with a "Flock" prefix in the shader).
    /// </summary>
    private static readonly string[] PortedFunctions =
    {
        "PathAngularSpeed", "PathPoint", "PathFrame", "UnitBallPoint", "Wobble",
        "Wander", "OrbitRate", "DriftSpeed", "Position", "Pose", "Frac", "AppendageOffset",
        "Margin", "JetClock", "DriftPoint", "FloatDrift",
        "FloorGlide",
    };

    private static int Main(string[] args)
    {
        if (args.Length >= 3)
        {
            _csharpMotionPath = args[0];
            _shaderMotionPath = args[1];
            _elementsPath = args[2];
        }

        Run("the shader motion carries the same constants as the C# reference", ShaderMatchesReference);
        Run("the species reference lists exactly the catalogue", ReferenceListsTheCatalogue);
        Run("the catalogue covers birds, sea, reef and aquarium fish", CatalogueIsBroad);
        Run("preset ids are unique and presets are sane", PresetsAreSane);
        Run("every preset builds a well-formed body at every tier", EveryBodyIsWellFormed);
        Run("detail tiers stay inside their triangle budgets", TiersStayInBudget);
        Run("higher tiers carry more detail than lower ones", TiersAreOrdered);
        Run("swarm meshes carry the full channel contract", SwarmChannelsAreComplete);
        Run("the channels decode to the builder's motion input", ChannelsMatchMotionInput);
        Run("swarm meshes are deterministic for a seed", SwarmsAreDeterministic);
        Run("large swarms switch to 32-bit indices", LargeSwarmsUse32BitIndices);
        Run("every pattern keeps every individual inside its area", PatternsStayInsideTheArea);
        Run("every preset's default swarm stays inside its bounds", DefaultSwarmsStayInsideBounds);
        Run("anchored animals stay fixed and walking birds stay on their plane", AnchoredAndGroundedMotion);
        Run("headings turn smoothly from frame to frame", HeadingsAreContinuous);
        Run("the pose frame stays orthonormal", PoseFrameIsOrthonormal);
        Run("V formations keep the leader ahead", VFormationKeepsTheLeaderAhead);
        Run("left and right wings are mirror images", WingsAreSymmetric);
        Run("wing tips and flight feathers take the detail colour", WingMarkingsAreColoured);
        Run("fish patterns reach the high tier", FishPatternsAreVisible);
        Run("degenerate settings stay finite", DegenerateSettingsStayFinite);
        Run("small aquarium fish travel and turn inside the glass", AquariumTravel);
        Run("large birds occupy broad independent flight paths", BroadBirdFlight);
        Run("jet contraction drives forward pulses", JetPulses);
        Run("jellyfish drift slowly in three dimensions and stay upright", JellyfishDrift);
        Run("manta follows broad fish paths with a lower depth preference", MantaFloorGlide);
        Run("fish heads have visible bilateral eyes and jaw and gill markings", FishHeads);
        Run("squid arms and fins use the mantle colour and octopus is absent", SquidColour);

        if (_failures > 0)
        {
            Console.Error.WriteLine($"\n{_failures} offline flock check(s) failed");
            return 1;
        }

        Console.WriteLine("\nall offline flock checks passed");
        return 0;
    }

    // ----------------------------------------------------------------------
    // Shader parity
    // ----------------------------------------------------------------------

    /// <summary>
    /// The shader cannot run here, so its port of the motion is compared with
    /// the C# reference textually: for every ported function, the numeric
    /// literals in the two bodies must form the same multiset. 0 and 1 are
    /// left out, because the languages spell unit vectors differently
    /// (Vector3.up against float3(0.0, 1.0, 0.0)). A constant edited on one
    /// side only, or a term added to one side, fails here.
    /// </summary>
    private static void ShaderMatchesReference()
    {
        if (_csharpMotionPath == null)
        {
            Fail("run with the paths of FlockMotion.cs and SabaFlockMotion.cginc");
            return;
        }

        string csharp = System.IO.File.ReadAllText(_csharpMotionPath);
        string shader = System.IO.File.ReadAllText(_shaderMotionPath);

        foreach (string name in PortedFunctions)
        {
            string csharpBody = FunctionBody(csharp, " " + name + "(");
            string shaderBody = FunctionBody(shader, " Flock" + name + "(");
            if (csharpBody == null || shaderBody == null)
            {
                Fail($"{name}: not found in {(csharpBody == null ? "FlockMotion.cs" : "SabaFlockMotion.cginc")}");
                continue;
            }

            List<float> a = Literals(csharpBody);
            List<float> b = Literals(shaderBody);
            if (a.Count != b.Count)
            {
                Fail($"{name}: {a.Count} constants in C#, {b.Count} in the shader\n    C#:     {string.Join(", ", a)}\n    shader: {string.Join(", ", b)}");
                continue;
            }

            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                {
                    Fail($"{name}: constant {a[i]} in C# against {b[i]} in the shader");
                    break;
                }
            }
        }
    }

    /// <summary>Body of the first definition whose signature contains <paramref name="marker"/>.</summary>
    private static string FunctionBody(string source, string marker)
    {
        int search = 0;
        while (true)
        {
            int at = source.IndexOf(marker, search, StringComparison.Ordinal);
            if (at < 0)
            {
                return null;
            }

            // A definition has its body brace before the next statement end.
            int brace = source.IndexOf('{', at);
            int semicolon = source.IndexOf(';', at);
            int arrow = source.IndexOf("=>", at, StringComparison.Ordinal);
            bool expressionBodied = arrow >= 0 && (brace < 0 || arrow < brace) && (semicolon < 0 || arrow < semicolon);
            if (expressionBodied)
            {
                return source.Substring(arrow, semicolon - arrow);
            }

            if (brace >= 0 && (semicolon < 0 || brace < semicolon))
            {
                int depth = 0;
                for (int i = brace; i < source.Length; i++)
                {
                    if (source[i] == '{')
                    {
                        depth++;
                    }
                    else if (source[i] == '}' && --depth == 0)
                    {
                        return source.Substring(brace, i - brace + 1);
                    }
                }

                return null;
            }

            search = at + marker.Length;
        }
    }

    private static List<float> Literals(string body)
    {
        var values = new List<float>();
        string withoutComments = System.Text.RegularExpressions.Regex.Replace(body, @"//[^\n]*", string.Empty);
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
            withoutComments, @"(?<![\w.])\d+(\.\d+)?"))
        {
            float v = float.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture);
            if (v != 0f && v != 1f)
            {
                values.Add(v);
            }
        }

        values.Sort();
        return values;
    }

    /// <summary>
    /// Every preset appears in Documentation~/elements.md with its id, name
    /// and default pattern and count, and the page lists no id the catalogue
    /// does not have.
    /// </summary>
    private static void ReferenceListsTheCatalogue()
    {
        if (_elementsPath == null)
        {
            Fail("run with the path of Documentation~/elements.md");
            return;
        }

        var rows = new Dictionary<string, string>();
        foreach (string line in System.IO.File.ReadAllLines(_elementsPath))
        {
            System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(line, @"^\|[^|]*\| `([a-z0-9-]+)` \|");
            if (m.Success)
            {
                rows[m.Groups[1].Value] = line;
            }
        }

        var ids = new HashSet<string>();
        foreach (FlockPreset preset in FlockSpeciesCatalog.All)
        {
            FlockSpecies s = preset.Species;
            ids.Add(s.id);
            if (!rows.TryGetValue(s.id, out string row))
            {
                Fail($"{s.id} is missing from elements.md");
                continue;
            }

            string[] cells = row.Split('|');
            Require(cells[1].Trim() == s.displayName, $"{s.id}: name '{cells[1].Trim()}' != '{s.displayName}'");
            Require(cells[4].Trim() == s.defaultPattern.ToString(), $"{s.id}: pattern '{cells[4].Trim()}' != {s.defaultPattern}");
            Require(cells[5].Trim() == s.defaultCount.ToString(), $"{s.id}: count '{cells[5].Trim()}' != {s.defaultCount}");
        }

        foreach (string id in rows.Keys)
        {
            Require(ids.Contains(id), $"elements.md lists '{id}', which is not in the catalogue");
        }
    }

    // ----------------------------------------------------------------------
    // Catalogue
    // ----------------------------------------------------------------------

    private static void CatalogueIsBroad()
    {
        var byHabitat = new Dictionary<FlockHabitat, int>();
        int birds = 0;
        int fish = 0;
        foreach (FlockPreset preset in FlockSpeciesCatalog.All)
        {
            byHabitat.TryGetValue(preset.Habitat, out int n);
            byHabitat[preset.Habitat] = n + 1;
            if (preset.Species.category == FlockCategory.Bird)
            {
                birds++;
            }
            else
            {
                fish++;
            }
        }

        Require(birds >= 20, $"only {birds} bird presets");
        Require(fish >= 25, $"only {fish} fish presets");
        foreach (FlockHabitat habitat in new[] { FlockHabitat.Sky, FlockHabitat.Sea, FlockHabitat.Reef, FlockHabitat.Aquarium })
        {
            byHabitat.TryGetValue(habitat, out int n);
            Require(n >= 5, $"only {n} presets for {habitat}");
        }
    }

    private static void PresetsAreSane()
    {
        var ids = new HashSet<string>();
        foreach (FlockPreset preset in FlockSpeciesCatalog.All)
        {
            FlockSpecies s = preset.Species;
            Require(!string.IsNullOrEmpty(s.id) && ids.Add(s.id), $"duplicate or empty id '{s.id}'");
            Require(!string.IsNullOrEmpty(s.displayName), $"{s.id}: no display name");
            Require(s.bodyLength > 0.01f && s.bodyLength < 5f, $"{s.id}: body length {s.bodyLength}");
            Require(s.cruiseSpeed > 0f, $"{s.id}: no cruise speed");
            Require(s.beatFrequency > 0f, $"{s.id}: no beat frequency");
            Require(s.defaultCount >= 1 && s.defaultCount <= 1000, $"{s.id}: default count {s.defaultCount}");
            Require(Math.Min(s.defaultArea.x, Math.Min(s.grounded ? s.defaultArea.x : s.defaultArea.y, s.defaultArea.z)) > s.bodyLength,
                $"{s.id}: default area is smaller than one body");

            bool bird = s.category == FlockCategory.Bird;
            Require(bird == (preset.Habitat == FlockHabitat.Sky), $"{s.id}: habitat does not match category");
            Require(bird == (s.animation == FlockAnimation.Flap || s.animation == FlockAnimation.Walk), $"{s.id}: animation does not match category");
            Require(!s.grounded || (s.defaultArea.y <= 0.001f && s.animation == FlockAnimation.Walk), $"{s.id}: invalid ground movement");

            FlockSpecies copy = FlockSpeciesCatalog.Create(s.id);
            Require(copy != null && !ReferenceEquals(copy, s), $"{s.id}: Create did not return a copy");
        }

        Require(FlockSpeciesCatalog.Create("no-such-species") == null, "unknown id returned a preset");
    }

    // ----------------------------------------------------------------------
    // Bodies
    // ----------------------------------------------------------------------

    private static void EveryBodyIsWellFormed()
    {
        foreach (FlockPreset preset in FlockSpeciesCatalog.All)
        {
            foreach (FlockDetail detail in Details)
            {
                var settings = new FlockSwarmSettings { count = 1, seed = 3, area = Vector3.one * 50f };
                Mesh mesh = FlockSwarmMeshBuilder.Build(preset.Species, settings, detail, "body");
                string label = $"{preset.Species.id}/{detail}";
                RequireWellFormed(mesh, label);

                float reach = FlockSwarmMeshBuilder.BodyReach(preset.Species);
                float scale = 1f + preset.Species.sizeVariance;
                foreach (Vector3 p in mesh.vertices)
                {
                    Require(p.magnitude * scale <= reach, $"{label}: vertex {p} lies beyond the bounds padding {reach:0.000} m");
                }

                // The padding follows the body, not a stray vertex: a few
                // lengths at most, even for filaments and whip tails.
                float size = Math.Max(preset.Species.Span, preset.Species.bodyLength);
                Require(reach <= 3f * size, $"{label}: bounds padding {reach:0.000} m for a {size:0.000} m body");
            }
        }
    }

    private static void TiersStayInBudget()
    {
        var ranges = new Dictionary<string, int[]>();
        void Track(string key, int value)
        {
            if (!ranges.TryGetValue(key, out int[] range))
            {
                ranges[key] = range = new[] { int.MaxValue, 0 };
            }

            range[0] = Math.Min(range[0], value);
            range[1] = Math.Max(range[1], value);
        }

        foreach (FlockPreset preset in FlockSpeciesCatalog.All)
        {
            string category = preset.Species.category.ToString();
            foreach (FlockDetail detail in Details)
            {
                FlockSwarmMeshBuilder.BodyCost(preset.Species, detail, out int vertices, out int triangles);
                Track($"{category} {detail} triangles", triangles);
                Track($"{category} {detail} vertices", vertices);
            }

            FlockSwarmMeshBuilder.BodyCost(preset.Species, FlockDetail.Silhouette, out _, out int silhouette);
            FlockSwarmMeshBuilder.BodyCost(preset.Species, FlockDetail.Low, out _, out int low);
            FlockSwarmMeshBuilder.BodyCost(preset.Species, FlockDetail.High, out int highVertices, out int high);
            string id = preset.Species.id;
            Require(silhouette <= 60, $"{id}: silhouette has {silhouette} triangles");
            Require(low <= 200, $"{id}: low has {low} triangles");
            Require(high <= 1200, $"{id}: high has {high} triangles");
            Require(highVertices <= 1200, $"{id}: high has {highVertices} vertices");
        }

        // Printed so the figures quoted in Documentation~/performance.md can be
        // read off a CI log.
        foreach (KeyValuePair<string, int[]> range in ranges)
        {
            Console.WriteLine($"    {range.Key} per individual: {range.Value[0]}-{range.Value[1]}");
        }

        FlockSpecies starling = FlockSpeciesCatalog.Create("starling");
        foreach (FlockDetail detail in Details)
        {
            FlockSwarmMeshBuilder.BodyCost(starling, detail, out int vertices, out int triangles);
            Console.WriteLine($"    starling x {starling.defaultCount} at {detail}: {vertices * starling.defaultCount} vertices, {triangles * starling.defaultCount} triangles");
        }
    }

    private static void TiersAreOrdered()
    {
        foreach (FlockPreset preset in FlockSpeciesCatalog.All)
        {
            FlockSwarmMeshBuilder.BodyCost(preset.Species, FlockDetail.Silhouette, out _, out int silhouette);
            FlockSwarmMeshBuilder.BodyCost(preset.Species, FlockDetail.Low, out _, out int low);
            FlockSwarmMeshBuilder.BodyCost(preset.Species, FlockDetail.High, out _, out int high);
            Require(silhouette < low && low < high,
                $"{preset.Species.id}: triangles {silhouette} / {low} / {high} are not increasing");
        }
    }

    // ----------------------------------------------------------------------
    // Swarm meshes
    // ----------------------------------------------------------------------

    private static void SwarmChannelsAreComplete()
    {
        FlockSpecies s = FlockSpeciesCatalog.Create("starling");
        var settings = new FlockSwarmSettings { count = 25, seed = 11, pattern = FlockPattern.Murmuration, area = new Vector3(60f, 20f, 60f) };
        Mesh mesh = FlockSwarmMeshBuilder.Build(s, settings, FlockDetail.Low, "swarm");
        RequireWellFormed(mesh, "starling swarm");

        FlockSwarmMeshBuilder.BodyCost(s, FlockDetail.Low, out int perBody, out int perBodyTriangles);
        Require(mesh.vertexCount == perBody * 25, "vertex count is not body x count");
        Require(mesh.GetIndexCount(0) == perBodyTriangles * 3 * 25, "index count is not body x count");

        var individual = new List<Vector4>();
        mesh.GetUVs(FlockShaderContract.IndividualChannel, individual);
        var seen = new HashSet<int>();
        for (int v = 0; v < individual.Count; v++)
        {
            int index = (int)individual[v].x;
            Require(index == v / perBody, $"vertex {v} carries index {index}");
            seen.Add(index);
            for (int c = 1; c < 4; c++)
            {
                float r = c == 1 ? individual[v].y : c == 2 ? individual[v].z : individual[v].w;
                Require(r >= 0f && r < 1f, $"vertex {v} random {r} outside [0, 1)");
            }
        }

        Require(seen.Count == 25, $"{seen.Count} distinct individuals");

        Bounds bounds = mesh.bounds;
        Vector3 expected = FlockSwarmMeshBuilder.BoundsExtents(s, settings);
        Require(Approximately(bounds.extents, expected, 1e-4f), $"bounds {bounds.extents} != {expected}");
    }

    private static void BroadBirdFlight()
    {
        foreach (string id in new[] { "swan", "crane" })
        {
            FlockSpecies species = FlockSpeciesCatalog.Create(id);
            var settings = new FlockSwarmSettings { pattern = FlockPattern.FreeFlight, count = 3, seed = 133,
                area = new Vector3(30f, 6f, 24f) };
            var distances = new List<float>();
            float minX = float.MaxValue, maxX = float.MinValue;
            for (float t = 0f; t < 1200f; t += 1f)
            {
                Vector3 a = FlockMotion.Position(FlockSwarmMeshBuilder.MotionInput(species, settings, 0), t);
                Vector3 b = FlockMotion.Position(FlockSwarmMeshBuilder.MotionInput(species, settings, 1), t);
                distances.Add((a - b).magnitude);
                minX = Mathf.Min(minX, a.x); maxX = Mathf.Max(maxX, a.x);
            }
            distances.Sort();
            Require(distances[distances.Count / 2] > species.Span * 3f, id + ": paths overlap for too much of the flight");
            Require(maxX - minX > 25f, id + ": trajectory too narrow");
        }
    }

    private static void AquariumTravel()
    {
        foreach (string id in new[] { "neon-tetra", "goldfish", "ryukin", "angelfish", "discus" })
        {
            FlockSpecies species = FlockSpeciesCatalog.Create(id);
            float depth = species.bodyLength >= 0.08f ? 0.45f : 0.3f;
            float height = species.bodyLength >= 0.08f ? 0.5f : 0.36f;
            var settings = new FlockSwarmSettings { pattern = FlockPattern.Wander, count = 3, seed = 215,
                area = new Vector3(0.29f, height * 0.5f - 0.02f, depth * 0.5f - 0.01f) };
            for (int i = 0; i < 3; i++)
            {
                FlockMotionInput input = FlockSwarmMeshBuilder.MotionInput(species, settings, i);
                Vector3 room = input.Area - input.BodyMargin;
                if (room.x <= 0f || room.y <= 0f || room.z <= 0f)
                {
                    Fail(id + ": body cannot fit inside tank; room=" + room);
                    return;
                }
                Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
                FlockMotion.Pose(input, 0f, out _, out Vector3 first, out _, out _);
                float leastDot = 1f;
                for (float t = 0f; t < 600f; t += 0.5f)
                {
                    FlockMotion.Pose(input, t, out Vector3 p, out Vector3 f, out _, out _);
                    min = new Vector3(Mathf.Min(min.x, p.x), Mathf.Min(min.y, p.y), Mathf.Min(min.z, p.z));
                    max = new Vector3(Mathf.Max(max.x, p.x), Mathf.Max(max.y, p.y), Mathf.Max(max.z, p.z));
                    if (Math.Abs(p.x) > room.x + 1e-5f || Math.Abs(p.y) > room.y + 1e-5f || Math.Abs(p.z) > room.z + 1e-5f)
                    {
                        Fail(id + ": animated body crosses glass");
                        return;
                    }
                    leastDot = Mathf.Min(leastDot, Vector3.Dot(first, f));
                }
                Require(max.x - min.x > room.x && max.z - min.z > room.z, id + ": insufficient travel");
                Require(leastDot < -0.5f, id + ": never turns around");
            }
        }
    }

    private static void JetPulses()
    {
        Vector3 random = new Vector3(0.2f, 0.4f, 0.7f);
        const float frequency = 0.8f;
        float min = 10f, max = 0f;
        for (float t = 0f; t < 10f; t += 0.01f)
        {
            float rate = (FlockMotion.JetClock(t + 0.001f, frequency, random) - FlockMotion.JetClock(t, frequency, random)) / 0.001f;
            min = Mathf.Min(min, rate); max = Mathf.Max(max, rate);
            float phase = FlockMotion.TwoPi * FlockMotion.Frac(random.y * 5.13f + random.z * 2.71f);
            Vector3 deformation = FlockMotion.AppendageOffset(Vector3.right, new Vector4(0f, 0f, 0f, 0f),
                (float)FlockAnimation.Jet, frequency, 0.06f, t, phase, 1f);
            Require(Math.Abs(rate - (1f - 0.65f * deformation.x / 0.08f)) < 0.015f, "jet pulse is out of phase with contraction");
        }
        Require(min > 0.3f && max / min > 4f, "jet must pulse without reversing");
        foreach (string id in new[] { "squid" })
        {
            FlockSpecies species = FlockSpeciesCatalog.Create(id);
            Require(species.defaultPattern == FlockPattern.Jet && species.animation == FlockAnimation.Jet, id + ": wrong motion");
        }
    }

    private static void FishHeads()
    {
        foreach (string id in new[] { "bluefin-tuna", "skipjack", "yellowtail", "barracuda", "salmon", "reef-shark" })
        foreach (FlockDetail detail in new[] { FlockDetail.Low, FlockDetail.High })
        {
            FlockSpecies species = FlockSpeciesCatalog.Create(id);
            FlockMeshBuffer mesh = FlockBodyBuilder.Build(species, detail);
            int left = 0, right = 0;
            for (int i = 0; i < mesh.VertexCount; i++)
            {
                Color c = mesh.Colors[i];
                if (c.r > 0.04f || c.g > 0.04f || c.b > 0.04f) continue;
                Vector3 p = mesh.Positions[i];
                Require(p.z > 0.15f * species.bodyLength, $"{id}: head marking is behind the head");
                if (p.x < 0f) left++; else right++;
                Require(Math.Abs(mesh.Normals[i].x) > 0.99f, $"{id}: head marking faces the wrong side");
            }
            Require(left >= 29 && left == right, $"{id}/{detail}: missing bilateral head features");
        }
    }

    private static void SquidColour()
    {
        foreach (FlockPreset preset in FlockSpeciesCatalog.All)
            Require(preset.Species.id != "octopus", "octopus is still in the catalog");
        FlockSpecies squid = FlockSpeciesCatalog.Create("squid");
        foreach (FlockDetail detail in Details)
        {
            FlockMeshBuffer mesh = FlockBodyBuilder.Build(squid, detail);
            foreach (Color color in mesh.Colors)
                Require(Math.Abs(color.r - squid.primary.r) < 1e-6f
                    && Math.Abs(color.g - squid.primary.g) < 1e-6f
                    && Math.Abs(color.b - squid.primary.b) < 1e-6f, "squid appendage differs from mantle colour");
        }
    }

    private static void MantaFloorGlide()
    {
        FlockSpecies species = FlockSpeciesCatalog.Create("manta");
        Require(species.defaultPattern == FlockPattern.FloorGlide, "manta must use low swimming");
        var settings = new FlockSwarmSettings { pattern = FlockPattern.FloorGlide, count = 3, area = new Vector3(11.8f, 4.8f, 5.8f) };
        FlockMotionInput input = FlockSwarmMeshBuilder.MotionInput(species, settings, 0);
        Vector3 room = input.Area - input.BodyMargin;
        int low = 0, total = 0;
        float sumY = 0f, minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        for (float t = 0f; t < 600f; t += 0.2f)
        {
            Vector3 p = FlockMotion.Position(input, t);
            Vector3 fish = FlockMotion.Wander(room, input.Speed, input.Random, t + input.TimeOffset);
            Require(Math.Abs(p.x - fish.x) < 1e-5f && Math.Abs(p.z - fish.z) < 1e-5f, "manta departs from normal fish path");
            if (p.y < 0f) low++;
            total++;
            sumY += p.y;
            minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
            minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
        }
        Require(low > total * 0.65f && sumY / total < -0.2f * room.y, "manta lacks a lower depth preference");
        Require(maxX - minX > room.x && maxZ - minZ > room.z, "manta swims in too narrow a range");
    }

    private static void JellyfishDrift()
    {
        FlockSpecies species = FlockSpeciesCatalog.Create("jellyfish");
        Require(species.defaultPattern == FlockPattern.Float, "jellyfish must float");
        var settings = new FlockSwarmSettings { pattern = FlockPattern.Float, count = 3, area = new Vector3(3.6f, 1.3f, 1.3f) };
        FlockMotionInput input = FlockSwarmMeshBuilder.MotionInput(species, settings, 0);
        float minY = float.MaxValue, maxY = float.MinValue;
        for (float t = 0f; t < 3600f; t += 1f)
        {
            FlockMotion.Pose(input, t, out Vector3 p, out _, out Vector3 up, out _);
            Vector3 next = FlockMotion.Position(input, t + 1f);
            Require((next - p).magnitude < 0.08f, "jellyfish moves too fast");
            Require(Approximately(up, Vector3.up, 1e-6f), "jellyfish tilts like a fish");
            minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
        }
        Require(maxY - minY > 0.3f, "jellyfish lacks vertical drift");
        float segment = (60f + 90f * input.Random.y) * input.BodyLength / input.Speed;
        for (int i = 1; i < 10; i++)
        {
            float t = segment * i - input.TimeOffset;
            Vector3 before = FlockMotion.Position(input, t - 0.1f), at = FlockMotion.Position(input, t), after = FlockMotion.Position(input, t + 0.1f);
            Require((after - at - (at - before)).magnitude < 0.001f, "jellyfish drift snaps at waypoint");
        }
    }

    private static void ChannelsMatchMotionInput()
    {
        foreach (FlockPattern pattern in Patterns)
        {
            FlockSpecies s = FlockSpeciesCatalog.Create("sardine");
            var settings = new FlockSwarmSettings { count = 7, seed = 5 + (int)pattern, pattern = pattern, area = new Vector3(20f, 10f, 20f), speedScale = 1.3f };
            Mesh mesh = FlockSwarmMeshBuilder.Build(s, settings, FlockDetail.Silhouette, "contract");
            FlockSwarmMeshBuilder.BodyCost(s, FlockDetail.Silhouette, out int perBody, out _);

            var individual = new List<Vector4>();
            var swarm = new List<Vector4>();
            var area = new List<Vector4>();
            var extra = new List<Vector4>();
            var margin = new List<Vector4>();
            var animation = new List<Vector4>();
            mesh.GetUVs(FlockShaderContract.IndividualChannel, individual);
            mesh.GetUVs(FlockShaderContract.SwarmChannel, swarm);
            mesh.GetUVs(FlockShaderContract.AreaChannel, area);
            mesh.GetUVs(FlockShaderContract.ExtraChannel, extra);
            mesh.GetUVs(FlockShaderContract.BodyMarginChannel, margin);
            mesh.GetUVs(FlockShaderContract.AnimationChannel, animation);

            for (int i = 0; i < settings.count; i++)
            {
                int v = i * perBody;
                var decoded = new FlockMotionInput
                {
                    Pattern = (FlockPattern)(int)swarm[v].x,
                    Speed = swarm[v].y,
                    TimeOffset = swarm[v].z,
                    ClusterRadius = swarm[v].w,
                    Area = new Vector3(area[v].x, area[v].y, area[v].z),
                    BodyLength = area[v].w,
                    BodyMargin = new Vector3(margin[v].x, margin[v].y, margin[v].z),
                    AnimationFrequency = animation[v].y,
                    Count = extra[v].x,
                    Index = individual[v].x,
                    Random = new Vector3(individual[v].y, individual[v].z, individual[v].w),
                    BankGain = extra[v].y,
                    MaxPitch = extra[v].z,
                };
                FlockMotionInput built = FlockSwarmMeshBuilder.MotionInput(s, settings, i);

                foreach (float t in new[] { 0f, 3.7f, 41f })
                {
                    Vector3 a = FlockMotion.Position(decoded, t);
                    Vector3 b = FlockMotion.Position(built, t);
                    Require(Approximately(a, b, 1e-5f), $"{pattern}: individual {i} decodes to {a}, built {b}");
                }
            }
        }
    }

    private static void SwarmsAreDeterministic()
    {
        FlockSpecies s = FlockSpeciesCatalog.Create("neon-tetra");
        var settings = new FlockSwarmSettings { count = 9, seed = 77 };
        Mesh a = FlockSwarmMeshBuilder.Build(s, settings, FlockDetail.High, "a");
        Mesh b = FlockSwarmMeshBuilder.Build(s, settings, FlockDetail.High, "b");
        Require(a.vertexCount == b.vertexCount, "vertex count changed for the same seed");
        for (int i = 0; i < a.vertexCount; i++)
        {
            Require(Approximately(a.vertices[i], b.vertices[i], 0f), $"vertex {i} changed for the same seed");
        }

        var ua = new List<Vector4>();
        var ub = new List<Vector4>();
        a.GetUVs(FlockShaderContract.IndividualChannel, ua);
        b.GetUVs(FlockShaderContract.IndividualChannel, ub);
        for (int i = 0; i < ua.Count; i++)
        {
            Require(ua[i].y == ub[i].y && ua[i].z == ub[i].z && ua[i].w == ub[i].w, $"random {i} changed for the same seed");
        }

        settings.seed = 78;
        Mesh c = FlockSwarmMeshBuilder.Build(s, settings, FlockDetail.High, "c");
        var uc = new List<Vector4>();
        c.GetUVs(FlockShaderContract.IndividualChannel, uc);
        int differing = 0;
        for (int i = 0; i < ua.Count; i++)
        {
            if (ua[i].y != uc[i].y)
            {
                differing++;
            }
        }

        Require(differing > ua.Count / 2, "a different seed left the randoms unchanged");
    }

    private static void LargeSwarmsUse32BitIndices()
    {
        FlockSpecies s = FlockSpeciesCatalog.Create("gull");
        var settings = new FlockSwarmSettings { count = 1000, seed = 2 };
        Mesh mesh = FlockSwarmMeshBuilder.Build(s, settings, FlockDetail.High, "large");
        Require(mesh.vertexCount > 65535, "test swarm is not large enough to need 32-bit indices");
        Require(mesh.indexFormat == UnityEngine.Rendering.IndexFormat.UInt32, "large swarm kept 16-bit indices");

        settings.count = 10;
        Mesh small = FlockSwarmMeshBuilder.Build(s, settings, FlockDetail.Silhouette, "small");
        Require(small.indexFormat == UnityEngine.Rendering.IndexFormat.UInt16, "small swarm switched to 32-bit indices");
    }

    // ----------------------------------------------------------------------
    // Motion
    // ----------------------------------------------------------------------

    private static void PatternsStayInsideTheArea()
    {
        string[] species = { "starling", "goose", "sardine", "medaka-orange", "manta" };
        foreach (string id in species)
        {
            FlockSpecies s = FlockSpeciesCatalog.Create(id);
            foreach (FlockPattern pattern in Patterns)
            {
                foreach (int count in new[] { 1, 12, 300 })
                {
                    var settings = new FlockSwarmSettings
                    {
                        pattern = pattern,
                        count = count,
                        seed = 19,
                        area = s.defaultArea,
                    };
                    RequireInsideArea(s, settings, $"{id}/{pattern}/{count}");
                }
            }
        }
    }

    private static void DefaultSwarmsStayInsideBounds()
    {
        foreach (FlockPreset preset in FlockSpeciesCatalog.All)
        {
            FlockSpecies s = preset.Species;
            var settings = new FlockSwarmSettings
            {
                pattern = s.defaultPattern,
                count = s.defaultCount,
                seed = 4,
                area = s.defaultArea,
            };
            RequireInsideArea(s, settings, s.id);
        }
    }

    private static void RequireInsideArea(FlockSpecies s, FlockSwarmSettings settings, string label)
    {
        Vector3 area = FlockSwarmMeshBuilder.Area(settings);
        Vector3 bounds = FlockSwarmMeshBuilder.BoundsExtents(s, settings);
        float tolerance = 1e-3f * Math.Max(area.x, Math.Max(area.y, area.z));
        int count = FlockSwarmMeshBuilder.Count(settings);
        int step = Math.Max(1, count / 40);

        for (int i = 0; i < count; i += step)
        {
            FlockMotionInput input = FlockSwarmMeshBuilder.MotionInput(s, settings, i);
            for (float t = 0f; t < 900f; t += 7.3f)
            {
                Vector3 p = FlockMotion.Position(input, t);
                if (!Finite(p))
                {
                    Fail($"{label}: individual {i} at t={t} is not finite");
                    return;
                }

                Vector3 margin = settings.pattern == FlockPattern.Wander || settings.pattern == FlockPattern.FreeFlight
                    || settings.pattern == FlockPattern.Jet || settings.pattern == FlockPattern.Float
                    || settings.pattern == FlockPattern.FloorGlide
                    ? input.BodyMargin : Vector3.one * s.bodyLength;
                bool inside = Math.Abs(p.x) <= area.x - margin.x + tolerance
                    && Math.Abs(p.y) <= area.y - margin.y + tolerance
                    && Math.Abs(p.z) <= area.z - margin.z + tolerance;
                bool coveredByBounds = Math.Abs(p.x) <= bounds.x && Math.Abs(p.y) <= bounds.y && Math.Abs(p.z) <= bounds.z;

                // An area thinner than two bodies cannot keep the margin; it
                // must still stay inside the renderer bounds.
                bool roomy = area.x > margin.x && area.y > margin.y && area.z > margin.z;
                if ((roomy && !inside) || !coveredByBounds)
                {
                    Fail($"{label}: individual {i} at t={t} is at {p}, outside area {area}");
                    return;
                }
            }
        }
    }

    private static void AnchoredAndGroundedMotion()
    {
        foreach (string id in new[] { "garden-eel", "urchin", "anemone", "oyster", "chicken", "chick" })
        {
            FlockSpecies species = FlockSpeciesCatalog.Create(id);
            var settings = new FlockSwarmSettings { pattern = species.defaultPattern, count = 8, area = species.defaultArea };
            for (int i = 0; i < settings.count; i++)
            {
                FlockMotionInput input = FlockSwarmMeshBuilder.MotionInput(species, settings, i);
                Vector3 initial = FlockMotion.Position(input, 0f);
                for (float t = 0f; t < 60f; t += 0.7f)
                {
                    FlockMotion.Pose(input, t, out Vector3 position, out Vector3 forward, out Vector3 up, out _);
                    if (species.grounded)
                    {
                        Require(Math.Abs(position.y) < 1e-6f && Math.Abs(forward.y) < 1e-6f && Approximately(up, Vector3.up, 1e-5f), id + ": walking bird left its plane");
                    }
                    else Require(Approximately(position, initial, 1e-6f), id + ": anchored animal moved");
                    Vector3 rootOffset = FlockMotion.AppendageOffset(Vector3.zero, new Vector4(0f, 0f, 4f, 0f),
                        (float)species.animation, species.beatFrequency, species.beatAmplitude, t, 0f, species.bodyLength);
                    Require(Approximately(rootOffset, Vector3.zero, 1e-6f), id + ": appendage root detached");
                }
            }
        }
    }

    private static void HeadingsAreContinuous()
    {
        const float frame = 1f / 30f;
        foreach (FlockPreset preset in FlockSpeciesCatalog.All)
        {
            FlockSpecies s = preset.Species;
            foreach (FlockPattern pattern in Patterns)
            {
                var settings = new FlockSwarmSettings { pattern = pattern, count = s.defaultCount, seed = 8, area = s.defaultArea };
                int count = FlockSwarmMeshBuilder.Count(settings);
                for (int i = 0; i < count; i += Math.Max(1, count / 6))
                {
                    FlockMotionInput input = FlockSwarmMeshBuilder.MotionInput(s, settings, i);
                    FlockMotion.Pose(input, 0f, out _, out Vector3 previous, out Vector3 previousUp, out _);
                    float worst = 0f;
                    float worstTime = 0f;
                    for (float t = frame; t < 120f; t += frame)
                    {
                        FlockMotion.Pose(input, t, out _, out Vector3 forward, out Vector3 up, out _);
                        float turn = Mathf.Acos(Vector3.Dot(previous, forward));
                        if (turn > worst)
                        {
                            worst = turn;
                            worstTime = t;
                        }

                        previous = forward;
                    }

                    if (worst > 0.35f)
                    {
                        Fail($"{s.id}/{pattern}: individual {i} turns {worst * Mathf.Rad2Deg:0.0} degrees in one frame at t={worstTime:0.00}");
                        break;
                    }
                }
            }
        }
    }

    private static void PoseFrameIsOrthonormal()
    {
        foreach (string id in new[] { "black-kite", "bat", "barracuda", "koi" })
        {
            FlockSpecies s = FlockSpeciesCatalog.Create(id);
            foreach (FlockPattern pattern in Patterns)
            {
                var settings = new FlockSwarmSettings { pattern = pattern, count = 20, seed = 31, area = s.defaultArea };
                for (int i = 0; i < 20; i += 3)
                {
                    FlockMotionInput input = FlockSwarmMeshBuilder.MotionInput(s, settings, i);
                    for (float t = 0f; t < 60f; t += 0.9f)
                    {
                        FlockMotion.Pose(input, t, out Vector3 p, out Vector3 f, out Vector3 u, out Vector3 r);
                        string where = $"{id}/{pattern} #{i} t={t}";
                        Require(Finite(p) && Finite(f) && Finite(u) && Finite(r), $"{where}: pose not finite");
                        Require(Math.Abs(f.magnitude - 1f) < 1e-3f && Math.Abs(u.magnitude - 1f) < 1e-3f && Math.Abs(r.magnitude - 1f) < 1e-3f,
                            $"{where}: frame not unit length");
                        Require(Math.Abs(Vector3.Dot(f, u)) < 1e-3f && Math.Abs(Vector3.Dot(f, r)) < 1e-3f && Math.Abs(Vector3.Dot(u, r)) < 1e-3f,
                            $"{where}: frame not orthogonal");
                        Require(Math.Abs(f.y) <= input.MaxPitch + 1e-3f, $"{where}: pitch {f.y} beyond {input.MaxPitch}");
                        Require(u.y > 0f, $"{where}: flying upside down");
                    }
                }
            }
        }
    }

    private static void VFormationKeepsTheLeaderAhead()
    {
        FlockSpecies s = FlockSpeciesCatalog.Create("goose");
        var settings = new FlockSwarmSettings { pattern = FlockPattern.VFormation, count = 15, seed = 1, area = s.defaultArea };
        for (float t = 0f; t < 200f; t += 13f)
        {
            FlockMotion.Pose(FlockSwarmMeshBuilder.MotionInput(s, settings, 0), t, out Vector3 leader, out Vector3 forward, out _, out _);
            for (int i = 1; i < settings.count; i++)
            {
                Vector3 p = FlockMotion.Position(FlockSwarmMeshBuilder.MotionInput(s, settings, i), t);
                float ahead = Vector3.Dot(leader - p, forward);
                Require(ahead > 0f, $"t={t}: individual {i} is {-ahead:0.00} m ahead of the leader");
            }
        }
    }

    // ----------------------------------------------------------------------
    // Colour
    // ----------------------------------------------------------------------

    /// <summary>
    /// Both wings (and both ray fins) cover the same area. A wing built with
    /// the wrong winding on one side, or dropped as degenerate, shows up here
    /// as an area mismatch.
    /// </summary>
    private static void WingsAreSymmetric()
    {
        foreach (FlockPreset preset in FlockSpeciesCatalog.All)
        {
            foreach (FlockDetail detail in Details)
            {
                Mesh mesh = FlockSwarmMeshBuilder.Build(preset.Species, new FlockSwarmSettings { count = 1 }, detail, "wings");
                var body = new List<Vector4>();
                mesh.GetUVs(FlockShaderContract.BodyChannel, body);
                Vector3[] v = mesh.vertices;
                int[] t = mesh.triangles;

                float left = 0f;
                float right = 0f;
                for (int i = 0; i < t.Length; i += 3)
                {
                    if ((int)body[t[i]].z != (int)FlockBodyPart.Wing)
                    {
                        continue;
                    }

                    float area = 0.5f * Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).magnitude;
                    if (body[t[i]].x + body[t[i + 1]].x + body[t[i + 2]].x >= 0f)
                    {
                        right += area;
                    }
                    else
                    {
                        left += area;
                    }
                }

                bool winged = preset.Species.category == FlockCategory.Bird || preset.Species.fishBody == FlockFishBody.Ray;
                if (!winged)
                {
                    Require(left + right == 0f, $"{preset.Species.id}/{detail}: a fish without pectoral discs has wing geometry");
                    continue;
                }

                Require(left > 0f && right > 0f && Math.Abs(left - right) <= 0.01f * Math.Max(left, right),
                    $"{preset.Species.id}/{detail}: wing areas left {left:0.00000} m2, right {right:0.00000} m2");
            }
        }
    }

    private static void WingMarkingsAreColoured()
    {
        FlockSpecies gull = FlockSpeciesCatalog.Create("gull");
        Mesh mesh = FlockSwarmMeshBuilder.Build(gull, new FlockSwarmSettings { count = 1 }, FlockDetail.High, "gull");
        var body = new List<Vector4>();
        mesh.GetUVs(FlockShaderContract.BodyChannel, body);
        Color[] colors = mesh.colors;

        int tips = 0;
        int inner = 0;
        for (int i = 0; i < body.Count; i++)
        {
            if ((int)body[i].z != (int)FlockBodyPart.Wing)
            {
                continue;
            }

            float span = Math.Abs(body[i].x);
            if (span > 1f - gull.wingTipFraction + 0.01f)
            {
                Require(SameColour(colors[i], gull.detail), $"wing tip vertex {i} is not the tip colour");
                tips++;
            }
            else if (span < 1f - gull.wingTipFraction - 0.01f)
            {
                Require(!SameColour(colors[i], gull.detail), $"inner wing vertex {i} took the tip colour");
                inner++;
            }
        }

        Require(tips > 0 && inner > 0, $"wing sampled {tips} tip and {inner} inner vertices");

        FlockSpecies crane = FlockSpeciesCatalog.Create("crane");
        Mesh craneMesh = FlockSwarmMeshBuilder.Build(crane, new FlockSwarmSettings { count = 1 }, FlockDetail.Low, "crane");
        int dark = 0;
        foreach (Color c in craneMesh.colors)
        {
            if (SameColour(c, crane.detail))
            {
                dark++;
            }
        }

        Require(dark > 0, "crane flight feathers are not dark");
    }

    private static void FishPatternsAreVisible()
    {
        foreach (FlockPreset preset in FlockSpeciesCatalog.All)
        {
            FlockSpecies s = preset.Species;
            if (s.category != FlockCategory.Fish || s.pattern == FlockColorPattern.Plain)
            {
                continue;
            }

            Mesh mesh = FlockSwarmMeshBuilder.Build(s, new FlockSwarmSettings { count = 1 }, FlockDetail.High, s.id);
            int marked = 0;
            foreach (Color c in mesh.colors)
            {
                if (SameColour(c, s.accent))
                {
                    marked++;
                }
            }

            Require(marked >= 3, $"{s.id}: {s.pattern} marks only {marked} vertices");
        }
    }

    private static void DegenerateSettingsStayFinite()
    {
        FlockSpecies s = FlockSpeciesCatalog.Create("pigeon");
        var cases = new[]
        {
            new FlockSwarmSettings { count = 0, area = Vector3.zero },
            new FlockSwarmSettings { count = 1, area = new Vector3(-5f, 0f, 5f), speedScale = 0f },
            new FlockSwarmSettings { count = 3, area = Vector3.one * 0.01f, clusterRadius = 100f },
            new FlockSwarmSettings { count = 2000, area = Vector3.one * 1e4f, speedScale = 50f },
        };

        foreach (FlockSwarmSettings settings in cases)
        {
            foreach (FlockPattern pattern in Patterns)
            {
                settings.pattern = pattern;
                int count = FlockSwarmMeshBuilder.Count(settings);
                Require(count >= 1 && count <= 1000, $"count {settings.count} resolved to {count}");
                for (int i = 0; i < Math.Min(count, 5); i++)
                {
                    FlockMotionInput input = FlockSwarmMeshBuilder.MotionInput(s, settings, i);
                    for (float t = 0f; t < 30f; t += 1.1f)
                    {
                        FlockMotion.Pose(input, t, out Vector3 p, out Vector3 f, out Vector3 u, out Vector3 r);
                        Require(Finite(p) && Finite(f) && Finite(u) && Finite(r),
                            $"{pattern} with count {settings.count}, area {settings.area}: pose not finite");
                    }
                }
            }
        }

        FlockSpecies odd = s.Clone();
        odd.bodyLength = 0.005f;
        odd.wingspan = 0f;
        odd.tailLength = 0f;
        odd.neckLength = 0f;
        odd.beakLength = 0f;
        odd.wingTipFraction = 1f;
        odd.flightFeatherFraction = 1f;
        foreach (FlockDetail detail in Details)
        {
            RequireWellFormed(FlockSwarmMeshBuilder.Build(odd, new FlockSwarmSettings { count = 1 }, detail, "odd"), $"odd bird/{detail}");
        }

        FlockSpecies fish = FlockSpeciesCatalog.Create("discus").Clone();
        fish.patternCount = 0;
        fish.caudalSize = 0f;
        fish.dorsalHeight = 0f;
        fish.pectoralSize = 0f;
        foreach (FlockDetail detail in Details)
        {
            RequireWellFormed(FlockSwarmMeshBuilder.Build(fish, new FlockSwarmSettings { count = 1 }, detail, "odd"), $"odd fish/{detail}");
        }
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    private static void RequireWellFormed(Mesh mesh, string label)
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Color[] colors = mesh.colors;
        int[] triangles = mesh.triangles;

        Require(vertices.Length > 0, $"{label}: no vertices");
        Require(triangles.Length > 0 && triangles.Length % 3 == 0, $"{label}: bad index count {triangles.Length}");
        Require(normals.Length == vertices.Length, $"{label}: normals do not match vertices");
        Require(colors.Length == vertices.Length, $"{label}: colours do not match vertices");

        for (int channel = 0; channel <= FlockShaderContract.BodyMarginChannel; channel++)
        {
            var uvs = new List<Vector4>();
            mesh.GetUVs(channel, uvs);
            Require(uvs.Count == vertices.Length, $"{label}: UV{channel} has {uvs.Count} of {vertices.Length} entries");
            foreach (Vector4 uv in uvs)
            {
                if (!Finite(uv.x) || !Finite(uv.y) || !Finite(uv.z) || !Finite(uv.w))
                {
                    Fail($"{label}: UV{channel} not finite");
                    break;
                }
            }
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            if (!Finite(vertices[i]))
            {
                Fail($"{label}: vertex {i} not finite");
                return;
            }

            if (Math.Abs(normals[i].magnitude - 1f) > 1e-3f)
            {
                Fail($"{label}: normal {i} has length {normals[i].magnitude}");
                return;
            }

            Color c = colors[i];
            if (c.r < 0f || c.r > 1f || c.g < 0f || c.g > 1f || c.b < 0f || c.b > 1f || c.a < 0f || c.a > 1f)
            {
                Fail($"{label}: colour {i} out of range");
                return;
            }
        }

        int degenerate = 0;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int a = triangles[i];
            int b = triangles[i + 1];
            int c = triangles[i + 2];
            if (a < 0 || b < 0 || c < 0 || a >= vertices.Length || b >= vertices.Length || c >= vertices.Length)
            {
                Fail($"{label}: triangle {i / 3} indexes outside the vertex range");
                return;
            }

            if (a == b || b == c || a == c)
            {
                Fail($"{label}: triangle {i / 3} repeats a vertex");
                return;
            }

            if (Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]).sqrMagnitude <= 0f)
            {
                degenerate++;
            }
        }

        // Tips and notches collapse a few quads by design; a body made mostly
        // of zero-area triangles would render as nothing.
        Require(degenerate * 10 <= triangles.Length / 3, $"{label}: {degenerate} of {triangles.Length / 3} triangles have zero area");
    }

    private static bool SameColour(Color a, Color b)
    {
        return Math.Abs(a.r - b.r) < 1e-4f && Math.Abs(a.g - b.g) < 1e-4f && Math.Abs(a.b - b.b) < 1e-4f;
    }

    private static bool Approximately(Vector3 a, Vector3 b, float tolerance)
    {
        return Math.Abs(a.x - b.x) <= tolerance && Math.Abs(a.y - b.y) <= tolerance && Math.Abs(a.z - b.z) <= tolerance;
    }

    private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

    private static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);

    private static void Run(string name, Action check)
    {
        _current = name;
        int before = _failures;
        try
        {
            check();
        }
        catch (Exception ex)
        {
            Fail($"threw {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }

        Console.WriteLine(_failures == before ? $"ok: {name}" : $"FAILED: {name}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            Fail(message);
        }
    }

    private static void Fail(string message)
    {
        _failures++;
        Console.Error.WriteLine($"  [{_current}] {message}");
    }
}
