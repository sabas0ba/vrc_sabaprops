# 独自modelへの適用

## Mesh channel

`SabaProps/Soft Surface`は次を前提にします。

- local +Yが押される表面の外向き法線
- `COLOR.r`: 変形mask。0は固定、1は完全に変形
- `COLOR.g`: ambient occlusion係数。0で暗く、1で変化なし
- 上面に十分なsubdivisionがある

generatorのrounded boxは端部の`COLOR.r`を0へ落とします。独自Meshでもside／縫い目と共有する境界を固定し、変形面と非変形面の亀裂を防いでください。

## Component構成

1. MeshRendererへ`SabaProps/Soft Surface`を設定する
2. 同じGameObjectにBox型`VRCContactReceiver`を追加し、上端を見た目の表面から約12 mmにする
3. Collision Tagsへ`Head`、`Torso`、`Hand`、`Foot`、必要なら`Finger`を設定する
4. 同じGameObjectに`SoftSurfaceContactController`をUdonSharp componentとして追加する
5. `targetRenderer`、`surfaceTransform`、`surfacePlaneY`を設定する

World ContactのeventはReceiverと同じGameObject上のUdonBehaviourに送られます。Receiverをchildへ分ける場合はcontrollerも同じchildへ置いてください。

棒／板の形状別footprintを使うworld objectには、Sender tagとしてそれぞれ`SoftProbeRod`／`SoftProbePlate`を設定します。寸法はcontrollerの`Rod Half Length / Radius`または`Plate Half Length / Width`とSender componentの値を一致させてください。点接触は`SoftProbeFinger`を使用します。

## Collider

変形shaderはColliderを変更しません。歩行面では安定性を優先してrest poseの上面にBoxColliderを残します。寝転びanimationを使用するstationでは、avatar poseと見た目の沈み込みが一致するようCollider上面を2～4 cm下げる調整が可能です。
