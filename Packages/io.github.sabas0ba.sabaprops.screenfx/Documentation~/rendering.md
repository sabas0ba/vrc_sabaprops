# プリセットの描画比較

以下は Unity の Camera で取得した実描画です。共通のテストパターンに各プリセットを適用し、左を GrabPass 版、右を Lite 版として並べています。画像を個別に開くと元の 512×512 ピクセルで確認できます。

## 撮影条件と読み方

- 撮影日: 2026-10-05
- Unity 2022.3.22f1、Built-in Render Pipeline、Direct3D 11、GeForce RTX 2070
- 各プリセットの初期値、Volume 内に置いた単眼 Camera、共通の背景
- 基準画像 1 枚と 24 プリセット × 2 種、合計 49 枚

![効果を適用していない基準画像](images/rendering/Baseline.png)

上が効果なしの基準画像です。線や色面を背景にして、歪み・ぼけ・色調・周辺減光などの変化を比較します。通常版と Lite 版の機能は同一ではありません。Lite は屈折やぼけを行わず、露出などの一部の効果も重ね合わせで近似します。仕様の比較は[パッケージ概要](../README.md)を参照してください。

画像は動く効果の一時点であり、雨・雪・揺れなどの時間変化や VR の両眼表示は示しません。背景・照明・Camera の向きによって実際の World での見え方は変わります。実際に操作する手順は[Udon Demo](demo.md)、プリセットごとの構成は[プリセット一覧](elements.md)を参照してください。

## 天候

| プリセット | GrabPass | Lite |
| --- | --- | --- |
| Rain | ![Rain 通常版](images/rendering/Rain-Standard.png) | ![Rain Lite 版](images/rendering/Rain-Lite.png) |
| Storm | ![Storm 通常版](images/rendering/Storm-Standard.png) | ![Storm Lite 版](images/rendering/Storm-Lite.png) |
| Snow | ![Snow 通常版](images/rendering/Snow-Standard.png) | ![Snow Lite 版](images/rendering/Snow-Lite.png) |
| Blizzard | ![Blizzard 通常版](images/rendering/Blizzard-Standard.png) | ![Blizzard Lite 版](images/rendering/Blizzard-Lite.png) |
| Fog | ![Fog 通常版](images/rendering/Fog-Standard.png) | ![Fog Lite 版](images/rendering/Fog-Lite.png) |
| Sandstorm | ![Sandstorm 通常版](images/rendering/Sandstorm-Standard.png) | ![Sandstorm Lite 版](images/rendering/Sandstorm-Lite.png) |

## 環境

| プリセット | GrabPass | Lite |
| --- | --- | --- |
| Underwater | ![Underwater 通常版](images/rendering/Underwater-Standard.png) | ![Underwater Lite 版](images/rendering/Underwater-Lite.png) |
| Mud | ![Mud 通常版](images/rendering/Mud-Standard.png) | ![Mud Lite 版](images/rendering/Mud-Lite.png) |
| Dark | ![Dark 通常版](images/rendering/Dark-Standard.png) | ![Dark Lite 版](images/rendering/Dark-Lite.png) |
| Heat | ![Heat 通常版](images/rendering/Heat-Standard.png) | ![Heat Lite 版](images/rendering/Heat-Lite.png) |
| Cold | ![Cold 通常版](images/rendering/Cold-Standard.png) | ![Cold Lite 版](images/rendering/Cold-Lite.png) |
| Humid | ![Humid 通常版](images/rendering/Humid-Standard.png) | ![Humid Lite 版](images/rendering/Humid-Lite.png) |
| Glare | ![Glare 通常版](images/rendering/Glare-Standard.png) | ![Glare Lite 版](images/rendering/Glare-Lite.png) |
| Smoke | ![Smoke 通常版](images/rendering/Smoke-Standard.png) | ![Smoke Lite 版](images/rendering/Smoke-Lite.png) |
| Fire | ![Fire 通常版](images/rendering/Fire-Standard.png) | ![Fire Lite 版](images/rendering/Fire-Lite.png) |

## 身体・心理

| プリセット | GrabPass | Lite |
| --- | --- | --- |
| Speed | ![Speed 通常版](images/rendering/Speed-Standard.png) | ![Speed Lite 版](images/rendering/Speed-Lite.png) |
| Drunk | ![Drunk 通常版](images/rendering/Drunk-Standard.png) | ![Drunk Lite 版](images/rendering/Drunk-Lite.png) |
| Tension | ![Tension 通常版](images/rendering/Tension-Standard.png) | ![Tension Lite 版](images/rendering/Tension-Lite.png) |
| Drowsy | ![Drowsy 通常版](images/rendering/Drowsy-Standard.png) | ![Drowsy Lite 版](images/rendering/Drowsy-Lite.png) |
| Dizzy | ![Dizzy 通常版](images/rendering/Dizzy-Standard.png) | ![Dizzy Lite 版](images/rendering/Dizzy-Lite.png) |
| Damage | ![Damage 通常版](images/rendering/Damage-Standard.png) | ![Damage Lite 版](images/rendering/Damage-Lite.png) |
| Poison | ![Poison 通常版](images/rendering/Poison-Standard.png) | ![Poison Lite 版](images/rendering/Poison-Lite.png) |
| Dream | ![Dream 通常版](images/rendering/Dream-Standard.png) | ![Dream Lite 版](images/rendering/Dream-Lite.png) |
| Faint | ![Faint 通常版](images/rendering/Faint-Standard.png) | ![Faint Lite 版](images/rendering/Faint-Lite.png) |

## 検証と画像の更新

撮影時は `SabaProps.ScreenFx.CITests` の 19 テストが成功しました。テストは Shader のエラー、効果なしの画像との差、Weight や Volume の適用範囲などを検査します。VRChat 実機の画質・快適性・フレーム時間の合格を意味するものではありません。

レビュー修正後は、double-wide の左右それぞれについて UV のずらし量と境界の双線形サンプリングを検査する 6 ケースを追加し、同環境で計 25 テストが成功しました。左右で異なる色を持つテクスチャと本体共通の UV 計算を使用する GPU テストです。HMD で double-wide 表示経路全体を再検証したものではありません。

開発時に更新する場合は、検証用 Unity プロジェクトで環境変数 `SABAPROPS_SCREENFX_CAPTURE` に PNG の出力先を指定して同テストを実行します。未指定時のテスト解像度は 128×128、指定時は 512×512 です。生成された 49 PNG をこのページの `images/rendering/` に反映し、撮影条件を更新します。

`.github/verify/build_screenfx_review.py <PNG出力先>` は、基準画像との比較境界を操作できるローカルレビュー用 HTML を生成します。このページには PNG を収録しているため、GitHub とドキュメントサイトから追加の生成操作なしに閲覧できます。
