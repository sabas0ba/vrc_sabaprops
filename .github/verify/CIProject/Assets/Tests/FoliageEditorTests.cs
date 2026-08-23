using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SabaProps.Foliage;
using SabaProps.Foliage.Editors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SabaProps.Foliage.CITests
{
    /// <summary>
    /// Verifies the one thing no offline harness can: that Unity itself accepts
    /// the surface shader. A syntax error, a bad #pragma, or a variant that
    /// fails to generate all surface here.
    /// </summary>
    public class FoliageShaderTests
    {
        [Test]
        public void Shader_IsFound()
        {
            Assert.IsNotNull(Shader.Find(FoliageAssetLibrary.ShaderName),
                $"shader '{FoliageAssetLibrary.ShaderName}' was not found");
        }

        [Test]
        public void Shader_CompilesWithoutErrors()
        {
            Shader shader = Shader.Find(FoliageAssetLibrary.ShaderName);
            Assert.IsNotNull(shader);

            if (ShaderUtil.ShaderHasError(shader))
            {
                var details = new List<string>();
                foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
                {
                    details.Add($"{message.file}({message.line}): {message.message} {message.messageDetails}");
                }

                Assert.Fail("shader failed to compile:\n" + string.Join("\n", details));
            }

            Assert.IsTrue(shader.isSupported, "shader is not supported on this platform");
        }

        [Test]
        public void DefaultMaterial_UsesFoliageShaderWithInstancing()
        {
            Material material = FoliageAssetLibrary.CreateOrLoadDefaultMaterial();

            Assert.IsNotNull(material, "default material could not be created");
            Assert.AreEqual(FoliageAssetLibrary.ShaderName, material.shader.name);

            // Without this flag Unity silently falls back to one draw call per
            // renderer, which defeats the entire point of the package.
            Assert.IsTrue(material.enableInstancing, "GPU instancing is not enabled on the default material");
        }
    }

    public class FoliageMeshTests
    {
        private static void AssertMeshIsWellFormed(Mesh mesh, string label)
        {
            Assert.IsNotNull(mesh, $"{label}: mesh is null");
            Assert.Greater(mesh.vertexCount, 0, $"{label}: no vertices");
            Assert.Greater(mesh.triangles.Length, 0, $"{label}: no triangles");

            foreach (Vector3 position in mesh.vertices)
            {
                Assert.IsFalse(
                    float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z) ||
                    float.IsInfinity(position.x) || float.IsInfinity(position.y) || float.IsInfinity(position.z),
                    $"{label}: non-finite vertex position");
            }

            foreach (Vector3 normal in mesh.normals)
            {
                Assert.AreEqual(1f, normal.magnitude, 1e-3f, $"{label}: normal is not unit length");
            }

            // The shader reads the sway pivot and stiffness from UV3; without it
            // every instance would pivot around the mesh origin.
            var uv3 = new List<Vector4>();
            mesh.GetUVs(3, uv3);
            Assert.AreEqual(mesh.vertexCount, uv3.Count, $"{label}: UV3 channel is missing");

            Assert.AreEqual(mesh.vertexCount, mesh.colors.Length, $"{label}: vertex colours are missing");

            // Wind displaces vertices in the vertex shader, so the bounds must be
            // padded past the raw geometry or foliage pops at the frustum edge.
            Bounds bounds = mesh.bounds;
            Assert.Greater(bounds.size.x, 0f, $"{label}: degenerate bounds");
            Assert.Greater(bounds.size.y, 0f, $"{label}: degenerate bounds");
        }

        [Test]
        public void GrassClump_TopologyMatchesParameters()
        {
            var species = ScriptableObject.CreateInstance<FoliageSpecies>();
            try
            {
                species.kind = FoliageSpeciesKind.GrassClump;
                GrassParams p = species.grass;

                Mesh mesh = FoliageMeshBuilder.Build(species);
                AssertMeshIsWellFormed(mesh, "grass");

                // Each blade is `segments` rows of 2 vertices plus a single tip.
                int expectedVertices = p.bladeCount * (p.segments * 2 + 1);
                int expectedTriangles = p.bladeCount * ((p.segments - 1) * 2 + 1);

                Assert.AreEqual(expectedVertices, mesh.vertexCount, "grass vertex count");
                Assert.AreEqual(expectedTriangles, mesh.triangles.Length / 3, "grass triangle count");

                // Measured on the raw geometry, not on mesh.bounds, which is
                // padded for wind and would hide a change in blade height. Bend
                // only displaces a blade horizontally, so the tallest vertex is
                // the tallest blade.
                float tallest = 0f;
                foreach (Vector3 vertex in mesh.vertices)
                {
                    tallest = Mathf.Max(tallest, vertex.y);
                }

                // An absolute guard rail, not a spec: ankle-high grass reads as
                // moss from standing height in VR, which is the mistake this
                // default has already been corrected for once.
                Assert.Greater(tallest, 0.4f, "default grass is unexpectedly short");
                Assert.Less(tallest, 1.5f, "default grass is unexpectedly tall");

                Object.DestroyImmediate(mesh);
            }
            finally
            {
                Object.DestroyImmediate(species);
            }
        }

        [Test]
        public void Sunflower_IsWellFormedAndCheap()
        {
            var species = ScriptableObject.CreateInstance<FoliageSpecies>();
            try
            {
                species.kind = FoliageSpeciesKind.Sunflower;

                Mesh mesh = FoliageMeshBuilder.Build(species);
                AssertMeshIsWellFormed(mesh, "sunflower");

                // A guard rail, not a spec: a sunflower that suddenly costs
                // thousands of triangles would break mass placement.
                Assert.Less(mesh.triangles.Length / 3, 400, "sunflower is unexpectedly heavy");
                Assert.Greater(mesh.bounds.size.y, 0.5f, "sunflower is unexpectedly short");

                Object.DestroyImmediate(mesh);
            }
            finally
            {
                Object.DestroyImmediate(species);
            }
        }

        /// <summary>
        /// Mirrors the shader's default _BendPower. Only used to compare wind
        /// amplitudes against each other, so drifting from the material's actual
        /// value would weaken this check rather than invalidate it.
        /// </summary>
        private const float BendPower = 2.2f;

        [Test]
        public void Sunflower_HeadDoesNotTearInWind()
        {
            var species = ScriptableObject.CreateInstance<FoliageSpecies>();
            try
            {
                species.kind = FoliageSpeciesKind.Sunflower;

                Mesh mesh = FoliageMeshBuilder.Build(species);
                Assert.IsNotNull(mesh);

                // Wind phase comes from COLOR.a. Stem, leaves, disc and petals
                // are one rigid plant, so a second phase anywhere means some
                // joint is being driven apart.
                var phases = new HashSet<float>();
                foreach (Color color in mesh.colors)
                {
                    phases.Add(color.a);
                }

                Assert.AreEqual(1, phases.Count,
                    "the sunflower must sway with a single wind phase, or its parts drift apart");

                var uv3 = new List<Vector4>();
                mesh.GetUVs(3, uv3);
                Vector2[] uv0 = mesh.uv;
                Vector3[] vertices = mesh.vertices;

                float top = 0f;
                foreach (Vector3 vertex in vertices)
                {
                    top = Mathf.Max(top, vertex.y);
                }

                // Everything from the topmost leaf up: stem tip, disc, petals.
                // These are rigidly joined, so their sway amplitudes have to stay
                // close or the petals slide off the disc.
                float headFloor = top - (species.sunflower.headRadius + species.sunflower.petalLength);

                float weakest = float.MaxValue;
                float strongest = 0f;
                int counted = 0;

                for (int i = 0; i < vertices.Length; i++)
                {
                    if (vertices[i].y < headFloor)
                    {
                        continue;
                    }

                    float bend = Mathf.Pow(Mathf.Clamp01(uv0[i].y), BendPower) * uv3[i].w;
                    weakest = Mathf.Min(weakest, bend);
                    strongest = Mathf.Max(strongest, bend);
                    counted++;
                }

                Assert.Greater(counted, 0, "found no head vertices to check");
                Assert.Greater(weakest, 0f, "part of the head does not move with the wind at all");
                Assert.Less(strongest / weakest, 1.3f,
                    "the flower head stretches too much; the petals will tear away from the disc");

                Object.DestroyImmediate(mesh);
            }
            finally
            {
                Object.DestroyImmediate(species);
            }
        }

        [Test]
        public void EverySpeciesKindBuildsAWellFormedMesh()
        {
            foreach (FoliageSpeciesKind kind in FoliageAssetLibrary.AllKinds)
            {
                var species = ScriptableObject.CreateInstance<FoliageSpecies>();
                try
                {
                    species.kind = kind;

                    Mesh mesh = FoliageMeshBuilder.Build(species);
                    AssertMeshIsWellFormed(mesh, kind.ToString());

                    // Mass placement is the whole point, so no stock species may
                    // quietly become expensive.
                    Assert.Less(mesh.triangles.Length / 3, 400, $"{kind} is unexpectedly heavy");

                    Object.DestroyImmediate(mesh);
                }
                finally
                {
                    Object.DestroyImmediate(species);
                }
            }
        }

        [Test]
        public void SingleStemmedSpeciesShareOneWindPhase()
        {
            // A grass or reed clump is separate blades that may sway out of step.
            // A clover or a sunflower is one plant, and parts of one plant that
            // move out of step come apart at the joints.
            foreach (FoliageSpeciesKind kind in new[] { FoliageSpeciesKind.Clover, FoliageSpeciesKind.Sunflower })
            {
                var species = ScriptableObject.CreateInstance<FoliageSpecies>();
                try
                {
                    species.kind = kind;

                    Mesh mesh = FoliageMeshBuilder.Build(species);

                    var phases = new HashSet<float>();
                    foreach (Color color in mesh.colors)
                    {
                        phases.Add(color.a);
                    }

                    Assert.AreEqual(1, phases.Count, $"{kind} must sway with a single wind phase");

                    Object.DestroyImmediate(mesh);
                }
                finally
                {
                    Object.DestroyImmediate(species);
                }
            }
        }

        [Test]
        public void SameSeedProducesIdenticalGeometry()
        {
            var a = ScriptableObject.CreateInstance<FoliageSpecies>();
            var b = ScriptableObject.CreateInstance<FoliageSpecies>();
            try
            {
                a.meshSeed = 4242;
                b.meshSeed = 4242;

                Mesh meshA = FoliageMeshBuilder.Build(a);
                Mesh meshB = FoliageMeshBuilder.Build(b);

                Assert.AreEqual(meshA.vertexCount, meshB.vertexCount);
                Vector3[] va = meshA.vertices;
                Vector3[] vb = meshB.vertices;

                for (int i = 0; i < va.Length; i++)
                {
                    Assert.AreEqual(va[i], vb[i], $"vertex {i} differs between two builds with the same seed");
                }

                Object.DestroyImmediate(meshA);
                Object.DestroyImmediate(meshB);
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }
    }

    public class FoliageFieldBuildTests
    {
        private GameObject _ground;
        private GameObject _fieldObject;

        [SetUp]
        public void SetUp()
        {
            // Default plane primitive is 10x10 m and carries a MeshCollider,
            // which is exactly what the scatterer raycasts against.
            _ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _ground.name = "CI Ground";
            _ground.transform.position = Vector3.zero;
            _ground.transform.localScale = new Vector3(5f, 1f, 5f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_fieldObject != null)
            {
                Object.DestroyImmediate(_fieldObject);
            }

            if (_ground != null)
            {
                Object.DestroyImmediate(_ground);
            }
        }

        [OneTimeTearDown]
        public void DeleteGeneratedAssets()
        {
            if (AssetDatabase.IsValidFolder("Assets/SabaProps"))
            {
                AssetDatabase.DeleteAsset("Assets/SabaProps");
            }
        }

        private FoliageField CreateField(FoliageOutputMode mode)
        {
            Material material = FoliageAssetLibrary.CreateOrLoadDefaultMaterial();
            Assert.IsNotNull(material);

            FoliageSpecies grass =
                FoliageAssetLibrary.CreateOrLoadDefaultSpecies(FoliageSpeciesKind.GrassClump, material);
            Assert.IsNotNull(grass);

            _fieldObject = new GameObject("CI Foliage Field");
            var field = _fieldObject.AddComponent<FoliageField>();

            field.shape = FoliageAreaShape.Rectangle;
            field.size = new Vector2(8f, 8f);
            field.density = 4f;
            field.seed = 1234;
            field.chunkSize = 4f;
            field.raycastHeight = 10f;
            field.raycastDistance = 40f;
            field.outputMode = mode;
            field.species.Add(grass);

            return field;
        }

        [Test]
        public void GpuInstanced_CreatesOneRendererPerInstance()
        {
            FoliageField field = CreateField(FoliageOutputMode.GpuInstanced);

            FoliageBuildStats stats = FoliageFieldBuilder.Build(field);

            Assert.IsNotNull(stats, "build produced no stats");
            Assert.Greater(stats.instanceCount, 0, "nothing was placed");
            Assert.AreEqual(stats.instanceCount, stats.rendererCount);
            Assert.Greater(stats.chunkCount, 0);

            var renderers = _fieldObject.GetComponentsInChildren<MeshRenderer>();
            Assert.AreEqual(stats.rendererCount, renderers.Length);

            foreach (MeshRenderer renderer in renderers)
            {
                Assert.IsNotNull(renderer.sharedMaterial);
                Assert.AreEqual(ShadowCastingMode.Off, renderer.shadowCastingMode,
                    "grass should not cast shadows by default");
                Assert.AreEqual(LightProbeUsage.BlendProbes, renderer.lightProbeUsage);
            }

            // Every instance must share one mesh, or there is no instancing.
            var meshes = new HashSet<Mesh>();
            foreach (MeshFilter filter in _fieldObject.GetComponentsInChildren<MeshFilter>())
            {
                meshes.Add(filter.sharedMesh);
            }

            Assert.AreEqual(1, meshes.Count, "instanced mode must reuse a single shared mesh");
        }

        [Test]
        public void MergedChunks_CollapsesRenderersIntoChunks()
        {
            FoliageField field = CreateField(FoliageOutputMode.MergedChunks);

            FoliageBuildStats stats = FoliageFieldBuilder.Build(field);

            Assert.IsNotNull(stats, "build produced no stats");
            Assert.Greater(stats.instanceCount, 0, "nothing was placed");
            Assert.Less(stats.rendererCount, stats.instanceCount,
                "merging should produce fewer renderers than instances");
            Assert.AreEqual(stats.chunkCount, stats.rendererCount,
                "one merged renderer per chunk is expected for a single species");

            foreach (MeshFilter filter in _fieldObject.GetComponentsInChildren<MeshFilter>())
            {
                Assert.IsNotNull(filter.sharedMesh);
                Assert.Greater(filter.sharedMesh.vertexCount, 0);

                var uv3 = new List<Vector4>();
                filter.sharedMesh.GetUVs(3, uv3);
                Assert.AreEqual(filter.sharedMesh.vertexCount, uv3.Count,
                    "merged mesh lost its UV3 channel");
            }
        }

        [Test]
        public void Clear_RemovesEverythingItGenerated()
        {
            FoliageField field = CreateField(FoliageOutputMode.GpuInstanced);

            Assert.IsNotNull(FoliageFieldBuilder.Build(field));
            Assert.Greater(_fieldObject.GetComponentsInChildren<MeshRenderer>().Length, 0);

            FoliageFieldBuilder.Clear(field);

            Assert.AreEqual(0, _fieldObject.GetComponentsInChildren<MeshRenderer>().Length,
                "Clear left renderers behind");
            Assert.IsNull(field.generatedRoot);
        }

        [Test]
        public void Build_IsDeterministicForAGivenSeed()
        {
            FoliageField field = CreateField(FoliageOutputMode.GpuInstanced);

            FoliageBuildStats first = FoliageFieldBuilder.Build(field);
            Assert.IsNotNull(first);

            var firstPositions = new List<Vector3>();
            foreach (MeshFilter filter in _fieldObject.GetComponentsInChildren<MeshFilter>())
            {
                firstPositions.Add(filter.transform.position);
            }

            FoliageBuildStats second = FoliageFieldBuilder.Build(field);
            Assert.IsNotNull(second);
            Assert.AreEqual(first.instanceCount, second.instanceCount);

            var secondPositions = new List<Vector3>();
            foreach (MeshFilter filter in _fieldObject.GetComponentsInChildren<MeshFilter>())
            {
                secondPositions.Add(filter.transform.position);
            }

            for (int i = 0; i < firstPositions.Count; i++)
            {
                Assert.AreEqual(firstPositions[i].x, secondPositions[i].x, 1e-4f);
                Assert.AreEqual(firstPositions[i].z, secondPositions[i].z, 1e-4f);
            }
        }

        [Test]
        public void AbsentSpecies_IsNotPlacedForItsSeason()
        {
            // An annual marked Absent is gone for that part of the year. The
            // rest of the field has to carry on as if it had never been listed,
            // rather than the field placing fewer plants overall.
            FoliageField field = CreateField(FoliageOutputMode.GpuInstanced);

            var sunflower = ScriptableObject.CreateInstance<FoliageSpecies>();
            sunflower.name = "CI Absent Sunflower";
            sunflower.kind = FoliageSpeciesKind.Sunflower;
            sunflower.material = FoliageAssetLibrary.CreateOrLoadDefaultMaterial();
            sunflower.season = FoliageSeason.WinterSnow;
            sunflower.seasonPalette.winterSnow.appearance = SeasonAppearance.Absent;
            sunflower.placementWeight = 1f;

            try
            {
                field.species.Add(sunflower);

                FoliageBuildStats stats = FoliageFieldBuilder.Build(field);

                Assert.IsNotNull(stats);
                Assert.Greater(stats.instanceCount, 0,
                    "the absent species took the whole field down with it");

                foreach (MeshFilter filter in field.GetComponentsInChildren<MeshFilter>())
                {
                    Assert.AreNotEqual(sunflower.name, filter.gameObject.name,
                        "a species marked Absent for its season was placed anyway");
                }
            }
            finally
            {
                Object.DestroyImmediate(sunflower);
            }
        }
    }

    /// <summary>
    /// The sample scene is the package's first impression, so it is worth a test
    /// of its own: it exercises both output modes, both species and the ground
    /// raycast in one pass, and a broken one is the most visible failure the
    /// package can ship.
    /// </summary>
    public class FoliageSampleSceneTests
    {
        private Scene _scene;

        /// <summary>
        /// Built once for the whole fixture. Generating the demo writes a merged
        /// mesh asset per chunk per species across sixteen plots, and every one
        /// is an AssetDatabase import: doing that per test costs minutes.
        /// Nothing here modifies the scene, so one build serves them all.
        /// </summary>
        [OneTimeSetUp]
        public void BuildSampleScene()
        {
            _scene = FoliageSampleScene.Create();
        }

        [OneTimeTearDown]
        public void RestoreEmptyScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            if (AssetDatabase.IsValidFolder("Assets/SabaProps"))
            {
                AssetDatabase.DeleteAsset("Assets/SabaProps");
            }
        }

        [Test]
        public void SampleScene_IsBuiltAndSaved()
        {
            Scene scene = _scene;

            Assert.IsTrue(scene.IsValid(), "sample scene was not created");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(FoliageSampleScene.ScenePath),
                $"sample scene was not saved to {FoliageSampleScene.ScenePath}");

            // Every plot in the demo, whatever section it belongs to, has to have
            // placed something: an empty plot in a showcase reads as a broken
            // package, and one silently empty plot is easy to miss by eye.
            var fields = new List<FoliageField>(Object.FindObjectsOfType<FoliageField>());
            Assert.Greater(fields.Count, 8, "the demo should lay out a garden of plots, not one field");

            foreach (FoliageField field in fields)
            {
                AssertBuilt(field, field.outputMode);
            }
        }

        [Test]
        public void SampleScene_SectionsShowWhatTheyClaimTo()
        {
            Scene scene = _scene;

            // Section 1 varies only the species, so each plot must place exactly
            // one, and between them they must cover every kind the package has.
            GameObject singles = FindRootObject(scene, FoliageSampleScene.SingleSpeciesRoot);
            Assert.IsNotNull(singles, "the demo has no single-species section");

            var covered = new HashSet<string>();
            int plots = 0;

            foreach (FoliageField field in singles.GetComponentsInChildren<FoliageField>())
            {
                Assert.AreEqual(1, CountPlacedSpecies(field),
                    $"'{field.name}' is in the single-species section but places more than one species");

                covered.UnionWith(PlacedSpecies(field));
                plots++;
            }

            Assert.AreEqual(FoliageAssetLibrary.AllKinds.Length, plots,
                "the single-species section should have one plot per species kind");
            Assert.AreEqual(FoliageAssetLibrary.AllKinds.Length, covered.Count,
                "two single-species plots ended up showing the same species");

            // Section 4 exists to show mixing, so at least one of its plots has
            // to actually place more than one species.
            GameObject mixes = FindRootObject(scene, FoliageSampleScene.MixRoot);
            Assert.IsNotNull(mixes, "the demo has no combinations section");

            bool anyMixed = false;
            foreach (FoliageField field in mixes.GetComponentsInChildren<FoliageField>())
            {
                anyMixed |= CountPlacedSpecies(field) > 1;
            }

            Assert.IsTrue(anyMixed, "no plot in the combinations section actually mixes species");

            // Section 5 is the output-mode comparison: same settings, and the
            // merged plot has to come out with fewer renderers or it is not
            // demonstrating anything.
            GameObject output = FindRootObject(scene, FoliageSampleScene.OutputRoot);
            Assert.IsNotNull(output, "the demo has no output-mode section");

            FoliageField instanced = FindChildField(output, FoliageSampleScene.InstancedPlotName);
            FoliageField merged = FindChildField(output, FoliageSampleScene.MergedPlotName);

            Assert.AreEqual(FoliageOutputMode.GpuInstanced, instanced.outputMode);
            Assert.AreEqual(FoliageOutputMode.MergedChunks, merged.outputMode);

            Assert.AreEqual(instanced.lastBuildStats.instanceCount, merged.lastBuildStats.instanceCount,
                "the two output-mode plots must place the same instances to be comparable");
            Assert.Less(merged.lastBuildStats.rendererCount, instanced.lastBuildStats.rendererCount,
                "merging did not reduce the renderer count");
        }

        [Test]
        public void SampleScene_TerrainSectionFiltersBySlope()
        {
            Scene scene = _scene;

            GameObject terrain = FindRootObject(scene, FoliageSampleScene.TerrainRoot);
            Assert.IsNotNull(terrain, "the demo has no terrain section");

            var plots = new List<FoliageField>(terrain.GetComponentsInChildren<FoliageField>());
            Assert.Greater(plots.Count, 1, "the terrain section needs more than one kind of ground to compare");

            // The ramp is steeper than the sunflower's slope limit and inside the
            // grass one, so it must end up carrying fewer species than the flat
            // mound plot built from the same mix.
            FoliageField mound = plots.Find(field => field.name == "Mound");
            FoliageField ramp = plots.Find(field => field.name == "Ramp");

            Assert.IsNotNull(mound, "the terrain section has no mound plot");
            Assert.IsNotNull(ramp, "the terrain section has no ramp plot");

            Assert.Less(CountPlacedSpecies(ramp), CountPlacedSpecies(mound),
                "the ramp should carry fewer species than the mound: the slope filter is what it demonstrates");
        }

        [Test]
        public void SampleScene_GrowsOnSkinnedGround()
        {
            Scene scene = _scene;

            GameObject terrain = FindRootObject(scene, FoliageSampleScene.TerrainRoot);
            Assert.IsNotNull(terrain, "the demo has no terrain section");

            FoliageField plot = FindChildField(terrain, FoliageSampleScene.SkinnedPlotName);

            Assert.AreEqual(1, plot.skinnedGround.Count, "the skinned plot has no skinned ground assigned");
            SkinnedMeshRenderer ground = plot.skinnedGround[0];
            Assert.IsNotNull(ground, "the skinned ground reference is empty");
            Assert.IsNull(ground.GetComponent<Collider>(),
                "the skinned ground has a collider of its own, so it is not testing the bake path");

            Assert.IsNotNull(plot.lastBuildStats);
            Assert.Greater(plot.lastBuildStats.instanceCount, 0,
                "nothing grew on the skinned ground; the pose was not baked into a collider");

            // The skin lifts the surface clear of the flat plane, so foliage at
            // ground level would mean the rays fell through to the plane.
            //
            // Measured on vertices, not on transforms or bounds: a merged chunk's
            // transform sits at the chunk origin rather than at any instance, and
            // its bounds are padded for wind.
            float lowest = float.MaxValue;
            foreach (MeshFilter filter in plot.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                Transform t = filter.transform;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    lowest = Mathf.Min(lowest, t.TransformPoint(vertex).y);
                }
            }

            Assert.Less(lowest, float.MaxValue, "the skinned plot has no geometry to measure");
            Assert.Greater(lowest, 0.05f,
                "foliage landed at plane height, so the rays missed the skinned surface");
        }

        [Test]
        public void SampleScene_SeasonSectionVariesOnlyBySeason()
        {
            GameObject seasons = FindRootObject(_scene, FoliageSampleScene.SeasonRoot);
            Assert.IsNotNull(seasons, "the demo has no season section");

            var plots = new List<FoliageField>(seasons.GetComponentsInChildren<FoliageField>());
            Assert.AreEqual(FoliageAssetLibrary.AllSeasons.Length, plots.Count,
                "the season section should have one plot per season");

            var warmth = new List<float>();

            foreach (FoliageField plot in plots)
            {
                AssertBuilt(plot, FoliageOutputMode.MergedChunks);
                warmth.Add(MeanWarmth(plot));
            }

            // Same seed, same species, same ground: the seasons in which every
            // species is present must place the same plants in the same places.
            // The sunflower goes dormant in autumn rather than absent, so it is
            // still one of them -- it just has no petals.
            FoliageField spring = FindChildField(seasons, FoliageSeason.Spring.ToString());
            FoliageField summer = FindChildField(seasons, FoliageSeason.Summer.ToString());
            FoliageField autumn = FindChildField(seasons, FoliageSeason.Autumn.ToString());

            Assert.AreEqual(summer.lastBuildStats.instanceCount, spring.lastBuildStats.instanceCount,
                "spring and summer place every species, so they must place the same plants");
            Assert.AreEqual(summer.lastBuildStats.instanceCount, autumn.lastBuildStats.instanceCount,
                "a dormant species is still placed; autumn should differ from summer in what it grows, not where");

            // The sunflower is an annual and is marked absent for both winters,
            // so those plots have to come out with fewer plants -- and with the
            // same number as each other, since they differ only in colour.
            FoliageField snow = FindChildField(seasons, FoliageSeason.WinterSnow.ToString());
            FoliageField bare = FindChildField(seasons, FoliageSeason.WinterBare.ToString());

            Assert.Less(snow.lastBuildStats.instanceCount, summer.lastBuildStats.instanceCount,
                "the winter plots still grow sunflowers; Absent did not take effect");
            Assert.AreEqual(snow.lastBuildStats.instanceCount, bare.lastBuildStats.instanceCount,
                "the two winters place different plants, so they are not comparable");

            // Every plot has to look different from every other, or a season is
            // silently doing nothing.
            for (int i = 0; i < warmth.Count; i++)
            {
                for (int j = i + 1; j < warmth.Count; j++)
                {
                    Assert.Greater(Mathf.Abs(warmth[i] - warmth[j]), 0.01f,
                        $"'{plots[i].name}' and '{plots[j].name}' came out the same colour");
                }
            }
        }

        /// <summary>
        /// Mean red-minus-blue over everything a field grew. Enough to tell two
        /// seasons apart without asserting a particular shade.
        /// </summary>
        private static float MeanWarmth(FoliageField field)
        {
            float sum = 0f;
            int count = 0;

            foreach (MeshFilter filter in field.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                foreach (Color color in filter.sharedMesh.colors)
                {
                    sum += color.r - color.b;
                    count++;
                }
            }

            Assert.Greater(count, 0, $"'{field.name}' has no vertex colours to measure");
            return sum / count;
        }

        [Test]
        public void SampleScene_LeavesNoBakedCollidersBehind()
        {
            // The bake proxies are hidden and unsaved, but a leak would put a
            // stray collider in the user's scene, so it is worth asserting.
            foreach (MeshCollider collider in Object.FindObjectsOfType<MeshCollider>())
            {
                Assert.IsFalse(collider.name.StartsWith("__SabaFoliageGround"),
                    $"a baked ground proxy survived the build: {collider.name}");
            }
        }

        /// <summary>
        /// Species a field actually placed, read from the names the builder gives
        /// its renderers.
        /// <para>
        /// Counting distinct meshes would not work: merged chunks produce one
        /// mesh per chunk per species, so a single-species plot split into four
        /// chunks owns four meshes. The renderer name is the species in both
        /// output modes.
        /// </para>
        /// </summary>
        private static HashSet<string> PlacedSpecies(FoliageField field)
        {
            var names = new HashSet<string>();
            foreach (MeshFilter filter in field.GetComponentsInChildren<MeshFilter>())
            {
                names.Add(filter.gameObject.name);
            }

            return names;
        }

        private static int CountPlacedSpecies(FoliageField field)
        {
            return PlacedSpecies(field).Count;
        }

        private static FoliageField FindChildField(GameObject root, string name)
        {
            foreach (FoliageField field in root.GetComponentsInChildren<FoliageField>())
            {
                if (field.name == name)
                {
                    return field;
                }
            }

            Assert.Fail($"'{name}' is missing from '{root.name}'");
            return null;
        }

        [Test]
        public void SampleScene_HasGroundToStandOn()
        {
            Scene scene = _scene;

            GameObject ground = FindRootObject(scene, FoliageSampleScene.GroundRoot);
            Assert.IsNotNull(ground, "the demo has no ground object");
            Assert.Greater(ground.GetComponentsInChildren<Collider>().Length, 0,
                "ground needs colliders or the scatterer has nothing to raycast against");

            Assert.IsNotNull(Camera.main, "the demo has no camera to look through");
        }

        [Test]
        public void SampleScene_MatchesTheVrchatSdkThatIsInstalled()
        {
            Scene scene = _scene;
            GameObject world = FindRootObject(scene, FoliageVrcWorld.WorldObjectName);

            if (!FoliageVrcWorld.IsSdkPresent)
            {
                Assert.IsNull(world,
                    "no VRChat Worlds SDK is installed, so the demo must not fabricate a world root");
                return;
            }

            Assert.IsNotNull(world, "the Worlds SDK is installed but the demo has no world root");

            Transform spawn = world.transform.Find(FoliageVrcWorld.SpawnObjectName);
            Assert.IsNotNull(spawn, "the world root has no spawn point");

            Component descriptor = null;
            foreach (Component component in world.GetComponents<Component>())
            {
                if (component != null && component.GetType().FullName == FoliageVrcWorld.DescriptorTypeName)
                {
                    descriptor = component;
                }
            }

            Assert.IsNotNull(descriptor, $"{FoliageVrcWorld.DescriptorTypeName} was not added");

            // FoliageVrcWorld assigns these by reflection, so a member the SDK
            // has renamed would leave the descriptor silently unconfigured and
            // the world unspawnable. Read them back the same way.
            FieldInfo spawnsField = descriptor.GetType()
                .GetField("spawns", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(spawnsField, "VRCSceneDescriptor no longer has a 'spawns' field");

            var spawns = spawnsField.GetValue(descriptor) as Transform[];
            Assert.IsNotNull(spawns, "'spawns' was not assigned");
            Assert.Contains(spawn, spawns, "the spawn point is not registered on the descriptor");
        }

        private static GameObject FindRootObject(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            return null;
        }

        private static void AssertBuilt(FoliageField field, FoliageOutputMode expectedMode)
        {
            Assert.AreEqual(expectedMode, field.outputMode, $"'{field.name}' uses the wrong output mode");
            Assert.IsNotNull(field.lastBuildStats, $"'{field.name}' was never built");
            Assert.Greater(field.lastBuildStats.instanceCount, 0, $"'{field.name}' placed nothing");

            var renderers = field.GetComponentsInChildren<MeshRenderer>();
            Assert.AreEqual(field.lastBuildStats.rendererCount, renderers.Length);

            foreach (MeshRenderer renderer in renderers)
            {
                Assert.IsNotNull(renderer.sharedMaterial, $"'{field.name}' produced a renderer with no material");
            }

            foreach (MeshFilter filter in field.GetComponentsInChildren<MeshFilter>())
            {
                Assert.IsNotNull(filter.sharedMesh, $"'{field.name}' produced a renderer with no mesh");
            }
        }
    }
}
