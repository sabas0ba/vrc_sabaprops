# 設計

このパッケージがなぜこの形をしているかの記録です。実装を読めば分かることは書かず、読んでも分からない「なぜ他の形にしなかったか」を残します。

## Canvas を使わない理由

VRChat の World Space Canvas は、操作時に UI レーザーを表示し、見た目も平面のパネルになります。タブレットを手元に出す用途では、レーザーより指で押す方が操作対象との距離に合っています。

ボタンは Collider とメッシュで構成し、押下は `TabletController` が指先のボーン位置から判定します。Desktop とレーザーでの操作は、同じ Collider の `Interact` で受けます。Canvas の入力経路を使わないため、`VRCUiShape` も EventSystem も不要です。

文字は TextMeshPro の 3D 表示 (`TextMeshPro`) で描き、`TextMeshProUGUI` は使いません。

## 指先の押下判定を 1 か所で行う理由

ボタンごとに Update で指先を調べると、ボタン数に比例して Udon の外部呼び出しが増えます。`TabletController` がタブレット表示中だけ、両手の指先の位置を 1 回ずつ取得し、本体から `interactionRadius` 以内にある場合だけボタンを走査します。非表示の間は判定処理を行いません。

判定はボタンの `BoxCollider` の local 座標で行います。領域は前面が local -Z 側、キャップが +Z 側にあり、前面からの押し込み量が `pressDepthRatio` を超えたときに押下とします。

状態遷移は 3 状態です。

| 状態 | 遷移 |
| --- | --- |
| Idle | 領域の前面から浅い位置に入ると Armed |
| Armed | 押し込むと Pressed (ここで 1 回押下)。前面より手前へ出ると Idle |
| Pressed | `releaseDepthRatio` より浅く戻ると Armed |

Idle から直接 Pressed へは遷移しません。横や裏から深い位置に入った指、ページが切り替わった直後に新しいボタンと重なった指で押下が発生しないようにするためです。解除の深さを押下の深さより浅くしているのは、境界付近の手の揺れで押下が連続しないようにするためです。この性質はオフライン層で検査しています (`a press fires once until the finger retracts`)。

## 継承を使わないモジュール構成

各機能 (`TabletToggle`、`TabletTeleport`) は独立した UdonSharpBehaviour で、共通の基底クラスを持ちません。ボタンは「呼び出し先の UdonSharpBehaviour とイベント名」の組だけを持ちます。

UdonSharp 1.x は継承に対応していますが、このリポジトリの他のパッケージは継承を使っておらず、オフライン層の `UdonSharpStub.cs` も対応していません。また、イベント名で呼ぶ形にしておくと、このパッケージを知らない既存のギミック (Stage Cam の操作パネルなど) もそのまま呼び出し先にできます。基底クラスを要求すると、既存のギミックを操作するには必ず中継用のクラスが必要になります。

引数が必要な場合は、呼び出し先の `tabletArgument` へ `SetProgramVariable` で書き込んでからイベントを送ります。Udon の `SendCustomEvent` が引数を取れないための、Udon で一般的な受け渡し方法です。

## 定義と生成物を分ける理由

構成は Editor 専用の `TabletDefinition` (`IEditorOnly`) に持ち、Build でボタンとモジュールを生成します。

生成物を直接編集する方式では、ボタンの追加のたびに配置、マテリアル、呼び出し先、`TabletController.buttons` への登録を手作業で揃える必要があり、1 か所の登録漏れがそのボタンの無反応になります。定義から毎回作り直すことで、生成物は常に定義と一致し、Theme の差し替えも Build し直すだけで済みます。

代償として、生成物に加えた手作業の変更は次の Build で失われます。生成後のページに独自の表示を加える場合は、`TabletBuilder.Built` で毎回加えます。Stage Cam サンプルがその実装例です。

`TabletDefinition` と `TabletTheme` は Udon のアセンブリとは別の `SabaProps.Tablet.Authoring` に置いています。`List`、enum、`IEditorOnly` を使う Editor 用のデータを、UdonSharp のコンパイル対象から外すためです。

## Theme を焼き込む理由

Udon は ScriptableObject を実行時に参照できません。Theme の値は Build 時にメッシュ、マテリアル、TextMeshPro の設定へ変換し、実行時のコンポーネントには Theme への参照を残しません。

マテリアルは状態ごと (通常、ON、押下中) に用意し、`TabletButton` は `sharedMaterial` を差し替えます。MaterialPropertyBlock で色だけを変える方式より状態の数だけマテリアルが増えますが、Theme でシェーダーやテクスチャごと差し替えられます。

## 表示をローカルにする理由

タブレットの位置、表示、ページは同期しません。各プレイヤーが同じ GameObject を持ち、自分の画面でだけ動かします。

タブレットは個人の操作パネルであり、他のプレイヤーに見せる必要がないためです。同期すると所有権の奪い合いになり、Pickup の Transform を他のクライアントへ送る帯域も必要になります。

切り替えの結果を全員で共有したい場合は、`TabletToggle` の `global` で状態だけを同期します。同期するのは 1 つの bool で、Manual 同期のため変更時にだけ送ります。

## 取っ手を本体の子に置く理由

本体全体を Pickup にすると、ボタンの Collider が Pickup の Rigidbody の一部になり、ボタンを指したときに Interact と Pickup のどちらが反応するかが VRChat 側の優先順位に依存します。

取っ手だけを Pickup にし、掴んでいる間は取っ手の姿勢から本体の位置を逆算して移動させ、取っ手は本体に対する定位置へ戻します。取っ手の local 回転を単位回転に固定しているため、逆算は位置の差だけで済みます。この往復はオフライン層で検査しています (`the handle round trip restores the body pose`)。

## 頭上の位置を水平方向の向きで決める理由

取り出し位置 `anchorOffset` は、頭の回転そのものではなく、頭の向きを水平面へ投影した方位で回します。頭の回転をそのまま使うと、見上げたときに頭上の位置が後方へ、見下ろしたときに前方へ動き、手を伸ばす位置が姿勢によって変わるためです。

## 検証の当て方

| 層 | 何を確かめるか |
| --- | --- |
| `.github/verify/verify.sh` | Runtime、Authoring、Editor、Stage Cam サンプルが実物の `VRCSDKBase.dll`、`VRCSDK3.dll`、`VRC.Udon.Common.dll` に対してコンパイルできること。召喚位置、押下の状態遷移、取り出し位置、プレイヤー選択、配置計算、角丸メッシュの閉包性と面の向きを実行して検査すること |
| `.github/verify/vrchat/` | UdonSharp が実際にコンパイルできること。サンプルシーンの生成後、すべてのボタンの呼び出し先がエクスポートされたイベントであること。Toggle の排他が動作すること |

TextMeshPro はオフライン層では手書きのスタブです。Unity のパッケージ内のソースであり、参照アセンブリとして配布されていないためです。

ClientSim はリモートプレイヤーを作れず、VR の入力も再現しません。指先の押下、Grab による取り出し、Global な Toggle の同期は実機で確認します。
