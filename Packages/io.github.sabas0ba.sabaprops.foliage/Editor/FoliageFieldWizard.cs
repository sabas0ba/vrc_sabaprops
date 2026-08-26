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
            var window = GetWindow<FoliageFieldWizard>(true, "Create Foliage Field", true);
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

            EditorGUILayout.LabelField("Species", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "チェックした種だけを配置します。Weight は種どうしの出現比率で、"
                + "このフィールドにのみ効きます（Species アセットは書き換えません）。",
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
                EditorGUILayout.HelpBox("種を 1 つ以上選んでください。", MessageType.Warning);
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Area", EditorStyles.boldLabel);

            _shape = (FoliageAreaShape)EditorGUILayout.EnumPopup("Shape", _shape);
            if (_shape == FoliageAreaShape.Circle)
            {
                _radius = Mathf.Max(0.1f, EditorGUILayout.FloatField("Radius (m)", _radius));
            }
            else
            {
                _size = EditorGUILayout.Vector2Field("Size (m)", _size);
            }

            _density = Mathf.Max(0.001f, EditorGUILayout.FloatField("Density (/m²)", _density));
            _seed = EditorGUILayout.IntField("Seed", _seed);

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Where it may grow", EditorStyles.boldLabel);

            _skinnedGround = EditorGUILayout.ObjectField(
                "Skinned Ground", _skinnedGround, typeof(SkinnedMeshRenderer), true) as SkinnedMeshRenderer;

            EditorGUILayout.LabelField(
                "SkinnedMeshRenderer を地面にする場合に指定します。生成時だけ現在のポーズをベイクした"
                + "一時 Collider を作るので、対象に Collider を付けておく必要はありません。",
                EditorStyles.wordWrappedMiniLabel);

            _limitAltitude = EditorGUILayout.Toggle("Limit by height", _limitAltitude);
            using (new EditorGUI.DisabledScope(!_limitAltitude))
            {
                _altitudeLimits = EditorGUILayout.Vector2Field("Height (m, world Y)", _altitudeLimits);
            }

            _densityMask = EditorGUILayout.ObjectField(
                "Density Mask", _densityMask, typeof(Texture2D), false) as Texture2D;

            using (new EditorGUI.DisabledScope(_densityMask == null))
            {
                _densityMaskThreshold = EditorGUILayout.Slider("Mask Threshold", _densityMaskThreshold, 0f, 1f);
                _invertDensityMask = EditorGUILayout.Toggle("Invert Mask", _invertDensityMask);
            }

            if (_densityMask != null && !_densityMask.isReadable)
            {
                EditorGUILayout.HelpBox(
                    "このテクスチャは Read/Write Enabled が OFF です。インポート設定で有効にしてください。",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

            _outputMode = (FoliageOutputMode)EditorGUILayout.EnumPopup("Mode", _outputMode);
            _chunkSize = Mathf.Max(1f, EditorGUILayout.FloatField("Chunk Size (m)", _chunkSize));

            EditorGUILayout.HelpBox(
                _outputMode == FoliageOutputMode.GpuInstanced
                    ? "1 個体 1 Renderer。個体ごとのカリングと距離縮退が効きます。数千個体まで。"
                    : "チャンク単位でメッシュ結合。1 チャンク 1 ドローコール。数千〜数万個体向け。",
                MessageType.None);

            EditorGUILayout.Space(6f);
            _buildImmediately = EditorGUILayout.Toggle("Generate now", _buildImmediately);

            EditorGUILayout.LabelField(
                $"概算 {EstimatedCount():N0} 個体",
                EditorStyles.miniLabel);

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8f);

            using (new EditorGUI.DisabledScope(total <= 0f))
            {
                if (GUILayout.Button("Create", GUILayout.Height(28f)))
                {
                    Create();
                }
            }
        }

        private int EstimatedCount()
        {
            float area = _shape == FoliageAreaShape.Circle
                ? Mathf.PI * _radius * _radius
                : Mathf.Abs(_size.x) * Mathf.Abs(_size.y);

            return Mathf.RoundToInt(area * _density);
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
    }
}
