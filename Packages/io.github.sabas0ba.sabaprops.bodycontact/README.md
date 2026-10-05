# SabaProps Body Contact

VRChat World内で、プレイヤーの頭・体幹同士の貫通を抑えるUdonコンポーネントです。
各クライアントが自分の位置を水平に補正します。独自の同期変数やネットワークイベントは使用しません。

## 対象と動作

- 自分側の判定点は頭・胸・腰の3点。VR・Desktop共通です。
- 相手側は頭の球と体幹のカプセルです。頭同士、頭と体幹、体幹同士を判定します。
- 手足・腕・脚の接触は移動補正しません。Grab・Pull・部位の姿勢操作は提供しません。
- 許容幅を超えた侵入だけを補正します。移動している側の負担を大きくし、双方静止時は分担します。
- スポーン時の重なりは離れるまで許容します。接触が一定時間続く場合も通り抜けを許可します。
- 移動先の壁を判定し、壁に阻まれる補正は取り消します。

許容幅、水平移動のみの補正、通信遅延、通り抜け機能があるため、完全な非貫通を保証するものではありません。

## 導入

VRChat Worlds SDKとUdonSharpを備えたUnityプロジェクトへパッケージを導入してください。
`GameObject > SabaProps > Body Contact System` でシステムを配置します。
`GameObject > SabaProps > Body Contact Dummy` で接触確認用のマネキンを追加できます。

### サンプルシーン

`Tools > SabaProps > Body Contact > Create Sample Scene` を実行します。
生成前にUdonをコンパイルし、`Assets/SabaProps/BodyContact/Samples/BodyContactDemo.unity` に保存します。
同じ保存先のシーンは再生成で置き換わるため、編集版は別名で保存してください。

床・壁・Spawn、外観付きのマネキン5体、Station、Gizmo・HUD・接触の切替ボタンを含みます。

| マネキン | 用途 |
| --- | --- |
| Dummy Open | 静止した標準体型との接触 |
| Dummy Wall | 壁際の補正 |
| Dummy Small | 0.6倍の体格との接触 |
| Dummy Moving | 全身の往復移動、±0.8 m、8秒周期 |
| Dummy Turning | 全身の旋回、±60度、8秒周期 |

動作型はAnimatorでルートを動かします。関節アニメーションではありません。
床は1 mの格子と10 cmの補助線です。表示用マネキンにはColliderを付けません。
最終配布では、シーンと必要なアセットを利用者が取り込めるサンプルとして同梱する予定です。現在は生成メニューによる提供です。

### Station

`Tools > SabaProps > Body Contact > Connect Scene Stations` でWorldのStationに停止用の補助コンポーネントを接続します。
着席中は接触補正を停止します。後からStationを追加した場合は再実行してください。
動的に生成するStationは生成元に `BodyContactStationRelay` とSource参照を設定します。
アバター内のStationは対象外です。手動の停止とStationの停止状態は独立しています。

## 設定

| 項目 | 既定値 | 内容 |
| --- | --- | --- |
| Contact Enabled | ON | 接触補正の有効化 |
| Tolerance | 0.03 m | 許容する侵入量 |
| Response Seconds | 0.03秒 | 補正の時定数 |
| Max Speed | 6 m/s | 補正速度の上限 |
| Pass Through Seconds | 2.5秒 | 接触継続による通り抜け許可。0以下で無効 |
| Radius Scale | 1 | 判定形状の太さ |
| Move Mode | 0 | 0: TeleportTo、1: SetVelocityによる比較用の移動 |
| World Collision Mask | Player/PlayerLocalを除外 | 壁判定のレイヤー |

## 診断表示

- 頭・体幹を緑、有効な通り抜け状態を黄、接触点を赤で表示します。
- 対象外の腕・脚の線は既定で非表示です。Show Inactive Limbsで灰色表示できます。
- Gizmoの形状更新は既定20 Hz、HUDの頭部追従と接触補正は毎フレームです。
- Spawn後方のGIZMO、HUD、CONTACTボタンでそれぞれをローカルに切り替えます。

| HUD | 内容 |
| --- | --- |
| head/body probes | 有効な判定点数。通常は3/3 |
| depth | 最大侵入量。許容幅内でも表示します |
| separation | 計算した水平補正量 |
| speed / step | 適用した補正速度／フレームあたりの移動量 |
| reversals | 直近1秒間の補正方向の反転回数 |
| blocked by world | 壁判定によって補正を取り消した状態 |
| pass-through | 重なりや接触継続で通り抜けを許可している対象数 |

## 外部イベント

| 対象 | イベント | 内容 |
| --- | --- | --- |
| BodyContactSystem | _Suspend / _Resume | 手動停止・再開 |
| BodyContactSystem | _ToggleEnabled | 接触処理の有効化切替 |
| BodyContactDebugView | _Show / _Hide / _ToggleVisible | Gizmoの線の表示切替 |
| BodyContactDebugView | _ToggleHud | HUD切替 |
| BodyContactDebugView | _ToggleInactiveLimbs | 対象外の四肢の参考表示切替 |

## 実クライアントでの検証

1. サンプルシーンをBuild & Testで起動し、マネキンへ頭・体幹を近づけて補正を確認します。
2. 手だけを押し込む・撫でる・握手する操作では補正されないことを確認します。
3. 壁際で補正が停止し、壁を抜けないことを確認します。
4. VR＋Desktopを同一インスタンスで起動し、頭・体幹同士の接触と静止時の反転を確認します。
5. 接触を2.5秒継続すると通り抜けが許可され、離れれば判定が再開することを確認します。
6. Station着席中の停止、降車後の再開を確認します。

この環境ではSDKのReload Your Last BuildでClients=2を指定して同一インスタンスへ入れています。
コードやシーンを更新した場合は、先に新しいビルドを作成してください。

## 制限

- 真上・真下の接触は水平移動では解消しません。
- VRの実際の頭の移動を物理的に止めることはできません。プレイヤーの位置補正として働きます。
- リモートの姿勢には遅延があります。視点の快適性は実機での調整が必要です。
- 非Humanoidの頭・体幹は標準体型で代替します。
- 同時に扱う対象はマネキンとリモートプレイヤーの合計16体までです。
- ClientSimではUdon実行によるHUD・Gizmo・切替を検査します。PostLateUpdateはテストから明示的に送り、実クライアントでの自動発火は別途確認が必要です。

## ライセンス

Apache-2.0。[LICENSE.md](LICENSE.md) を参照してください。
