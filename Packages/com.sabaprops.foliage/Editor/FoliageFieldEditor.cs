using UnityEditor;
using UnityEngine;

namespace SabaProps.Foliage.Editors
{
    [CustomEditor(typeof(FoliageField))]
    [CanEditMultipleObjects]
    public class FoliageFieldEditor : UnityEditor.Editor
    {
        private SerializedProperty _shape;
        private SerializedProperty _size;
        private SerializedProperty _radius;

        private SerializedProperty _density;
        private SerializedProperty _seed;
        private SerializedProperty _maxInstances;

        private SerializedProperty _groundLayers;
        private SerializedProperty _raycastHeight;
        private SerializedProperty _raycastDistance;
        private SerializedProperty _requireGroundHit;
        private SerializedProperty _altitudeLimits;
        private SerializedProperty _groundOffset;

        private SerializedProperty _exclusionLayers;
        private SerializedProperty _exclusionRadius;

        private SerializedProperty _densityMask;
        private SerializedProperty _densityMaskThreshold;
        private SerializedProperty _invertDensityMask;

        private SerializedProperty _species;
        private SerializedProperty _speciesWeights;

        private SerializedProperty _outputMode;
        private SerializedProperty _chunkSize;

        private bool _showGround = true;
        private bool _showMask;
        private bool _showExclusion;

        private void OnEnable()
        {
            _shape = serializedObject.FindProperty("shape");
            _size = serializedObject.FindProperty("size");
            _radius = serializedObject.FindProperty("radius");

            _density = serializedObject.FindProperty("density");
            _seed = serializedObject.FindProperty("seed");
            _maxInstances = serializedObject.FindProperty("maxInstances");

            _groundLayers = serializedObject.FindProperty("groundLayers");
            _raycastHeight = serializedObject.FindProperty("raycastHeight");
            _raycastDistance = serializedObject.FindProperty("raycastDistance");
            _requireGroundHit = serializedObject.FindProperty("requireGroundHit");
            _altitudeLimits = serializedObject.FindProperty("altitudeLimits");
            _groundOffset = serializedObject.FindProperty("groundOffset");

            _exclusionLayers = serializedObject.FindProperty("exclusionLayers");
            _exclusionRadius = serializedObject.FindProperty("exclusionRadius");

            _densityMask = serializedObject.FindProperty("densityMask");
            _densityMaskThreshold = serializedObject.FindProperty("densityMaskThreshold");
            _invertDensityMask = serializedObject.FindProperty("invertDensityMask");

            _species = serializedObject.FindProperty("species");
            _speciesWeights = serializedObject.FindProperty("speciesWeights");

            _outputMode = serializedObject.FindProperty("outputMode");
            _chunkSize = serializedObject.FindProperty("chunkSize");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var field = (FoliageField)target;

            DrawArea();
            DrawDensity(field);
            DrawGround();
            DrawExclusion();
            DrawMask();
            DrawSpecies();
            DrawOutput(field);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8f);
            DrawActions();
            DrawStats(field);
        }

        private void DrawArea()
        {
            EditorGUILayout.LabelField("Area", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_shape);

            if ((FoliageAreaShape)_shape.enumValueIndex == FoliageAreaShape.Circle)
            {
                EditorGUILayout.PropertyField(_radius);
            }
            else
            {
                EditorGUILayout.PropertyField(_size);
            }
        }

        private void DrawDensity(FoliageField field)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Density", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_density);
            EditorGUILayout.PropertyField(_seed);
            EditorGUILayout.PropertyField(_maxInstances);

