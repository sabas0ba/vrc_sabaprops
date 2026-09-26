using System.Collections.Generic;
using NUnit.Framework;
using SabaProps.Flock;
using SabaProps.Flock.Editors;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

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
            for (int channel = 0; channel <= FlockShaderContract.ExtraChannel; channel++)
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
