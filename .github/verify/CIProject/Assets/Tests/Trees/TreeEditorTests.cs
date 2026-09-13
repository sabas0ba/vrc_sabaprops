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

                EditorSceneManager.OpenScene(TreeBundledDemo.SeasonalScenePath);
                LODGroup[] seasonalTrees = Object.FindObjectsOfType<LODGroup>();
                Assert.AreEqual(37, seasonalTrees.Length);
                for (int i = 0; i < seasonalTrees.Length; i++)
                for (int j = i + 1; j < seasonalTrees.Length; j++)
                {
                    MeshFilter first = seasonalTrees[i].GetComponentInChildren<MeshFilter>();
                    MeshFilter second = seasonalTrees[j].GetComponentInChildren<MeshFilter>();
                    if (first.sharedMesh == second.sharedMesh) continue;
                    foreach (Renderer a in seasonalTrees[i].GetComponentsInChildren<Renderer>())
                    foreach (Renderer b in seasonalTrees[j].GetComponentsInChildren<Renderer>())
                        Assert.IsFalse(a.bounds.Intersects(b.bounds),
                            "Different species/seasons must not overlap in the comparison scene: "
                            + seasonalTrees[i].name + " / " + seasonalTrees[j].name);
                }

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

        [TestCase(TreeBotanicalPreset.JapaneseMaple)]
        [TestCase(TreeBotanicalPreset.JapaneseWhiteBirch)]
        public void LowerBoleTapersWithoutAPinchedBand(
            TreeBotanicalPreset preset)
        {
            TreeSpecies species = CreateSpecies(preset);
            try
            {
                Mesh mesh = TreeMeshBuilder.Build(species, 0);
                try
                {
                    Vector3[] vertices = mesh.vertices;
                    int sides = species.structure.radialSegments;
                    var radii = new float[3];
                    for (int ring = 0; ring < radii.Length; ring++)
                    {
                        Vector3 centre = Vector3.zero;
                        for (int side = 0; side < sides; side++)
                        {
                            centre += vertices[ring * sides + side];
                        }
                        centre /= sides;

                        for (int side = 0; side < sides; side++)
                        {
                            radii[ring] += Vector3.Distance(
                                centre,
                                vertices[ring * sides + side]);
                        }
                        radii[ring] /= sides;
                    }

                    Assert.GreaterOrEqual(radii[0] + 1e-5f, radii[1]);
                    Assert.GreaterOrEqual(radii[1] + 1e-5f, radii[2]);
                    Assert.GreaterOrEqual(radii[2], radii[0] * 0.70f,
                        "the lower quarter of the trunk must not collapse into a waist");
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
        public void BotanicalPresetsEncodeObservedBranchAndLeafArrangements()
        {
            TreeSpecies zelkova = CreateSpecies(TreeBotanicalPreset.JapaneseZelkova);
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
                Assert.AreEqual(TreeCrownShape.Rounded,
                    zelkova.structure.crownShape,
                    "managed Zelkova should use the volume-preserving rounded crown");
                Assert.AreEqual(1f, zelkova.structure.crownEnvelopeStrength);
                Assert.AreEqual(TreeBranchArrangement.Opposite,
                    maple.structure.branchArrangement);
                Assert.AreEqual(TreeLeafArrangement.Opposite,
                    maple.appearance.leafArrangement);
                Assert.AreEqual(1f, maple.structure.crownEnvelopeStrength,
                    "street-tree presets should keep branches inside their crown envelope");
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
                Assert.Less(pine.structure.crownEnvelopeStrength, 0.25f,
                    "open red-pine crowns should retain irregular growth");
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
                Assert.GreaterOrEqual(sakuraSpring.structure.branchCount, 4,
                    "seasonal Sakura needs a visible structural crown");
                Assert.AreEqual(2, sakuraSpring.appearance.foliageDepth,
                    "seasonal Sakura foliage should stay on terminal branches");
                Assert.AreEqual(TreeLeafShape.Fan,
                    ginkgoSummer.appearance.leafShape);
                Assert.AreEqual(TreeLeafArrangement.Clustered,
                    ginkgoSummer.appearance.leafArrangement);
                Assert.AreEqual(ginkgoSummer.meshSeed, ginkgoAutumn.meshSeed,
                    "seasonal Ginkgo variants should retain their branch structure");
                Assert.GreaterOrEqual(ginkgoSummer.structure.branchCount, 3,
                    "seasonal Ginkgo needs a visible structural crown");
                Assert.AreEqual(2, ginkgoSummer.appearance.foliageDepth,
                    "seasonal Ginkgo foliage should stay on terminal branches");
                Assert.LessOrEqual(sakuraSpring.lod.lod2ScreenHeight, 0.005f,
                    "distant trees should retain their final LOD instead of culling early");
            }
            finally
            {
                Object.DestroyImmediate(zelkova);
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
        public void CrownEnvelopeStrengthConstrainsUpwardBranchGrowth()
        {
            TreeSpecies species = CreateSpecies(TreeArchetype.Broadleaf);
            try
            {
                species.structure.crownShape = TreeCrownShape.Rounded;
                species.structure.branchAngle = 10f;
                species.structure.branchAngleJitter = 0f;
                species.structure.maxDepth = 5;
                species.structure.branchCount = 3;
                species.structure.lengthDecay = 0.82f;
                species.structure.trunkBranchStart = 0.20f;
                species.structure.crookedness = 0f;
                species.structure.tipUpturn = 0.4f;
                species.appearance.leafShape = TreeLeafShape.None;

                species.structure.crownEnvelopeStrength = 0f;
                Mesh freeGrowth = TreeMeshBuilder.Build(species, 0);
                species.structure.crownEnvelopeStrength = 1f;
                Mesh constrained = TreeMeshBuilder.Build(species, 0);
                try
                {
                    AssertMesh(freeGrowth, "free-growth crown");
                    AssertMesh(constrained, "constrained crown");
                    float freeMaximumY = float.MinValue;
                    foreach (Vector3 vertex in freeGrowth.vertices)
                    {
                        freeMaximumY = Mathf.Max(freeMaximumY, vertex.y);
                    }
                    float constrainedMaximumY = float.MinValue;
                    foreach (Vector3 vertex in constrained.vertices)
                    {
                        constrainedMaximumY = Mathf.Max(
                            constrainedMaximumY,
                            vertex.y);
                    }
                    Assert.Greater(
                        freeMaximumY,
                        constrainedMaximumY + 0.2f,
                        "the envelope should visibly trim upward branch growth");
                    Assert.LessOrEqual(
                        constrainedMaximumY / constrained.bounds.size.x,
                        freeMaximumY / freeGrowth.bounds.size.x,
                        "shaping should reduce the excessive vertical aspect ratio");
                }
                finally
                {
                    Object.DestroyImmediate(freeGrowth);
                    Object.DestroyImmediate(constrained);
                }
            }
            finally
            {
                Object.DestroyImmediate(species);
            }
        }

        [TestCase(TreeBotanicalPreset.JapaneseZelkova)]
        [TestCase(TreeBotanicalPreset.JapaneseMaple)]
        [TestCase(TreeBotanicalPreset.JapaneseCedar)]
        [TestCase(TreeBotanicalPreset.JapaneseWhiteBirch)]
        [TestCase(TreeBotanicalPreset.JapaneseRedPine)]
        [TestCase(TreeBotanicalPreset.HinokiCypress)]
        [TestCase(TreeBotanicalPreset.SomeiYoshinoSpring)]
        [TestCase(TreeBotanicalPreset.SomeiYoshinoSummer)]
        [TestCase(TreeBotanicalPreset.GinkgoSummer)]
        [TestCase(TreeBotanicalPreset.GinkgoAutumn)]
        public void CrownEnvelopePreservesReferenceBoundsVolume(
            TreeBotanicalPreset preset)
        {
            TreeSpecies species = CreateSpecies(preset);
            // The size reference precedes the explicitly requested widening.
            species.structure.crownRadialScale = 1f;
            species.structure.primaryBranchDeparture = 0f;
            try
            {
                float configuredStrength =
                    species.structure.crownEnvelopeStrength;
                float configuredVolumeScale =
                    species.structure.crownVolumeScale;
                species.structure.crownEnvelopeStrength = 0f;
                Mesh freeGrowth = TreeMeshBuilder.Build(species, 0);
                species.structure.crownEnvelopeStrength = configuredStrength;
                Mesh shaped = TreeMeshBuilder.Build(species, 0);
                try
                {
                    float baselineVolume = BoundsVolume(freeGrowth);
                    float shapedVolume = BoundsVolume(shaped);
                    float ratio = shapedVolume / baselineVolume;
                    Assert.GreaterOrEqual(
                        ratio,
                        configuredVolumeScale * 0.98f,
                        preset + " did not reach its configured bounds volume");
                    Assert.LessOrEqual(
                        ratio,
                        configuredVolumeScale * 1.02f,
                        preset + " exceeded its configured bounds volume");

                    float referenceRatio = shapedVolume
                        / PreEnvelopeBoundsVolume(preset);
                    Assert.GreaterOrEqual(
                        referenceRatio,
                        0.80f,
                        preset + " lost more than 20% of pre-envelope volume");
                    Assert.LessOrEqual(
                        referenceRatio,
                        1.20f,
                        preset + " gained more than 20% over pre-envelope volume");
                }
                finally
                {
                    Object.DestroyImmediate(freeGrowth);
                    Object.DestroyImmediate(shaped);
                }
            }
            finally
            {
                Object.DestroyImmediate(species);
            }
        }

        [TestCase(TreeBotanicalPreset.JapaneseWhiteBirch, 0)]
        [TestCase(TreeBotanicalPreset.JapaneseWhiteBirch, 1)]
        [TestCase(TreeBotanicalPreset.JapaneseWhiteBirch, 2)]
        [TestCase(TreeBotanicalPreset.JapaneseMaple, 0)]
        [TestCase(TreeBotanicalPreset.JapaneseMaple, 1)]
        [TestCase(TreeBotanicalPreset.JapaneseMaple, 2)]
        public void BarkColourIsContinuousAcrossBranchesAndCaps(TreeBotanicalPreset preset, int lod)
        {
            TreeSpecies species = CreateSpecies(preset);
            species.appearance.leafShape = TreeLeafShape.None;
            species.structure.crownEnvelopeStrength = 0f;
            Mesh mesh = null;
            try
            {
                mesh = TreeMeshBuilder.Build(species, lod);
                Vector3[] vertices = mesh.vertices;
                Color[] colors = mesh.colors;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Color expected = Color.Lerp(species.appearance.barkRootColor,
                        species.appearance.barkTipColor,
                        Mathf.Clamp01(vertices[i].y / species.structure.trunkLength * 0.35f));
                    Assert.AreEqual(expected.r, colors[i].r, 1e-5f, "bark red at " + i);
                    Assert.AreEqual(expected.g, colors[i].g, 1e-5f, "bark green at " + i);
                    Assert.AreEqual(expected.b, colors[i].b, 1e-5f, "bark blue at " + i);
                }
            }
            finally
            {
                if (mesh != null) Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(species);
            }
        }

        [TestCase(TreeBotanicalPreset.SomeiYoshinoSummer, 0)]
        [TestCase(TreeBotanicalPreset.SomeiYoshinoSummer, 1)]
        [TestCase(TreeBotanicalPreset.GinkgoSummer, 0)]
        [TestCase(TreeBotanicalPreset.GinkgoSummer, 1)]
        [TestCase(TreeBotanicalPreset.JapaneseMaple, 0)]
        [TestCase(TreeBotanicalPreset.JapaneseWhiteBirch, 0)]
        public void CrownShapingKeepsBranchesAscendingAndVerticallyDistributed(
            TreeBotanicalPreset preset, int seedOffset)
        {
            TreeSpecies species = CreateSpecies(preset);
            species.meshSeed += seedOffset;
            species.structure.branchDroop = 0f;
            species.appearance.leafShape = TreeLeafShape.None;
            Mesh mesh = null;
            try
            {
                mesh = TreeMeshBuilder.Build(species, 0);
                // Bark vertices are emitted in axial rings, with one random
                // alpha per branch (also used by its caps). Average each ring
                // to measure the generated centreline independently of taper.
                var branches = new List<List<Vector3>>();
                Vector3[] vertices = mesh.vertices;
                Color[] colors = mesh.colors;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (i == 0 || colors[i].a != colors[i - 1].a)
                        branches.Add(new List<Vector3>());
                    branches[branches.Count - 1].Add(vertices[i]);
                }
                float lowestTip = float.MaxValue;
                float highestTip = float.MinValue;
                float widest = 0f;
                for (int branch = 1; branch < branches.Count; branch++)
                {
                    int sides = species.structure.radialSegments;
                    int segments = species.structure.segmentsPerBranch;
                    Assert.GreaterOrEqual(branches[branch].Count, sides * (segments + 1));
                    Vector3 previous = Vector3.zero;
                    for (int ring = 0; ring <= segments; ring++)
                    {
                        Vector3 centre = Vector3.zero;
                        for (int side = 0; side < sides; side++)
                            centre += branches[branch][ring * sides + side];
                        centre /= sides;
                        if (ring > 0)
                            Assert.GreaterOrEqual(centre.y, previous.y - 1e-4f,
                                preset + " has a downward structural segment");
                        previous = centre;
                    }
                    lowestTip = Mathf.Min(lowestTip, previous.y);
                    highestTip = Mathf.Max(highestTip, previous.y);
                    widest = Mathf.Max(widest,
                        new Vector2(previous.x, previous.z).magnitude * 2f);
                }
                Assert.Greater(highestTip - lowestTip,
                    widest / species.structure.crownRadialScale * 0.30f,
                    preset + " concentrates branch tips in a flat canopy");
            }
            finally
            {
                if (mesh != null) Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(species);
            }
        }

        [TestCase(TreeBotanicalPreset.SomeiYoshinoSummer)]
        [TestCase(TreeBotanicalPreset.GinkgoAutumn)]
        public void CrownSizeCorrectionKeepsTrunkCentrelineIdenticalAcrossLods(
            TreeBotanicalPreset preset)
        {
            TreeSpecies species = CreateSpecies(preset);
            var reference = new List<Vector3>();
            try
            {
                for (int lod = 0; lod < 3; lod++)
                {
                    Mesh mesh = TreeMeshBuilder.Build(species, lod);
                    try
                    {
                        Vector3[] vertices = mesh.vertices;
                        int sides = Mathf.Max(6, species.structure.radialSegments - lod * 2);
                        int segments = Mathf.Max(8, species.structure.segmentsPerBranch);
                        for (int ring = 0; ring <= segments; ring++)
                        {
                            Vector3 centre = Vector3.zero;
                            for (int side = 0; side < sides; side++)
                                centre += vertices[ring * sides + side];
                            centre /= sides;
                            if (lod == 0) reference.Add(centre);
                            else Assert.Less(Vector3.Distance(reference[ring], centre), 1e-4f,
                                preset + " trunk moves during LOD transition");
                        }
                    }
                    finally { Object.DestroyImmediate(mesh); }
                }
            }
            finally { Object.DestroyImmediate(species); }
        }

        [TestCase(TreeBotanicalPreset.SomeiYoshinoSummer)]
        [TestCase(TreeBotanicalPreset.GinkgoAutumn)]
        [TestCase(TreeBotanicalPreset.JapaneseMaple)]
        [TestCase(TreeBotanicalPreset.JapaneseWhiteBirch)]
        public void RadialSpreadWidensBranchesWithoutResizingTheTrunk(
            TreeBotanicalPreset preset)
        {
            TreeSpecies species = CreateSpecies(preset);
            species.appearance.leafShape = TreeLeafShape.None;
            Mesh narrow = null;
            Mesh wide = null;
            try
            {
                species.structure.crownRadialScale = 1f;
                narrow = TreeMeshBuilder.Build(species, 0);
                species.structure.crownRadialScale = 1.5f;
                wide = TreeMeshBuilder.Build(species, 0);
                Vector3[] original = narrow.vertices;
                Vector3[] expanded = wide.vertices;
                Assert.AreEqual(original.Length, expanded.Length,
                    "radial spread must not add branches or leaves");
                int trunkVertices = (Mathf.Max(8, species.structure.segmentsPerBranch) + 1)
                    * Mathf.Max(6, species.structure.radialSegments) + 2;
                for (int i = 0; i < trunkVertices; i++)
                    Assert.Less(Vector3.Distance(original[i], expanded[i]), 1e-5f,
                        "trunk geometry changed with radial spread");
                float narrowRadius = 0f;
                float wideRadius = 0f;
                float narrowTop = 0f;
                float wideTop = 0f;
                for (int i = trunkVertices; i < original.Length; i++)
                {
                    narrowRadius = Mathf.Max(narrowRadius,
                        new Vector2(original[i].x, original[i].z).magnitude);
                    wideRadius = Mathf.Max(wideRadius,
                        new Vector2(expanded[i].x, expanded[i].z).magnitude);
                    narrowTop = Mathf.Max(narrowTop, original[i].y);
                    wideTop = Mathf.Max(wideTop, expanded[i].y);
                }
                Assert.Greater(wideRadius / narrowRadius, 1.40f);
                Assert.Less(wideRadius / narrowRadius, 1.60f);
                Assert.Less(Mathf.Abs(wideTop - narrowTop), 0.05f,
                    "radial spread should preserve crown height");
            }
            finally
            {
                if (narrow != null) Object.DestroyImmediate(narrow);
                if (wide != null) Object.DestroyImmediate(wide);
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
                species.structure.crownEnvelopeStrength = -1f;
                species.structure.crownWidthScale = 4f;
                species.structure.crownVolumeScale = 4f;
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
                Assert.AreEqual(0f,
                    species.structure.crownEnvelopeStrength, 1e-6f);
                Assert.AreEqual(1.5f,
                    species.structure.crownWidthScale, 1e-6f);
                Assert.AreEqual(2f,
                    species.structure.crownVolumeScale, 1e-6f);
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

        private static float BoundsVolume(Mesh mesh)
        {
            Vector3 size = mesh.bounds.size;
            return size.x * size.y * size.z;
        }

        private static float PreEnvelopeBoundsVolume(
            TreeBotanicalPreset preset)
        {
            switch (preset)
            {
                case TreeBotanicalPreset.JapaneseZelkova: return 881.52f;
                case TreeBotanicalPreset.JapaneseMaple: return 877.20f;
                case TreeBotanicalPreset.JapaneseCedar: return 951.70f;
                case TreeBotanicalPreset.JapaneseWhiteBirch: return 667.82f;
                case TreeBotanicalPreset.JapaneseRedPine: return 1840.53f;
                case TreeBotanicalPreset.HinokiCypress: return 647.14f;
                case TreeBotanicalPreset.SomeiYoshinoSpring: return 1151.03f;
                case TreeBotanicalPreset.SomeiYoshinoSummer: return 1177.24f;
                case TreeBotanicalPreset.GinkgoSummer:
                case TreeBotanicalPreset.GinkgoAutumn:
                    return 468.27f;
                default:
                    throw new System.ArgumentOutOfRangeException(
                        nameof(preset), preset, null);
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
