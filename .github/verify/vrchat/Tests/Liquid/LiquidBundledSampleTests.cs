using System.IO;
using NUnit.Framework;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.Udon;

namespace SabaProps.Liquid.WorldTests
{
    /// <summary>
    /// The sample shipped in Samples~/LiquidDemo, imported the way a user gets it.
    /// <para>
    /// The generator tests check what the generator makes today; this checks the
    /// copy that was exported and committed. A stale export, a material missing
    /// from the copy or a serialized program left behind shows up here as a
    /// reference that does not resolve.
    /// </para>
    /// </summary>
    public class LiquidBundledSampleTests
    {
        private const string SampleSource = "Packages/io.github.sabas0ba.sabaprops.liquid/Samples~/LiquidDemo/Assets";
        private const string ImportedScene = "Assets/SabaProps/Liquid/Samples/LiquidDemo.unity";

        [OneTimeSetUp]
        public void ImportSample()
        {
            // The generator writes to the same paths; start from the shipped copy alone.
            AssetDatabase.DeleteAsset("Assets/SabaProps/Liquid");
            CopyDirectory(Path.GetFullPath(SampleSource), Path.GetFullPath("Assets"));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.OpenScene(ImportedScene);
        }

        [OneTimeTearDown]
        public void RemoveSample()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset("Assets/SabaProps/Liquid");
        }

        [Test]
        public void EveryCanvasAndSource_ResolvesItsReferences()
        {
            LiquidCanvasPool pool = Object.FindObjectOfType<LiquidCanvasPool>();
            Assert.IsNotNull(pool, "the shipped scene has no canvas pool");
            Assert.IsNotEmpty(pool.canvases);
            Assert.IsNotEmpty(pool.mannequins);

            foreach (LiquidBodyCanvas canvas in Object.FindObjectsOfType<LiquidBodyCanvas>())
            {
                Assert.IsNotNull(canvas.projectorMaterial, canvas.name + ": projector material is missing from the sample");
                Assert.IsNotNull(canvas.updateMaterial, canvas.name + ": update material is missing from the sample");
                Assert.IsNotNull(canvas.projectorMaterial.shader, canvas.name);
                Assert.AreEqual("SabaProps/Liquid/Body Projector", canvas.projectorMaterial.shader.name);
            }

            foreach (LiquidSprayer sprayer in Object.FindObjectsOfType<LiquidSprayer>())
            {
                Assert.IsNotNull(sprayer.profile, sprayer.name);
                Assert.IsNotNull(sprayer.stream.GetComponent<ParticleSystemRenderer>().sharedMaterial,
                    sprayer.name + ": stream material is missing from the sample");
            }
        }

        [Test]
        public void EveryUdonBehaviour_HasItsSerializedProgram()
        {
            foreach (UdonSharpBehaviour behaviour in Object.FindObjectsOfType<UdonSharpBehaviour>())
            {
                UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(behaviour);
                Assert.IsNotNull(backing, behaviour.name + " has no backing UdonBehaviour");
                Assert.IsNotNull(backing.programSource, behaviour.name + " lost its program source");
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(directory.Replace(source, destination));
            }

            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string target = file.Replace(source, destination);
                // Serialized programs are shared with the project's own compile; keep the existing ones.
                if (!File.Exists(target))
                {
                    File.Copy(file, target);
                }
            }
        }
    }
}
