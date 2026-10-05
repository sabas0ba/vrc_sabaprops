using SabaProps.Tablet.Authoring;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Tablet.Editors
{
    /// <summary>TabletDefinition の Inspector。既定の表示に、検査結果と Build の操作を加えます。</summary>
    [CustomEditor(typeof(TabletDefinition))]
    public class TabletDefinitionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var definition = (TabletDefinition)target;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Build", GUILayout.Height(28f)))
                {
                    TabletBuilder.Build(definition);
                }

                if (GUILayout.Button("Setup Window", GUILayout.Height(28f)))
                {
                    TabletSetupWindow.Open(definition);
                }
            }

            foreach (TabletIssue issue in TabletValidation.Validate(definition))
            {
                EditorGUILayout.HelpBox(issue.message, issue.type);
            }

            EditorGUILayout.HelpBox(
                "変更後は Build を実行してください。子の Body、Modules、Triggers は Build のたびに作り直します。",
                MessageType.None);
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}
