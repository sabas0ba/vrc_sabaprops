# 更新と配布

## 0.1.0から0.2.0への更新

0.2.0は自動比較デモ、登録Colliderによる接触評価、立位荷重の補助検出を追加します。接触応答も変更しているため、旧版で調整した値は再確認してください。

1. UnityのPlayを停止し、編集したsceneとPrefabを保存・バックアップします。
2. VCC/VPMでpackageを更新します。開発中のembedded packageではリポジトリの対象packageを更新します。
3. Unityのimportとコンパイル完了を待ちます。
4. `Tools > SabaProps > Soft Props > Open Demo Scene`を実行します。
5. 新版の自動比較と立位荷重を確認し、既存worldへ設定を反映します。

新版デモは`Assets/SabaProps/SoftPropsDemoMotion/SoftPropsDemo.unity`へ導入されます。旧版の`Assets/SabaProps/SoftPropsDemo`は変更しません。既に新版folderが存在する場合も上書きせず、そのsceneを開きます。再導入が必要な場合は、そのfolderをUnityのProjectビューで別名へ移動し、メニューを再実行してください。

## 既存Prefab・sceneの注意点

package更新だけでは、利用者が生成・配置したPrefabの設定を新版へ自動移行しません。特に以下を確認してください。

| 対象 | 更新時の確認 |
| --- | --- |
| World probe | `Probe Colliders`と同順の`Probe Kinds`を設定する。指=0、棒=1、板=2 |
| 棒の表示 | 旧版の縦に引き伸ばしたCapsule meshを使用する場合、表示とCollider形状を照合する |
| 立位荷重 | 支持Colliderとcontrollerを同じGameObjectに置き、`Player Standing Load`、`Surface Half Size`、`Surface Plane Y`を確認する |
| 接触応答 | 登録Colliderは底面が未変形面へ侵入したときだけ荷重を生成する。固定Collider上へ置くだけでは侵入しない場合がある |
| 調整値 | controllerが実行開始時にMaterialへ値を反映する。Materialだけで硬さを調整しない |

`Generate All Prefabs`は`Assets/SabaProps/SoftPropsGenerated`の既知の生成assetsを置き換えます。編集済みの生成物には直接実行せず、バックアップまたは別projectで生成した新版と比較してください。

UdonSharp programの重複を避けるため、`Samples~`の手動コピーは行わず専用メニューを使用します。GUIDは導入時に再割当てし、既存controller programがある場合は共有します。

## バージョン方針

Soft Propsの版は自身の`package.json`で管理し、Foliageとは独立して更新します。0.xは開発段階です。機能追加や接触応答・設定の変更はminor、機能・設定を変えない修正はpatchを更新し、移行上の注意をCHANGELOGへ記載します。0.x間の無条件の互換性は保証しません。

## リリース手順（maintainer向け）

1. packageのversionとCHANGELOGを更新し、PRでレビューします。
2. ドキュメント・manifestのCI検査と、SDK付きUnityでの関連テストを確認します。VRChat実clientでの手動確認結果も区別して記録します。
3. マージ後、公開対象commitにpackage固有タグを付けます。0.2.0のタグ名は`io.github.sabas0ba.sabaprops.softprops/v0.2.0`です。
4. Build Releaseのpackage zipと、Build VPM Listingの更新結果を確認します。

複数packageを含むリポジトリのため、Soft Propsだけの公開には上記のpackage固有タグを使います。PR作成とRelease公開は別操作です。versionの更新だけではVCCのリスティングに公開されません。
