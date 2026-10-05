using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SabaProps.BodyContact.Editors
{
    /// <summary>シーンへ Body Contact System と検証用の構成を配置するメニュー。</summary>
    public static class BodyContactMenu
    {
        private const string AssetFolder = "Assets/SabaProps/BodyContact";
        private const string LineMaterialPath = AssetFolder + "/BodyContactDebugLine.mat";

        [MenuItem("GameObject/SabaProps/Body Contact System", false, 10)]
        public static void CreateSystemFromMenu()
        {
            GameObject root = CreateSystem();
            Undo.RegisterCreatedObjectUndo(root, "Create Body Contact System");
            Selection.activeGameObject = root;
        }

        [MenuItem("GameObject/SabaProps/Body Contact Dummy", false, 11)]
        public static void CreateDummyFromMenu()
        {
            BodyContactSystem system = Object.FindObjectOfType<BodyContactSystem>();
            if (system == null)
            {
                Debug.LogWarning("[SabaProps Body Contact] 先に Body Contact System を配置してください。");
                return;
            }

            GameObject dummy = AddDummy(system, new Vector3(0f, 0f, 1.5f));
            Undo.RegisterCreatedObjectUndo(dummy, "Create Body Contact Dummy");
            Selection.activeGameObject = dummy;
        }

        [MenuItem("Tools/SabaProps/Body Contact/Documentation", false, 100)]
        public static void OpenDocumentation()
        {
            Application.OpenURL(
                "https://github.com/sabas0ba/vrc_sabaprops/blob/main/Packages/io.github.sabas0ba.sabaprops.bodycontact/README.md");
        }

        /// <summary>
        /// System、線のデバッグ表示、数値表示を作成して接続します。
        /// 線のメッシュは頂点をワールド座標で持つため、全体をシーンのルートの原点に置きます。
        /// </summary>
        public static GameObject CreateSystem()
        {
            var root = new GameObject("Body Contact");
            BodyContactSystem system = root.AddUdonSharpComponent<BodyContactSystem>();

            // NoneとManualの同期モードを別GameObjectへ分離します。
            var pullObject = new GameObject("Body Pull Session");
            pullObject.transform.SetParent(root.transform, false);
            BodyContactPull pull = pullObject.AddUdonSharpComponent<BodyContactPull>();
            pull.source = system;
            system.pull = pull;
            UdonSharpEditorUtility.CopyProxyToUdon(pull);

            var lines = new GameObject("Debug Lines");
            lines.transform.SetParent(root.transform, false);
            MeshFilter filter = lines.AddComponent<MeshFilter>();
            MeshRenderer renderer = lines.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = LoadOrCreateLineMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var hud = new GameObject("Debug HUD");
            hud.transform.SetParent(root.transform, false);
            Text label = CreateLabel(hud.transform);

            BodyContactDebugView view = lines.AddUdonSharpComponent<BodyContactDebugView>();
            view.source = system;
            view.lineFilter = filter;
            view.lineRenderer = renderer;
            view.statusLabel = label;
            view.hudRoot = hud.transform;
            UdonSharpEditorUtility.CopyProxyToUdon(view);

            system.debugView = view;
            UdonSharpEditorUtility.CopyProxyToUdon(system);
            return root;
        }

        /// <summary>標準体型の固定ダミーを追加し、System の dummies へ登録します。</summary>
        public static GameObject AddDummy(BodyContactSystem system, Vector3 position)
        {
            var dummy = new GameObject("Body Contact Dummy");
            dummy.transform.SetParent(system.transform, false);
            dummy.transform.position = position;
            BodyContactSampleVisuals.AddMannequin(system, dummy.transform);

            int count = system.dummies == null ? 0 : system.dummies.Length;
            var next = new Transform[count + 1];
            for (int i = 0; i < count; i++)
            {
                next[i] = system.dummies[i];
            }

            next[count] = dummy.transform;
            system.dummies = next;
            UdonSharpEditorUtility.CopyProxyToUdon(system);
            EditorUtility.SetDirty(system);
            return dummy;
        }

        // ワールド空間の Canvas。1 px を 1 mm とし、視点の前 0.6 m に置いて読める大きさにします。
        private static Text CreateLabel(Transform parent)
        {
            var canvasObject = new GameObject("Canvas");
            canvasObject.transform.SetParent(parent, false);
            canvasObject.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.sizeDelta = new Vector2(520f, 280f);

            var labelObject = new GameObject("Status");
            labelObject.transform.SetParent(canvasObject.transform, false);
            Text label = labelObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 22;
            label.color = Color.white;
            label.alignment = TextAnchor.UpperLeft;
            label.raycastTarget = false;
            label.text = "Body Contact";

            var labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            return label;
        }

        // 既存の生成済みマテリアルも更新し、シーンを作り直すと透過表示へ移行します。
        private static Material LoadOrCreateLineMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(LineMaterialPath);
            Shader shader = Shader.Find("SabaProps/Body Contact/Debug Lines");
            if (shader == null)
            {
                Debug.LogError("[SabaProps Body Contact] Debug Lines シェーダーが見つかりません。");
                return null;
            }

            EnsureFolder("Assets", "SabaProps");
            EnsureFolder("Assets/SabaProps", "BodyContact");
            if (material != null)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
                return material;
            }
            material = new Material(shader);
            material.name = "BodyContactDebugLine";
            AssetDatabase.CreateAsset(material, LineMaterialPath);
            return material;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        [MenuItem("Tools/SabaProps/Body Contact/Connect Scene Stations", false, 2)]
        public static void ConnectSceneStations()
        {
            BodyContactSystem system = Object.FindObjectOfType<BodyContactSystem>();
            if (system == null) return;
            foreach (VRC.SDK3.Components.VRCStation station in Object.FindObjectsOfType<VRC.SDK3.Components.VRCStation>(true))
            {
                BodyContactStationRelay relay = station.GetComponent<BodyContactStationRelay>();
                if (relay == null) relay = station.gameObject.AddUdonSharpComponent<BodyContactStationRelay>();
                relay.source = system;
                UdonSharpEditorUtility.CopyProxyToUdon(relay);
                EditorUtility.SetDirty(relay);
            }
        }
    }
}
