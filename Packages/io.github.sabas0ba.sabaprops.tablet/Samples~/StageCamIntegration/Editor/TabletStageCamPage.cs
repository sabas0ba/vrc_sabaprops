using SabaProps.StageCam;
using SabaProps.StageCam.Editors;
using SabaProps.Tablet.Authoring;
using SabaProps.Tablet.Editors;
using TMPro;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

namespace SabaProps.Tablet.Samples.Editors
{
    /// <summary>
    /// タブレットから Stage Cam を操作するページの実装例。
    /// <para>
    /// ボタンは TabletEntryKind.CustomEvent で StageCamControlPanel のイベントを直接呼びます。
    /// 独自の UI を持つ既存のギミックも、同じように「呼び出し先とイベント名」を登録するだけで操作できます。
    /// 映像と追従対象の表示は、TabletBuilder.Built で生成後のページに加えます。
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class TabletStageCamPage
    {
        public const string PageTitle = "Stage Cam";
        private const string DisplayName = "Stage Cam Display";

        private static readonly string[,] Buttons =
        {
            { "Cam <", "_PreviousCamera" },
            { "Cam >", "_NextCamera" },
            { "Track me", "_TargetSelf" },
            { "Stop", "_StopFollowing" },
            { "Player <", "_PreviousPlayer" },
            { "Player >", "_NextPlayer" },
            { "Track", "_ApplyPlayer" },
            { "Auto move", "_ToggleCameraWork" },
        };

        static TabletStageCamPage()
        {
            TabletBuilder.Built += AddDisplay;
        }

        [MenuItem("Tools/SabaProps/Tablet/Samples/Add Stage Cam Page", false, 20)]
        public static void AddPageFromMenu()
        {
            TabletDefinition definition = Object.FindObjectOfType<TabletDefinition>();
            if (definition == null)
            {
                EditorUtility.DisplayDialog("Stage Cam Page", "シーンに TabletDefinition がありません。", "OK");
                return;
            }

            AddPage(definition, FindOrCreatePanel());
            TabletBuilder.Build(definition);
            Selection.activeGameObject = definition.gameObject;
        }

        /// <summary>Stage Cam ページを定義へ追加します。既にある場合は項目を作り直します。</summary>
        public static void AddPage(TabletDefinition definition, StageCamControlPanel panel)
        {
            Undo.RecordObject(definition, "Add Stage Cam Page");
            TabletPage page = definition.FindOrAddPage(PageTitle);
            page.entries.Clear();
            for (int i = 0; i < Buttons.GetLength(0); i++)
            {
                page.entries.Add(new TabletEntry
                {
                    label = Buttons[i, 0],
                    kind = TabletEntryKind.CustomEvent,
                    target = panel,
                    eventName = Buttons[i, 1],
                });
            }

            EditorUtility.SetDirty(definition);
        }

        /// <summary>
        /// シーンの StageCamControlPanel を返します。無ければ全リグを登録したパネルを作ります。
        /// パネルは選択中のカメラとプレイヤーを保持するため、タブレットだけで操作する場合も必要です。
        /// </summary>
        public static StageCamControlPanel FindOrCreatePanel()
        {
            StageCamControlPanel panel = Object.FindObjectOfType<StageCamControlPanel>();
            if (panel != null)
            {
                return panel;
            }

            panel = StageCamControlPanelBuilder.Create(Object.FindObjectsOfType<StageCamRig>());
            Undo.RegisterCreatedObjectUndo(panel.gameObject, "Create Stage Camera Control Panel");
            return panel;
        }

        private static void AddDisplay(TabletBuildResult result)
        {
            GameObject page = result.FindPage(PageTitle);
            StageCamControlPanel panel = FindPanel(result.Definition);
            if (page == null || panel == null)
            {
                return;
            }

            // 8 個のボタンの下の行に、映像と追従対象を並べます。
            int row = (Buttons.GetLength(0) + result.Columns - 1) / result.Columns;
            if (row >= result.Rows)
            {
                return;
            }

            Vector2 left = result.CellCenter(row * result.Columns);
            Vector2 right = result.CellCenter(row * result.Columns + result.Columns - 1);
            float height = result.CellSize.y;
            float previewWidth = Mathf.Min(height * 16f / 9f, (right.x - left.x) * 0.5f);
            float previewX = left.x - result.CellSize.x * 0.5f + previewWidth * 0.5f;

            var root = new GameObject(DisplayName);
            root.transform.SetParent(page.transform, false);

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = "Preview";
            quad.transform.SetParent(root.transform, false);
            quad.transform.localPosition = new Vector3(previewX, left.y, result.Surface - 0.0004f);
            quad.transform.localScale = new Vector3(previewWidth, height, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Unlit/Texture"));

            float textLeft = previewX + previewWidth * 0.5f + result.Spacing;
            float textRight = right.x + result.CellSize.x * 0.5f;
            var textSize = new Vector2(Mathf.Max(0.01f, textRight - textLeft), height * 0.45f);
            float textX = (textLeft + textRight) * 0.5f;

            TabletStageCamDisplay display = root.AddUdonSharpComponent<TabletStageCamDisplay>();
            display.panel = panel;
            display.preview = renderer;
            display.cameraLabel = result.CreateLabel(root.transform, "Camera", "", new Vector3(textX, left.y + height * 0.25f,
                result.Surface - 0.0004f), textSize, TextAlignmentOptions.Left);
            display.targetLabel = result.CreateLabel(root.transform, "Target", "", new Vector3(textX, left.y - height * 0.25f,
                result.Surface - 0.0004f), textSize, TextAlignmentOptions.Left);
            UdonSharpEditorUtility.CopyProxyToUdon(display);
            EditorUtility.SetDirty(display);
        }

        private static StageCamControlPanel FindPanel(TabletDefinition definition)
        {
            foreach (TabletPage page in definition.pages)
            {
                if (page == null || page.title != PageTitle)
                {
                    continue;
                }

                foreach (TabletEntry entry in page.entries)
                {
                    if (entry != null && entry.target is StageCamControlPanel panel)
                    {
                        return panel;
                    }
                }
            }

            return null;
        }
    }
}
