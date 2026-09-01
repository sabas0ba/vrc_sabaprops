using System;
using System.Reflection;
using UnityEngine;

namespace SabaProps.Water.Editors
{
    /// <summary>
    /// Creates a portable spawn root and adds VRCSceneDescriptor when the
    /// VRChat Worlds SDK is installed. Reflection keeps the SDK optional.
    /// </summary>
    public static class WaterVrcWorld
    {
        public const string WorldObjectName = "VRCWorld";
        public const string SpawnObjectName = "Spawn";
        public const string DescriptorTypeName = "VRC.SDK3.Components.VRCSceneDescriptor";

        public static bool IsSdkPresent => FindType(DescriptorTypeName) != null;

        /// <summary>
        /// Always creates VRCWorld and Spawn so the prebuilt UPM sample has a
        /// stable inspection point. The SDK component is added when available.
        /// </summary>
        public static GameObject CreateWorld(
            Vector3 spawnPosition,
            Quaternion spawnRotation,
            Camera referenceCamera)
        {
            GameObject world = GameObject.Find(WorldObjectName) ?? new GameObject(WorldObjectName);
            Transform spawnTransform = world.transform.Find(SpawnObjectName);
            GameObject spawn = spawnTransform != null
                ? spawnTransform.gameObject
                : new GameObject(SpawnObjectName);
            spawn.transform.SetParent(world.transform, false);
            spawn.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

            Type descriptorType = FindType(DescriptorTypeName);
            if (descriptorType == null)
            {
                return world;
            }

            Component descriptor = world.GetComponent(descriptorType) ?? world.AddComponent(descriptorType);
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
                $"[SabaProps Water] VRCSceneDescriptor の '{memberName}' を設定できませんでした。" +
                "SDK のバージョン差の可能性があります。Inspector で確認してください。");
        }
    }
}
