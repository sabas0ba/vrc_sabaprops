# SabaProps Trees

VRChat ワールド向けのプロシージャル樹木ジェネレータです。広葉樹、針葉樹、枯れ木、
砂漠低木を 1 つの再帰枝ジェネレータのパラメータ差分として生成します。

このパッケージは `io.github.sabas0ba.sabaprops.foliage` 0.3.0 に依存します。
Foliage の shader と風チャンネル契約を共有しますが、草向けの Distance Shrink と
影 OFF の既定値は使いません。

## 使い方

1. `Tools > SabaProps > Trees > Create Default Assets` を実行します
2. `Assets/SabaProps/Trees/Species/` から `TreeSpecies` を選びます
3. Inspector で構造・葉・LOD を調整し、`Rebuild LOD Meshes` を押します
4. `Create LOD Group in Scene` を押すか、Hierarchy の
   `GameObject > SabaProps > Tree LOD Group` を実行します

既存の LOD Mesh は同じ GUID のまま更新されます。シーン参照は失われません。
Mesh の AssetDatabase 書き出しは Unity Undo の対象外です。Scene に生成した
`LODGroup` は Undo できます。

## 生成モデル

- 幹は動かさず、一次枝ごとに UV3 の wind pivot を持ちます
- 子孫枝は一次枝の pivot と接続点の bend 値を継承するため、風で継ぎ目が開きません
- `UV3.xyz` は object-space pivot、`UV3.w` は stiffness、`UV0.y` は bend です
- Seed が同じなら頂点・index・LOD の生成結果は同じです
- LOD1/LOD2 は再帰深度、断面数、枝分岐数、葉数を段階的に減らします

## Preset

| Archetype | 葉 | 構造 |
|---|---|---|
| Broadleaf | 幅広の葉 | 中程度の角度で広がる樹冠 |
| Conifer | 針葉 | 長い幹と水平寄りの枝 |
| Deadwood | 無し | 枝数が少なく歪みが大きい枯れ木 |
| DesertScrub | 無し | 低く強く分岐する砂漠低木 |

`Apply Archetype Preset` は現在のパラメータを preset で置き換えます。実行前の
`TreeSpecies` は Undo できます。

## 現在の範囲

0.1.0 は 1 個体の生成と Scene への LODGroup 配置を対象にします。複数個体の
スキャッタリングは Foliage 側の配置アルゴリズムを汎用化して共有する予定で、
Trees 側には重複実装しません。
