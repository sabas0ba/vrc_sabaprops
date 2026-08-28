# Changelog

このパッケージの変更点をまとめています。
フォーマットは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に、
バージョニングは [Semantic Versioning](https://semver.org/lang/ja/) に従います。

## [Unreleased]

### Added

- `SurfaceGrowthGraph` と `SurfaceVine`: ガイド点列の投影スプラインまたは決定的な表面クロールから分岐 Graph を作り、Collider 表面へツタを焼き込みます
  - 経路数、被覆率、分岐頻度、Node 予算、葉数、葉サイズ、4 種類の葉形、若葉・成葉・秋色・枯葉パレットを個別に制御できます
  - Creeping Fig / English Ivy / Boston Ivy の形態 preset を追加しました
- `RhizomePatch`: 同じ Graph を地下茎として使い、ドクダミ型の心形葉、紫赤色の差し色、白い苞を持つ地上茎を生成します
- Package Manager から導入できる、表面ツタと根茎パッチを含む生成済み `Foliage Demo` Sample

- `Window > SabaProps > Foliage Palette`: 配合、選択中 Species の形状パラメータ、メッシュプレビュー、Scene 配置をまとめた常設 Editor ウィンドウ
  - `WorkingCopy` は一時コピーだけを編集し、配置時に新しい Species アセットへ保存します。試行中に既存フィールドの見た目を変えません
  - `DirectAsset` は既存 Species を `SerializedObject` 経由で直接編集し、同じ GUID の Mesh を更新します。同じアセットを参照するフィールドへ変更が即時反映されます
  - Scene ビューで地面をクリックすると、現在の配合と Field 設定で生成します。地面に Collider が無い場合は Y=0 平面へ配置します
  - パネル操作とフィールド生成は Undo に対応します。`AssetDatabase` による Species／Mesh の書き出しは Unity の Undo 対象外です
- `FoliageSurfaceScatterer`: エリア走査、Density Mask、地面レイキャスト、高度制限、除外判定を prop パッケージ間で共有する決定的な Editor API
  - 種の選択、サーフェス条件、最小間隔、最終姿勢は callback として分離し、既存 Foliage Field の Seed 列を維持します
- `FoliageAreaUtility`: 矩形／円形の面積、範囲、包含判定、Mask UV 変換を共有する Runtime API
- `Vine`: 壁上の根から −Y 方向へ垂れる複数の茎と不透明な葉を生成する決定的メッシュ
  - 既存 Foliage Field を壁上端に細く配置して使う第 1 段階です。UV0 bend と UV3 root/stiffness は既存の風 shader 契約を使います
  - サンプルシーンに壁上配置の Vine 区画を追加しました

### Changed

- Surface Vine の Guide を経路そのものから誘導場へ変更し、根元範囲、経路長、葉間隔、葉角度を Seed から散らすようにしました
- Surface Vine の葉へ葉縁リング、主脈、葉柄の部分着色を追加しました。Boston Ivy preset は緑主体とし、葉全体の赤紫色を少数へ抑えました
- Field Wizard と Foliage Palette の既定 Weight を `FoliageAssetLibrary.DefaultFieldWeight()` に集約しました

## [0.3.0] - 2026-08-26

### Added

- `Small Flower`: 汎用の小花。茎・葉と 1 株あたり複数の花からなり、花弁の枚数・丸み・色でネモフィラやジャガイモの花などを作り分けます
  - 花ごとに生成器を分けていません。この大きさでは、どれも「淡い花芯のまわりに 5 枚の丸い花弁」であり、
    違うのは色と比率だけです。2 つ目の生成器は 1 つ目に別の定数を入れたものにしかなりません
  - 既定値はネモフィラ寄りです。一年草として、秋は `Dormant`（花を落として茎と葉だけ）、冬は `Absent` になります
  - サンプルシーンの組み合わせセクションに `Flower Field` を追加しました。花を主役にし、草を少数派にした区画です
- `Weed`: 雑草。広い葉が根元から放射状に出て、細い花茎が数本立ちます
  - 草と同じブレード生成を共有し、幅のプロファイルだけを変えています。草のブレードは根元が最も広く先へ細り、
    広葉は中ほどが最も広い。同じ 1 枚のストリップの違いはそこだけで、生成器を分ける理由になりません
  - 丈のばらつきを草より大きく取っています。芝と雑草を見分けている手掛かりは葉の形よりも不揃いさで、
    そこが揃うと「粗い芝」にしか見えません
  - 傾斜の上限を 60 度にしています。生える場所を選ばないことが、雑草を雑草たらしめている性質です
- `Grain`: 穀物。直立した葉と穂からなり、穂の垂れ具合と芒の長さで麦と稲を作り分けます
  - 麦と稲は同じ生成器です。畑で両者を見分けているのは穂が首を垂れるかどうかと芒の有無で、
    どちらもパラメータなので、違いはプリセット 1 つ分で足ります
  - サンプルシーンの形状パラメータのセクションに `Grain - Rice` を追加しました。隣の麦と同じ生成器です
  - 冬は `Absent` です。冬の畑に立っているのは刈り株で、それはこのメッシュではありません
- `Dandelion`: たんぽぽ。鋸歯のある葉が地面に張り付き、裸の花茎に花か綿毛が付きます
  - `Seed Head` で花と綿毛を切り替えます。綿毛は放射状の細い三角形なので、**花より三角形数が少なくなります**。
    アルファテストの板で作った場合と逆で、不透明で通す方針がそのまま効いています
  - 冠毛は球状に散らします。綿毛には正面が無く、平面に並べると横から見たとき消えるためです
  - 多年草なので、他の花と違って冬も `Absent` にしません。葉のロゼットは残り、頭だけが消えます
  - 鋸歯は共有のブレード生成にプロファイルとして追加しました。既定は 0 なので他の種の生成結果は変わりません

## [0.2.0] - 2026-08-23

### Added

- `Season`: 春夏秋冬の色をメッシュ生成時に頂点カラーへ焼き込みます。実行時のコストはありません
  - 寄せ方（目標色・寄せる割合・彩度・明度）は Species ごとに `Season Palette` として持ちます。
    同じ秋でも種ごとに違う枯れ方をさせられます
  - `Summer` は何も変えません。既存の Species アセットは `Summer` として読み込まれるため、
    生成されるメッシュはこれまでと同一です
  - 花弁など、季節が変わっても色を保つべき部位は生成側で効き方を弱めています
    （ひまわりの花弁 30 %、花芯 55 %、葦の穂 50 %）
  - 季節は 5 つです。冬は「雪の下の枯野」と「雪の無い寒い日の枯野」で見え方が別物なので、
    `Winter Snow`（白っぽく退色）と `Winter Bare`（黒に近い茶）に分けています
  - 季節ごとに姿も指定できます。`Dormant` は一年で落ちる部位を生成せず、`Absent` はその季節に配置しません。
    ひまわりは既定で秋が `Dormant`（花弁を落とした種頭だけ）、冬が `Absent`（一年草なので姿を消す）です。
    枯草色に染めただけの満開のひまわりは実在しないためです。
    `Absent` は配置の重みを 0 として扱うので、他の種の密度は変わりません
  - `Wind Scale` と `Droop`: 水分の抜けた株は風でしなりにくく、根元から先端へ向けて倒れます。
    `Droop` は横へずらすのではなく根元まわりの回転なので茎の長さが変わらず、法線も同じ回転で追従します。
    倒す量は風と同じ bend マスクから決まるため、接合部の関係は崩れません
- サンプルシーンにセクション 6 `Seasons` を追加しました。種・比率・シードが共通で、季節だけが違う 5 区画です
- [拡充ロードマップ](Documentation~/roadmap.md): 種と樹木、ツタまでの計画と、その過程での設計上の線引きを記録しています
- `Tools/SabaProps/Foliage/Import VRChat Demo Movement`: デモ用の移動設定（歩行 4 m/s・走行 9 m/s・ジャンプ可）を
  取り込みます。VRChat の既定は歩行 2 m/s・ジャンプ不可で、これらは `VRCSceneDescriptor` の項目ではなく
  `VRCPlayerApi` を実行時に呼ぶ仕様のため Udon が必要です。スクリプトは `Samples~` に置き、
  取り込みを任意の操作にしています。取り込まない限り UdonSharp への依存は発生しません
- ドキュメントにレンダリング図を追加しました。パラメータごとの形状の違いを、実際のメッシュ生成器の出力を並べた図で示します
  - 図は `Documentation~/images/generated/` にあり、`.github/figures/render.sh` が生成します。CI が生成結果との一致を検査するため、生成器を変えて図を作り直し忘れると Pull Request が落ちます
  - 一覧は `Documentation~/parameters.md`（ドキュメントサイトの `parameters` ページ）にまとまっています
  - 図はパッケージの zip には含まれません。導入したプロジェクトの容量は増えません

### Changed

- サンプルシーンの地面を、追加した 6 番目の区画列まで届くように広げました

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

[Unreleased]: https://github.com/sabas0ba/vrc_sabaprops/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/sabas0ba/vrc_sabaprops/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/sabas0ba/vrc_sabaprops/compare/v0.1.1...v0.2.0
[0.1.1]: https://github.com/sabas0ba/vrc_sabaprops/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/sabas0ba/vrc_sabaprops/releases/tag/v0.1.0
