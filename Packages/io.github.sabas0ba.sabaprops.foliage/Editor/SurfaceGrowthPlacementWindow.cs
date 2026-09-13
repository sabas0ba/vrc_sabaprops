using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Foliage.Editors
{
    public enum SurfaceGrowthPlacementKind
    {
        SurfaceVine = 0,
        RhizomePatch = 1,
    }

    public enum SurfaceVinePlacementPreset
    {
        CreepingFig = 0,
        EnglishIvy = 1,
        BostonIvy = 2,
    }

    public enum SurfaceGrowthPlacementDirection
    {
        WorldUp = 0,
        WorldDown = 1,
        WorldRight = 2,
        WorldLeft = 3,
        Custom = 4,
    }

    /// <summary>Creates surface-growth authoring objects with usable initial guides.</summary>
    public static class SurfaceGrowthPlacementUtility
    {
        public static Vector3 SuggestStartPoint(Collider surface)
        {
            SceneView scene = SceneView.lastActiveSceneView;
            Vector3 probe = scene != null ? scene.pivot : Vector3.zero;
            if (surface == null)
            {
                return probe;
            }

            if (scene != null && scene.camera != null)
            {
                Vector3 direction = surface.bounds.center - scene.camera.transform.position;
                if (direction.sqrMagnitude > 0.000001f && surface.Raycast(
                    new Ray(scene.camera.transform.position, direction.normalized),
                    out RaycastHit hit,
                    direction.magnitude + surface.bounds.extents.magnitude * 2f))
                {
                    return hit.point;
                }
            }

            return surface.ClosestPoint(probe);
        }

        public static SurfaceVine CreateVine(
            Collider targetSurface,
            IList<Collider> additionalSurfaces,
            Material material,
            Vector3 worldStart,
            Vector3 worldDirection,
            float guideLength,
            SurfaceVinePlacementPreset preset,
            GameObject hierarchyParent,
            bool buildImmediately)
        {
            var gameObject = new GameObject("Surface Vine");
            Undo.RegisterCreatedObjectUndo(gameObject, "Place Surface Vine");
            gameObject.transform.position = worldStart;
            if (hierarchyParent != null)
            {
                gameObject.transform.SetParent(hierarchyParent.transform, true);
            }

            SurfaceVine vine = gameObject.AddComponent<SurfaceVine>();
            vine.targetSurface = targetSurface;
            CopyAdditionalSurfaces(vine.additionalSurfaces, targetSurface, additionalSurfaces);
            vine.material = material != null
                ? material
                : FoliageAssetLibrary.CreateOrLoadDefaultMaterial();

            switch (preset)
            {
                case SurfaceVinePlacementPreset.CreepingFig:
                    vine.morphology.ApplyCreepingFigPreset();
                    break;
                case SurfaceVinePlacementPreset.BostonIvy:
                    vine.morphology.ApplyBostonIvyPreset();
                    break;
                default:
                    vine.morphology.ApplyEnglishIvyPreset();
                    break;
            }

            Vector3 direction = worldDirection.sqrMagnitude > 0.000001f
                ? worldDirection.normalized
                : Vector3.up;
            Vector3 localDirection = gameObject.transform.InverseTransformDirection(direction);
            float length = Mathf.Max(0.1f, guideLength);
            vine.guidePoints.Clear();
            vine.guidePoints.Add(Vector3.zero);
            vine.guidePoints.Add(localDirection * (length * 0.5f));
            vine.guidePoints.Add(localDirection * length);

            EditorUtility.SetDirty(vine);
            Selection.activeGameObject = gameObject;
            if (buildImmediately && targetSurface != null)
            {
                SurfaceGrowthAuthoringBuilder.Build(vine);
            }
            return vine;
        }

        public static RhizomePatch CreateRhizomePatch(
            Collider targetSurface,
            IList<Collider> additionalSurfaces,
            Material material,
            Vector3 worldStart,
            GameObject hierarchyParent,
            bool buildImmediately)
        {
            var gameObject = new GameObject("Rhizome Patch");
            Undo.RegisterCreatedObjectUndo(gameObject, "Place Rhizome Patch");
            gameObject.transform.position = worldStart;
            if (hierarchyParent != null)
            {
                gameObject.transform.SetParent(hierarchyParent.transform, true);
            }

            RhizomePatch patch = gameObject.AddComponent<RhizomePatch>();
            patch.targetSurface = targetSurface;
            CopyAdditionalSurfaces(patch.additionalSurfaces, targetSurface, additionalSurfaces);
            patch.material = material != null
                ? material
                : FoliageAssetLibrary.CreateOrLoadDefaultMaterial();
            patch.guidePoints.Clear();
            patch.guidePoints.Add(Vector3.zero);

            EditorUtility.SetDirty(patch);
            Selection.activeGameObject = gameObject;
            if (buildImmediately && targetSurface != null)
            {
                SurfaceGrowthAuthoringBuilder.Build(patch);
            }
            return patch;
        }

        private static void CopyAdditionalSurfaces(
            List<Collider> destination,
            Collider primary,
            IList<Collider> source)
        {
            destination.Clear();
            if (source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                Collider surface = source[i];
                if (surface != null && surface != primary && !destination.Contains(surface))
                {
                    destination.Add(surface);
                }
            }
        }
    }

    /// <summary>Dockable placement UI for Surface Vine and Rhizome Patch.</summary>
    public sealed class SurfaceGrowthPlacementWindow : EditorWindow
    {
        [SerializeField] private SurfaceGrowthPlacementKind kind;
        [SerializeField] private SurfaceVinePlacementPreset vinePreset =
            SurfaceVinePlacementPreset.EnglishIvy;
        [SerializeField] private SurfaceGrowthPlacementDirection direction =
            SurfaceGrowthPlacementDirection.WorldUp;
        [SerializeField] private Vector3 customDirection = Vector3.up;
        [SerializeField] private float guideLength = 2.2f;
        [SerializeField] private Collider targetSurface;
        [SerializeField] private List<Collider> additionalSurfaces =
            new List<Collider>();
        [SerializeField] private Material material;
        [SerializeField] private GameObject hierarchyParent;
        [SerializeField] private Vector3 worldStart;
        [SerializeField] private bool hasSuggestedStart;
        [SerializeField] private bool buildImmediately = true;
        private Vector2 scroll;

        [MenuItem("Window/SabaProps/Placement/Surface Growth", false, 2020)]
        public static void Open()
        {
            GameObject selected = Selection.activeGameObject;
            Open(selected != null ? selected.GetComponent<Collider>() : null);
        }

        public static void Open(Collider suggestedSurface)
        {
            var window = GetWindow<SurfaceGrowthPlacementWindow>(
                false,
                SabaPropsEditorLocalization.Text("表面植生", "Surface Growth"),
                true);
            window.minSize = new Vector2(360f, 440f);
            if (suggestedSurface != null)
            {
                window.targetSurface = suggestedSurface;
                window.worldStart = SurfaceGrowthPlacementUtility.SuggestStartPoint(
                    suggestedSurface);
                window.hasSuggestedStart = true;
            }
            window.Show();
        }

        private void OnSelectionChange()
        {
            if (targetSurface != null)
            {
                return;
            }

            GameObject selected = Selection.activeGameObject;
            Collider selectedCollider = selected != null
                ? selected.GetComponent<Collider>()
                : null;
            if (selectedCollider != null)
            {
                targetSurface = selectedCollider;
                SuggestStart();
                Repaint();
            }
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            SabaPropsEditorLocalization.DrawLanguageSelector();
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(L("表面植生の配置", "Surface Growth Placement"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                L(
                    "Collider と成長タイプを選び、編集用オブジェクトを作成します。ガイド点は作成後も Scene View で編集できます。",
                    "Select a Collider, choose the growth type, then create an authoring object. " +
                    "Guide points remain editable in the Scene view."),
                MessageType.Info);

            kind = (SurfaceGrowthPlacementKind)SabaPropsEditorLocalization.Popup(
                "種類",
                "Type",
                (int)kind,
                new[] { "表面ツタ", "根茎パッチ" },
                new[] { "Surface Vine", "Rhizome Patch" });
            if (kind == SurfaceGrowthPlacementKind.SurfaceVine)
            {
                vinePreset = (SurfaceVinePlacementPreset)SabaPropsEditorLocalization.Popup(
                    "植物プリセット",
                    "Botanical Preset",
                    (int)vinePreset,
                    new[] { "オオイタビ", "セイヨウキヅタ", "アメリカヅタ" },
                    new[] { "Creeping Fig", "English Ivy", "Boston Ivy" });
                direction = (SurfaceGrowthPlacementDirection)SabaPropsEditorLocalization.Popup(
                    "初期成長方向",
                    "Initial Direction",
                    (int)direction,
                    new[] { "上", "下", "右", "左", "カスタム" },
                    new[] { "World Up", "World Down", "World Right", "World Left", "Custom" });
                if (direction == SurfaceGrowthPlacementDirection.Custom)
                {
                    customDirection = EditorGUILayout.Vector3Field(
                        L("ワールド方向", "World Direction"), customDirection);
                }
                guideLength = Mathf.Max(
                    0.1f, EditorGUILayout.FloatField(L("ガイド長 (m)", "Guide Length (m)"), guideLength));
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(L("対象面", "Surfaces"), EditorStyles.boldLabel);
            Collider nextTarget = EditorGUILayout.ObjectField(
                L("主 Collider", "Primary Collider"), targetSurface, typeof(Collider), true) as Collider;
            if (nextTarget != targetSurface)
            {
                targetSurface = nextTarget;
                SuggestStart();
            }

            for (int i = 0; i < additionalSurfaces.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    additionalSurfaces[i] = EditorGUILayout.ObjectField(
                        L("隣接面 ", "Adjacent ") + (i + 1),
                        additionalSurfaces[i],
                        typeof(Collider),
                        true) as Collider;
                    if (GUILayout.Button("-", GUILayout.Width(24f)))
                    {
                        additionalSurfaces.RemoveAt(i);
                        break;
                    }
                }
            }
            if (GUILayout.Button(L("隣接 Collider を追加", "Add Adjacent Collider")))
            {
                additionalSurfaces.Add(null);
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(L("配置", "Placement"), EditorStyles.boldLabel);
            hierarchyParent = EditorGUILayout.ObjectField(
                L("Hierarchy 親", "Hierarchy Parent"), hierarchyParent, typeof(GameObject), true) as GameObject;
            worldStart = EditorGUILayout.Vector3Field(L("開始位置（ワールド）", "World Start"), worldStart);
            if (GUILayout.Button(L("Scene View / 表面位置を使用", "Use Scene View / Surface Point")))
            {
                SuggestStart();
            }
            material = EditorGUILayout.ObjectField(
                L("マテリアル", "Material"), material, typeof(Material), false) as Material;
            buildImmediately = EditorGUILayout.Toggle(L("作成時にビルド", "Build Immediately"), buildImmediately);

            if (hierarchyParent != null && !ApproximatelyOne(
                hierarchyParent.transform.lossyScale))
            {
                EditorGUILayout.HelpBox(
                    L(
                        "拡大縮小された親はメートル単位の成長値へ影響します。Scale 1 の整理用オブジェクトを推奨します。",
                        "A scaled parent changes metre-based growth values. Prefer an unscaled " +
                        "organizer object."),
                    MessageType.Warning);
            }
            if (!hasSuggestedStart)
            {
                EditorGUILayout.HelpBox(
                    L(
                        "作成前に Scene View から開始位置を設定してください。",
                        "Set the start point from the Scene view before creating the object."),
                    MessageType.Warning);
            }

            EditorGUILayout.EndScrollView();
            using (new EditorGUI.DisabledScope(targetSurface == null))
            {
                if (GUILayout.Button(L("シーンに作成", "Create in Scene"), GUILayout.Height(30f)))
                {
                    Create();
                }
            }
        }

        private void SuggestStart()
        {
            worldStart = SurfaceGrowthPlacementUtility.SuggestStartPoint(targetSurface);
            hasSuggestedStart = targetSurface != null;
        }

        private void Create()
        {
            if (kind == SurfaceGrowthPlacementKind.RhizomePatch)
            {
                SurfaceGrowthPlacementUtility.CreateRhizomePatch(
                    targetSurface,
                    additionalSurfaces,
                    material,
                    worldStart,
                    hierarchyParent,
                    buildImmediately);
                return;
            }

            SurfaceGrowthPlacementUtility.CreateVine(
                targetSurface,
                additionalSurfaces,
                material,
                worldStart,
                DirectionVector(),
                guideLength,
                vinePreset,
                hierarchyParent,
                buildImmediately);
        }

        private Vector3 DirectionVector()
        {
            switch (direction)
            {
                case SurfaceGrowthPlacementDirection.WorldDown:
                    return Vector3.down;
                case SurfaceGrowthPlacementDirection.WorldRight:
                    return Vector3.right;
                case SurfaceGrowthPlacementDirection.WorldLeft:
                    return Vector3.left;
                case SurfaceGrowthPlacementDirection.Custom:
                    return customDirection;
                default:
                    return Vector3.up;
            }
        }

        private static bool ApproximatelyOne(Vector3 value)
        {
            return Mathf.Abs(value.x - 1f) < 0.001f
                && Mathf.Abs(value.y - 1f) < 0.001f
                && Mathf.Abs(value.z - 1f) < 0.001f;
        }

        private static string L(string japanese, string english)
        {
            return SabaPropsEditorLocalization.Text(japanese, english);
        }
    }
}
