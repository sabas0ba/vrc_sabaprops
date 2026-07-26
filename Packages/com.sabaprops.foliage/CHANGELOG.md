# Changelog

このパッケージの変更点をまとめています。
フォーマットは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に、
バージョニングは [Semantic Versioning](https://semver.org/lang/ja/) に従います。

## [Unreleased]

## [0.1.0] - 2026-07-26

### Added

- `SabaProps/Foliage` シェーダー（Built-in RP / GPU インスタンシング対応）
  - ワールド座標ハッシュによる個体差（色相・彩度・明度）。追加の per-instance データ不要
  - ワールド空間を進行する風波＋突風＋乱流の頂点アニメーション
  - 距離に応じて根元へ縮退させる Distance Shrink（実質的な密度 LOD）
  - ラップディフューズ＋透過光による葉らしいライティング（`noforwardadd` で軽量化）
- `FoliageField` コンポーネントとエディタ配置ツール
  - 矩形／円形エリア、密度指定、シード固定の決定論的スキャッタリング
  - 地面へのレイキャスト吸着、傾斜／高度フィルタ、除外レイヤー、密度マスクテクスチャ
  - 出力モード: **GPU Instanced**（1 個体 1 Renderer）/ **Merged Chunks**（チャンク結合）
  - チャンク分割、統計表示（インスタンス数・三角形数・推定ドローコール）
- プロシージャルメッシュ生成
  - `GrassClump`: 曲率付きブレードの草叢。テクスチャ不要
  - `Sunflower`: 茎・葉・花芯・花弁からなるひまわり。部位ごとの風剛性を持つ
- `FoliageSpecies` ScriptableObject による種別プリセット
- セットアップメニュー: `Tools/SabaProps/Foliage/Create Default Assets`

[Unreleased]: https://github.com/sabas0ba/vrc_sabaprops/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/sabas0ba/vrc_sabaprops/releases/tag/v0.1.0
