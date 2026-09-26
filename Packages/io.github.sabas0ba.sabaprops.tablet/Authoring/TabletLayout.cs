using UnityEngine;

namespace SabaProps.Tablet.Authoring
{
    /// <summary>
    /// 本体上の配置計算。座標は本体中心を原点とし、+x が右、+y が上です。
    /// UnityEngine の型は値型だけを使い、.github/verify/offline で Unity なしに検査します。
    /// </summary>
    public static class TabletLayout
    {
        public static int CellsPerPage(int columns, int rows)
        {
            return Mathf.Max(1, columns) * Mathf.Max(1, rows);
        }

        /// <summary>項目数を 1 ページのセル数で分けたページ数。項目が無くても 1 ページとします。</summary>
        public static int PageCount(int entryCount, int cellsPerPage)
        {
            if (entryCount <= 0)
            {
                return 1;
            }

            int cells = Mathf.Max(1, cellsPerPage);
            return (entryCount + cells - 1) / cells;
        }

        /// <summary>画面の大きさ。本体からベゼルを除いた領域です。</summary>
        public static Vector2 ScreenSize(Vector2 body, float bezel)
        {
            return new Vector2(Mathf.Max(0f, body.x - bezel * 2f), Mathf.Max(0f, body.y - bezel * 2f));
        }

        /// <summary>ヘッダーの中心。画面の上端に接します。</summary>
        public static Vector2 HeaderCenter(Vector2 screen, float headerHeight)
        {
            return new Vector2(0f, screen.y * 0.5f - headerHeight * 0.5f);
        }

        /// <summary>ボタンを並べる領域の大きさ。画面からヘッダーと外周の余白を除きます。</summary>
        public static Vector2 GridSize(Vector2 screen, float headerHeight, float spacing)
        {
            return new Vector2(
                Mathf.Max(0f, screen.x - spacing * 2f),
                Mathf.Max(0f, screen.y - headerHeight - spacing * 2f));
        }

        /// <summary>ボタンを並べる領域の中心。</summary>
        public static Vector2 GridCenter(float headerHeight)
        {
            return new Vector2(0f, -headerHeight * 0.5f);
        }

        /// <summary>1 セルの大きさ。セル間に spacing の隙間を取ります。</summary>
        public static Vector2 CellSize(Vector2 grid, int columns, int rows, float spacing)
        {
            int c = Mathf.Max(1, columns);
            int r = Mathf.Max(1, rows);
            return new Vector2(
                Mathf.Max(0f, (grid.x - spacing * (c - 1)) / c),
                Mathf.Max(0f, (grid.y - spacing * (r - 1)) / r));
        }

        /// <summary>ページ内 index 番目のセルの中心。左上から右へ、行が尽きたら下の行へ進みます。</summary>
        public static Vector2 CellCenter(int index, Vector2 grid, Vector2 gridCenter, int columns, int rows, float spacing)
        {
            int c = Mathf.Max(1, columns);
            Vector2 cell = CellSize(grid, columns, rows, spacing);
            int column = index % c;
            int row = index / c;
            float left = gridCenter.x - grid.x * 0.5f;
            float top = gridCenter.y + grid.y * 0.5f;
            return new Vector2(
                left + cell.x * 0.5f + column * (cell.x + spacing),
                top - cell.y * 0.5f - row * (cell.y + spacing));
        }
    }
}
