# Trees Demo

Package Manager から Sample を Import した後、次のシーンを開いてください。

- `TreesDemo.unity`: ケヤキ・イロハモミジ・アカマツの混交林、スギ・ヒノキの植林、
  ケヤキの街路樹
- `SeasonalTreesDemo.unity`: 春／夏のソメイヨシノ並木、夏／秋のイチョウ並木、
  高さにばらつきのあるシラカバ林
- `ForestLoadDemo.unity`: 64 本ずつの混交広葉樹、針葉樹植林、季節混交林。Hierarchy の
  `Load Group 1` から順に有効化し、64 / 128 / 192 本の負荷と見栄えを比較

各個体は 1 つの `LODGroup` と 3 つの永続 Mesh asset を使います。生成済みのため、最初に
generator を実行する必要はありません。季節違いのソメイヨシノとイチョウは同じ Seed と
枝構造を共有し、花／葉形／色だけを切り替えています。

森林は高さを約±22%、植林は約-14〜+16%、街路樹と並木は約±8〜9%変えています。
配置と個体差は固定Seedで再生成でき、全個体が樹種ごとのLOD Meshを共有します。単木の細部
よりも、中距離での反復感、樹冠の重なり、LOD遷移、風のまとまりを評価するための構成です。
負荷計測時は `ForestLoadDemo.unity` で比較対象以外の `Load Group` を無効化し、Game View の
解像度とカメラ位置を固定してください。各群は 64 個の `LODGroup` と 192 個の Renderer を
持ち、3 群すべてを有効にすると 192 本、576 Renderer です。

構造枝は下向きに生やさず、細い末端枝だけに小さな下垂を許可しています。各 preset は
一次枝まで葉を分布させ、`Crown Density` と `Foliage Depth` で枝葉密度を調整できます。
風は既定で有効です。Species の `Wind Enabled` / `Wind Response` を変更して LOD Mesh を
再生成するか、共有 Material の `Strength` / `Speed` / `Direction` を調整してください。

元の `TreeSpecies` asset も Mesh と同じフォルダにあります。これを出発点にするか、
`Tools > SabaProps > Trees > Create Default Assets` からプロジェクト用の既定 asset を
作成してください。
