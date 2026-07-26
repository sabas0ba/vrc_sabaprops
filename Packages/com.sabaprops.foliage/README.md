# SabaProps Foliage

GPU インスタンシング前提の、軽量な草木スキャッタリングツールです。
グラスシード（草叢）とひまわりをプロシージャルに生成し、広い範囲に大量配置できます。

- テクスチャ不要（頂点カラー駆動）。パッケージにバイナリアセットを含みません
- ワールド座標ハッシュによる個体差なので、per-instance データの送信が一切不要です
- Built-in Render Pipeline / Unity 2022.3 / VRChat ワールド・アバターの両方で使えます

---

## 設計上の前提：VRChat で「GPU インスタンシング」を効かせるということ

VRChat のワールドとアバターでは、**実行時に C# が動きません**（動くのは Udon だけです）。
つまり `Graphics.DrawMeshInstanced` を毎フレーム呼ぶタイプの実装はアップロード後に何も描画しません。

そこでこのパッケージは、実際に VRChat で効く 2 つの方法だけを採用しています。

| モード | 仕組み | 向いている場面 |
| --- | --- | --- |
| **GPU Instanced** | 同一メッシュ＋同一マテリアルの Renderer を並べ、Unity の自動インスタンシングに任せる | 〜数千個体。個体ごとのカリングと距離縮退が効く |
| **Merged Chunks** | チャンク単位でメッシュを結合し、1 チャンク 1 ドローコールにする | 数千〜数万個体。CPU コストが最小 |

どちらのモードでも風揺れ・個体差・距離縮退は同じシェーダーが処理します。
配置計算はすべてエディタ時に完結し、シーンに残るのは MeshRenderer だけです。

---

## クイックスタート

1. `Tools > SabaProps > Foliage > Create Default Assets`
   `Assets/SabaProps/Foliage/` にマテリアルと 2 種類の Species（GrassSeed / Sunflower）が作られます。
2. 地面に Collider を付ける
   Terrain なら TerrainCollider、メッシュ地形なら MeshCollider。**これが無いと 1 本も生えません。**
3. `GameObject > SabaProps > Foliage Field` でフィールドを作成
   シーンビューの中心に、既定の Species が入った状態で置かれます。
4. Area / Density を調整して **Generate**

やり直したいときは **Clear**、パラメータを変えたら再度 **Generate** で作り直します。
`Seed` が同じなら何度ビルドしても同じ配置になるので、他の PC でも結果は一致します。

---

## Foliage Field の主な設定

### Area

| 項目 | 説明 |
| --- | --- |
| Shape | `Rectangle` / `Circle` |
| Size / Radius | エリアの大きさ (m)。シーンビューのハンドルでも変更できます |

### Density

| 項目 | 説明 |
| --- | --- |
| Density | 1 m² あたりの個体数。草原なら 6〜15 くらいが目安 |
| Seed | 配置の乱数シード。決定論的なので同じ値なら常に同じ結果 |
| Max Instances | 安全弁。到達するとその時点で打ち切り、警告を出します |

### Ground

地面へのレイキャストで配置高さを決めます。

| 項目 | 説明 |
| --- | --- |
| Ground Layers | 地面として扱うレイヤー |
| Require Ground Hit | OFF にすると地面が無い場所ではエリア平面に配置します |
| Raycast Height / Distance | レイの開始高さと到達距離。地形の起伏より大きく取ってください |
| Altitude Limits | 配置を許可するワールド Y の範囲。水面下を除外するときなどに |
| Ground Offset | 地面に少し埋める量。既定の `-0.01` で接地の隙間が消えます |

### Exclusion / Density Mask

- **Exclusion**: 指定レイヤーのコライダー付近には生やしません（道や建物の除外に）
- **Density Mask**: グレースケールテクスチャをエリアに投影します。
  しきい値以上でも値に応じて確率的に間引くので、グラデーションがそのまま密度勾配になります。
  テクスチャの **Read/Write Enabled を ON** にしてください。

### Output

| 項目 | 説明 |
| --- | --- |
| Output Mode | `GPU Instanced` / `Merged Chunks`（上の表を参照） |
| Chunk Size | チャンクの一辺 (m)。小さいほどカリングが効き、ドローコールは増えます |

---

## Species

Species は「1 つのメッシュ ＝ 1 つのインスタンシングバッチ」の単位です。
`Assets > Create > SabaProps > Foliage > Species` で追加できます。

