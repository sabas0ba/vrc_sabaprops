// Minimal UnityEditor surface used by com.sabaprops.foliage, so the Editor
// assembly can be compiled outside Unity.
//
// CAVEAT: these signatures are written by hand. They verify that the package's
// own code is internally consistent and syntactically valid, and (because the
// UnityEngine side uses real reference assemblies) that its UnityEngine usage
// is correct. They do NOT independently verify UnityEditor signatures.
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityEditor
{
    public enum MessageType { None = 0, Info = 1, Warning = 2, Error = 3 }

    [Flags]
    public enum StaticEditorFlags
    {
        ContributeGI = 1,
        OccluderStatic = 2,
        BatchingStatic = 4,
        NavigationStatic = 8,
        OccludeeStatic = 16,
        OffMeshLinkGeneration = 32,
        ReflectionProbeStatic = 64,
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CustomEditorAttribute : Attribute
    {
        public CustomEditorAttribute(Type inspectedType) { }
        public CustomEditorAttribute(Type inspectedType, bool editorForChildClasses) { }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CanEditMultipleObjectsAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MenuItemAttribute : Attribute
    {
        public MenuItemAttribute(string itemName) { }
        public MenuItemAttribute(string itemName, bool isValidateFunction) { }
        public MenuItemAttribute(string itemName, bool isValidateFunction, int priority) { }
    }

    public sealed class MenuCommand
    {
        public UnityEngine.Object context;
        public int userData;
    }

    public class SerializedProperty
    {
        public int enumValueIndex { get; set; }
        public int arraySize { get; set; }
        public UnityEngine.Object objectReferenceValue { get; set; }
        public bool boolValue { get; set; }
        public float floatValue { get; set; }
        public int intValue { get; set; }
        public string stringValue { get; set; }
        public SerializedProperty GetArrayElementAtIndex(int index) => null;
    }

    public class SerializedObject
    {
        public SerializedObject(UnityEngine.Object obj) { }
        public bool isEditingMultipleObjects => false;
        public SerializedProperty FindProperty(string propertyPath) => null;
        public void Update() { }
        public bool ApplyModifiedProperties() => false;
    }

    public class Editor : ScriptableObject
    {
        public UnityEngine.Object target { get; set; }
        public UnityEngine.Object[] targets { get; set; }
        public SerializedObject serializedObject { get; set; }

        public virtual void OnInspectorGUI() { }
        public void DrawDefaultInspector() { }

        public static void DrawPropertiesExcluding(SerializedObject obj, params string[] propertyToExclude) { }
    }

    public class MaterialProperty { }

    public class MaterialEditor : Editor
    {
        public void PropertiesDefaultGUI(MaterialProperty[] props) { }
    }

    public abstract class ShaderGUI
    {
        public virtual void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties) { }
    }

    public static class EditorStyles
    {
        public static GUIStyle boldLabel => null;
        public static GUIStyle miniLabel => null;
        public static GUIStyle miniBoldLabel => null;
        public static GUIStyle helpBox => null;
        public static GUIStyle foldoutHeader => null;
        public static GUIStyle label => null;
    }

    public static class ShaderUtil
    {
        public static bool ShaderHasError(Shader s) => false;
        public static int GetShaderMessageCount(Shader s) => 0;
        public static ShaderMessage[] GetShaderMessages(Shader s) =>
            new ShaderMessage[0];
    }

    public static class EditorGUIUtility
    {
        public static void PingObject(UnityEngine.Object obj) { }
        public static float currentViewWidth => 0f;
    }

    public static class EditorUtility
    {
        public static void SetDirty(UnityEngine.Object target) { }
        public static void CopySerialized(UnityEngine.Object source, UnityEngine.Object dest) { }
        public static bool DisplayProgressBar(string title, string info, float progress) => false;
        public static void ClearProgressBar() { }
        public static bool DisplayDialog(string title, string message, string ok) => false;
    }

    public static class AssetDatabase
    {
        public static bool IsValidFolder(string path) => false;
        public static string CreateFolder(string parentFolder, string newFolderName) => string.Empty;
        public static void CreateAsset(UnityEngine.Object asset, string path) { }
        public static bool DeleteAsset(string path) => false;
        public static T LoadAssetAtPath<T>(string assetPath) where T : UnityEngine.Object => null;
        public static string GetAssetPath(UnityEngine.Object assetObject) => string.Empty;
        public static string AssetPathToGUID(string path) => string.Empty;
        public static void SaveAssets() { }
        public static void Refresh() { }
        public static void StartAssetEditing() { }
        public static void StopAssetEditing() { }
        public static void AddObjectToAsset(UnityEngine.Object objectToAdd, UnityEngine.Object assetObject) { }
    }

    public static class Selection
    {
        public static GameObject activeGameObject { get; set; }
        public static UnityEngine.Object activeObject { get; set; }
        public static UnityEngine.Object[] objects { get; set; }
    }

    public static class Undo
    {
        public static void RegisterCreatedObjectUndo(UnityEngine.Object objectToUndo, string name) { }
        public static void DestroyObjectImmediate(UnityEngine.Object objectToUndo) { }
        public static void RecordObject(UnityEngine.Object objectToUndo, string name) { }
        public static int GetCurrentGroup() => 0;
        public static void SetCurrentGroupName(string name) { }
        public static void CollapseUndoOperations(int groupIndex) { }
        public static void SetTransformParent(Transform transform, Transform newParent, string name) { }
    }

    public static class GameObjectUtility
    {
        public static void SetParentAndAlign(GameObject child, GameObject parent) { }
        public static void SetStaticEditorFlags(GameObject go, StaticEditorFlags flags) { }
        public static StaticEditorFlags GetStaticEditorFlags(GameObject go) => default;
    }

    public static class HandleUtility
    {
        public static float GetHandleSize(Vector3 position) => 1f;
    }

    public static class Handles
    {
        public delegate void CapFunction(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType);

        public static Color color { get; set; }
        public static Matrix4x4 matrix { get; set; }

        public static void DrawWireCube(Vector3 center, Vector3 size) { }
        public static void DrawLine(Vector3 p1, Vector3 p2) { }

        public static float RadiusHandle(Quaternion rotation, Vector3 position, float radius) => radius;

        public static float ScaleValueHandle(
            float value, Vector3 position, Quaternion rotation, float size,
            CapFunction capFunction, float snap) => value;

        public static void ConeHandleCap(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType) { }
        public static void SphereHandleCap(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType) { }

        public struct DrawingScope : IDisposable
        {
            public DrawingScope(Color color) { }
            public DrawingScope(Matrix4x4 matrix) { }
            public DrawingScope(Color color, Matrix4x4 matrix) { }
            public void Dispose() { }
        }
    }

    public class SceneView
    {
        public static SceneView lastActiveSceneView => null;
        public Vector3 pivot { get; set; }
        public void Repaint() { }
    }

    public static class EditorGUI
    {
        public static void BeginChangeCheck() { }
        public static bool EndChangeCheck() => false;

        public class IndentLevelScope : IDisposable
        {
            public IndentLevelScope() { }
            public IndentLevelScope(int increment) { }
            public void Dispose() { }
        }

        public class DisabledScope : IDisposable
        {
            public DisabledScope(bool disabled) { }
            public void Dispose() { }
        }
    }

    public static class EditorGUILayout
    {
        public static void Space() { }
        public static void Space(float width) { }

        public static void LabelField(string label, params GUILayoutOption[] options) { }
        public static void LabelField(string label, GUIStyle style, params GUILayoutOption[] options) { }
        public static void LabelField(string label, string label2, params GUILayoutOption[] options) { }
        public static void LabelField(string label, string label2, GUIStyle style, params GUILayoutOption[] options) { }

        public static bool PropertyField(SerializedProperty property, params GUILayoutOption[] options) => false;
        public static bool PropertyField(SerializedProperty property, bool includeChildren, params GUILayoutOption[] options) => false;

        public static void HelpBox(string message, MessageType type) { }
        public static void HelpBox(string message, MessageType type, bool wide) { }

        public static bool Foldout(bool foldout, string content, bool toggleOnLabelClick, GUIStyle style) => foldout;
        public static bool Foldout(bool foldout, string content) => foldout;

        public static UnityEngine.Object ObjectField(UnityEngine.Object obj, Type objType, bool allowSceneObjects, params GUILayoutOption[] options) => obj;

        public class HorizontalScope : IDisposable
        {
            public HorizontalScope(params GUILayoutOption[] options) { }
            public HorizontalScope(GUIStyle style, params GUILayoutOption[] options) { }
            public void Dispose() { }
        }

        public class VerticalScope : IDisposable
        {
            public VerticalScope(params GUILayoutOption[] options) { }
            public VerticalScope(GUIStyle style, params GUILayoutOption[] options) { }
            public void Dispose() { }
        }
    }
}

namespace UnityEditor.SceneManagement
{
    public static class EditorSceneManager
    {
        public static bool MarkSceneDirty(Scene scene) => false;
    }
}

// ShaderMessage lives in UnityEditor, not UnityEditor.Rendering, in 2022.3.
// Declaring it anywhere else makes this harness agree with a test file that
// real Unity rejects.
namespace UnityEditor
{
    public struct ShaderMessage
    {
        public string message { get; }
        public string messageDetails { get; }
        public string file { get; }
        public int line { get; }
    }
}

// Enough NUnit to compile-check the CI project's EditMode tests offline. The
// real assertions run inside Unity via the Unity workflow.
namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TestAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class SetUpAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TearDownAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class OneTimeSetUpAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class OneTimeTearDownAttribute : Attribute { }

    public static class Assert
    {
        public static void Fail(string message) { }

        public static void IsNull(object value) { }
        public static void IsNull(object value, string message) { }
        public static void IsNotNull(object value) { }
        public static void IsNotNull(object value, string message) { }

        public static void IsTrue(bool condition) { }
        public static void IsTrue(bool condition, string message) { }
        public static void IsFalse(bool condition) { }
        public static void IsFalse(bool condition, string message) { }

        public static void AreEqual(object expected, object actual) { }
        public static void AreEqual(object expected, object actual, string message) { }
        public static void AreEqual(double expected, double actual, double delta) { }
        public static void AreEqual(double expected, double actual, double delta, string message) { }

        public static void AreSame(object expected, object actual) { }
        public static void AreSame(object expected, object actual, string message) { }

        public static void Greater(double arg1, double arg2) { }
        public static void Greater(double arg1, double arg2, string message) { }
        public static void Less(double arg1, double arg2) { }
        public static void Less(double arg1, double arg2, string message) { }
    }
}
