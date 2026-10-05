using System.Collections.Generic;
using SabaProps.Tablet.Authoring;
using TMPro;
using UnityEngine;

namespace SabaProps.Tablet.Editors
{
    /// <summary>
    /// TabletBuilder.Built に渡す生成結果。生成したページの GameObject と、
    /// ボタンと同じ配置規則で部品を置くための寸法を持ちます。
    /// </summary>
    public sealed class TabletBuildResult
    {
        private readonly TabletBuilder.Context context;

        internal TabletBuildResult(TabletBuilder.Context context)
        {
            this.context = context;
        }

        public TabletDefinition Definition => context.definition;
        public TabletController Controller => context.controller;
        public IReadOnlyList<GameObject> Pages => context.pages;
        public IReadOnlyList<string> PageTitles => context.pageTitles;

        /// <summary>ボタンを並べる領域の大きさ (m)。</summary>
        public Vector2 GridSize => context.gridSize;

        public Vector2 CellSize => context.cellSize;

        /// <summary>ボタンを置く画面表面の z。本体の local 座標です。</summary>
        public float Surface => context.Surface;

        public int Columns => Mathf.Max(1, context.theme.columns);
        public int Rows => Mathf.Max(1, context.theme.rows);
        public float Spacing => context.theme.spacing;

        /// <summary>定義上のタイトルが title で始まる最初の生成ページ。無ければ null です。</summary>
        public GameObject FindPage(string title)
        {
            for (int i = 0; i < context.pages.Count; i++)
            {
                if (context.pageTitles[i].StartsWith(title))
                {
                    return context.pages[i];
                }
            }

            return null;
        }

        /// <summary>ページ内 index 番目のセルの中心。ボタンと同じ並びです。</summary>
        public Vector2 CellCenter(int index)
        {
            return TabletLayout.CellCenter(index, context.gridSize, context.gridCenter, Columns, Rows, Spacing);
        }

        /// <summary>Theme のフォントと色でラベルを作ります。position は親の local 座標です。</summary>
        public TextMeshPro CreateLabel(Transform parent, string name, string text, Vector3 position, Vector2 size,
            TextAlignmentOptions alignment)
        {
            return TabletBuilder.Label(context, parent, name, text, position, size, alignment);
        }
    }
}
