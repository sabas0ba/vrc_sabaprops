using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SabaProps.SoftProps.Editors
{
    /// <summary>
    /// SDKとUdonSharpのEditor API差分を局所化する。packageはWorlds SDK
    /// 3.10.4以上を要求するが、公開Editor APIのassembly名には依存しない。
    /// </summary>
    internal static class SoftPropsVrcBridge
    {
        private const string ReceiverTypeName =
            "VRC.SDK3.Dynamics.Contact.Components.VRCContactReceiver";
        private const string SenderTypeName =
            "VRC.SDK3.Dynamics.Contact.Components.VRCContactSender";
        private const string PickupTypeName = "VRC.SDK3.Components.VRCPickup";
        private const string ObjectSyncTypeName = "VRC.SDK3.Components.VRCObjectSync";

        private const string ProgramAssetTypeName = "UdonSharp.UdonSharpProgramAsset";
        private const string ExtensionTypeName = "UdonSharpEditor.UdonSharpComponentExtensions";
        private const string CompilerTypeName = "UdonSharp.Compiler.UdonSharpCompilerV1";
        private const string UdonAssemblyTypeName =
            "UdonSharpEditor.UdonSharpAssemblyDefinition";
        private const string ControllerScriptPath =
            "Packages/io.github.sabas0ba.sabaprops.softprops/Runtime/SoftSurfaceContactController.cs";
        private const string RuntimeAssemblyPath =
            "Packages/io.github.sabas0ba.sabaprops.softprops/Runtime/SabaProps.SoftProps.Runtime.asmdef";
        private const string UdonAssemblyAssetPath =
            "Packages/io.github.sabas0ba.sabaprops.softprops/Runtime/SabaProps.SoftProps.UdonSharp.asset";

        public static bool IsAvailable(out string reason)
        {
            if (FindType(ReceiverTypeName) == null || FindType(SenderTypeName) == null)
            {
                reason = "VRChat Worlds SDK 3.10.xのWorld Contactsが見つかりません。";
                return false;
            }

            if (FindType(ProgramAssetTypeName) == null
                || FindType(ExtensionTypeName) == null
                || FindType(UdonAssemblyTypeName) == null)
            {
                reason = "UdonSharpのEditor APIが見つかりません。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public static SoftSurfaceContactController AddController(GameObject target, string programAssetPath)
        {
            EnsureProgramAsset(programAssetPath);

            Type extensions = FindType(ExtensionTypeName);
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
                throw new InvalidOperationException(
                    "UdonSharpComponentExtensions.AddUdonSharpComponentを解決できません。");
            }

            object result = add.Invoke(null, new object[]
            {
                target,
                typeof(SoftSurfaceContactController),
            });

            var controller = result as SoftSurfaceContactController;
            if (controller == null)
            {
                throw new InvalidOperationException("SoftSurfaceContactControllerを追加できませんでした。");
            }

            return controller;
        }

        public static Component AddBoxReceiver(
            GameObject target,
            Vector3 size,
            Vector3 position,
            string[] collisionTags)
        {
            Type receiverType = FindType(ReceiverTypeName);
            if (receiverType == null)
            {
                throw new InvalidOperationException("VRCContactReceiverが見つかりません。");
            }

            Component receiver = target.AddComponent(receiverType);
            SetMember(receiver, "rootTransform", target.transform);
            SetEnumMember(receiver, "shapeType", "Box");
            SetMember(receiver, "size", size);
            SetMember(receiver, "position", position);
            SetMember(receiver, "rotation", Quaternion.identity);

            MethodInfo updateTags = receiverType.GetMethod(
                "UpdateCollisionTags",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(string[]) },
                null);
            if (updateTags == null)
            {
                throw new MissingMethodException(receiverType.FullName, "UpdateCollisionTags");
            }

            updateTags.Invoke(receiver, new object[] { collisionTags });

            MethodInfo apply = receiverType.GetMethod(
                "ApplyConfigurationChanges",
                BindingFlags.Public | BindingFlags.Instance);
            if (apply != null)
            {
                apply.Invoke(receiver, null);
            }

            return receiver;
        }

        public static Component AddContactSender(
            GameObject target,
            string shape,
            float radius,
            float height,
            Vector3 size,
            string collisionTag)
        {
            Type senderType = FindType(SenderTypeName);
            if (senderType == null)
            {
                throw new InvalidOperationException("VRCContactSenderが見つかりません。");
            }

            Component sender = target.AddComponent(senderType);
            SetMember(sender, "rootTransform", target.transform);
            SetEnumMember(sender, "shapeType", shape);
            SetMember(sender, "position", Vector3.zero);
            SetMember(sender, "rotation", Quaternion.identity);

            if (shape == "Box")
            {
                SetMember(sender, "size", size);
            }
            else
            {
                SetMember(sender, "radius", radius);
                if (shape == "Capsule")
                {
                    SetMember(sender, "height", height);
                }
            }

            UpdateCollisionTags(senderType, sender, new[] { collisionTag });
            ApplyConfigurationChanges(senderType, sender);
            return sender;
        }

        public static void AddPickup(GameObject target)
        {
            Type pickupType = FindType(PickupTypeName);
            Type objectSyncType = FindType(ObjectSyncTypeName);
            if (pickupType == null || objectSyncType == null)
            {
                throw new InvalidOperationException("VRCPickupまたはVRCObjectSyncが見つかりません。");
            }

            target.AddComponent(objectSyncType);
            target.AddComponent(pickupType);
        }

        public static void CompileControllerProgram(string programAssetPath)
        {
            ScriptableObject programAsset =
                AssetDatabase.LoadAssetAtPath<ScriptableObject>(programAssetPath);
            if (programAsset == null)
            {
                throw new InvalidOperationException(
                    "SoftSurfaceContactControllerのUdonSharp program assetが見つかりません。");
            }

            // CreateAsset後のpostprocessorにcache更新を確実に通知してから、Worldへ
            // 配置可能なserialized Udon programまで同期的に生成する。
            AssetDatabase.ImportAsset(programAssetPath, ImportAssetOptions.ForceUpdate);

            Type compilerType = FindType(CompilerTypeName);
            MethodInfo compileSync = compilerType == null
                ? null
                : compilerType.GetMethod(
                    "CompileSync",
                    BindingFlags.Public | BindingFlags.Static);
            if (compileSync == null)
            {
                throw new MissingMethodException(CompilerTypeName, "CompileSync");
            }

            // UdonSharpCompileOptionsのoptional argumentへnullを渡し、SDK既定値を使う。
            compileSync.Invoke(null, new object[] { null });

            programAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(programAssetPath);
            MethodInfo updateProgram = programAsset.GetType().GetMethod(
                "UpdateProgram",
                BindingFlags.Public | BindingFlags.Instance);
            MethodInfo getSerializedProgram = programAsset.GetType().GetMethod(
                "GetSerializedUdonProgramAsset",
                BindingFlags.Public | BindingFlags.Instance);
            if (updateProgram == null || getSerializedProgram == null)
            {
                throw new MissingMethodException(
                    programAsset.GetType().FullName,
                    "UpdateProgram/GetSerializedUdonProgramAsset");
            }

            updateProgram.Invoke(programAsset, null);
            if (getSerializedProgram.Invoke(programAsset, null) == null)
            {
                throw new InvalidOperationException(
                    "SoftSurfaceContactControllerのUdonSharp compileに失敗しました。Consoleを確認してください。");
            }
        }

        private static void EnsureProgramAsset(string programAssetPath)
        {
            EnsureUdonAssemblyDefinition();

            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(programAssetPath) != null)
            {
                return;
            }

            Type programAssetType = FindType(ProgramAssetTypeName);
            var source = AssetDatabase.LoadAssetAtPath<MonoScript>(ControllerScriptPath);
            if (programAssetType == null || source == null)
            {
                throw new InvalidOperationException(
                    "SoftSurfaceContactControllerのUdonSharp program assetを作成できません。");
            }

            ScriptableObject programAsset = ScriptableObject.CreateInstance(programAssetType);
            FieldInfo sourceField = FindField(programAssetType, "sourceCsScript");
            if (sourceField == null)
            {
                UnityEngine.Object.DestroyImmediate(programAsset);
                throw new MissingFieldException(programAssetType.FullName, "sourceCsScript");
            }

            sourceField.SetValue(programAsset, source);
            AssetDatabase.CreateAsset(programAsset, programAssetPath);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureUdonAssemblyDefinition()
        {
            Type assemblyDefinitionType = FindType(UdonAssemblyTypeName);
            UnityEngine.Object sourceAssembly =
                AssetDatabase.LoadMainAssetAtPath(RuntimeAssemblyPath);
            if (assemblyDefinitionType == null || sourceAssembly == null)
            {
                throw new InvalidOperationException(
                    "Soft Props runtime assemblyのUdonSharp定義を作成できません。");
            }

            ScriptableObject definition =
                AssetDatabase.LoadAssetAtPath<ScriptableObject>(UdonAssemblyAssetPath);
            if (definition == null)
            {
                throw new InvalidOperationException(
                    "package同梱のUdonSharp assembly definitionが見つかりません。");
            }

            FieldInfo sourceField = FindField(assemblyDefinitionType, "sourceAssembly");
            if (sourceField == null || !sourceField.FieldType.IsInstanceOfType(sourceAssembly))
            {
                throw new MissingFieldException(assemblyDefinitionType.FullName, "sourceAssembly");
            }

            if (sourceField.GetValue(definition) != sourceAssembly)
            {
                throw new InvalidOperationException(
                    "UdonSharp assembly definitionがSoft Props runtime assemblyを参照していません。");
            }
        }

        private static void UpdateCollisionTags(
            Type componentType,
            Component component,
            string[] collisionTags)
        {
            MethodInfo updateTags = componentType.GetMethod(
                "UpdateCollisionTags",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(string[]) },
                null);
            if (updateTags == null)
            {
                throw new MissingMethodException(componentType.FullName, "UpdateCollisionTags");
            }

            updateTags.Invoke(component, new object[] { collisionTags });
        }

        private static void ApplyConfigurationChanges(Type componentType, Component component)
        {
            MethodInfo apply = componentType.GetMethod(
                "ApplyConfigurationChanges",
                BindingFlags.Public | BindingFlags.Instance);
            if (apply != null)
            {
                apply.Invoke(component, null);
            }
        }

        private static void SetEnumMember(object target, string name, string enumName)
        {
            Type memberType = GetMemberType(target.GetType(), name);
            if (memberType == null || !memberType.IsEnum)
            {
                throw new MissingMemberException(target.GetType().FullName, name);
            }

            SetMember(target, name, Enum.Parse(memberType, enumName));
        }

        private static void SetMember(object target, string name, object value)
        {
            Type type = target.GetType();
            FieldInfo field = FindField(type, name);
            if (field != null && field.FieldType.IsInstanceOfType(value))
            {
                field.SetValue(target, value);
                return;
            }

            PropertyInfo property = type.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property != null && property.CanWrite && property.PropertyType.IsInstanceOfType(value))
            {
                property.SetValue(target, value);
                return;
            }

            throw new MissingMemberException(type.FullName, name);
        }

        private static Type GetMemberType(Type type, string name)
        {
            FieldInfo field = FindField(type, name);
            if (field != null)
            {
                return field.FieldType;
            }

            PropertyInfo property = type.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return property == null ? null : property.PropertyType;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(
                    name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
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
