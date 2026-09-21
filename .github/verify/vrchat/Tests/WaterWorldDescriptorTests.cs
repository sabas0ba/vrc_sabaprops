using System.Reflection;
using NUnit.Framework;
using SabaProps.Water.Editors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;

namespace SabaProps.Foliage.WorldTests
{
    /// <summary>
    /// Exercises the optional reflection branch against the pinned Worlds SDK.
    /// The normal CI project intentionally has no VRChat assemblies.
    /// </summary>
    public class WaterWorldDescriptorTests
    {
        [Test]
        public void Gallery_ConfiguresTheInstalledWorldsSdkDescriptor()
        {
            Assert.IsTrue(WaterVrcWorld.IsSdkPresent, "Worlds SDK type was not detected");
            Scene scene = WaterSampleScene.Create();
            try
            {
                GameObject world = GameObject.Find(WaterVrcWorld.WorldObjectName);
                Assert.IsNotNull(world);
                VRCSceneDescriptor descriptor = world.GetComponent<VRCSceneDescriptor>();
                Assert.IsNotNull(descriptor, "VRCSceneDescriptor was not added");
                Assert.IsNotNull(descriptor.spawns);
                Assert.AreEqual(1, descriptor.spawns.Length);
                Assert.AreEqual(WaterVrcWorld.SpawnObjectName, descriptor.spawns[0].name);
                const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
                PropertyInfo referenceCameraProperty = descriptor.GetType()
                    .GetProperty("ReferenceCamera", PublicInstance);
                FieldInfo referenceCameraField = descriptor.GetType()
                    .GetField("ReferenceCamera", PublicInstance);
                Assert.IsTrue(
                    referenceCameraProperty != null || referenceCameraField != null,
                    "VRCSceneDescriptor no longer exposes ReferenceCamera");
                object referenceCamera = referenceCameraProperty != null
                    ? referenceCameraProperty.GetValue(descriptor)
                    : referenceCameraField.GetValue(descriptor);
                Assert.AreEqual(Camera.main.gameObject, referenceCamera);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(WaterAssetLibrary.RootFolder);
            }
        }
    }
}
