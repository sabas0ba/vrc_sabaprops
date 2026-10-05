using System.Collections.Generic;
using SabaProps.Tablet.Authoring;
using UdonSharp;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Components;

namespace SabaProps.Tablet.Editors
{
    /// <summary>
    /// ミラー、コライダー、テレポート地点、任意のイベントをタブレットへ登録するウィンドウ。
    /// 頻繁に追加・変更する項目をシーンから拾って登録し、Build までを 1 か所で行います。
    /// 項目の細かい設定は TabletDefinition の Inspector で編集します。
    /// </summary>
    public class TabletSetupWindow : EditorWindow
    {
        public const string MirrorPage = "Mirror";
        public const string ColliderPage = "Collider";
        public const string ObjectPage = "Objects";
        public const string TeleportPage = "Teleport";
        public const string CustomPage = "Custom";
        public const string MirrorGroup = "Mirror";

        private TabletDefinition definition;
        private Vector2 scroll;
        private UdonSharpBehaviour customTarget;
        private int customEvent;
        private string customLabel = "";

        [MenuItem("Window/SabaProps/Tablet Setup", false, 2100)]
        public static void Open()
        {
            Open(null);
        }

        /// <summary>ウィンドウを開きます。target が null なら前回の対象を保ちます。</summary>
        public static void Open(TabletDefinition target)
        {
            var window = GetWindow<TabletSetupWindow>(false, "Tablet Setup", true);
            window.minSize = new Vector2(360f, 420f);
            if (target != null)
            {
                window.definition = target;
            }

            window.Show();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawTarget();
            if (definition != null)
            {
                EditorGUILayout.Space();
                DrawMirrors();
                EditorGUILayout.Space();
                DrawSelection();
                EditorGUILayout.Space();
                DrawCustomEvent();
                EditorGUILayout.Space();
                DrawPages();
                EditorGUILayout.Space();
                DrawBuild();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawTarget()
        {
            EditorGUILayout.LabelField("タブレット", EditorStyles.boldLabel);
            definition = (TabletDefinition)EditorGUILayout.ObjectField("Definition", definition, typeof(TabletDefinition), true);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("シーンから探す"))
                {
                    definition = Object.FindObjectOfType<TabletDefinition>();
                }

                if (GUILayout.Button("新規作成"))
                {
                    definition = TabletMenu.CreateDefinition(null);
                }
            }

            if (definition == null)
            {
                EditorGUILayout.HelpBox("TabletDefinition を選択するか、新規作成してください。", MessageType.Info);
                return;
            }

            TabletTheme theme = (TabletTheme)EditorGUILayout.ObjectField("Theme", definition.theme, typeof(TabletTheme), false);
            if (theme != definition.theme)
            {
                Undo.RecordObject(definition, "Change Tablet Theme");
                definition.theme = theme;
                EditorUtility.SetDirty(definition);
            }

            if (definition.theme == null && GUILayout.Button("Theme を作成"))
            {
                Undo.RecordObject(definition, "Create Tablet Theme");
                definition.theme = TabletMenu.CreateThemeAsset();
                EditorUtility.SetDirty(definition);
            }
        }

        // ------------------------------------------------------------------
        // ミラー
        // ------------------------------------------------------------------

        private void DrawMirrors()
        {
            EditorGUILayout.LabelField("ミラー", EditorStyles.boldLabel);
            VRCMirrorReflection[] mirrors = Object.FindObjectsOfType<VRCMirrorReflection>(true);
            if (mirrors.Length == 0)
            {
                EditorGUILayout.HelpBox("シーンに VRCMirrorReflection がありません。", MessageType.None);
                return;
            }

            var missing = new List<GameObject>();
            foreach (VRCMirrorReflection mirror in mirrors)
            {
                GameObject go = mirror.gameObject;
                bool registered = definition.ContainsToggleTarget(go);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(go.name, registered ? "登録済み" : "未登録");
                    using (new EditorGUI.DisabledScope(registered))
                    {
                        if (GUILayout.Button("追加", GUILayout.Width(60f)))
                        {
                            AddMirrors(definition, new[] { go });
                        }
                    }
                }

                if (!registered)
                {
                    missing.Add(go);
                }
            }

            using (new EditorGUI.DisabledScope(missing.Count == 0))
            {
                if (GUILayout.Button("未登録のミラーをすべて追加"))
                {
                    AddMirrors(definition, missing.ToArray());
                }
            }
        }

        /// <summary>
        /// ミラーを Mirror ページへ追加します。同じグループに入れるため、1 つを ON にすると他は OFF になります。
        /// </summary>
        public static void AddMirrors(TabletDefinition target, GameObject[] mirrors)
        {
            Undo.RecordObject(target, "Add Mirrors");
            TabletPage page = target.FindOrAddPage(MirrorPage);
            foreach (GameObject mirror in mirrors)
            {
                if (mirror == null || target.ContainsToggleTarget(mirror))
                {
                    continue;
                }

                page.entries.Add(new TabletEntry
                {
                    label = mirror.name,
                    kind = TabletEntryKind.Toggle,
                    objects = new[] { mirror },
                    startOn = mirror.activeSelf,
                    exclusiveGroup = MirrorGroup,
                });
            }

            EditorUtility.SetDirty(target);
        }

        // ------------------------------------------------------------------
        // 選択中のオブジェクト
        // ------------------------------------------------------------------

        private void DrawSelection()
        {
            EditorGUILayout.LabelField("選択中のオブジェクト", EditorStyles.boldLabel);
            GameObject[] selected = Selection.gameObjects;
            EditorGUILayout.LabelField(selected.Length + " 個選択中");

            using (new EditorGUI.DisabledScope(selected.Length == 0))
            {
                if (GUILayout.Button("Collider のトグルとして追加"))
                {
                    AddColliders(definition, selected);
                }

                if (GUILayout.Button("GameObject のトグルとして追加"))
                {
                    AddObjects(definition, selected);
                }

                if (GUILayout.Button("テレポート地点として追加"))
                {
                    AddDestinations(definition, selected);
                }
            }
        }

