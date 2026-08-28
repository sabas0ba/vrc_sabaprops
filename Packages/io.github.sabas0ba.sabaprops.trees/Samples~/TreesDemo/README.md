# Trees Demo

Package Manager から Sample を Import した後、次のシーンを開いてください。

- `TreesDemo.unity`: ケヤキ、イロハモミジ、スギ、ヒノキ、アカマツ
- `SeasonalTreesDemo.unity`: シラカバ、ソメイヨシノの春／夏、イチョウの夏／秋

各個体は 1 つの `LODGroup` と 3 つの永続 Mesh asset を使います。生成済みのため、最初に
generator を実行する必要はありません。季節違いのソメイヨシノとイチョウは同じ Seed と
枝構造を共有し、花／葉形／色だけを切り替えています。

構造枝は下向きに生やさず、細い末端枝だけに小さな下垂を許可しています。各 preset は
一次枝まで葉を分布させ、`Crown Density` と `Foliage Depth` で枝葉密度を調整できます。
風は既定で有効です。Species の `Wind Enabled` / `Wind Response` を変更して LOD Mesh を
再生成するか、共有 Material の `Strength` / `Speed` / `Direction` を調整してください。

元の `TreeSpecies` asset も Mesh と同じフォルダにあります。これを出発点にするか、
`Tools > SabaProps > Trees > Create Default Assets` からプロジェクト用の既定 asset を
作成してください。
