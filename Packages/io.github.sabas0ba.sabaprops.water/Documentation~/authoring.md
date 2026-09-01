# 配置・調整手順

## 水たまり

1. 地面にColliderを設定し、水たまり対象だけをLayerで分離します。
2. Puddle Stamp Toolの`Physics Layer Mask`へ対象Layer mask値を設定します。
3. 平坦面では`Radial Rings`を2から4、起伏面では4から8にします。
4. 地面を貫通する場合は`Surface Offset`を増やします。
5. 頂点が地形を追えない場合は`Projection Distance`を増やします。

水たまりの外形はMesh自体でも不規則になります。Materialの`UV Edge Fade`は透明度をさらに落とすため、
不透明な外周が必要な場合は0へ戻します。

## 河川

1. Riverを生成します。
2. control pointを水路の中心へ置きます。
3. `Width`を設定します。
4. 曲率が大きい区間だけ`Subdivisions`を増やします。
5. `UV Meters Per Tile`でflow patternの長さを調整します。

幅が急変する河川は現時点の一定幅pathでは表現しません。区間ごとに複数pathへ分けるか、生成Meshを通常の
modeling toolで編集します。岸への自動intersectionやterrain carvingは行いません。

control pointのYを変えると斜面と落差を持つstripを生成できます。滝の前後は制御点間隔を短くし、
`Whitewater` Materialの狭い補助stripと`Splash` Particle Systemを重ねます。GalleryのRiver rootは、浅い上流、
底が見えにくい下流、落差、白泡、飛沫を含む編集例です。

## 雨

雨rigの初期値は22 m四方、最大6000 particleです。次の順で削減します。

1. Collisionの`Collides With`を地面と水面Layerに限定
2. emission範囲をcamera周辺に限定
3. `Rate over Time`を削減
4. Collision qualityを必要な範囲で下げる
5. 遠距離雨をcollisionなしの別systemへ分離

波紋を不要とする場所では`Collision Ripple`objectを無効化するか、親のSub Emittersから当該entryを外します。

## 霧・雲

Particle fogはcameraとの交差でsoft particle depthを使います。camera depthが利用できない描画経路では
soft intersectionが無効になるため、particle sizeとalphaを下げて境界を目立たなくします。

Fog Volume Highは小さなvolumeに限定します。World全体を覆う霧はUnity RenderSettingsのFogまたは
距離別の遠景materialと組み合わせます。

局所光はFog Materialの`Local Light`項目へVolume object spaceで設定します。静的なPoint Light表現向けで、
Light componentからruntime同期はしません。

## 水中

Underwater rigのroot位置が水面高です。`Underwater Volume` childの上面を水面へ合わせたまま、深さと水平範囲を
調整します。底面の`Caustics Receiver Overlay`は実際の地形形状へ自動追従しないため、平坦でない水底では複製して
小区画へ分けるか、対象Meshを水底に沿う形へ置換します。

水中から水上を見せる場合は`Underwater Surface View`を水面直下へ置きます。Liteは背景取得なし、Standardは
通常CameraとMirror Cameraを分離する専用GrabPassで水上景色を取得します。
