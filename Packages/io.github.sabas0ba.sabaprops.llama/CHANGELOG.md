# Changelog

このパッケージの変更点をまとめています。
フォーマットは [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) に、
バージョニングは [Semantic Versioning](https://semver.org/lang/ja/) に従います。

## [Unreleased]

## [0.1.0] - 2026-10-12

最初の公開版です。概念実証（PoC）向けの最小構成で、入力の続きを生成するcompletionのみに対応します。

### Added

- GGUF v2/v3のLlama（F32/F16/Q4_0/Q8_0）と内蔵tokenizer、またはFP32 llama2.c v0 checkpointとtokenizer.binを、RFloat textureと推論Materialへ変換するEditor拡張。
- 寸法・ファイル長・非有限値の検査と、入力ファイルのSHA-256・出典・ライセンスの記録。
- RMSNorm、RoPE、GQA対応の因果Attention、SwiGLU、greedy生成のfragment shader実装。
- フレーム分割・非同期readback・停止と再実行に対応した、ローカル専用のUdonSharp runtime。
- ワールド用のCube・入力欄・出力パネルを生成するWorld Runnerと、Build & Test用の最小シーンを生成する`Create Sample Scene`。
- 変換済みモデルのCPU/GPU照合ボタンと、FP32展開後の重み容量・作業VRAMの表示。
- 数値検証用の未学習モデル、CPU参照実装、GPU比較テスト、ClientSimでの実行テスト。
- 固定revision・SHA-256によるStories260K / Stories15M Q4_0の取得手順と、実ファイルのCPU検査。
- llama2.c（MIT）に基づく部分の著作権表示と許諾条件（`THIRD-PARTY-NOTICES.md`）。

### 確認状況

- Unity 2022.3.22f1・Direct3D 11・VRChat Worlds SDK 3.10.4で、GPUとCPU参照の全logitの一致、client向けUdonコンパイル、ClientSimでの生成・停止・再実行を確認。
- Stories260Kのサンプルシーンが、VRChatクライアント上のワールドで生成することを確認。
- 速度・VRAM・品質の測定と、upload後の動作確認は未完了。

### 制限

- chat template、温度・top-p、K/IQ量子化、Llama 3系のtokenizer、Questには対応しません。
- 学習済みの重みは同梱しません。変換した重みはワールドに含まれるため、元モデルの再配布条件に従ってください。
