# 利用方法

自分のワールドに液体の付着を組み込む手順です。サンプルの内容は [サンプルの導入とレビュー](demo.md)、
方式と制約の理由は [設計](design.md) を参照してください。

## 前提

- VRChat Worlds SDK 3.10.x（UdonSharp を含む）。VCC でこのパッケージを追加すると一緒に解決されます
- 対象は PC です。Android / iOS では Projector による描画を想定していません
- 付着は各クライアントの RenderTexture に描きます。メモリの目安は「[メモリと負荷](#メモリと負荷)」を参照してください

## 最小構成

1. Hierarchy の右クリックから `SabaProps > Liquid > Prefabs` の Source（例：Shower、Spray Gun）を置きます。
   シーンに Canvas Pool（`Liquid Canvas Pool`）が無ければ、同時に作成されます
2. ワールドの主光源（Directional Light）に `SabaProps/Liquid/Liquid Lighting` を付け、`mainLight` に
   その Light を設定します。付けない場合、付着は環境光だけで照らされます
3. `Tools > SabaProps > Liquid` のサンプル生成、または Build & Test で確認します

これで、プレイヤーの体に液体が付き、流れ、乾きます。プールの `canvases` の数（既定 12）が、
同時に付着を表示できる人数の上限です。超えた場合は、最も長く入力の無い人の Canvas を使い回します。

## Source を置く

Prefab は自分の液体の定義（子の `Liquid`）を持ち、プールは実行時に名前で探します。そのまま置けば動きます。

| Prefab | 使い方 |
|---|---|
| Liquid Cup / Liquid Bucket | 持って使用ボタンで、中身を前へ投げかけます。コップは少量、バケツは 5 L を一度に |
| Liquid Faucet | 流しと蛇口。Interact で開閉し、差し出した手を濡らします |
| Liquid Shower | 高さ 2.2 m の固定シャワー。Interact で切り替えます |
| Liquid Water Gun | 持って使用ボタンを押している間、細い水流を放ちます |
| Liquid Spray Gun | 持ち運べる噴射器。使用ボタンを押している間だけ噴射し、状態は全員に同期されます |
| Liquid Nozzle Stand | 操作盤付きのノズル。量、距離、速さ、断面を変え、Start / Stop で出し続けます |
| Liquid Umbrella | 持つか立てておくと、傘の下には雨と雪が降りません |
| Liquid Pen / Liquid Stamp / Liquid Eraser | 向けた先に線を引く、形を置く、消す |
| Liquid Reset Panel | 付着を消す操作盤（自分、マネキン、全員、壁と床） |

Prefab 以外に、`SabaProps > Liquid` のメニューから次を配置できます。

| メニュー | 配置されるもの |
|---|---|
| Canvas Pool | Canvas 12 個分のプール。Projector のマテリアルは `Assets/SabaProps/Liquid/Materials` に生成されます |
| Pool Volume (Water) / Mud Volume | 上面を液面とするトリガーの箱。入った体を液面の高さまで濡らし、汚します |
| Shower / Water Gun | Prefab と同じものを、シーンのプールと液体の定義につないで配置します |
| Rain Area / Snow Area | 10 m 四方、高さ 6 m の範囲に雨または雪を降らせる Source とパーティクル |
| Surface Canvas (Walls and Floor) | 壁と床に付着を描く範囲（後述） |

コンポーネントを直接付ける Source（`Add Component > SabaProps > Liquid`）もあります。

| コンポーネント | 用途 |
|---|---|
| Sprayer | サーバー時刻に合わせて自動で液体を放ち続けます。噴水や滴りの演出に使います。同期しません |
| Nozzle | 1 回、定期、連続、押している間の 4 通りの放ち方を持つ汎用の Source。Prefab のコップやノズル台の中身です |
| Humidity | 範囲内の乾きを遅らせ、閾値を超えると体に結露と垂れる水滴を生じさせます。サウナや浴室に使います |
| Light Zone | 屋内や暗い部屋の照明を付着へ伝えます。紫外線（ブラックライト）で蛍光の顔料が光ります |

### 液体を変える

液体は `LiquidProfile` で定義します。Prefab では子の `Liquid`、メニューで置いた Source では
`Liquid Profiles` の下にあります。

| 項目 | 効果 |
|---|---|
| `pigmentColor` / `pigmentAmount` | 顔料の色と被覆。0 で水のような色の無い液体、1 で不透明な塗料 |
| `filmAmount` | 液膜の量。表面の暗化と反射の強さ |
| `smoothness` | 濡れた面の艶 |
| `viscosity` | 粘性。0 でさらさら流れ、1 で流れません |
| `dryingSeconds` | 乾ききるまでの秒数。0 以下で乾きません |
| `washStrength` | 他の液体の顔料を洗い流す強さ。水は大きく、塗料は 0 |
| `fluorescence` / `luminescence` | 紫外線で光る割合 / 光を蓄えて暗がりで光る割合 |
| `edgeIrregularity` | 付着の輪郭の不規則さ |

### 受け手の素材を変える

同じ液体でも、布は吸って暗くなり、革や樹脂ははじいて水滴になります。受け手は `LiquidSurfaceProfile` で
定義し、Canvas の部位ごと（上着、ズボン、靴、髪、肌）に設定します。プールの Canvas には既定の割り当てが
入っています。アバターの実際のマテリアルは読めないため、部位は頭、手、足、腰の位置から推定します。

| 項目 | 効果 |
|---|---|
| `absorbency` | 吸水性。高いほど暗く深い色になり、水滴になりません |
| `repellency` | 撥水性。高いほど水が水滴になります |
| `sheen` | 濡れたときに表面全体が帯びる艶 |
| `friction` | 流れにくさ |
| `bleed` | 塗料の縁のにじみ |
| `strands` | 液が上下の筋にまとまる度合い（髪） |
| `beadSize` | 水滴の大きさ（m） |

## 壁と床に付着させる

付着を許す範囲に Surface Canvas を置きます。`SabaProps > Liquid > Surface Canvas (Walls and Floor)` で
配置し、次を設定します。

1. GameObject を部屋の中心に置き、`Canvas` の `halfExtents` を部屋の半分の寸法より 0.2 m ほど大きくします。
   箱の中にある壁、床、天井、家具の面が対象です
2. `Tools > SabaProps > Liquid > Register Paint Tools and Surfaces` を実行します。Projector の投影範囲が
   箱に合わせ直され、プールに登録されます。Udon は実行時に Projector を操作できないため、寸法は
   Editor で決めます。`halfExtents` を変えるたびに実行してください
3. `bodySurface` に受け手の素材（Painted Wall、Concrete、Glazed Tile など）を設定します。箱ごとに 1 つです
4. `faceResolution` を決めます。1 テクセルは「箱の辺の長さ ÷ 解像度」です

これで、既存の Source の液体が箱の中の壁や床にも付きます。体に当たらなかった光線が、Default または
Environment レイヤの Collider に当たった点が対象です。壁や床に Collider が必要です。

Surface Canvas を複製、削除したときも、同じメニューを実行し直してください。複製したものは Projector の
マテリアルを共有しているため、`projectorMaterial` と Projector の Material を、複製した別のマテリアルに
差し替えてください。Canvas ごとに別のマテリアルが必要です。

### ペン、スタンプ、消しゴム

Prefab を置けば、マネキンと他のプレイヤーには描けます。壁と床に描くには、その範囲に Surface Canvas が
必要です。

描いたものを後から入った人にも見せるには、ツールと Surface Canvas を描画の履歴に登録します。
ツールを置き終えたら `Tools > SabaProps > Liquid > Register Paint Tools and Surfaces` を実行します。
履歴（`Liquid Paint Log`）がプールの下に作られ、ツールに番号が振られます。ツールを追加、削除、
並べ替えたときは実行し直してください。番号がずれると、後から入った人には別のツールの色や形で再生されます。

| ツールの項目 | 効果 |
|---|---|
| `mode` | 0: ペン、1: スタンプ、2: 消しゴム |
| `shape` | スタンプの形。1: 円、2: 四角、3: 星、4: ハート、5: 輪 |
| `radius` | 線の太さの半分、またはスタンプと消しゴムの半径（m） |
| `range` | 届く距離（m） |
| `hitPlayers` | 他のプレイヤーの体にも描くか |
| `profile` | インク。流れないよう `viscosity` を 1 にした液体の定義を使います |

履歴の `capacity`（既定 1000）が、後から入った人へ渡す件数の上限です。ペンは動かしている間、
1 秒に最大 16 件を使います。

## 環境をつくる

### 雨と雪

`Rain Area` / `Snow Area` は、範囲の箱の中の体に降らせます。頭上に Collider（屋根）がある体と、
傘の下の体には降りません。降る周期はサーバー時刻から決まり、全員で一致します。

- 地面を濡らす・積もらせるには、地面に `SabaProps/Liquid/Weather Surface` のマテリアルを使い、
  Weather の `groundMaterials` に設定します。屋根の下の面には使わないでください（面ごとの遮蔽は持ちません）
- 範囲に Surface Canvas があれば、雨粒がその面にも落ちます（`surfaceDropsPerSecond`）

### 湿度

`Humidity` を範囲の中心に置き、`areaSize`、`humidity`、`condensationThreshold` を設定します。
`linkedShower` を設定すると、シャワーが出ている間だけ湿度が上がります。湯気のパーティクル、
霧の体積（`SabaProps/Liquid/Fog Volume`）、曇る鏡や壁（`Fogged Glass`、`Weather Surface`）を
つなぐと、湿度に合わせて変化します。

裸が自然な場所（サウナ、浴室）では `assumeBareSkin` を有効にし、`bareSkinSurface` に肌の素材を設定します。
範囲内の体の衣服の部位を肌として扱います。

### 屋内と暗い部屋

付着の上には Unity の影が落ちないため、屋内でも主光源で照らされて見えます。部屋ごとに `Light Zone` を
置き、`lamp` に室内の Light を設定してください。範囲内の体と Surface Canvas は、その明かりで照らされます。

`blacklight` を設定すると、紫外線だけの状態を作れます。蛍光の顔料は紫外線を受けている間だけ光り、
蓄光の顔料は明かりを消した後もしばらく光ります。`automatic` を有効にすると、点灯、紫外線のみ、
消灯を繰り返します。`ToggleLamp`、`ToggleBlacklight`、`ResumeAutomatic` を `LiquidButton` から
呼ぶと、スイッチになります。

## 消去

`Liquid Reset Panel` を置くと、自分、マネキン、全員、壁と床の付着を、全員の画面で消せます。
`everyoneMayClearAll` を無効にすると、全員の消去はインスタンスのマスターだけが行えます。

アバターを変えたとき、そのプレイヤーの付着は自動で消えます。

## 同期

同期するのは描画に必要な入力だけで、RenderTexture は同期しません。

| 対象 | 方式 | 途中参加者 |
|---|---|---|
| シャワー、ノズル、明かりの範囲 | 状態（出ているか、設定、点灯）を Manual sync | 状態は伝わる。それまでの付着は伝わらない |
| 水鉄砲、1 回の放出 | 命中または乱数の種をイベントで送る | 伝わらない |
| 雨、雪、自動散布、湿度 | サーバー時刻から各クライアントで計算。同期なし | 入った時点から同じ周期で動く |
| ペン、スタンプ、消しゴム | 1 区間ごとにイベントで送る。壁と床の分は履歴に残す | 壁と床の直近の描画は伝わる。体の分は伝わらない |

場の Source（シャワー、雨など）の命中は各クライアントで判定するため、付き方はクライアント間で
厳密には一致しません。

## メモリと負荷

Canvas 1 つは、3 × 2 面のアトラスの RenderTexture を 8 枚持ち、約 240 × 解像度² バイトを使います。

| 対象 | 解像度 | 1 つあたり |
|---|---|---|
| プレイヤーの Canvas（既定） | 160 | 約 6 MiB（12 個で約 70 MiB） |
| Surface Canvas | 192 | 約 8 MiB |
| Surface Canvas | 256 | 約 15 MiB |
| Surface Canvas | 384 | 約 34 MiB |

- プレイヤーの Canvas は、最初に付着を受けたときに確保します
- Projector は、範囲内の Renderer を付着のシェーダでもう一度描きます。アバターでは
  「付着のある人数 × マテリアルスロット数」、Surface Canvas では箱の中の Renderer の数だけ描画が増えます
- Surface Canvas は、最後の付着から `surfaceIdleSeconds` を過ぎると RenderTexture の更新を止めます
- マネキン（固定の人型への付着）は 1 体ごとに Canvas を持ちます。サンプルは比較のために多く置いていますが、
  実際のワールドでは数を絞ってください

## 制限

- 部位の推定は体の形からの近似で、長い髪、手袋、半袖の前腕の肌などとは一致しません
- 付着を描く箱は固定寸法です。極端に大きい・小さいアバターでは、箱の外に付着が描かれません
- 関節を大きく曲げると、付着が表面上で少し滑って見えます（Canvas は腰に追従します）
- Surface Canvas の細部は解像度で決まります。箱が大きいほど粗くなります
- 描画ツールの送り主の検証は、有効なプレイヤーであることだけです

## サンプルの移動設定

サンプルのワールドは、`VRCWorld` の `LiquidDemoMovement` で移動速度を上げ、ジャンプできるようにしています。
自分のワールドに流用する場合は、値を変えるか外してください。
