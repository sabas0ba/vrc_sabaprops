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

## 水面Foam

procedural foamは既定Profileでは無効です。使用する場合は`Foam Strength`を少量ずつ上げ、次の順で調整します。

| Parameter | 用途 |
| --- | --- |
| `Foam Color` | 泡の色。既定値は各水面の`Shallow Color`に近い色です。白泡が必要な場合だけ白へ寄せます |
| `Crest Foam Threshold` | 波高のうち泡を生成し始める位置 |
| `Crest Foam Width` | 波頭に沿う泡の幅 |
| `Residual Foam` | 砕波後に残る泡の量 |
| `Foam Breakup` | 泡を連続帯から分断する強さ |
| `Foam Pattern Scale` | 分断模様の空間スケール |
| `Foam Pattern Speed` | 分断模様が流れる速度。0で停止 |
| `Foam Pattern Warp` | 模様の規則性を崩すdomain warp量 |

Shader内のfoamは広域水面全体で同じ計算を行います。落差上端、着水点、岸の一部など局所的な白水は、
`Foam Strength`を水面全体へ上げず、`Splash` Particle Systemを必要な位置だけへ配置してください。

## 河川

1. Riverを生成します。
2. control pointを水路の中心へ置きます。
3. `Width`を設定します。
4. 曲率が大きい区間だけ`Subdivisions`を増やします。
5. `UV Meters Per Tile`でflow patternの長さを調整します。

幅が急変する河川は現時点の一定幅pathでは表現しません。区間ごとに複数pathへ分けるか、生成Meshを通常の
modeling toolで編集します。岸への自動intersectionやterrain carvingは行いません。

control pointのYを変えると斜面と落差を持つstripを生成できます。滝の前後は制御点間隔を短くし、落差上端と
着水点へ`Splash` Particle Systemを置きます。既定Galleryは`Render Mode=Stretch`、`Start Size=0.002–0.012 m`、
`Length Scale=1.2`を基準にした点に近い飛沫です。連続した補助stripは白帯に見えやすいため使用しません。

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
水中空間全体の揺らぎはvolume Materialの`Volume Distortion`、水面・空気境界は裏面Materialの
`Boundary Refraction Distortion`で別々に調整します。通常はvolume側を0–0.002程度に抑えます。

## 濡れた表面

通常は不透明な`SabaProps/Water/Wet Surface`を使用します。ガラス、薄布、透明overlay等で元の景色を残す場合だけ
`SabaProps/Water/Wet Surface Transparent`を選び、`Opacity`を調整します。半透明版はrender sortingとoverdrawの
影響を受けるため、重なる面や広い画面占有率では不透明版を優先してください。

水滴headはUVの-Y方向を重力方向として、下側が膨らみ、上側がtrailへ細く接続する形状です。MeshのUVが実際の
重力方向と一致しない場合は、UVを修正するか、重力方向ごとにMaterialを分けます。trailの開始位置は常にhead上端より
上へ固定されます。`Trail Persistence`は長い濡れ筋の残量、`Trail Slide`は古いtrailを短縮する量です。

`Droplet Head Normal`と`Droplet Trail Normal`は個別に調整できます。まず両方を0にし、headを0.2–0.4、trailを
0.05–0.2程度まで上げると、過度な凹凸を避けて調整できます。水滴ごとの擬似質量により重い滴から先に移動します。
World object用のShaderであり、他者アバターのMaterialをWorld側から変更する機能ではありません。

## 照明確認

`WaterLightingGallery.unity`はDirectional Lightを持たず、暗い環境光のみ、Point Light、Spot Lightの3区画で
Standard水面、雨、衝突飛沫、Wet Surfaceを比較します。暗所区画で必要以上に発光していないことを確認した後、
Point／Spot区画で光源色、距離減衰、照射範囲への追従を確認してください。

水面、雨、飛沫、波紋はUnity Light Probe SHとForward Base／Forward Add lightへ反応します。VRCLightVolumesを
直接sampleする場合は、同packageの`LightVolumes.cginc`を参照するproject側adapterが別途必要です。
