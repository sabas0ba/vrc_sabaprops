using System;
using System.Collections.Generic;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

namespace SabaProps.Tablet.Authoring
{
    /// <summary>項目の種類。</summary>
    public enum TabletEntryKind
    {
        /// <summary>GameObject、Collider、Behaviour の有効・無効を切り替えます。</summary>
        Toggle = 0,

        /// <summary>指定した地点へ自分をテレポートします。</summary>
        Teleport = 1,

        /// <summary>任意の UdonSharpBehaviour のイベントを呼びます。</summary>
        CustomEvent = 2,

        /// <summary>別のページを開きます。</summary>
        PageLink = 3,

        /// <summary>物理スライダーで float tabletValue を渡してイベントを呼びます。</summary>
        Slider = 4,
    }

    /// <summary>タブレットの 1 ボタン分の定義。</summary>
    [Serializable]
    public class TabletEntry
    {
        public string label = "";
        public Texture2D icon;
        public TabletEntryKind kind;

        [Header("Toggle")]
        public GameObject[] objects = new GameObject[0];
        public GameObject[] invertedObjects = new GameObject[0];
        public Collider[] colliders = new Collider[0];
        public Behaviour[] behaviours = new Behaviour[0];
        public bool startOn = true;

        [Tooltip("全員の状態を同期します。")]
        public bool global;

        [Tooltip("同じ名前のグループ内では 1 つだけが ON になります。空欄ならグループに属しません。")]
        public string exclusiveGroup = "";

        [Header("Teleport")]
        public Transform destination;

        [Header("CustomEvent")]
        public UdonSharpBehaviour target;
        public string eventName = "";
        public bool useArgument;
        public int argument;

        [Header("PageLink")]
        public int pageIndex;

        [Header("Slider")]
        public float minimum;
        public float maximum = 1f;
        public float initialValue;

        [Header("配置")]
        public bool customPlacement;
        [Tooltip("ボタン領域に対する中心位置。中央が (0, 0)、端が ±0.5 です。")]
        public Vector2 normalizedCenter;
        public Vector2 normalizedSize = new Vector2(0.25f, 0.25f);
    }

    /// <summary>タブレットの 1 ページ分の定義。ボタン数が 1 画面を超える場合は Build で複数ページに分けます。</summary>
    [Serializable]
    public class TabletPage
    {
        public string title = "";
        public List<TabletEntry> entries = new List<TabletEntry>();
        [Tooltip("中央にベッドの平面図を表示します。頭側が上です。")]
        public bool bedDiagram;
    }

    /// <summary>
    /// タブレットの構成を保持する Editor 専用のコンポーネント。Build でこの内容から
    /// 本体、ボタン、モジュールを生成します。IEditorOnly のため、アップロード時に取り除かれます。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("SabaProps/Tablet Definition")]
    public class TabletDefinition : MonoBehaviour, IEditorOnly
    {
        [Tooltip("外観。未設定なら既定値で生成します。")]
        public TabletTheme theme;

        public List<TabletPage> pages = new List<TabletPage>();

        [Header("生成するページ")]
        [Tooltip("プレイヤーを選んでその後方へ移動するページを追加します。")]
        public bool includePlayerPage = true;

        [Tooltip("テレポート後にタブレットを収納します。")]
        public bool stowAfterTeleport = true;

        [Header("召喚方法")]
        public bool keyTrigger = true;
        public KeyCode key = KeyCode.B;

        [Tooltip("頭上に手を伸ばして Grab すると取り出します (VR)。")]
        public bool reachTrigger = true;

        [Tooltip("頭の水平方向の向きを基準にした取り出し位置 (m)。")]
        public Vector3 reachOffset = new Vector3(0f, 0.22f, 0f);

        [Tooltip("Interact で召喚するワールド内のアイテム。Collider が必要です。")]
        public List<GameObject> interactItems = new List<GameObject>();

        public bool startVisible;

        [Tooltip("生成したメッシュとマテリアルの保存先。空なら Build 時に決めます。")]
        public string generatedFolder = "";

        /// <summary>同じタイトルのページを返します。無ければ末尾に追加します。</summary>
        public TabletPage FindOrAddPage(string title)
        {
            foreach (TabletPage page in pages)
            {
                if (page != null && page.title == title)
                {
                    return page;
                }
            }

            var added = new TabletPage { title = title };
            pages.Add(added);
            return added;
        }

        /// <summary>いずれかの Toggle 項目が object を対象にしているか。</summary>
        public bool ContainsToggleTarget(GameObject target)
        {
            foreach (TabletPage page in pages)
            {
                if (page == null)
                {
                    continue;
                }

                foreach (TabletEntry entry in page.entries)
                {
                    if (entry == null || entry.kind != TabletEntryKind.Toggle)
                    {
                        continue;
                    }

                    if (Array.IndexOf(entry.objects, target) >= 0)
                    {
                        return true;
                    }

                    foreach (Collider collider in entry.colliders)
                    {
                        if (collider != null && collider.gameObject == target)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
