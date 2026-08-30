# 設計と制約

## runtime依存を持たないbake構成

VRChat Worldでは通常のcustom `MonoBehaviour`を実行できないため、実行時のC#からMesh生成や
Material更新を行いません。Editor toolは以下へ変換します。

| 入力 | bake後の出力 |
| --- | --- |
| Puddle Stampのbrush設定 | Mesh asset、MeshFilter、MeshRenderer、Material |
| WaterPathの制御点 | 河川Mesh asset、MeshFilter、MeshRenderer、Material |
| Rain generator | 3つの標準Particle Systemとsub-emitter接続 |
| Fog／Cloud generator | 標準Particle Systemまたはcube MeshRenderer |
| Underwater generator | cube、grid、light shaftのMeshRenderer群 |

`WaterSurfaceProfile`と`WaterPath`はauthoring用です。表示に必要な値とMeshはMaterial／MeshFilter側にも保存されます。

## 波紋

波紋には2つの独立した方式があります。

- 雨rigのcollision ripple: 実際にColliderへ当たった位置へ水平Mesh particleを生成
- 水面Shaderの疑似波紋: world座標cellごとにhashした時刻と位置からringを生成

後者は雨滴との一致を保証しませんが、RenderTexture、compute shader、波動buffer、camera間の共有状態を必要としません。
水たまりのような小面積で見た目に対する費用対効果が高い方式です。

## 水面品質

Liteはopaque sceneを再sampleしません。透明度、procedural normal、reflection probe、direct light、Fresnelで水面を構成します。
Standardはnamed GrabPass `_SabaWaterGrab`と`_CameraDepthTexture`を使用し、refractionと水深色を追加します。

named GrabPassは同一camera内で共有されますが、camera、mirror、描画条件ごとのcopy costは残ります。
Standardを広い海面へ適用するときは、shaderの算術量よりframe buffer copyとoverdrawを先に確認します。

## Fog Volume

Fog Volumeはunit cube内でray-box intersectionを計算し、object scaleを反映した区間を固定sample数で積分します。
Liteは6、Highは20 sampleです。3D textureを使わず、複数のsineから低周波密度を作ります。

完全なpost-processing fogではないため、すべてのopaque surfaceへ大気遠近を適用する用途には向きません。
局所霧、洞窟、滝周辺、視界を限定した演出を対象にしています。

## 水中volume

camera位置をobject localへ変換し、unit cube内部の場合だけfragmentを残します。VRChat APIやUdonによる
入水判定を必要としません。volumeが非一様scaleでもinside判定は維持されます。

Standardは画面をGrabPassから再構成するため、volume外側からは描画しません。隣接する複数volumeを重ねると
境界で重複描画が発生するため、水域ごとに重ならないboxへ分割してください。
