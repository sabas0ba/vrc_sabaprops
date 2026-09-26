using System.Collections.Generic;
using SabaProps.Tablet.Authoring;
using TMPro;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Components;

namespace SabaProps.Tablet.Editors
{
    /// <summary>
    /// TabletDefinition と TabletTheme から、本体・ボタン・モジュール・召喚トリガを生成します。
    /// <para>
    /// 生成物は定義の子の Body、Modules、Triggers にまとめ、Build のたびに作り直します。
    /// これらの子に手作業で加えた変更は次の Build で失われます。
    /// </para>
    /// </summary>
    public static class TabletBuilder
    {
        public const string BodyName = "Body";
        public const string ModulesName = "Modules";
        public const string TriggersName = "Triggers";
        public const string HandleName = "Handle";

        private const int CornerSegments = 6;

        /// <summary>表面から手前へ重ねる要素の間隔 (m)。z-fighting を避けるための値です。</summary>
        private const float Layer = 0.0004f;

        /// <summary>
        /// Build の完了直前に呼ばれます。生成したページへ独自の表示を加える拡張に使います。
        /// ここで加えたオブジェクトも次の Build で作り直されるため、毎回この通知で加えます。
        /// </summary>
        public static event System.Action<TabletBuildResult> Built;

        public static TabletController Build(TabletDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            bool temporaryTheme = definition.theme == null;
            TabletTheme theme = temporaryTheme ? ScriptableObject.CreateInstance<TabletTheme>() : definition.theme;
            try
            {
                return BuildWithTheme(definition, theme);
            }
            finally
            {
                if (temporaryTheme)
                {
                    Object.DestroyImmediate(theme);
                }
            }
        }

        private static TabletController BuildWithTheme(TabletDefinition definition, TabletTheme theme)
        {
            Undo.RecordObject(definition, "Build Tablet");
            Transform root = definition.transform;
            RemoveGenerated(root);

            var context = new Context(definition, theme);
            context.CreateAssets();

            TabletController controller = root.GetComponent<TabletController>();
            if (controller == null)
            {
                controller = root.gameObject.AddUdonSharpComponent<TabletController>();
            }

            context.controller = controller;

            GameObject body = Child(root, BodyName);
            context.body = body.transform;
            var modules = Child(root, ModulesName).transform;
            context.modules = modules;

            MeshChild(body, "Housing", context.bodyMesh, context.bodyMaterial, Vector3.zero);
            MeshChild(body, "Screen", context.screenMesh, context.screenMaterial,
                new Vector3(0f, 0f, context.Front - Layer));
            TabletThemeDecorations.Build(body.transform, theme, definition.generatedFolder);

            BuildHeader(context);
            BuildPages(context);
            VRCPickup handle = theme.handle ? BuildHandle(context) : null;

            controller.body = body.transform;
            controller.handle = handle;
            controller.buttons = context.buttons.ToArray();
            controller.pages = context.pages.ToArray();
            controller.pageTitles = context.pageTitles.ToArray();
            controller.titleLabel = context.titleLabel;
            controller.pageLabel = context.pageLabel;
            controller.startVisible = definition.startVisible;

            LinkExclusiveGroups(context);
            BuildTriggers(context, root);

            // Editor では最初のページだけを表示します。実行時は TabletController が決めます。
            for (int i = 0; i < context.pages.Count; i++)
            {
                context.pages[i].SetActive(i == 0);
            }

            if (context.titleLabel != null && context.pageTitles.Count > 0)
            {
                context.titleLabel.text = context.pageTitles[0];
            }

            foreach (UdonSharpBehaviour behaviour in context.behaviours)
            {
                UdonSharpEditorUtility.CopyProxyToUdon(behaviour);
                EditorUtility.SetDirty(behaviour);
            }

            UdonSharpEditorUtility.CopyProxyToUdon(controller);
            EditorUtility.SetDirty(controller);

            if (Built != null)
            {
                Built(new TabletBuildResult(context));
            }

            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(definition.gameObject.scene);
            return controller;
        }

