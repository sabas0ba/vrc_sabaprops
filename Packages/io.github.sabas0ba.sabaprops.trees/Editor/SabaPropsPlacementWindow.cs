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
                false, "SabaProps Placement", true);
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
            EditorGUILayout.LabelField("Scene Placement", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Use this window for authoring objects. Demo generators are under " +
                "Tools > SabaProps > Debug.",
                MessageType.Info);

            hierarchyParent = EditorGUILayout.ObjectField(
                "Hierarchy Parent", hierarchyParent, typeof(GameObject), true) as GameObject;

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Ground Foliage", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Compose species, preview them, then place a field by clicking in the Scene view.",
                EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Foliage Palette", GUILayout.Height(28f)))
                {
                    FoliagePaletteWindow.Open();
                }
                if (GUILayout.Button("Quick Field...", GUILayout.Height(28f)))
                {
                    FoliageFieldWizard.Open(hierarchyParent);
                }
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Surface Growth", EditorStyles.boldLabel);
            surface = EditorGUILayout.ObjectField(
                "Selected Collider", surface, typeof(Collider), true) as Collider;
            EditorGUILayout.LabelField(
                "Creates Surface Vine or Rhizome Patch with a botanical preset, " +
                "initial direction, adjacent surfaces, and optional immediate build.",
                EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("Open Surface Growth Placer", GUILayout.Height(28f)))
            {
                SurfaceGrowthPlacementWindow.Open(surface);
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Trees", EditorStyles.boldLabel);
            treeSpecies = EditorGUILayout.ObjectField(
                "Tree Species", treeSpecies, typeof(TreeSpecies), false) as TreeSpecies;
            using (new EditorGUI.DisabledScope(treeSpecies == null))
            {
                if (GUILayout.Button("Place One Tree at Scene Pivot", GUILayout.Height(28f)))
                {
                    TreePlacementUtility.CreateTree(
                        treeSpecies, hierarchyParent, ScenePivot());
                }

                treeFieldSize = EditorGUILayout.Vector2Field(
                    "Field Size (m)", treeFieldSize);
                treeDensity = Mathf.Max(
                    0.0001f,
                    EditorGUILayout.FloatField("Density (/m²)", treeDensity));
                buildTreeFieldImmediately = EditorGUILayout.Toggle(
                    "Build Immediately", buildTreeFieldImmediately);
                if (GUILayout.Button("Create Tree Field at Scene Pivot", GUILayout.Height(28f)))
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
                    "Select a TreeSpecies asset to enable tree placement.",
                    MessageType.Warning);
                if (GUILayout.Button("Select / Create Default Broadleaf"))
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
    }
}
