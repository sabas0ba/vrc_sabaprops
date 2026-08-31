using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Foliage.Editors
{
    /// <summary>
    /// Asks which species a new field should scatter, and in what proportion,
    /// before creating it.
    /// <para>
    /// The mix is the decision that shapes a field, and it is tedious to undo:
    /// changing it afterwards means editing the list, rebuilding, and looking
    /// again. Asking once, up front, is cheaper than the round trip.
    /// </para>
    /// </summary>
    public class FoliageFieldWizard : EditorWindow
    {
        private struct Entry
        {
            public FoliageSpeciesKind Kind;
            public bool Enabled;
            public float Weight;
        }

        private readonly List<Entry> _entries = new List<Entry>();

        private FoliageAreaShape _shape = FoliageAreaShape.Rectangle;
        private Vector2 _size = new Vector2(16f, 16f);
        private float _radius = 8f;
        private float _density = 8f;
        private int _seed = 12345;
        private FoliageOutputMode _outputMode = FoliageOutputMode.GpuInstanced;
        private float _chunkSize = 12f;
        private bool _buildImmediately = true;

        private SkinnedMeshRenderer _skinnedGround;

        private bool _limitAltitude;
        private Vector2 _altitudeLimits = new Vector2(0f, 10f);

        private Texture2D _densityMask;
        private float _densityMaskThreshold = 0.05f;
        private bool _invertDensityMask;

        private GameObject _parent;
        private Vector2 _scroll;

        public static void Open(GameObject parent)
        {
            var window = GetWindow<FoliageFieldWizard>(
                true,
                SabaPropsEditorLocalization.Text("植生フィールドを作成", "Create Foliage Field"),
                true);
            window._parent = parent;
            window.minSize = new Vector2(380f, 480f);
            window.ResetEntries();
            window.ShowUtility();
        }

        private void ResetEntries()
        {
            _entries.Clear();

            foreach (FoliageSpeciesKind kind in FoliageAssetLibrary.AllKinds)
            {
                _entries.Add(new Entry
                {
                    Kind = kind,

                    // Grass and clover alone read as a lawn, which is the least
                    // surprising thing to hand someone who just pressed create.
                    Enabled = kind == FoliageSpeciesKind.GrassClump || kind == FoliageSpeciesKind.Clover,
                    Weight = FoliageAssetLibrary.DefaultFieldWeight(kind),
                });
            }
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            SabaPropsEditorLocalization.DrawLanguageSelector();
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(L("Species", "Species"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                L(
                    "チェックした種だけを配置します。Weight は種どうしの出現比率で、このフィールドにのみ効きます（Species アセットは書き換えません）。",
                    "Only checked species are placed. Weight controls their relative frequency in this field without changing Species assets."),
                EditorStyles.wordWrappedMiniLabel);

            float total = 0f;
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Enabled)
                {
                    total += Mathf.Max(0f, _entries[i].Weight);
                }
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];

                using (new EditorGUILayout.HorizontalScope())
                {
                    entry.Enabled = EditorGUILayout.ToggleLeft(
                        FoliageAssetLibrary.DisplayName(entry.Kind), entry.Enabled, GUILayout.Width(120f));

                    using (new EditorGUI.DisabledScope(!entry.Enabled))
                    {
                        entry.Weight = EditorGUILayout.Slider(entry.Weight, 0.01f, 2f);

                        string share = entry.Enabled && total > 0f
                            ? $"{Mathf.Max(0f, entry.Weight) / total * 100f:0.#} %"
                            : "-";

                        EditorGUILayout.LabelField(share, GUILayout.Width(52f));
                    }
                }

                _entries[i] = entry;
            }

            if (total <= 0f)
            {
                EditorGUILayout.HelpBox(
                    L("種を 1 つ以上選んでください。", "Select at least one species."),
                    MessageType.Warning);
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField(L("範囲", "Area"), EditorStyles.boldLabel);

            _shape = (FoliageAreaShape)SabaPropsEditorLocalization.Popup(
                "形状",
                "Shape",
                (int)_shape,
                new[] { "矩形", "円形" },
                new[] { "Rectangle", "Circle" });
            if (_shape == FoliageAreaShape.Circle)
            {
                _radius = FoliageStampUtility.SanitizeRadius(
                    EditorGUILayout.FloatField(L("半径 (m)", "Radius (m)"), _radius));
            }
            else
            {
                _size = FoliageStampUtility.SanitizeSize(
                    EditorGUILayout.Vector2Field(L("寸法 X/Z (m)", "Size X/Z (m)"), _size));
            }

            _density = Mathf.Max(
                0.001f,
                EditorGUILayout.FloatField(L("密度 (/m²)", "Density (/m²)"), _density));
            _seed = EditorGUILayout.IntField(L("シード", "Seed"), _seed);

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField(L("生育条件", "Where It May Grow"), EditorStyles.boldLabel);

            _skinnedGround = EditorGUILayout.ObjectField(
                L("Skinned 地面", "Skinned Ground"), _skinnedGround, typeof(SkinnedMeshRenderer), true) as SkinnedMeshRenderer;

            EditorGUILayout.LabelField(
                L(
                    "SkinnedMeshRenderer を地面にする場合に指定します。生成時だけ現在のポーズをベイクした一時 Collider を作るため、対象に Collider は不要です。",
                    "Set a SkinnedMeshRenderer as ground. A temporary collider is baked from its current pose during generation."),
                EditorStyles.wordWrappedMiniLabel);

            _limitAltitude = EditorGUILayout.Toggle(L("高度を制限", "Limit by Height"), _limitAltitude);
            using (new EditorGUI.DisabledScope(!_limitAltitude))
            {
                _altitudeLimits = EditorGUILayout.Vector2Field(
                    L("高度 (m, ワールド Y)", "Height (m, World Y)"),
                    _altitudeLimits);
            }

            _densityMask = EditorGUILayout.ObjectField(
                L("密度マスク", "Density Mask"), _densityMask, typeof(Texture2D), false) as Texture2D;

            using (new EditorGUI.DisabledScope(_densityMask == null))
            {
                _densityMaskThreshold = EditorGUILayout.Slider(
                    L("マスク閾値", "Mask Threshold"), _densityMaskThreshold, 0f, 1f);
                _invertDensityMask = EditorGUILayout.Toggle(
                    L("マスクを反転", "Invert Mask"), _invertDensityMask);
            }

            if (_densityMask != null && !_densityMask.isReadable)
            {
                EditorGUILayout.HelpBox(
                    L(
                        "このテクスチャは Read/Write Enabled が OFF です。インポート設定で有効にしてください。",
                        "Read/Write Enabled is off for this texture. Enable it in the import settings."),
                    MessageType.Warning);
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField(L("出力", "Output"), EditorStyles.boldLabel);

            _outputMode = (FoliageOutputMode)SabaPropsEditorLocalization.Popup(
                "モード",
                "Mode",
                (int)_outputMode,
                new[] { "GPU インスタンシング", "チャンク結合" },
                new[] { "GPU Instanced", "Merged Chunks" });
            _chunkSize = Mathf.Max(
                1f,
                EditorGUILayout.FloatField(L("チャンク寸法 (m)", "Chunk Size (m)"), _chunkSize));

            EditorGUILayout.HelpBox(
                _outputMode == FoliageOutputMode.GpuInstanced
                    ? L(
                        "1 個体 1 Renderer。個体ごとのカリングと距離縮退が効きます。数千個体まで。",
                        "One Renderer per instance, with per-instance culling and distance reduction. Suitable for thousands of instances.")
                    : L(
                        "チャンク単位でメッシュ結合。1 チャンク 1 ドローコール。数千〜数万個体向け。",
                        "Meshes are merged per chunk, with one draw call per chunk. Suitable for thousands to tens of thousands."),
                MessageType.None);

            EditorGUILayout.Space(6f);
            _buildImmediately = EditorGUILayout.Toggle(
                L("作成時に生成", "Generate on Creation"), _buildImmediately);

            EditorGUILayout.LabelField(
                L(
                    $"概算 {EstimatedCount():N0} 個体",
                    $"Estimated {EstimatedCount():N0} instances"),
                EditorStyles.miniLabel);

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8f);

            using (new EditorGUI.DisabledScope(total <= 0f))
            {
                if (GUILayout.Button(L("作成", "Create"), GUILayout.Height(28f)))
                {
                    Create();
                }
            }
        }

        private int EstimatedCount()
        {
            return FoliageStampUtility.EstimateInstanceCount(
                _shape, _size, _radius, _density);
        }

        private void Create()
        {
            var kinds = new List<FoliageSpeciesKind>();
            var weights = new List<float>();

            foreach (Entry entry in _entries)
            {
                if (!entry.Enabled)
                {
                    continue;
                }

                kinds.Add(entry.Kind);
                weights.Add(Mathf.Max(0.001f, entry.Weight));
            }

            List<FoliageSpecies> species =
                FoliageAssetLibrary.CreateOrLoadDefaults(out Material material, kinds.ToArray());

            if (material == null || species == null || species.Count == 0)
            {
                return;
            }

            AssetDatabase.SaveAssets();

            var go = new GameObject("Foliage Field");
            GameObjectUtility.SetParentAndAlign(go, _parent);

            var field = go.AddComponent<FoliageField>();
            field.shape = _shape;
            field.size = _size;
            field.radius = _radius;
            field.density = _density;
            field.seed = _seed;
            field.outputMode = _outputMode;
            field.chunkSize = _chunkSize;
            field.species.AddRange(species);
            field.speciesWeights.AddRange(weights);

            if (_skinnedGround != null)
            {
                field.skinnedGround.Add(_skinnedGround);
            }

            if (_limitAltitude)
            {
                field.altitudeLimits = _altitudeLimits;
            }

            field.densityMask = _densityMask;
            field.densityMaskThreshold = _densityMaskThreshold;
            field.invertDensityMask = _invertDensityMask;

            // Drop the field where the user is looking, not at the world origin.
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null && _parent == null)
            {
                go.transform.position = sceneView.pivot;
            }

            Undo.RegisterCreatedObjectUndo(go, "Create Foliage Field");
            Selection.activeGameObject = go;

            if (_buildImmediately)
            {
                FoliageFieldBuilder.Build(field);
            }

            Close();
        }

        private static string L(string japanese, string english)
        {
            return SabaPropsEditorLocalization.Text(japanese, english);
        }
    }
}
