# Trees 要素別リファレンス

![春夏の桜、シラカバ、夏秋のイチョウ](images/seasonal-overview.png)

画像はUnity 2022.3.22f1で実際に描画したものです。各Presetは`Mesh Seed`、樹冠、分枝、葉序、葉形、樹皮色をまとめて設定します。

## ケヤキ

![Treesの樹冠と枝葉の実描画例](images/sakura-summer.png)

| 項目 | Preset値の特徴 |
| --- | --- |
| 樹冠 | 街路樹向けの丸い樹冠 |
| 分枝 | 上向きの螺旋枝 |
| 葉 | 互生する小型広葉 |
| 主な調整 | `Crown Shape`、`Crown Width Scale`、`Branch Angle` |

## イロハモミジ

![広葉樹の樹冠と枝葉の実描画例](images/sakura-summer.png)

| 項目 | Preset値の特徴 |
| --- | --- |
| 樹冠 | 低い位置から広がる層状樹冠 |
| 分枝 | 対生分枝 |
| 葉 | 5裂の掌状葉 |
| 主な調整 | `Crown Shape`、`Trunk Branch Start`、`Leaf Shape` |

## スギ

![Trees Demoの樹種比較](images/seasonal-overview.png)

| 項目 | Preset値の特徴 |
| --- | --- |
| 樹冠 | 主幹優勢の円錐形 |
| 分枝 | 水平からやや下垂する輪生枝 |
| 葉 | 輪生状の短い針葉 |
| 主な調整 | `Apical Dominance`、`Whorl Size`、`Branch Droop` |

## シラカバ

![シラカバの幹と枝の接合部](images/birch-junction.png)

| 項目 | Preset値の特徴 |
| --- | --- |
| 樹冠 | 中心主幹と細い開出枝 |
| 分枝 | 先端ほど下垂 |
| 葉・樹皮 | 互生する広葉、白い幹と褐色の若枝 |
| 主な調整 | `Bark Root Color`、`Bark Tip Color`、`Tip Upturn` |

## アカマツ

![Trees Demoの樹種比較](images/seasonal-overview.png)

| 項目 | Preset値の特徴 |
| --- | --- |
| 樹冠 | 上部に偏る開いた樹冠 |
| 幹 | 曲がりを持つ主幹 |
| 葉・樹皮 | 2本束の長い針葉、橙赤色の樹皮 |
| 主な調整 | `Crookedness`、`Branch Arrangement`、`Leaf Length` |

## ヒノキ

![Trees Demoの樹種比較](images/seasonal-overview.png)

| 項目 | Preset値の特徴 |
| --- | --- |
| 樹冠 | 主幹優勢の円錐形 |
| 分枝 | 水平な輪生枝、末端のみ下垂 |
| 葉 | 対生する鱗片葉 |
| 主な調整 | `Apical Dominance`、`Branch Droop`、`Leaf Shape` |

## ソメイヨシノ

![夏のソメイヨシノの樹冠](images/sakura-summer.png)

| 項目 | Preset値の特徴 |
| --- | --- |
| 樹冠 | 太い枝が横へ広がる丸い樹冠 |
| 春 | 葉より先に付く淡桃色から白色の5弁花 |
| 夏 | 互生する卵形の緑葉 |
| 主な調整 | `Crown Radial Scale`、`Leaf Shape`、季節別の色 |

## イチョウ

![秋のイチョウの枝葉](images/ginkgo-autumn.png)

| 項目 | Preset値の特徴 |
| --- | --- |
| 樹冠 | 直立する主幹と円錐状の樹冠 |
| 葉 | 短枝にまとまる扇形葉 |
| 季節 | 夏の緑色、秋の黄色 |
| 主な調整 | `Apical Dominance`、`Leaves Per Tip`、季節別の色 |

共通パラメータの作用と調整順は[樹木の調整・確認ガイド](tree-authoring.md)を参照してください。
