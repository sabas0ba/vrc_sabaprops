# Changelog

## [0.1.0] - 2026-09-26

### Added

- 鳥 25 種、魚 31 種 (海、サンゴ礁、水槽・池) のプリセットと、パラメータから形状と模様を生成する body generator
- 巡航、マーマレーション、V 字編隊、上昇気流での旋回、回遊列、ベイトボール、トルネード、水槽内の遊泳の 8 種の群れの動き
- 個体の移動、向き、傾き、羽ばたき、体のくねりを頂点 Shader で計算する `SabaProps/Flock/Swarm` Shader
- Silhouette / Low / High の 3 段階の Mesh と LODGroup の生成
- 遠方の個体をシルエット色または水の色へ寄せる距離処理
- 群れの Inspector、Hierarchy の Create menu、全種を並べるギャラリー Scene の生成
