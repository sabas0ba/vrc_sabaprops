# 利用方法

編集可能な配置例は [Flock Sample Scene](sample-scene.md) にあります。Unity Package Manager から Import すると、8 種・8 動作の生成済み群れをコピーして試せます。

## 群れを追加する

Hierarchy の Create menu (`SabaProps > Flock`) から、生息域ごとの代表種で群れを追加します。

| メニュー | 種 | 既定の動き |
| --- | --- | --- |
| Sky Swarm (ムクドリ) | ムクドリ | Murmuration |
| V Formation (マガン) | マガン | VFormation |
| Soaring (トビ) | トビ | Thermal |
| Sea School (マイワシ) | マイワシ | BaitBall |
| Reef School (キンギョハナダイ) | キンギョハナダイ | Cruise |
| Aquarium (ネオンテトラ) | ネオンテトラ | Cruise |
| Pond (錦鯉) | 錦鯉 (紅白) | Wander |

追加後は Inspector の「プリセット」から任意の種に切り替えられます。プリセットを選ぶと、種のパラメータと、その種の既定の動き、個体数、範囲が上書きされ、Mesh が再生成されます。

同じ種の群れを複数置く場合は、GameObject を複製して `settings.seed` を変えます。seed が異なると、個体の配置と動きの位相が変わります。

## 設定

`settings` の各項目は Mesh に焼き込まれます。変更後は「生成 / 更新」を押してください。シーン内の全群れをまとめて再生成する場合は `Tools > SabaProps > Flock > Rebuild All Swarms In Scene` を使います。

| 項目 | 内容 |
| --- | --- |
| `pattern` | 群れの動き。[要素別リファレンス](elements.md#群れの動き) を参照 |
| `count` | 個体数 (1〜1000) |
| `area` | 群れが動き回る範囲の半径 (m)。GameObject の位置を中心とし、Transform の回転と拡大に従う |
| `speedScale` | 種の巡航速度に掛ける倍率 |
| `clusterRadius` | 群れの半径 (m)。0 のとき、個体数と種の大きさから自動で決める |
| `seed` | 個体の乱数と時刻のずれ |
| `detail` | `lodMode` が Single のときの形状の段階 |
| `lodMode` | Single は Renderer 1 つ。LodGroup は High / Low / Silhouette の 3 つを LODGroup で切り替える |
| `lodTransitions` | LODGroup の各段階へ切り替える、1 個体の大きさの画面占有率。Silhouette の値を下回ると描画しない |

群れの半径は、個体が範囲の壁から体長 1 つ分内側に収まるように制限されます。水平方向は範囲の余裕の半分までに制限されます。群れの中心が動く経路を確保し、群れが端で折り返すたびに向きが反転するのを避けるためです。範囲に対して群れが小さく見える場合は、`area` を広げるか `clusterRadius` を指定してください。

## 範囲の決め方

- 空の群れは、飛ばしたい空域を覆う直方体を指定します。GameObject を空中に置き、`area.y` で高度の振れ幅を決めます。
- 海の魚群は、水面より下に収まるように GameObject の高さと `area.y` を決めます。
- 水槽の魚は、水槽の内寸の半分を `area` に指定します。GameObject は水槽の中心に置きます。メダカのように水面近くを泳ぐ種は、`area.y` を小さくし、GameObject を水面の少し下に置きます。

## 固定配置と専用の動き


マンタには `FloorGlide` を使用します。通常の魚の `Wander` と同じ経路で広く泳ぎ、高さだけが底寄りになります。GameObject を水槽内の配置空間の中心に置き、内寸から余白を引いた半寸法を指定してください。実際の Collider や地形を検出する機能はありません。

`Anchored` は個体の中心を固定します。1 個体では GameObject の原点、複数では範囲内に seed に基づいて配置します。チンアナゴ、イソギンチャク、ウニ、カキなどを置く場合に使用します。触腕や傘の動作は固定配置でも継続します。ニワトリとヒヨコは平らな地面に原点を置き、既定の Wander を使用してください。

まばらな鳥には `FreeFlight` を選び、翼開長に対して十分に広い範囲と少ない個体数を指定してください。イカには `Jet` と同名の体の動作を組み合わせると、収縮と加速が連動します。クラゲには `Float` を使用します。既定速度では経由点の間隔が 120～300 秒の浮遊です。速度倍率を上げると長周期の移動を早送りして確認できます。

`Wander` / `FreeFlight` / `Jet` / `Float` / `FloorGlide` の範囲は、体を含む配置空間の半寸法です。水槽では内寸の半分からガラスや底砂の余白を引いて指定してください。Shader が動作中の体の到達半径をさらに一度だけ差し引きます。範囲の各軸は到達半径より大きくする必要があります。UV6 を追加した版へ更新した際には、既存の群れを再生成してください。

## Material

群れは生息域ごとの共有 Material を使います。初回生成時に `Assets/SabaProps/Flock/Materials` に作成されます。

| Material | 用途 | 距離処理 |
| --- | --- | --- |
| `Flock_Sky` | 空の群れ | 80 m から 300 m にかけて暗いシルエット色へ寄せる |
| `Flock_Water` | 海とサンゴ礁 | 距離に応じて水の色へ寄せる (密度 0.04 / m) |
| `Flock_Aquarium` | 水槽・池 | 距離処理なし |

`material` 欄に別の Material を指定すると、その群れだけ差し替えられます。Shader のパラメータは次のとおりです。

| パラメータ | 内容 |
| --- | --- |
| Diffuse Wrap | 光の回り込み。遠景で陰になった面が黒く潰れるのを防ぐ |
| Scale Sheen Strength / Sharpness | 魚の鱗の銀色の反射 |
| Silhouette Color / Start / End | 遠方でシルエット色へ寄せる色と距離。A を 0 にすると無効 |
| Water / Haze Color / Density | 水や霞の色と濃さ |
| Time Scale | 群れ全体の時間の進み方 |

## 群れを移動させる

標準の Directional / Point / Spot Light、Light Probe / 環境光と受ける影に対応します。個体の移動後の座標を照明に使用します。Light Probe の SH は群れの Renderer 単位で共通です。照明が異なる領域をまたぐ広い群れは、複数に分割してください。影は落としません。VRChat Light Volumes は未対応です。

GameObject の Transform を動かすと群れ全体が移動します。Animator や Udon で GameObject を動かせば、群れを横切らせるような演出ができます。Mesh の bounds は範囲全体を覆うため、GameObject が画面外にあっても範囲が画面に入っていれば描画されます。

## VRChat へのアップロード

`FlockSwarm` コンポーネントは Editor 専用で、ビルドに含まれません。ビルドに残るのは MeshRenderer、MeshFilter、LODGroup と生成済みの Mesh、Material だけです。
