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
- 潮位変動、砕波直後のwhitecap、下流へ運ばれる残留泡を持つOcean／Lake表現
- 曝気開始点、縦渦、気泡、落下点のfroth／mistを持つRiver／Waterfall表現
- Box Projection、歪み、雨天時blurを調整できるPuddle Reflection Probe表現
- 上下と水平8方向を個別に選択できる水中境界Shaderとトンネル例
- 擬似質量、等幅の滴／軌跡、残留と遅延移動を持つWet Surface水滴

### Fixed

- Water Feature Gallery配布SampleのScene参照とMaterial、Mesh、Profileのmeta GUID不一致
- Gallery Groundが水中Cameraの視線を遮る配置と、水面境界端の過大な屈折
