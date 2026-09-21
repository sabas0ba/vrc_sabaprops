using NUnit.Framework;
using SabaProps.Foliage;
using SabaProps.Foliage.Editors;
using SabaProps.Trees.Editors;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Trees.CITests
{
    public sealed class PlacementToolTests
    {
        [Test]
        public void PlacementLanguageDefaultsToJapaneseAndCanSwitchToEnglish()
        {
            const string key = "SabaProps.Editor.Language";
            bool hadPreference = EditorPrefs.HasKey(key);
            int previous = EditorPrefs.GetInt(key, 0);
            try
            {
                EditorPrefs.DeleteKey(key);
                Assert.AreEqual(SabaPropsEditorLanguage.Japanese, SabaPropsEditorLocalization.Language);
                Assert.AreEqual("配置", SabaPropsEditorLocalization.Text("配置", "Placement"));

                SabaPropsEditorLocalization.Language = SabaPropsEditorLanguage.English;
                Assert.AreEqual("Placement", SabaPropsEditorLocalization.Text("配置", "Placement"));
            }
            finally
            {
                if (hadPreference)
                {
                    EditorPrefs.SetInt(key, previous);
                }
                else
                {
                    EditorPrefs.DeleteKey(key);
                }
            }
        }

        [Test]
        public void FoliageStampRangeSanitizesDimensionsAndEstimatesArea()
        {
            Vector2 size = FoliageStampUtility.SanitizeSize(new Vector2(-4f, 0f));
            Assert.AreEqual(new Vector2(4f, 0.1f), size);
            Assert.AreEqual(0.1f, FoliageStampUtility.SanitizeRadius(0f), 0.0001f);

            int rectangle = FoliageStampUtility.EstimateInstanceCount(
                FoliageAreaShape.Rectangle,
                new Vector2(4f, 5f),
                1f,
                2f);
            int circle = FoliageStampUtility.EstimateInstanceCount(
                FoliageAreaShape.Circle,
                Vector2.one,
                2f,
                1f);

            Assert.AreEqual(40, rectangle);
            Assert.AreEqual(13, circle);
        }

        [Test]
        public void SurfaceGrowthPlacementCreatesConfiguredEditableVine()
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject slope = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Material material = null;
            SurfaceVine vine = null;
            try
            {
                material = new Material(Shader.Find("Standard"));
                Collider wallCollider = wall.GetComponent<Collider>();
                Collider slopeCollider = slope.GetComponent<Collider>();
                vine = SurfaceGrowthPlacementUtility.CreateVine(
                    wallCollider,
                    new[] { slopeCollider, wallCollider, slopeCollider },
                    material,
                    new Vector3(1f, 2f, 3f),
                    Vector3.up,
                    2.4f,
                    SurfaceVinePlacementPreset.EnglishIvy,
                    null,
                    false);

                Assert.IsNotNull(vine);
                Assert.AreSame(wallCollider, vine.targetSurface);
                Assert.AreSame(material, vine.material);
                Assert.AreEqual(1, vine.additionalSurfaces.Count);
                Assert.AreSame(slopeCollider, vine.additionalSurfaces[0]);
                Assert.AreEqual(3, vine.guidePoints.Count);
                Assert.AreEqual(2.4f, vine.guidePoints[2].y, 0.0001f);
                Assert.AreEqual(SurfaceLeafShape.Lobed, vine.morphology.leafShape);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), vine.transform.position);
            }
            finally
            {
                if (vine != null)
                {
                    Object.DestroyImmediate(vine.gameObject);
                }
                Object.DestroyImmediate(wall);
                Object.DestroyImmediate(slope);
                if (material != null)
                {
                    Object.DestroyImmediate(material);
                }
            }
        }

        [Test]
        public void TreePlacementCreatesConfiguredFieldWithoutGeneratingIt()
        {
            TreeSpecies species = ScriptableObject.CreateInstance<TreeSpecies>();
            TreeField field = null;
            try
            {
                species.placement.placementWeight = 0.75f;
                field = TreePlacementUtility.CreateField(
                    species,
                    null,
                    new Vector3(4f, 0f, -3f),
                    new Vector2(18f, 12f),
                    0.12f,
                    false);

                Assert.IsNotNull(field);
                Assert.AreEqual(new Vector3(4f, 0f, -3f), field.transform.position);
                Assert.AreEqual(new Vector2(18f, 12f), field.size);
                Assert.AreEqual(0.12f, field.density, 0.0001f);
                Assert.AreSame(species, field.species[0]);
                Assert.AreEqual(0.75f, field.speciesWeights[0], 0.0001f);
                Assert.IsNull(field.generatedRoot);
            }
            finally
            {
                if (field != null)
                {
                    Object.DestroyImmediate(field.gameObject);
                }
                Object.DestroyImmediate(species);
            }
        }
    }
}
