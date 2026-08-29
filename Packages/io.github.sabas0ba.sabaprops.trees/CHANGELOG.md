# Changelog

All notable changes to this package are documented in this file.

## [Unreleased]

### Added

- ケヤキ、イロハモミジ、スギ、シラカバ、アカマツ、ヒノキの実在種 preset
- ソメイヨシノの春／夏とイチョウの夏／秋を、同一 Seed・同一枝構造で切り替える季節 preset
- 樹冠形、枝序、主幹優勢、枝の下垂と先端上向き、葉序を分けた生成規則
- Species ごとの `Wind Enabled` / `Wind Response` と、主枝・葉密度を独立制御する `Crown Density` / `Foliage Depth`
- Package Manager から導入できる、10 preset と 3 段階 LOD を生成済みの `Trees Demo` / `Seasonal Trees Demo` Sample
- Demoを混交林、スギ・ヒノキ植林、街路樹、サクラ・イチョウ並木、シラカバ林の群生配置へ変更し、用途別の高さ揺らぎを追加しました
- 64 / 128 / 192 本の群生を同一条件で比較できる `Forest Load Demo` Sample

### Changed

- Relicensed the package from MIT to Apache License 2.0.
- Trunks now taper steadily from a flared base, child radii are capped at their junction, and branches emerge from the parent tangent before turning toward their target direction.
- LOD1/LOD2 preserve crown mass with fewer, broader leaf cards and a longer far-distance range; the 192-tree load scene uses denser stands for silhouette and load evaluation.
- Japanese red pine bark/needles and summer ginkgo foliage use less saturated colours.

- 子枝の太さへ分岐数に基づく上限を設け、枝次数に応じて樹皮色を補間するようにしました
- 掌状葉、鱗片状の短葉、2 針束、5 弁花、扇形葉を含む樹種別の葉配置へ変更しました
- 構造枝の下向き成長を抑え、下垂を末端枝だけへ適用しました。継続枝は親枝終端の位置・半径を引き継ぎ、切断面を作りません
- 実在種 preset は葉を一次枝まで分布させ、主枝層を増やしました。既定 LOD0 は各 preset 10 万 triangle 未満です

## [0.1.0] - 2026-08-27

### Added

- `TreeSpecies`: 広葉樹、針葉樹、枯れ木、砂漠低木を同一パラメータモデルで表現
- 再帰的な枝分かれと決定的な Seed から 3 段階の LOD Mesh を生成
- 枝サブツリー単位の UV3 wind pivot と、接続点で連続する bend 座標
- SabaProps Foliage shader を使い、Distance Shrink を無効化した樹木用 Material
- 影を有効にした Scene 用 `LODGroup` の生成
- `TreeField`: 固定 Seed、地面吸着、高度／傾斜／除外／Density Mask と種別 Weight を使った複数個体の配置
- Species ごとに生成した 3 個の LOD Mesh を全個体で共有する edit-time builder
- `GameObject > SabaProps > Tree Field` と、生成・クリア・統計表示を行う Inspector
