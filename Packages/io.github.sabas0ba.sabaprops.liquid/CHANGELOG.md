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

## [0.0.1] - 2026-09-23

設計段階のパッケージとして追加しました。実行可能なコンポーネントは含みません。

### Added

- 設計文書（`Documentation~/design.md`）
