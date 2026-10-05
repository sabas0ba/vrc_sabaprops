# Udon Driver

Screen FX Volumeの`Weight`を実行時に操作するUdonSharpBehaviourです。VRChat Worlds SDKが必要です。

## 使い方

1. `GameObject > SabaProps > Screen FX`からVolumeを配置します
2. 空のGameObjectへ`ScreenFxDriver`を追加し、`Target`へVolumeのRendererを設定します
3. 用途に応じて`Follow Local Player`、`Activate On Trigger`、`Speed Linked`を設定します

fieldと公開イベントの一覧は、packageの`Documentation~/authoring.md`の「Udon Driver」を参照してください。

## 構成

| ファイル | 内容 |
| --- | --- |
| `ScreenFxDriver.cs` | Weightの補間、Trigger判定、移動速度への連動、頭への追従を行うUdonSharpBehaviour |
| `ScreenFxDriver.asset` | 上記のUdonSharp program asset |
