using System;
using System.Reflection;
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
