using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SabaProps.ScreenFx.Editors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SabaProps.ScreenFx.CITests
{
    public class ScreenFxShaderTests
    {
        private static readonly string[] ShaderNames =
        {
            ScreenFxAssetLibrary.StandardShaderName,
            ScreenFxAssetLibrary.LiteShaderName,
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
        public void Shaders_RestoreTheEyeIndexAndSampleTheGrabPerEye()
        {
            foreach (string shaderName in ShaderNames)
            {
                StringAssert.Contains("UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX", ReadSource(shaderName));
            }

            string standard = ReadSource(ScreenFxAssetLibrary.StandardShaderName);
            StringAssert.Contains("UNITY_DECLARE_SCREENSPACE_TEXTURE(_GrabTexture)", standard);
            StringAssert.Contains("unity_StereoEyeIndex", standard);
        }

        private static string ReadSource(string shaderName)
        {
            return File.ReadAllText(AssetDatabase.GetAssetPath(Shader.Find(shaderName)));
        }
    }

    public class ScreenFxAssetTests
    {
        [TearDown]
        public void RemoveGeneratedAssets()
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(ScreenFxAssetLibrary.RootFolder);
        }

        [Test]
        public void PresetMaterials_CarryEveryValueTheShaderDeclares()
        {
            foreach (ScreenFxPreset preset in ScreenFxPresets.All)
            {
                foreach (bool lite in new[] { false, true })
                {
                    Material material = ScreenFxAssetLibrary.CreateMaterial(preset, lite);
                    try
                    {
                        Assert.AreEqual(ScreenFxAssetLibrary.ShaderName(lite), material.shader.name);
                        foreach (ScreenFxValue value in preset.values)
                        {
                            string where = $"{material.name}.{value.property}";
                            if (!material.HasProperty(value.property))
                            {
                                Assert.IsTrue(lite, $"{where} is missing from the GrabPass shader");
                                continue;
                            }

                            if (value.kind == ScreenFxValueKind.Float)
                            {
                                Assert.AreEqual(value.value.x, material.GetFloat(value.property), 1e-5f, where);
                            }
                            else
                            {
                                Assert.Less(
                                    Vector4.Distance(value.value, material.GetVector(value.property)), 1e-4f, where);
                            }
                        }
                    }
                    finally
                    {
                        Object.DestroyImmediate(material);
                    }
                }
            }
        }

        [Test]
        public void ApplyPreset_ClearsWhatThePreviousPresetSet()
        {
            Material material = ScreenFxAssetLibrary.CreateMaterial(ScreenFxPresets.Find("Rain"), false);
            try
            {
                ScreenFxAssetLibrary.SetUseDepth(material, true);
                ScreenFxAssetLibrary.ApplyPreset(material, ScreenFxPresets.Find("Speed"));
                Assert.AreEqual(0f, material.GetFloat("_Particle"));
                Assert.AreEqual(0f, material.GetFloat("_LensDrops"));
                Assert.AreEqual(0.6f, material.GetFloat("_RadialBlur"), 1e-5f);
                Assert.IsFalse(material.IsKeywordEnabled(ScreenFxAssetLibrary.DepthKeyword));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void CreateDefaults_WritesBothShadersForEveryPreset()
        {
            List<Object> assets = ScreenFxAssetLibrary.CreateOrLoadDefaults();
            Assert.AreEqual(ScreenFxPresets.All.Length * 2, assets.Count);

            // A second call returns the same assets instead of replacing edits.
            Material first = (Material)assets[0];
            first.SetFloat("_Weight", 0.25f);
            Material again = ScreenFxAssetLibrary.CreateOrLoadMaterial(ScreenFxPresets.All[0], false);
            Assert.AreEqual(first.GetInstanceID(), again.GetInstanceID());
            Assert.AreEqual(0.25f, again.GetFloat("_Weight"), 1e-5f);
        }

        [Test]
        public void Volume_IsTheBuiltInCubeWithoutACollider()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject volume = ScreenFxRigFactory.CreateVolume(ScreenFxPresets.Find("Snow"), false);
            Assert.IsNull(volume.GetComponent<Collider>());

            MeshRenderer renderer = volume.GetComponent<MeshRenderer>();
            Assert.AreEqual(ScreenFxAssetLibrary.StandardShaderName, renderer.sharedMaterial.shader.name);
            Assert.AreEqual(ShadowCastingMode.Off, renderer.shadowCastingMode);

            // The vertex shader keeps the face whose normal is +Z and maps its
            // UVs to the screen, so that face has to span the full UV square.
            Mesh mesh = volume.GetComponent<MeshFilter>().sharedMesh;
            Vector2 minimum = Vector2.one;
            Vector2 maximum = Vector2.zero;
            int faceVertices = 0;
            for (int i = 0; i < mesh.vertexCount; i++)
            {
                if (mesh.normals[i].z < 0.5f)
                {
                    continue;
                }

                faceVertices++;
                minimum = Vector2.Min(minimum, mesh.uv[i]);
                maximum = Vector2.Max(maximum, mesh.uv[i]);
            }

            Assert.AreEqual(4, faceVertices);
            Assert.AreEqual(Vector2.zero, minimum);
            Assert.AreEqual(Vector2.one, maximum);
        }

        [Test]
        public void Gallery_HasOneVolumePerPreset()
        {
            ScreenFxGallery.Create(false);
            Assert.IsTrue(File.Exists(ScreenFxGallery.ScenePath));

            GameObject root = GameObject.Find(ScreenFxGallery.RootName);
            Assert.IsNotNull(root);
            var materials = new HashSet<Material>();
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.sharedMaterial.shader.name == ScreenFxAssetLibrary.StandardShaderName)
                {
                    materials.Add(renderer.sharedMaterial);
                }
            }

            Assert.AreEqual(ScreenFxPresets.All.Length, materials.Count);
        }
    }
}
