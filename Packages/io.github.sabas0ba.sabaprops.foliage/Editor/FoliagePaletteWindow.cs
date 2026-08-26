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
            var window = GetWindow<FoliagePaletteWindow>(false, "Foliage Palette", true);
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

            DrawMode();
            EditorGUILayout.Space(8f);
            DrawComposition();
            EditorGUILayout.Space(8f);
            DrawParameters();
            EditorGUILayout.Space(8f);
            DrawPreview();
            EditorGUILayout.Space(8f);
            DrawFieldSettings();

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6f);
            DrawPlacementActions();
        }

        private void DrawMode()
        {
            EditorGUILayout.LabelField("Editing", EditorStyles.boldLabel);

            var next = (FoliagePaletteEditMode)EditorGUILayout.EnumPopup("Mode", editMode);
            if (next != editMode)
            {
                Undo.RecordObject(this, "Change Foliage Palette Mode");
                editMode = next;
                EnsureWorkingCopies();
                RebuildPreview(CurrentSpecies());
            }

            EditorGUILayout.HelpBox(
                editMode == FoliagePaletteEditMode.WorkingCopy
                    ? "作業用コピーだけを変更します。配置時に新しい Species アセットとして保存され、既存フィールドには影響しません。"
                    : "既存 Species アセットを直接変更します。同じアセットを参照する全フィールドへ反映されます。",
                editMode == FoliagePaletteEditMode.WorkingCopy ? MessageType.Info : MessageType.Warning);
        }

        private void DrawComposition()
        {
            EditorGUILayout.LabelField("Composition", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "有効な種の Weight がフィールド内の出現比率になります。種名を押すと形状パラメータを編集できます。",
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

            if (GUILayout.Button("Add Species"))
            {
                Undo.RecordObject(this, "Add Foliage Species");
                entries.Add(new Entry());
                selectedIndex = entries.Count - 1;
            }

            if (EnabledSpeciesCount() == 0)
            {
                EditorGUILayout.HelpBox("配置する Species を 1 つ以上有効にしてください。", MessageType.Warning);
            }
        }

        private void DrawParameters()
        {
            EditorGUILayout.LabelField("Parameters", EditorStyles.boldLabel);

            FoliageSpecies species = CurrentSpecies();
            if (species == null)
            {
                EditorGUILayout.HelpBox("Composition から Species を選択してください。", MessageType.Info);
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
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            Rect rect = GUILayoutUtility.GetRect(10f, 230f, GUILayout.ExpandWidth(true));
            FoliageSpecies species = CurrentSpecies();
            EnsurePreview();

            if (species == null || species.material == null)
            {
                EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));
                GUI.Label(rect, "Material を持つ Species を選択してください。", EditorStyles.whiteBoldLabel);
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

        private void DrawFieldSettings()
        {
            EditorGUILayout.LabelField("Field", EditorStyles.boldLabel);

            FoliageAreaShape nextShape = (FoliageAreaShape)EditorGUILayout.EnumPopup("Shape", shape);
            Vector2 nextSize = size;
            float nextRadius = radius;

            if (nextShape == FoliageAreaShape.Circle)
            {
                nextRadius = Mathf.Max(0.1f, EditorGUILayout.FloatField("Radius (m)", radius));
            }
            else
            {
                nextSize = EditorGUILayout.Vector2Field("Size (m)", size);
                nextSize.x = Mathf.Max(0.1f, Mathf.Abs(nextSize.x));
                nextSize.y = Mathf.Max(0.1f, Mathf.Abs(nextSize.y));
            }

            float nextDensity = Mathf.Max(0.001f, EditorGUILayout.FloatField("Density (/m²)", density));
            int nextSeed = EditorGUILayout.IntField("Seed", seed);
            FoliageOutputMode nextOutput =
                (FoliageOutputMode)EditorGUILayout.EnumPopup("Output", outputMode);
            float nextChunk = Mathf.Max(1f, EditorGUILayout.FloatField("Chunk Size (m)", chunkSize));
            bool nextBuild = EditorGUILayout.Toggle("Generate now", buildImmediately);
            GameObject nextParent = EditorGUILayout.ObjectField(
                "Parent", parent, typeof(GameObject), true) as GameObject;

            if (nextShape != shape || nextSize != size || !Mathf.Approximately(nextRadius, radius)
                || !Mathf.Approximately(nextDensity, density) || nextSeed != seed
                || nextOutput != outputMode || !Mathf.Approximately(nextChunk, chunkSize)
                || nextBuild != buildImmediately || nextParent != parent)
            {
                Undo.RecordObject(this, "Edit Foliage Field Settings");
                shape = nextShape;
                size = nextSize;
                radius = nextRadius;
                density = nextDensity;
                seed = nextSeed;
                outputMode = nextOutput;
                chunkSize = nextChunk;
                buildImmediately = nextBuild;
                parent = nextParent;
            }

            float area = shape == FoliageAreaShape.Circle
                ? Mathf.PI * radius * radius
                : size.x * size.y;
            EditorGUILayout.LabelField($"概算 {Mathf.RoundToInt(area * density):N0} 個体", EditorStyles.miniLabel);
        }

        private void DrawPlacementActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(EnabledSpeciesCount() == 0))
                {
                    string label = placing ? "Stop Scene Placement" : "Place in Scene";
                    if (GUILayout.Button(label, GUILayout.Height(30f)))
                    {
                        Undo.RecordObject(this, "Toggle Foliage Placement");
                        placing = !placing;
                        hasPlacementPoint = false;
                        SceneView.RepaintAll();
                    }
                }

                if (GUILayout.Button("Place at Scene Pivot", GUILayout.Height(30f), GUILayout.Width(150f)))
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
                current.Use();
                Repaint();
                return;
            }

            if (current.alt)
            {
                return;
            }

            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            hasPlacementPoint = TryFindPlacementPoint(ray, out placementPoint);

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

                Handles.Label(placementPoint + Vector3.up * 0.2f, "Click to place Foliage Field");
            }

            if (current.type == EventType.MouseDown && current.button == 0 && hasPlacementPoint)
            {
                CreateFieldAt(placementPoint);
                current.Use();
            }

            if (current.type == EventType.MouseMove)
            {
                sceneView.Repaint();
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
    }
}
