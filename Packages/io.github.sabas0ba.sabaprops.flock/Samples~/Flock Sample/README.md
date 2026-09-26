# Flock Sample

## World の状況で確認する

`FlockWorldScenarios.unity` を開き、`Tools > SabaProps > Flock > Sample View` で視点を切り替えます。
全 69 種を空・地面、幅 60 cm の小型水槽 9 台、幅 8 m の大型水槽、川、幅 24 m の水槽へ配置しています。
1 Unity unit = 1 m で、周辺 Props と個体の実寸、密度、遊泳範囲を比較できます。

Play Mode で動きを確認してください。鳥は広い `FreeFlight`、小型水槽の魚は `Wander`、イカは収縮と加速が連動する `Jet`、タコは俊敏な三次元浮遊の `OctopusDrift`、マンタは底付近と壁際を泳ぐ `FloorGlide`、クラゲは直立した長周期の `Float` です。クラゲの経由点は既定で 120～300 秒ごとに変わります。速度倍率を上げると早送りで確認できます。大きい観賞魚の水槽は高さ 0.50 m・奥行き 0.45 m としています。
各区画の `Flock <種名>` をコピーし、Inspector の値を変えて `生成 / 更新` を押してください。
Play Mode では動きを確認でき、Scene view では観察位置を自由に変えられます。
区画ごとに 2 台の Camera と、地上の鳥を確認する Camera があり、全 11 台です。`Sample View > Ground birds / Oceanarium` で追加の視点を選べます。
水とガラスは比較用の簡易 Material です。衝突回避、屈折、波、カースティクスは含みません。

## 種と動作の一覧で確認する

`FlockSample.unity` には 8 種の群れを 1 種ずつ配置し、8 種の群れの動きをそれぞれ設定してあります。
各群れは生成済みの Mesh と Material を参照するため、Scene を開いてすぐに確認できます。

## 使い方

`FlockComparisons.unity` には全 69 種の実寸標本と、同じマイワシ・ムクドリで動きだけを変えた比較を配置しています。小さな標本は選択後に Scene view の `F` で注目します。`Sample View > Compare swimming / Compare flying` で比較用 Camera を選べます。

`Sample Lighting > Day / Evening / Night` で標準 Light と環境光を切り替えられます。大型水槽には Point Light もあります。照明領域が異なる場合の Light Probe 補間は群れ単位です。

1. `FlockSample.unity` を開きます。
2. Hierarchy の `SabaProps Flock Sample > Copy These Swarms` から群れを選びます。
3. 対象 Scene に GameObject をコピーし、位置を調整します。`Presentation Only` は展示用の背景とラベルなので、コピーする必要はありません。
4. 種、個体数、範囲、動き、seed などを Inspector で変更し、`生成 / 更新` を押します。

コピーした群れを再生成すると、Sample に同梱された Mesh は保持され、新しい Mesh が
`Assets/SabaProps/Flock/Meshes` に作成されます。別の種へ変更したい場合は Inspector の
`プリセット` を選んでください。全 69 種を利用できます。

このパッケージは Particle System を使用しません。個体の運動は Mesh と Shader で表現しています。

設定項目と制約はパッケージの `Documentation~/authoring.md` と
`Documentation~/sample-scene.md` を参照してください。
