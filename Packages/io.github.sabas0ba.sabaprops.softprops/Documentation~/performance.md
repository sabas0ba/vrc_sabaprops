# 性能設計

## 1変形面あたりのcost

- World Contact Receiver: 1
- Udon behaviour: 1
- 動的Material instance: 1
- 接触slot: 8
- 接触中のMaterial write: 位置とfootprintの`Vector4`を各8個、既定30 Hz
- shader: vertexごとに最大8接触点。pixel側は通常のopaque surface shaderと織り目計算のみ

VRChatはworld内のactive Contact Sender／Receiver合計に1024個の上限を設けています。Sofaは6 Receiverを使用するため、同じSofaを100台置くと600 Receiverになります。通常の家具配置では上限よりshaderのvertex costが先に問題になります。

## 配置時の推奨

- 1 room内では変形面を32～48個程度までに抑える
- 遠距離から見える家具には`LODGroup`を追加し、低LODでは非変形mesh／Materialを使用する
- 触れられない展示物ではReceiverとUdonBehaviourを無効化し、非変形Materialへ置き換える
- `Update Rate`は通常30 Hz、柔らかく復元の遅い物では20 Hzまで下げられる
- `Contact Radius`を必要以上に大きくしない。半径自体はvertex演算回数を変えないが、同時に視認される変形範囲が増える

## 同期

接触slotをUdonSyncedにしないでください。8点を継続同期するとbandwidthとownership処理が増えます。World Contactsは各clientでavatar Senderを評価できるため、視覚効果はlocal reconstructionとします。