        private static void RemoveGenerated(Transform root)
        {
            foreach (string name in new[] { BodyName, ModulesName, TriggersName })
            {
                Transform existing = root.Find(name);
                while (existing != null)
                {
                    Undo.DestroyObjectImmediate(existing.gameObject);
                    existing = root.Find(name);
                }
            }
        }

        // ------------------------------------------------------------------
        // ヘッダー
        // ------------------------------------------------------------------

        private static void BuildHeader(Context context)
        {
            TabletTheme theme = context.theme;
            Vector2 center = TabletLayout.HeaderCenter(context.screenSize, theme.headerHeight);
            float height = Mathf.Max(0.005f, theme.headerHeight - theme.spacing);
            float width = height * 1.8f;
            float right = context.screenSize.x * 0.5f - theme.spacing - width * 0.5f;

            var header = Child(context.body, "Header").transform;
            var size = new Vector2(width, height);
            TabletController controller = context.controller;

            Button(context, header, "Close", "Close", null, new Vector2(right, center.y), size,
                context.headerMesh, context.headerMaterial, controller, "_Stow", false, 0);
            Button(context, header, "Next page", "Next", null, new Vector2(right - (width + theme.spacing), center.y), size,
                context.headerMesh, context.headerMaterial, controller, "_NextPage", false, 0);
            Button(context, header, "Previous page", "Prev", null, new Vector2(right - 2f * (width + theme.spacing), center.y), size,
                context.headerMesh, context.headerMaterial, controller, "_PreviousPage", false, 0);

            float pageWidth = width * 1.2f;
            float pageX = right - 3f * (width + theme.spacing) - pageWidth * 0.5f + width * 0.5f;
            context.pageLabel = Label(context, header, "Page", "", new Vector3(pageX, center.y, context.Surface - Layer),
                new Vector2(pageWidth, height), TextAlignmentOptions.Center);

            float left = -context.screenSize.x * 0.5f + theme.spacing;
            float titleRight = pageX - pageWidth * 0.5f - theme.spacing;
            context.titleLabel = Label(context, header, "Title", "",
                new Vector3((left + titleRight) * 0.5f, center.y, context.Surface - Layer),
                new Vector2(Mathf.Max(0.01f, titleRight - left), height), TextAlignmentOptions.Left);
        }

        // ------------------------------------------------------------------
        // ページとボタン
        // ------------------------------------------------------------------

        private static void BuildPages(Context context)
        {
            TabletDefinition definition = context.definition;
            TabletTheme theme = context.theme;
            int cells = TabletLayout.CellsPerPage(theme.columns, theme.rows);

            // PageLink が指す生成後のページ番号を先に決めます。
            var firstPage = new int[definition.pages.Count];
            int generated = 0;
            for (int i = 0; i < definition.pages.Count; i++)
            {
                firstPage[i] = generated;
                TabletPage page = definition.pages[i];
                generated += TabletLayout.PageCount(page == null ? 0 : page.entries.Count, cells);
            }

            var pagesRoot = Child(context.body, "Pages").transform;
            for (int i = 0; i < definition.pages.Count; i++)
            {
                TabletPage page = definition.pages[i];
                List<TabletEntry> entries = page == null ? new List<TabletEntry>() : page.entries;
                string title = page == null || string.IsNullOrEmpty(page.title) ? "Page " + (i + 1) : page.title;
                int count = TabletLayout.PageCount(entries.Count, cells);

                for (int split = 0; split < count; split++)
                {
                    string splitTitle = count > 1 ? title + " (" + (split + 1) + "/" + count + ")" : title;
                    GameObject pageObject = Child(pagesRoot, splitTitle);
                    context.pages.Add(pageObject);
                    context.pageTitles.Add(splitTitle);

                    if (page != null && page.bedDiagram) BuildBedDiagram(context, pageObject.transform);

                    int end = Mathf.Min(entries.Count, (split + 1) * cells);
                    for (int e = split * cells; e < end; e++)
                    {
                        if (entries[e] != null)
                        {
                            BuildEntry(context, pageObject.transform, entries[e], e - split * cells, firstPage);
                        }
                    }
                }
            }

            if (definition.includePlayerPage)
            {
                BuildPlayerPage(context, pagesRoot);
            }
        }

