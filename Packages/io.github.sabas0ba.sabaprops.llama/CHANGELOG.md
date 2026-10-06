# Changelog

## [Unreleased]

### Changed

- Apache-2.0 の宣言、公式ライセンスURL、同梱するライセンス全文と README の表記を統一。
- llama2.c（MIT）に基づく部分の著作権表示と許諾条件を`THIRD-PARTY-NOTICES.md`に掲載し、READMEの記述を実態に合わせて修正。
- World Runnerの出力Canvasに暗色の背景パネルを追加。明るい背景で白い出力文字が読めなかったため。

### Fixed

- `LlamaWorldBuilder`の`PackageInfo`曖昧参照によりUnity Editorでコンパイルできなかった問題を修正。
- `SabaLlamaRunner.OnAsyncGpuReadbackComplete`が基底メンバーを隠蔽していた問題を修正。

### Added

- GGUF v2/v3のLlama F32/F16/Q4_0/Q8_0をFP32 textureへ変換し、内蔵tokenizerを取り込むEditor readerを追加。
- 固定revision・SHA-256によるStories260K / Stories15M Q4_0の取得手順と実ファイルCPU検査を追加。
- 変換済みモデルのCPU/GPU照合ボタンと、FP32展開後の重み容量表示を追加。

## [0.1.0]

- FP32 llama2.c v0 checkpointとtokenizer.binのEditor変換を追加。
- RMSNorm、RoPE、GQA対応の因果Attention、SwiGLU、greedy生成をfragment shaderで実装。
- フレーム分割・非同期readback・ローカル専用のUdonSharp runtimeを追加。
- 数値検証用の未学習モデル、CPU参照実装、GPU比較テストを追加。
- Unity / VRChat実機検証と学習済みモデルでの速度・品質測定は未完了。
