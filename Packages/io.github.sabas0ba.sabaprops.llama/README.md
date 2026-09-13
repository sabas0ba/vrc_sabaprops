# SabaProps Llama

VRChatのPCワールドで、小型Llama系モデルをfragment shaderにより推論する実験パッケージです。Unity Editor拡張で重みをtextureへ変換し、UdonSharpが推論passとテキスト入出力を制御します。外部サーバー、native DLL、compute shaderは使用しません。

**初期実装です。Unity Editor・UdonSharp・VRChat実機での動作確認と、学習済みモデルでの速度・品質測定は未完了です。会話できる学習済みモデルは同梱していません。** 数値比較テストの存在と実行済みの結果は区別してください。

## 対応範囲

| 項目 | 内容 |
| --- | --- |
| Unity | リポジトリ検証環境は2022.3.22f1 |
| VRChat | 既存検証環境のWorlds SDK 3.10.4を使用。hashはリポジトリの`.github/verify/vrchat/packages.lock`に固定 |
| 重み | llama2.cのlegacy FP32 v0 checkpoint。little-endian、7個のint32ヘッダー、FP32配列 |
| tokenizer | 対応するtokenizer.bin。BOS=1、EOS=2、scoreによるBPEとbyte fallback |
| モデル演算 | RMSNorm（epsilon=1e-5）、隣接ペアRoPE（theta=10000）、MHA/GQA、因果Attention、SwiGLU |
| 出力 | Greedy argmax。温度・top-p・音声認識・音声合成は未対応 |
| Context | 変換時に1〜256、元モデルの上限以内。既定128 |
| モデル上限 | dimension 1024、hidden 4096、16 layers、32768語彙、重み256 MiB |
| 実行 | PC向け。各プレイヤーのローカル処理で、結果のネットワーク同期は行わない |

GGUF、safetensors、量子化checkpoint、Llama 3等の異なるtokenizer、RoPE scaling、chat templateは未対応です。ファイルの拡張子だけを変更しても読み込めません。モデルとtokenizerは対応した組み合わせが必要です。形式・語彙数の検査だけでは学習時の組み合わせまで保証できません。

## 導入と操作

1. VRChat Worlds SDKを導入済みのUnityプロジェクトに本パッケージを追加します。VPM配布前はリポジトリ内の`Packages/io.github.sabas0ba.sabaprops.llama`を、プロジェクトの`Packages`へコピーします。
2. `Tools > SabaProps > Llama > Import Model`を開きます。
3. 利用者が用意したcheckpointとtokenizerを指定し、Contextと重みの出典・ライセンスを入力します。ダウンロード機能はありません。
4. 「検査してtextureへ変換」を実行します。出力は`Assets/SabaPropsLlama/ImportedModel...`です。既存の出力を上書きせず、新しいフォルダーを作成します。
5. 「VRChat runtimeをimport」を実行し、Unity・UdonSharpのコンパイルが終わるまで待ちます。これは初回のみ必要です。既存runtimeを編集している場合は自動上書きしません。
6. 変換済みモデルを選択し、「選択モデルのWorld Runnerを作成」を実行します。既存のワールドシーンにCubeとテキスト入力・出力Canvasを追加します。ワールドのEventSystem、VRCSceneDescriptor、Spawnは既存設定を使用します。
7. シーン上で位置を調整して保存します。入力欄へ文章を入力し、CubeをInteractすると生成を開始します。生成中のInteractは停止です。

`Generate`と`StopGeneration`はUdon custom eventとしても呼び出せます。入力欄を接続しない場合はrunnerの`prompt`を使用します。テキストは入力の続きを生成するcompletion形式です。チャットモデル用の役割タグなどは自動挿入しません。

まず導入だけ確認する場合は「検証用モデルを作成（未学習）」を使用できます。入力例は`ab`です。小さい語彙と決定的な重みを持つ数値検証用モデルなので、自然な文章や会話は生成しません。

## 実行時の制限

- `passesPerFrame`は1〜8、既定1です。1フレームのGPU負荷と生成速度の調整値です。1pass自体の処理時間の上限を保証するものではありません。
- `maxNewTokens`は1〜64、既定32です。Context末尾に到達した場合も停止します。長い入力はエラーにし、履歴を黙って切り捨てません。
- 入力は256 UTF-16 code units以内です。初期tokenizeとBPEの探索もフレーム分割します。日本語の会話品質は使用する学習済みモデルによります。
- `maxWorkingMiB`は中間bufferとKVキャッシュの上限で、既定128 MiBです。重みtextureのVRAMとCPU側コピーは含みません。Editor変換時には元配列・padding配列・textureのコピーも一時的に必要です。
- 生成結果の1 pixelのみを非同期readbackします。GPU readback待機中に停止した場合は、callbackを破棄してから再実行を受け付けます。
- 各runnerは独立したMaterialとRenderTextureを確保します。複数runnerの同時利用はGPU負荷とメモリ使用量を増やします。
- 推論のためにフレーム数が必要です。6 layersでは138 passes/tokenとなり、既定1 pass/frame、90 FPSを仮定してもスケジューリングだけで約1.53秒/tokenです。prefillとreadback待機は別に加算されます。これは実測の速度ではありません。

