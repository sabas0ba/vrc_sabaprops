using NUnit.Framework;
using SabaProps.Tablet.Authoring;
using SabaProps.Tablet.Editors;
using UnityEngine;

namespace SabaProps.Tablet.WorldTests
{
    public class TabletThemeTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        public void Preset_ChangesAppearanceAndPreservesControls(int index)
        {
            TabletSampleScene.Create();
            TabletDefinition definition = Object.FindObjectOfType<TabletDefinition>();
            TabletController controller = definition.GetComponent<TabletController>();
            int buttons = controller.buttons.Length;
            int pages = controller.pages.Length;
            TabletTheme preset = TabletThemePresets.Load(index);
            Assert.That(preset, Is.Not.Null);
            TabletThemePresets.Apply(definition, preset);
            Assert.That(controller.buttons.Length, Is.EqualTo(buttons));
            Assert.That(controller.pages.Length, Is.EqualTo(pages));
            Transform decoration = controller.body.Find("Theme Decoration");
            if (preset.decoration != TabletDecoration.None)
            {
                Assert.That(decoration, Is.Not.Null);
                Assert.That(decoration.GetComponentsInChildren<Collider>().Length, Is.Zero);
            }
            else Assert.That(decoration, Is.Null);
            if (preset.skeletonFrame)
            {
                Transform frame = controller.body.Find("Internal Frame");
                Assert.That(frame, Is.Not.Null);
                Assert.That(frame.GetComponentsInChildren<Collider>().Length, Is.Zero);
                Material housing = controller.body.Find("Housing").GetComponent<Renderer>().sharedMaterial;
                Assert.That(housing.renderQueue, Is.EqualTo(3000));
                Assert.That(housing.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON"), Is.True);
                Assert.That(housing.color.a, Is.LessThan(1f));
            }
            Assert.That(controller.handle, Is.Not.Null);
            Color actual = controller.body.Find("Housing").GetComponent<Renderer>().sharedMaterial.color;
            Assert.That(Vector4.Distance(actual, preset.bodyColor), Is.LessThan(0.00001f));
            foreach (TabletButton button in controller.buttons)
            {
                Assert.That(button.pressZone.isTrigger, Is.True);
                Assert.That(button.target, Is.Not.Null);
                Assert.That(button.label.color, Is.EqualTo(preset.labelColor));
            }
        }

        [Test]
        public void EditableCopy_DoesNotModifyThePreset()
        {
            TabletTheme original = TabletThemePresets.Load(0);
            Color before = original.bodyColor;
            TabletTheme copy = TabletThemePresets.EditableCopy(original);
            try
            {
                copy.bodyColor = Color.black;
                Assert.That(original.bodyColor, Is.EqualTo(before));
                Assert.That(copy, Is.Not.SameAs(original));
            }
            finally
            {
                UnityEditor.AssetDatabase.DeleteAsset(UnityEditor.AssetDatabase.GetAssetPath(copy));
            }
        }
    }
}
