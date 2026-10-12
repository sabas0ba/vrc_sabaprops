# Screen FX 配置と調整

## Volume

`GameObject > SabaProps > Screen FX > <プリセット名>`で生成されるのは、Collider を外したUnity標準のCubeです。Camera がCubeの内側にある間だけ、画面全体へ効果を描画します。

- 適用範囲はTransformの位置、回転、Scaleで決まります
- `Edge Fade (m)`は境界から内側へ向かって効果が立ち上がる距離です。0にすると境界で即時に切り替わります
- `Weight`は全ブロックの強さへ掛かる係数です。AnimatorやUdonから操作する場合はこの値を使用します
- MeshはUnity標準のCubeから変更できません。Shaderが+Z面の法線とUVを使って画面を覆います

VRでは両眼の中点でVolumeの内外を判定します。鏡のCameraには描画しません。写真撮影用Cameraには描画します。

### 複数のVolume

Volumeが重なる場所では、それぞれの効果が順に適用されます。GrabPass版はVolumeごとに画面全体をコピーするため、常時重なる組み合わせは1つのMaterialへまとめてください。Materialのpropertyはすべてのブロックを同時に持つため、たとえば雨と暗がりを1つのMaterialで表現できます。

### 負荷

GrabPass版のVolumeは、Cameraが外側にあっても視錐台へ入っている間は画面のコピーを実行します。常に視界へ入る大きなVolumeを多数置く構成は避け、必要な場所だけに置くか、Udon DriverでRendererを無効化してください。Lite版は範囲外では描画面積が0になり、コピーも行いません。

## 距離に応じた霧

`Use Scene Depth`を有効にすると、`Fog Density (Depth)`が物体までの距離に応じた霧として作用します。Underwater、Mud、Dark、Fog、Sandstormのプリセットはこの値を設定済みですが、既定では無効です。

VRChatでは、影を落とすリアルタイムのDirectional LightがWorldにある場合などに限りCamera depthが生成されます。depthが生成されないWorldで有効にすると霧の濃さが不定になるため、無効のまま`Fog Veil`（距離によらない一様な霧）を使用してください。

## 効果ブロック

すべてのブロックは既定値で無効です。主となるpropertyを0より大きくすると作用します。

| ブロック | 主なproperty | 内容 |
| --- | --- | --- |
| 揺れ | `Wobble`、`Wobble Speed`、`Wobble Scale` | 画面全体の低周波の歪み、ゆっくりした回転と拡縮 |
| 陽炎 | `Heat Haze`、`Heat Haze Scale`、`Heat Haze Speed` | 上昇する細かい揺らぎ |
| 二重像 | `Double Vision` | 左右にずれた像の重ね合わせ |
| ぼけ | `Blur`、`Blur Edge Only` | 8 tapのぼけ。`Blur Edge Only`を上げると周辺だけに限定 |
| 放射ぶれ | `Radial Blur` | 正面へ向かうぶれ。周辺ほど強い |
| 色収差 | `Chromatic Aberration` | 放射方向の色ずれ |
| 水滴 | `Lens Drops`、`Lens Drop Scale`、`Lens Drop Slide` | 視界に付く水滴の屈折とハイライト。`Slide`を0付近にすると結露 |
| 霜 | `Frost`、`Frost Color` | 縁から中央へ広がる白い結晶と屈折 |
| 付着物 | `Splat`、`Splat Color`、`Splat Scale`、`Splat Drip` | 視界に付着して垂れる不透明な汚れ |
| 色調 | `Exposure (EV)`、`Saturation`、`Contrast`、`Tint` | 露出、彩度、コントラスト、乗算色。`Tint`のalphaが強さ |
| 霧 | `Fog Color`、`Fog Veil`、`Fog Density (Depth)`、`Fog Noise` | 一様な霧、距離に応じた霧、流れるむら |
| まぶしさ | `Glare`、`Glare Color`、`Glare Direction`、`Glare Focus` | 加算の白飛び。方向を指定すると、その方向を見たときに強まる |
| 周辺減光 | `Vignette`、`Vignette Color`、`Vignette Radius`、`Vignette Softness` | 縁の色。黒以外も指定可能 |
| 心拍 | `Pulse`、`Pulse Rate (Hz)` | 周辺減光の半径と画面の拡縮を2拍1休のリズムで動かす |
| 粒子 | `Particles`、`Particle Color`、`Particle Size`、`Particle Density`、`Particle Velocity`、`Particle Stretch`、`Particle Sway` | 3層の粒子。雨筋、雪、砂、火の粉、水中の浮遊物に使用 |
| 集中線 | `Speed Lines`、`Speed Line Color`、`Speed Line Inner Radius`、`Speed Line Rate` | 周辺から外へ流れる放射状の線 |
| コースティクス | `Caustics`、`Caustics Scale` | 水中の光の網目模様 |
| ノイズ | `Grain` | 画素単位の明滅 |
| まぶた | `Eyelid Closure`、`Automatic Blink`、`Blink Rate (Hz)` | 上下から閉じる黒い縁。固定値と周期動作 |

