# SabaProps Water

`WaterLightingGallery.unity`ではDirectional Lightを除いた暗所、Point Light、Spot Lightの比較ができます。`Tools > SabaProps > Water > Create Lighting Gallery`から個別生成も可能です。

水面Foamの既定色は各Profileの浅瀬色に近い値で、`Foam Pattern Scale / Speed / Warp`を編集できます。Wet Surfaceは不透明・半透明Shaderを備え、滴headとtrailのNormal強度を個別に調整できます。

VRChat World向けの水面、雨、霧、雲、水中エフェクトをEditor上で作成するpackageです。
配置計算とrig構築はEditorで完結し、表示とanimationは標準Unity componentおよびShaderが担当します。

- Built-in Render Pipeline / Unity 2022.3向け
- 外部textureおよび追加packageは不要
- 動的な波動simulationとRenderTextureは不使用
- 雨の地面衝突はParticle System collisionとsub-emitterで処理
- 水たまりと河川のMeshはEditorでbake
- 水面、霧、水中表現はLite／StandardまたはHighを分離
- Worlds SDKがある場合だけ`VRCSceneDescriptor`を追加する任意連携

## クイックスタート

全機能をまとめて確認する場合は、Package Managerの`Samples`から`Water Feature Gallery`をimportします。
import後の`WaterFeatureGallery.unity`を開き、Play Modeに入ると雨、衝突波紋、霧、雲が動作します。
各展示rootは`[Copy Ready]`と命名されており、そのまま対象Sceneへコピーできます。

同じGalleryを現在のprojectへ直接生成する場合は、次を実行します。

`Tools > SabaProps > Water > Create Feature Gallery`

詳細な構成と撮影用Cameraは[Water Feature Gallery](Documentation~/sample-gallery.md)を参照してください。

個別に作り始める場合は、最初に以下を実行します。

`Tools > SabaProps > Water > Create Default Assets`

`Assets/SabaProps/Water`以下にsurface profile、Material、共有Meshが作成されます。package更新時に
`Packages/`以下が置換されても、sceneから参照する生成assetのGUIDは維持されます。

水面とeffectはHierarchyのCreate menuから追加できます。

- `SabaProps > Water > Puddle / River / Lake / Ocean`
- `SabaProps > Water > Underwater Lake`
- `SabaProps > Water > Wet Surface Preview`
- `SabaProps > Weather > Rain Rig / Ground Fog / Cloud Layer / Fog Volume`

## 水面

| Profile | 初期Mesh | 主な用途 |
| --- | --- | --- |
| Puddle | 不定形disc | 小さな水たまり、雨天時の局所波紋 |
| River | 編集可能なCatmull-Rom path | 河川、水路、既存world mesh上の細長い水面 |
| Lake | 20 m grid | 湖、池、比較的狭い平面水面 |
| Ocean | 100 m grid | 海、広域水面 |

各profileには次の品質があります。

| Quality | 内容 | 主な制約 |
| --- | --- | --- |
| Lite | 非周期の複合波、reflection probe、Fresnel、頂点波、疑似波紋、UV境界による浅瀬近似 | GrabPassなし。実際の水深は取得しない |
| Standard | Liteにrefraction、scene depthによる水深色を追加 | PC向け。GrabPassとcamera depthを使用 |

Material parameterを直接編集することもできますが、再利用する設定は
`Assets/SabaProps/Water/Profiles`の`WaterSurfaceProfile`で管理し、`Apply to Material`を実行します。

`Wave Scale`は方向、周波数、速度が異なる4成分をまとめて拡縮します。単一の格子模様にはなりません。
`Shallow Edge Width`はLiteで浅瀬を近似し、Standardではscene depthの浅瀬判定を補助します。
`Tide Height`と`Tide Speed`は水面全体の低周波な上下動です。連続した白帯に見えやすいprocedural foamは
既定profileでは無効です。砕波や落水の白水は、必要な場所だけへ小径の`Splash` Particle Systemを配置します。

Puddleではbox projectionを有効にしたReflection Probeを水面範囲へ置きます。`Reflection Strength`は映り込み、
`Reflection Distortion`は水面normalと雨波紋による歪み、`Reflection Blur`と`Ripple Reflection Blur`は粗さと雨天時の
霞を制御します。LiteはProbe cubemapを1回参照する近似、Standardは屈折・深度取得と組み合わせる構成です。

### 水たまりstamp

`Tools > SabaProps > Water > Puddle Stamp Tool`

