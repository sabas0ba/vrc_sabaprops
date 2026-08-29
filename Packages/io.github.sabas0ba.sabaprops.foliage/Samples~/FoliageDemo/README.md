# Foliage Demo

Package Manager から Sample を Import した後、目的に応じて次のシーンを開いてください。

- `FoliageDemo.unity`: 床・壁・斜面を横断する Surface Vine と根茎パッチ
- `FoliageSpeciesDemo.unity`: Grass、Clover、Sunflower、Reed、Small Flower、Weed、
  Grain、Dandelion、従来型の垂れ下がる Vine を小群落で並べた種別ギャラリー

`FoliageDemo.unity` には次の生成結果が含まれます。

- Grass / Small Flower / Dandelion の通常 Species
- 床から壁へ連続して這う English Ivy preset
- 壁を垂直に這い、緑の葉身へ紫褐色の葉縁・主脈・葉柄を加えた Boston Ivy preset
- 床から斜面を経由して壁へ這う Creeping Fig preset
- 分岐する地下茎 Graph を共有するドクダミ型の地上茎

Surface Vine または Rhizome Patch を選ぶと、ガイド点、被覆率、分岐密度・深度・長さ・
角度・各種揺らぎ、進行方向の `Direction Jitter` / `Direction Persistence`、葉形、葉密度、
パレット、Mesh 予算を確認できます。`Target Surface` に加えた `Additional Surfaces` により、
接する床・壁・斜面 Collider を1本の経路で横断します。根元には短いテーパー付き collar を
生成し、始点の切断面を隠します。`Build / Rebuild` は各 Collider へ経路を再投影し、永続
Mesh を `Assets/SabaProps/Foliage/Generated/SurfaceGrowth` 以下へ更新します。

29区画で全パラメータ、地形、出力モード、季節を比較する従来の大規模Demoも残っています。
`Tools > SabaProps > Foliage > Create Sample Scene` からプロジェクト内へ生成できます。