### 粒子の向き

`Particle Velocity`のYは落下速度、Xは横方向の流れです。落下方向はWorldの下方向を画面へ投影して決めるため、頭を傾けても雨は地面へ向かって落ちます。Yを負にすると上昇します（火の粉、泡）。`Particle Stretch`は進行方向へ粒子を伸ばす量で、雨筋では10以上、雪では0を使用します。

### まぶしさの方向

`Glare Direction`はWorld空間で光源へ向かうベクトルです。Directional Lightの向きの逆を指定すると、太陽を見上げたときに白飛びが強まります。(0, 0, 0)では向きによらず一様に作用します。

### 照明への追従

粒子、霧、霜、付着物、水滴のハイライトは、VolumeのLight Probeと主Directional Lightの明るさに追従します。`Lighting Response`を0にすると照明を無視して指定色のまま描画します。暗いWorldで粒子や霧が発光して見える場合は1へ近づけてください。

## 実行時の操作

### scriptを使わない方法

- Volumeへの出入りだけで足りる場合、追加の設定は不要です
- 切り替えが必要な場合は、VolumeのGameObjectまたはRendererを有効・無効にします
- 段階的な変化が必要な場合は、AnimatorでMaterialの`_Weight`を操作します

### Udon Driver

Package Managerの`Samples`から`Udon Driver`をimportすると、`ScreenFxDriver`が追加されます。VRChat Worlds SDKが必要です。

操作パネル付き Scene とコピー用 Prefab も含まれます。画像、導入手順、他の UdonSharp から呼び出すコード例は[Udon Demo の導入と再利用](demo.md)を参照してください。

| field | 内容 |
| --- | --- |
| `Target` | 操作するScreen FX VolumeのRenderer |
| `Follow Local Player` | Volumeを利用者の頭へ追従させ、場所によらず適用する |
| `Target Weight` | 開始時のWeightの目標値 |
| `Fade Seconds` | Weightが0から1へ変化する秒数 |
| `Activate On Trigger` | DriverのTrigger Colliderへ利用者が入っている間だけ適用する |
| `Speed Linked` | 移動速度が`Speed Minimum`から`Speed Maximum`へ上がるにつれてWeightを上げる |
| `Presets` / `Lite Presets` | 切替用 Material の配列。空の場合は Target の Material を使用 |
| `Preset Names` / `Preset Index` | 表示名と選択するプリセットのインデックス |
| `Use Lite` | Lite 側の Material 配列を使用 |
| `Auto Off Seconds` | ON から自動停止までの秒数。0 は無制限、デモは 20 秒 |

公開イベントは`_FadeIn`、`_FadeOut`、`_Toggle`、`_StopImmediately`、`_NextPreset`、`_PreviousPreset`、`_ResetPreset`、`_ToggleLite`です。SabaProps Tabletの`CustomEvent`項目や他のUdonから呼び出せます。Weightが0の間はRendererを無効化するため、GrabPass版でも待機中の画面コピーは発生しません。

UdonSharp からは `SelectPreset(int)`、`SetWeight(float)`、`SetFloat(string, float)`、`GetFloat(string)`、`GetPresetName()` も使用できます。Driver ごとに実行時 Material を生成し、元の Material アセットと他の Driver を変更しません。

Speedプリセットを移動速度へ連動させる場合は、`Follow Local Player`と`Speed Linked`を有効にし、`Target Weight`を1にします。

状態は利用者ごとで、同期しません。

## 制約

- 画面に固定された模様は、VRでは無限遠に見えます。粒子だけは層ごとに1.5 m、2.6 m、4.4 mの視差を持ちます
- 粒子と水滴は画面上の模様であり、World内の屋根や壁で遮られません。屋内ではVolumeを分けてください
- GrabPass版は描画順がOverlayのため、同じOverlayで描画する他のShaderとの前後は保証されません
- `Blur`を大きくすると、8 tapを画素ごとに回転させるためのざらつきが見えます
- 露出の時間的な順応（暗所へ入った直後だけ暗い等）は、前frameの状態を持たないため表現できません。`Weight`をAnimatorまたはUdonで時間変化させてください