1. `Use Default Puddle Lite Material`または任意の水面Materialを選択します。
2. `Start Painting`を有効にします。
3. Colliderがある地面をScene Viewでclickします。

外周の不規則さ、縦横比、回転、Mesh分割数をseedから決定し、各頂点を地面へraycastして追従させます。
生成Meshは`Assets/SabaProps/Water/Generated/Puddles`に保存されます。Colliderがないvisual meshへは配置できません。

### 河川path

`River Lite`または`River Standard`を追加し、`WaterPath`のScene View handleを移動します。
各区間はCatmull-Rom補間され、幅とUV距離を保ったstrip meshへbakeされます。

斜面では`Flow Turbulence`を上げます。白水は水面全体へ重ねず、落差上端と着水点へ短寿命の小径sprayを置きます。
Galleryのsprayは`Start Size=0.002–0.012 m`、`Length Scale=1.2`の速度方向へ伸長するbillboardです。

VRChat buildではcustom `MonoBehaviour`が実行されません。`WaterPath`は編集情報だけを保持し、表示に必要な
`MeshFilter`、`MeshRenderer`、生成Mesh、Materialは別に保存されます。build後の形状変更は行いません。

## 雨

`Rain Rig`には次の3つのParticle Systemがあります。

- `Rain`: box範囲から落下し、World collisionを行う雨滴
- `Collision Splash`:衝突時に上方向へ放出される短寿命particle
- `Collision Ripple`:衝突面に置かれる水平Mesh particle

降雨強度は`Rain > Emission > Rate over Time`、範囲は`Shape > Scale`、風は
`Velocity over Lifetime`で調整します。衝突対象は`Collision > Collides With`で必要なLayerだけに限定してください。
遠距離の雨にcollisionを使う利点は小さいため、広域雨はcollisionなしの別Particle Systemとし、
衝突するrigはplayer周辺または見せたい場所に限定する構成を推奨します。

水面Materialの`Rain Ripple Strength`はcollisionとは独立した安価な疑似波紋です。水たまり等の指定Materialだけで
有効にでき、個々の雨滴との位置同期や波動simulationは行いません。回転、密度、周期が異なる3層を合成するため、
波紋同士は重なり、規則的な1 cell 1波紋の配置にはなりません。

水面、雨滴、飛沫、波紋はUnityの主光源、Forward AddのPoint／Spot Light、ambient／Light Probe SHへ反応します。
`Lighting Response`を0にすると従来のunlit寄り、1にすると照明追従を最大にできます。VRCLightVolumesのvolumeを
直接sampleするには同packageの`LightVolumes.cginc`を使う専用adapterが必要です。本packageは外部依存を持たないため
adapterを同梱せず、VRCLV未導入時にも動作するUnity Light Probe経路を基準にしています。

## 霧と雲

| Generator | 方式 | 用途 |
| --- | --- | --- |
| Ground Fog Particles | Soft Particle billboard | 地面霧、狭域、移動objectにparentする霧 |
| Cloud Layer | 大型Particle billboard | World spaceの広域雲 |
| Fog Volume Lite | 1 pixelあたり6 sampleのbox volume | 局所volume、通常用途 |
| Fog Volume High | 1 pixelあたり20 sampleのbox volume | 狭い範囲の高品質volume |

User spaceへ追従させる場合は、生成したParticle SystemまたはFog Volumeを既存のUdon／constraint構成から
local playerへ追従させます。本packageはUdonSharp依存を追加せず、player追従scriptも自動importしません。

Fog Volumeは透明objectの後段合成や完全なatmospheric scatteringを行うpost effectではありません。
opaque geometryとの前後関係を優先する局所volumeです。World全体を覆う高品質volumeは使用しないでください。

Fog Volumeは1灯分の局所散乱をMaterialの`Local Light Position / Color / Range / Intensity`で追加できます。
位置はVolumeのobject spaceです。Galleryの`Point-lit Fog High`はPoint Lightと同じ値を設定した静的例であり、
任意のLight componentをruntimeで自動検索しません。Volume rootごと移動する場合は再設定不要ですが、Lightだけを
移動した場合はMaterialの位置も更新してください。

## 水中

`Underwater Lake Lite`または`Underwater Lake Standard`は次をまとめて生成します。

- 水面
- 水中から見た水面裏面用のLite／Standard Shader
- cameraが内部にある場合だけ描画するbox volume
- 底面用procedural caustics overlay
- additive light shaft mesh

