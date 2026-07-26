using System.Collections.Generic;
using NUnit.Framework;
using SabaProps.Foliage;
using SabaProps.Foliage.Editors;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

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
                foreach (UnityEditor.Rendering.ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
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
}
