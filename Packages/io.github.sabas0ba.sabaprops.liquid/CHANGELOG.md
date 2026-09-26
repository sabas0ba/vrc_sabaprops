# Changelog

このパッケージの変更点をまとめています。
フォーマットは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に、
バージョニングは [Semantic Versioning](https://semver.org/lang/ja/) に従います。

## [Unreleased]

### Added

- `LiquidBodyCanvas`: プレイヤーの全身に液体の付着を描く Body Canvas。
  腰・胸・太腿の位置から体の座標系を組み、6 面アトラスの RenderTexture に顔料と液膜を蓄える。
  重力方向への流下、液膜の蒸発、液面より下への浸漬による付着に対応
- `LiquidCanvasPool`: 付着の入力を受けたプレイヤーへ Canvas を割り当てるプール
- `LiquidProfile`: 液体の定義
- Canvas 更新用の Blit シェーダと、アバターへ描画する Projector シェーダ
- Canvas Pool を生成する GameObject メニュー
- `LiquidImmersionVolume`: プール・浴槽・海・水たまり・泥沼の浸漬 Source。波による液面の上下に対応
- `LiquidShower`: 固定シャワー・水道・手に持つシャワーヘッドの流下 Source。着水点より下を濡らして洗う
- `LiquidWaterGun`: 水鉄砲。所有者が命中を判定し、プレイヤー基準の座標で全員へ送る
- `LiquidCanvasPool.CastTargets`: 体をカプセルで近似した光線の命中判定
- `LiquidBodyCanvas.WashBelow`: 指定した高さより下の顔料の洗浄
- 水と泥のプロファイル、各 Source を配置する GameObject メニュー
- プール、泥沼、シャワー、水道、水鉄砲、鏡を並べたサンプルシーンの生成（`Tools > SabaProps > Liquid > Create Sample Scene`）
- 付着した面の奥行きの記録。同じ向きを向いた別の面（胴の側面と腕の外側など）に付着が写らないようにする
- 差し出した手への命中判定（シャワーと水道が手を濡らす）
- マネキン：固定の Transform に追従する Body Canvas。Source はプレイヤーとマネキンを同じ規則で扱う
- `LiquidSprayer`: サーバー時刻に合わせて液体を放ち続ける自動散布の Source（同期なし）
- `LiquidTurntable`: サーバー時刻に合わせて回る台
- `LiquidLighting`: ワールドの主光源を付着のシェーダへ渡す。顔料の拡散光と濡れたハイライトが付く
- 付着の厚みの勾配による法線、顔料の粒状のむら
- シャワーの自動運転（放水と停止を時刻で繰り返す）
- プリセット：ジュース、赤と青の塗料、スライム、シロップ
- サンプル `Liquid Demo World` を同梱。液体の比較と Source の比較の列を追加
- `LiquidSurfaceProfile`: 液体を受ける素材（柔らかい布、硬い布、革、髪、肌、樹脂）。吸水による暗化、撥水による水滴、艶、にじみ、毛束、流れにくさ
- 頭と手の位置から髪・肌・衣服の部位を推定し、部位ごとの素材で描く
- プリセット：黄の塗料、黒から白までの 5 段階の塗料
- デモに受け手の素材、体の色、液体の色の比較の列を追加
- `LiquidWeather`: 範囲に雨または雪を降らせる降下の Source。頭上の遮蔽、サーバー時刻による周期、
  風による雨粒の傾きに対応。雪は上を向いた面に積もり、止むと溶けて水になる
- `LiquidBodyCanvas.ApplySnow` / `ApplyRain`: 積雪の深さと、雨と溶けた雪で上を向いた面に残る水の量
- `SabaProps/Liquid/Weather Surface`: 天候に応じて地面を濡らし、水たまりと積雪を描くシェーダ
- Rain Area と Snow Area を配置する GameObject メニュー
- 部位の推定に下半身の衣服と靴を追加。プレイヤー用の Canvas は下半身を硬い布、靴を革とする
- 服を着たマネキン（Tシャツとジーンズ、雨合羽、ニットと短パン）
- デモに服を着た人型の列と、雨と雪の区画を追加
- 比較の列を別のシーン（`LiquidComparison.unity`）に分け、`Create Comparison Scene` メニューを追加
- `LiquidNozzle`: 1 回、定期、連続の放ち方と、量、距離、速さ、断面の設定を持つ汎用の Source。操作盤から設定を変えられる
- `LiquidHumidity`: 湿度の Source。乾きを遅らせ、結露と垂れる水滴を生じさせる。シャワーとの連動、湯気、霧、面の曇り
- `LiquidLightZone`: 暗い部屋の照明と紫外線を付着に伝える明かりの範囲
- 液体の蛍光と蓄光。Canvas に発光の割合の RenderTexture を追加
- プリセット：蛍光塗料（桃、緑）、蓄光塗料
- `LiquidUmbrella`: 雨と雪を遮る傘
- `LiquidButton`: Interact と Pickup の使用ボタンでイベントを呼ぶボタン
- 粘性に応じたパーティクル（伸びる粒と飛沫、塊、糸）と湯気のパーティクル
- シェーダ：霧の体積（`SabaProps/Liquid/Fog Volume`）、曇るガラス（`SabaProps/Liquid/Fogged Glass`）。
  `Weather Surface` に結露を追加
- Prefab：コップ、バケツ、水道、シャワー、傘、水鉄砲、ノズル台（`Prefabs`）と、配置する GameObject メニュー
- Source はプールが未設定なら名前で探す
- 操作と環境のシーン（`LiquidInteractive.unity`）と `Create Interactive Scene` メニュー
- `LiquidHumidity.assumeBareSkin`: 裸が自然な場所で、範囲内の体の衣服の部位を肌として扱う（`LiquidBodyCanvas.ApplyBareSkin`）
- 立った面を垂れる水滴。滴が時間とともに滑り落ち、細い筋を残す。水平な面では垂れない
- `LiquidNozzle` の押す間の放ち方と持ち運び。使用ボタン、離したとき、手放したときを `LiquidButton` で中継し、放出の状態を同期する
- Prefab：持ち運べる放水具（Liquid Spray Gun）
- デモ：サウナで体を肌として扱う設定と水着の人型、持ち運べるノズルでかけ合う区画
- ワールドのテスト：全 UdonBehaviour のシリアライズ済みプログラムが読み込めることを検査
- `LiquidResetPanel` と Prefab（Liquid Reset Panel）：自分、マネキン、全員の付着を全クライアントで消す。3 つのシーンのスポーン横に配置
- アバターが変わったプレイヤーの付着を消す（`LiquidCanvasPool.OnAvatarChanged`）
- 持ち運べるノズルに中身を示すタンク（液体の色、蛍光と蓄光は発光）。すべての液体のノズルを並べた棚をデモに追加

### Changed

- 顔料の付着を、中心が不透明で縁だけ薄くなる形に変更。黒や濃い色の塗料が半透明に見えていた
- 水鉄砲の同期変数をやめ、放水の開始と停止をイベントで送るように変更。VRCObjectSync と Manual sync の干渉を避けるため
- 付着した面の奥行きの許容値を 0.08 m から 0.12 m に変更
- 撥水面の水滴を、向きの異なる 2 層の格子と密度の粗密で置き、大きさの偏りと重力方向への
  伸びを付けるように変更。格子状に規則的に並んで見えていた
- 水滴の大きさのばらつきを広げ（まれに合わさった大粒）、重力方向への伸びを面の傾きに比例させた
- 垂れる滴の速さと離れるかどうかに乱数を持たせ、立った面ほど離れやすく速くした。全体に遅くし、
  水が十分溜まるまで垂れないようにした。流下の速さの既定値を 0.08 から 0.05 に、結露の水滴の頻度を下げた

### Fixed

- Projector を Player レイヤに置くように変更。Default に置くと、アバターだけを映すミラーに付着が映らなかった
- 顔料の色をリニア色空間へ変換してから Canvas に書くように修正。塗料や泥が白っぽく見えていた
- 頭頂や肩など上を向いた面で、垂れる水滴が上へ動くことがあった。上を向いたタイルでは垂らさず、
  立った面の滴はタイル全体で一定の向きに並べるように修正（丸い面で斜めに横切って見えていた）

## [0.0.1] - 2026-09-23

設計段階のパッケージとして追加しました。実行可能なコンポーネントは含みません。

### Added

- 設計文書（`Documentation~/design.md`）
