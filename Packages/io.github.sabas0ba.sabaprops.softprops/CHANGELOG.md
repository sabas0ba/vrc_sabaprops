# Changelog

## [0.2.0] - 2026-09-07

デモ・接地検出の機能追加と接触応答の変更を含むminor更新です。既存sceneは自動移行しません。[更新手順](Documentation~/upgrading.md)を参照してください。

- 共有Udon programをデモ外へ配置し、デモ削除時に生成家具の参照が失われる問題を修正。旧配置からはGUIDを維持して移動
- Senderへのslot再割り当て時に古いplayer所有情報を解除し、再接触によるSenderの上書きを防止

- 登録Colliderの底面侵入量から荷重を求め、接触前の先行変形と過大な凹みを抑制
- 実Colliderを上下させる3形状×3presetの自動比較台と距離・圧縮率の表示を追加
- player capsuleの衝突、およびローカルプレイヤーの接地状態と支持ColliderのRaycast照合から立位荷重を検出する経路を追加
- 棒の表示meshをCapsuleColliderと一致する円柱＋半球形状へ修正
- ClientSimで非接触・押下・復元・自動運動・立位荷重を検証する回帰テストを追加
- 新版sampleを別folderへ導入し、旧版の編集内容とGUIDを保護
- 照明・床・Spawn・家具・接触試験台・静的形状比較を含むレビュー用sceneをSamples~に同梱
- Open Demo Sceneメニューと導入済みsampleの参照検証を追加

## [0.1.0] - 2026-09-01

- World Contactsを使用する最大8点の接触変形controllerを追加
- PC向けの布地／フォーム変形shaderを追加
- ふとん、ベッド、ソファー、クッションのMesh／Material／Prefab生成器を追加
- 硬さ、沈み込み、影響半径、応答、復元、しわをprop単位で設定可能にした
- 接触開始を表面近傍へ限定し、非接触距離での先行変形を除去
- 指、棒、板のfootprintとpickup式ContactProbeTestを追加
- NonToonの低Smoothness肌色Materialを追加し、同心円状のしわを不規則なcreaseへ変更
