# 配置・編集 UI 操作ガイド

SabaProps の配置 UI で、草地、ツタ、根茎パッチ、単木、樹木フィールドを Scene へ配置し、
生成後に調整する手順をまとめます。UI の既定言語は日本語です。各配置ウィンドウ上部の
`表示言語 / UI Language` で英語へ切り替えられ、選択はウィンドウ間で共有されます。

## 入口

Trees パッケージを導入している場合は、`Window > SabaProps > Placement` を共通の入口として
使用します。Foliage 単体では、次のウィンドウを直接開けます。

| 目的 | メニュー |
| --- | --- |
| 草地を配合・プレビューして配置 | `Window > SabaProps > Foliage Palette` |
| ツタ・根茎パッチを Collider 上へ配置 | `Window > SabaProps > Placement > Surface Growth` |
| 草地を少ない項目で作成 | `GameObject > SabaProps > Placement > Foliage Field` |
| 選択位置を基準に個別作成 | `GameObject > SabaProps > Placement` |

共通配置ウィンドウを開く前に Hierarchy 上の GameObject を選択すると、選択内容を次のように
初期値へ取り込みます。

- `TreeSpecies` を選択している場合は `樹種 / Tree Species` へ設定します。
- Collider を持つ GameObject は `対象 Collider / Selected Collider` へ設定します。
- それ以外の GameObject は `Hierarchy 親 / Hierarchy Parent` へ設定します。

配置先を整理したい場合は、Scale が `(1, 1, 1)` の空 GameObject を作成し、`Hierarchy 親` に
指定してください。特に Surface Growth はメートル単位の値を使うため、拡大縮小された親を
指定するとガイド長や葉間隔も拡大縮小されます。

## Foliage Palette で草地を配置する

### 1. 編集方法を選ぶ

`編集 / Editing` の `モード / Mode` には次の 2 種類があります。

| モード | 用途 | 既存 Scene への影響 |
| --- | --- | --- |
| `作業用コピー / Working Copy` | 今回の配置専用に形状を調整する | 配置時に新しい Species asset を保存するため、既存 Field は変わりません |
| `アセットを直接編集 / Direct Asset` | 共通 Species asset 自体を調整する | 同じ Mesh を共有する GPU Instanced Field へ反映されます。Merged Chunks Field は再度 `Generate` が必要です |

通常は `作業用コピー` を使用します。同じ Species の変更を複数 Scene へ一括反映したい場合だけ、
変更範囲を確認して `アセットを直接編集` を使用してください。

### 2. 種の配合と形状を決める

1. `配合 / Composition` で使用する Species のチェックを有効にします。
2. Weight を調整します。右端の割合表示は、有効な Species 内での出現比率です。
3. Species 名を押して編集対象にします。
4. `形状パラメータ / Parameters` を変更し、`プレビュー / Preview` で確認します。

密度は Field 全体の個体数、Weight はその個体数を Species 間で分ける比率です。Weight を
増やしても Field 全体の密度は増えません。

### 3. スタンプ範囲を決める

`スタンプ範囲 / Stamp Range` で矩形または円形を選び、寸法または半径を入力します。
`2 / 5 / 10 / 20 m` は、矩形では正方形の一辺、円形では直径として適用されます。
`概算 / Estimated` は現在の範囲と密度から求めた生成予定個体数です。

Scene View 上で範囲を決める場合は、`Scene View で範囲を編集 / Edit Range in Scene View` を
有効にします。

### 4. Scene View へスタンプする

1. `Scene View でスタンプ配置 / Stamp in Scene View` を押します。
2. Scene View 上でマウスを動かし、ワイヤーフレームの配置範囲を確認します。
3. 左クリックで Field を配置します。配置モードは継続するため、続けて複数配置できます。
4. `Esc` または `配置を終了 / Stop Scene Placement` で終了します。

| 配置中の操作 | 結果 |
| --- | --- |
| `Space` | プレビュー位置を固定します。再度押すと地表追従へ戻ります |
| 固定中に Scene handle をドラッグ | 矩形の X/Z または円の半径を変更します |
| 左クリック | 現在の位置と範囲で Field を配置します |
| `Esc` | 配置モードを終了します |
| `Alt` を押しながら操作 | Unity 標準の Scene View カメラ操作を優先します |

正確な座標へ 1 つだけ置く場合は、Scene View の Pivot を合わせて
`Scene Pivot に配置 / Place at Scene Pivot` を使用します。

`配置時に生成 / Generate on Placement` が有効なら配置直後に Mesh を生成します。無効の場合は
Field を選択し、Inspector の `Generate` を押してください。

## 簡易 Foliage Field

共通配置ウィンドウの `簡易フィールド... / Quick Field...`、または
`GameObject > SabaProps > Placement > Foliage Field` を使用します。

