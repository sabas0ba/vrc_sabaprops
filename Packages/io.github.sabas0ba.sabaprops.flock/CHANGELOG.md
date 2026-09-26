# Changelog

## [0.1.0] - 2026-09-26

### Added

- 最初の World 状況別 Sample に全 69 種を配置。種ごとの小型水槽 9 台、24 m 水槽、地上の鳥の視点を追加

- 鳥 27 種、魚・水生生物 42 種 (海、サンゴ礁、水槽・池) のプリセットと、パラメータから形状と模様を生成する body generator
- 巡航、マーマレーション、V 字編隊、上昇気流での旋回、回遊列、ベイトボール、トルネード、水槽内の遊泳と固定配置の 9 種の群れの動き
- 個体の移動、向き、傾き、羽ばたき、体のくねりを頂点 Shader で計算する `SabaProps/Flock/Swarm` Shader
- Silhouette / Low / High の 3 段階の Mesh と LODGroup の生成
- 遠方の個体をシルエット色または水の色へ寄せる距離処理
- 群れの Inspector、Hierarchy の Create menu、全種を並べるギャラリー Scene の生成
- 標準 Light / Light Probe、追加 pixel light、受ける影に対応。距離色にも照明を適用
- World の空・小型水槽・大型水槽・川、全 69 種、同種の動作比較を含む 3 つの Sample Scene と Unity RenderImage
- authoring component の除去を確認する実 AssetBundle 検査と、暗所・追加光源の描画検査
