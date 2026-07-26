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

## ライセンス

MIT License. 詳細は [LICENSE](LICENSE) を参照してください。
