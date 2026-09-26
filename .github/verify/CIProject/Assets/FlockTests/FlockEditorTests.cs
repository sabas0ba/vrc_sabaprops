using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SabaProps.Flock;
using SabaProps.Flock.Editors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SabaProps.Flock.CITests
{
    public class FlockEditorTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _created)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _created.Clear();
            AssetDatabase.DeleteAsset(FlockAssetLibrary.RootFolder);
        }

        private FlockSwarm Create(string presetId)
        {
            FlockSwarm swarm = FlockSwarmBuilder.Create(presetId, null, Vector3.zero);
            _created.Add(swarm.gameObject);
            return swarm;
        }

        [Test]
        public void BundledSample_ImportsWithEightEditableSwarms()
        {
            const string source =
                "Packages/io.github.sabas0ba.sabaprops.flock/Samples~/Flock Sample";
            const string destination = "Assets/ImportedFlockSample";
            Assert.IsTrue(Directory.Exists(source), "bundled sample is missing");

            try
            {
                FileUtil.CopyFileOrDirectory(source, destination);
                AssetDatabase.Refresh();
                string scenePath = destination + "/FlockSample.unity";
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath));
                EditorSceneManager.OpenScene(scenePath);

                FlockSwarm[] swarms = Object.FindObjectsOfType<FlockSwarm>();
                Assert.AreEqual(8, swarms.Length);
                var patterns = new HashSet<FlockPattern>();
                foreach (FlockSwarm swarm in swarms)
                {
                    patterns.Add(swarm.settings.pattern);
                    Assert.IsNotNull(swarm.generatedMeshes);
                    Assert.AreEqual(1, swarm.generatedMeshes.Length);
                    Assert.IsNotNull(swarm.generatedMeshes[0]);
                    var renderer = swarm.GetComponentInChildren<MeshRenderer>();
                    Assert.IsNotNull(renderer);
                    Assert.IsNotNull(renderer.sharedMaterial);
                }

                Assert.AreEqual(8, patterns.Count);

                FlockSwarm copy = swarms[0];
                Mesh bundledMesh = copy.generatedMeshes[0];
                int bundledVertexCount = bundledMesh.vertexCount;
                copy.settings.count += 1;
                FlockSwarmBuilder.Rebuild(copy);
                Assert.IsFalse(ReferenceEquals(bundledMesh, copy.generatedMeshes[0]));
                Assert.AreEqual(bundledVertexCount, bundledMesh.vertexCount);
                Assert.IsTrue(AssetDatabase.GetAssetPath(copy.generatedMeshes[0]).StartsWith(
                    FlockAssetLibrary.MeshesFolder + "/", System.StringComparison.Ordinal));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(destination);
            }
        }

        [Test]
        public void WorldSample_ImportsWithRealScaleTanksAndWorkingViewpoints()
        {
            const string source = "Packages/io.github.sabas0ba.sabaprops.flock/Samples~/Flock Sample";
            const string destination = "Assets/ImportedWorldSample";
            try
            {
                FileUtil.CopyFileOrDirectory(source, destination);
                AssetDatabase.Refresh();
                EditorSceneManager.OpenScene(destination + "/FlockWorldScenarios.unity");
                FlockSwarm[] swarms = Object.FindObjectsOfType<FlockSwarm>();
                var speciesIds = new HashSet<string>();
                foreach (FlockSwarm swarm in swarms) speciesIds.Add(swarm.presetId);
                Assert.AreEqual(FlockSpeciesCatalog.All.Count, speciesIds.Count, "world scenarios must include all species");
                foreach (FlockPreset preset in FlockSpeciesCatalog.All) Assert.IsTrue(speciesIds.Contains(preset.Species.id));
                int smallTankCount = 0;
                int lodCount = 0;
                foreach (FlockSwarm swarm in swarms)
                {
                    Assert.AreEqual(Vector3.one, swarm.transform.lossyScale);
                    Assert.IsTrue(swarm.generatedMeshes.Length > 0);
                    foreach (Mesh mesh in swarm.generatedMeshes) Assert.IsNotNull(mesh);
                    if (swarm.presetId == "swan" || swarm.presetId == "crane")
                    {
                        Assert.AreEqual(FlockPattern.FreeFlight, swarm.settings.pattern);
                        Assert.LessOrEqual(swarm.settings.count, 3);
                        Assert.GreaterOrEqual(swarm.settings.area.x, 25f);
                        Assert.GreaterOrEqual(swarm.settings.area.z, 24f);
                    }
                    if (swarm.presetId == "squid" || swarm.presetId == "octopus")
                    {
                        Assert.AreEqual(FlockPattern.Jet, swarm.settings.pattern);
                        Assert.AreEqual(FlockAnimation.Jet, swarm.species.animation);
                    }
                    if (swarm.presetId == "jellyfish") Assert.AreEqual(FlockPattern.Float, swarm.settings.pattern);
                    if (swarm.GetComponent<LODGroup>() != null) lodCount++;
                    if (swarm.presetId == "neon-tetra")
                    {
                        smallTankCount++;
                        Assert.AreEqual(0.03f, swarm.species.bodyLength, 0.0001);
                        Assert.Less(swarm.settings.area.x, 0.3f);
                        Assert.Less(swarm.settings.area.y, 0.18f);
                        Assert.Less(swarm.settings.area.z, 0.15f);
                        FlockMotionInput input = FlockSwarmMeshBuilder.MotionInput(swarm.species, swarm.settings, 0);
                        Assert.Greater(input.BodyMargin.x, 0f);
                        foreach (Mesh mesh in swarm.generatedMeshes)
                            Assert.AreEqual(4, mesh.GetVertexAttributeDimension(VertexAttribute.TexCoord6));
                    }
                }
                Assert.AreEqual(1, smallTankCount);
                Assert.GreaterOrEqual(lodCount, 27);
                Assert.AreEqual(11, Object.FindObjectsOfType<Camera>().Length);
                FlockWorldSample.SmallView();
                int enabled = 0;
                foreach (Camera camera in Object.FindObjectsOfType<Camera>())
                {
                    if (!camera.enabled) continue;
                    enabled++;
                    Assert.AreEqual("Small tank - standing eye 1.65 m", camera.name);
                    Assert.AreEqual(1.65f, camera.transform.position.y, 0.0001);
                }
                Assert.AreEqual(1, enabled);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(destination);
            }
        }

        [Test]
        public void BundledComparisons_ContainsAllSpeciesAndControlledSwimmingExamples()
        {
            const string source = "Packages/io.github.sabas0ba.sabaprops.flock/Samples~/Flock Sample";
            const string destination = "Assets/ImportedFlockComparisons";
            try
            {
                FileUtil.CopyFileOrDirectory(source, destination); AssetDatabase.Refresh();
                EditorSceneManager.OpenScene(destination + "/FlockComparisons.unity");
                var ids = new HashSet<string>(); var patterns = new HashSet<FlockPattern>();
                int specimens = 0;
                foreach (FlockSwarm swarm in Object.FindObjectsOfType<FlockSwarm>())
                {
                    Assert.IsNotNull(swarm.generatedMeshes[0]);
                    Assert.IsNotNull(swarm.GetComponentInChildren<MeshRenderer>().sharedMaterial);
                    if (swarm.transform.parent.name.StartsWith("01 ", System.StringComparison.Ordinal))
                    {
                        specimens++; ids.Add(swarm.presetId);
                        Assert.AreEqual(1, swarm.settings.count);
                        Assert.AreEqual(Vector3.one, swarm.transform.localScale);
                        Assert.AreEqual(FlockPattern.Anchored, swarm.settings.pattern);
                    }
                    if (swarm.transform.parent.name.StartsWith("02 ", System.StringComparison.Ordinal))
                    {
                        Assert.AreEqual("sardine", swarm.presetId); Assert.AreEqual(24, swarm.settings.count);
                        Assert.AreEqual(42, swarm.settings.seed); Assert.AreEqual(new Vector3(2f, 1f, 2f), swarm.settings.area);
                        patterns.Add(swarm.settings.pattern);
                    }
                }
                Assert.AreEqual(FlockSpeciesCatalog.All.Count, specimens);
                Assert.AreEqual(FlockSpeciesCatalog.All.Count, ids.Count);
                Assert.AreEqual(4, patterns.Count);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(destination);
            }
        }

        [Test]
        public void Shader_IsFoundAndCompiles()
        {
            Shader shader = Shader.Find(FlockShaderContract.ShaderName);
            Assert.IsNotNull(shader, $"shader '{FlockShaderContract.ShaderName}' was not found");
            if (!ShaderUtil.ShaderHasError(shader))
            {
                Assert.IsTrue(shader.isSupported, "flock shader is unsupported");
                return;
            }

            var details = new List<string>();
            foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
            {
                details.Add($"{message.file}({message.line}): {message.message} {message.messageDetails}");
            }

            Assert.Fail("flock shader failed to compile:\n" + string.Join("\n", details));
        }

        [Test]
        public void Create_BuildsThreeLodRenderersSharingOneMaterial()
        {
            FlockSwarm swarm = Create("starling");
            var group = swarm.GetComponent<LODGroup>();
            Assert.IsNotNull(group, "no LODGroup");

            Assert.AreEqual(FlockSwarmBuilder.IndividualSize(swarm.species), group.size, 1e-4,
                "the LOD group must be sized as one individual, not as the whole swarm");

            LOD[] lods = group.GetLODs();
            Assert.AreEqual(3, lods.Length);
            Material material = null;
            foreach (LOD lod in lods)
            {
                Assert.AreEqual(1, lod.renderers.Length);
                Renderer renderer = lod.renderers[0];
                Assert.AreEqual(ShadowCastingMode.Off, renderer.shadowCastingMode);
                Assert.IsTrue(renderer.receiveShadows);
                Assert.AreEqual((StaticEditorFlags)0, GameObjectUtility.GetStaticEditorFlags(renderer.gameObject),
                    "a flock renderer must not be static: batching would break the shader motion");
                material = material ?? renderer.sharedMaterial;
                Assert.AreSame(material, renderer.sharedMaterial, "LOD renderers use different materials");
            }

            Assert.AreEqual(FlockShaderContract.ShaderName, material.shader.name);
        }

        [Test]
        public void GeneratedMesh_CarriesEveryChannelAndCoversTheArea()
        {
            FlockSwarm swarm = Create("sardine");
            Mesh mesh = swarm.generatedMeshes[0];
            for (int channel = 0; channel <= FlockShaderContract.BodyMarginChannel; channel++)
            {
                Assert.AreEqual(4, mesh.GetVertexAttributeDimension(VertexAttribute.TexCoord0 + channel),
                    $"UV{channel} is not four-dimensional");
                var uvs = new List<Vector4>();
                mesh.GetUVs(channel, uvs);
                Assert.AreEqual(mesh.vertexCount, uvs.Count, $"UV{channel} is incomplete");
            }

            Vector3 extents = mesh.bounds.extents;
            Vector3 area = swarm.settings.area;
            Assert.GreaterOrEqual(extents.x, area.x);
            Assert.GreaterOrEqual(extents.y, area.y);
            Assert.GreaterOrEqual(extents.z, area.z);
        }

        [Test]
        public void Rebuild_KeepsTheMeshAsset()
        {
            FlockSwarm swarm = Create("goose");
            string path = AssetDatabase.GetAssetPath(swarm.generatedMeshes[0]);
            string guid = AssetDatabase.AssetPathToGUID(path);
            Assert.IsNotEmpty(guid);

            swarm.settings.count = 7;
            FlockSwarmBuilder.Rebuild(swarm);

            Assert.AreEqual(path, AssetDatabase.GetAssetPath(swarm.generatedMeshes[0]));
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path));
            var individuals = new List<Vector4>();
            swarm.generatedMeshes[0].GetUVs(FlockShaderContract.IndividualChannel, individuals);
            float highest = 0f;
            foreach (Vector4 v in individuals)
            {
                highest = Mathf.Max(highest, v.x);
            }

            Assert.AreEqual(6f, highest, 0.0, "rebuild did not apply the new count");
        }

        [Test]
        public void DuplicatedSwarm_GetsItsOwnMeshAsset()
        {
            FlockSwarm original = Create("goose");
            Mesh originalMesh = original.generatedMeshes[0];
            string originalPath = AssetDatabase.GetAssetPath(originalMesh);
            int originalVertices = originalMesh.vertexCount;

            GameObject copy = Object.Instantiate(original.gameObject);
            _created.Add(copy);
            var duplicate = copy.GetComponent<FlockSwarm>();
            duplicate.settings.count = 3;
            duplicate.settings.lodMode = FlockLodMode.Single;
            FlockSwarmBuilder.Rebuild(duplicate);

            Assert.AreNotEqual(originalPath, AssetDatabase.GetAssetPath(duplicate.generatedMeshes[0]));
            Assert.AreEqual(originalPath, AssetDatabase.GetAssetPath(originalMesh), "the original's asset was deleted");
            Assert.AreEqual(originalVertices, originalMesh.vertexCount, "rebuilding the copy rewrote the original's mesh");
        }

        [Test]
        public void SingleMode_ReplacesTheLodGroup()
        {
            FlockSwarm swarm = Create("koi");
            swarm.settings.lodMode = FlockLodMode.Single;
            swarm.settings.detail = FlockDetail.High;
            FlockSwarmBuilder.Rebuild(swarm);

            Assert.IsNull(swarm.GetComponent<LODGroup>());
            Assert.AreEqual(1, swarm.generatedMeshes.Length);
            int renderers = swarm.GetComponentsInChildren<MeshRenderer>().Length;
            Assert.AreEqual(1, renderers, "stale LOD renderers were left behind");
        }

        [Test]
        public void EveryPreset_CreatesAMaterialForItsHabitat()
        {
            foreach (FlockHabitat habitat in new[] { FlockHabitat.Sky, FlockHabitat.Sea, FlockHabitat.Reef, FlockHabitat.Aquarium })
            {
                Material material = FlockAssetLibrary.DefaultMaterial(habitat);
                Assert.IsNotNull(material, $"no material for {habitat}");
                Assert.AreEqual(FlockShaderContract.ShaderName, material.shader.name);
            }
        }
    }
}
