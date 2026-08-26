using UnityEditor;
using UnityEngine;

namespace SabaProps.Trees.Editors
{
    [CustomEditor(typeof(TreeField))]
    [CanEditMultipleObjects]
    public sealed class TreeFieldEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(
                serializedObject, "m_Script", "generatedRoot", "lastBuildStats");
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space(8f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate", GUILayout.Height(28f)))
                {
                    foreach (Object each in targets)
                    {
                        TreeFieldBuilder.Build((TreeField)each);
                    }
                }

                if (GUILayout.Button(
                    "Clear", GUILayout.Height(28f), GUILayout.Width(90f)))
                {
                    foreach (Object each in targets)
                    {
                        TreeFieldBuilder.Clear((TreeField)each);
                    }
                }
            }

            if (!serializedObject.isEditingMultipleObjects)
            {
                DrawStats((TreeField)target);
            }
        }

        private static void DrawStats(TreeField field)
        {
            TreeBuildStats stats = field.lastBuildStats;
            if (stats == null || stats.instanceCount <= 0)
            {
                return;
            }

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Last Build", EditorStyles.boldLabel);
                Row("Instances", $"{stats.instanceCount:N0}");
                Row("Renderers", $"{stats.rendererCount:N0}");
                Row("LOD0 triangles", $"{stats.lod0TriangleCount:N0}");
                Row("LOD0 vertices", $"{stats.lod0VertexCount:N0}");
                Row("Build time", $"{stats.buildSeconds:0.00} s");
            }
        }

        private static void Row(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, GUILayout.Width(140f));
                EditorGUILayout.LabelField(value, EditorStyles.miniBoldLabel);
            }
        }
    }
}
