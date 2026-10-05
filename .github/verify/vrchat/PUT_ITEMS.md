# Put Items の検証

SDK は packages.lock の SHA256 固定アーカイブを使用します。Unity Editor は ../CIProject/ProjectSettings/ProjectVersion.txt の 2022.3.22f1 を使用します。

## 環境

開発環境は sabas0ba/dotfiles の fc4cdecc02a6a95c81a259549d3fb9e7df18bb8f を参照しています。同リポジトリの CLAUDE.md、home/.claude/CLAUDE.md、docs/development.md に従い、Nix で定義したツールをコンテナ内で使用します。dotfiles の構成・ホームへの配置・WSL の隔離設定は変更しません。

この開発環境で確認した既存イメージは localhost/vrc-sabaprops-dev-python:dotfiles-fc4cdecc、内容 ID は sha256:83e56d6d37df4176de93f526c12e46d786c613d0a72daa5aaa56d5b7b9a17825 です。DOTFILES_ENV=nix-develop と Python の Nix profile 内の所在を確認しています。このローカルイメージは配布物ではありません。

第三者と CI は、既存の .github/scripts/run.sh が指定する digest 固定 Python コンテナから同じ準備スクリプトを実行できます。ホストに Python を導入する必要はありません。SDK 取得は既存の fetch.sh の固定コンテナを使用します。

## 手順

リポジトリ直下で実行します。

```sh
.github/verify/vrchat/fetch.sh
.github/scripts/run.sh .github/verify/vrchat/prepare_putitems.py
```

prepare_putitems.py は build/vpm の ZIP を再検証し、build/PutItemsProject を構築します。再実行では自身が配置する SDK・パッケージ・テストを更新し、Unity Library を維持します。既存 ZIP があれば外部通信なしで実行できます。

同梱シーンを再生成する場合は、次を実行します。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .github/verify/vrchat/build-putitems-demo.ps1
```

この処理は専用の build/PutItemsProject 内でメッシュ・マテリアル・シーン・プレビューを生成し、パッケージの `Samples~/KitchenDemo` へ出力します。通常の利用者は生成せず、VPM で配布された `Open Demo Scene` を使用します。

Windows では次を実行します。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .github/verify/vrchat/run-putitems-tests.ps1
```

結果は build/PutItemsProject/results.xml、ログは同ディレクトリの unity.log です。スクリプトは Unity の終了コードと全テストの Passed を検査します。実行中は経過時間・ログサイズを表示します。

他の OS では同じ Unity バージョンで build/PutItemsProject を開き、Test Runner の EditMode で SabaProps.PutItems.Tests を実行します。既存の assemble.sh / run-tests.sh にもこのパッケージとテストを組み込んでいます。

## 検証範囲

幾何計算、無所有権時の移動拒否、デモの参照設定、および実際の UdonSharp コンパイルを確認します。ネットワーク遅延、所有権移動、途中参加は実 VRChat の2クライアント以上で確認する必要があります。README の実機確認項目を使用してください。

Claude Code へ引き継ぐ場合も、上記 dotfiles の固定 revision・CLAUDE.md、コンテナ使用方針、および実機検証の未実施項目を明示してください。
