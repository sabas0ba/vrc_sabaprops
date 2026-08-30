using UnityEditor;
using UnityEngine;

namespace SabaProps.Foliage.Editors
{
    [CustomEditor(typeof(SurfaceVine))]
    public sealed class SurfaceVineEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var vine = (SurfaceVine)target;

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
                    SurfaceGrowthAuthoringBuilder.Build(vine);
                }
                if (GUILayout.Button("Clear", GUILayout.Height(24f)))
                {
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
        }

        private void OnSceneGUI()
        {
            var vine = (SurfaceVine)target;
            SurfaceGrowthSceneHandles.DrawGuideHandles(
                vine.transform,
                vine.guidePoints,
                vine,
                "Move Vine Guide Point");
            SurfaceGrowthSceneHandles.DrawGraph(
                vine.transform,
                vine.generatedGraph,
                new Color(0.18f, 0.65f, 0.16f, 1f));
        }
    }

    [CustomEditor(typeof(RhizomePatch))]
    public sealed class RhizomePatchEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var patch = (RhizomePatch)target;

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
                    SurfaceGrowthAuthoringBuilder.Build(patch);
                }
                if (GUILayout.Button("Clear", GUILayout.Height(24f)))
                {
                    SurfaceGrowthAuthoringBuilder.Clear(patch);
                }
            }
        }

        private void OnSceneGUI()
        {
            var patch = (RhizomePatch)target;
            SurfaceGrowthSceneHandles.DrawGuideHandles(
                patch.transform,
                patch.guidePoints,
                patch,
                "Move Rhizome Seed Point");
            SurfaceGrowthSceneHandles.DrawGraph(
                patch.transform,
                patch.generatedGraph,
                new Color(0.52f, 0.30f, 0.12f, 1f));
        }
    }

    internal static class SurfaceGrowthSceneHandles
    {
        public static void DrawGuideHandles(
            Transform transform,
            System.Collections.Generic.List<Vector3> guidePoints,
            Object owner,
            string undoName)
        {
            if (transform == null || guidePoints == null)
            {
                return;
            }
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
                    }
                }
            }
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
