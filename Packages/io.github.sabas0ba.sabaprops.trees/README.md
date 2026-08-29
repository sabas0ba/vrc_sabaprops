# SabaProps Trees

VRChat ワールド向けのプロシージャル樹木ジェネレータです。互換用の広葉樹、針葉樹、
枯れ木、砂漠低木 archetype に加え、実在種を根拠にした樹冠・分枝・葉序の preset を生成します。

このパッケージは `io.github.sabas0ba.sabaprops.foliage` 0.4.0 に依存します。
Foliage の shader と風チャンネル契約を共有しますが、草向けの Distance Shrink と
影 OFF の既定値は使いません。

## 使い方

Package Manager で `SabaProps Trees` を選び、`Samples > Trees Demo > Import` を実行すると、
10 preset と各 3 段階 LOD を生成済みの 3 シーンを確認できます。`TreesDemo.unity` は
ケヤキ等の混交林、スギ・ヒノキの植林、ケヤキ街路樹、`SeasonalTreesDemo.unity` は
シラカバ林、ソメイヨシノの春／夏の並木、イチョウの夏／秋の並木を収録しています。
`ForestLoadDemo.unity` は 64 本単位の 3 群を収録し、群を順番に有効化して 64 / 128 / 192 本の
描画負荷、樹冠の重なり、LOD 遷移を同じカメラ条件で比較できます。
高さと向きに固定Seedの個体差を加え、各樹種のLOD Meshを全個体で共有しています。Demo は
各 preset の既定分岐予算、樹冠、枝序、葉序、風応答をそのまま焼き込んでいます。

1. `Tools > SabaProps > Trees > Create Default Assets` を実行します。10 preset と枯れ木・低木を作成します
2. `Assets/SabaProps/Trees/Species/` から `TreeSpecies` を選びます
3. Inspector で構造・葉・LOD を調整し、`Rebuild LOD Meshes` を押します
4. `Create LOD Group in Scene` を押すか、Hierarchy の
   `GameObject > SabaProps > Tree LOD Group` を実行します

既存の LOD Mesh は同じ GUID のまま更新されます。シーン参照は失われません。
Mesh の AssetDatabase 書き出しは Unity Undo の対象外です。Scene に生成した
`LODGroup` は Undo できます。

## 複数個体の配置

1. Hierarchy の `GameObject > SabaProps > Tree Field` を実行します
2. `Species` に配置する `TreeSpecies` と、必要ならフィールド固有の Weight を設定します
3. エリア、密度、地面レイヤー、最小間隔などを調整して `Generate` を押します
4. 再生成前の結果を削除する場合は `Clear` を押します

`TreeField` は Foliage 0.4.0 の共有サーフェス散布 API を利用します。矩形／円形、固定
Seed、地面へのレイキャスト、高度制限、除外レイヤー、Density Mask の挙動は
Foliage Field と共通です。種の選択、傾斜制限、最小間隔、スケールと姿勢だけを
Trees 側のポリシーとして追加します。

LOD Mesh は Species ごとに 3 個だけ生成し、すべての個体で共有します。Scene には
通常の `LODGroup` と `MeshRenderer` を焼き込むため、ビルド後のワールドで C# は
実行されません。生成された Scene 階層は Unity Undo の対象ですが、Mesh アセットの
書き出しは Undo の対象外です。

## 生成モデル

- 幹は動かさず、一次枝ごとに UV3 の wind pivot を持ちます
- 子孫枝は一次枝の pivot と接続点の bend 値を継承するため、風で継ぎ目が開きません
- `UV3.xyz` は object-space pivot、`UV3.w` は stiffness、`UV0.y` は bend です
- Seed が同じなら頂点・index・LOD の生成結果は同じです
- LOD1/LOD2 は再帰深度、断面数、枝分岐数、葉数を段階的に減らします
- 一次枝と子枝は `Spiral` / `Opposite` / `Whorled` / `Irregular` の枝序で配置します
- `Crown Shape` が高さごとの枝長包絡を、`Apical Dominance` が主幹と側枝の比率を決めます
- `Crown Density` は再帰深度を変えずに主枝層を増減し、`Foliage Depth` は葉を付ける末端側の枝階層数を決めます
- 構造枝は水平より下へ向けず、`Branch Droop` は細い末端枝だけへ適用します。`Tip Upturn` は先端の上向きを加えます
- 子枝半径は分岐数の平方根で上限を設け、親より太い末端枝が生じないようにします
- 樹皮色は幹から枝次数の高い若枝へ補間し、葉は互生・対生・輪生・2 針束を分けます
- 親枝の終端と継続枝は同じ位置・半径で接続し、非終端の切断面を作りません

