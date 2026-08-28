# Changelog

All notable changes to this package are documented in this file.

## [Unreleased]

### Added

- ケヤキ、イロハモミジ、スギ、シラカバ、アカマツの実在種 preset
- 樹冠形、枝序、主幹優勢、枝の下垂と先端上向き、葉序を分けた生成規則
- Package Manager から導入できる、5 実在種と 3 段階 LOD を生成済みの `Trees Demo` Sample

### Changed

- 子枝の太さへ分岐数に基づく上限を設け、枝次数に応じて樹皮色を補間するようにしました
- 掌状葉、鱗片状の短葉、2 針束を含む樹種別の葉配置へ変更しました

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
