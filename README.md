# vrc_sabaprops

VRChat 向けのアセットを **VCC (VRChat Creator Companion) / VPM** で配布するためのリポジトリです。

複数パッケージの集合体として育てていく前提の構成になっています。第一弾として、GPU インスタンシング前提の軽量な草木配置パッケージ **SabaProps Foliage** を収録しています。

---

## VCC への追加

VCC の「Settings → Packages → Add Repository」に以下の URL を登録してください。

```
https://sabas0ba.github.io/vrc_sabaprops/index.json
```

または、リスティングサイトの「Add to VCC」ボタンからワンクリックで追加できます。

> リスティングは GitHub Releases から自動生成され、GitHub Pages で公開されます。
> リポジトリの Settings → Pages で Source を **GitHub Actions** にしておく必要があります。

---

## 収録パッケージ

| Package ID | 名前 | 概要 |
| --- | --- | --- |
| `io.github.sabas0ba.sabaprops.foliage` | SabaProps Foliage | GPU インスタンシング対応の草木スキャッタリングツール。グラスシード／ひまわりをプロシージャル生成し、大量配置しても軽量。 |
| `io.github.sabas0ba.sabaprops.trees` | SabaProps Trees | 再帰枝ジェネレータから樹木と 3 段階 LOD を生成。Foliage の shader／風チャンネルを共有。 |

各パッケージの詳細は `Packages/<package-id>/README.md` を参照してください。

導入後に動作を確認する最短手順は `Tools > SabaProps > Foliage > Create Sample Scene` です。
地面・ライト・カメラと 2 種類の出力モードのフィールドを含むデモシーンが、ビルド済みの状態で生成されます。
VRChat Worlds SDK が入っているプロジェクトでは `VRCSceneDescriptor` と Spawn も配置され、そのままアップロードできます。

---

## リポジトリ構成

```
.
├── Packages/                       # 配布する VPM パッケージ群（1 フォルダ = 1 パッケージ）
│   ├── io.github.sabas0ba.sabaprops.foliage/
│   │   ├── package.json            # VPM マニフェスト
│   │   ├── Runtime/                # シーンに残る最小限のコンポーネントとシェーダー
│   │   ├── Editor/                 # 生成・配置ツール（ビルドには含まれない）
│   │   └── Documentation~/
│   └── io.github.sabas0ba.sabaprops.trees/
│       ├── package.json
│       ├── Runtime/                # TreeSpecies とパラメータ
│       └── Editor/                 # 再帰枝、LOD Mesh、LODGroup 生成
├── Website/                        # GitHub Pages で公開するリスティングサイト
├── source.json                     # VPM リスティングのメタ情報
└── .github/
    ├── scripts/build_listing.py    # Releases → index.json 生成
    ├── figures/                    # ドキュメントの図の生成器（下記）
    └── workflows/
        ├── build-release.yml       # タグを打つと zip を作って Release を発行
        └── build-listing.yml       # Release 発行時にリスティングを再生成して Pages へ
```

---

## 開発フロー

### 1. Unity プロジェクトで編集する

このリポジトリ自体は Unity プロジェクトではありません。開発時は VCC で作った Unity プロジェクトの
`Packages/` 配下にこのリポジトリの `Packages/io.github.sabas0ba.sabaprops.foliage` をシンボリックリンク（または
クローンごと配置）してください。

```bash
# 例: macOS / Linux
ln -s /path/to/vrc_sabaprops/Packages/io.github.sabas0ba.sabaprops.foliage \
      /path/to/UnityProject/Packages/io.github.sabas0ba.sabaprops.foliage
```

```powershell
# 例: Windows (管理者 PowerShell)
New-Item -ItemType Junction `
  -Path "C:\UnityProject\Packages\io.github.sabas0ba.sabaprops.foliage" `
  -Target "C:\dev\vrc_sabaprops\Packages\io.github.sabas0ba.sabaprops.foliage"
```

Unity が生成した `.meta` ファイルは **必ずコミット**してください。GUID が変わるとユーザー側の参照が壊れます。

### 2. リリースする

1. `Packages/<pkg>/package.json` の `version` を上げる
2. `Packages/<pkg>/CHANGELOG.md` を更新する
3. コミットして `v<version>` のタグを打つ

```bash
git tag v0.1.0
git push origin v0.1.0
```

`build-release.yml` が zip を作って Release を発行し、続けて `build-listing.yml` が
`index.json` を再生成して GitHub Pages に反映します。

> 複数パッケージになったら、タグは `io.github.sabas0ba.sabaprops.foliage/v0.2.0` の形式でも受け付けます。

### 3. 手動でリスティングだけ作り直す

Actions から `Build VPM Listing` を `workflow_dispatch` で実行してください。

---

## ドキュメントサイト

リスティングと同じ GitHub Pages に、パッケージのドキュメントを静的サイトとして公開します。

```
https://sabas0ba.github.io/vrc_sabaprops/docs/
```

ソースはリポジトリ内の Markdown そのものです（各パッケージの `README.md` / `CHANGELOG.md` /
`Documentation~/*.md`）。二重管理はありません。

