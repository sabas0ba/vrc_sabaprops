# Screen FX プリセット一覧

プリセットは効果ブロックの組み合わせです。表の「主なブロック」はMaterialのproperty名に対応します。ブロックの意味は[配置と調整](authoring.md#効果ブロック)を参照してください。

## 天候

| プリセット | 内容 | 主なブロック |
| --- | --- | --- |
| Rain | 雨。落下する雨筋と、視界に付いて流れる水滴 | `Particles`、`Lens Drops`、`Fog Veil` |
| Storm | 豪雨。斜めに吹き付ける雨、暗い視界、多量の水滴 | `Particles`、`Lens Drops`、`Exposure`、`Blur` |
| Snow | 雪。揺れながらゆっくり落ちる雪片と、わずかに白い空気 | `Particles`、`Particle Sway`、`Tint` |
| Blizzard | 吹雪。横殴りの雪、白い遮蔽、縁の凍結 | `Particles`、`Fog Veil`、`Frost` |
| Fog | 霧。流れるむらのある白い遮蔽と、低い彩度・階調 | `Fog Veil`、`Fog Density`、`Fog Noise` |
| Sandstorm | 砂嵐。横に流れる砂粒、黄褐色の遮蔽、ざらつき | `Particles`、`Fog Veil`、`Tint`、`Grain` |

## 環境

| プリセット | 内容 | 主なブロック |
| --- | --- | --- |
| Underwater | 水中。青緑の吸収、揺らぎ、漂う粒子、コースティクス | `Tint`、`Wobble`、`Caustics`、`Fog Density` |
| Mud | 泥の中。茶色の濁り、強いぼけ、視界に付着して垂れる泥 | `Splat`、`Fog Veil`、`Blur`、`Tint` |
| Dark | 暗がり。低露出、色の喪失、狭い視野、ノイズ | `Exposure`、`Vignette`、`Grain`、`Saturation` |
| Heat | 熱気。立ち上る陽炎、暖色の空気、わずかなまぶしさ | `Heat Haze`、`Tint`、`Glare` |
| Cold | 冷気。縁から広がる霜、青白い色調 | `Frost`、`Tint`、`Saturation` |
| Humid | 高湿度。白く曇る視界、細かい結露、弱い陽炎 | `Fog Veil`、`Blur`、`Lens Drops` |
| Glare | まぶしさ。光源方向で強まる白飛びと、にじみ | `Glare`、`Glare Direction`、`Exposure` |
| Smoke | 煙。暗い流動する遮蔽、舞い上がる火の粉 | `Fog Veil`、`Fog Noise`、`Particles` |
| Fire | 火災。強い陽炎、橙色の光、火の粉 | `Heat Haze`、`Particles`、`Glare` |

## 身体・心理

| プリセット | 内容 | 主なブロック |
| --- | --- | --- |
| Speed | 高速移動。放射状のぶれ、集中線、周辺の色ずれ | `Radial Blur`、`Speed Lines`、`Chromatic Aberration` |
| Drunk | 酩酊。ゆっくりした揺れ、二重像、ぼけ、重いまぶた | `Wobble`、`Double Vision`、`Blur` |
| Tension | 緊張。心拍に合わせて狭まる視野、周辺のぼけ、硬い階調 | `Vignette`、`Pulse`、`Blur Edge Only` |
| Drowsy | 眠気。周期的に落ちるまぶた、ぼけ、暗い視界 | `Automatic Blink`、`Blur`、`Vignette` |
| Dizzy | めまい。速い揺れ、二重像、色ずれ | `Wobble`、`Double Vision`、`Chromatic Aberration` |
| Damage | 負傷。心拍に合わせて脈打つ赤い縁、色の喪失 | `Vignette`、`Pulse`、`Saturation` |
| Poison | 毒。緑がかった色調、うねり、強い色ずれ | `Tint`、`Wobble`、`Chromatic Aberration` |
| Dream | 夢・回想。周辺のぼけ、淡い色、白い縁 | `Blur Edge Only`、`Glare`、`Vignette` |
| Faint | 失神寸前。閉じかけたまぶた、狭い視野、色の喪失 | `Eyelid Closure`、`Vignette`、`Saturation` |

## Lite版での差

Lite版は背景を取得しないため、`Wobble`、`Heat Haze`、`Double Vision`、`Blur`、`Radial Blur`、`Chromatic Aberration`、`Saturation`、`Contrast`、`Fog Density`が作用しません。Speed、Drunk、Dizzyのように歪みとぼけが主体のプリセットは、Lite版では集中線、周辺減光、まぶたなど重ね合わせ部分だけが残ります。
