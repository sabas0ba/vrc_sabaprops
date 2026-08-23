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
    }

    /// <summary>
    /// The sample scene is the package's first impression, so it is worth a test
    /// of its own: it exercises both output modes, both species and the ground
    /// raycast in one pass, and a broken one is the most visible failure the
    /// package can ship.
    /// </summary>
    public class FoliageSampleSceneTests
    {
        [TearDown]
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
            Scene scene = FoliageSampleScene.Create();

            Assert.IsTrue(scene.IsValid(), "sample scene was not created");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(FoliageSampleScene.ScenePath),
                $"sample scene was not saved to {FoliageSampleScene.ScenePath}");

            FoliageField meadow = FindField(scene, FoliageSampleScene.MeadowName);
            FoliageField clearing = FindField(scene, FoliageSampleScene.ClearingName);

            AssertBuilt(meadow, FoliageOutputMode.GpuInstanced);
            AssertBuilt(clearing, FoliageOutputMode.MergedChunks);

            // The meadow mixes grass and sunflowers; one shared mesh per species
            // is what makes each of them a single instancing batch.
            var meadowMeshes = new HashSet<Mesh>();
            foreach (MeshFilter filter in meadow.GetComponentsInChildren<MeshFilter>())
            {
                meadowMeshes.Add(filter.sharedMesh);
            }

            Assert.AreEqual(2, meadowMeshes.Count,
                "the meadow should place exactly two species, each with one shared mesh");

            Assert.Less(clearing.lastBuildStats.rendererCount, clearing.lastBuildStats.instanceCount,
                "the clearing should merge instances into fewer renderers");
        }

        [Test]
        public void SampleScene_HasGroundToStandOn()
        {
            Scene scene = FoliageSampleScene.Create();

            GameObject ground = FindRootObject(scene, "Ground");
            Assert.IsNotNull(ground, "the demo has no ground object");
            Assert.Greater(ground.GetComponentsInChildren<Collider>().Length, 0,
                "ground needs colliders or the scatterer has nothing to raycast against");

            Assert.IsNotNull(Camera.main, "the demo has no camera to look through");
        }

        [Test]
        public void SampleScene_MatchesTheVrchatSdkThatIsInstalled()
        {
            Scene scene = FoliageSampleScene.Create();
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

        private static FoliageField FindField(Scene scene, string name)
        {
            GameObject go = FindRootObject(scene, name);
            Assert.IsNotNull(go, $"'{name}' is missing from the sample scene");

            var field = go.GetComponent<FoliageField>();
            Assert.IsNotNull(field, $"'{name}' has no FoliageField component");
            return field;
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
