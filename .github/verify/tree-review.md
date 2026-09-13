# 樹冠形状の確認

Unity 2022.3.22f1の検証ProjectにPackagesと `CIProject/Assets/Tests` を同期して使用します。既存Projectに未保存の編集がある場合は実行しないでください。

## 自動検証

EditModeの `SabaProps.Trees.CITests.TreeEditorTests` は、半径拡大前のサイズ参照、枝の下降がないこと、枝先の高さ分布、LOD間の主幹中心線の一致を検査します。半径拡大は幹の頂点を維持し、枝の到達半径が約1.5倍になることを別途検証します。半径拡大後の体積には旧±20%制約を適用しません。外接箱体積は枝葉の実占有体積ではなく、これらの検査だけで見栄えを合格とはしません。

## キャプチャ

`-executeMethod SabaProps.Trees.CITests.TreeCrownReview.Capture` を指定するとSampleを再生成し、Project直下の `CrownReview` にサクラ春・夏、イチョウ秋、イロハモミジ、シラカバのPNGを出力します。描画が必要なため `-nographics` は指定しません。

`SeasonalTrees-overview.png` は季節比較シーン全体、`*-junction.png` は樹皮の接合部の拡大です。`AuditSeasonalScene` は既存の季節シーンを開き、LOD間の樹種参照と異なるpresetのLOD0境界の重なりをログへ出力します。全LODの異種区画の非交差はEditModeの配布サンプルテストで検証します。

公開ガイドの画像は `Packages/io.github.sabas0ba.sabaprops.trees/Documentation~/images/` に配置します。全景を `seasonal-overview.png`、夏桜を `sakura-summer.png`、秋イチョウを `ginkgo-autumn.png`、シラカバ接合部を `birch-junction.png` としてコピーします。実際のUnity描画を使用し、画像生成による補正は行いません。

葉付きと枝のみを同じ縮尺・正投影カメラで比較し、`*-top.png` で真上からの分布も確認します。風は停止します。枝のみの生成で外接箱が変わる分は根元の半径を使って縮尺を合わせます。

同期前に `python3 .github/verify/compare_tree_bounds.py <前回のAssetsディレクトリ> <今回のAssetsディレクトリ>` を専用コンテナ内で実行すると、風用paddingを除いたX/Z幅と高さの比を確認できます。これは外接箱の計測であり、葉密度や実占有体積の計測ではありません。

- 枝が付け根から上向きに伸び、外形に沿って下向きに折り返していないか
- 枝葉が上端の薄い層だけに集中していないか
- 樹冠に高さ方向の厚みがあり、樹種に適した輪郭になっているか
- 単木の確認に加え、SeasonalTreesDemoで群生状態とLOD切替を確認する

ローカル運用の `.verify/local-unity-trees` で検証後、レビューProjectを閉じた状態で専用コンテナから `bash .github/verify/sync-tree-review.sh` を実行すると、既存GUIDを照合して配布Sampleと `.verify/local-unity-vine` に同期します。コンテナにマウントするのは本リポジトリのみです。
