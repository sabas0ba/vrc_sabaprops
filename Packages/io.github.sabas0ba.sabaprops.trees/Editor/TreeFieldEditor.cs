using UnityEditor;
using UnityEngine;
using SabaProps.Foliage.Editors;

namespace SabaProps.Trees.Editors
{
    [CustomEditor(typeof(TreeField))]
    [CanEditMultipleObjects]
    public sealed class TreeFieldEditor : UnityEditor.Editor
    {
        private SerializedProperty _autoRebuild;

        private void OnEnable()
        {
            _autoRebuild = serializedObject.FindProperty("autoRebuild");
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(
                serializedObject, "m_Script", "generatedRoot", "lastBuildStats",
                "autoRebuild");
            EditorGUILayout.PropertyField(
                _autoRebuild,
                new GUIContent(SabaPropsEditorLocalization.Text(
                    "値変更時に自動再生成",
                    "Auto Rebuild on Changes")));
            EditorGUILayout.HelpBox(
                SabaPropsEditorLocalization.Text(
                    "生成済みの内容だけを値変更後に更新します。初回は Generate を使用してください。Component は World ビルド時に自動除外されるため、手動で削除する必要はありません。",
                    "Updates existing generated content after value changes. Use Generate for the first build. The component is automatically excluded from world builds and does not need to be removed manually."),
                MessageType.None);

            if (serializedObject.ApplyModifiedProperties())
            {
                ScheduleAutoRebuilds();
            }
            EditorGUILayout.Space(8f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate", GUILayout.Height(28f)))
                {
                    foreach (Object each in targets)
                    {
                        SabaPropsAutoRebuild.Cancel(each);
                        TreeFieldBuilder.Build((TreeField)each);
                    }
                }

                if (GUILayout.Button(
                    "Clear", GUILayout.Height(28f), GUILayout.Width(90f)))
                {
                    foreach (Object each in targets)
                    {
                        SabaPropsAutoRebuild.Cancel(each);
                        TreeFieldBuilder.Clear((TreeField)each);
                    }
                }
            }

            if (!serializedObject.isEditingMultipleObjects)
            {
                DrawStats((TreeField)target);
            }
        }

        private void OnUndoRedo()
        {
            ScheduleAutoRebuilds();
        }

        private void ScheduleAutoRebuilds()
        {
            foreach (Object each in targets)
            {
                var field = each as TreeField;
                if (field == null || !field.autoRebuild || !HasGeneratedOutput(field))
                {
                    SabaPropsAutoRebuild.Cancel(each);
                    continue;
                }

                SabaPropsAutoRebuild.Schedule(
                    field,
                    () => TreeFieldBuilder.Build(field, false));
            }
        }

        private static bool HasGeneratedOutput(TreeField field)
        {
            return field.generatedRoot != null ||
                field.transform.Find(TreeField.GeneratedRootName) != null;
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
