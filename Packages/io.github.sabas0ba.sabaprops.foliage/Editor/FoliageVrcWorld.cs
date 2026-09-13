using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Foliage.Editors
{
    /// <summary>
    /// Adds a VRChat scene descriptor and spawn point when the Worlds SDK is
    /// installed in the project.
    /// <para>
    /// The SDK is bound by reflection rather than by an assembly reference. This
    /// package declares no VPM dependencies and is usable in avatar projects, so
    /// it has to compile with no VRChat SDK present. An asmdef reference would
    /// make the SDK mandatory, and an optional assembly gated by
    /// defineConstraints still has to resolve those references at import time.
    /// </para>
    /// </summary>
    public static class FoliageVrcWorld
    {
        public const string WorldObjectName = "VRCWorld";
        public const string SpawnObjectName = "Spawn";

        public const string DescriptorTypeName = "VRC.SDK3.Components.VRCSceneDescriptor";

        /// <summary>True when the VRChat Worlds SDK is present in this project.</summary>
        public static bool IsSdkPresent => FindType(DescriptorTypeName) != null;

        /// <summary>
        /// Creates the world root, one spawn point and the scene descriptor.
        /// Returns null without touching the scene when the SDK is absent.
        /// </summary>
        public static GameObject TryCreateWorld(
            Vector3 spawnPosition, Quaternion spawnRotation, Camera referenceCamera)
        {
            Type descriptorType = FindType(DescriptorTypeName);
            if (descriptorType == null)
            {
                return null;
            }

            var world = new GameObject(WorldObjectName);

            var spawn = new GameObject(SpawnObjectName);
            spawn.transform.SetParent(world.transform, false);
            spawn.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

            Component descriptor = world.AddComponent(descriptorType);

            TrySet(descriptor, "spawns", new[] { spawn.transform });
            TrySet(descriptor, "RespawnHeightY", -50f);

            if (referenceCamera != null)
            {
                TrySet(descriptor, "ReferenceCamera", referenceCamera.gameObject);
            }

            return world;
        }

        /// <summary>Type name of the sample movement behaviour, once imported.</summary>
        public const string MovementTypeName = "FoliageDemoMovement";

        /// <summary>
        /// Creates the UdonSharp program asset that pairs with an imported
        /// behaviour script, next to it and named after it.
        /// <para>
        /// An UdonSharp behaviour is two files: the script, and a program asset
        /// that the compiler fills with the Udon program the script becomes.
        /// Without the second one the behaviour is an inert component, and the
        /// SDK says so — "Unable to find valid U# program asset associated with
        /// script". Unity's own New U# Script menu creates the pair together;
        /// copying a script in has to do the same.
        /// </para>
        /// </summary>
        public static bool TryCreateUdonProgramAsset(string scriptAssetPath)
        {
            Type programAssetType = FindType("UdonSharp.UdonSharpProgramAsset");
            if (programAssetType == null)
            {
                Debug.LogWarning(
                    "[SabaProps Foliage] UdonSharp.UdonSharpProgramAsset が見つかりません。"
                    + "移動設定のプログラムアセットは作成していません。");
                return false;
            }

            string assetPath = Path.ChangeExtension(scriptAssetPath, ".asset");
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath) != null)
            {
                return true;
            }

            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptAssetPath);
            if (script == null)
            {
                Debug.LogWarning($"[SabaProps Foliage] {scriptAssetPath} を MonoScript として読めませんでした。");
                return false;
            }

            ScriptableObject programAsset = ScriptableObject.CreateInstance(programAssetType);

            FieldInfo sourceField = programAssetType.GetField(
                "sourceCsScript", BindingFlags.Public | BindingFlags.Instance);

            if (sourceField == null)
            {
                Debug.LogWarning(
                    "[SabaProps Foliage] UdonSharpProgramAsset に 'sourceCsScript' がありません。"
                    + "UdonSharp のバージョン差の可能性があります。");
                UnityEngine.Object.DestroyImmediate(programAsset);
                return false;
            }

            sourceField.SetValue(programAsset, script);
            AssetDatabase.CreateAsset(programAsset, assetPath);
            AssetDatabase.SaveAssets();

            return true;
        }

        /// <summary>
        /// Adds the demo movement behaviour to the world root, if the sample has
        /// been imported into the project.
        /// <para>
        /// Movement speed and jumping are not fields on the scene descriptor:
        /// VRChat applies them through VRCPlayerApi at runtime, so a world that
        /// wants them needs Udon. The behaviour therefore ships in Samples~ and
        /// is imported on request — see
        /// <c>Tools > SabaProps > Debug > Foliage > Import VRChat Demo Movement</c>.
        /// Absent, this does nothing and the demo walks at VRChat's defaults.
        /// </para>
        /// </summary>
        /// <returns>True when the behaviour was added.</returns>
        public static bool TryAddDemoMovement(GameObject world)
        {
            if (world == null)
            {
                return false;
            }

            Type behaviourType = FindType(MovementTypeName);
            if (behaviourType == null)
            {
                return false;
            }

            // AddUdonSharpComponent, rather than AddComponent: an UdonSharp
            // behaviour added on its own is an inert proxy, and this is what
            // pairs it with the UdonBehaviour that actually runs.
            //
            // It is declared as an extension method on GameObject, so it is
            // reached here as the plain static it compiles down to.
            Type extensions = FindType("UdonSharpEditor.UdonSharpComponentExtensions");

            MethodInfo add = extensions == null
                ? null
                : extensions.GetMethod(
                    "AddUdonSharpComponent",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(GameObject), typeof(Type) },
                    null);

            if (add == null)
            {
                Debug.LogWarning(
                    "[SabaProps Foliage] UdonSharpComponentExtensions.AddUdonSharpComponent が見つかりません。"
                    + "UdonSharp のバージョン差の可能性があります。移動設定は追加していません。");
                return false;
            }

            return add.Invoke(null, new object[] { world, behaviourType }) != null;
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

        /// <summary>
        /// Assigns a public field or property if it still exists. A member
        /// renamed between SDK versions then costs one warning rather than a
        /// broken scene.
        /// </summary>
        private static void TrySet(Component target, string memberName, object value)
        {
            Type type = target.GetType();

            FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.FieldType.IsInstanceOfType(value))
            {
                field.SetValue(target, value);
                return;
            }

            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.CanWrite && property.PropertyType.IsInstanceOfType(value))
            {
                property.SetValue(target, value);
                return;
            }

            Debug.LogWarning(
                $"[SabaProps Foliage] VRCSceneDescriptor の '{memberName}' を設定できませんでした。" +
                "SDK のバージョン差の可能性があります。Inspector で手動設定してください。");
        }
    }
}
