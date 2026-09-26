# Changelog

このパッケージの変更点をまとめています。
フォーマットは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に、
バージョニングは [Semantic Versioning](https://semver.org/lang/ja/) に従います。

## [0.1.0] - 2026-09-26

### Added

- キー入力、頭上からの取り出し、ワールド内アイテムの Interact で呼び出すタブレット本体。
- VR では指先で押し込み、Desktop では Interact で押す物理ボタン。
- GameObject、Collider、Behaviour を切り替える Toggle。排他グループと Global 同期に対応。
- 登録した地点と、選択したプレイヤーの後方へのテレポート。
- 任意の UdonSharpBehaviour のイベントを呼ぶ項目と、ページへのリンク。
- ミラー、Collider、テレポート地点、任意のイベントを登録する Setup Window。
- 外観を差し替える Tablet Theme と、角丸の本体とボタンのメッシュ生成。
- 生成後のページに表示を加える `TabletBuilder.Built`。
- ミラー、Collider、エフェクト、ライト、テレポート地点を含むサンプルシーン。
- Stage Cam の操作パネルをタブレットから操作するサンプル。
- ベッドの頭側・足側・左右・天井のミラーと、ベッド Collider を図に合わせて切り替えるサンプル。
- VR の指によるドラッグ、Desktop の Interact と増減ボタンで調整する物理スライダー。
- Reference Camera の Post Processing 設定と、明るさ・色相・Glow・有効状態をローカルで調整するモジュール。
- ページ内の項目を位置とサイズで配置する Custom Placement とベッドの平面図。
