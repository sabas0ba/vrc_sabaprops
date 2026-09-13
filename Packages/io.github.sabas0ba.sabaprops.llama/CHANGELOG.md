# Changelog

## [0.1.0]

- FP32 llama2.c v0 checkpointとtokenizer.binのEditor変換を追加。
- RMSNorm、RoPE、GQA対応の因果Attention、SwiGLU、greedy生成をfragment shaderで実装。
- フレーム分割・非同期readback・ローカル専用のUdonSharp runtimeを追加。
- 数値検証用の未学習モデル、CPU参照実装、GPU比較テストを追加。
- Unity / VRChat実機検証と学習済みモデルでの速度・品質測定は未完了。