        private static void BuildEntry(Context context, Transform page, TabletEntry entry, int cell, int[] firstPage)
        {
            TabletTheme theme = context.theme;
            Vector2 center = TabletLayout.CellCenter(cell, context.gridSize, context.gridCenter, theme.columns, theme.rows, theme.spacing);
            Vector2 size = context.cellSize;
            if (entry.customPlacement)
            {
                center = context.gridCenter + Vector2.Scale(entry.normalizedCenter, context.gridSize);
                size = Vector2.Scale(entry.normalizedSize, context.gridSize);
            }
            string label = string.IsNullOrEmpty(entry.label) ? entry.kind.ToString() : entry.label;

            context.entrySize = size;

            switch (entry.kind)
            {
                case TabletEntryKind.Toggle:
                {
                    var module = new GameObject("Toggle - " + label);
                    module.transform.SetParent(context.modules, false);
                    TabletToggle toggle = module.AddUdonSharpComponent<TabletToggle>();
                    toggle.objects = NonNull(entry.objects);
                    toggle.invertedObjects = NonNull(entry.invertedObjects);
                    toggle.colliders = NonNull(entry.colliders);
                    toggle.behaviours = NonNull(entry.behaviours);
                    toggle.isOn = entry.startOn;
                    toggle.global = entry.global;
                    context.behaviours.Add(toggle);

                    TabletButton button = CellButton(context, page, label, entry.icon, center, toggle, "_Toggle", false, 0);
                    toggle.buttons = new[] { button };

                    if (!string.IsNullOrEmpty(entry.exclusiveGroup))
                    {
                        if (!context.groups.TryGetValue(entry.exclusiveGroup, out List<TabletToggle> members))
                        {
                            members = new List<TabletToggle>();
                            context.groups.Add(entry.exclusiveGroup, members);
                        }

                        members.Add(toggle);
                    }

                    break;
                }

                case TabletEntryKind.Teleport:
                {
                    TabletTeleport teleport = context.Teleport();
                    int index = context.destinations.Count;
                    context.destinations.Add(entry.destination);
                    teleport.destinations = context.destinations.ToArray();
                    CellButton(context, page, label, entry.icon, center, teleport, "_TeleportToDestination", true, index);
                    break;
                }

                case TabletEntryKind.CustomEvent:
                {
                    TabletButton button = CellButton(context, page, label, entry.icon, center, entry.target, entry.eventName, entry.useArgument, entry.argument);
                    if (entry.target is TabletPostEffects effects && entry.eventName == "_Toggle")
                    {
                        var indicators = new List<TabletButton>();
                        if (effects.buttons != null)
                            foreach (TabletButton existing in effects.buttons)
                                if (existing != null) indicators.Add(existing);
                        indicators.Add(button);
                        effects.buttons = indicators.ToArray();
                        UdonSharpEditorUtility.CopyProxyToUdon(effects);
                        EditorUtility.SetDirty(effects);
                    }
                    break;
                }

                case TabletEntryKind.Slider:
                    BuildSlider(context, page, label, center, size, entry);
                    break;

                case TabletEntryKind.PageLink:
                {
                    int target = entry.pageIndex >= 0 && entry.pageIndex < firstPage.Length ? firstPage[entry.pageIndex] : 0;
                    CellButton(context, page, label, entry.icon, center, context.controller, "_ShowPage", true, target);
                    break;
                }
            }
        }

