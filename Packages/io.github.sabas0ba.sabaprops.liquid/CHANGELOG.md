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

### Changed

- 顔料の付着を、中心が不透明で縁だけ薄くなる形に変更。黒や濃い色の塗料が半透明に見えていた
- 水鉄砲の同期変数をやめ、放水の開始と停止をイベントで送るように変更。VRCObjectSync と Manual sync の干渉を避けるため
- 付着した面の奥行きの許容値を 0.08 m から 0.12 m に変更

### Fixed

- Projector を Player レイヤに置くように変更。Default に置くと、アバターだけを映すミラーに付着が映らなかった
- 顔料の色をリニア色空間へ変換してから Canvas に書くように修正。塗料や泥が白っぽく見えていた

## [0.0.1] - 2026-09-23

設計段階のパッケージとして追加しました。実行可能なコンポーネントは含みません。

### Added

- 設計文書（`Documentation~/design.md`）
