# SabaProps Screen FX

VRChat World向けの画面全体エフェクトです。雨、雪、高速移動、酩酊、水中、泥、暗がり、緊張、熱気、冷気、高湿度、霧、まぶしさなどの状態を、Volumeへ入ったCameraにだけ適用します。

- Built-in Render Pipeline / Unity 2022.3向け
- 外部textureおよび追加packageは不要。模様はすべてShader内で生成
- 必須のruntime scriptなし。Volumeへの出入りはShaderがCamera位置から判定
- 18の効果ブロックを1 passで合成。プリセットはブロックの組み合わせ
- GrabPass版（屈折、ぼけ、色調を含む全機能）とLite版（GrabPassなし、重ね合わせのみ）
- VRでは視線方向を基準に模様を生成し、両眼で一致させる。粒子は層ごとに視差を付与
- 鏡の中では描画しない

## 収録プリセット

| 分類 | プリセット |
| --- | --- |
| 天候 | Rain / Storm / Snow / Blizzard / Fog / Sandstorm |
| 環境 | Underwater / Mud / Dark / Heat / Cold / Humid / Glare / Smoke / Fire |
| 身体・心理 | Speed / Drunk / Tension / Drowsy / Dizzy / Damage / Poison / Dream / Faint |

各プリセットの内容は[プリセット一覧](Documentation~/elements.md)を参照してください。

## クイックスタート

全プリセットを確認する場合は、次を実行します。

`Tools > SabaProps > Screen FX > Create Gallery Scene`

プリセットごとの区画が並んだSceneが`Assets/SabaProps/ScreenFx/Samples`へ生成されます。Play Modeで区画へ入るか、Scene ViewのCameraを区画内へ移動すると効果が適用されます。

個別に配置する場合は、HierarchyのCreate menuから追加します。

- `SabaProps > Screen FX > <プリセット名>`: GrabPass版
- `SabaProps > Screen FX Lite > <プリセット名>`: Lite版

生成されるのはUnity標準のCubeです。Transformの位置とScaleを適用範囲へ合わせます。MaterialはAssets/SabaProps/ScreenFx/Materialsへ作成され、package更新後も編集内容が残ります。

配置、パラメータ、実行時の操作、制約は[配置と調整](Documentation~/authoring.md)を参照してください。

## GrabPass版とLite版

| | GrabPass版 | Lite版 |
| --- | --- | --- |
| Shader | `SabaProps/Screen FX/Composite` | `SabaProps/Screen FX/Composite Lite` |
| 歪み、ぼけ、放射ぶれ、色収差、二重像 | 対応 | 非対応 |
| 彩度、コントラスト | 対応 | 非対応 |
| 露出、色かぶり | 対応 | 減光と薄い色の重ね合わせで近似 |
| 距離に応じた霧 | Camera depthがある場合に対応 | 非対応 |
| 粒子、集中線、霜、付着物、周辺減光、まぶた、霧、まぶしさ | 対応 | 対応 |
| 負荷 | Volumeごとに画面全体のコピー1回と最大8 tap | 画面全体の半透明描画1回 |

GrabPass版はPC向けです。Android向けWorldではLite版を使用してください。

## 検証状況

Shaderのコンパイル、プリセットとShader propertyの整合、Unity EditorでのCamera描画結果は自動テストで確認しています。VRChat実機での両眼表示、鏡、他のOverlay Shaderとの描画順は環境に依存するため、Worldごとに確認してください。

## ライセンス

Apache License 2.0。詳細は[LICENSE.md](LICENSE.md)を参照してください。
