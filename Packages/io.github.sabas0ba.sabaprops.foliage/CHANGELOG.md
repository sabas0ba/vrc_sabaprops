# Changelog

このパッケージの変更点をまとめています。
フォーマットは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に、
バージョニングは [Semantic Versioning](https://semver.org/lang/ja/) に従います。

## [Unreleased]

### Added

- ドキュメントにレンダリング図を追加しました。パラメータごとの形状の違いを、実際のメッシュ生成器の出力を並べた図で示します
  - 図は `Documentation~/images/generated/` にあり、`.github/figures/render.sh` が生成します。CI が生成結果との一致を検査するため、生成器を変えて図を作り直し忘れると Pull Request が落ちます
  - 一覧は `Documentation~/parameters.md`（ドキュメントサイトの `parameters` ページ）にまとまっています
  - 図はパッケージの zip には含まれません。導入したプロジェクトの容量は増えません

## [0.1.1] - 2026-08-23

`v0.1.0` はリスティングへ公開されないまま終わったため、実質的にこれが最初の公開版です。
以下は `0.1.0` のタグ以降ではなく、`0.1.0` の CHANGELOG に記載が無かった分をまとめたものです。

### Added

- サンプルシーン生成: `Tools/SabaProps/Foliage/Create Sample Scene`
  - 地面・起伏・傾斜・ライト・カメラと、GPU Instanced / Merged Chunks 両モードのフィールドを含むデモを生成し、
    ビルドまで済ませた状態で `Assets/SabaProps/Foliage/Samples/FoliageDemo.unity` に保存します
  - シーンも生成メッシュもプロジェクト側に書き出されるため、パッケージにバイナリアセットは増えません
  - VRChat Worlds SDK が導入されているプロジェクトでは `VRCSceneDescriptor` と Spawn も配置され、
    そのままアップロードできるワールドになります。SDK が無い場合はスキップし、通常の Unity シーンとして生成します
- `FoliageAssetLibrary.CreateOrLoadDefaults()`: 共有マテリアルと 2 種の Species をまとめて用意する API

- Species を 2 種追加しました
  - `Clover`: 低いグラウンドカバー。ハート形の小葉を持ち、草の隙間を埋めます
  - `Reed`: 直立した葦。穂付きで、草より高い縦のアクセントになります
- フィールド作成ウィザード: `GameObject > SabaProps > Foliage Field` がダイアログを開き、
  配置する種と出現比率、エリア形状、密度、出力モードを作成前に選べます
- `FoliageField.speciesWeights`: 出現比率をフィールド単位で持てるようになりました。
  0 のときは従来どおり Species アセットの `Placement Weight` を使います

### Changed

- グラスシードの既定の草丈を 0.35 m から 0.6 m に変更しました。既存の Species アセットは影響を受けません
  （新規作成時の既定値のみ変更）
- サンプルシーンを 2 区画から 5 セクション 16 区画の構成に拡充しました。
  隣り合う区画は 1 つだけ条件が異なります（種のみ／形状パラメータのみ／地形のみ／組み合わせ／出力モードのみ）
- Scene ビューで各 Foliage Field の名前がその場に表示されるようになりました（エディタのみ。ビルド結果には影響しません）
- `Face Sun`: 個体の向きをランダムにせず、シーンの Directional Light の方位へ揃えます。
  ひまわりは既定で ON（ばらつき 16 度）。太陽が無いシーンでは従来どおりランダムです
- `Skinned Ground`: 地面として `SkinnedMeshRenderer` を指定できるようになりました。
  生成時だけ現在のポーズをベイクした一時 MeshCollider を作ってレイキャストし、生成後に破棄します。
  対象に Collider を付けておく必要はありません
- 作成ウィザードで高さ制限・Density Mask・Skinned Ground を指定できるようになりました
- サンプルの地形セクションに、Collider を持たないスキンメッシュの起伏地形を追加しました

- **挙動変更**: `Min Spacing` を同じ種どうしの判定に変更しました。
  従来は種をまたいで判定していたため、密なグラウンドカバーに少数だけ混ぜた背の高い種が
  ほぼ配置されませんでした（草の平均間隔がひまわりの `Min Spacing` を下回るため）。
  既存のフィールドでは、混合時に少数派の種が以前より多く配置されます

### Fixed

- ひまわりが風で揺れたときに花びらが花芯から分離する問題を修正しました。
  花芯・花びら・茎頂点で風の位相と bend マスク、stiffness が食い違っており、接合部が相対移動していました。
  頭部は茎頂点と一体で動くようになり、花びら先端の追加の動きは花びらに沿った stiffness の勾配で表現します
- 葉の付け根が茎から浮く問題を修正しました（同じ原因の小規模版）

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

[Unreleased]: https://github.com/sabas0ba/vrc_sabaprops/compare/v0.1.1...HEAD
[0.1.1]: https://github.com/sabas0ba/vrc_sabaprops/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/sabas0ba/vrc_sabaprops/releases/tag/v0.1.0
