# Changelog

## 0.1.0

- Pickup を手放したとき、近くの矩形面へ接触点と姿勢を補正する `PlacementSolver` を追加。
- `VRCObjectSync` を使う標準接続と、既存の `UdonSynced` 制御から呼べる計算 API を追加。
- 移動面への接続状態と相対姿勢を別 GameObject で同期し、おぼん・皿・料理の入れ子追従に対応。
- 机、椅子、食器、おぼん、冷蔵庫、磁石付き Props を含む Kitchen Demo Scene と Unity EditMode テストを同梱。
