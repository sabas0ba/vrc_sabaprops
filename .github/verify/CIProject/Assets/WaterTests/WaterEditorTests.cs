using System.Collections.Generic;
using NUnit.Framework;
using SabaProps.Water.Editors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SabaProps.Water.CITests
{
    public class WaterShaderTests
    {
        private static readonly string[] ShaderNames =
        {
            WaterSurfaceProfile.LiteShaderName,
            WaterSurfaceProfile.StandardShaderName,
            WaterAssetLibrary.RainShaderName,
            WaterAssetLibrary.SplashShaderName,
            WaterAssetLibrary.RippleShaderName,
            WaterAssetLibrary.FogParticleShaderName,
            WaterAssetLibrary.FogVolumeShaderName,
            WaterAssetLibrary.UnderwaterLiteShaderName,
            WaterAssetLibrary.UnderwaterStandardShaderName,
            WaterAssetLibrary.UnderwaterSurfaceLiteShaderName,
            WaterAssetLibrary.UnderwaterSurfaceStandardShaderName,
            WaterAssetLibrary.CausticsShaderName,
            WaterAssetLibrary.LightShaftShaderName,
            WaterAssetLibrary.WhitewaterShaderName,
            WaterAssetLibrary.WetSurfaceShaderName,
        };

        [Test]
        public void EveryShader_IsFoundAndCompiles()
        {
            foreach (string shaderName in ShaderNames)
            {
                Shader shader = Shader.Find(shaderName);
                Assert.IsNotNull(shader, $"shader '{shaderName}' was not found");

                if (!ShaderUtil.ShaderHasError(shader))
                {
                    Assert.IsTrue(shader.isSupported, $"shader '{shaderName}' is unsupported");
                    continue;
                }

                var details = new List<string>();
                foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
                {
                    details.Add($"{message.file}({message.line}): {message.message} {message.messageDetails}");
                }

                Assert.Fail($"shader '{shaderName}' failed to compile:\n" + string.Join("\n", details));
            }
        }
    }

    public class WaterMeshTests
    {
        [Test]
        public void Puddle_IsFiniteAndHasExpectedTopology()
        {
            Mesh mesh = WaterMeshBuilder.BuildPuddle(2f, 1.4f, 4, 24, 42);
            try
            {
                Assert.IsNotNull(mesh);
                Assert.AreEqual(1 + 4 * 24, mesh.vertexCount);
                Assert.AreEqual(24 + 3 * 24 * 2, mesh.triangles.Length / 3);
                Assert.AreEqual(mesh.vertexCount, mesh.uv.Length);
                Assert.AreEqual(mesh.vertexCount, mesh.normals.Length);
                AssertFinite(mesh);

                foreach (Vector3 normal in mesh.normals)
                {
                    Assert.Greater(normal.y, 0.99f, "puddle normal faces down");
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void River_IsDeterministicAndUvAdvancesDownstream()
        {
            var points = new List<Vector3>
            {
                new Vector3(0f, 0f, -4f),
                Vector3.zero,
                new Vector3(1f, 0.2f, 4f),
                new Vector3(0f, 0f, 8f),
            };

            Mesh first = WaterMeshBuilder.BuildRiver(points, 2f, 5, 2f);
            Mesh second = WaterMeshBuilder.BuildRiver(points, 2f, 5, 2f);
            try
            {
                Assert.AreEqual(first.vertexCount, second.vertexCount);
                Assert.AreEqual(first.triangles.Length, second.triangles.Length);
                AssertFinite(first);

                for (int index = 0; index < first.vertexCount; index++)
                {
                    Assert.AreEqual(first.vertices[index], second.vertices[index]);
                    Assert.AreEqual(first.uv[index], second.uv[index]);
                    if (index >= 2)
                    {
                        Assert.Greater(first.uv[index].y + 1e-5f, first.uv[index - 2].y);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        private static void AssertFinite(Mesh mesh)
        {
            foreach (Vector3 vertex in mesh.vertices)
            {
                Assert.IsFalse(
                    float.IsNaN(vertex.x) || float.IsNaN(vertex.y) || float.IsNaN(vertex.z) ||
                    float.IsInfinity(vertex.x) || float.IsInfinity(vertex.y) || float.IsInfinity(vertex.z),
                    "mesh contains a non-finite vertex");
            }
        }
    }

    public class WaterAssetAndRigTests
    {
        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(WaterAssetLibrary.RootFolder);
        }

        [Test]
        public void DefaultProfiles_CreateTheExpectedMaterials()
        {
            foreach (WaterBodyKind bodyKind in WaterAssetLibrary.AllBodyKinds)
            {
                foreach (WaterQuality quality in WaterAssetLibrary.AllQualities)
                {
                    WaterSurfaceProfile profile = WaterAssetLibrary.CreateOrLoadProfile(bodyKind, quality);
                    Assert.IsNotNull(profile, bodyKind + " " + quality);
                    Assert.IsNotNull(profile.material, bodyKind + " " + quality + " material");
                    Assert.AreEqual(
                        quality == WaterQuality.Standard
                            ? WaterSurfaceProfile.StandardShaderName
                            : WaterSurfaceProfile.LiteShaderName,
                        profile.material.shader.name);
                    Assert.IsTrue(profile.material.enableInstancing);
                }
            }
        }

        [Test]
        public void RainRig_UsesCollisionSubEmittersAndAHorizontalRippleMesh()
        {
            GameObject rig = WaterRigFactory.CreateRainRig();
            try
            {
                ParticleSystem rain = rig.transform.Find("Rain").GetComponent<ParticleSystem>();
                Assert.IsTrue(rain.collision.enabled);
                Assert.AreEqual(2, rain.subEmitters.subEmittersCount);
                Assert.IsTrue(rain.main.playOnAwake);
                Assert.IsTrue(rain.main.prewarm);

                Transform rippleTransform = rig.transform.Find("Rain/Collision Ripple");
                Assert.IsNotNull(rippleTransform);
                var rippleRenderer = rippleTransform.GetComponent<ParticleSystemRenderer>();
                Assert.AreEqual(ParticleSystemRenderMode.Mesh, rippleRenderer.renderMode);
                Assert.IsNotNull(rippleRenderer.mesh);
                Assert.Less(rippleRenderer.mesh.bounds.size.y, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(rig);
            }
        }

        [Test]
        public void FogQuality_IsAnExplicitMaterialVariant()
        {
            Material lite = WaterAssetLibrary.CreateOrLoadEnvironmentMaterial(
                WaterAssetLibrary.FogVolumeLiteMaterialName);
            Material high = WaterAssetLibrary.CreateOrLoadEnvironmentMaterial(
                WaterAssetLibrary.FogVolumeHighMaterialName);

            Assert.IsFalse(lite.IsKeywordEnabled("_FOG_HIGH_QUALITY"));
            Assert.IsTrue(high.IsKeywordEnabled("_FOG_HIGH_QUALITY"));
        }
    }

    /// <summary>
    /// The gallery is both the first-run sample and the source for documentation
    /// captures, so its hierarchy and portable asset references are CI contracts.
    /// </summary>
    public class WaterSampleSceneTests
    {
        private const string PackageSamplePath =
            "Packages/io.github.sabas0ba.sabaprops.water/Samples~/Water Feature Gallery";
        private const string ImportedSamplePath = "Assets/ImportedWaterFeatureGalleryTest";

        [Test]
        public void FeatureGallery_CoversEveryFeatureAndIsSelfContained()
        {
            Scene scene = WaterSampleScene.Create();
            try
            {
                Assert.IsTrue(scene.IsValid(), "feature gallery scene was not created");
                Assert.IsNotNull(
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(WaterSampleScene.ScenePath),
                    "feature gallery scene was not saved");

                Assert.IsNotNull(GameObject.Find(WaterSampleScene.SurfaceRootName));
                Assert.IsNotNull(GameObject.Find(WaterSampleScene.RainRootName));
                Assert.IsNotNull(GameObject.Find(WaterSampleScene.AtmosphereRootName));
                Assert.IsNotNull(GameObject.Find(WaterSampleScene.UnderwaterRootName));
                Assert.IsNotNull(GameObject.Find(WaterSampleScene.WetSurfaceRootName));
                Assert.IsNotNull(GameObject.Find(WaterVrcWorld.WorldObjectName));
                Assert.IsNotNull(GameObject.Find(WaterVrcWorld.SpawnObjectName));
                Assert.IsNotNull(GameObject.Find(WaterSampleScene.OverviewCameraName));
                Assert.IsNotNull(GameObject.Find(WaterSampleScene.UnderwaterCameraName));
                WaterSampleScene.ValidateOpenGallery();

                Assert.AreEqual(2, Object.FindObjectsOfType<WaterPath>().Length,
                    "gallery must include editable Lite and Standard rivers");
                Assert.Greater(Object.FindObjectsOfType<ParticleSystem>().Length, 6,
                    "gallery must include rain, splash, ripple, fog, cloud and waterfall particles");
                Assert.IsNotNull(GameObject.Find("Whitewater Crest [Copy Ready]"));
                Assert.IsNotNull(GameObject.Find("Underwater Surface View"));
                Assert.IsNotNull(GameObject.Find("DROPLETS Surface Mannequin [Copy Ready]"));
                Assert.IsNotNull(GameObject.Find("Fog Point Light"));

                foreach (Renderer renderer in Object.FindObjectsOfType<Renderer>())
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        Assert.IsNotNull(material, renderer.name + " has a missing material");

                        string path = AssetDatabase.GetAssetPath(material);
                        Assert.IsTrue(
                            path.StartsWith(WaterSampleScene.SampleFolder + "/"),
                            renderer.name + " references a material outside the sample: " + path);
                    }
                }

                foreach (MeshFilter filter in Object.FindObjectsOfType<MeshFilter>())
                {
                    string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
                    if (!path.StartsWith("Assets/"))
                    {
                        continue;
                    }

                    Assert.IsTrue(
                        path.StartsWith(WaterSampleScene.SampleFolder + "/"),
                        filter.name + " references a mesh outside the sample: " + path);
                }
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(WaterAssetLibrary.RootFolder);
            }
        }

        [Test]
        public void DistributedGallery_ImportsWithAllReferences()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(ImportedSamplePath);
            FileUtil.CopyFileOrDirectory(PackageSamplePath, ImportedSamplePath);
            AssetDatabase.Refresh();

            try
            {
                string scenePath = ImportedSamplePath + "/WaterFeatureGallery.unity";
                Scene scene = EditorSceneManager.OpenScene(scenePath);
                Assert.IsTrue(scene.IsValid(), "distributed gallery scene could not be opened");
                WaterSampleScene.ValidateOpenGallery();
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(ImportedSamplePath);
            }
        }
    }
}
