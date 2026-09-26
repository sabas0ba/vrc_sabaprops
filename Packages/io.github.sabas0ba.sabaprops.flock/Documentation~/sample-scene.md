# Flock Sample Scene

Unity 2022.3 の Built-in Render Pipeline で描画した Sample Scene です。8 種の群れが 8 種の動きを示します。
背景とラベルは展示用で、群れの GameObject は個別にコピーできます。

![Flock Sample Scene の全景](images/captured/sample-overview.jpg)

## Import と操作

1. Unity Package Manager で `SabaProps Flock` を選び、`Samples` から `Flock Sample` を Import します。
2. `Assets/Samples/SabaProps Flock/0.1.0/Flock Sample/FlockSample.unity` を開きます。
3. Hierarchy の `SabaProps Flock Sample > Copy These Swarms` にある各群れを選びます。
4. 群れの GameObject を対象 Scene にコピーして位置を変えます。複製した群れでは seed を変更すると、個体の配置と動きの位相が変わります。
5. Inspector で `pattern`、`count`、`area`、`speedScale`、`detail` を変更したら、`生成 / 更新` を押します。

`Presentation Only` には背景とラベルだけが入っています。自作 World へコピーする必要はありません。
Sample の Mesh は再生成時に保持され、編集した群れにはプロジェクト側の新しい Mesh asset が割り当てられます。
動きは Play Mode の Game view で確認できます。表示例は静止画です。

Sample は形状を比較しやすいよう、全群れを `Single / High` で生成しています。
LOD の切り替えを試す場合は `lodMode` を `LodGroup` に変更して再生成します。
小さな魚の近接確認は Scene view で群れを選び、`F` キーで注目してください。

| 展示 | 種 | 群れの動き |
| --- | --- | --- |
| Sky formations | ムクドリ、マガン | Murmuration、VFormation |
| Sky cruising | トビ、カモメ | Thermal、Cruise |
| Sea schools | マイワシ、マサバ | BaitBall、Stream |
| Reef and aquarium | キンギョハナダイ、ネオンテトラ | Tornado、Wander |

全 56 種は Inspector のプリセットから選べます。種ごとの形状比較は[要素別リファレンス](elements.md)を参照してください。

## Unity の描画例

以下は Sample Scene の Mesh と Shader を Unity 2022.3.22f1 で実際に描画した画像です。
背景色は見やすさのため Sample Scene の展示用パネルで設定しています。

![ムクドリの群れの Unity 描画](images/captured/starling.jpg)

![マガンの V 字編隊の Unity 描画](images/captured/goose.jpg)

![マイワシの魚群の Unity 描画](images/captured/sardine.jpg)

![キンギョハナダイの魚群の Unity 描画](images/captured/anthias.jpg)

文書用画像の再生成手順はリポジトリの `.github/figures/capture/README.md` にあります。