            if (!serializedObject.isEditingMultipleObjects)
            {
                int estimate = Mathf.RoundToInt(field.AreaSquareMeters * field.density);
                string clampNote = estimate > field.maxInstances
                    ? $"  →  Max Instances により {field.maxInstances:N0} で打ち切られます"
                    : string.Empty;

                EditorGUILayout.LabelField(
                    " ",
                    $"面積 {field.AreaSquareMeters:N0} m² / 目標 約 {estimate:N0} 個体{clampNote}",
                    EditorStyles.miniLabel);
            }
        }

        private void DrawGround()
        {
            EditorGUILayout.Space(6f);
            _showGround = EditorGUILayout.Foldout(_showGround, "Ground", true, EditorStyles.foldoutHeader);
            if (!_showGround)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(_groundLayers);
                EditorGUILayout.PropertyField(_requireGroundHit);
                EditorGUILayout.PropertyField(_raycastHeight);
                EditorGUILayout.PropertyField(_raycastDistance);
                EditorGUILayout.PropertyField(_altitudeLimits);
                EditorGUILayout.PropertyField(_groundOffset);

                EditorGUILayout.HelpBox(
                    "地面には Collider が必要です。Terrain は TerrainCollider、メッシュ地形は MeshCollider を付けてください。",
                    MessageType.None);
            }
        }

        private void DrawExclusion()
        {
            EditorGUILayout.Space(6f);
            _showExclusion = EditorGUILayout.Foldout(_showExclusion, "Exclusion", true, EditorStyles.foldoutHeader);
            if (!_showExclusion)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(_exclusionLayers);
                EditorGUILayout.PropertyField(_exclusionRadius);
            }
        }

        private void DrawMask()
        {
            EditorGUILayout.Space(6f);
            _showMask = EditorGUILayout.Foldout(_showMask, "Density Mask", true, EditorStyles.foldoutHeader);
            if (!_showMask)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(_densityMask);
                EditorGUILayout.PropertyField(_densityMaskThreshold);
                EditorGUILayout.PropertyField(_invertDensityMask);

                var mask = _densityMask.objectReferenceValue as Texture2D;
                if (mask != null && !mask.isReadable)
                {
                    EditorGUILayout.HelpBox(
                        "このテクスチャは Read/Write Enabled が OFF です。インポート設定で有効にしてください。",
                        MessageType.Warning);
                }
            }
        }

        private void DrawSpecies()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Species", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_species, true);

            if (_species.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "Species が空です。Tools/SabaProps/Foliage/Create Default Assets でプリセットを作成できます。",
                    MessageType.Info);
                return;
            }

            DrawSpeciesWeights();
        }

        /// <summary>
        /// Mix for this field, shown next to the species it belongs to. The
        /// weights are a parallel list in the serialised data, which on its own
        /// would be impossible to line up by eye.
        /// </summary>
        private void DrawSpeciesWeights()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Mix", EditorStyles.miniBoldLabel);

            // Grow the parallel list to match rather than asking the user to.
            while (_speciesWeights.arraySize < _species.arraySize)
            {
                _speciesWeights.InsertArrayElementAtIndex(_speciesWeights.arraySize);
                _speciesWeights.GetArrayElementAtIndex(_speciesWeights.arraySize - 1).floatValue = 0f;
            }

            var field = (FoliageField)target;
            float total = 0f;
            for (int i = 0; i < _species.arraySize; i++)
            {
                total += field.PlacementWeightAt(i);
            }

            for (int i = 0; i < _species.arraySize; i++)
            {
                var species = _species.GetArrayElementAtIndex(i).objectReferenceValue as FoliageSpecies;
                SerializedProperty weight = _speciesWeights.GetArrayElementAtIndex(i);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        species != null ? species.name : "(none)", GUILayout.Width(120f));

                    weight.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField(weight.floatValue));

                    string share = total > 0f
                        ? $"{field.PlacementWeightAt(i) / total * 100f:0.#} %"
                        : "-";

                    EditorGUILayout.LabelField(share, GUILayout.Width(52f));
                }
            }

            EditorGUILayout.LabelField(
                "0 にすると Species アセット側の Placement Weight を使います。",
                EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawOutput(FoliageField field)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_outputMode);
            EditorGUILayout.PropertyField(_chunkSize);

            var mode = (FoliageOutputMode)_outputMode.enumValueIndex;
            EditorGUILayout.HelpBox(
                mode == FoliageOutputMode.GpuInstanced
                    ? "GPU Instanced: 1 個体 = 1 Renderer。同一メッシュ／マテリアルなので Unity が自動でインスタンシング結合します。"
                      + "個体ごとにカリングと距離縮退が効く反面、Transform 数が増えるので数千個体までが目安です。"
                    : "Merged Chunks: チャンク単位でメッシュを結合します。ドローコールと CPU コストは最小ですが、"
                      + "カリングはチャンク単位になります。数万個体規模ではこちらが有利です。",
                MessageType.None);
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate", GUILayout.Height(28f)))
                {
                    foreach (Object each in targets)
                    {
                        FoliageFieldBuilder.Build((FoliageField)each);
                    }
                }

                if (GUILayout.Button("Clear", GUILayout.Height(28f), GUILayout.Width(90f)))
                {
                    foreach (Object each in targets)
                    {
                        FoliageFieldBuilder.Clear((FoliageField)each);
                    }
                }
            }
        }

        private void DrawStats(FoliageField field)
        {
            if (serializedObject.isEditingMultipleObjects)
            {
                return;
            }

            // Unity resurrects [Serializable] class fields as zeroed instances
            // after a domain reload, so a null check alone is not enough to tell
            // "never built" from "built".
            FoliageBuildStats stats = field.lastBuildStats;
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
                Row("Chunks", $"{stats.chunkCount:N0}");
                Row("Triangles", $"{stats.triangleCount:N0}");
                Row("Vertices", $"{stats.vertexCount:N0}");
                Row("Draw calls (概算)", $"{stats.EstimatedDrawCalls:N0}");
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

        private void OnSceneGUI()
        {
            var field = (FoliageField)target;

            using (new Handles.DrawingScope(new Color(0.4f, 0.9f, 0.35f, 1f), field.transform.localToWorldMatrix))
            {
                EditorGUI.BeginChangeCheck();

                if (field.shape == FoliageAreaShape.Circle)
                {
                    float radius = Handles.RadiusHandle(Quaternion.Euler(90f, 0f, 0f), Vector3.zero, field.radius);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(field, "Resize Foliage Field");
                        field.radius = Mathf.Max(0.1f, radius);
                    }
                }
                else
                {
                    var size = new Vector3(Mathf.Abs(field.size.x), 0f, Mathf.Abs(field.size.y));
                    Handles.DrawWireCube(Vector3.zero, size);

                    float halfX = Handles.ScaleValueHandle(
                        size.x * 0.5f, new Vector3(size.x * 0.5f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f),
                        HandleUtility.GetHandleSize(Vector3.zero) * 1.2f, Handles.ConeHandleCap, 0.1f);

                    float halfZ = Handles.ScaleValueHandle(
                        size.z * 0.5f, new Vector3(0f, 0f, size.z * 0.5f), Quaternion.identity,
                        HandleUtility.GetHandleSize(Vector3.zero) * 1.2f, Handles.ConeHandleCap, 0.1f);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(field, "Resize Foliage Field");
                        field.size = new Vector2(
                            Mathf.Max(0.1f, halfX * 2f),
                            Mathf.Max(0.1f, halfZ * 2f));
                    }
                }
            }
        }
    }
}
