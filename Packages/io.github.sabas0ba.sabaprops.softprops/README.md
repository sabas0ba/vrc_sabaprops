# SabaProps Soft Props

PC VRChat world向けの接触変形shaderと、ふとん、ベッド、ソファー、クッションのmodel／Prefab生成器です。VRChat Worlds SDK 3.10系のWorld Contactsを使用します。

## 要件

- Unity 2022.3
- VRChat Worlds SDK 3.10.x（3.10.4で検証）
- PC build target
- Built-in Render Pipeline

VPM依存として`com.vrchat.worlds 3.10.x`を宣言しています。3.10.4で検証し、VRChatのVPM互換性指針に従ってbreaking versionを3.10系へ限定します。Quest向けshader variantは収録していません。

## 導入と生成

1. VCC／ALCOMで本packageをworld projectへ追加する
2. Unityの`Tools > SabaProps > Soft Props > Generate All Prefabs`を実行する
3. `Assets/SabaProps/SoftPropsGenerated/Prefabs`から必要なPrefabをsceneへ置く

`Create Showcase In Scene`は4種をまとめてsceneへ配置します。generatorは以下を生成します。

| Prefab | 変形面 | 既定の性質 |
| --- | ---: | --- |
| Futon | 1 | 柔らかい、深い沈み込み、遅い復元、強めのしわ |
| Bed | 1 | 中程度に硬い、広い荷重分散、速い復元 |
| Sofa | 6 | 3座面＋3背面、座面と背面で異なる硬さ |
| Cushion | 1 | 最も柔らかい、局所的に深い、遅い復元 |
| ContactProbeTest | 1 | 指、棒、板のpickupで接触形状を比較する肌色test surface |

`Create Contact Probe Test In Scene`でtest Prefabを配置し、Build & Test後に3個のpickupを表面へ押し当てて確認できます。指は点状、棒はcapsule状、板はoriented box状のfootprintになります。

`DefaultSkinMatte.mat`はStandard lightingを用いるNonToon materialです。肌色、低Smoothness、微細なsurface grainを既定値とし、`ContactProbeTest`へ適用しています。shader自体の初期値も同じ方向へ揃えています。家具用Materialは織り目を明示的に有効化しています。

再生成時は上記生成先にある既知のMesh、Material、Prefabを置き換えます。生成物を直接変更する場合は別のfolderへ複製してください。

## 仕組み

各変形面は次の構成です。

1. 表面上方12 mm、下方最大18 mmの薄いBox型`VRCContactReceiver`がavatar標準Senderとtest probeを検出する
2. `SoftSurfaceContactController`が最大8 Senderの現在位置、回転、進入速度、接触tagを保持する
3. 荷重とfootprintを応答／復元の時定数で補間し、instance化したMaterialへlocal座標で渡す
4. `SabaProps/Soft Surface`がvertex stageで沈み込み、周辺隆起、横方向の逃げ、しわ、normal補正を計算する

Receiver外では荷重を生成しないため、物体の表面が接触する直前まで変形しません。Avatar側`ContactSenderProxy`から元のSender寸法は取得できないため、標準tagはFinger／Hand／Foot／Torsoごとの軽量な円形近似を使います。検証用の`SoftProbeFinger`、`SoftProbeRod`、`SoftProbePlate`は既知寸法とSender回転から形状別footprintを生成します。

接触状態はnetwork同期しません。各clientが既に受信しているavatar poseとWorld Contactsから同じ見た目を再構成するため、Udonのnetwork trafficは発生しません。

## 調整

Prefab内の変形面にある`SoftSurfaceContactController`で設定します。

| Parameter | 内容 |
| --- | --- |
| Hardness | 0で柔らかく深く、1で硬く浅く狭い変形になる |
| Maximum Indent | 最大沈み込み距離。visualのみでColliderは変形しない |
| Contact Radius | 1接触点の影響半径 |
| Finger Radius | Finger系tagの点接触半径 |
| Rod Half Length / Radius | `SoftProbeRod`のcapsule footprint |
| Plate Half Length / Width | `SoftProbePlate`のoriented box footprint |
| Rim Lift | 沈み込み外周の小さな隆起 |
| Wrinkle Strength / Frequency | 接触周囲のしわの振幅／密度 |
| Response Seconds | 荷重へ追従する時定数 |
| Recovery Seconds | 離れた後に復元する時定数 |
| Update Rate | Material parameter更新頻度。既定30 Hz |
| Impact Response | 進入速度による瞬間的な追加荷重 |

接触開始距離を変更する場合は、変形面の`VRCContactReceiver`にあるBoxのY寸法と中心位置を変更します。既定値は見た目の表面から上方12 mmです。大きくすると非接触に見える反応が再発するため、avatar Sender形状のばらつきを吸収できる範囲に留めてください。

実行開始時にcontrollerが同じ値をMaterialへ反映します。Material側の値だけを変更してもruntimeに上書きされるため、物理presetはcontroller側で調整してください。色、織り目、surface grain、SmoothnessはMaterialで変更します。

## 性能

- 非接触時はcontrollerごとに4 Hzまで更新頻度を下げる
- 接触時は既定30 Hzで最大8組の接触位置／footprint `Vector4`をMaterialへ設定する
- 変形はvertex stageのみ。接触点のloopをfragment stageへ持ち込まない
- World Contact Receiver数はFuton 1、Bed 1、Sofa 6、Cushion 1
- shaderは1頂点あたり最大8接触点を評価するため、遠距離家具ではRendererを無効化するかLODを設定する

VRChatのWorld Contactsにはworld全体でactive component数の上限があります。大量配置時の見積りは[performance.md](Documentation~/performance.md)を参照してください。

## 制限

- Unity Colliderおよびplayer capsuleは変形しません。見た目の沈み込みとcollision surfaceには最大沈み込み分の差が生じます。
- Contact permissionやSafe Modeでavatar Senderが無効な場合は変形しません。
- avatar標準Senderの形状はavatarごとに異なるため、厳密な圧力や質量のsimulationではありません。
- shaderはlocal +Y方向を表面法線として扱います。縦置きの背面cushionはGameObjectを回転して使用してください。
- 静的batchingは使用できません。shaderの`DisableBatching=True`は、object-localの接触座標を維持するために必要です。

詳細なmodel追加手順は[authoring.md](Documentation~/authoring.md)を参照してください。
