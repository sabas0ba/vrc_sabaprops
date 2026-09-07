# 配置・調整手順

## 水たまり

1. 地面にColliderを設定し、水たまり対象だけをLayerで分離します。
2. Puddle Stamp Toolの`Physics Layer Mask`へ対象Layer mask値を設定します。
3. 平坦面では`Radial Rings`を2から4、起伏面では4から8にします。
4. 地面を貫通する場合は`Surface Offset`を増やします。
5. 頂点が地形を追えない場合は`Projection Distance`を増やします。

水たまりの外形はMesh自体でも不規則になります。Materialの`UV Edge Fade`は透明度をさらに落とすため、
不透明な外周が必要な場合は0へ戻します。

風景を映す場合は水たまりを覆うReflection Probeを置き、Box Projectionを有効にします。静的WorldではBaked、
動く空や時間帯が必要な場合はRealtime + On Awakeを基準にします。GalleryはLite=64、Standard=128です。
Every Frame更新は6面renderが継続するため、必要な小範囲に限定してください。`Reflection Distortion`は水面と雨波紋、
`Ripple Reflection Blur`は雨天時の反射の霞を調整します。

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

`Whitewater`はUV下流方向へ、曝気開始前の透明な縦筋、曝気開始点、成長する白濁、気泡、側縁filamentを合成します。
`Aeration Inception`を落差上端、`Aeration Growth`を白濁が成長する区間へ合わせます。落下点には`Plunge Pool Froth`と
低速mistを置き、落水部の強い循環流と飛沫を近似します。流体simulationではなく、保存済みMesh上のstateless表現です。

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

境界方向はMaterialの3 vectorで選択します。`Boundary Up / Down=(上, 下, 0, 0)`、
`Boundary N / E / S / W=(+Z, +X, -Z, -X)`、`Boundary NE / SE / SW / NW=(NE, SE, SW, NW)`です。
単純なプールは上だけ、海底ガラストンネルは必要な側面だけを1にします。境界Meshの法線が方向判定に使われます。
UV seam付近の過大な屈折は`Distortion Edge Fade`を上げて抑えます。

## 濡れた表面

`Trail Persistence`は滴が通った後の筋の残留時間、`Trail Slide`は残留筋が遅れて下へずれる量です。
水滴ごとの擬似質量により重い滴から先に移動します。World object用のShaderであり、他者アバターのMaterialを
World側から変更する機能ではありません。
