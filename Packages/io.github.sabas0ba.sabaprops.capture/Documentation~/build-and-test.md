# Build & Test での確認手順

自動検証では確かめられない項目を、VRChat クライアント上で確認する手順です。対象は次の 4 点です。

- `VRCGraphics.Blit` が保存枠へ画像を複製すること
- `Camera.Render()` が撮影時にだけ描画すること
- 実行時に `RenderTexture` を生成、破棄できること
- Quest で上記が動作すること

自動検証が確かめる範囲は [README の「検証」](../README.md#検証) を参照してください。

## 準備

1. VRChat Worlds SDK 3.10.x を導入したワールドプロジェクトに、このパッケージを追加します
2. `GameObject > SabaProps > Capture Recorder with Playback Panel` で Recorder と再生パネルを配置します
3. Recorder を次のように設定します。満杯までの時間を短くし、1 回の確認を数十秒で終えるための値です

| 項目 | 値 |
| --- | --- |
| `Interval` | 1 |
| `Frame Width` / `Frame Height` | 256 x 144 |
| `Max Frames` | 8 |
| `Memory Budget Megabytes` | 0 |

入力は 2 通りを確認します。

| 構成 | 設定 |
| --- | --- |
| Texture 入力 | 常時描画している Camera の `Target Texture` に使っている RenderTexture を、Recorder の `Source Texture` に割り当てます |
| Camera 入力 | 無効にした Camera を Recorder の `Source Camera` に割り当てます。その Camera の `Culling Mask` から Water を外します |

どちらの構成でも、時間とともに見た目が変わる物体 (回転する物体など) を画角に入れておくと、画像の違いを判別できます。

## 確認項目

以下の枚数と時間は、上の設定を前提にしています。

| 番号 | 操作 | 期待する結果 |
| --- | --- | --- |
| 1 | `REC / PAUSE` を押す | 状態表示が `REC` になります。すぐに 1 枚目が撮られ、以後 1 秒ごとに `n / 8 frames` の n が増えます。プレビューは最新の画像に変わり、`x MB` は 4 枚目で 1 になります |
| 2 | Camera 入力で 1 を行う | 画像内の物体の前後関係が正しく描かれます。再生パネルは画像に映り込みません。無効にした Camera は、撮影の瞬間以外は描画しません |
| 3 | `Full Policy` を 2 (間引き) にして満杯まで待つ | 8 枚に達した 1 秒後に 5 枚へ減り、状態表示が `every 2 s` になります。`FIRST` で表示される画像は、撮影開始時の画像のままです |
| 4 | `Full Policy` を 0 (上書き) にして満杯まで待つ | 枚数は 8 枚のまま変わりません。`FIRST` で表示される画像の時刻が、1 秒ごとに進みます |
| 5 | `Full Policy` を 1 (停止) にして満杯まで待つ | 8 枚目を撮った時点で状態表示が `FULL` になり、経過時間が止まります |
| 6 | タイムラインをドラッグする。`<` / `>`、`PLAY / PAUSE`、`SLOWER` / `FASTER` を押す | ドラッグした位置の画像が表示されます。コマ送りと再生が動作し、再生中は状態表示が `PLAY x8` などになります |
| 7 | 撮影中に `FIRST` を押し、その後 `LATEST` を押す | `FIRST` の後は、新しい画像が撮られても表示が変わりません (`HOLD`)。`LATEST` の後は、撮影のたびに表示が最新の画像へ移ります |
| 8 | 満杯になる前に `REC / PAUSE` を押し、数秒後にもう一度押す | 一時停止中は状態表示が `PAUSED` になり、経過時間が進みません。再開後の撮影は、一時停止した時間を除いて 1 秒間隔を保ちます |
| 9 | `CLEAR` を押す | 枚数が 0 になり、`NO FRAMES` を表示します。`x MB` は変わりません |
| 10 | `RELEASE VRAM` を押す | `x MB` が 0 になります。その後 `REC / PAUSE` を押すと、再び 1 から撮影できます |

`Full Policy` は Inspector で設定する値です。3〜5 は、値を変えてビルドし直したうえで、それぞれ行います。

## Quest での確認

Android 向けにビルドし、実機で確認項目の 1〜5 を行います。確かめる点は次の 2 つです。

- 撮影のたびに画像が更新されること。保存枠は深度を持たないため、`VRCGraphics.Blit` の書き込み先の制約には該当しない想定です
- `x MB` の表示が、設定した `Memory Budget Megabytes` を超えないこと。確認時は `Memory Budget Megabytes` を 1 に設定します。256 x 144 の ARGB32 では保持できる枚数が 7 枚になります
