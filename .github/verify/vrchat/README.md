# VRChat ワールド検証プロジェクト

`Create Sample Scene` は、VRChat Worlds SDK が入っているプロジェクトでのみ
`VRCSceneDescriptor` と Spawn を配置します。この分岐はリフレクションで
SDK を参照しているため、SDK が無い環境ではコンパイルエラーにならず、
何も検証されないまま通ってしまいます。

ここにあるのは、その分岐を実際に実行するためのプロジェクト組み立て手順です。

## 方針

SDK の取得はコンテナ内で行い、ローカルの VCC / ALCOM のキャッシュには依存しません。
再現性の担保は次の 2 点です。

- パッケージは [`packages.lock`](packages.lock) に URL と SHA256 で固定。ハッシュが合わなければ失敗します
- ダウンロード環境は alpine イメージを digest で固定。busybox の `wget` / `unzip` / `sha256sum` のみを使い、
  コンテナ内でのパッケージ導入も行いません

Unity Editor だけはホストのものを使います。Unity をコンテナで動かすにはライセンスが必要で、
それは `.github/workflows/unity.yml` と同じ制約です（後述）。

## 手順

```sh
./.github/verify/vrchat/run-tests.sh
```

これだけで、SDK の取得（コンテナ）・プロジェクト組み立て・EditMode テスト実行までを行います。
個別に実行することもできます。

```sh
./.github/verify/vrchat/fetch.sh      # SDK をコンテナで取得し build/vpm へ展開
./.github/verify/vrchat/assemble.sh   # build/WorldProject を組む
```

`run-tests.sh` は Unity を 2 回起動します。1 回目でプロジェクト設定を VRChat 準拠にし、2 回目でテストを実行します。
Active Input Handling の変更は次回起動時にしか反映されないため、セッションを分けています。
設定内容は `Setup/FoliageWorldProjectSetup.cs` にあり、SDK の `UpdateLayers` と
ClientSim の `ClientSimProjectSettingsSetup` を呼ぶだけです。SDK コントロールパネルの
「Review Any Alerts」のボタンと同じ処理で、VCC がワールドテンプレートから作ったプロジェクトには
最初から入っているものです。

レイヤーと Collision Matrix は警告を消すためだけのものではありません。プレイヤーが何と衝突するかを決めるため、
これらが無い状態でのテストは VRChat の物理を検証したことになりません。

Unity は Unity Hub の既定の場所から `ProjectVersion.txt` に一致するバージョンを探します。
見つからない場合は `UNITY` に Editor の実行ファイルか Hub のインストールルートを指定してください。
コンテナエンジンは `podman`、無ければ `docker` を自動で選びます。`CONTAINER_ENGINE` で明示指定もできます。

初回は SDK が要求する UPM パッケージ（burst、collections、cinemachine 等）を
Unity がレジストリから取得するため、数分かかります。

テストは `SabaProps.Foliage.CITests` に絞って実行します。
SDK 自身のテストアセンブリも同じプロジェクトに存在しますが、
本パッケージとは無関係な理由で 2 件失敗する（ランダム生成の JSON ファズケースと、
docs.microsoft.com の URL 到達性を検証するもの）ため、終了コードを意味のあるものにするためです。

## テストの構成

| アセンブリ | 種別 | 内容 |
| --- | --- | --- |
| `SabaProps.Foliage.CITests` | EditMode | シーンが正しく作られているか。SDK の有無で期待値が切り替わります |
| `SabaProps.Foliage.WorldTests` | PlayMode | ClientSim でワールドとして実行し、プレイヤーが Spawn するか |

`WorldTests` は SDK を参照するため CI プロジェクト側には置けません。`Tests/` にあり、
`assemble.sh` がワールドプロジェクトへコピーします。

ClientSim は VRChat クライアントのエディタ内ランタイムです。`VRCSceneDescriptor` を読んで
ローカルプレイヤーを生成するため、descriptor の設定が間違っていればプレイヤーは出ません。

なお ClientSim は起動時、入力システムが注入される前に `ClientSimPlayerController.Update` が走り、
`NullReferenceException` を毎回 1 回出します（その直後に `ClientSim Initialized` に到達します）。
SDK 側の起動順の問題でこちらから直せないため、ワールドが立ち上がるまでの区間に限って
`LogAssert.ignoreFailingMessages` で許容しています。

## これで検証できること

`FoliageVrcWorld` が SDK を見つけ、`VRCWorld` ルートと Spawn を作り、
`VRCSceneDescriptor` を実際に AddComponent できること。
`FoliageSampleSceneTests.SampleScene_MatchesTheVrchatSdkThatIsInstalled` が
SDK の有無を見て期待値を切り替えるため、同じテストが両方の環境で意味を持ちます。

リフレクションで設定している `spawns` / `RespawnHeightY` / `ReferenceCamera` の
いずれかが SDK 側で改名されていた場合は、テストではなく Console の警告として現れます。

## これでは検証できないこと

実機の VRChat へアップロードした結果。ビルド＆アップロードには VRChat アカウントでの
ログインが必要で、自動化の対象外です。

## SDK のバージョンを上げるには

[`packages.lock`](packages.lock) の該当行のバージョン・SHA256・URL を、
公式リスティング <https://packages.vrchat.com/official> の値に差し替えて
`fetch.sh` を再実行してください。ハッシュが合わない場合は取得を失敗させます。

## Unity 自体をコンテナで動かす場合

GameCI のイメージを使えば Unity もコンテナ化できますが、ライセンスが必要です。
`UNITY_EMAIL` / `UNITY_PASSWORD` / `UNITY_LICENSE` を用意したうえで
`.github/workflows/unity.yml` と同じ構成にしてください。
またコンテナへ 8 GB 程度のメモリ割り当てが要ります
（podman machine の既定は小さいことが多いので、`podman machine set --memory` で拡張が必要です）。
