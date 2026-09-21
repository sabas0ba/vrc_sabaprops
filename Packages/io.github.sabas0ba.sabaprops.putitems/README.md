# SabaProps Put Items

VRChat の Pickup を手放したとき、近くの静止した机・壁へ接触点と姿勢を補正する UdonSharp パッケージです。面に沿った位置は維持し、接触基準の +Y を面の +Y に合わせます。面の法線まわりの回転を追加せず、傾きだけを最短回転で補正します。

[デモの導入とレビュー](Documentation~/demo-review.md)に操作例と確認項目、[配置と同期の設定](Documentation~/authoring.md)に各コンポーネントの設定と追従構成を記載しています。

## 導入

Unity 2022.3 / VRChat Worlds SDK 3.10.4 を使用します。VPM で追加するか、このパッケージを Unity プロジェクトの Packages に配置します。

最初に Tools > SabaProps > Put Items > Prepare Udon Programs を実行します。Assets/SabaProps/PutItemsPrograms に4個のプログラムアセットを生成します。Create Placement Demo からも同じ準備を行います。

1. 机の天板または壁の表面に空の GameObject を作り、PlacementSurface を追加します。ローカル XZ を吸着面、+Y を物を置く側に合わせ、size で矩形の大きさを指定します。Collider は吸着計算には不要ですが、通常の物理衝突には必要です。
2. Prop の底面または背面に接触基準の子 Transform を配置します。+Y は Prop の内側を向けます。Prop の原点が接触点なら自身の Transform も使用できます。
3. Prop に PlacementSolver を追加し、surfaces に対象面を登録します。maximumDistance は表側の吸着距離、maximumPenetration は許容するめり込み、maximumTilt は補正する傾きの上限です。
4. VRCObjectSync / VRCPickup / Rigidbody と同じ GameObject に ObjectSyncPlacement を追加し、solver と contact を指定します。category と面の acceptedCategories に共通ビットがある面だけが候補になります。
5. normalKinematic / normalGravity を、その Prop の通常の物理設定に合わせます。これは別プレイヤーが再取得した場合にも使用する復元値です。

Tools > SabaProps > Put Items > Open Demo Scene で、同梱の Kitchen Demo を Assets/SabaProps/PutItemsKitchenDemoV2 に導入して開きます。旧 Demo を導入済みの場合も、旧 Scene を上書きせず更新版を開きます。食卓、椅子、シンク、食器棚、冷蔵庫を含み、コップ、皿、フォーク、ナイフ、スプーンを机へ、買い物メモ、鍵、アクセサリーチャームを冷蔵庫へ配置できます。机と椅子の Collider は VRChat の Walkthrough Layer にあり、プレイヤーが通り抜けられます。各操作対象は手に取り、吸着面へ近付けて離してください。面の境界と category の違いも確認できます。

食卓のおぼんには、個別に拾える皿、フォーク、スプーンと、皿上の料理があります。おぼんを動かすと全体が、皿を動かすと料理が追従します。皿・カトラリーはおぼんと机に置き直せます。机に置くとおぼんからの追従は解除されます。看板の `03 / TRAY` に操作例を記載しています。

冷蔵庫のドアは壁面配置の実例です。紙は背面、鍵とチャームは磁石の面を接触基準にしています。説明看板の `01 / TABLE` と `02 / FRIDGE` が操作手順を示します。デモ用のメッシュとマテリアルはパッケージに同梱し、外部モデルやテクスチャへ依存しません。

Tools > SabaProps > Put Items > Developer > Create Minimal Test Rig は、吸着計算だけを確認するための従来の最小構成です。既存の VRChat ワールドに導入する場合は、こちらで面と Prop の設定を確認できます。

## 標準の Object Sync 接続

ドロップを受けた owner が、次のフレームで一度だけ候補を計算します。補正直前に owner と未保持状態を再確認し、投射速度を消去して kinematic にし、位置・回転を適用します。補正結果と物理状態の配信は Object Sync に委ねます。静止面だけを使う場合、追加の同期変数・ネットワークイベントは送信しません。

再取得、所有権変更、コンポーネント無効化で保留中の補正を取り消します。所有権変更だけでは新しい吸着を開始しません。再取得時には設定した通常の物理状態へ戻します。Allow Collision Ownership Transfer は意図しない owner 変更を避けるため無効を推奨します。

既存の Respawn / Pool 処理からは、owner 上で CancelAndRestorePhysics() を呼んでから位置を戻してください。無効化だけでは静止保持を解除しません。標準接続を使う Prop では、他のスクリプトが姿勢・kinematic・gravity を同時に更新しないようにしてください。

