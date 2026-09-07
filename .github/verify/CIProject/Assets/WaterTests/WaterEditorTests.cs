using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SabaProps.Water.Editors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
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
            WaterAssetLibrary.WetSurfaceShaderName,
            WaterAssetLibrary.WetSurfaceTransparentShaderName,
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

        [Test]
        public void StereoDependentShaders_RestoreTheEyeIndexInFragments()
        {
            string[] stereoDependentShaders =
            {
                WaterSurfaceProfile.LiteShaderName,
                WaterSurfaceProfile.StandardShaderName,
                WaterAssetLibrary.FogParticleShaderName,
                WaterAssetLibrary.FogVolumeShaderName,
                WaterAssetLibrary.UnderwaterLiteShaderName,
                WaterAssetLibrary.UnderwaterStandardShaderName,
                WaterAssetLibrary.UnderwaterSurfaceLiteShaderName,
                WaterAssetLibrary.UnderwaterSurfaceStandardShaderName,
            };

            foreach (string shaderName in stereoDependentShaders)
            {
                AssertSourceContains(
                    shaderName,
                    "UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX");
            }
        }

        [Test]
        public void VolumeShaders_UseDepthAndInwardFacingBoundaryPasses()
        {
            string fogSource = ReadShaderSource(WaterAssetLibrary.FogVolumeShaderName);
            Assert.IsTrue(fogSource.Contains("ZTest Always"));
            Assert.IsTrue(fogSource.Contains("_CameraDepthTexture"));
            Assert.IsTrue(fogSource.Contains("depthFraction"));

            AssertSourceContains(WaterAssetLibrary.UnderwaterSurfaceLiteShaderName, "Cull Front");
            AssertSourceContains(WaterAssetLibrary.UnderwaterSurfaceStandardShaderName, "Cull Front");
        }

        private static void AssertSourceContains(string shaderName, string expected)
        {
            Assert.IsTrue(
                ReadShaderSource(shaderName).Contains(expected),
                shaderName + " must contain " + expected);
        }

        private static string ReadShaderSource(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            Assert.IsNotNull(shader, shaderName);
            string assetPath = AssetDatabase.GetAssetPath(shader);
            return File.ReadAllText(assetPath);
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
                Assert.IsTrue(
                    first.normals.Any(normal => normal.y < 0.999f),
                    "sloped river samples must not retain flat upward normals");

                for (int index = 0; index < first.vertexCount; index++)
                {
                    Assert.AreEqual(first.vertices[index], second.vertices[index]);
                    Assert.AreEqual(first.uv[index], second.uv[index]);
                    Assert.AreEqual(first.normals[index], second.normals[index]);
                    Assert.AreEqual(1f, first.normals[index].magnitude, 1e-4f);
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
                    Assert.IsTrue(profile.material.HasProperty("_CrestFoamWidth"));
                    Assert.IsTrue(profile.material.HasProperty("_FoamTrailStrength"));
                    Assert.IsTrue(profile.material.HasProperty("_FoamPatternScale"));
                    Assert.IsTrue(profile.material.HasProperty("_FoamPatternSpeed"));
                    Assert.IsTrue(profile.material.HasProperty("_FoamPatternWarp"));
                    Assert.IsTrue(profile.material.HasProperty("_FlowTurbulence"));
                    Assert.IsTrue(profile.material.HasProperty("_ReflectionDistortion"));
                    Assert.IsTrue(profile.material.HasProperty("_LightingResponse"));
                    Assert.AreEqual(0f, profile.foamStrength, 0.0001f,
                        "continuous procedural foam bands must be disabled in default profiles");
                    Assert.Less(
                        Vector4.Distance(profile.foamColor, profile.shallowColor),
                        0.4f,
                        "default foam tint should remain close to its surface colour");
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

                ParticleSystem splash = rig.transform.Find("Rain/Collision Splash")
                    .GetComponent<ParticleSystem>();
                ParticleSystemRenderer splashRenderer = splash.GetComponent<ParticleSystemRenderer>();
                Assert.AreEqual(ParticleSystemRenderMode.Stretch, splashRenderer.renderMode);
                Assert.IsTrue(splash.main.startSize.constantMax <= 0.012f);
                Assert.AreEqual(LightProbeUsage.BlendProbes, splashRenderer.lightProbeUsage);

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
                Assert.IsNull(GameObject.Find("Whitewater Crest [Copy Ready]"));
                Assert.IsNull(GameObject.Find("Plunge Pool Froth [Copy Ready]"));
                AssertStretchSpray("Breaking Wave Spray [Copy Ready]", 0.012f);
                AssertStretchSpray("Waterfall Spray [Copy Ready]", 0.012f);
                AssertStretchSpray("Plunge Pool Spray [Copy Ready]", 0.012f);
                Assert.IsNotNull(GameObject.Find("Underwater Surface View"));
                GameObject standardPool = GameObject.Find("Standard Underwater Pool [Copy Ready]");
                Assert.IsNotNull(standardPool);
                Transform tunnelTransform = standardPool.transform.Find(
                    "Eight-Direction Tunnel Boundary [Copy Ready]");
                Assert.IsNotNull(tunnelTransform);
                Assert.IsFalse(tunnelTransform.gameObject.activeSelf,
                    "tunnel boundary preview must not obstruct the top-only pool camera by default");
                Assert.IsNotNull(GameObject.Find("DROPLETS OPAQUE Surface Mannequin [Copy Ready]"));
                Assert.IsNotNull(GameObject.Find("DROPLETS TRANSPARENT Surface Mannequin [Copy Ready]"));
                Assert.IsNotNull(GameObject.Find("Wet Surface Spot Light"));
                Assert.IsTrue(GameObject.FindObjectsOfType<Light>()
                    .Count(light => light.type == LightType.Spot) >= 3);
                Assert.IsNotNull(GameObject.Find("Fog Point Light"));
                Assert.AreEqual(2, Object.FindObjectsOfType<ReflectionProbe>().Length,
                    "Lite and Standard puddle exhibits must each include a reflection probe");

                GameObject ground = GameObject.Find("Gallery Ground");
                Assert.IsNotNull(ground);
                Assert.Less(ground.GetComponent<Renderer>().bounds.max.y, -6f,
                    "the gallery ground must not intersect the underwater pools");

                GameObject tunnel = tunnelTransform.gameObject;
                Material tunnelMaterial = tunnel.GetComponent<Renderer>().sharedMaterial;
                Assert.AreEqual(Vector4.zero, tunnelMaterial.GetVector("_BoundaryUpDown"));
                Assert.AreEqual(Vector4.one, tunnelMaterial.GetVector("_BoundaryCardinal"));
                Assert.AreEqual(Vector4.one, tunnelMaterial.GetVector("_BoundaryDiagonal"));

                Material underwaterVolume = standardPool.transform.Find(
                    "Underwater Standard [Copy Ready]/Underwater Volume")
                    .GetComponent<Renderer>().sharedMaterial;
                Assert.IsTrue(underwaterVolume.HasProperty("_VolumeDistortionStrength"));
                Assert.IsTrue(underwaterVolume.GetFloat("_VolumeDistortionStrength") <= 0.002f);

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
        public void LightingGallery_CoversDarkPointAndSpotExamples()
        {
            Scene scene = WaterSampleScene.CreateLightingGallery();
            try
            {
                Assert.IsTrue(scene.IsValid(), "lighting gallery scene was not created");
                Assert.IsNotNull(
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(WaterSampleScene.LightingScenePath));
                Assert.IsNotNull(GameObject.Find(WaterSampleScene.LightingGalleryRootName));
                Assert.IsNotNull(GameObject.Find("DARK AMBIENT Example [Copy Ready]"));
                Assert.IsNotNull(GameObject.Find("POINT LIGHT Example [Copy Ready]"));
                Assert.IsNotNull(GameObject.Find("SPOT LIGHT Example [Copy Ready]"));
                Assert.IsNotNull(GameObject.Find("Wet Opaque Preview [Copy Ready]"));
                Assert.IsNotNull(GameObject.Find("Wet Transparent Preview [Copy Ready]"));
                Assert.IsNotNull(GameObject.Find(WaterSampleScene.LightingCameraName));

                Light[] lights = Object.FindObjectsOfType<Light>();
                Assert.AreEqual(0, lights.Count(light => light.type == LightType.Directional));
                Assert.AreEqual(1, lights.Count(light => light.type == LightType.Point));
                Assert.AreEqual(1, lights.Count(light => light.type == LightType.Spot));
                Assert.AreEqual(3, Object.FindObjectsOfType<ParticleSystem>()
                    .Count(system => system.name == "Rain"));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(WaterAssetLibrary.RootFolder);
            }
        }

        private static void AssertStretchSpray(string objectName, float maximumSize)
        {
            GameObject sprayObject = GameObject.Find(objectName);
            Assert.IsNotNull(sprayObject, objectName);
            ParticleSystem particles = sprayObject.GetComponent<ParticleSystem>();
            ParticleSystemRenderer renderer = sprayObject.GetComponent<ParticleSystemRenderer>();
            Assert.IsNotNull(particles);
            Assert.IsNotNull(renderer);
            Assert.AreEqual(ParticleSystemRenderMode.Stretch, renderer.renderMode);
            Assert.IsTrue(particles.main.startSize.constantMax <= maximumSize);
            Assert.IsTrue(particles.main.playOnAwake);
            Assert.AreEqual(LightProbeUsage.BlendProbes, renderer.lightProbeUsage);
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

                string lightingScenePath = ImportedSamplePath + "/WaterLightingGallery.unity";
                Scene lightingScene = EditorSceneManager.OpenScene(lightingScenePath);
                Assert.IsTrue(lightingScene.IsValid(), "distributed lighting gallery could not be opened");
                Assert.IsNotNull(GameObject.Find(WaterSampleScene.LightingGalleryRootName));
                Assert.IsNotNull(GameObject.Find("POINT LIGHT Source"));
                Assert.IsNotNull(GameObject.Find("SPOT LIGHT Source"));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(ImportedSamplePath);
            }
        }
    }
}