        private static void BuildPlayerPage(Context context, Transform pagesRoot)
        {
            TabletTheme theme = context.theme;
            const string title = "Players";
            GameObject page = Child(pagesRoot, title);
            context.pages.Add(page);
            context.pageTitles.Add(title);

            TabletTeleport teleport = context.Teleport();
            int rows = Mathf.Max(2, theme.rows);
            Vector2 first = TabletLayout.CellCenter(0, context.gridSize, context.gridCenter, 1, rows, theme.spacing);
            Vector2 row = TabletLayout.CellSize(context.gridSize, 1, rows, theme.spacing);
            teleport.playerLabel = Label(context, page.transform, "Selected player", "-",
                new Vector3(first.x, first.y, context.Surface - Layer), new Vector2(row.x, row.y * 0.8f), TextAlignmentOptions.Center);

            Vector2 cell = TabletLayout.CellSize(context.gridSize, 3, rows, theme.spacing);
            string[] captions = { "Prev", "Teleport", "Next" };
            string[] events = { "_PreviousPlayer", "_TeleportToSelectedPlayer", "_NextPlayer" };
            for (int i = 0; i < 3; i++)
            {
                Vector2 center = TabletLayout.CellCenter(3 + i, context.gridSize, context.gridCenter, 3, rows, theme.spacing);
                Button(context, page.transform, captions[i], captions[i], null, center, cell,
                    context.capMesh3, context.buttonMaterial, teleport, events[i], false, 0);
            }
        }

        private static TabletButton CellButton(Context context, Transform parent, string label, Texture2D icon, Vector2 center,
            UdonSharpBehaviour target, string eventName, bool useArgument, int argument)
        {
            return Button(context, parent, label, label, icon, center, context.entrySize, context.SizedCap(context.entrySize),
                context.buttonMaterial, target, eventName, useArgument, argument);
        }

        private static void BuildBedDiagram(Context context, Transform page)
        {
            Vector2 size = Vector2.Scale(context.gridSize, new Vector2(0.24f, 0.62f));
            GameObject bed = MeshChild(page.gameObject, "Bed diagram", context.SizedCap(size),
                context.headerMaterial, new Vector3(0f, context.gridCenter.y, context.Surface));
            Label(context, bed.transform, "Bed", "BED", new Vector3(0f, -size.y * 0.1f, -context.theme.buttonHeight),
                new Vector2(size.x * 0.9f, size.y * 0.25f), TextAlignmentOptions.Center);
            MeshChild(bed, "Pillow", context.SizedCap(new Vector2(size.x * 0.75f, size.y * 0.15f)),
                context.buttonMaterial, new Vector3(0f, size.y * 0.3f, -context.theme.buttonHeight));
        }

        private static void BuildSlider(Context context, Transform page, string label, Vector2 center, Vector2 size, TabletEntry entry)
        {
            GameObject root = Child(page, "Slider - " + label);
            root.transform.localPosition = new Vector3(center.x, center.y, context.Surface);
            TabletSlider slider = root.AddUdonSharpComponent<TabletSlider>();
            slider.target = entry.target;
            slider.eventName = entry.eventName;
            slider.minimum = entry.minimum;
            slider.maximum = entry.maximum;
            slider.value = entry.initialValue;
            float width = size.x * 0.56f;
            float y = -size.y * 0.16f;
            var zone = root.AddComponent<BoxCollider>();
            zone.isTrigger = true;
            zone.center = new Vector3(0f, y, -context.theme.buttonHeight - context.theme.pokeDepth * 0.5f);
            zone.size = new Vector3(width, size.y * 0.45f, context.theme.pokeDepth);
            slider.pressZone = zone;
            MeshChild(root, "Track", context.SizedCap(new Vector2(width, size.y * 0.08f)),
                context.headerMaterial, new Vector3(0f, y, -context.theme.buttonHeight * 0.5f));
            slider.thumb = MeshChild(root, "Thumb", context.SizedCap(new Vector2(size.x * 0.035f, size.y * 0.4f)),
                context.buttonActiveMaterial, new Vector3((Mathf.InverseLerp(entry.minimum, entry.maximum, entry.initialValue) - 0.5f) * width,
                y, -context.theme.buttonHeight)).transform;
            Label(context, root.transform, "Caption", label, new Vector3(-size.x * 0.1f, size.y * 0.28f, -context.theme.buttonHeight),
                new Vector2(size.x * 0.6f, size.y * 0.35f), TextAlignmentOptions.Left);
            slider.valueLabel = Label(context, root.transform, "Value", entry.initialValue.ToString("0.00"),
                new Vector3(size.x * 0.32f, size.y * 0.28f, -context.theme.buttonHeight),
                new Vector2(size.x * 0.2f, size.y * 0.35f), TextAlignmentOptions.Right);
            Vector2 buttonSize = new Vector2(size.x * 0.1f, size.y * 0.48f);
            Button(context, root.transform, "Decrease", "-", null, new Vector2(-size.x * 0.4f, y), buttonSize,
                context.SizedCap(buttonSize), context.buttonMaterial, slider, "_Decrease", false, 0);
            Button(context, root.transform, "Increase", "+", null, new Vector2(size.x * 0.4f, y), buttonSize,
                context.SizedCap(buttonSize), context.buttonMaterial, slider, "_Increase", false, 0);
            context.behaviours.Add(slider);
        }

