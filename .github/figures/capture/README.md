# サンプルシーンの実写

Foliage と Flock の文書用画像を、実際の Unity Shader で描画するツールです。

`Tools > SabaProps > Foliage > Capture Docs Images` で、サンプルシーンを実際の Unity で描画して
`Packages/io.github.sabas0ba.sabaprops.foliage/Documentation~/images/captured/` に JPEG を書き出します。

CI では動きません。GameCI の runner には GPU が無く、ライセンスも常に揃うとは限らないためです。
実写が必要になるのは見た目が変わったとき（シェーダー、既定パラメータ、サンプルシーンの構成）だけなので、
そのときに手元で 1 回実行してコミットする運用にしてあります。

## 実行

1. VCC で作った Unity プロジェクトに、このリポジトリの `Packages/io.github.sabas0ba.sabaprops.foliage` を
   シンボリックリンク等で配置します（リポジトリ直下の README を参照）
2. このフォルダを、そのプロジェクトの `Assets/` 配下にシンボリックリンクします

   ```bash
   ln -s /path/to/vrc_sabaprops/.github/figures/capture \
         /path/to/UnityProject/Assets/SabaPropsDocsCapture
   ```

   ```powershell
   New-Item -ItemType Junction `
     -Path "C:\UnityProject\Assets\SabaPropsDocsCapture" `
     -Target "C:\dev\vrc_sabaprops\.github\figures\capture"
   ```

3. Unity で `Tools > SabaProps > Foliage > Capture Docs Images` を実行します

サンプルシーンの生成からやり直すため、**開いているシーンは置き換わります**。未保存の変更は先に保存してください。

書き出されるのは次の 4 枚です。フレーミングは対象の bounds から計算するので、シーンの構成が変わっても同じ手順で撮り直せます。

| ファイル | 内容 |
| --- | --- |
| `sample-scene.jpg` | デモシーン全景 |
| `single-species.jpg` | セクション 1（種のみを変えた 4 区画） |
| `terrain.jpg` | セクション 3（地面の形だけを変えた 4 区画） |
| `output-modes.jpg` | セクション 5（GPU Instanced / Merged Chunks） |

## ドキュメントへの載せ方

書き出した後、Markdown から相対パスで参照します。

```markdown
![デモシーンの全景](Documentation~/images/captured/sample-scene.jpg)
```

`build_docs.py` が参照された画像だけをドキュメントサイトへコピーします。
参照したファイルが存在しなければ `check_docs.py` が Pull Request を落とすので、
「撮ったつもりで載っていない」状態にはなりません。

## このツールがパッケージに入っていない理由

出力は一度コミットすれば済むもので、パッケージの利用者が実行することはありません。
`Editor/` に置けば全利用者のプロジェクトにコンパイル対象として配られてしまうため、リポジトリ側に置いています。
代わりに `verify.sh` がこのファイルを実物の UnityEngine 参照アセンブリに対してコンパイルするので、
放置して壊れることはありません。

## Flock の Sample と画像

Flock の capture tool は `flock/` にあり、Foliage とは別の assembly です。
追加依存はありません。再生成には Unity 2022.3 の Built-in Render Pipeline を使用します。

1. 空の Unity プロジェクトの `Packages/` に Flock パッケージを配置します。
2. `SabaProps.Flock.Editors.FlockSampleScene.GenerateAllForDistribution` をコマンドラインの `-executeMethod` で実行します。
   `Assets/SabaProps/FlockSample` が既に存在する場合は生成を止めるため、Sample の再生成には空のプロジェクトを使ってください。
3. `Assets/SabaProps/FlockSample` と `Assets/SabaProps/FlockComparisons` の Scene と backdrop / World Material、および
   `Assets/SabaProps/Flock` の `Meshes`、`Materials` を、meta を含めて
   `Packages/io.github.sabas0ba.sabaprops.flock/Samples~/Flock Sample` に配置します。
4. このリポジトリの `capture/flock/` をプロジェクトの `Assets/` 配下へコピーまたはリンクします。
5. `Tools > SabaProps > Flock > Capture Docs Images` を実行します。
   batch mode では `SabaProps.Flock.DocsCapture.FlockDocsCapture.CaptureExpanded` を `-executeMethod` で実行できます。

`CaptureExpanded` は 3 つの Scene を開き、従来の 5 枚、World の 6 枚、夕方・夜の空と水槽 4 枚、泳ぎ方・飛び方の比較 2 枚、追加種 13 枚を描画します。全 Scene の共有 Mesh と Material は一度だけコピーします。各種の撮影では対象以外の Renderer を一時的に非表示にします。Shader のコンパイルエラーがないことと実画像を確認してから文書へ反映してください。

Capture は生成済みの `Assets/SabaProps/FlockSample/FlockSample.unity` を開きます。
現在の Scene は置き換わるため、未保存の変更は先に保存してください。

次の 5 枚を Flock の `Documentation~/images/captured/` に書き出します。

| ファイル | 内容 |
| --- | --- |
| `sample-overview.jpg` | 8 種・8 動作を配置した Sample Scene の全景 |
| `starling.jpg` | ムクドリの近接表示 |
| `goose.jpg` | マガンの近接表示 |
| `sardine.jpg` | マイワシの近接表示 |
| `anthias.jpg` | キンギョハナダイの近接表示 |

配布用 Sample を検証する際は、生成元の assets がない別のプロジェクトで Import します。
生成元と Sample のコピーは同じ GUID を持つため、同じプロジェクトで両方を Import すると
Unity が GUID を付け替え、配布時とは異なる参照状態になります。

### World の状況別 Scene

別の空のプロジェクトで `SabaProps.Flock.Editors.FlockWorldSample.GenerateForDistribution` を
`-executeMethod` で実行します。`FlockWorldScenarios.unity`、`WorldMaterials` とその meta を
Sample のルートへコピーします。生成された `Flock/Meshes` は `WorldMeshes`、
`Flock/Materials` は `WorldFlockMaterials` として meta を含めてコピーします。
既存の比較 Scene の Mesh と Material は保持してください。参照は GUID で維持されます。

`SabaProps.Flock.DocsCapture.FlockDocsCapture.CaptureWorld` を実行すると、
`world-sky`、`world-small-tank`、`world-small-tank-close`、`world-large-tank`、
`world-river`、`world-river-bridge` の 6 枚を JPG で出力します。
空は遠景 Camera、小型水槽は立位と近接、大型水槽は観覧者の目線、川は川岸と橋からの描画です。
画像は Shader の運動の一時点です。時間経過による密度や画面内への入り方は Play Mode で確認します。

レビュー用プロジェクトでは `FlockWorldSample.OpenForReview` を `-executeMethod` で起動すると、
生成した Scene と Game view を開けます。この起動には `-batchmode` と `-quit` を付けません。
