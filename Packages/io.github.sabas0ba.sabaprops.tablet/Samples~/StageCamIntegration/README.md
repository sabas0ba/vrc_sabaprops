# Stage Cam Integration

SabaProps Stage Cam の操作パネル (`StageCamControlPanel`) を、タブレットのページから操作する実装例です。`io.github.sabas0ba.sabaprops.stagecam` が必要です。

## 使い方

1. シーンに Stage Cam のリグと、SabaProps Tablet のタブレットを配置します
2. `Tools > SabaProps > Tablet > Samples > Add Stage Cam Page` を実行します

シーンに `StageCamControlPanel` が無ければ、すべてのリグを登録したパネルを作成します。タブレットに `Stage Cam` ページを追加して Build します。

## 構成

| ファイル | 内容 |
| --- | --- |
| `Editor/TabletStageCamPage.cs` | `Stage Cam` ページの項目を追加するメニューと、Build 後に映像と追従対象の表示を加える `TabletBuilder.Built` の処理 |
| `TabletStageCamDisplay.cs` | 選択中のカメラの映像と追従対象を表示する UdonSharpBehaviour |

ボタンは `CustomEvent` 項目として `StageCamControlPanel` のイベント (`_NextCamera`、`_TargetSelf` など) を直接呼びます。独自の UI を持つ他のギミックも、呼び出し先とイベント名を登録するだけで同じように操作できます。
