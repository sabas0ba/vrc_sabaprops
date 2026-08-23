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
| `com.sabaprops.foliage` | SabaProps Foliage | GPU インスタンシング対応の草木スキャッタリングツール。グラスシード／ひまわりをプロシージャル生成し、大量配置しても軽量。 |

各パッケージの詳細は `Packages/<package-id>/README.md` を参照してください。

導入後に動作を確認する最短手順は `Tools > SabaProps > Foliage > Create Sample Scene` です。
地面・ライト・カメラと 2 種類の出力モードのフィールドを含むデモシーンが、ビルド済みの状態で生成されます。
VRChat Worlds SDK が入っているプロジェクトでは `VRCSceneDescriptor` と Spawn も配置され、そのままアップロードできます。

---

## リポジトリ構成

```
.
├── Packages/                       # 配布する VPM パッケージ群（1 フォルダ = 1 パッケージ）
│   └── com.sabaprops.foliage/
│       ├── package.json            # VPM マニフェスト
│       ├── Runtime/                # シーンに残る最小限のコンポーネントとシェーダー
│       ├── Editor/                 # 生成・配置ツール（ビルドには含まれない）
│       └── Documentation~/
├── Website/                        # GitHub Pages で公開するリスティングサイト
├── source.json                     # VPM リスティングのメタ情報
└── .github/
    ├── scripts/build_listing.py    # Releases → index.json 生成
    └── workflows/
        ├── build-release.yml       # タグを打つと zip を作って Release を発行
        └── build-listing.yml       # Release 発行時にリスティングを再生成して Pages へ
```

---

## 開発フロー

### 1. Unity プロジェクトで編集する

このリポジトリ自体は Unity プロジェクトではありません。開発時は VCC で作った Unity プロジェクトの
`Packages/` 配下にこのリポジトリの `Packages/com.sabaprops.foliage` をシンボリックリンク（または
クローンごと配置）してください。

```bash
# 例: macOS / Linux
ln -s /path/to/vrc_sabaprops/Packages/com.sabaprops.foliage \
      /path/to/UnityProject/Packages/com.sabaprops.foliage
```

```powershell
# 例: Windows (管理者 PowerShell)
New-Item -ItemType Junction `
  -Path "C:\UnityProject\Packages\com.sabaprops.foliage" `
  -Target "C:\dev\vrc_sabaprops\Packages\com.sabaprops.foliage"
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

> 複数パッケージになったら、タグは `com.sabaprops.foliage/v0.2.0` の形式でも受け付けます。

### 3. 手動でリスティングだけ作り直す

Actions から `Build VPM Listing` を `workflow_dispatch` で実行してください。

---

## 検証 (CI)

`Verify` ワークフローが PR と main への push で走ります。ローカルでも同じものを実行できます。

```bash
./.github/verify/verify.sh
```

必要なもの: .NET SDK 8+, `glslangValidator` (`apt install glslang-tools`), curl, unzip, python3。
初回は参照アセンブリをダウンロードするため数分かかります（`.verify/` にキャッシュされます）。

### 検証できること

| 対象 | 内容 |
| --- | --- |
| Runtime アセンブリ | **実物の UnityEngine 参照アセンブリ**（Unity が NuGet で配布している `UnityEngine.Modules`）に対してコンパイル |
| Editor アセンブリ | コンパイル。UnityEngine の API 使用は実物に対して検証されます |
| シェーダー | `shader_feature` の全 4 組み合わせで HLSL として型チェック |
| マニフェスト | `package.json` の必須項目、フォルダ名との一致、CHANGELOG のバージョン記載、`source.json` への登録、`.meta` の欠落 |

### 検証できないこと

- **UnityEditor の API シグネチャ。** `UnityEditor.dll` は再配布できないため、
  `.github/verify/UnityEditorStub.cs` が手書きのスタブとして代役を務めています。
  スタブと実装が同じ勘違いをしていれば、このチェックは通ってしまいます。
- **サーフェスシェーダーの生成結果。** `#pragma surface` の設定を Unity が受け付けるか、
  生成されたバリアントがコンパイルできるかは検証していません。チェックしているのは
  シェーダー自身のコード（`vert` / `surf` / ライティング関数と `SabaFoliageCore.cginc`）だけです。

この 2 つは下の `Unity` ワークフローが埋めます。

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
`Packages/com.sabaprops.foliage` を embedded package として配置してから Unity を起動します。
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