        private static TabletButton Button(Context context, Transform parent, string name, string caption, Texture2D icon,
            Vector2 center, Vector2 size, Mesh capMesh, Material normal, UdonSharpBehaviour target, string eventName,
            bool useArgument, int argument)
        {
            TabletTheme theme = context.theme;
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(center.x, center.y, context.Surface);

            // 押下判定の領域はキャップの前方に置きます。Interact もこの Collider で受けます。
            var zone = root.AddComponent<BoxCollider>();
            zone.isTrigger = true;
            zone.center = new Vector3(0f, 0f, -theme.buttonHeight - theme.pokeDepth * 0.5f);
            zone.size = new Vector3(size.x, size.y, theme.pokeDepth);

            GameObject cap = MeshChild(root, "Cap", capMesh, normal, new Vector3(0f, 0f, -theme.buttonHeight * 0.5f));
            float face = -theme.buttonHeight * 0.5f - Layer;

            TabletButton button = root.AddUdonSharpComponent<TabletButton>();
            button.target = target;
            button.eventName = eventName;
            button.useArgument = useArgument;
            button.argument = argument;
            button.pressZone = zone;
            button.cap = cap.transform;
            button.capRenderer = cap.GetComponent<MeshRenderer>();
            button.normalMaterial = normal;
            button.activeMaterial = context.buttonActiveMaterial;
            button.pressedMaterial = context.buttonPressedMaterial;
            button.pressTravel = Mathf.Min(theme.pressTravel, theme.buttonHeight);

            Vector2 labelSize = new Vector2(size.x * 0.88f, size.y * 0.7f);
            float labelY = 0f;
            if (icon != null)
            {
                float iconSize = size.y * 0.5f;
                Material material = context.IconMaterial(icon);
                GameObject quad = MeshChild(cap, "Icon", context.iconMesh, material, new Vector3(0f, size.y * 0.16f, face));
                quad.transform.localScale = new Vector3(iconSize, iconSize, 1f);
                labelSize = new Vector2(size.x * 0.88f, size.y * 0.3f);
                labelY = -size.y * 0.3f;
            }

            button.label = Label(context, cap.transform, "Label", caption, new Vector3(0f, labelY, face), labelSize,
                TextAlignmentOptions.Center);

            context.buttons.Add(button);
            context.behaviours.Add(button);
            return button;
        }

        internal static TextMeshPro Label(Context context, Transform parent, string name, string text, Vector3 position,
            Vector2 size, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshPro>();
            if (context.font != null)
            {
                label.font = context.font;
            }

            label.text = text;
            label.color = context.theme.labelColor;
            label.alignment = alignment;
            // TextMeshPro の 3D 表示ではフォントサイズ 10 が約 1 m に相当します。
            label.enableAutoSizing = true;
            label.fontSizeMax = size.y * 10f * 0.8f;
            label.fontSizeMin = label.fontSizeMax * 0.25f;
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Ellipsis;
            // 表示名などをリッチテキストとして解釈させません。
            label.richText = false;

            RectTransform rect = label.rectTransform;
            rect.sizeDelta = size;
            rect.localPosition = position;
            return label;
        }

        // ------------------------------------------------------------------
        // 取っ手とトリガ
        // ------------------------------------------------------------------