```bash
./.github/scripts/run.sh .github/scripts/build_docs.py    # Website/docs/ を生成
./.github/scripts/run.sh .github/scripts/check_docs.py    # 生成物を検査
```

このリポジトリの Python は**すべて digest 固定のコンテナ内で実行**します
（`.github/scripts/run.sh`）。ホストの `python3` へフォールバックはしません。
黙って手元の処理系を使うと再現性が失われるためです。

公開は `Build VPM Listing` ワークフローが担当します。リリース発行時のほか、main へ push された変更が
サイトの素材（`source.json` / `Website/` / パッケージの Markdown・`package.json`・`Documentation~/` /
`build_docs.py` / `check_docs.py`）に触れていれば起動します。手動で流したいときは Actions から
`workflow_dispatch` で実行してください。

### ドキュメントの図

パラメータごとの形状の違いは、実際のメッシュ生成器の出力を並べた図で示しています。

```bash
./.github/figures/render.sh            # 図を生成して Documentation~/images/generated/ に置く
./.github/figures/render.sh --check    # 生成結果と committed の図の一致を検査する（CI と同じ）
```

図は 2 段で作ります。パッケージのメッシュ生成器を `.github/verify/offline/` のシムの上で実行して形状を書き出し、
それを Python が SVG に描きます。図に出るのは常に実際の生成結果で、図のために描いた形ではありません。
`Verify` ワークフローが `--check` を実行するため、生成器を変えて図を作り直し忘れると PR が落ちます。

実際の見た目（風・透過光・影・数千個体）は実物の Unity でしか撮れないので、そちらは
`.github/figures/capture/` の Editor スクリプトで手元から撮ってコミットします。詳細は
[`.github/figures/README.md`](.github/figures/README.md) を参照してください。

図はパッケージの zip には含めていません（`build-release.yml` が `Documentation~/images/*` を除外します）。
VCC 利用者の取得サイズは増えません。

### Markdown 変換

Markdown 変換は `build_listing.py` と同じ方針で自前実装です。CI で動くものの可動部を増やさないためで、
対応しているのはリポジトリ内の文書が実際に使っている構文（見出し・段落・箇条書き・表・コードブロック・
引用・水平線・強調・リンク）に限ります。

手書きの変換器が壊れるときはクラッシュではなく「見た目は出るが本文に生の記法が残る」「リンクが黙って
どこにも行かない」という形になるため、`check_docs.py` がその 2 つを検査します。
生成物のテキストノードに未変換の記法が残っていないか、サイト内リンクと画像が実在するか、
すべての画像に alt があるか、全パッケージが索引から辿れるかを確認し、1 つでも該当すれば非ゼロで終了します。
`Verify` ワークフローでも実行しているので、文書の不備は公開時ではなく PR で落ちます。

---

## 検証 (CI)

`Verify` ワークフローが PR と main への push で走ります。ローカルでも同じものを実行できます。

```bash
./.github/verify/verify.sh
```

必要なもの: .NET SDK 8+, `glslangValidator` (`apt install glslang-tools`), curl, unzip,
および podman か docker。Python はホストに不要です（コンテナ内で実行します）。
初回は参照アセンブリをダウンロードするため数分かかります（`.verify/` にキャッシュされます）。

### 検証できること

| 対象 | 内容 |
| --- | --- |
| Runtime アセンブリ | **実物の UnityEngine 参照アセンブリ**（Unity が NuGet で配布している `UnityEngine.Modules`）に対してコンパイル |
| Editor アセンブリ | コンパイル。UnityEngine の API 使用は実物に対して検証されます |
| シェーダー | `shader_feature` の全 4 組み合わせで HLSL として型チェック |
| **メッシュ生成** | **実際に実行**して形状を検査（下記） |
| **ドキュメントの図** | 生成器を実行し直し、committed の図と一致するかを検査 |
| ドキュメント | サイトの生成、未変換の記法・壊れたリンク・存在しない画像の検出 |
| マニフェスト | `package.json` の必須項目、フォルダ名との一致、CHANGELOG のバージョン記載、`source.json` への登録、`.meta` の欠落 |

#### メッシュ生成の実行検査

`.github/verify/offline/` に、UnityEngine の数学型・`Mesh` を**実行可能な形で置き換えたシム**があります。
これにより Unity 無しで生成器そのものを走らせ、出来上がった形状を検査できます。

| 検査 | 内容 |
| --- | --- |
| 乱数 | 決定性、値域、近接シードが同じ初期値にならないこと |
| 全 4 種のメッシュ | NaN・退化三角形・インデックス範囲・法線の単位長・`UV0` / `UV3` / `COLOR` の充足・三角形数の上限 |
| 決定性 | 同じシードで同一、違うシードで別形状になること |
| 草の背丈 | 既定値が想定の範囲に収まっていること |
| 風の接合部 | 1 株が単一の風位相を持つこと、ひまわりの頭部の揺れ量の広がりが 30% 未満であること |
| 退化パラメータ | `clumpRadius=0`、`headTilt=0`、`lean=0`、`bladeCount=1` などで NaN が出ないこと |
| チャンク結合 | 頂点・三角形数の保存と、`UV3` の風ピボットがインスタンス変換に追従すること |

