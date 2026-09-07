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

World objectの接触開始をCollider形状に合わせる場合は、対象面の`Probe Colliders`へSphereCollider／CapsuleCollider／BoxColliderを登録します。`Probe Kinds`を同じ順序で0（指）／1（棒）／2（板）に設定します。最大8slotのうち登録数分を予約し、残りをavatar用に使用します。Senderは不要です。

棒／板のfootprint寸法は`Rod Half Length / Radius`または`Plate Half Length / Width`で設定します。底面距離はColliderから計算しますが、footprintは軽量な形状近似です。棒はY軸のCapsuleColliderを表面と平行に置く使い方、板はBoxColliderを平行に置く使い方を想定します。負のscale、shear、MeshColliderは対応範囲外です。

Avatarと同じContact経路を使用するworld objectには`SoftProbeFinger`／`SoftProbeRod`／`SoftProbePlate`のSender tagを設定できます。ただし、この経路では初回接触点の追跡による近似となり、登録Colliderと同等の接触精度にはなりません。

## Collider

変形shaderはColliderを変更しません。歩行面では安定性を優先してrest poseの上面にBoxColliderを残します。寝転びanimationを使用するstationでは、avatar poseと見た目の沈み込みが一致するようCollider上面を2～4 cm下げる調整が可能です。

立位荷重を使う面は、非Triggerの支持Colliderとcontrollerを同じGameObjectへ配置し、`Player Standing Load`を有効にします。足元の判定範囲は`Surface Half Size`、高さは`Surface Plane Y`です。足元と未変形面の高さの差が35 mmを超えると立位として扱いません。