Quest対応や実用的な日本語NPCとしての品質は、現段階の保証範囲に含みません。

## 変換後のデータ

| 出力 | 内容 |
| --- | --- |
| `Weights.asset` | 幅4096のRFloat texture。Linear、Point、Clamp、mipmapなし。元のfloat indexを行優先で配置 |
| `LlamaPassNNN.mat` | 演算種類・重みoffset・寸法を保存したMaterial |
| `Model.asset` | Editor用のモデル情報、tokenizer、演算スケジュール |
| `Provenance.json` | 入力2ファイルのSHA-256、Context、パッケージ版、出典・ライセンス |

重みoffsetは整数です。Unityのlegacy `Material.SetInt`のfloat経由の保存で大きなoffsetが丸められないよう、16bit単位の2値へ分割して保存します。

RuntimeはEditor用のModel.assetを参照しません。runnerへ設定値とMaterial参照を転記するため、ワールド内で動くコードはUdonSharpとshaderだけです。変換済み重み・語彙はワールドに含まれるので、元モデルの再配布条件に従ってください。

## 検証

### Nix / オフライン検査

リポジトリの`flake.nix`と`flake.lock`は、承認済みのdotfiles commit `9b9619ba2d1780157b426e2fa440744b45becfb2`と同じnixpkgs revision・narHashを使用します。本リポジトリの既存検査で使用している.NETとglslangを開発シェルへ定義し、ホストへのglobal導入は行いません。Unityは同梱しません。

リポジトリrootから実行します。

```bash
nix develop --command bash .github/verify/llama/verify.sh
```

Nixを利用しない場合は.NET SDK 8以降とglslangValidatorを用意し、同じscriptを実行できます。既存のVerify CIにも追加しています。NuGet restoreや追加ライブラリは不要です。

この検査はFP32形式のroundtrip、不正ファイルの拒否、参照推論の解析解・履歴依存・reset、tokenizer、およびfragment HLSLの型検査を行います。Unity API・Udon公開API・GPU上の数値一致は、この検査の対象外です。

### Unity / GPU比較

既存のUnity CIは本パッケージと`SabaProps.Llama.CITests`も読み込みます。Unityライセンスがなければ従来どおりjobをskipします。GPU比較はNull graphics deviceでskipし、成功した数値比較として扱いません。

実GPUのあるUnityで`.github/verify/CIProject/Assets/LlamaTests`をプロジェクトの`Assets`へコピーし、Test RunnerのEditModeで`SabaProps.Llama.CITests`を実行します。`-nographics`は使用しません。

- 全logitを独立したscalar CPU参照と比較（絶対誤差2e-4以内）。
- MHA/GQA、共有/非共有classifier、複数tokenとRoPE、Context末尾、resetを比較。
- 同一モデルの複数GPU sessionを交互に動かし、状態が混ざらないことを比較。
- 保存したRFloat texture・Materialの設定と、保存後のGPU推論を比較。

### VRChat SDK / 実機確認

既存の`.github/verify/vrchat/assemble.sh`と`run-tests.sh`へ本パッケージ・runtime import・数値比較を組み込みました。SDK取得は既存の固定hashを使用します。

この経路と実際のワールドで、次を確認する必要があります。

1. UdonSharpのコンパイルが通り、runnerのbacking UdonBehaviourに設定値が保存されること。
2. Build & Testで入力欄、Interact、停止・再実行、disable/enableを確認すること。
3. 実際の学習済みcheckpointでCPUとの一致を確認し、FPS・tokens/s・VRAM・日本語品質を記録すること。
4. VRChat upload後も同じ動作になること。

## 参照した仕様

- [VRChat VRCGraphics](https://creators.vrchat.com/worlds/udon/vrc-graphics/)
- [VRChat AsyncGPUReadback](https://creators.vrchat.com/worlds/udon/vrc-graphics/asyncgpureadback/)
- [llama2.c checkpoint・tokenizer形式](https://github.com/karpathy/llama2.c)

外部の推論ライブラリや学習済み重みは本パッケージへ取り込んでいません。
