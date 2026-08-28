# Foliage Demo

Package Manager から Sample を Import した後、`FoliageDemo.unity` を開いてください。

シーンには次の生成結果が含まれます。

- Grass / Small Flower / Dandelion の通常 Species
- 床から壁へ連続して這う English Ivy preset
- 壁を垂直に這い、緑の葉身へ紫褐色の葉縁・主脈・葉柄を加えた Boston Ivy preset
- 床から斜面を経由して壁へ這う Creeping Fig preset
- 分岐する地下茎 Graph を共有するドクダミ型の地上茎

Surface Vine または Rhizome Patch を選ぶと、ガイド点、被覆率、分岐、葉形、葉密度、
パレット、Mesh 予算を確認できます。`Target Surface` に加えた `Additional Surfaces` により、
接する床・壁・斜面 Collider を1本の経路で横断します。根元には短いテーパー付き collar を
生成し、始点の切断面を隠します。`Build / Rebuild` は各 Collider へ経路を再投影し、永続
Mesh を `Assets/SabaProps/Foliage/Generated/SurfaceGrowth` 以下へ更新します。