        private static VRCPickup BuildHandle(Context context)
        {
            TabletTheme theme = context.theme;
            Vector3 size = theme.handleSize;
            GameObject handle = MeshChild(context.body.gameObject, HandleName, context.handleMesh, context.bodyMaterial,
                new Vector3(0f, theme.bodySize.y * 0.5f + size.y * 0.5f + 0.004f, 0f));

            var collider = handle.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = size;

            var rigidbody = handle.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            var pickup = handle.AddComponent<VRCPickup>();
            pickup.InteractionText = "Tablet";
            return pickup;
        }

        private static void BuildTriggers(Context context, Transform root)
        {
            TabletDefinition definition = context.definition;
            var triggers = Child(root, TriggersName).transform;

            if (definition.keyTrigger)
            {
                var go = new GameObject("Key");
                go.transform.SetParent(triggers, false);
                TabletKeyTrigger key = go.AddUdonSharpComponent<TabletKeyTrigger>();
                key.controller = context.controller;
                key.key = definition.key;
                context.behaviours.Add(key);
            }

            if (definition.reachTrigger)
            {
                var go = new GameObject("Reach");
                go.transform.SetParent(triggers, false);
                TabletReachTrigger reach = go.AddUdonSharpComponent<TabletReachTrigger>();
                reach.controller = context.controller;
                reach.anchorOffset = definition.reachOffset;
                context.behaviours.Add(reach);
            }

            // 定義から外したアイテムに以前の Build で付けたトリガを取り除きます。
            // 対象はこのタブレットを呼ぶトリガだけで、他のタブレットのものには触れません。
            foreach (TabletInteractTrigger stale in Object.FindObjectsOfType<TabletInteractTrigger>(true))
            {
                if (stale.controller == context.controller && !definition.interactItems.Contains(stale.gameObject))
                {
                    // UdonSharp の component と、実行時に使われる UdonBehaviour の両方を取り除きます。
                    var backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(stale);
                    if (backing != null)
                    {
                        Undo.DestroyObjectImmediate(backing);
                    }

                    if (stale != null)
                    {
                        Undo.DestroyObjectImmediate(stale);
                    }
                }
            }

            foreach (GameObject item in definition.interactItems)
            {
                if (item == null)
                {
                    continue;
                }

                TabletInteractTrigger trigger = item.GetComponent<TabletInteractTrigger>();
                if (trigger == null)
                {
                    trigger = item.AddUdonSharpComponent<TabletInteractTrigger>();
                }

                Undo.RecordObject(trigger, "Build Tablet");
                trigger.controller = context.controller;
                context.behaviours.Add(trigger);
            }

            if (context.teleport != null)
            {
                context.teleport.stowController = definition.stowAfterTeleport ? context.controller : null;
            }
        }

        private static void LinkExclusiveGroups(Context context)
        {
            foreach (List<TabletToggle> members in context.groups.Values)
            {
                TabletToggle[] group = members.ToArray();
                foreach (TabletToggle toggle in members)
                {
                    toggle.exclusive = group;
                }
            }
        }

        // ------------------------------------------------------------------
        // 補助
        // ------------------------------------------------------------------

        private static GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Build Tablet");
            return go;
        }

        private static GameObject Child(GameObject parent, string name)
        {
            return Child(parent.transform, name);
        }

