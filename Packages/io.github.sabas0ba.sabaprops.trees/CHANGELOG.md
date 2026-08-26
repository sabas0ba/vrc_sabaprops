# Changelog

All notable changes to this package are documented in this file.

## [0.1.0] - 2026-08-27

### Added

- `TreeSpecies`: 広葉樹、針葉樹、枯れ木、砂漠低木を同一パラメータモデルで表現
- 再帰的な枝分かれと決定的な Seed から 3 段階の LOD Mesh を生成
- 枝サブツリー単位の UV3 wind pivot と、接続点で連続する bend 座標
- SabaProps Foliage shader を使い、Distance Shrink を無効化した樹木用 Material
- 影を有効にした Scene 用 `LODGroup` の生成
