# Foliage 要素別リファレンス

![収録している8 Speciesの生成結果](images/generated/species-overview.svg)

上図はPackageの実メッシュ生成器から出力した形状比較です。各要素の専用画像がある項目は以下にも掲載します。

## Grass Clump

![Grass ClumpのBlade Count比較](images/generated/grass-blade-count.svg)

| 主要パラメータ | 内容 |
| --- | --- |
| `Blade Count` | 1株のブレード数。形状密度と三角形数に影響 |
| `Height` / `Width` | 草丈とブレード幅 |
| `Bend` | 根元から先端への倒れ |
| `Clump Radius` | 株内のブレード配置半径 |

[全パラメータと比較画像](parameters.md#grass-clump)

## Clover

![CloverのLeaflet Count比較](images/generated/clover-leaflet-count.svg)

| 主要パラメータ | 内容 |
| --- | --- |
| `Leaflet Count` | 1葉を構成する小葉数 |
| `Leaf Length` / `Leaf Width` | 小葉の寸法 |
| `Notch` | 小葉先端の切れ込み |
| `Stem Height` | 地面から葉までの高さ |

[全パラメータと比較画像](parameters.md#clover)

## Sunflower

![SunflowerのHead Tilt比較](images/generated/sunflower-head-tilt.svg)

| 主要パラメータ | 内容 |
| --- | --- |
| `Stem Height` / `Lean` | 茎の高さと傾き |
| `Head Radius` / `Head Tilt` | 花芯の大きさと向き |
| `Petal Count` | 花弁数 |
| `Face Sun` | Directional Light方向への整列 |

[全パラメータと比較画像](parameters.md#sunflower)

## Reed

![ReedのSpread比較](images/generated/reed-spread.svg)

| 主要パラメータ | 内容 |
| --- | --- |
| `Blade Count` | 株の葉数 |
| `Height` / `Spread` | 高さと先端の開き |
| `Spike` / `Spike Length` | 穂の有無と長さ |
| `Clump Radius` | 株元の広がり |

[全パラメータと比較画像](parameters.md#reed)

## Small Flower

上部のSpecies比較図の左下に生成結果を掲載しています。

| 主要パラメータ | 内容 |
| --- | --- |
| `Plant Height` | 草丈 |
| `Flower Count` | 1株の花数 |
| `Petal Count` / `Petal Length` | 花弁数と長さ |
| `Flower Tilt` | 花の傾き |

## Weed

上部のSpecies比較図の下段2番目に生成結果を掲載しています。

| 主要パラメータ | 内容 |
| --- | --- |
| `Leaf Count` | 葉数 |
| `Leaf Length` / `Leaf Width` | 葉の寸法 |
| `Prostrate` | 地面へ寝かせる量 |
| `Flower Stem Count` | 花茎数 |

## Grain

上部のSpecies比較図の下段3番目に生成結果を掲載しています。

| 主要パラメータ | 内容 |
| --- | --- |
| `Height` | 草丈 |
| `Leaf Count` / `Spread` | 葉数と開き |
| `Head Length` / `Head Rows` | 穂の長さと段数 |
| `Awn Length` | 芒の長さ |

## Dandelion

上部のSpecies比較図の右下に生成結果を掲載しています。

| 主要パラメータ | 内容 |
| --- | --- |
| `Leaf Count` / `Serration` | 葉数と鋸歯の深さ |
| `Flower Stem Count` | 花茎数 |
| `Flower / Seed Head` | 花と綿毛の切替 |
| `Floret Count` | 小花数 |

## Surface Vine

![Surface Vineを含むFoliage Demo](images/generated/foliage-demo-overview.svg)

| 主要パラメータ | 内容 |
| --- | --- |
| `Surface Collider` | 成長対象の面 |
| `Initial Direction` | 初期成長方向 |
| `Step Length` | 探索1段階の長さ |
| `Branch Chance` | 分岐率 |

[配置方法と詳細パラメータ](parameters.md#surface-vine)

## Rhizome Patch

![Rhizome Patchを含むFoliage Demo](images/generated/foliage-demo-overview.svg)

| 主要パラメータ | 内容 |
| --- | --- |
| `Node Spacing` | 地下茎ノード間隔 |
| `Spread Radius` | パッチの広がり |
| `Branch Chance` | 地下茎の分岐率 |
| `Species` | ノードから生成する植生 |

[配置方法と詳細パラメータ](parameters.md#rhizome-patch)
