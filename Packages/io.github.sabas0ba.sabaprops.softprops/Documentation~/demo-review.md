# デモの導入とレビュー

## 開く

PC用のVCC World projectへpackageを追加後、`Tools > SabaProps > Soft Props > Open Demo Scene`を選択します。変更中のsceneがある場合は保存確認が表示されます。

初回は同梱済みsampleを`Assets/SabaProps/SoftPropsDemo`へimportし、`SoftPropsDemo.unity`を開きます。MeshやPrefabの事前生成、手動配置は不要です。2回目以降は導入済みsceneを開き、利用者の編集内容を上書きしません。

UdonSharpのprogram重複を避けるため、sampleの導入には上記メニューを使用します。既に家具を生成したprojectでは、そのcontroller programを共有します。`Samples~`の手動コピーによる重複導入は避けてください。

## シーンの内容

![Unityで描画したデモ全景](demo-overview.png)

| 場所 | 内容 | 確認方法 |
| --- | --- | --- |
| 手前左 | LIVE CONTACT TEST。肌Materialの試験面と指・棒・板のPickup | VRChatのBuild & Testで操作 |
| 手前右 | STATIC SHAPE REFERENCE。同一mesh・Material設定・荷重で3接触形状を比較 | Scene/GameビューでPlay前から確認可能 |
| 奥 | Futon、Bed、Sofa、Cushion | 外観・寸法・配置を確認し、VRChatで接触 |

床Collider、照明、overview camera、VRCSceneDescriptor、Spawnを含みます。シーン内の英語ラベルはUnity内蔵fontを使用し、外部fontの導入を必要としません。

## レビュー項目

![同一荷重で指・棒・板の接触形状を比較](demo-footprints.png)

1. **肌Material**: 手前右の3試験面で、光沢、肌色、微細grainを確認します。照明方向を変えた際の見た目も確認してください。
2. **接触形状**: 静的比較では、指が局所的な凹み、棒が細長い凹み、板が平坦な底を持つ広い凹みになることを確認します。これは固定shader入力による参照表示で、接触イベントの動作証明ではありません。
3. **実接触**: VRChat SDKのBuild & Testで起動し、手前左のPickupを持って試験面へ近づけます。Receiverは未変形表面から上方12 mmで検出を開始します。Senderの見た目と判定形状には差があり得ます。
4. **離脱と復元**: Pickupを表面から離し、元の形へ戻ることを確認します。棒と板は表面と平行に当てて比較してください。大きく傾けた接触は厳密な接触面計算ではありません。
5. **家具**: 手・頭・胴体等のSenderが許可されたavatarで各家具に触れ、沈み込みと復元の違いを確認します。Sofaは座面と背面がそれぞれ変形します。

Unity Play Mode / ClientSimだけでは、VRChat実clientのWorld Contactsと同じ動作を保証しません。接触・Pickup・複数clientでの見え方の最終確認はBuild & Testで行ってください。

## 現段階の制限

- Colliderは固定です。物体を置くと、shaderの沈み込み面と物理的な支持面に差が生じます。
- 指・棒・板は簡易probeで、指の解剖学的modelではありません。
- 質量や圧力の測定に基づくsimulationではありません。標準avatar Senderの寸法を取得できないため部位別近似です。
- 寝転び・着座専用のStationやanimationは含みません。
- 静的参照3面は常に変形しています。実接触の開始距離は左のLIVE CONTACT TESTで確認してください。

## 配布シーンの再生成（開発者向け）

独立したUnity 2022.3.22f1 / VRCSDK 3.10.4検証projectで`SabaProps.SoftProps.Editors.SoftPropsDemo.GenerateSample`を`-executeMethod`として実行します。これは生成先の家具assetsを再生成する開発用APIです。

シーンと依存assetsを`Samples~/ReviewDemo`へ出力し、生成用assetsとは別のGUIDに変換します。package内のshader、script、SDK参照は元のGUIDを維持します。既存sampleのGUIDは再生成時も維持します。Unity実描画のPNGは検証projectの`ReviewCaptures`へ出力します。