共通の配置設定に加えて、種類ごとの形状パラメータを持ちます。

- **Grass Clump** — ブレード枚数、分割数、クランプ半径、高さ・幅とそのばらつき、倒れ具合、根元 AO、色
- **Sunflower** — 茎の高さ・傾き、葉の枚数と垂れ、花芯の半径とチルト、花弁の枚数・長さ・反り、色

`Placement Weight` で複数種の混合比を決めます。既定ではひまわりが `0.06`、草が `1.0` なので、
草の中にひまわりがぽつぽつ混ざる比率になります。

---

## シェーダー `SabaProps/Foliage`

Built-in RP のサーフェスシェーダーです。

- **個体差** — 要素の根元のワールド座標をハッシュして色相・彩度・明度をずらします。
  `MaterialPropertyBlock` はシーンに保存されないため使っていません。ハッシュならリロード後も同じ見た目です。
- **風** — ワールド空間を進行する波＋大きな突風＋先端のフラッター。
  揺れ量は頂点の高さ比 (`UV0.y`) を `Bend Falloff` 乗したものに、部位ごとの剛性 (`UV3.w`) を掛けた値です。
- **Distance Shrink** — `Shrink Start` から `Shrink End` にかけて、各要素をその根元へ縮退させます。
  遠景の頂点負荷とオーバードローが下がり、実質的な密度 LOD として働きます。
- **ライティング** — ラップディフューズ＋透過光。`noforwardadd` 指定なので、
  **追加のリアルタイムライトはピクセルライトではなく頂点ライトとして扱われます**。
  草原にポイントライトを大量に置いても描画が増えないための意図的な選択です。

### メッシュのチャンネル規約

自作メッシュを差し替える場合は、この規約に合わせてください。

| チャンネル | 内容 |
| --- | --- |
| `COLOR.rgb` | ベースアルベド（根元→先端のグラデーションと AO を焼き込み済み） |
| `COLOR.a` | 要素ごとの乱数シード (0〜1) |
| `UV0.x` | 要素の幅方向 |
| `UV0.y` | 高さ比 0〜1。そのまま風の曲げマスクになります |
| `UV3.xyz` | 要素の根元（オブジェクト空間）。揺れと縮退のピボット |
| `UV3.w` | 風に対する柔らかさ 0〜1 |

`UV1` / `UV2` はライトマップ UV 用に Unity が予約しているため、意図的に避けて `UV3` を使っています。

---

## パフォーマンスの指針

- **影を落とさない。** 草の `Cast Shadows` は既定で OFF です。数千の影投影は最も簡単にフレームレートを落とします。
- **ライトマップではなくライトプローブ。** ライトマップは Batching Static を要求し、それはインスタンシングを潰します。
  生成された Renderer はライトプローブを使う設定になっています。
- **静的バッチングは無効。** シェーダー側で `DisableBatching = True` を指定しています。
  静的バッチングは頂点をワールド空間へ焼き込むため、風の計算に必要なオブジェクト行列が失われます。
- **モードの切り替えどき。** GPU Instanced で Renderer が 1 万を超えると警告が出ます。
  その規模なら Merged Chunks の方が総合的に軽くなります。
- **Distance Shrink を使う。** 遠景を縮退させるだけで頂点処理が大きく減ります。

---

## 生成物の置き場所

| パス | 内容 |
| --- | --- |
| `Assets/SabaProps/Foliage/Materials/` | 共有マテリアル |
| `Assets/SabaProps/Foliage/Species/` | Species プリセット |
| `Assets/SabaProps/Foliage/Generated/Species/` | Species ごとのメッシュ（再ビルドで上書き。GUID は維持されます） |
| `Assets/SabaProps/Foliage/Generated/Merged/<field>/` | Merged Chunks モードの結合メッシュ。Clear で削除されます |

生成物はパッケージフォルダの外に置かれます。VCC はアップグレード時にパッケージフォルダを丸ごと置き換えるためです。

---

## 制限事項

- Built-in Render Pipeline 専用です（URP / HDRP は未対応）
- ライトマップベイクには対応していません。ライトプローブを使ってください
- 配置はエディタ時のみです。ワールド内で動的に草を生やすことはできません

---

## ライセンス

MIT License. リポジトリの [LICENSE](https://github.com/sabas0ba/vrc_sabaprops/blob/main/LICENSE) を参照してください。
