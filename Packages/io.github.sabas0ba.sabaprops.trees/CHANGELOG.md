# Changelog

All notable changes to this package are documented in this file.

## [Unreleased]

### Added

- Package Manager から導入できる、4 archetype と 3 段階 LOD を生成済みの `Trees Demo` Sample

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