Liteはtint、distance fog、causticsをalpha blendします。StandardはGrabPassを使い、弱いvolume shimmer、
chromatic aberration、scene depth連動のfogを追加します。volumeの上面を水面と一致させ、側面と底面が水域を
完全に覆うようscaleしてください。

水面裏面のLiteは着色とFresnel highlightだけで水上方向を近似します。Standardは専用GrabPassで水上の景色を
取得し、波normalで屈折させます。通常CameraとMirror Cameraの誤共有を避けるため、他の水面GrabPassは再利用しません。

裏面ShaderはMesh法線から有効な境界方向を選びます。`Boundary Up / Down`はx=上、y=下、
`Boundary N / E / S / W`はx=+Z、y=+X、z=-Z、w=-X、`Boundary NE / SE / SW / NW`は同じ順の斜め方向です。
通常のプールは上だけを有効にします。海底トンネル等では必要な側面方向を有効にし、`Distortion Edge Fade`で
各面のUV端に近い歪みを減衰させます。
`Underwater Volume`の`Volume Distortion`は水中空間全体の弱い揺らぎ、`Underwater Surface View`の
`Boundary Refraction Distortion`は水面・空気境界だけの屈折です。既定値は境界側を主とし、volume側は低くしています。

camera-inside判定には各cameraの`_WorldSpaceCameraPos`を使うため、通常cameraとmirror cameraは個別に判定されます。
StandardはGrabPassのためPC向けです。水面を複数作る場合はStandardの使用数を抑え、mirrorを含めた実測で判断します。

## 濡れた表面とアバター

`SabaProps/Water/Wet Surface`は、Albedoの暗化、Smoothness上昇、procedural水滴normal、垂れる水滴速度を
`Wetness`で制御する標準Surface Shaderです。`Wet Surface Preview`またはGalleryのDry／Wet／Droplets比較で
Material設定を確認できます。外部textureは不要です。

水滴はcellごとの質量から開始時刻と落下速度を変え、重い滴ほど先に速く動きます。長い軌跡を残し、終端では
滴本体が縮小しながらfadeします。小滴は`Small Droplet Scatter`の淡青白色へ寄りますが、Emissionではなく
Standard照明を受けるAlbedoなので暗所では暗くなります。`Trail Persistence`は通過後に残る濡れ筋、
`Trail Slide`は古い筋を短縮する量を制御します。trailは常に滴headより上から始まるため、headを追い越しません。
`Droplet Head Normal`と`Droplet Trail Normal`は個別に調整できます。通常は不透明版を使用し、元の景色を透過させる
必要がある小範囲だけ`SabaProps/Water/Wet Surface Transparent`と`Opacity`を使用します。

World側から任意のアバターMaterialを変更することはできません。アバターで使用する場合は、そのアバターの
Materialへ本Shaderを割り当てるか、既存Shaderへ同等のwetness処理を組み込む必要があります。PoiyomiやlilToon等の
第三者Shaderへ本packageから自動patchは行いません。Galleryの人型はWorld objectのproxyであり、任意アバターへの
強制適用を示すものではありません。

## VRChat World Descriptor

GalleryにはSDKの有無にかかわらず`VRCWorld/Spawn`を配置します。Worlds SDKが導入済みの場合は、Gallery生成時に
`VRCSceneDescriptor`、Spawn、Reference Cameraを設定します。import済みGalleryへ後から追加する場合は次を実行します。

`Tools > SabaProps > Water > Configure VRChat World Descriptor`

SDK型はreflectionで参照するため、本package自体にはVPM依存を追加しません。

## 対応範囲と性能

- 初期対象はVRChat WorldのPC版です。
- Lite water、rain、splash、ripple、particle fogはQuest移植候補ですが、Android buildでのShader検証と実機計測が必要です。
- Standard water／underwaterはGrabPassを使用するためQuest対象外です。Standard水中rigはVolumeと水面裏面で
  それぞれ背景取得を行います。
- Fog Volume Highは描画pixelごとに20 sampleを取ります。cameraを覆う大volumeではfill rateが支配的になります。
- 透明surfaceは重なりとmirrorで描画回数が増えます。Material variant数、描画面積、overdrawを併せて確認してください。

詳細は[設計と制約](Documentation~/architecture.md)、[配置・調整手順](Documentation~/authoring.md)、
[Water Feature Gallery](Documentation~/sample-gallery.md)を参照してください。
