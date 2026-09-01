using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SabaProps.SoftProps.WorldTests
{
    /// <summary>
    /// Worlds SDKとUdonSharpが実在するprojectでgeneratorを実行する。
    /// package本体はpredefined assemblyに属するため、test asmdefからは
    /// reflectionで呼び、assembly間のcompile順序を依存関係にしない。
    /// </summary>
    public class SoftPropsPrefabTests
    {
        private const string GeneratorTypeName = "SabaProps.SoftProps.Editors.SoftPropGenerator";
        private const string OutputRoot = "Assets/SabaProps/SoftPropsGenerated";
        private const string PrefabFolder = OutputRoot + "/Prefabs";

        [Test]
        public void Generator_CreatesInteractivePropsAndContactProbeTest()
        {
            Type generator = FindType(GeneratorTypeName);
            Assert.IsNotNull(generator, "SoftPropGenerator was not compiled");

            MethodInfo generate = generator.GetMethod(
                "GenerateAll",
                BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(generate, "GenerateAll entry point is missing");

            try
            {
                generate.Invoke(null, null);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }

            string[] names = { "Futon", "Bed", "Sofa", "Cushion", "ContactProbeTest" };
            foreach (string name in names)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    PrefabFolder + "/" + name + ".prefab");
                Assert.IsNotNull(prefab, name + " prefab was not generated");

                MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
                Assert.IsTrue(renderers.Any(renderer =>
                        renderer.sharedMaterial != null
                        && renderer.sharedMaterial.shader != null
                        && renderer.sharedMaterial.shader.name == "SabaProps/Soft Surface"),
                    name + " has no deformable renderer");
            }

            GameObject sofa = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/Sofa.prefab");
            MeshRenderer[] softRenderers = sofa.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer.sharedMaterial != null
                    && renderer.sharedMaterial.shader.name == "SabaProps/Soft Surface")
                .ToArray();
            Assert.AreEqual(6, softRenderers.Length, "sofa must have three seat and three back surfaces");

            Type receiverType = FindType(
                "VRC.SDK3.Dynamics.Contact.Components.VRCContactReceiver");
            Assert.IsNotNull(receiverType, "World Contacts are unavailable");

            GameObject contactTest = AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabFolder + "/ContactProbeTest.prefab");
            Type senderType = FindType(
                "VRC.SDK3.Dynamics.Contact.Components.VRCContactSender");
            Assert.IsNotNull(senderType, "World Contact Senders are unavailable");
            Component[] senders = contactTest.GetComponentsInChildren(senderType, true);
            Assert.AreEqual(3, senders.Length,
                "contact test must provide finger, rod and plate senders");
            CollectionAssert.AreEquivalent(
                new[] { "Sphere", "Capsule", "Box" },
                senders.Select(sender => GetMemberValue(sender, "shapeType").ToString()).ToArray(),
                "contact test must exercise all supported footprint shapes");

            Type pickupType = FindType("VRC.SDK3.Components.VRCPickup");
            Assert.IsNotNull(pickupType, "VRCPickup is unavailable");
            Assert.AreEqual(3, contactTest.GetComponentsInChildren(pickupType, true).Length,
                "all contact probes must be pickupable");

            MeshRenderer skinSurface = contactTest.GetComponentsInChildren<MeshRenderer>(true)
                .First(renderer => renderer.name == "SkinSurface");
            Material skinMaterial = skinSurface.sharedMaterial;
            Assert.AreEqual("DefaultSkinMatte", skinMaterial.name);
            Assert.LessOrEqual(skinMaterial.GetFloat("_Smoothness"), 0.06f);
            Assert.AreEqual(0f, skinMaterial.GetFloat("_WeaveContrast"), 0.0001f);
            Assert.Greater(skinMaterial.GetFloat("_SurfaceGrainStrength"), 0f);

            Component testReceiver = skinSurface.GetComponent(receiverType);
            Assert.IsNotNull(testReceiver, "contact test surface has no receiver");
            Vector3 testReceiverSize = (Vector3)GetMemberValue(testReceiver, "size");
            Assert.LessOrEqual(testReceiverSize.y, 0.035f,
                "receiver must activate only at contact or near-contact distance");

            int receiverCount = 0;
            foreach (MeshRenderer renderer in softRenderers)
            {
                Assert.IsNotNull(renderer.GetComponent(receiverType),
                    renderer.name + " has no VRCContactReceiver");

                Component controller = renderer.GetComponents<Component>()
                    .FirstOrDefault(component => component != null
                        && component.GetType().FullName
                        == "SabaProps.SoftProps.SoftSurfaceContactController");
                Assert.IsNotNull(controller, renderer.name + " has no contact controller proxy");

                Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                Assert.IsNotNull(mesh, renderer.name + " has no mesh");
                Assert.Greater(mesh.vertexCount, 700, renderer.name + " is under-subdivided");

                Color[] colors = mesh.colors;
                Assert.AreEqual(mesh.vertexCount, colors.Length, "deformation mask is missing");
                Assert.IsTrue(colors.Any(color => color.r > 0.95f), "mesh has no movable center");
                Assert.IsTrue(colors.Any(color => color.r < 0.01f), "mesh has no anchored seam");
                receiverCount++;
            }

            Assert.AreEqual(6, receiverCount);

            Shader shader = Shader.Find("SabaProps/Soft Surface");
            Assert.IsNotNull(shader, "soft surface shader was not imported");
            string shaderErrors = string.Join("\n", ShaderUtil.GetShaderMessages(shader)
                .Where(message => message.severity.ToString() == "Error")
                .Select(message => message.message));
            Assert.IsTrue(string.IsNullOrEmpty(shaderErrors), shaderErrors);

            ScriptableObject programAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                OutputRoot + "/SoftSurfaceContactController.asset");
            Assert.IsNotNull(programAsset, "UdonSharp program asset was not generated");

            PropertyInfo compiledVersion = programAsset.GetType().GetProperty("CompiledVersion");
            Assert.IsNotNull(compiledVersion, "UdonSharp compiled version is unavailable");
            Assert.Greater(Convert.ToInt32(compiledVersion.GetValue(programAsset)), 0,
                "UdonSharp program asset was not compiled");

            MethodInfo getSerializedProgram = programAsset.GetType().GetMethod(
                "GetSerializedUdonProgramAsset",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(getSerializedProgram, "serialized Udon program API is unavailable");
            Assert.IsNotNull(getSerializedProgram.Invoke(programAsset, null),
                "serialized Udon program was not generated");
        }

        private static object GetMemberValue(object target, string name)
        {
            Type type = target.GetType();
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(
                    name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    return field.GetValue(target);
                }
            }

            PropertyInfo property = type.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(property, type.FullName + "." + name + " is unavailable");
            return property.GetValue(target);
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
