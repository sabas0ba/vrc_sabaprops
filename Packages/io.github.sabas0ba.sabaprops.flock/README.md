# SabaProps Flock

VRChat World 向けに、鳥の群れ、魚群、水槽や池の魚を中景から遠景に配置するパッケージです。個体の移動と羽ばたき・泳ぎの動作は Shader が時刻から計算するため、Udon、Animator、Particle System、runtime script は使いません。

- Built-in Render Pipeline / Unity 2022.3、PC 向け
- 鳥 27 種、魚・水生生物 41 種 (海 23、サンゴ礁 7、水槽・池 11) のプリセット
- 群れの動き 13 種: 巡航、マーマレーション、V 字編隊、上昇気流での旋回、回遊列、ベイトボール、トルネード、水槽内の遊泳、固定配置、噴射移動、長周期の浮遊、広い独立飛行、底寄りの遊泳
- イカの収縮と移動の連動、触腕の揺れ、クラゲの脈動、ニワトリ・ヒヨコの歩行動作
- 標準 Light / Light Probe と受ける影に対応。昼・夕方・夜の確認メニューを同梱
- 形状は Silhouette / Low / High の 3 段階で、LODGroup により切り替え
- 遠方の個体を空に対するシルエット色、または水の色へ寄せる距離処理
- 外部の model、texture、追加 package は不要。形状と模様はすべてパラメータから生成

![地上の目線から見た鳥群の Unity 描画](Documentation~/images/captured/world-sky.jpg)

![収録している鳥 27 種の High 段の形状](Documentation~/images/generated/flock-birds.svg)

魚の一覧と各種の既定値は [要素別リファレンス](Documentation~/elements.md) を参照してください。

## クイックスタート

1. Hierarchy の Create menu から群れを追加します。
   - `SabaProps > Flock > Sky Swarm (ムクドリ)` など、生息域ごとの代表種が選べます。
2. 追加された `Flock <種名>` を選択し、Inspector の「プリセット」で種を選びます。種を選ぶと、その種の既定の群れの動き、個体数、範囲が適用され、Mesh が再生成されます。
3. 範囲 (`settings.area`) を配置先に合わせ、「生成 / 更新」を押します。

全種を並べて確認する場合は `Tools > SabaProps > Flock > Create Species Gallery Scene` を実行します。生息域ごとの近景の列と、遠景を飛ぶ群れを含む `Assets/SabaProps/Flock/FlockGallery.unity` が作成されます。

配置状況を確認するには Unity Package Manager の `Samples` から `Flock Sample` を Import し、`FlockWorldScenarios.unity` を開きます。空、60 cm 水槽、8 m 水槽、川に実寸の Props と群れを配置しています。`Tools > SabaProps > Flock > Sample View` で視点を切り替え、各区画の群れをコピーして調整できます。8 種・全 8 動作の一覧比較用 `FlockSample.unity` も同梱しています。操作と Unity の描画例は [Sample Scene](Documentation~/sample-scene.md) を参照してください。

## 仕組み

1 つの群れは 1 つの MeshRenderer (LODGroup 使用時は段階ごとに 1 つ) です。Mesh には 1 個体分の形状が個体数分だけ複製され、各頂点に個体番号、個体ごとの乱数、群れの動きのパラメータが焼き込まれます。頂点 Shader はそれらと時刻から個体の位置、向き、傾き、羽ばたきを計算します。

- GameObject を移動・回転・拡大すると、群れ全体がそれに従います。Animator や Udon で GameObject を動かせば、群れを移動させられます。
- すべてのパラメータが Mesh に入っているため、Material は生息域ごとの 1 つを全群れで共有できます。
- 各クライアントの Shader 時刻で動きます。装飾用途では同期処理は不要ですが、プレイヤー間で個体の位置や位相が一致する保証はありません。
- 個体同士の衝突回避や、プレイヤーを避ける動作はありません。

詳細は [設計詳細](Documentation~/architecture.md) を参照してください。

## 文書

最初に開く `FlockWorldScenarios.unity` にも全 68 種を配置しています。小型水槽は種ごとの 9 台、大型種は 24 m の水槽に分け、地上の鳥を含めて確認できます。

`FlockComparisons.unity` には全 68 種の実寸標本と、同じ種・個体数・seed・範囲で動作だけを変えた魚と鳥の比較を配置しています。小さい種は Hierarchy から選択して `F` で注目してください。`Tools > SabaProps > Flock > Sample Lighting` から昼・夕方・夜を切り替えられます。

- [利用方法](Documentation~/authoring.md): 範囲、個体数、群れの動き、LOD、Material の設定
- [Sample Scene](Documentation~/sample-scene.md): Import、群れのコピー、実描画画像
- [要素別リファレンス](Documentation~/elements.md): 収録種と群れの動きの一覧
- [設計詳細](Documentation~/architecture.md): 頂点チャンネルの割り当て、運動の式、検証の範囲
- [性能詳細](Documentation~/performance.md): 段階ごとの三角形数と負荷の目安

## 制約

- 影を落としません。Directional / Point / Spot Light と、その影を受けます。
- 生成された Renderer を Batching Static にしないでください。静的 batching は頂点を world 空間に変換するため、Shader の運動が成立しなくなります。生成時に Static flag は外されます。
- 個体の運動は周期関数の組み合わせです。群れの形は時間とともに変化しますが、実際の群れの相互作用を模擬するものではありません。
