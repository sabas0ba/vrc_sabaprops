# Flock Sample

`FlockSample.unity` には 8 種の群れを 1 種ずつ配置し、8 種の群れの動きをそれぞれ設定してあります。
各群れは生成済みの Mesh と Material を参照するため、Scene を開いてすぐに確認できます。

## 使い方

1. `FlockSample.unity` を開きます。
2. Hierarchy の `SabaProps Flock Sample > Copy These Swarms` から群れを選びます。
3. 対象 Scene に GameObject をコピーし、位置を調整します。`Presentation Only` は展示用の背景とラベルなので、コピーする必要はありません。
4. 種、個体数、範囲、動き、seed などを Inspector で変更し、`生成 / 更新` を押します。

コピーした群れを再生成すると、Sample に同梱された Mesh は保持され、新しい Mesh が
`Assets/SabaProps/Flock/Meshes` に作成されます。別の種へ変更したい場合は Inspector の
`プリセット` を選んでください。全 56 種を利用できます。

このパッケージは Particle System を使用しません。個体の運動は Mesh と Shader で表現しています。

設定項目と制約はパッケージの `Documentation~/authoring.md` と
`Documentation~/sample-scene.md` を参照してください。
