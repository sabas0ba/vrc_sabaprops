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
- `LiquidCanvasPool.CastPlayers`: 体をカプセルで近似した光線の命中判定
- `LiquidBodyCanvas.WashBelow`: 指定した高さより下の顔料の洗浄
- 水と泥のプロファイル、各 Source を配置する GameObject メニュー
- プール、泥沼、シャワー、水道、水鉄砲、鏡を並べたサンプルシーンの生成（`Tools > SabaProps > Liquid > Create Sample Scene`）
- 付着した面の奥行きの記録。同じ向きを向いた別の面（胴の側面と腕の外側など）に付着が写らないようにする
- 差し出した手への命中判定（シャワーと水道が手を濡らす）

### Fixed

- Projector を Player レイヤに置くように変更。Default に置くと、アバターだけを映すミラーに付着が映らなかった

## [0.0.1] - 2026-09-23

設計段階のパッケージとして追加しました。実行可能なコンポーネントは含みません。

### Added

- 設計文書（`Documentation~/design.md`）