## 移動する面への追従

移動面の PlacementSurface.carrier に親側の ObjectSyncPlacement を指定します。子 Prop の ObjectSyncPlacement.followState には、子 GameObject 上の PlacementFollowState を指定します。state の placement は子 Prop を指し、surfaceIndex は PlacementSolver.surfaces における接続先の添字です。初期状態から接続する場合は localPosition / localRotation に carrier から見た子の姿勢を設定します。親の carriedItems には、直接の子に加えて、追従させる孫以降も登録します。おぼん・皿・料理の設定例は Kitchen Demo Scene にあります。

配置した子の owner が相対姿勢を毎フレーム計算し、Object Sync が各 Prop の位置を配信します。親を拾った owner は、接続中の子孫の所有権を取得してから移動します。子を個別に拾うと接続を解除し、移動面へ置き直すと接続先と相対姿勢を更新します。接続状態と相対姿勢だけを別 GameObject の Manual 同期変数へ保存するため、同じ GameObject 上の Object Sync と同期モードを混在させません。親子連鎖は16段まで、接続に循環を含めないでください。追従対象の Transform は一様スケール1を使用してください。

親を別の制御で動かす場合も、親の owner 変更時に子孫を同じ owner へ引き継ぐ必要があります。移動面に既存の UdonSynced 制御がある場合は、その制御の所有権・姿勢更新と追従の順序を合わせてください。

## 独自の UdonSynced との接続

独自同期では ObjectSyncPlacement を追加せず、既存の制御から PlacementSolver.TryFindPose(item, contact, category) を呼びます。計算には副作用がなく、Transform や同期変数を書き換えません。戻り値が true の場合、resultPosition / resultRotation / resultSurface が使用できます。次の検索で結果は上書きされ、失敗時は hasResult が false になり結果は消去されます。

呼び出し元はドロップを確定してから計算し、実際に姿勢を同期する GameObject の owner と未保持状態を適用直前に確認します。補正結果を自身の姿勢・物理状態・同期変数へ反映し、Manual 同期ならその変数を持つ Behaviour から RequestSerialization() を呼びます。補間用の目標値も同時に更新してください。リモート側は既存の受信処理だけで反映します。

PlacementSurface、PlacementSolver、ObjectSyncPlacement は NoVariableSync を使用します。PlacementFollowState だけは子 GameObject 上で Manual 同期を使用します。Object Sync と同じ GameObject の同期モードに影響させないための構成です。独自同期で保持状態を管理する場合は、owner 退出と途中参加で復元できる状態を既存の同期データに含めてください。

## 対象範囲

- 静止した矩形面への配置と保持、および明示的に carrier を設定した移動面への親子追従を扱います。Prop 同士の重なり回避は含みません。
- Prop と吸着面の transform 階層には正の一様スケールを使用してください。鏡映・shear は対象外です。面の寸法は size で調整します。
- 接触基準は Prop 自身またはその子に置きます。1個の計算呼び出しにつき接触基準は1つです。
- footprintRadius は接触点を中心とする占有円の半径 (m) です。矩形の皿なら対角線の半分以上にすると、はみ出しを保守的に排除できます。0 は一点だけを検査します。
- 距離が最小の適合面を選び、同距離では surfaces の配列順を使用します。
- 通常の投擲を残す用途では、吸着距離・カテゴリ・傾きの上限を調整してください。速度による投擲判定は行いません。

## 検証

Tests~ に Unity EditMode テストを収録しています。実行環境と手順はリポジトリの .github/verify/vrchat/PUT_ITEMS.md を参照してください。幾何計算のテストだけではマルチプレイヤー同期の実証にはなりません。

実機では2クライアント以上を使い、机と壁への配置、おぼん→皿→料理の入れ子追従、別プレイヤーによる親・子の再取得、owner 退出、途中参加、ドロップ直後の再取得、リセット、独自同期の姿勢更新と補正の競合を確認します。確認基準は、全員で同じ位置・向きになり、再取得後やリセット後に古い補正が再適用されないことです。

## 公式仕様

- [VRC Object Sync](https://creators.vrchat.com/worlds/components/vrc_objectsync/)
- [Network Variables](https://creators.vrchat.com/worlds/udon/networking/variables/)
- [Object Ownership](https://creators.vrchat.com/worlds/udon/networking/ownership/)
- [UdonSharp Attributes](https://creators.vrchat.com/worlds/udon/udonsharp/attributes/)
- [Unity Layers in VRChat](https://creators.vrchat.com/worlds/layers/)
