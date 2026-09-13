using UnityEditor;
using UnityEngine;

namespace SabaProps.Water.Editors
{
    /// <summary>Scene View brush that bakes conforming puddle meshes.</summary>
    public sealed class PuddlePainterWindow : EditorWindow
    {
        private Material material;
        private float radius = 1.25f;
        private float aspect = 1.2f;
        private float aspectJitter = 0.25f;
        private float rotationJitter = 180f;
        private float irregularity = 0.18f;
        private float surfaceOffset = 0.008f;
        private float projectionDistance = 1f;
        private int rings = 4;
        private int radialSegments = 24;
        private int layerMask = ~0;
        private int seed = 1;
        private bool painting;

        [MenuItem("Tools/SabaProps/Water/Puddle Stamp Tool", false, 20)]
        public static void Open()
        {
            var window = GetWindow<PuddlePainterWindow>(false, "Puddle Stamp", true);
            window.minSize = new Vector2(340f, 430f);
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += DuringSceneGui;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DuringSceneGui;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Surface", EditorStyles.boldLabel);
            material = (Material)EditorGUILayout.ObjectField("Material", material, typeof(Material), false);
            radius = EditorGUILayout.Slider("Radius", radius, 0.1f, 20f);
            aspect = EditorGUILayout.Slider("Aspect", aspect, 0.25f, 4f);
            aspectJitter = EditorGUILayout.Slider("Aspect Jitter", aspectJitter, 0f, 0.8f);
            rotationJitter = EditorGUILayout.Slider("Rotation Jitter", rotationJitter, 0f, 180f);
            irregularity = EditorGUILayout.Slider("Boundary Irregularity", irregularity, 0f, 0.45f);
            surfaceOffset = EditorGUILayout.Slider("Surface Offset", surfaceOffset, 0f, 0.05f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Conformance", EditorStyles.boldLabel);
            projectionDistance = EditorGUILayout.Slider("Projection Distance", projectionDistance, 0.05f, 5f);
            rings = EditorGUILayout.IntSlider("Radial Rings", rings, 1, 12);
            radialSegments = EditorGUILayout.IntSlider("Radial Segments", radialSegments, 8, 64);
            layerMask = EditorGUILayout.IntField("Physics Layer Mask", layerMask);
            seed = EditorGUILayout.IntField("Next Seed", seed);

            EditorGUILayout.Space();
            if (material == null && GUILayout.Button("Use Default Puddle Lite Material"))
            {
                material = WaterAssetLibrary.CreateOrLoadProfile(
                    WaterBodyKind.Puddle, WaterQuality.Lite)?.material;
                AssetDatabase.SaveAssets();
            }

            bool nextPainting = GUILayout.Toggle(
                painting,
                painting ? "Painting: click Scene View to place" : "Start Painting",
                "Button",
                GUILayout.Height(32f));
            if (nextPainting != painting)
            {
                painting = nextPainting;
                SceneView.RepaintAll();
            }

            EditorGUILayout.HelpBox(
                "Colliderへraycastして各頂点を地面へ投影します。起伏が大きい場所ではProjection Distanceを増やしてください。生成物はAssets/SabaProps/Water/Generated/Puddlesに保存されます。",
                MessageType.Info);
        }

        private void DuringSceneGui(SceneView sceneView)
        {
            if (!painting)
            {
                return;
            }

            Event current = Event.current;
            if (current == null || current.alt)
            {
                return;
            }

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    10000f,
                    layerMask,
                    QueryTriggerInteraction.Ignore))
            {
                return;
            }

            Handles.color = new Color(0.1f, 0.75f, 1f, 0.9f);
            Handles.DrawWireDisc(
                hit.point + hit.normal * surfaceOffset,
                hit.normal,
                radius);
            sceneView.Repaint();

            if (current.type != EventType.MouseDown || current.button != 0)
            {
                return;
            }

            CreatePuddle(hit);
            current.Use();
        }

        private void CreatePuddle(RaycastHit centreHit)
        {
            if (material == null)
            {
                material = WaterAssetLibrary.CreateOrLoadProfile(
                    WaterBodyKind.Puddle, WaterQuality.Lite)?.material;
            }

            float randomAspect = aspect * Mathf.Lerp(
                1f - aspectJitter,
                1f + aspectJitter,
                Hash01(seed, 17));
            float yaw = Mathf.Lerp(-rotationJitter, rotationJitter, Hash01(seed, 31));

            Mesh mesh = WaterMeshBuilder.BuildPuddle(
                radius,
                randomAspect,
                rings,
                radialSegments,
                seed,
                irregularity);

            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, centreHit.normal)
                * Quaternion.AngleAxis(yaw, Vector3.up);
            Vector3 position = centreHit.point + centreHit.normal * surfaceOffset;
            ConformMesh(mesh, position, rotation, centreHit.normal);

            mesh = WaterAssetLibrary.WriteUniqueMesh(
                mesh,
                WaterAssetLibrary.GeneratedPuddlesFolder,
                "Puddle_" + seed);

            var puddle = new GameObject("Puddle " + seed, typeof(MeshFilter), typeof(MeshRenderer));
            puddle.transform.SetPositionAndRotation(position, rotation);
            puddle.GetComponent<MeshFilter>().sharedMesh = mesh;
            puddle.GetComponent<MeshRenderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(puddle, StaticEditorFlags.BatchingStatic);
            Undo.RegisterCreatedObjectUndo(puddle, "Place Puddle");
            Selection.activeGameObject = puddle;

            seed++;
            AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(this);
        }

        private void ConformMesh(
            Mesh mesh,
            Vector3 position,
            Quaternion rotation,
            Vector3 projectionNormal)
        {
            Vector3[] vertices = mesh.vertices;
            Quaternion inverseRotation = Quaternion.Inverse(rotation);

            for (int index = 0; index < vertices.Length; index++)
            {
                Vector3 world = position + rotation * vertices[index];
                Vector3 origin = world + projectionNormal * projectionDistance;
                if (!Physics.Raycast(
                        origin,
                        -projectionNormal,
                        out RaycastHit hit,
                        projectionDistance * 2f,
                        layerMask,
                        QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                Vector3 projected = hit.point + hit.normal * surfaceOffset;
                vertices[index] = inverseRotation * (projected - position);
            }

            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
        }

        private static float Hash01(int value, int salt)
        {
            unchecked
            {
                uint hash = (uint)(value * 73856093) ^ (uint)(salt * 19349663);
                hash ^= hash >> 13;
                hash *= 1274126177u;
                hash ^= hash >> 16;
                return hash / (float)uint.MaxValue;
            }
        }
    }
}
