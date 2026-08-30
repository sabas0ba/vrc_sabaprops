// Minimal UnityEditor surface used by io.github.sabas0ba.sabaprops.foliage,
// so the Editor assembly can be compiled outside Unity.
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

    [Flags]
    public enum GizmoType
    {
        Pickable = 1,
        NotInSelectionHierarchy = 2,
        Selected = 4,
        Active = 8,
        InSelectionHierarchy = 16,
        NonSelected = 32,
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class DrawGizmoAttribute : Attribute
    {
        public DrawGizmoAttribute(GizmoType gizmo) { }
        public DrawGizmoAttribute(GizmoType gizmo, Type drawnGizmoType) { }
    }

    public class SerializedProperty
    {
        public int enumValueIndex { get; set; }
        public int arraySize { get; set; }
        public bool hasMultipleDifferentValues { get; set; }
        public UnityEngine.Object objectReferenceValue { get; set; }
        public bool boolValue { get; set; }
        public float floatValue { get; set; }
        public int intValue { get; set; }
        public string stringValue { get; set; }
        public SerializedProperty FindPropertyRelative(string relativePropertyPath) => null;
        public SerializedProperty GetArrayElementAtIndex(int index) => null;
        public void InsertArrayElementAtIndex(int index) { }
        public void DeleteArrayElementAtIndex(int index) { }
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

    public class EditorWindow : ScriptableObject
    {
        public Vector2 minSize { get; set; }
        public Vector2 maxSize { get; set; }
        public string title { get; set; }

        public static T GetWindow<T>(bool utility, string title, bool focus) where T : EditorWindow =>
            CreateInstance<T>();

        public void Show() { }
        public void ShowUtility() { }
        public void Close() { }
        public void Repaint() { }
    }

    public class MonoScript : TextAsset { }

    [Flags]
    public enum ImportAssetOptions
    {
        Default = 0,
        ForceUpdate = 1,
        ForceSynchronousImport = 8,
        ImportRecursive = 256,
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
        public static GUIStyle wordWrappedMiniLabel => null;
        public static GUIStyle whiteBoldLabel => null;
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
        public static string GenerateUniqueAssetPath(string path) => path;
        public static void SaveAssets() { }
        public static void Refresh() { }
        public static void ImportAsset(string path) { }
        public static void ImportAsset(string path, ImportAssetOptions options) { }
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
        public static void AddDefaultControl(int controlId) { }
        public static Ray GUIPointToWorldRay(Vector2 position) => default;
    }

    public static class Handles
    {
        public delegate void CapFunction(int controlID, Vector3 position, Quaternion rotation, float size, EventType eventType);

        public static Color color { get; set; }
        public static Matrix4x4 matrix { get; set; }

        public static void DrawWireCube(Vector3 center, Vector3 size) { }
        public static void DrawWireDisc(Vector3 center, Vector3 normal, float radius) { }
        public static void Label(Vector3 position, string text) { }
        public static void Label(Vector3 position, string text, GUIStyle style) { }
        public static void DrawLine(Vector3 p1, Vector3 p2) { }

        public static float RadiusHandle(Quaternion rotation, Vector3 position, float radius) => radius;
        public static Vector3 PositionHandle(Vector3 position, Quaternion rotation) => position;

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
        public static event Action<SceneView> duringSceneGui;
        public static SceneView lastActiveSceneView => null;
        public Vector3 pivot { get; set; }
        public void Repaint() { }
        public void LookAt(Vector3 point, Quaternion direction, float newSize) { }
        public static void RepaintAll() { }
    }

    public class SceneAsset : UnityEngine.Object { }

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
        public static bool PropertyField(SerializedProperty property, GUIContent label, params GUILayoutOption[] options) => false;
        public static bool PropertyField(SerializedProperty property, GUIContent label, bool includeChildren, params GUILayoutOption[] options) => false;

        public static void HelpBox(string message, MessageType type) { }
        public static void HelpBox(string message, MessageType type, bool wide) { }

        public static bool Foldout(bool foldout, string content, bool toggleOnLabelClick, GUIStyle style) => foldout;
        public static bool Foldout(bool foldout, string content) => foldout;

        public static UnityEngine.Object ObjectField(UnityEngine.Object obj, Type objType, bool allowSceneObjects, params GUILayoutOption[] options) => obj;
        public static UnityEngine.Object ObjectField(string label, UnityEngine.Object obj, Type objType, bool allowSceneObjects, params GUILayoutOption[] options) => obj;

        public static bool Toggle(string label, bool value, params GUILayoutOption[] options) => value;
        public static bool ToggleLeft(string label, bool value, params GUILayoutOption[] options) => value;

        public static float Slider(float value, float leftValue, float rightValue, params GUILayoutOption[] options) => value;
        public static float Slider(string label, float value, float leftValue, float rightValue, params GUILayoutOption[] options) => value;

        public static float FloatField(float value, params GUILayoutOption[] options) => value;
        public static float FloatField(string label, float value, params GUILayoutOption[] options) => value;

        public static int IntField(string label, int value, params GUILayoutOption[] options) => value;
        public static int IntSlider(string label, int value, int leftValue, int rightValue, params GUILayoutOption[] options) => value;

        public static Vector2 Vector2Field(string label, Vector2 value, params GUILayoutOption[] options) => value;

        public static Enum EnumPopup(string label, Enum selected, params GUILayoutOption[] options) => selected;

        public static Vector2 BeginScrollView(Vector2 position, params GUILayoutOption[] options) => position;
        public static void EndScrollView() { }

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
    public enum NewSceneSetup { EmptyScene = 0, DefaultGameObjects = 1 }

    public enum NewSceneMode { Single = 0, Additive = 1 }

    public static class EditorSceneManager
    {
        public static bool MarkSceneDirty(Scene scene) => false;
        public static Scene NewScene(NewSceneSetup setup, NewSceneMode mode) => default;
        public static bool SaveScene(Scene scene, string dstScenePath) => false;
        public static bool SaveCurrentModifiedScenesIfUserWantsTo() => false;
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
        public static void AreNotEqual(object expected, object actual) { }
        public static void AreNotEqual(object expected, object actual, string message) { }

        public static void AreSame(object expected, object actual) { }
        public static void AreSame(object expected, object actual, string message) { }

        public static void Contains(object expected, System.Collections.ICollection actual) { }
        public static void Contains(object expected, System.Collections.ICollection actual, string message) { }

        public static void Greater(double arg1, double arg2) { }
        public static void Greater(double arg1, double arg2, string message) { }
        public static void Less(double arg1, double arg2) { }
        public static void Less(double arg1, double arg2, string message) { }
    }
}

namespace UnityEditor.PackageManager
{
    /// <summary>
    /// Only what PackagePath() needs: where this package resolved to on disk.
    /// FindForAssembly returns null here, which is the "package not found" path
    /// the caller already has to handle.
    /// </summary>
    public class PackageInfo
    {
        public string resolvedPath { get; set; }

        public static PackageInfo FindForAssembly(System.Reflection.Assembly assembly) => null;
    }
}
