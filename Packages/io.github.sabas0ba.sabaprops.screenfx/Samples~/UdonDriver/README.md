# Udon Driver

Screen FX を Udon から操作するデモです。Unity 2022.3.22f1、VRChat Worlds SDK 3.10.4 で検証します。追加の外部アセットは不要です。

## デモを開く

1. VRChat World 用プロジェクトへ Screen FX パッケージを導入します。
2. Package Manager の Screen FX → Samples → **Udon Driver** を Import します。
3. インポート先の `Demo/ScreenFxUdonDemo.unity` を開きます。
4. ClientSim の Play Mode または SDK の Build & Test で確認します。パネルは利用者本人の画面だけを操作し、他の参加者へ同期しません。

パネルの ON／OFF NOW、プリセット前後、GrabPass／Lite、Weight・Exposure・Particles の増減を使用できます。RESET PARAMETERS は現在のプリセット値へ戻します。プリセットと版を切り替えた際もパラメータは初期値に戻り、Weight は維持されます。

デモの効果はオンから **20 秒で自動停止**します。暗転系の効果でパネルが読めなくなった場合も復帰できます。OFF NOW とリスポーンは即時停止です。青い床の領域は、入ると水中効果、出るとフェードアウトする Trigger の独立例です。領域内で自動停止した場合は入り直してください。

デモを再生成する場合は `Tools > SabaProps > Screen FX > Create Udon Demo Scene` を実行します。`Assets/SabaProps/ScreenFx/UdonDemo` の空いている名前へ出力し、既存の編集内容は上書きしません。

## ワールドへコピーする

- **手動操作**：`Demo/ScreenFxControlRig.prefab` を配置するか、デモ内の `Screen FX - Copyable Control Rig` をルートごとコピーします。Driver・Volume・パネル・ボタンの参照はこのルート内で完結します。
- **領域連動**：`Demo/ScreenFxTriggerRig.prefab` を配置します。ルートの BoxCollider の Size と位置を編集します。Volume は頭へ追従するため、ルートを拡大縮小せず Collider の Size で領域を調整してください。
- パネルはルート内の `Control Panel` を移動して配置します。World に EventSystem がなければ Unity の `GameObject > UI > Event System` で一つ追加してください。Canvas には VRCUiShape が設定済みです。
- 同一プロジェクト内の別 Scene には Prefab だけを配置できます。別プロジェクトへ渡す場合は、このサンプル全体（スクリプト・Udon program asset・asmdef・Demo の Material）と Screen FX パッケージを導入してください。Hierarchy のコピーだけではアセット依存は移りません。
- 生成物を編集する場合は、そのフォルダーを Samples のインポート先から自分の管理フォルダーへ移動してください。サンプルの再 Import と利用者の改変を分離できます。
- `Auto Off Seconds` はコピー後も 20 秒です。用途に応じて変更し、無制限にする場合は 0 にします。共有 Material は変更せず、各 Driver が実行時の専用 Material を所有します。
- Trigger と手動操作の効果は独立しています。同時に有効にすると重なるため、単独比較時は手動側を OFF にしてください。GrabPass の負荷も有効な Volume 数に応じて増えます。

## 他の Udon から操作する

UdonSharp の参照フィールドに、コピーしたルートの ScreenFxDriver を設定します。

```csharp
public ScreenFxDriver effect;

public void EnableEffect()
{
    effect.SelectPreset(0);
    effect._FadeIn();
    effect.SetWeight(0.7f);
    effect.SetFloat("_Exposure", -0.5f);
}

public void DisableEffect()
{
    effect._StopImmediately();
}
```

上記には `using SabaProps.ScreenFx.Samples;` が必要です。独自 asmdef を使う場合は `SabaProps.ScreenFx.Sample` を参照します。Udon Graph や UI からは Driver の backing UdonBehaviour に `SendCustomEvent` で `_FadeIn`、`_FadeOut`、`_Toggle`、`_StopImmediately`、`_NextPreset`、`_PreviousPreset`、`_ResetPreset`、`_ToggleLite` を送れます。

`SetFloat` は Material に存在する float property だけを変更します。値の範囲は呼び出し側で制限してください（デモ UI は範囲を制限済み）。`_Weight` は専用の補間処理へ渡されます。Lite で存在しない property は無視します。複数項目を変えるときはプリセット選択後に設定してください。

## 検証範囲

自動検査は実 Udon コンパイル、Prefab 内の参照、ClientSim 上の実 Udon 経由のボタン操作・Material の独立性・Reset・自動停止・Trigger を対象とします。VRChat クライアント内の両眼表示、複数参加者、Android の描画・負荷は対象 World で確認してください。Android 向けは Lite を選択します。

## Driver を単独で配置する

1. `GameObject > SabaProps > Screen FX`からVolumeを配置します
2. 空のGameObjectへ`ScreenFxDriver`を追加し、`Target`へVolumeのRendererを設定します
3. 用途に応じて`Follow Local Player`、`Activate On Trigger`、`Speed Linked`を設定します

fieldと公開イベントの一覧は、packageの`Documentation~/authoring.md`の「Udon Driver」を参照してください。

## 構成

| ファイル | 内容 |
| --- | --- |
| `ScreenFxDriver.cs` | Weightの補間、Trigger判定、移動速度への連動、頭への追従を行うUdonSharpBehaviour |
| `ScreenFxDriver.asset` | 上記のUdonSharp program asset |
| `ScreenFxPanel.cs` / `.asset` | ボタンから Driver を操作し、現在値を表示する UdonSharpBehaviour |
| `Demo/` | 配置済み Scene、手動操作・Trigger の Prefab、プリセット Material |
| `CompiledPrograms/` | Prefab が参照するコンパイル済み Udon program asset |
| `Editor/` | デモ Scene と Prefab の再生成メニュー |
