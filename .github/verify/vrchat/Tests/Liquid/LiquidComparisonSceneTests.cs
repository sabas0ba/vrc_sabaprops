using NUnit.Framework;
using SabaProps.Liquid.Editors;
using UdonSharp.Compiler;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Components;

namespace SabaProps.Liquid.WorldTests
{
    /// <summary>
    /// The generated comparison world: the liquid, surface and colour rows,
    /// split from the demo world so neither carries every mannequin.
    /// </summary>
    public class LiquidComparisonSceneTests
    {
        [OneTimeSetUp]
        public void CreateScene()
        {
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { IsEditorBuild = true });
            LiquidSampleScene.CreateComparison();
            EditorSceneManager.OpenScene(LiquidSampleScene.ComparisonScenePath, OpenSceneMode.Single);
        }

        [OneTimeTearDown]
        public void CloseScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void Scene_IsSavedAsAWorldWithASpawnAndLight()
        {
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(LiquidSampleScene.ComparisonScenePath));

            VRCSceneDescriptor descriptor = Object.FindObjectOfType<VRCSceneDescriptor>();
            Assert.IsNotNull(descriptor, "no VRCSceneDescriptor");
            Assert.AreEqual(1, descriptor.spawns.Length);
            Assert.IsNotNull(Object.FindObjectOfType<LiquidLighting>(), "no LiquidLighting");
        }

        [Test]
        public void Mannequins_FillEveryRowWithMaterialsOfTheirOwn()
        {
            LiquidCanvasPool pool = Object.FindObjectOfType<LiquidCanvasPool>();
            Assert.IsNotNull(pool, "no canvas pool");
            int expected = LiquidSourceBuilder.PresetNames.Length          // liquid row
                + LiquidSurfaceBuilder.PresetNames.Length + 1               // surface row and avatar regions
                + 7                                                         // body colour row
                + LiquidSourceBuilder.GreyscalePaintNames.Length + 2;       // liquid colour row and the two mixes
            Assert.AreEqual(expected, pool.mannequins.Length, "a comparison row is missing mannequins");

            LiquidSampleSceneTests.AssertMannequinsDrawOnTheirOwnLayer(pool);

            // Numbered apart from the demo scene, so generating one scene never
            // rewrites the projector materials the other one uses.
            foreach (LiquidBodyCanvas mannequin in pool.mannequins)
            {
                StringAssert.IsMatch(@"_1\d\d\.mat$", AssetDatabase.GetAssetPath(mannequin.projectorMaterial),
                    mannequin.name + " uses a projector material numbered for the demo scene");
            }
        }

        [Test]
        public void EveryUdonBehaviour_LoadsItsProgram()
        {
            LiquidSampleSceneTests.AssertEveryProgramLoads();
        }

        [Test]
        public void EverySprayer_IsWiredToThePoolAndAProfile()
        {
            LiquidCanvasPool pool = Object.FindObjectOfType<LiquidCanvasPool>();
            LiquidSprayer[] sprayers = Object.FindObjectsOfType<LiquidSprayer>();
            Assert.IsNotEmpty(sprayers);
            foreach (LiquidSprayer sprayer in sprayers)
            {
                Assert.AreSame(pool, sprayer.pool, sprayer.name);
                Assert.IsNotNull(sprayer.profile, sprayer.name);
            }
        }
    }
}
