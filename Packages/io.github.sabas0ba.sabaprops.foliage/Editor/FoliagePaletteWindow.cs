using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Foliage.Editors
{
    /// <summary>Where parameter edits made in the foliage palette are stored.</summary>
    public enum FoliagePaletteEditMode
    {
        /// <summary>
        /// Edit an in-memory copy and write a new Species asset when a field is
        /// placed. Existing fields are never changed by experimentation here.
        /// </summary>
        WorkingCopy = 0,

        /// <summary>Edit the selected Species assets themselves.</summary>
        DirectAsset = 1,
    }

    /// <summary>Range calculations shared by the palette UI and its tests.</summary>
    public static class FoliageStampUtility
    {
        public static Vector2 SanitizeSize(Vector2 value)
        {
            return new Vector2(
                Mathf.Max(0.1f, Mathf.Abs(value.x)),
                Mathf.Max(0.1f, Mathf.Abs(value.y)));
        }

        public static float SanitizeRadius(float value)
        {
            return Mathf.Max(0.1f, Mathf.Abs(value));
        }

        public static float Area(FoliageAreaShape shape, Vector2 size, float radius)
        {
            if (shape == FoliageAreaShape.Circle)
            {
                float safeRadius = SanitizeRadius(radius);
                return Mathf.PI * safeRadius * safeRadius;
            }

            Vector2 safeSize = SanitizeSize(size);
            return safeSize.x * safeSize.y;
        }

        public static int EstimateInstanceCount(
            FoliageAreaShape shape,
            Vector2 size,
            float radius,
            float density)
        {
            return Mathf.Max(0, Mathf.RoundToInt(
                Area(shape, size, radius) * Mathf.Max(0f, density)));
        }
    }

    /// <summary>
    /// Dockable workspace for composing, shaping, previewing and placing a
    /// foliage field without moving between the field and Species inspectors.
    /// </summary>
    public class FoliagePaletteWindow : EditorWindow
    {
        private const string PaletteSpeciesFolder =
            FoliageAssetLibrary.SpeciesFolder + "/Palette";

        [Serializable]
        private class Entry
        {
            public bool enabled;
            public float weight = 1f;
            public FoliageSpecies source;
            public FoliageSpecies workingCopy;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        [SerializeField] private int selectedIndex;
        [SerializeField] private FoliagePaletteEditMode editMode = FoliagePaletteEditMode.WorkingCopy;

        [SerializeField] private FoliageAreaShape shape = FoliageAreaShape.Rectangle;
        [SerializeField] private Vector2 size = new Vector2(8f, 8f);
        [SerializeField] private float radius = 4f;
        [SerializeField] private float density = 8f;
        [SerializeField] private int seed = 12345;
        [SerializeField] private FoliageOutputMode outputMode = FoliageOutputMode.GpuInstanced;
        [SerializeField] private float chunkSize = 12f;
        [SerializeField] private bool buildImmediately = true;
        [SerializeField] private GameObject parent;
        [SerializeField] private bool placing;
        [SerializeField] private bool editStampSizeInScene = true;
        [SerializeField] private bool stampPreviewPinned;

        private Vector2 scroll;
        private readonly List<FoliageSpecies> retiredWorkingCopies = new List<FoliageSpecies>();
        private PreviewRenderUtility preview;
        private Mesh previewMesh;
        private FoliageSpecies previewSpecies;
        private Vector3 placementPoint;
        private bool hasPlacementPoint;

        [MenuItem("Window/SabaProps/Foliage Palette", false, 2100)]
        public static void Open()
        {
            var window = GetWindow<FoliagePaletteWindow>(
                false,
                SabaPropsEditorLocalization.Text("植生パレット", "Foliage Palette"),
                true);
            window.minSize = new Vector2(390f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;

            EnsurePreview();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            Undo.undoRedoPerformed -= OnUndoRedo;
            CleanupPreview();
        }

        private void OnDestroy()
        {
            foreach (Entry entry in entries)
            {
                DestroyWorkingCopy(entry);
            }

            foreach (FoliageSpecies copy in retiredWorkingCopies)
            {
                if (copy != null)
                {
                    DestroyImmediate(copy);
                }
            }

            retiredWorkingCopies.Clear();
        }

        private void OnUndoRedo()
        {
            FoliageSpecies species = CurrentSpecies();
            RefreshDirectAssetMesh(species);
            RebuildPreview(species);
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnGUI()
        {
            EnsureEntries();

            scroll = EditorGUILayout.BeginScrollView(scroll);

            SabaPropsEditorLocalization.DrawLanguageSelector();
            EditorGUILayout.Space(8f);
            DrawMode();
            EditorGUILayout.Space(8f);
            DrawComposition();
            EditorGUILayout.Space(8f);
            DrawParameters();
            EditorGUILayout.Space(8f);
            DrawPreview();
            EditorGUILayout.Space(8f);
            DrawStampSettings();
            EditorGUILayout.Space(8f);
            DrawFieldSettings();

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6f);
            DrawPlacementActions();
        }

        private void DrawMode()
        {
            EditorGUILayout.LabelField(L("編集", "Editing"), EditorStyles.boldLabel);

            var next = (FoliagePaletteEditMode)SabaPropsEditorLocalization.Popup(
                "モード",
                "Mode",
                (int)editMode,
                new[] { "作業用コピー", "アセットを直接編集" },
                new[] { "Working Copy", "Direct Asset" });
            if (next != editMode)
            {
                Undo.RecordObject(this, "Change Foliage Palette Mode");
                editMode = next;
                EnsureWorkingCopies();
                RebuildPreview(CurrentSpecies());
            }

            EditorGUILayout.HelpBox(
                editMode == FoliagePaletteEditMode.WorkingCopy
                    ? L(
                        "作業用コピーだけを変更します。配置時に新しい Species アセットとして保存され、既存フィールドには影響しません。",
                        "Only the working copy is changed. Placement saves a new Species asset and does not affect existing fields.")
                    : L(
                        "既存 Species アセットを直接変更します。同じアセットを参照する全フィールドへ反映されます。",
                        "Edits the existing Species asset. Changes affect every field that references the same asset."),
                editMode == FoliagePaletteEditMode.WorkingCopy ? MessageType.Info : MessageType.Warning);
        }

        private void DrawComposition()
        {
            EditorGUILayout.LabelField(L("配合", "Composition"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                L(
                    "有効な種の Weight がフィールド内の出現比率になります。種名を押すと形状パラメータを編集できます。",
                    "Enabled species weights determine their share in the field. Select a species name to edit its shape."),
                EditorStyles.wordWrappedMiniLabel);

            float total = TotalWeight();

            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];

                using (new EditorGUILayout.HorizontalScope())
                {
                    bool enabled = EditorGUILayout.Toggle(entry.enabled, GUILayout.Width(18f));
                    string label = entry.source != null ? entry.source.name : "(Species)";

                    if (GUILayout.Button(label, GUILayout.Width(112f)))
                    {
                        Undo.RecordObject(this, "Select Foliage Species");
                        selectedIndex = i;
                        RebuildPreview(CurrentSpecies());
                    }

                    var source = EditorGUILayout.ObjectField(
                        entry.source, typeof(FoliageSpecies), false) as FoliageSpecies;

                    float weight = EditorGUILayout.Slider(entry.weight, 0f, 2f, GUILayout.MinWidth(90f));
                    string share = entry.enabled && total > 0f
                        ? $"{Mathf.Max(0f, entry.weight) / total * 100f:0.#}%"
                        : "-";
                    EditorGUILayout.LabelField(share, GUILayout.Width(42f));

                    if (GUILayout.Button("-", GUILayout.Width(22f)))
                    {
                        RemoveEntry(i);
                        return;
                    }

                    if (enabled != entry.enabled || source != entry.source || !Mathf.Approximately(weight, entry.weight))
                    {
                        Undo.RecordObject(this, "Edit Foliage Composition");
                        entry.enabled = enabled;
                        entry.weight = Mathf.Max(0f, weight);

                        if (source != entry.source)
                        {
                            RetireWorkingCopy(entry);
                            entry.source = source;
                            entry.workingCopy = CreateWorkingCopy(source);

                            if (selectedIndex == i)
                            {
                                RebuildPreview(CurrentSpecies());
                            }
                        }
                    }
                }
            }

            if (GUILayout.Button(L("Species を追加", "Add Species")))
            {
                Undo.RecordObject(this, "Add Foliage Species");
                entries.Add(new Entry());
                selectedIndex = entries.Count - 1;
            }

            if (EnabledSpeciesCount() == 0)
            {
                EditorGUILayout.HelpBox(
                    L(
                        "配置する Species を 1 つ以上有効にしてください。",
                        "Enable at least one Species for placement."),
                    MessageType.Warning);
            }
        }

        private void DrawParameters()
        {
            EditorGUILayout.LabelField(L("形状パラメータ", "Parameters"), EditorStyles.boldLabel);

            FoliageSpecies species = CurrentSpecies();
            if (species == null)
            {
                EditorGUILayout.HelpBox(
                    L(
                        "配合から Species を選択してください。",
                        "Select a Species from Composition."),
                    MessageType.Info);
                return;
            }

            var serialized = new SerializedObject(species);
            serialized.Update();

            EditorGUILayout.PropertyField(serialized.FindProperty("meshSeed"));
            EditorGUILayout.PropertyField(serialized.FindProperty("season"));

            SerializedProperty parameters = serialized.FindProperty(
                FoliageAssetLibrary.ParameterProperty(species.kind));

            if (parameters != null)
            {
                EditorGUILayout.PropertyField(parameters, true);
            }

            if (serialized.ApplyModifiedProperties())
            {
                RefreshDirectAssetMesh(species);
                RebuildPreview(species);
                SceneView.RepaintAll();
            }
        }

        private void DrawPreview()
        {
            EditorGUILayout.LabelField(L("プレビュー", "Preview"), EditorStyles.boldLabel);

            Rect rect = GUILayoutUtility.GetRect(10f, 230f, GUILayout.ExpandWidth(true));
            FoliageSpecies species = CurrentSpecies();
            EnsurePreview();

            if (species == null || species.material == null)
            {
                EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));
                GUI.Label(
                    rect,
                    L(
                        "Material を持つ Species を選択してください。",
                        "Select a Species with a Material."),
                    EditorStyles.whiteBoldLabel);
                return;
            }

            if (previewMesh == null || previewSpecies != species)
            {
                RebuildPreview(species);
            }

            if (previewMesh == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            Bounds bounds = previewMesh.bounds;
            float extent = Mathf.Max(0.1f, Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z)));
            Vector3 target = bounds.center;

            preview.BeginPreview(rect, GUIStyle.none);
            preview.camera.transform.position = target + new Vector3(1.5f, 1.0f, -2.2f).normalized * extent * 3.2f;
            preview.camera.transform.rotation = Quaternion.LookRotation(target - preview.camera.transform.position);
            preview.camera.nearClipPlane = 0.01f;
            preview.camera.farClipPlane = extent * 12f;
            preview.camera.fieldOfView = 30f;

            preview.DrawMesh(previewMesh, Matrix4x4.identity, species.material, 0);
            preview.Render();
            preview.EndAndDrawPreview(rect);
        }

        private void DrawStampSettings()
        {
            EditorGUILayout.LabelField(L("スタンプ範囲", "Stamp Range"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                L(
                    "形状と範囲は配置中でも変更できます。Space でプレビュー位置を固定すると Scene View のハンドルで調整できます。",
                    "Shape and range remain editable while placing. Press Space to pin the preview and resize it with Scene View handles."),
                EditorStyles.wordWrappedMiniLabel);

            FoliageAreaShape nextShape = (FoliageAreaShape)SabaPropsEditorLocalization.Popup(
                "形状",
                "Shape",
                (int)shape,
                new[] { "矩形", "円形" },
                new[] { "Rectangle", "Circle" });
            Vector2 nextSize = size;
            float nextRadius = radius;

            if (nextShape == FoliageAreaShape.Circle)
            {
                nextRadius = FoliageStampUtility.SanitizeRadius(
                    EditorGUILayout.FloatField(L("半径 (m)", "Radius (m)"), radius));
            }
            else
            {
                nextSize = FoliageStampUtility.SanitizeSize(
                    EditorGUILayout.Vector2Field(L("寸法 X/Z (m)", "Size X/Z (m)"), size));
            }

            EditorGUILayout.LabelField(
                nextShape == FoliageAreaShape.Circle
                    ? L("直径プリセット", "Diameter Presets")
                    : L("正方形プリセット", "Square Presets"),
                EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                float[] extents = { 2f, 5f, 10f, 20f };
                for (int i = 0; i < extents.Length; i++)
                {
                    float extent = extents[i];
                    if (GUILayout.Button($"{extent:0} m"))
                    {
                        if (nextShape == FoliageAreaShape.Circle)
                        {
                            nextRadius = extent * 0.5f;
                        }
                        else
                        {
                            nextSize = new Vector2(extent, extent);
                        }
                    }
                }
            }

            bool nextEditInScene = EditorGUILayout.Toggle(
                L("Scene View で範囲を編集", "Edit Range in Scene View"),
                editStampSizeInScene);

            if (nextShape != shape || nextSize != size
                || !Mathf.Approximately(nextRadius, radius)
                || nextEditInScene != editStampSizeInScene)
            {
                Undo.RecordObject(this, "Edit Foliage Stamp Range");
                shape = nextShape;
                size = nextSize;
                radius = nextRadius;
                editStampSizeInScene = nextEditInScene;
                SceneView.RepaintAll();
            }

            int estimate = FoliageStampUtility.EstimateInstanceCount(
                shape, size, radius, density);
            EditorGUILayout.LabelField(
                L($"概算 {estimate:N0} 個体", $"Estimated {estimate:N0} instances"),
                EditorStyles.miniLabel);
        }

        private void DrawFieldSettings()
        {
            EditorGUILayout.LabelField(L("フィールド設定", "Field Settings"), EditorStyles.boldLabel);

            float nextDensity = Mathf.Max(
                0.001f,
                EditorGUILayout.FloatField(L("密度 (/m²)", "Density (/m²)"), density));
            int nextSeed = EditorGUILayout.IntField(L("シード", "Seed"), seed);
            FoliageOutputMode nextOutput = (FoliageOutputMode)SabaPropsEditorLocalization.Popup(
                "出力",
                "Output",
                (int)outputMode,
                new[] { "GPU インスタンシング", "チャンク結合" },
                new[] { "GPU Instanced", "Merged Chunks" });
            float nextChunk = Mathf.Max(
                1f,
                EditorGUILayout.FloatField(L("チャンク寸法 (m)", "Chunk Size (m)"), chunkSize));
            bool nextBuild = EditorGUILayout.Toggle(L("配置時に生成", "Generate on Placement"), buildImmediately);
            GameObject nextParent = EditorGUILayout.ObjectField(
                L("親", "Parent"), parent, typeof(GameObject), true) as GameObject;

            if (!Mathf.Approximately(nextDensity, density) || nextSeed != seed
                || nextOutput != outputMode || !Mathf.Approximately(nextChunk, chunkSize)
                || nextBuild != buildImmediately || nextParent != parent)
            {
                Undo.RecordObject(this, "Edit Foliage Field Settings");
                density = nextDensity;
                seed = nextSeed;
                outputMode = nextOutput;
                chunkSize = nextChunk;
                buildImmediately = nextBuild;
                parent = nextParent;
            }
        }

        private void DrawPlacementActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(EnabledSpeciesCount() == 0))
                {
                    string label = placing
                        ? L("配置を終了", "Stop Scene Placement")
                        : L("Scene View でスタンプ配置", "Stamp in Scene View");
                    if (GUILayout.Button(label, GUILayout.Height(30f)))
                    {
                        Undo.RecordObject(this, "Toggle Foliage Placement");
                        placing = !placing;
                        hasPlacementPoint = false;
                        stampPreviewPinned = false;
                        SceneView.RepaintAll();
                    }
                }

                if (GUILayout.Button(
                    L("Scene Pivot に配置", "Place at Scene Pivot"),
                    GUILayout.Height(30f),
                    GUILayout.Width(150f)))
                {
                    SceneView scene = SceneView.lastActiveSceneView;
                    CreateFieldAt(scene != null ? scene.pivot : Vector3.zero);
                }
            }
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!placing)
            {
                return;
            }

            Event current = Event.current;
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                Undo.RecordObject(this, "Stop Foliage Placement");
                placing = false;
                hasPlacementPoint = false;
                stampPreviewPinned = false;
                current.Use();
                Repaint();
                return;
            }

            if (current.alt)
            {
                return;
            }

            int placementControl = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(placementControl);

            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Space
                && hasPlacementPoint)
            {
                stampPreviewPinned = !stampPreviewPinned;
                current.Use();
                Repaint();
                sceneView.Repaint();
            }

            if (!stampPreviewPinned)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
                hasPlacementPoint = TryFindPlacementPoint(ray, out placementPoint);
            }

            if (hasPlacementPoint)
            {
                float markerRadius = shape == FoliageAreaShape.Circle
                    ? radius
                    : Mathf.Max(size.x, size.y) * 0.5f;

                Handles.color = new Color(0.4f, 0.9f, 0.35f, 0.85f);
                if (shape == FoliageAreaShape.Circle)
                {
                    Handles.DrawWireDisc(placementPoint, Vector3.up, markerRadius);
                }
                else
                {
                    Handles.DrawWireCube(placementPoint, new Vector3(size.x, 0f, size.y));
                }

                if (editStampSizeInScene && stampPreviewPinned)
                {
                    DrawStampRangeHandles();
                }

                string instruction = stampPreviewPinned
                    ? L(
                        "範囲を調整 / クリックで配置 / Space で追従再開 / Esc で終了",
                        "Resize / Click to place / Space to resume tracking / Esc to stop")
                    : L(
                        "クリックで配置 / Space で位置固定 / Esc で終了",
                        "Click to place / Space to pin / Esc to stop");
                Handles.Label(placementPoint + Vector3.up * 0.2f, instruction);
            }

            if (current.type == EventType.MouseDown && current.button == 0
                && hasPlacementPoint && HandleUtility.nearestControl == placementControl)
            {
                CreateFieldAt(placementPoint);
                current.Use();
            }

            if (current.type == EventType.MouseMove)
            {
                sceneView.Repaint();
            }
        }

        private void DrawStampRangeHandles()
        {
            EditorGUI.BeginChangeCheck();
            Vector2 nextSize = size;
            float nextRadius = radius;

            if (shape == FoliageAreaShape.Circle)
            {
                nextRadius = Handles.RadiusHandle(
                    Quaternion.Euler(90f, 0f, 0f),
                    placementPoint,
                    radius);
            }
            else
            {
                float halfX = size.x * 0.5f;
                float halfZ = size.y * 0.5f;
                Vector3 xPosition = placementPoint + Vector3.right * halfX;
                Vector3 zPosition = placementPoint + Vector3.forward * halfZ;

                halfX = Handles.ScaleValueHandle(
                    halfX,
                    xPosition,
                    Quaternion.Euler(0f, 90f, 0f),
                    HandleUtility.GetHandleSize(xPosition) * 1.2f,
                    Handles.ConeHandleCap,
                    0.1f);
                halfZ = Handles.ScaleValueHandle(
                    halfZ,
                    zPosition,
                    Quaternion.identity,
                    HandleUtility.GetHandleSize(zPosition) * 1.2f,
                    Handles.ConeHandleCap,
                    0.1f);
                nextSize = new Vector2(halfX * 2f, halfZ * 2f);
            }

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(this, "Resize Foliage Stamp Range");
                radius = FoliageStampUtility.SanitizeRadius(nextRadius);
                size = FoliageStampUtility.SanitizeSize(nextSize);
                Repaint();
            }
        }

        private static bool TryFindPlacementPoint(Ray ray, out Vector3 point)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, ~0, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                return true;
            }

            var ground = new Plane(Vector3.up, Vector3.zero);
            if (ground.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }

            point = Vector3.zero;
            return false;
        }

        private void CreateFieldAt(Vector3 point)
        {
            List<FoliageSpecies> species = ResolveSpeciesForPlacement();
            if (species.Count == 0)
            {
                Debug.LogWarning("[SabaProps Foliage] Palette に配置可能な Species がありません。");
                return;
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Place Foliage Field");

            var go = new GameObject("Foliage Field (Palette)");
            GameObjectUtility.SetParentAndAlign(go, parent);
            go.transform.position = point;

            var field = go.AddComponent<FoliageField>();
            field.shape = shape;
            field.size = size;
            field.radius = radius;
            field.density = density;
            field.seed = seed;
            field.outputMode = outputMode;
            field.chunkSize = chunkSize;

            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (!IsPlacementEntry(entry))
                {
                    continue;
                }

                field.species.Add(species[field.species.Count]);
                field.speciesWeights.Add(Mathf.Max(0.001f, entry.weight));
            }

            Undo.RegisterCreatedObjectUndo(go, "Place Foliage Field");
            Selection.activeGameObject = go;

            if (buildImmediately)
            {
                FoliageFieldBuilder.Build(field);
            }

            Undo.CollapseUndoOperations(undoGroup);
        }

        private List<FoliageSpecies> ResolveSpeciesForPlacement()
        {
            var result = new List<FoliageSpecies>();

            if (editMode == FoliagePaletteEditMode.DirectAsset)
            {
                foreach (Entry entry in entries)
                {
                    if (IsPlacementEntry(entry))
                    {
                        result.Add(entry.source);
                    }
                }

                return result;
            }

            FoliageAssetLibrary.EnsureFolder(PaletteSpeciesFolder);

            foreach (Entry entry in entries)
            {
                if (!IsPlacementEntry(entry) || entry.workingCopy == null)
                {
                    continue;
                }

                FoliageSpecies snapshot = Instantiate(entry.workingCopy);
                snapshot.generatedMesh = null;
                snapshot.hideFlags = HideFlags.None;

                string baseName = FoliageAssetLibrary.DisplayName(snapshot.kind) + "_Palette";
                string path = AssetDatabase.GenerateUniqueAssetPath(
                    $"{PaletteSpeciesFolder}/{baseName}.asset");

                snapshot.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(snapshot, path);
                FoliageAssetLibrary.WriteSpeciesMesh(snapshot);
                result.Add(snapshot);
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        private void EnsureEntries()
        {
            if (entries.Count > 0)
            {
                EnsureWorkingCopies();
                selectedIndex = Mathf.Clamp(selectedIndex, 0, entries.Count - 1);
                return;
            }

            List<FoliageSpecies> defaults =
                FoliageAssetLibrary.CreateOrLoadDefaults(out Material material);

            if (material == null || defaults == null)
            {
                return;
            }

            foreach (FoliageSpecies species in defaults)
            {
                entries.Add(new Entry
                {
                    enabled = species.kind == FoliageSpeciesKind.GrassClump
                        || species.kind == FoliageSpeciesKind.Clover,
                    weight = FoliageAssetLibrary.DefaultFieldWeight(species.kind),
                    source = species,
                    workingCopy = CreateWorkingCopy(species),
                });
            }

            selectedIndex = 0;
            RebuildPreview(CurrentSpecies());
        }

        private void EnsureWorkingCopies()
        {
            foreach (Entry entry in entries)
            {
                if (entry.workingCopy == null && entry.source != null)
                {
                    entry.workingCopy = CreateWorkingCopy(entry.source);
                }
            }
        }

        private static FoliageSpecies CreateWorkingCopy(FoliageSpecies source)
        {
            if (source == null)
            {
                return null;
            }

            FoliageSpecies copy = Instantiate(source);
            copy.name = source.name + " (Palette Working Copy)";
            copy.generatedMesh = null;
            copy.hideFlags = HideFlags.HideAndDontSave;
            return copy;
        }

        private void RemoveEntry(int index)
        {
            if (index < 0 || index >= entries.Count)
            {
                return;
            }

            Undo.RecordObject(this, "Remove Foliage Species");
            RetireWorkingCopy(entries[index]);
            entries.RemoveAt(index);
            selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, entries.Count - 1));
            RebuildPreview(CurrentSpecies());
        }

        private static void DestroyWorkingCopy(Entry entry)
        {
            if (entry != null && entry.workingCopy != null)
            {
                DestroyImmediate(entry.workingCopy);
                entry.workingCopy = null;
            }
        }

        private void RetireWorkingCopy(Entry entry)
        {
            if (entry != null && entry.workingCopy != null)
            {
                retiredWorkingCopies.Add(entry.workingCopy);
                entry.workingCopy = null;
            }
        }

        private FoliageSpecies CurrentSpecies()
        {
            if (entries.Count == 0 || selectedIndex < 0 || selectedIndex >= entries.Count)
            {
                return null;
            }

            Entry entry = entries[selectedIndex];
            return editMode == FoliagePaletteEditMode.WorkingCopy
                ? entry.workingCopy
                : entry.source;
        }

        private int EnabledSpeciesCount()
        {
            int count = 0;
            foreach (Entry entry in entries)
            {
                if (IsPlacementEntry(entry))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsPlacementEntry(Entry entry)
        {
            return entry != null && entry.enabled && entry.source != null && entry.weight > 0f;
        }

        private float TotalWeight()
        {
            float total = 0f;
            foreach (Entry entry in entries)
            {
                if (entry.enabled && entry.source != null)
                {
                    total += Mathf.Max(0f, entry.weight);
                }
            }

            return total;
        }

        private void EnsurePreview()
        {
            if (preview != null)
            {
                return;
            }

            preview = new PreviewRenderUtility();
            preview.camera.clearFlags = CameraClearFlags.Color;
            preview.camera.backgroundColor = new Color(0.09f, 0.10f, 0.08f, 1f);
            preview.ambientColor = new Color(0.35f, 0.38f, 0.32f, 1f);
            preview.lights[0].intensity = 1.25f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            preview.lights[1].intensity = 0.65f;
        }

        private void RebuildPreview(FoliageSpecies species)
        {
            if (previewMesh != null)
            {
                DestroyImmediate(previewMesh);
            }

            previewSpecies = species;
            previewMesh = species != null ? FoliageMeshBuilder.Build(species) : null;
            Repaint();
        }

        private void RefreshDirectAssetMesh(FoliageSpecies species)
        {
            if (editMode == FoliagePaletteEditMode.DirectAsset && species != null)
            {
                FoliageAssetLibrary.WriteSpeciesMesh(species);
            }
        }

        private void CleanupPreview()
        {
            if (previewMesh != null)
            {
                DestroyImmediate(previewMesh);
                previewMesh = null;
            }

            if (preview != null)
            {
                preview.Cleanup();
                preview = null;
            }
        }

        private static string L(string japanese, string english)
        {
            return SabaPropsEditorLocalization.Text(japanese, english);
        }
    }
}
