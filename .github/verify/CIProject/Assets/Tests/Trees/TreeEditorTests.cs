using System.Collections.Generic;
using NUnit.Framework;
using SabaProps.Foliage;
using SabaProps.Foliage.Editors;
using SabaProps.Trees.Editors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SabaProps.Trees.CITests
{
    public sealed class TreeEditorTests
    {
        [Test]
        public void TreeFieldAuthoringComponentIsExcludedFromBuilds()
        {
            var gameObject = new GameObject("Tree Field");
            try
            {
                TreeField field = gameObject.AddComponent<TreeField>();
                Assert.AreNotEqual(
                    0,
                    (int)(field.hideFlags & HideFlags.DontSaveInBuild),
                    "TreeField must not be included in a world build");
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ForestLoadSampleProvidesThreeComparableDensitySteps()
        {
            Assert.AreEqual(64, TreeBundledDemo.LoadGroupSize);
            Assert.AreEqual(3, TreeBundledDemo.LoadGroupCount);
            Assert.AreEqual(192, TreeBundledDemo.LoadSampleTreeCount);
            Assert.AreEqual(576, TreeBundledDemo.LoadSampleRendererCount);
        }

        [Test]
        public void BundledSamplesGenerateDistributionAssets()
        {
            try
            {
                TreeBundledDemo.Create();
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    TreeBundledDemo.LoadScenePath));
                Scene treeScene = EditorSceneManager.OpenScene(
                    TreeBundledDemo.LoadScenePath);
                Assert.IsTrue(treeScene.IsValid());

                LODGroup[] groups = Object.FindObjectsOfType<LODGroup>();
                Assert.AreEqual(TreeBundledDemo.LoadSampleTreeCount, groups.Length);
                int rendererCount = 0;
                foreach (LODGroup group in groups)
                {
                    foreach (LOD lod in group.GetLODs())
                    {
                        rendererCount += lod.renderers.Length;
                    }
                }
                Assert.AreEqual(
                    TreeBundledDemo.LoadSampleRendererCount,
                    rendererCount);

                const string sampleMeshPath =
                    "Assets/SabaProps/TreesBundledDemo/Assets/JapaneseZelkova_LOD0.asset";
                string meshGuid = AssetDatabase.AssetPathToGUID(sampleMeshPath);
                string sceneGuid = AssetDatabase.AssetPathToGUID(
                    TreeBundledDemo.LoadScenePath);
                Assert.IsNotEmpty(meshGuid);
                Assert.IsNotEmpty(sceneGuid);

                TreeBundledDemo.Create();
                Assert.AreEqual(meshGuid,
                    AssetDatabase.AssetPathToGUID(sampleMeshPath),
                    "bundled mesh regeneration must preserve its GUID");
                Assert.AreEqual(sceneGuid,
                    AssetDatabase.AssetPathToGUID(TreeBundledDemo.LoadScenePath),
                    "bundled scene regeneration must preserve its GUID");

                FoliageBundledDemo.GenerateForDistribution();
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    FoliageBundledDemo.ScenePath));
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    FoliageBundledDemo.SpeciesScenePath));
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    FoliageBundledDemo.LoadScenePath));

                Scene vineScene = EditorSceneManager.OpenScene(
                    FoliageBundledDemo.ScenePath);
                Assert.IsTrue(vineScene.IsValid());
                SurfaceVine slopeVine = null;
                foreach (SurfaceVine candidate in Object.FindObjectsOfType<SurfaceVine>())
                {
                    if (candidate.name.Contains("Floor Slope Wall"))
                    {
                        slopeVine = candidate;
                        break;
                    }
                }
                Assert.IsNotNull(slopeVine);
                bool foundSlopeNode = false;
                foreach (SurfaceGrowthNode node in slopeVine.generatedGraph.Nodes)
                {
                    foundSlopeNode |= node.normal.y > 0.75f
                        && node.normal.y < 0.98f
                        && Mathf.Abs(node.normal.z) > 0.20f;
                }
                Assert.IsTrue(foundSlopeNode,
                    "slope vine did not retain nodes on the inclined surface");

                Scene foliageLoadScene = EditorSceneManager.OpenScene(
                    FoliageBundledDemo.LoadScenePath);
                Assert.IsTrue(foliageLoadScene.IsValid());
                GameObject loadFields = GameObject.Find("GPU Instanced Patch Fields");
                Assert.IsNotNull(loadFields);
                MeshRenderer[] loadRenderers =
                    loadFields.GetComponentsInChildren<MeshRenderer>();
                Assert.AreEqual(
                    FoliageBundledDemo.LoadSampleRendererCount,
                    loadRenderers.Length);
                var loadMaterials = new HashSet<Material>();
                foreach (MeshRenderer renderer in loadRenderers)
                {
                    loadMaterials.Add(renderer.sharedMaterial);
                }
                Assert.AreEqual(1, loadMaterials.Count,
                    "load fields should share one instanced material");
                foreach (Material material in loadMaterials)
                {
                    Assert.AreEqual(
                        0f,
                        material.GetFloat(FoliageShaderContract.DistanceFadeProperty),
                        1e-6f,
                        "the load scene must show its authored plant density");
                    Assert.IsFalse(material.IsKeywordEnabled(
                        FoliageShaderContract.DistanceFadeKeyword));
                }
            }
            finally
            {
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
            }
        }

        [TearDown]
        public void CleanGeneratedAssets()
        {
            foreach (TreeField field in Object.FindObjectsOfType<TreeField>())
            {
                Object.DestroyImmediate(field.gameObject);
            }

            foreach (LODGroup group in Object.FindObjectsOfType<LODGroup>())
            {
                if (group.name.EndsWith(" Tree"))
                {
                    Object.DestroyImmediate(group.gameObject);
                }
            }

            if (AssetDatabase.IsValidFolder(TreeAssetLibrary.RootFolder))
            {
                AssetDatabase.DeleteAsset(TreeAssetLibrary.RootFolder);
            }
        }

        [Test]
        public void EveryArchetypeBuildsThreeWellFormedLods()
        {
            foreach (TreeArchetype archetype in TreeAssetLibrary.AllArchetypes)
            {
                TreeSpecies species = CreateSpecies(archetype);
                try
                {
                    Mesh lod0 = TreeMeshBuilder.Build(species, 0);
                    Mesh lod1 = TreeMeshBuilder.Build(species, 1);
                    Mesh lod2 = TreeMeshBuilder.Build(species, 2);
                    try
                    {
                        AssertMesh(lod0, archetype + " LOD0");
                        AssertMesh(lod1, archetype + " LOD1");
                        AssertMesh(lod2, archetype + " LOD2");

                        Assert.Less(lod0.triangles.Length / 3, 100000,
                            archetype + " default LOD0 exceeds the triangle budget");

                        Assert.Greater(lod0.triangles.Length, lod1.triangles.Length,
                            archetype + " LOD1 should contain fewer triangles than LOD0");
                        Assert.Greater(lod1.triangles.Length, lod2.triangles.Length,
                            archetype + " LOD2 should contain fewer triangles than LOD1");
                    }
                    finally
                    {
                        Object.DestroyImmediate(lod0);
                        Object.DestroyImmediate(lod1);
                        Object.DestroyImmediate(lod2);
                    }
                }
                finally
                {
                    Object.DestroyImmediate(species);
                }
            }
        }

        [Test]
        public void BotanicalPresetsBuildDistinctWellFormedLods()
        {
            var vertexCounts = new HashSet<int>();
            foreach (TreeBotanicalPreset preset in TreeAssetLibrary.AllBotanicalPresets)
            {
                TreeSpecies species = CreateSpecies(preset);
                try
                {
                    Mesh lod0 = TreeMeshBuilder.Build(species, 0);
                    Mesh lod1 = TreeMeshBuilder.Build(species, 1);
                    Mesh lod2 = TreeMeshBuilder.Build(species, 2);
                    try
                    {
                        AssertMesh(lod0, preset + " LOD0");
                        AssertMesh(lod1, preset + " LOD1");
                        AssertMesh(lod2, preset + " LOD2");
                        Assert.Greater(lod0.triangles.Length, lod1.triangles.Length);
                        Assert.Greater(lod1.triangles.Length, lod2.triangles.Length);
                        Assert.Less(lod0.triangles.Length / 3, 100000,
                            preset + " default LOD0 exceeds the triangle budget");
                        vertexCounts.Add(lod0.vertexCount);
                    }
                    finally
                    {
                        Object.DestroyImmediate(lod0);
                        Object.DestroyImmediate(lod1);
                        Object.DestroyImmediate(lod2);
                    }
                }
                finally
                {
                    Object.DestroyImmediate(species);
                }
            }

            Assert.GreaterOrEqual(
                vertexCounts.Count,
                7,
                "botanical families should retain several distinct generated topologies");
        }

        [Test]
        public void BotanicalPresetsEncodeObservedBranchAndLeafArrangements()
        {
            TreeSpecies maple = CreateSpecies(TreeBotanicalPreset.JapaneseMaple);
            TreeSpecies cedar = CreateSpecies(TreeBotanicalPreset.JapaneseCedar);
            TreeSpecies birch = CreateSpecies(TreeBotanicalPreset.JapaneseWhiteBirch);
            TreeSpecies pine = CreateSpecies(TreeBotanicalPreset.JapaneseRedPine);
            TreeSpecies hinoki = CreateSpecies(TreeBotanicalPreset.HinokiCypress);
            TreeSpecies sakuraSpring = CreateSpecies(TreeBotanicalPreset.SomeiYoshinoSpring);
            TreeSpecies sakuraSummer = CreateSpecies(TreeBotanicalPreset.SomeiYoshinoSummer);
            TreeSpecies ginkgoSummer = CreateSpecies(TreeBotanicalPreset.GinkgoSummer);
            TreeSpecies ginkgoAutumn = CreateSpecies(TreeBotanicalPreset.GinkgoAutumn);
            try
            {
                Assert.AreEqual(TreeBranchArrangement.Opposite,
                    maple.structure.branchArrangement);
                Assert.AreEqual(TreeLeafArrangement.Opposite,
                    maple.appearance.leafArrangement);
                Assert.AreEqual(TreeBranchArrangement.Whorled,
                    cedar.structure.branchArrangement);
                Assert.AreEqual(TreeCrownShape.Pyramidal,
                    cedar.structure.crownShape);
                Assert.AreEqual(TreeLeafShape.Needle,
                    cedar.appearance.leafShape);
                Assert.Less(cedar.structure.branchDroop, 0.1f);
                Assert.Less(birch.structure.branchDroop, 0.15f);
                Assert.AreEqual(TreeLeafArrangement.FasciclePairs,
                    pine.appearance.leafArrangement);
                Assert.AreEqual(TreeCrownShape.OpenIrregular,
                    pine.structure.crownShape);
                Assert.AreEqual(TreeLeafShape.Scale,
                    hinoki.appearance.leafShape);
                Assert.AreEqual(TreeLeafArrangement.Opposite,
                    hinoki.appearance.leafArrangement);
                Assert.AreEqual(TreeLeafShape.Blossom,
                    sakuraSpring.appearance.leafShape);
                Assert.AreEqual(TreeLeafShape.Broad,
                    sakuraSummer.appearance.leafShape);
                Assert.AreEqual(sakuraSpring.meshSeed, sakuraSummer.meshSeed,
                    "seasonal Sakura variants should retain their branch structure");
                Assert.AreEqual(TreeLeafShape.Fan,
                    ginkgoSummer.appearance.leafShape);
                Assert.AreEqual(TreeLeafArrangement.Clustered,
                    ginkgoSummer.appearance.leafArrangement);
                Assert.AreEqual(ginkgoSummer.meshSeed, ginkgoAutumn.meshSeed,
                    "seasonal Ginkgo variants should retain their branch structure");
                Assert.LessOrEqual(sakuraSpring.lod.lod2ScreenHeight, 0.005f,
                    "distant trees should retain their final LOD instead of culling early");
            }
            finally
            {
                Object.DestroyImmediate(maple);
                Object.DestroyImmediate(cedar);
                Object.DestroyImmediate(birch);
                Object.DestroyImmediate(pine);
                Object.DestroyImmediate(hinoki);
                Object.DestroyImmediate(sakuraSpring);
                Object.DestroyImmediate(sakuraSummer);
                Object.DestroyImmediate(ginkgoSummer);
                Object.DestroyImmediate(ginkgoAutumn);
            }
        }

        [Test]
        public void SameSeedProducesIdenticalMesh()
        {
            TreeSpecies species = CreateSpecies(TreeArchetype.Broadleaf);
            try
            {
                Mesh first = TreeMeshBuilder.Build(species, 0);
                Mesh second = TreeMeshBuilder.Build(species, 0);
                try
                {
                    Assert.AreEqual(first.vertexCount, second.vertexCount);
                    Assert.AreEqual(first.triangles.Length, second.triangles.Length);

                    Vector3[] firstVertices = first.vertices;
                    Vector3[] secondVertices = second.vertices;
                    for (int i = 0; i < firstVertices.Length; i++)
                    {
                        Assert.AreEqual(firstVertices[i], secondVertices[i], "vertex " + i);
                    }

                    int[] firstTriangles = first.triangles;
                    int[] secondTriangles = second.triangles;
                    for (int i = 0; i < firstTriangles.Length; i++)
                    {
                        Assert.AreEqual(firstTriangles[i], secondTriangles[i], "index " + i);
                    }
                }
                finally
                {
                    Object.DestroyImmediate(first);
                    Object.DestroyImmediate(second);
                }
            }
            finally
            {
                Object.DestroyImmediate(species);
            }
        }

        [Test]
        public void ValidationClampsUnsafeApiValues()
        {
            TreeSpecies species = CreateSpecies(TreeArchetype.Broadleaf);
            try
            {
                species.structure.trunkLength = -1f;
                species.structure.radialSegments = 99;
                species.structure.maxDepth = 99;
                species.structure.branchAngle = -20f;
                species.structure.lengthDecay = 4f;
                species.structure.crookedness = 4f;
                species.structure.crownDensity = 4f;
                species.appearance.leafLength = 0f;
                species.appearance.foliageDepth = 99;
                species.appearance.windResponse = 4f;
                species.appearance.branchStiffness = 4f;
                species.lod.lod0ScreenHeight = 0f;
                species.lod.lod1ScreenHeight = 1f;
                species.lod.lod2ScreenHeight = 1f;
                species.placement.placementWeight = -1f;
                species.placement.minSpacing = -1f;
                species.placement.scaleRange = new Vector2(2f, 0f);
                species.placement.maxTilt = 90f;
                species.placement.alignToGroundNormal = 2f;
                species.placement.slopeLimits = new Vector2(95f, -5f);

                species.ValidateParameters();

                Assert.AreEqual(0.2f, species.structure.trunkLength, 1e-6f);
                Assert.AreEqual(12, species.structure.radialSegments);
                Assert.AreEqual(6, species.structure.maxDepth);
                Assert.AreEqual(5f, species.structure.branchAngle, 1e-6f);
                Assert.AreEqual(0.85f, species.structure.lengthDecay, 1e-6f);
                Assert.AreEqual(0.5f, species.structure.crookedness, 1e-6f);
                Assert.AreEqual(1.5f, species.structure.crownDensity, 1e-6f);
                Assert.AreEqual(0.01f, species.appearance.leafLength, 1e-6f);
                Assert.AreEqual(4, species.appearance.foliageDepth);
                Assert.AreEqual(2f, species.appearance.windResponse, 1e-6f);
                Assert.AreEqual(1f, species.appearance.branchStiffness, 1e-6f);
                Assert.AreEqual(0.03f, species.lod.lod0ScreenHeight, 1e-6f);
                Assert.AreEqual(0.02f, species.lod.lod1ScreenHeight, 1e-6f);
                Assert.AreEqual(0.01f, species.lod.lod2ScreenHeight, 1e-6f);
                Assert.AreEqual(0f, species.placement.placementWeight, 1e-6f);
                Assert.AreEqual(0f, species.placement.minSpacing, 1e-6f);
                Assert.AreEqual(new Vector2(0.001f, 2f),
                    species.placement.scaleRange);
                Assert.AreEqual(45f, species.placement.maxTilt, 1e-6f);
                Assert.AreEqual(1f,
                    species.placement.alignToGroundNormal, 1e-6f);
                Assert.AreEqual(new Vector2(0f, 90f),
                    species.placement.slopeLimits);
            }
            finally
            {
                Object.DestroyImmediate(species);
            }
        }

        [Test]
        public void WindDataKeepsTrunkRigidAndBranchesPivoted()
        {
            TreeSpecies species = CreateSpecies(TreeArchetype.Broadleaf);
            try
            {
                Mesh mesh = TreeMeshBuilder.Build(species, 0);
                try
                {
                    var uv0 = new List<Vector2>();
                    var uv3 = new List<Vector4>();
                    mesh.GetUVs(0, uv0);
                    mesh.GetUVs(FoliageShaderContract.WindDataUvChannel, uv3);

                    int rigidVertices = 0;
                    int flexibleVertices = 0;
                    var pivots = new HashSet<Vector3>();
                    var pivotHasRoot = new HashSet<Vector3>();
                    for (int i = 0; i < mesh.vertexCount; i++)
                    {
                        Assert.IsTrue(uv0[i].y >= 0f && uv0[i].y <= 1f,
                            "bend coordinate must stay in [0,1]");

                        Vector4 wind = uv3[i];
                        if (wind.w <= 0f)
                        {
                            rigidVertices++;
                            continue;
                        }

                        flexibleVertices++;
                        var pivot = new Vector3(wind.x, wind.y, wind.z);
                        pivots.Add(pivot);
                        if (uv0[i].y <= 1e-5f)
                        {
                            pivotHasRoot.Add(pivot);
                        }
                    }

                    Assert.Greater(rigidVertices, 0, "the trunk must not move");
                    Assert.Greater(flexibleVertices, 0, "branches must carry wind data");
                    Assert.Greater(pivots.Count, 1, "primary branches need independent pivots");
                    Assert.AreEqual(pivots.Count, pivotHasRoot.Count,
                        "every primary branch subtree needs vertices at bend zero");
                }
                finally
                {
                    Object.DestroyImmediate(mesh);
                }
            }
            finally
            {
                Object.DestroyImmediate(species);
            }
        }

        [Test]
        public void WindCanBeDisabledPerSpecies()
        {
            TreeSpecies species = CreateSpecies(TreeBotanicalPreset.JapaneseZelkova);
            species.appearance.windEnabled = false;
            try
            {
                Mesh mesh = TreeMeshBuilder.Build(species, 0);
                try
                {
                    var uv3 = new List<Vector4>();
                    mesh.GetUVs(FoliageShaderContract.WindDataUvChannel, uv3);
                    Assert.AreEqual(mesh.vertexCount, uv3.Count);
                    foreach (Vector4 wind in uv3)
                    {
                        Assert.AreEqual(0f, wind.w, 1e-6f);
                    }
                }
                finally
                {
                    Object.DestroyImmediate(mesh);
                }
            }
            finally
            {
                Object.DestroyImmediate(species);
            }
        }

        [Test]
        public void SceneTreeUsesLodGroupShadowsAndNoDistanceShrink()
        {
            TreeSpecies species = TreeAssetLibrary.CreateOrLoadSpecies(TreeArchetype.Broadleaf);
            GameObject root = TreeAssetLibrary.CreateLodGroup(species);

            Assert.IsNotNull(root);
            LODGroup group = root.GetComponent<LODGroup>();
            Assert.IsNotNull(group);
            Assert.AreEqual(3, group.GetLODs().Length);

            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>();
            Assert.AreEqual(3, renderers.Length);
            foreach (MeshRenderer renderer in renderers)
            {
                Assert.AreEqual(ShadowCastingMode.On, renderer.shadowCastingMode);
                Assert.IsTrue(renderer.receiveShadows);
                Assert.AreEqual(species.material, renderer.sharedMaterial);
            }

            Assert.AreEqual(0f,
                species.material.GetFloat(FoliageShaderContract.DistanceFadeProperty), 1e-6f);
            Assert.IsFalse(species.material.IsKeywordEnabled(
                FoliageShaderContract.DistanceFadeKeyword));
            Assert.Greater(species.material.GetFloat("_WindStrength"), 0f,
                "the default tree material must enable shader wind");
        }

        [Test]
        public void TreeFieldBuildIsDeterministicAndReusesSpeciesLods()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "TreeField Test Ground";
            var fieldObject = new GameObject("TreeField Test");
            TreeField field = fieldObject.AddComponent<TreeField>();
            TreeSpecies species =
                TreeAssetLibrary.CreateOrLoadSpecies(TreeArchetype.Broadleaf);

            field.size = new Vector2(8f, 8f);
            field.density = 0.25f;
            field.seed = 4815;
            field.maxInstances = 100;
            field.groundOffset = 0f;
            field.species.Add(species);

            try
            {
                TreeBuildStats firstStats = TreeFieldBuilder.Build(field);
                Assert.IsNotNull(firstStats);
                Assert.Greater(firstStats.instanceCount, 1);
                Assert.AreEqual(
                    firstStats.instanceCount * 3, firstStats.rendererCount);
                Assert.AreEqual(firstStats.instanceCount,
                    field.generatedRoot.childCount);

                Vector3[] firstPositions = CapturePositions(field.generatedRoot);
                var sharedMeshes = new HashSet<Mesh>();
                foreach (MeshFilter filter in
                    field.generatedRoot.GetComponentsInChildren<MeshFilter>())
                {
                    sharedMeshes.Add(filter.sharedMesh);
                }
                Assert.AreEqual(3, sharedMeshes.Count,
                    "one species must reuse exactly three LOD mesh assets");

                foreach (LODGroup group in
                    field.generatedRoot.GetComponentsInChildren<LODGroup>())
                {
                    Assert.AreEqual(3, group.GetLODs().Length);
                }
                foreach (MeshRenderer renderer in
                    field.generatedRoot.GetComponentsInChildren<MeshRenderer>())
                {
                    Assert.AreEqual(
                        ShadowCastingMode.On, renderer.shadowCastingMode);
                    Assert.IsTrue(renderer.receiveShadows);
                }

                AssertMinimumSpacing(
                    firstPositions, species.placement.minSpacing);

                TreeBuildStats secondStats = TreeFieldBuilder.Build(field);
                Assert.IsNotNull(secondStats);
                Assert.AreEqual(firstStats.instanceCount, secondStats.instanceCount);
                CollectionAssert.AreEqual(
                    firstPositions, CapturePositions(field.generatedRoot));

                TreeFieldBuilder.Clear(field);
                Assert.IsNull(field.generatedRoot);
                Assert.IsNull(field.lastBuildStats);
                Assert.IsNull(field.transform.Find(TreeField.GeneratedRootName));
            }
            finally
            {
                Object.DestroyImmediate(fieldObject);
                Object.DestroyImmediate(ground);
            }
        }

        private static TreeSpecies CreateSpecies(TreeArchetype archetype)
        {
            TreeSpecies species = ScriptableObject.CreateInstance<TreeSpecies>();
            species.name = archetype.ToString();
            species.ApplyArchetypePreset(archetype);
            return species;
        }

        private static TreeSpecies CreateSpecies(TreeBotanicalPreset preset)
        {
            TreeSpecies species = ScriptableObject.CreateInstance<TreeSpecies>();
            species.name = preset.ToString();
            species.ApplyBotanicalPreset(preset);
            return species;
        }

        private static Vector3[] CapturePositions(Transform generatedRoot)
        {
            var positions = new Vector3[generatedRoot.childCount];
            for (int i = 0; i < generatedRoot.childCount; i++)
            {
                positions[i] = generatedRoot.GetChild(i).position;
            }
            return positions;
        }

        private static void AssertMinimumSpacing(
            IReadOnlyList<Vector3> positions, float minimum)
        {
            float minimumSquared = minimum * minimum;
            for (int i = 0; i < positions.Count; i++)
            {
                for (int j = i + 1; j < positions.Count; j++)
                {
                    Vector3 delta = positions[i] - positions[j];
                    float planarSquared =
                        delta.x * delta.x + delta.z * delta.z;
                    Assert.GreaterOrEqual(
                        planarSquared, minimumSquared - 1e-4f,
                        $"instances {i} and {j} violate minimum spacing");
                }
            }
        }

        private static void AssertMesh(Mesh mesh, string label)
        {
            Assert.IsNotNull(mesh, label);
            Assert.Greater(mesh.vertexCount, 0, label + " has no vertices");
            Assert.Greater(mesh.triangles.Length, 0, label + " has no triangles");
            Assert.AreEqual(0, mesh.triangles.Length % 3, label + " index count");
            Assert.AreEqual(mesh.vertexCount, mesh.normals.Length, label + " normals");
            Assert.AreEqual(mesh.vertexCount, mesh.colors.Length, label + " colors");

            var uv0 = new List<Vector2>();
            var uv3 = new List<Vector4>();
            mesh.GetUVs(0, uv0);
            mesh.GetUVs(FoliageShaderContract.WindDataUvChannel, uv3);
            Assert.AreEqual(mesh.vertexCount, uv0.Count, label + " UV0");
            Assert.AreEqual(mesh.vertexCount, uv3.Count, label + " UV3");

            foreach (Vector3 vertex in mesh.vertices)
            {
                Assert.IsFalse(float.IsNaN(vertex.x) || float.IsInfinity(vertex.x), label + " vertex.x");
                Assert.IsFalse(float.IsNaN(vertex.y) || float.IsInfinity(vertex.y), label + " vertex.y");
                Assert.IsFalse(float.IsNaN(vertex.z) || float.IsInfinity(vertex.z), label + " vertex.z");
            }

            foreach (int index in mesh.triangles)
            {
                Assert.IsTrue(index >= 0 && index < mesh.vertexCount, label + " index range");
            }
        }
    }
}