        /// <summary>各オブジェクトの Collider をまとめて 1 項目として Collider ページへ追加します。</summary>
        public static void AddColliders(TabletDefinition target, GameObject[] objects)
        {
            Undo.RecordObject(target, "Add Collider Toggles");
            TabletPage page = target.FindOrAddPage(ColliderPage);
            foreach (GameObject go in objects)
            {
                if (go == null || target.ContainsToggleTarget(go))
                {
                    continue;
                }

                Collider[] colliders = go.GetComponents<Collider>();
                if (colliders.Length == 0)
                {
                    Debug.LogWarning("[SabaProps Tablet] " + go.name + " に Collider がないため追加しませんでした。", go);
                    continue;
                }

                page.entries.Add(new TabletEntry
                {
                    label = go.name,
                    kind = TabletEntryKind.Toggle,
                    colliders = colliders,
                    startOn = colliders[0].enabled,
                });
            }

            EditorUtility.SetDirty(target);
        }

        public static void AddObjects(TabletDefinition target, GameObject[] objects)
        {
            Undo.RecordObject(target, "Add Object Toggles");
            TabletPage page = target.FindOrAddPage(ObjectPage);
            foreach (GameObject go in objects)
            {
                if (go == null || target.ContainsToggleTarget(go))
                {
                    continue;
                }

                page.entries.Add(new TabletEntry
                {
                    label = go.name,
                    kind = TabletEntryKind.Toggle,
                    objects = new[] { go },
                    startOn = go.activeSelf,
                });
            }

            EditorUtility.SetDirty(target);
        }

        public static void AddDestinations(TabletDefinition target, GameObject[] objects)
        {
            Undo.RecordObject(target, "Add Teleport Destinations");
            TabletPage page = target.FindOrAddPage(TeleportPage);
            foreach (GameObject go in objects)
            {
                if (go == null)
                {
                    continue;
                }

                page.entries.Add(new TabletEntry
                {
                    label = go.name,
                    kind = TabletEntryKind.Teleport,
                    destination = go.transform,
                });
            }

            EditorUtility.SetDirty(target);
        }

        // ------------------------------------------------------------------
        // 任意のイベント
        // ------------------------------------------------------------------

        private void DrawCustomEvent()
        {
            EditorGUILayout.LabelField("任意の UdonSharpBehaviour のイベント", EditorStyles.boldLabel);
            customTarget = (UdonSharpBehaviour)EditorGUILayout.ObjectField("呼び出し先", customTarget, typeof(UdonSharpBehaviour), true);
            string[] events = customTarget == null ? new string[0] : TabletValidation.ListEvents(customTarget.GetType());
            if (customTarget != null && events.Length == 0)
            {
                EditorGUILayout.HelpBox("引数なしの public メソッドがありません。", MessageType.Warning);
                return;
            }

            customEvent = EditorGUILayout.Popup("イベント", Mathf.Clamp(customEvent, 0, Mathf.Max(0, events.Length - 1)), events);
            customLabel = EditorGUILayout.TextField("表示名", customLabel);

            using (new EditorGUI.DisabledScope(customTarget == null || events.Length == 0))
            {
                if (GUILayout.Button("Custom ページへ追加"))
                {
                    AddCustomEvent(definition, customTarget, events[customEvent],
                        string.IsNullOrEmpty(customLabel) ? events[customEvent] : customLabel);
                }
            }
        }

        public static void AddCustomEvent(TabletDefinition target, UdonSharpBehaviour behaviour, string eventName, string label)
        {
            Undo.RecordObject(target, "Add Custom Event");
            target.FindOrAddPage(CustomPage).entries.Add(new TabletEntry
            {
                label = label,
                kind = TabletEntryKind.CustomEvent,
                target = behaviour,
                eventName = eventName,
            });
            EditorUtility.SetDirty(target);
        }

        // ------------------------------------------------------------------
        // ページ一覧と Build
        // ------------------------------------------------------------------

        private void DrawPages()
        {
            EditorGUILayout.LabelField("ページ", EditorStyles.boldLabel);
            for (int p = 0; p < definition.pages.Count; p++)
            {
                TabletPage page = definition.pages[p];
                if (page == null)
                {
                    continue;
                }

                EditorGUILayout.LabelField(p + ": " + page.title, page.entries.Count + " 項目");
                using (new EditorGUI.IndentLevelScope())
                {
                    for (int e = 0; e < page.entries.Count; e++)
                    {
                        TabletEntry entry = page.entries[e];
                        if (entry == null)
                        {
                            continue;
                        }

                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(entry.label, entry.kind.ToString());
                            if (GUILayout.Button("削除", GUILayout.Width(48f)))
                            {
                                Undo.RecordObject(definition, "Remove Tablet Entry");
                                page.entries.RemoveAt(e);
                                EditorUtility.SetDirty(definition);
                                GUIUtility.ExitGUI();
                            }
                        }
                    }
                }
            }
        }

        private void DrawBuild()
        {
            foreach (TabletIssue issue in TabletValidation.Validate(definition))
            {
                EditorGUILayout.HelpBox(issue.message, issue.type);
            }

            if (GUILayout.Button("Build", GUILayout.Height(32f)))
            {
                TabletBuilder.Build(definition);
            }

            if (GUILayout.Button("Inspector で詳細を編集"))
            {
                Selection.activeGameObject = definition.gameObject;
            }
        }
    }
}
