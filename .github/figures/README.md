# ドキュメントの図

ドキュメントに載せる図の生成器です。2 系統あり、担当が分かれています。

| 系統 | 生成するもの | 実行 | リポジトリ内の置き場所 |
| --- | --- | --- | --- |
| オフライン（このフォルダの `render.sh`） | パラメータごとの形状比較図 (SVG) | CI で検査、手元で再生成 | `Packages/<pkg>/Documentation~/images/generated/` |
| Unity（`capture/`） | サンプルシーンの実写 (JPEG) | 手元の Unity で手動 | `Packages/<pkg>/Documentation~/images/captured/` |

前者は「パラメータを変えると形がどう変わるか」を示すもので、比較が仕事なので毎回同じ結果になる必要があります。
後者は「実際にどう見えるか」を示すもので、風・透過光・影・数千個体といった、オフラインでは代替できないものを写します。

どちらの出力も VPM の zip には含まれません（`build-release.yml` が `Documentation~/images/*` を除外します）。

---

## オフラインの図

```sh
./.github/figures/render.sh            # 図を作り直す
./.github/figures/render.sh --check    # 生成結果と committed の図が一致するか検査する（CI と同じ）
```

必要なもの: .NET SDK 8+ と、podman か docker（Python は `.github/scripts/run.sh` の固定コンテナ内で動きます）。

内部は 2 段です。

1. `DumpFigures.cs` — パッケージのメッシュ生成器を `.github/verify/offline/UnityEngineShim.cs` の上で実行し、各タイルの形状を JSON に書き出します。図に出るのは常に**実際の生成結果**で、図のために作られた形状ではありません
2. `render_figures.py` — その形状を投影・陰影付けして 1 図 1 枚の SVG にします。ラベル・スケールバー・高さと三角形数の注記もここで付きます

図の一覧（どのパラメータをどの値で並べるか）は `DumpFigures.cs` の `Main` にあります。追加するときはそこに 1 行足してください。

`verify.sh` が `--check` を実行するので、生成器を変えて図を作り直し忘れると Pull Request が落ちます。
図を意図的に変えた場合は `render.sh` を実行して結果をコミットしてください。

### 陰影について

`render_figures.py` の陰影はパッケージのシェーダーではありません。シルエットと奥行きが読めることだけを目的にした固定のライティングです。
シェーダーの見た目を示すのは `capture/` の実写の役目で、こちらの役目は「2 つのタイルの違いがパラメータだけである」ことを示すことです。

ただし、シェーダーの挙動そのものを図にしているものが 2 つあります。いずれも規約として文書化済みの入力だけを使っています。

- `distance-shrink` — 頂点を `UV3.xyz`（要素の根元）へ寄せる縮退。シェーダーと同じ `lerp` を `DumpFigures.cs` で適用しています
- `mesh-channels` — `UV0.y` と `UV3.w` をそのまま色に割り当てたもの

## Unity での実写

`capture/` の手順は [capture/README.md](capture/README.md) を参照してください。