        private static GameObject MeshChild(GameObject parent, string name, Mesh mesh, Material material, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = position;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        private static T[] NonNull<T>(T[] items) where T : Object
        {
            var list = new List<T>();
            if (items != null)
            {
                foreach (T item in items)
                {
                    if (item != null)
                    {
                        list.Add(item);
                    }
                }
            }

            return list.ToArray();
        }

        /// <summary>1 回の Build の間に共有する値。</summary>
        internal sealed class Context
        {
            public readonly TabletDefinition definition;
            public readonly TabletTheme theme;
            public readonly Vector2 screenSize;
            public readonly Vector2 gridSize;
            public readonly Vector2 gridCenter;
            public readonly Vector2 cellSize;
            public Vector2 entrySize;
            public readonly TMP_FontAsset font;

            public TabletController controller;
            public Transform body;
            public Transform modules;
            public TabletTeleport teleport;
            public TextMeshPro titleLabel;
            public TextMeshPro pageLabel;

            public readonly List<TabletButton> buttons = new List<TabletButton>();
            public readonly List<GameObject> pages = new List<GameObject>();
            public readonly List<string> pageTitles = new List<string>();
            public readonly List<Transform> destinations = new List<Transform>();
            public readonly List<UdonSharpBehaviour> behaviours = new List<UdonSharpBehaviour>();
            public readonly Dictionary<string, List<TabletToggle>> groups = new Dictionary<string, List<TabletToggle>>();
            private readonly Dictionary<Texture2D, Material> icons = new Dictionary<Texture2D, Material>();

            public Mesh bodyMesh;
            public Mesh screenMesh;
            public Mesh capMesh;
            public Mesh capMesh3;
            public Mesh headerMesh;
            public Mesh handleMesh;
            public Mesh iconMesh;
            public Material bodyMaterial;
            public Material screenMaterial;
            public Material buttonMaterial;
            public Material buttonActiveMaterial;
            public Material buttonPressedMaterial;
            public Material headerMaterial;

            private string folder;
            private readonly Dictionary<Vector2, Mesh> sizedCaps = new Dictionary<Vector2, Mesh>();

            public Mesh SizedCap(Vector2 size)
            {
                if (size == cellSize && capMesh != null) return capMesh;
                if (!sizedCaps.TryGetValue(size, out Mesh mesh))
                {
                    mesh = SaveMesh(TabletMeshBuilder.RoundedBox(size.x, size.y, theme.buttonHeight,
                        theme.buttonCornerRadius, CornerSegments), "SizedCap" + sizedCaps.Count);
                    sizedCaps.Add(size, mesh);
                }
                return mesh;
            }

            public Context(TabletDefinition definition, TabletTheme theme)
            {
                this.definition = definition;
                this.theme = theme;
                screenSize = TabletLayout.ScreenSize(theme.bodySize, theme.bezel);
                gridSize = TabletLayout.GridSize(screenSize, theme.headerHeight, theme.spacing);
                gridCenter = TabletLayout.GridCenter(theme.headerHeight);
                cellSize = TabletLayout.CellSize(gridSize, theme.columns, theme.rows, theme.spacing);
                font = theme.font != null ? theme.font : TMP_Settings.defaultFontAsset;
            }

            /// <summary>本体の前面の z。</summary>
            public float Front => -theme.bodyThickness * 0.5f;

            /// <summary>ボタンを置く画面の表面の z。</summary>
            public float Surface => Front - Layer * 2f;

            public TabletTeleport Teleport()
            {
                if (teleport == null)
                {
                    var go = new GameObject("Teleport");
                    go.transform.SetParent(modules, false);
                    teleport = go.AddUdonSharpComponent<TabletTeleport>();
                    teleport.destinations = new Transform[0];
                    behaviours.Add(teleport);
                }

                return teleport;
            }

            public void CreateAssets()
            {
                if (string.IsNullOrEmpty(definition.generatedFolder) || !definition.generatedFolder.StartsWith("Assets/")
                    || FolderSharedWithAnotherTablet(definition))
                {
                    TabletAssets.EnsureFolder(TabletAssets.GeneratedFolder);
                    definition.generatedFolder = AssetDatabase.GenerateUniqueAssetPath(
                        TabletAssets.GeneratedFolder + "/" + SafeName(definition.gameObject.name));
                }

                folder = definition.generatedFolder;
                TabletAssets.EnsureFolder(folder);

                float screenRadius = Mathf.Max(0f, theme.cornerRadius - theme.bezel);
                float headerHeight = Mathf.Max(0.005f, theme.headerHeight - theme.spacing);
                Vector2 playerCell = TabletLayout.CellSize(gridSize, 3, Mathf.Max(2, theme.rows), theme.spacing);

                bodyMesh = theme.bodyMesh != null ? theme.bodyMesh : SaveMesh(TabletMeshBuilder.RoundedBox(
                    theme.bodySize.x, theme.bodySize.y, theme.bodyThickness, theme.cornerRadius, CornerSegments), "Body");
                screenMesh = SaveMesh(TabletMeshBuilder.RoundedPanel(screenSize.x, screenSize.y, screenRadius, CornerSegments), "Screen");
                capMesh = theme.buttonMesh != null ? theme.buttonMesh : SaveMesh(TabletMeshBuilder.RoundedBox(
                    cellSize.x, cellSize.y, theme.buttonHeight, theme.buttonCornerRadius, CornerSegments), "Cap");
                capMesh3 = SaveMesh(TabletMeshBuilder.RoundedBox(
                    playerCell.x, playerCell.y, theme.buttonHeight, theme.buttonCornerRadius, CornerSegments), "CapWide");
                headerMesh = SaveMesh(TabletMeshBuilder.RoundedBox(
                    headerHeight * 1.8f, headerHeight, theme.buttonHeight, theme.buttonCornerRadius, CornerSegments), "HeaderCap");
                handleMesh = SaveMesh(TabletMeshBuilder.RoundedBox(
                    theme.handleSize.x, theme.handleSize.y, theme.handleSize.z,
                    theme.handleCornerRadius < 0f ? theme.handleSize.y * 0.5f : theme.handleCornerRadius, CornerSegments), "Handle");
                iconMesh = SaveMesh(TabletMeshBuilder.RoundedPanel(1f, 1f, 0f, 0), "Icon");

                bodyMaterial = theme.bodyMaterial != null ? theme.bodyMaterial : SaveMaterial(theme.bodyColor, "Body");
                screenMaterial = theme.screenMaterial != null ? theme.screenMaterial : SaveMaterial(theme.screenColor, "Screen");
                buttonMaterial = theme.buttonMaterial != null ? theme.buttonMaterial : SaveMaterial(theme.buttonColor, "Button");
                buttonActiveMaterial = theme.buttonActiveMaterial != null ? theme.buttonActiveMaterial
                    : SaveMaterial(theme.buttonActiveColor, "ButtonActive");
                buttonPressedMaterial = theme.buttonPressedMaterial != null ? theme.buttonPressedMaterial
                    : SaveMaterial(theme.buttonPressedColor, "ButtonPressed");
                headerMaterial = SaveMaterial(theme.headerButtonColor, "HeaderButton");
            }

            public Material IconMaterial(Texture2D icon)
            {
                if (!icons.TryGetValue(icon, out Material material))
                {
                    material = TabletAssets.CreateOrReplace(
                        TabletAssets.IconMaterial(icon, "Icon " + icons.Count), folder + "/Icon" + icons.Count + ".mat");
                    icons.Add(icon, material);
                }

                return material;
            }

            private Mesh SaveMesh(TabletMeshBuilder.MeshData data, string name)
            {
                return TabletAssets.CreateOrReplace(TabletMeshBuilder.ToMesh(data, name), folder + "/" + name + ".asset");
            }

            private Material SaveMaterial(Color color, string name)
            {
                return TabletAssets.CreateOrReplace(
                    TabletAssets.ColorMaterial(theme.shader, color, theme.smoothness, name), folder + "/" + name + ".mat");
            }

            /// <summary>
            /// 複製したタブレットは generatedFolder も複製されます。同じフォルダを使う別の定義が
            /// 開いているシーンにあれば、Build する側に新しいフォルダを割り当て、複製元のアセットを書き換えないようにします。
            /// </summary>
            private static bool FolderSharedWithAnotherTablet(TabletDefinition definition)
            {
                foreach (TabletDefinition other in Object.FindObjectsOfType<TabletDefinition>(true))
                {
                    if (other != definition && other.generatedFolder == definition.generatedFolder)
                    {
                        return true;
                    }
                }

                return false;
            }

            private static string SafeName(string name)
            {
                foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                {
                    name = name.Replace(c, '_');
                }

                return string.IsNullOrEmpty(name) ? "Tablet" : name;
            }
        }
    }
}
