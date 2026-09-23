# Foliage Demo

![地面配置向け8種の草花を混植したDemo](images/generated/foliage-demo-overview.svg)

この画像はPackageの実メッシュ生成器の出力を固定時刻で描画したものです。Unity画面のスクリーンショットではありません。

## Sampleを開く

Package Managerで`SabaProps Foliage`を選び、`Samples > Foliage Demo > Import`を実行します。

- `FoliageDemo.unity`: 草地、壁面のSurface Vine、Rhizome Patch
- `FoliageSpeciesDemo.unity`: 収録Speciesの比較
- `FoliageLoadDemo.unity`: 配置量と描画負荷の比較

大規模な比較Sceneは`Tools > SabaProps > Debug > Foliage > Create Sample Scene`から生成できます。

## Demoで確認する項目

1. Speciesごとの高さと密度
2. GPU Instanced / Merged ChunksのRenderer数
3. Spring / Summer / Autumn / Winterの色と姿
4. Surface VineとRhizome Patchの面追従
5. 距離縮退と風

各要素の画像と主要設定は[要素別リファレンス](elements.md)を参照してください。