### 風

`Wind Enabled` は Species 単位の風応答を切り替え、`Wind Response` は共有 Material の風に
対する倍率を設定します。変更後は `Rebuild LOD Meshes` が必要です。OFF の場合は UV3.w を
0 として焼き込むため、実行時スクリプトや Species ごとの Material は増えません。
`Branch Stiffness` と `Leaf Stiffness` は枝と葉の相対的な応答量です。全体の風向、速度、強度は
Material の `Direction`、`Speed`、`Strength` で設定します。

## 実在種 Preset

| Preset | 構造 | 葉・樹皮 |
|---|---|---|
| Japanese Zelkova / ケヤキ | 若木の箒状・花瓶状樹冠、上向きの螺旋枝 | 互生する小型広葉、灰褐色から若枝色 |
| Japanese Maple / イロハモミジ | 低い位置から広がる層状樹冠、対生分枝 | 対生する 5 裂の掌状葉 |
| Japanese Cedar / スギ | 主幹優勢の円錐樹冠、水平からやや下垂する輪生枝 | 輪生状の短い針葉、赤褐色樹皮 |
| Japanese White Birch / シラカバ | 中心主幹と細い開出枝、先端ほど下垂 | 互生する小型広葉、白い幹から褐色若枝 |
| Japanese Red Pine / アカマツ | 曲がる幹、上部に偏る疎な輪生枝、開いた樹冠 | 2 本束の長い針葉、灰色の根元から橙赤色樹皮 |
| Hinoki Cypress / ヒノキ | 主幹優勢の円錐樹冠、水平な輪生枝、細い末端だけが小さく下垂 | 対生する鱗片葉、暗緑色の葉、赤褐色樹皮 |
| Somei Yoshino Spring / ソメイヨシノ・春 | 太い枝が横へ広がる丸い樹冠 | 葉より先に付く淡桃色から白色の 5 弁花 |
| Somei Yoshino Summer / ソメイヨシノ・夏 | 春と同じ Seed・枝構造 | 互生する卵形の緑葉 |
| Ginkgo Summer / イチョウ・夏 | 直立する主幹と円錐状の樹冠 | 短枝にまとまる緑色の扇形葉 |
| Ginkgo Autumn / イチョウ・秋 | 夏と同じ Seed・枝構造 | 黄色く紅葉した扇形葉 |

形態の根拠は、North Carolina State University Extension の
[ケヤキ](https://plants.ces.ncsu.edu/plants/zelkova-serrata/common-name/japanese-zelkova/)、
[イロハモミジ](https://plants.ces.ncsu.edu/plants/acer-palmatum/)、
[スギ](https://plants.ces.ncsu.edu/plants/cryptomeria-japonica/)、
[シラカバ](https://plants.ces.ncsu.edu/plants/betula-platyphylla-var-japonica/)、
[アカマツ](https://plants.ces.ncsu.edu/plants/pinus-densiflora/)の記載に基づきます。
ヒノキ、ソメイヨシノ、イチョウの追加形態は森林総合研究所の
[ヒノキ](https://www.ffpri.go.jp/kys/business/jumokuen/jumoku/zukan/hinoki.html)、
[ソメイヨシノ](https://www.ffpri.go.jp/kys/business/jumokuen/jumoku/zukan/someiyosino.html)、
[イチョウ](https://www.ffpri.go.jp/kys/business/jumokuen/jumoku/zukan/ityou.html)を参照しています。
生成器は種同定用の精密模型ではなく、低ポリゴンの樹冠シルエットと分枝差を目的とします。

`Apply Botanical Preset` は現在選択中の実在種 preset でパラメータを置き換えます。
`Apply Archetype Preset` は従来の汎用形へ戻します。実行前の `TreeSpecies` は Undo できます。

## 現在の範囲

0.1.0 は 1 個体の生成と `TreeField` による複数個体の Scene 配置を対象にします。
ブラシで塗る配置、結合メッシュ出力、実行時の動的生成は対象外です。