1. Species と Weight を選択します。
2. 矩形／円形、寸法、密度、Seed を設定します。
3. 必要に応じて Skinned Ground、高度制限、Density Mask を指定します。
4. `作成時に生成 / Generate on Creation` を選び、`作成 / Create` を押します。

細かい配置範囲を Scene View で調整する場合は、作成後に Field を選択して緑色の範囲 handle を
操作します。

## Surface Vine／Rhizome Patch を配置する

### 1. 対象面を設定する

1. Collider を持つ床、壁、または斜面を選択します。
2. `表面植生の配置を開く / Open Surface Growth Placer` を押します。
3. `種類 / Type` で `Surface Vine` または `Rhizome Patch` を選びます。
4. `主 Collider / Primary Collider` を確認します。

床から斜面、斜面から壁のように経路が別 Collider へ続く場合は、
`隣接 Collider を追加 / Add Adjacent Collider` で接続先をすべて登録します。経路生成時は主面と
隣接面から最も近い投影先を選びます。

### 2. 初期形状と配置位置を決める

Surface Vine では Botanical Preset、初期成長方向、Guide Length を指定します。
`World Start` は生成開始位置です。`Scene View / 表面位置を使用 / Use Scene View / Surface Point`
を押すと、対象面を基準に開始位置を設定します。

`作成時にビルド / Build Immediately` が有効なら、`シーンに作成 / Create in Scene` の直後に
Mesh まで生成します。無効の場合は、作成された Component の Inspector で
`Build / Rebuild` を押してください。

### 3. 作成後にガイドを調整する

作成した Surface Vine または Rhizome Patch を選択すると、Scene View にガイド点が表示されます。
Position handle でガイド点を動かすと、生成済みの場合は短い待ち時間の後に Mesh も更新されます。
対象 Collider からガイドが離れすぎて生成できない場合は、ガイドを面へ近づけるか
`Projection Distance` を増やしてください。

## 樹木を配置する

Trees パッケージの共通配置ウィンドウで `樹種 / Tree Species` を指定します。

- `Scene Pivot に単木を配置 / Place One Tree at Scene Pivot`: 生成済み 3 段階 LOD を持つ単木を配置します。
- `Scene Pivot に樹木フィールドを作成 / Create Tree Field at Scene Pivot`: 寸法と密度を指定して Tree Field を作成します。
- `作成時にビルド / Build Immediately`: Tree Field 作成直後に樹木群を生成します。

既定 asset がない場合は `既定の広葉樹を選択 / 作成 / Select / Create Default Broadleaf` で
開始できます。樹種を変更したい場合は `TreeSpecies` の Inspector で preset を適用し、
`Rebuild LOD Meshes` を実行します。

## 生成後の編集と自動再生成

Field／Surface Growth の Inspector にある `値変更時に自動再生成 / Auto Rebuild on Changes` は
既定で有効です。

| Component | 初回生成 | 自動更新される操作 | 削除 |
| --- | --- | --- | --- |
| `FoliageField` | `Generate` | Inspector の値、Scene View の範囲 handle、Undo/Redo | `Clear` |
| `TreeField` | `Generate` | Inspector の値、Undo/Redo | `Clear` |
| `SurfaceVine` | `Build / Rebuild` | Inspector の値、ガイド点、preset、Undo/Redo | `Clear` |
| `RhizomePatch` | `Build / Rebuild` | Inspector の値、ガイド点、Undo/Redo | `Clear` |

自動再生成は生成済みの内容だけを対象にし、変更後 0.4 秒間の操作を一回へまとめます。未設定の
Component を勝手に初回生成することはありません。

多数の樹木や草をまとめて編集するときは、自動再生成を一時的に無効にしてください。複数項目を
変更した後に `Generate` または `Build / Rebuild` を一回実行すると、編集途中の重い再生成を
避けられます。

自動再生成された hierarchy／Mesh は authoring 値から導出される出力として扱われ、余分な Undo
履歴を作りません。手動の Generate／Clear は従来どおり Undo できます。

## VRChat World ビルド前の扱い

`FoliageField`、`FoliageChunk`、`SurfaceVine`、`RhizomePatch`、`TreeField` は Unity Editor で
生成条件を保持する authoring／marker Component です。Scene 上に表示されたままで問題ありません。

- 各 Component は `DontSaveInBuild` により World ビルドから自動除外されます。
- ビルド前に Component を手動削除する必要はありません。
- 生成済みの GameObject、`MeshFilter`、`MeshRenderer`、`LODGroup` はビルドへ残ります。
- UdonSharp への置き換えや追加設定は不要です。

Component を手動削除すると、その後の範囲・密度・形態調整と再生成ができなくなるため、Scene の
authoring 情報として残してください。

## 関連資料

- [パラメータと見た目の対応](parameters.md)
- [トラブルシューティング](troubleshooting.md)
- [実装ロードマップと設計上の線引き](roadmap.md)
