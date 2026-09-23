# SabaProps Liquid

ワールド側から、アバターとワールドの表面へ液体の付着を描画する Udon パッケージです。
水、塗料、泥、粘性の高い液体などが表面に付着し、垂れ、乾き、洗い流される様子を
Projector で描画します。

- アバター（ローカル／リモート／鏡像）とワールド表面の両方に付着を描画
- 液体の見た目はマテリアルとプロファイルで定義
- プール、海、雨、シャワー、水鉄砲、筆、ペンなどの発生源（Source）から付着と洗浄を行う
- 対象は PC のみ

現在は開発中です。アバターの全身に付着を描く Body Canvas とその割り当てまでを含み、
付着を与える Source とサンプルシーンはまだ含みません。
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

Hierarchy の `SabaProps > Liquid Canvas Pool` から、Canvas 12 個分のプールを配置できます。
Canvas ごとの Projector マテリアルは `Assets/SabaProps/Liquid/Materials` に生成されます。
