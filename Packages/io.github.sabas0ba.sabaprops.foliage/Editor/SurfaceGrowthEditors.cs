using UnityEditor;
using UnityEngine;

namespace SabaProps.Foliage.Editors
{
    [CustomEditor(typeof(SurfaceVine))]
    public sealed class SurfaceVineEditor : UnityEditor.Editor
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
            var vine = (SurfaceVine)target;
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "autoRebuild");
            EditorGUILayout.PropertyField(
                _autoRebuild,
                new GUIContent(SabaPropsEditorLocalization.Text(
                    "値変更時に自動再生成",
                    "Auto Rebuild on Changes")));
            if (serializedObject.ApplyModifiedProperties())
            {
                ScheduleAutoRebuild(vine);
            }

            DrawBuildExclusionHelp();

            EditorGUILayout.Space(8f);
            if (vine.generatedGraph != null)
            {
                EditorGUILayout.LabelField(
                    "Generated",
                    vine.generatedGraph.Nodes.Count + " nodes");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Build / Rebuild", GUILayout.Height(24f)))
                {
                    SabaPropsAutoRebuild.Cancel(vine);
                    SurfaceGrowthAuthoringBuilder.Build(vine);
                }
                if (GUILayout.Button("Clear", GUILayout.Height(24f)))
                {
                    SabaPropsAutoRebuild.Cancel(vine);
                    SurfaceGrowthAuthoringBuilder.Clear(vine);
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Botanical presets", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Creeping Fig"))
                {
                    ApplyPreset(vine, vine.morphology.ApplyCreepingFigPreset);
                }
                if (GUILayout.Button("English Ivy"))
                {
                    ApplyPreset(vine, vine.morphology.ApplyEnglishIvyPreset);
                }
                if (GUILayout.Button("Boston Ivy"))
                {
                    ApplyPreset(vine, vine.morphology.ApplyBostonIvyPreset);
                }
            }
        }

        private static void ApplyPreset(SurfaceVine vine, System.Action preset)
        {
            Undo.RecordObject(vine, "Apply Surface Vine Preset");
            preset();
            EditorUtility.SetDirty(vine);
            ScheduleAutoRebuild(vine);
        }

        private void OnSceneGUI()
        {
            var vine = (SurfaceVine)target;
            if (SurfaceGrowthSceneHandles.DrawGuideHandles(
                vine.transform,
                vine.guidePoints,
                vine,
                "Move Vine Guide Point"))
            {
                ScheduleAutoRebuild(vine);
            }
            SurfaceGrowthSceneHandles.DrawGraph(
                vine.transform,
                vine.generatedGraph,
                new Color(0.18f, 0.65f, 0.16f, 1f));
        }

        private void OnUndoRedo()
        {
            ScheduleAutoRebuild((SurfaceVine)target);
        }

        private static void ScheduleAutoRebuild(SurfaceVine vine)
        {
            MeshFilter filter = vine != null ? vine.GetComponent<MeshFilter>() : null;
            if (vine == null || !vine.autoRebuild || filter == null || filter.sharedMesh == null)
            {
                SabaPropsAutoRebuild.Cancel(vine);
                return;
            }

            SabaPropsAutoRebuild.Schedule(
                vine,
                () => SurfaceGrowthAuthoringBuilder.Build(vine, false));
        }

        private static void DrawBuildExclusionHelp()
        {
            EditorGUILayout.HelpBox(
                SabaPropsEditorLocalization.Text(
                    "生成済みの内容だけを値・ガイド・Undo/Redo の変更後に更新します。初回は Build / Rebuild を使用してください。Component は World ビルド時に自動除外されるため、手動で削除する必要はありません。",
                    "Updates existing generated content after value, guide, or Undo/Redo changes. Use Build / Rebuild for the first build. The component is automatically excluded from world builds and does not need to be removed manually."),
                MessageType.None);
        }
    }

    [CustomEditor(typeof(RhizomePatch))]
    public sealed class RhizomePatchEditor : UnityEditor.Editor
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
            var patch = (RhizomePatch)target;
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "autoRebuild");
            EditorGUILayout.PropertyField(
                _autoRebuild,
                new GUIContent(SabaPropsEditorLocalization.Text(
                    "値変更時に自動再生成",
                    "Auto Rebuild on Changes")));
            if (serializedObject.ApplyModifiedProperties())
            {
                ScheduleAutoRebuild(patch);
            }

            EditorGUILayout.HelpBox(
                SabaPropsEditorLocalization.Text(
                    "生成済みの内容だけを値・ガイド・Undo/Redo の変更後に更新します。初回は Build / Rebuild を使用してください。Component は World ビルド時に自動除外されるため、手動で削除する必要はありません。",
                    "Updates existing generated content after value, guide, or Undo/Redo changes. Use Build / Rebuild for the first build. The component is automatically excluded from world builds and does not need to be removed manually."),
                MessageType.None);

            EditorGUILayout.Space(8f);
            if (patch.generatedGraph != null)
            {
                EditorGUILayout.LabelField(
                    "Generated",
                    patch.generatedGraph.Nodes.Count + " nodes");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Build / Rebuild", GUILayout.Height(24f)))
                {
                    SabaPropsAutoRebuild.Cancel(patch);
                    SurfaceGrowthAuthoringBuilder.Build(patch);
                }
                if (GUILayout.Button("Clear", GUILayout.Height(24f)))
                {
                    SabaPropsAutoRebuild.Cancel(patch);
                    SurfaceGrowthAuthoringBuilder.Clear(patch);
                }
            }
        }

        private void OnSceneGUI()
        {
            var patch = (RhizomePatch)target;
            if (SurfaceGrowthSceneHandles.DrawGuideHandles(
                patch.transform,
                patch.guidePoints,
                patch,
                "Move Rhizome Seed Point"))
            {
                ScheduleAutoRebuild(patch);
            }
            SurfaceGrowthSceneHandles.DrawGraph(
                patch.transform,
                patch.generatedGraph,
                new Color(0.52f, 0.30f, 0.12f, 1f));
        }

        private void OnUndoRedo()
        {
            ScheduleAutoRebuild((RhizomePatch)target);
        }

        private static void ScheduleAutoRebuild(RhizomePatch patch)
        {
            MeshFilter filter = patch != null ? patch.GetComponent<MeshFilter>() : null;
            if (patch == null || !patch.autoRebuild || filter == null || filter.sharedMesh == null)
            {
                SabaPropsAutoRebuild.Cancel(patch);
                return;
            }

            SabaPropsAutoRebuild.Schedule(
                patch,
                () => SurfaceGrowthAuthoringBuilder.Build(patch, false));
        }
    }

    internal static class SurfaceGrowthSceneHandles
    {
        public static bool DrawGuideHandles(
            Transform transform,
            System.Collections.Generic.List<Vector3> guidePoints,
            Object owner,
            string undoName)
        {
            if (transform == null || guidePoints == null)
            {
                return false;
            }
            bool changed = false;
            using (new Handles.DrawingScope(transform.localToWorldMatrix))
            {
                Handles.color = new Color(0.95f, 0.72f, 0.12f, 1f);
                for (int i = 0; i < guidePoints.Count; i++)
                {
                    if (i > 0)
                    {
                        Handles.DrawLine(guidePoints[i - 1], guidePoints[i]);
                    }
                    Handles.Label(guidePoints[i], i.ToString());
                    EditorGUI.BeginChangeCheck();
                    Vector3 moved = Handles.PositionHandle(
                        guidePoints[i],
                        Quaternion.identity);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(owner, undoName);
                        guidePoints[i] = moved;
                        EditorUtility.SetDirty(owner);
                        changed = true;
                    }
                }
            }
            return changed;
        }

        public static void DrawGraph(
            Transform transform,
            SurfaceGrowthGraph graph,
            Color colour)
        {
            if (transform == null || graph == null)
            {
                return;
            }
            using (new Handles.DrawingScope(colour, transform.localToWorldMatrix))
            {
                for (int i = 0; i < graph.Nodes.Count; i++)
                {
                    SurfaceGrowthNode node = graph.Nodes[i];
                    if (node.parentIndex >= 0 && node.parentIndex < graph.Nodes.Count)
                    {
                        Handles.DrawLine(
                            graph.Nodes[node.parentIndex].position,
                            node.position);
                    }
                }
            }
        }
    }

    public static class SurfaceGrowthMenu
    {
        [MenuItem("GameObject/SabaProps/Placement/Surface Vine", false, 13)]
        public static void CreateSurfaceVine(MenuCommand command)
        {
            var gameObject = new GameObject("Surface Vine");
            Undo.RegisterCreatedObjectUndo(gameObject, "Create Surface Vine");
            GameObject parent = command.context as GameObject;
            if (parent != null)
            {
                // Keep world scale at one. A scaled wall is common, but an
                // inherited non-uniform scale would turn metre-based path and
                // spacing values into three different units.
                gameObject.transform.position = parent.transform.position;
                gameObject.transform.rotation = parent.transform.rotation;
            }

            SurfaceVine vine = gameObject.AddComponent<SurfaceVine>();
            vine.targetSurface = parent != null ? parent.GetComponent<Collider>() : null;
            vine.material = FoliageAssetLibrary.CreateOrLoadDefaultMaterial();
            Selection.activeGameObject = gameObject;
        }

        [MenuItem("GameObject/SabaProps/Placement/Rhizome Patch", false, 14)]
        public static void CreateRhizomePatch(MenuCommand command)
        {
            var gameObject = new GameObject("Rhizome Patch");
            Undo.RegisterCreatedObjectUndo(gameObject, "Create Rhizome Patch");
            GameObject parent = command.context as GameObject;
            if (parent != null)
            {
                gameObject.transform.position = parent.transform.position;
                gameObject.transform.rotation = parent.transform.rotation;
            }

            RhizomePatch patch = gameObject.AddComponent<RhizomePatch>();
            patch.targetSurface = parent != null ? parent.GetComponent<Collider>() : null;
            patch.material = FoliageAssetLibrary.CreateOrLoadDefaultMaterial();
            Selection.activeGameObject = gameObject;
        }
    }
}
