# Changelog

## [0.1.0] - 2026-08-30

### Added

- 水たまり、川、湖、海向けの軽量・標準水面Shader
- Scene View上で地面へ追従させる水たまりstamp tool
- pathから河川Meshを生成するauthoring component
- 衝突時のsplashと波紋を含む雨rig generator
- 局所霧、広域霧、雲layerのgeneratorとvolume Shader
- 水中の色収差、歪み、コースティクス、light shaft用Shaderとvolume generator
- 全Featureを比較してcopyできるWater Feature GalleryサンプルSceneと再生成menu
- 非周期の複合波、深度色、波頭／岸泡、重なり可能な3層疑似波紋
- 斜面、浅深差、Whitewater、Waterfall Sprayを含むRiverサンプル
- 濃度、色、静的Point Light散乱を比較するFogサンプル
- 水中から水上を見るためのLite／Standard水面裏面Shader
- procedural水滴とwetnessを持つWet Surface Shaderおよび人型proxyサンプル
- Worlds SDKを任意検出するVRCWorld、Spawn、VRCSceneDescriptor設定menu

### Fixed

- Water Feature Gallery配布SampleのScene参照とMaterial、Mesh、Profileのmeta GUID不一致
