# Changelog

## [0.1.0] - 2026-08-30

### Added

- アバター側の準備なしで対応する受け面へ水滴を投影するDroplet Projectorと比較Scene

- 暗所、Point Light、Spot Lightで水面・雨・Wet Surfaceを比較する`WaterLightingGallery` Scene
- 水面Foamの模様を調整するscale、speed、warpパラメータ
- 不透明・半透明のWet Surface Shaderと、滴head・trail別のNormal強度

- 水たまり、川、湖、海向けの軽量・標準水面Shader
- Scene View上で地面へ追従させる水たまりstamp tool
- pathから河川Meshを生成するauthoring component
- 衝突時のsplashと波紋を含む雨rig generator
- 局所霧、広域霧、雲layerのgeneratorとvolume Shader
- 水中の色収差、歪み、コースティクス、light shaft用Shaderとvolume generator
- 全Featureを比較してcopyできるWater Feature GalleryサンプルSceneと再生成menu
- 非周期の複合波、深度色、重なり可能な3層疑似波紋
- 斜面、浅深差、小径のWaterfall／Plunge Pool Sprayを含むRiverサンプル
- 濃度、色、静的Point Light散乱を比較するFogサンプル
- 水中から水上を見るためのLite／Standard水面裏面Shader
- procedural水滴とwetnessを持つWet Surface Shaderおよび人型proxyサンプル
- Worlds SDKを任意検出するVRCWorld、Spawn、VRCSceneDescriptor設定menu
- 潮位変動、局所的なBreaking Wave Sprayを持つOcean／Lake表現
- 速度方向へ伸長する落差上端／着水点のRiver／Waterfall飛沫
- Box Projection、歪み、雨天時blurを調整できるPuddle Reflection Probe表現
- 上下と水平8方向を個別に選択できる水中境界Shaderとトンネル例
- 擬似質量、等幅の滴／軌跡、残留と遅延移動を持つWet Surface水滴
- 主光源、Light Probe SH、Point／Spot Lightへ反応する水面・雨滴・飛沫・波紋
- Ocean砕波、River落差上端／着水点向けの小径・速度伸長spray particle

### Fixed

- Wet Surfaceのtrailが滴headを追い越す形状と、円形だった滴headを重力方向へ膨らむ雨滴形状へ修正
- Ocean・River・雨衝突の飛沫を点に近い数mm級へ縮小

- Water Feature Gallery配布SampleのScene参照とMaterial、Mesh、Profileのmeta GUID不一致
- Gallery Groundが水中Cameraの視線を遮る配置と、水面境界端の過大な屈折
- 連続した白帯に見える既定foam mesh、過大な円形spray、過剰な水中volume歪み
- Wet Surface水滴が途中で消える終端表現と、小滴が暗色化する散乱色
