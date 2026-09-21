# 配置と同期の設定

## 静止した机・壁に置く

1. 対象面に空の GameObject と `PlacementSurface` を配置します。ローカル XZ を吸着面とし、+Y を物を置く側に向け、`size` に有効な矩形寸法をメートル単位で設定します。壁面では面の Transform を回転します。
2. Prop に `Rigidbody`、`VRCPickup`、`VRCObjectSync`、`PlacementSolver`、`ObjectSyncPlacement` を配置します。`PlacementSolver.surfaces` に候補面を登録し、`ObjectSyncPlacement.solver` と `contact` を指定します。`contact` は Prop 自身、または底面・背面に置いた子 Transform です。+Y を Prop の内側へ向けます。
3. `category` と `acceptedCategories` に共通ビットを設定します。机用と壁用を別ビットにすると、誤った面への吸着を防げます。
4. `normalKinematic` / `normalGravity` に、Prop を再取得したときの通常状態を設定します。吸着後は owner が Object Sync を介して kinematic にし、再取得時にこの値へ戻します。

`PlacementSurface` の Collider は吸着計算に使用しません。Collider が必要なのは、物理的な支持や接触も再現したい場合です。デモの机と椅子は、プレイヤーを通過させるため Collider を `Walkthrough` Layer にしています。

| 設定 | 用途 |
| --- | --- |
| `maximumDistance` | 面の表側から吸着を認める最大距離 |
| `maximumPenetration` | 面の裏側への許容侵入量 |
| `maximumTilt` | 接触基準 +Y と面法線の許容角度 |
| `footprintRadius` | 接触点を中心に面内へ収まる必要がある半径 |
| `acceptedCategories` / `category` | 共通ビットがある Prop と面だけを対応させるマスク |

同距離の候補では `surfaces` の配列順が優先されます。Transform 階層には正の一様スケールを使います。接触基準は 1 回の計算につき 1 点です。Prop 同士の重なり判定は行いません。

## おぼん・皿・料理を追従させる

親 Prop は通常の Pickup と Object Sync を持ったまま、その子 Transform に `PlacementSurface` を追加します。面の `carrier` には親 Prop の `ObjectSyncPlacement` を指定します。子 Prop も独立した Pickup と Object Sync を持ち、`PlacementSolver.surfaces` に親の面を登録します。机へ置き直す場合は、机の面も候補に追加します。

子 Prop のさらに下に、Collider を持たない GameObject と `PlacementFollowState` を追加し、`placement` に子 Prop、子 Prop の `ObjectSyncPlacement.followState` にこの state を指定します。初期状態から親へ置く場合、`surfaceIndex` に親の面の候補番号を入れ、`localPosition` と `localRotation` に親から見た子の姿勢を設定します。未接続の初期状態は `surfaceIndex = -1` です。

親の `ObjectSyncPlacement.carriedItems` には、持ち上げたとき所有権を移す子孫をすべて登録します。おぼんなら皿・カトラリーに加えて皿上の料理、皿なら料理を登録します。接続の循環を作らず、連鎖は 16 段以下、一様スケール 1 で構成します。[Kitchen Demo](demo-review.md)に完成済みの設定例があります。

子を面へ置くと、子の owner が相対姿勢を計算して接続先を保存します。親を拾った owner は接続中の子孫の所有権を取得し、各子の owner が毎フレーム姿勢を更新します。位置・回転と kinematic 状態の配信はそれぞれの `VRCObjectSync` に任せます。接続先の候補番号と相対姿勢だけは、別 GameObject の `PlacementFollowState` で Manual 同期します。所有権移行中の変更は取得後にシリアライズします。

`PlacementSurface`、`PlacementSolver`、`ObjectSyncPlacement` は `NoVariableSync` です。Manual 同期を使う `PlacementFollowState` は Object Sync のある GameObject と分けてください。親を別スクリプトで移動させる場合も、親の owner と子孫の owner がそろうようにしてください。

## 既存の UdonSynced 制御へ組み込む

既存の制御が位置・回転を同期している Prop では `ObjectSyncPlacement` を追加せず、`PlacementSolver.TryFindPose(item, contact, category)` を計算器として呼びます。戻り値が `true` の場合、`resultPosition`、`resultRotation`、`resultSurface` を既存の姿勢・同期変数へ反映します。失敗時には結果が消去されます。

ドロップ確定後に計算し、反映直前に owner と未保持状態を再確認します。Manual 同期なら同期変数を持つ Behaviour から `RequestSerialization()` を呼び、補間用の目標値も更新します。保持状態や親子接続を独自に管理する場合は、途中参加・owner 退出後に復元できる情報をその制御の同期データへ含めてください。

## リセットと検証

Respawn や Pool の処理で Prop を戻す前に、owner 上で `ObjectSyncPlacement.CancelAndRestorePhysics()` を呼びます。無効化だけでは吸着後の静止状態は解除しません。同じ Prop の姿勢・kinematic・gravity を複数のスクリプトから同時に更新しないでください。

Unity EditMode テストは幾何計算、同梱 Scene の参照、追従姿勢、UdonSharp コンパイルを確認します。複数人での所有権変更、通信遅延、途中参加は VRChat 実機で[デモの確認表](demo-review.md)に従って確認してください。[ローカル検証環境](../../../.github/verify/vrchat/PUT_ITEMS.md)に再現手順を記載しています。
