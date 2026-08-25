# サンプルシーンの実写

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
