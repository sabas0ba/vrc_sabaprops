# Changelog

このパッケージの変更点をまとめています。
フォーマットは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に、
バージョニングは [Semantic Versioning](https://semver.org/lang/ja/) に従います。

## [Unreleased]

### Added

- サンプルシーン生成: `Tools/SabaProps/Foliage/Create Sample Scene`
  - 地面・起伏・傾斜・ライト・カメラと、GPU Instanced / Merged Chunks 両モードのフィールドを含むデモを生成し、
    ビルドまで済ませた状態で `Assets/SabaProps/Foliage/Samples/FoliageDemo.unity` に保存します
  - シーンも生成メッシュもプロジェクト側に書き出されるため、パッケージにバイナリアセットは増えません
  - VRChat Worlds SDK が導入されているプロジェクトでは `VRCSceneDescriptor` と Spawn も配置され、
    そのままアップロードできるワールドになります。SDK が無い場合はスキップし、通常の Unity シーンとして生成します
- `FoliageAssetLibrary.CreateOrLoadDefaults()`: 共有マテリアルと 2 種の Species をまとめて用意する API

### Changed

- グラスシードの既定の草丈を 0.35 m から 0.6 m に変更しました。既存の Species アセットは影響を受けません
  （新規作成時の既定値のみ変更）
- サンプルシーンの密度を引き上げました（Meadow 4.5 → 12 /m²、Clearing 8 → 20 /m²）

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
