# SabaProps Liquid

ワールド側から、アバターとワールドの表面へ液体の付着を描画する Udon パッケージです。
水、塗料、泥、粘性の高い液体などが表面に付着し、垂れ、乾き、洗い流される様子を
Projector で描画します。

- アバター（ローカル／リモート／鏡像）とワールド表面の両方に付着を描画
- 液体の見た目はマテリアルとプロファイルで定義
- プール、海、雨、シャワー、水鉄砲、筆、ペンなどの発生源（Source）から付着と洗浄を行う
- 対象は PC のみ

現在は開発中です。アバターの全身に付着を描く Body Canvas と、浸漬（プール・泥沼）、
シャワー・水道、水鉄砲の Source、それらを並べたサンプルシーンを含みます。
ワールド表面への付着はまだ含みません。

## サンプルシーン

`Tools > SabaProps > Liquid > Create Sample Scene` で、`Assets/SabaProps/Liquid/Samples/LiquidDemo.unity` に
次のワールドを生成します。VRChat Worlds SDK の `VRCSceneDescriptor` とスポーン地点を含み、そのままアップロードできます。

| 場所 | 内容 |
|---|---|
| 左 | プール。スロープから入ると、浸かった高さまで濡れ、出ると上から乾いていきます |
| 右 | 泥沼。泥の表面より床が 0.45 m 低く、足が沈みます。沈んだ高さまで泥が付き、水で洗うまで残ります |
| 奥 | シャワー。Interact で放水を切り替えます。当たった所から下が濡れ、泥が洗い流されます |
| 右手前 | 水道。手を差し出すと手が濡れます。Interact で開閉します |
| 左手前 | 水鉄砲 2 丁。持って使用ボタンを押している間放水し、命中は全員に同期されます |
| 正面奥 | 鏡。自分のアバターへの付着を確認できます |
方針と制約は [設計](Documentation~/design.md) にまとめています。

---

## 導入

VCC でこのパッケージを追加すると、依存する VRChat Worlds SDK も一緒に解決されます。

## 構成

| コンポーネント | 役割 |
|---|---|
| `LiquidBodyCanvas` | 1 人のプレイヤーの全身に付着を描く。体の動きに追従し、付着を RenderTexture に蓄える |
| `LiquidCanvasPool` | 付着の入力を受けたプレイヤーに Body Canvas を割り当てる |
| `LiquidProfile` | 液体の定義。顔料の色と量、液膜の量・平滑度・粘性・乾燥時間、洗浄の強さ |
| `LiquidImmersionVolume` | 浸漬の Source。トリガーに入ったプレイヤーを液面の高さまで濡らす・汚す。波の上下に対応 |
| `LiquidShower` | 流下の Source。固定シャワー、水道、手に持つシャワーヘッド。着水点より下を濡らして洗う |
| `LiquidWaterGun` | 遠距離の流下の Source。所有者が命中を判定し、全員へ送る |

Hierarchy の `SabaProps > Liquid` から配置できます。

| メニュー | 配置されるもの |
|---|---|
| Canvas Pool | Canvas 12 個分のプール。Canvas ごとの Projector マテリアルは `Assets/SabaProps/Liquid/Materials` に生成されます |
| Pool Volume (Water) / Mud Volume | 上面を液面とするトリガーの箱 |
| Shower | 高さ 2.2 m の下向きのシャワー。Interact で放水を切り替えます |
| Water Gun | Pickup の水鉄砲。使用ボタンを押している間放水します |

Source を配置すると、シーンにプールが無ければ作成し、水と泥のプロファイルを
`Liquid Profiles` の下にまとめて作成します。
