# パラメータと見た目の対応

Species の形状パラメータが実際にどう効くかを、生成されたメッシュそのもので並べた早見です。

図はすべて**このリポジトリの生成器の出力**です。`.github/figures/render.sh` がパッケージのメッシュ生成器を実行し、出てきた形状をそのまま描いています。手で描いた図ではないので、生成器を変えれば図も変わります（CI が committed の図と生成結果の一致を検査します）。

図の読み方は共通です。

- 1 つの図の中では**縮尺が共通**です。タイル間で背丈をそのまま比べられます。図ごとの縮尺は左下のスケールバーが示します
- 変えているのは見出しのパラメータ 1 つだけで、`meshSeed` を含む他はすべて既定値です
- 各タイルの下は、そのメッシュの**高さ (m)** と**三角形数**です
- 地面の楕円は y = 0 の面です

---

## Grass Clump

### Blade Count

![bladeCount を 2 / 6 / 12 / 24 に変えた草叢の比較](images/generated/grass-blade-count.svg)

三角形数はブレード枚数にそのまま比例します。広い面積を埋めるときは、枚数を増やすより `Density` を上げた方が同じ三角形数でも見た目の密度が出ます。1 株が単体で見える近景では枚数が効きます。

### Height

![height を 0.25 / 0.6 / 1 / 1.4 m に変えた草叢の比較](images/generated/grass-height.svg)

`heightVariance` は高さに対する比率なので、`height` を変えても株内のばらつきの割合は保たれます。

### Bend

![bend を 0 / 0.45 / 0.9 / 1.4 に変えた草叢の比較](images/generated/grass-bend.svg)

先端が根元からどれだけ倒れるかです。0 は直立した芝、1 を超えると寝そべった野草になります。倒すほど水平方向の投影面積が増えるため、同じ `Density` でも地面の被覆率は上がります。

### Clump Radius

![clumpRadius を 0 / 0.04 / 0.08 / 0.16 m に変えた草叢の比較](images/generated/grass-clump-radius.svg)

ブレードの根元が散る円の半径です。0 では 1 点から生えるので束に見えます。広げるほど 1 株の占有面積が増えるので、`Min Spacing` も併せて調整してください。

### Mesh Seed

![meshSeed を 1 / 2 / 3 / 4 に変えた草叢の比較](images/generated/mesh-seed.svg)

パラメータが同じでもシードが違えば別の形になります。逆に、同じシードなら別の PC で作り直しても同じメッシュです。種を複数用意して見た目を散らしたい場合は、パラメータではなくシードを変えるのが最も安全です（1 種 = 1 メッシュ = 1 インスタンシングバッチなので、種を増やせばバッチも増えます）。

---

## Clover

### Leaflet Count

![leafletCount を 2 / 3 / 4 / 5 に変えたクローバーの比較](images/generated/clover-leaflet-count.svg)

4 枚にすれば四つ葉です。三つ葉の中に混ぜたい場合は、四つ葉の Species を別に作り、 `Placement Weight` を小さくして混ぜてください。

### Notch

![notch を 0 / 0.12 / 0.22 / 0.45 に変えたクローバーの比較](images/generated/clover-notch.svg)

小葉の先端の切れ込みです。0 は丸葉で、深くするほどハート形になります。

---

## Sunflower

### Head Tilt

![headTilt を 0 / 20 / 38 / 70 度に変えたひまわりの比較](images/generated/sunflower-head-tilt.svg)

0 度で真上を向き、大きくすると正面を向きます。`Face Sun` と併せると畑全体の向きが揃います。

### Petal Count

![petalCount を 6 / 10 / 15 / 24 に変えたひまわりの比較](images/generated/sunflower-petal-count.svg)

花弁の枚数だけが変わります。花芯の大きさは `headRadius` が決めるので、枚数を増やすと 1 枚あたりの間隔が詰まります。

### Lean

![lean を 0 / 0.18 / 0.45 / 0.8 m に変えたひまわりの比較](images/generated/sunflower-lean.svg)

茎の頂点が根元からどれだけ横へずれるかです。個体ごとの傾きの向きはシードで決まるため、値を大きくすると畑全体がばらけた印象になります。

---

## Reed

### Spread

![spread を 0 / 0.16 / 0.4 / 0.8 m に変えた葦の比較](images/generated/reed-spread.svg)

先端の開きです。小さいほど直立し、水辺の縦のシルエットになります。大きくすると草叢に近づきます。

### Spike

![spike の有無と spikeLength を変えた葦の比較](images/generated/reed-spike.svg)

最も高いブレードの先に付く穂です。`spike` を OFF にすると穂の分の三角形が消えます。

---

## 関連

- 各パラメータの一覧と既定値は [README](../README.md) を参照してください
- 生えない・重いといった症状は [トラブルシューティング](troubleshooting.md) にまとめてあります
