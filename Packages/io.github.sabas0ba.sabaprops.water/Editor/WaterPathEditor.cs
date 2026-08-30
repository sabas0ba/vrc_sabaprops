using UnityEditor;
using UnityEngine;

namespace SabaProps.Water.Editors
{
    [CustomEditor(typeof(WaterPath))]
    public sealed class WaterPathEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            var path = (WaterPath)target;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Point"))
                {
                    Undo.RecordObject(path, "Add Water Path Point");
                    Vector3 last = path.controlPoints[path.controlPoints.Count - 1];
                    Vector3 previous = path.controlPoints[path.controlPoints.Count - 2];
                    Vector3 direction = last - previous;
                    if (direction.sqrMagnitude < 1e-6f)
                    {
                        direction = Vector3.forward * 5f;
                    }

                    path.controlPoints.Add(last + direction);
                    EditorUtility.SetDirty(path);
                    Rebuild(path);
                }

                using (new EditorGUI.DisabledScope(path.controlPoints.Count <= 2))
                {
                    if (GUILayout.Button("Remove Last"))
                    {
                        Undo.RecordObject(path, "Remove Water Path Point");
                        path.controlPoints.RemoveAt(path.controlPoints.Count - 1);
                        EditorUtility.SetDirty(path);
                        Rebuild(path);
                    }
                }
            }

            if (GUILayout.Button("Rebuild River Mesh"))
            {
                Rebuild(path);
            }

            EditorGUILayout.HelpBox(
                "Scene Viewのhandleでpathを編集後、Meshは自動更新されます。生成後のMeshFilterとMaterialは通常のUnity componentなのでruntime scriptを必要としません。",
                MessageType.Info);
        }

        private void OnSceneGUI()
        {
            var path = (WaterPath)target;
            if (path.controlPoints == null)
            {
                return;
            }

            bool changed = false;
            for (int index = 0; index < path.controlPoints.Count; index++)
            {
                Vector3 world = path.transform.TransformPoint(path.controlPoints[index]);
                Handles.Label(world + Vector3.up * 0.25f, index.ToString());

                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(world, Quaternion.identity);
                if (!EditorGUI.EndChangeCheck())
                {
                    continue;
                }

                if (!changed)
                {
                    Undo.RecordObject(path, "Move Water Path Point");
                }

                path.controlPoints[index] = path.transform.InverseTransformPoint(moved);
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(path);
                Rebuild(path);
            }
        }

        public static Mesh Rebuild(WaterPath path)
        {
            if (path == null)
            {
                return null;
            }

            path.Normalize();
            Mesh generated = WaterMeshBuilder.BuildRiver(
                path.controlPoints,
                path.width,
                path.subdivisions,
                path.uvMetersPerTile);
            if (generated == null)
            {
                return null;
            }

            Undo.RecordObject(path, "Rebuild Water Path");
            Mesh mesh = WaterAssetLibrary.ReplaceOrWriteMesh(
                generated,
                path.generatedMesh,
                WaterAssetLibrary.GeneratedSurfacesFolder,
                path.name + "_River");

            path.generatedMesh = mesh;
            path.GetComponent<MeshFilter>().sharedMesh = mesh;
            path.ApplyProfile();
            EditorUtility.SetDirty(path);
            EditorUtility.SetDirty(path.GetComponent<MeshFilter>());
            AssetDatabase.SaveAssets();
            SceneView.RepaintAll();
            return mesh;
        }
    }
}