これは「コンパイルが通る」から一歩進んで、**生成されたジオメトリの性質を毎 PR で検査する**層です。
過去に実際にあった不具合（花びらが花芯から分離する、風の位相が部位ごとに割れる）を注入すると、
この層だけで検出できることを確認済みです。

### 検証できないこと

- **UnityEditor の API シグネチャ。** `UnityEditor.dll` は再配布できないため、
  `.github/verify/UnityEditorStub.cs` が手書きのスタブとして代役を務めています。
  スタブと実装が同じ勘違いをしていれば、このチェックは通ってしまいます。
- **サーフェスシェーダーの生成結果。** `#pragma surface` の設定を Unity が受け付けるか、
  生成されたバリアントがコンパイルできるかは検証していません。チェックしているのは
  シェーダー自身のコード（`vert` / `surf` / ライティング関数と `SabaFoliageCore.cginc`）だけです。
- **Unity の数学との数値一致。** オフライン実行はシムの上で動くため、`Slerp` や `AngleAxis` の
  厳密な値には依存しない性質だけを検査しています。値そのものを検証すればシムを検証することになります。
- **エディタ実行時に依存する処理。** 配置（レイキャストを使う）、アセット書き出し、
  サンプルシーン、VRChat ワールド経路はこの層では動かせません。

これらは下の `Unity` ワークフローが埋めます。

---

## 検証 (実物の Unity)

`Unity` ワークフローが、GameCI 経由で実際の Unity Editor 上でパッケージをコンパイルし、
EditMode テストを実行します。オフライン検証では届かない部分を担当します。

| テスト | 内容 |
| --- | --- |
| `Shader_CompilesWithoutErrors` | `ShaderUtil.ShaderHasError` でサーフェスシェーダーの生成とコンパイルを確認。**シェーダーのギャップはここで埋まります** |
| `DefaultMaterial_UsesFoliageShaderWithInstancing` | 既定マテリアルのシェーダーと GPU Instancing フラグ |
| `GrassClump_TopologyMatchesParameters` | 頂点数・三角形数がパラメータから導かれる値と一致するか |
| `Sunflower_IsWellFormedAndCheap` | NaN・法線・UV3・バウンディング、および三角形数の上限 |
| `SameSeedProducesIdenticalGeometry` | メッシュ生成の決定性 |
| `GpuInstanced_CreatesOneRendererPerInstance` | 実際に地面へ配置し、全個体が単一メッシュを共有しているか |
| `MergedChunks_CollapsesRenderersIntoChunks` | 結合後の Renderer 数と UV3 の保持 |
| `Clear_RemovesEverythingItGenerated` | Clear の後始末 |
| `Build_IsDeterministicForAGivenSeed` | 同じシードで同じ配置になるか |

エディタコード全体が実物の Unity でコンパイルされるため、**UnityEditor の API シグネチャの
ギャップもここで埋まります**。

### 必要なシークレット

Unity のライセンスをリポジトリの Secrets に登録してください。
未設定の場合、このジョブは何もせず成功扱いになります（フォークからの PR をブロックしないため）。

| Secret | 内容 |
| --- | --- |
| `UNITY_EMAIL` | Unity ID のメールアドレス |
| `UNITY_PASSWORD` | Unity ID のパスワード |
| `UNITY_LICENSE` | Personal ライセンスの `Unity_v20XX.ulf` の中身をそのまま |
| `UNITY_SERIAL` | Plus / Pro の場合は `UNITY_LICENSE` の代わりにこちら |

`.ulf` の取得手順は [GameCI の Activation ドキュメント](https://game.ci/docs/github/activation) を参照してください。

CI 用の Unity プロジェクトは `.github/verify/CIProject/` にあります。ワークフローがこれをコピーし、
`Packages/io.github.sabas0ba.sabaprops.foliage` を embedded package として配置してから Unity を起動します。
Unity のバージョンは `.github/verify/CIProject/ProjectSettings/ProjectVersion.txt` で決まります。

---

## 検証 (VRChat Worlds SDK)

サンプルシーンの `VRCSceneDescriptor` 配置はリフレクションで SDK を参照しているため、
SDK が無い環境ではコンパイルエラーにならず、検証されないまま通ります。
この分岐を実際に実行するための手順が `.github/verify/vrchat/` にあります。

```sh
./.github/verify/vrchat/run-tests.sh
```

SDK の取得はコンテナ内で行い、ローカルの VCC / ALCOM のキャッシュには依存しません。
バージョン・URL・SHA256 は `.github/verify/vrchat/packages.lock` に固定してあり、
ダウンロード環境の alpine イメージも digest で固定しています。
組み上がったプロジェクトに対して、ホストの Unity で EditMode テストを実行します。

詳細は [`.github/verify/vrchat/README.md`](.github/verify/vrchat/README.md) を参照してください。

---

## ライセンス

MIT License. 詳細は [LICENSE](LICENSE) を参照してください。
