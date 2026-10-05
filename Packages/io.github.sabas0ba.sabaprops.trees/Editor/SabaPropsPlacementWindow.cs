using SabaProps.Foliage.Editors;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Trees.Editors
{
    /// <summary>Reusable scene creation operations exposed by the placement UI.</summary>
    public static class TreePlacementUtility
    {
        public static GameObject CreateTree(
            TreeSpecies species,
            GameObject hierarchyParent,
            Vector3 worldPosition)
        {
            if (species == null)
            {
                return null;
            }

            GameObject tree = TreeAssetLibrary.CreateLodGroup(
                species,
                hierarchyParent != null ? hierarchyParent.transform : null);
            if (tree != null)
            {
                tree.transform.position = worldPosition;
                Selection.activeGameObject = tree;
            }
            return tree;
        }

        public static TreeField CreateField(
            TreeSpecies species,
            GameObject hierarchyParent,
            Vector3 worldPosition,
            Vector2 size,
            float density,
            bool buildImmediately)
        {
            if (species == null)
            {
                return null;
            }

            var gameObject = new GameObject("Tree Field");
            Undo.RegisterCreatedObjectUndo(gameObject, "Place Tree Field");
            gameObject.transform.position = worldPosition;
            if (hierarchyParent != null)
            {
                gameObject.transform.SetParent(hierarchyParent.transform, true);
            }

            TreeField field = gameObject.AddComponent<TreeField>();
            field.size = new Vector2(
                Mathf.Max(0.1f, Mathf.Abs(size.x)),
                Mathf.Max(0.1f, Mathf.Abs(size.y)));
            field.density = Mathf.Max(0.0001f, density);
            field.species.Add(species);
            field.speciesWeights.Add(Mathf.Max(
                0.0001f, species.placement.placementWeight));
            Selection.activeGameObject = gameObject;

            if (buildImmediately)
            {
                TreeFieldBuilder.Build(field);
            }
            return field;
        }
    }

    /// <summary>One entry point for foliage, surface growth, and tree placement.</summary>
    public sealed class SabaPropsPlacementWindow : EditorWindow
    {
        [SerializeField] private GameObject hierarchyParent;
        [SerializeField] private Collider surface;
        [SerializeField] private TreeSpecies treeSpecies;
        [SerializeField] private Vector2 treeFieldSize = new Vector2(30f, 30f);
        [SerializeField] private float treeDensity = 0.08f;
        [SerializeField] private bool buildTreeFieldImmediately;
        private Vector2 scroll;

        [MenuItem("Window/SabaProps/Placement", false, 2000)]
        public static void Open()
        {
            var window = GetWindow<SabaPropsPlacementWindow>(
                false,
                SabaPropsEditorLocalization.Text("SabaProps 配置", "SabaProps Placement"),
                true);
            window.minSize = new Vector2(380f, 470f);
            window.UseSelectionIfRelevant();
            window.Show();
        }

        [MenuItem("Tools/SabaProps/Placement/Open Placement Window", false, 0)]
        public static void OpenFromToolsMenu()
        {
            Open();
        }

        private void OnSelectionChange()
        {
            UseSelectionIfRelevant();
            Repaint();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            SabaPropsEditorLocalization.DrawLanguageSelector();
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(L("シーン配置", "Scene Placement"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                L(
                    "配置用オブジェクトはこのウィンドウから作成します。デモ生成は Tools > SabaProps > Debug にあります。",
                    "Use this window for authoring objects. Demo generators are under " +
                    "Tools > SabaProps > Debug."),
                MessageType.Info);

            hierarchyParent = EditorGUILayout.ObjectField(
                L("Hierarchy 親", "Hierarchy Parent"), hierarchyParent, typeof(GameObject), true) as GameObject;

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField(L("地表の植生", "Ground Foliage"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                L(
                    "植物を配合・プレビューし、Scene View をクリックしてフィールドを配置します。",
                    "Compose species, preview them, then place a field by clicking in the Scene view."),
                EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(L("植生パレットを開く", "Open Foliage Palette"), GUILayout.Height(28f)))
                {
                    FoliagePaletteWindow.Open();
                }
                if (GUILayout.Button(L("簡易フィールド...", "Quick Field..."), GUILayout.Height(28f)))
                {
                    FoliageFieldWizard.Open(hierarchyParent);
                }
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField(L("表面植生", "Surface Growth"), EditorStyles.boldLabel);
            surface = EditorGUILayout.ObjectField(
                L("対象 Collider", "Selected Collider"), surface, typeof(Collider), true) as Collider;
            EditorGUILayout.LabelField(
                L(
                    "植物プリセット、初期方向、隣接面を指定して表面ツタまたは根茎パッチを作成します。",
                    "Creates Surface Vine or Rhizome Patch with a botanical preset, " +
                    "initial direction, adjacent surfaces, and optional immediate build."),
                EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button(L("表面植生の配置を開く", "Open Surface Growth Placer"), GUILayout.Height(28f)))
            {
                SurfaceGrowthPlacementWindow.Open(surface);
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField(L("樹木", "Trees"), EditorStyles.boldLabel);
            treeSpecies = EditorGUILayout.ObjectField(
                L("樹種", "Tree Species"), treeSpecies, typeof(TreeSpecies), false) as TreeSpecies;
            using (new EditorGUI.DisabledScope(treeSpecies == null))
            {
                if (GUILayout.Button(L("Scene Pivot に単木を配置", "Place One Tree at Scene Pivot"), GUILayout.Height(28f)))
                {
                    TreePlacementUtility.CreateTree(
                        treeSpecies, hierarchyParent, ScenePivot());
                }

                treeFieldSize = EditorGUILayout.Vector2Field(
                    L("フィールド寸法 (m)", "Field Size (m)"), treeFieldSize);
                treeDensity = Mathf.Max(
                    0.0001f,
                    EditorGUILayout.FloatField(L("密度 (/m²)", "Density (/m²)"), treeDensity));
                buildTreeFieldImmediately = EditorGUILayout.Toggle(
                    L("作成時にビルド", "Build Immediately"), buildTreeFieldImmediately);
                if (GUILayout.Button(L("Scene Pivot に樹木フィールドを作成", "Create Tree Field at Scene Pivot"), GUILayout.Height(28f)))
                {
                    TreePlacementUtility.CreateField(
                        treeSpecies,
                        hierarchyParent,
                        ScenePivot(),
                        treeFieldSize,
                        treeDensity,
                        buildTreeFieldImmediately);
                }
            }

            if (treeSpecies == null)
            {
                EditorGUILayout.HelpBox(
                    L(
                        "樹木を配置するには TreeSpecies アセットを選択してください。",
                        "Select a TreeSpecies asset to enable tree placement."),
                    MessageType.Warning);
                if (GUILayout.Button(L("既定の広葉樹を選択 / 作成", "Select / Create Default Broadleaf")))
                {
                    treeSpecies = TreeAssetLibrary.CreateOrLoadSpecies(
                        TreeArchetype.Broadleaf);
                    Selection.activeObject = treeSpecies;
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void UseSelectionIfRelevant()
        {
            TreeSpecies selectedSpecies = Selection.activeObject as TreeSpecies;
            if (selectedSpecies != null)
            {
                treeSpecies = selectedSpecies;
                return;
            }

            GameObject selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
            {
                return;
            }

            Collider selectedCollider = selectedObject.GetComponent<Collider>();
            if (selectedCollider != null)
            {
                surface = selectedCollider;
            }
            else
            {
                hierarchyParent = selectedObject;
            }
        }

        private static Vector3 ScenePivot()
        {
            SceneView scene = SceneView.lastActiveSceneView;
            return scene != null ? scene.pivot : Vector3.zero;
        }

        private static string L(string japanese, string english)
        {
            return SabaPropsEditorLocalization.Text(japanese, english);
        }
    }
}
