# Water Feature Gallery

`WaterFeatureGallery.unity`を開き、Play Modeで雨、波紋、霧、雲を確認します。

- `1 Water Surfaces`: Reflection Probe付きPuddle、曝気・落水付きRiver、砕波泡を持つLake／OceanのLite／Standard比較
- `2 Rain and Ripples`: 雨滴collision、splash、ripple
- `3 Fog and Clouds`: Lite、濃霧、着色霧、Point Light、Particle fog、cloud
- `4 Underwater`: 水中歪み、上面／inactiveの8方向境界例、コースティクス、light shaft
- `5 Wet Surfaces and VRChat`: Dry／Wet／質量差・残留軌跡付きDroplets比較、VRCWorld／Spawn

Hierarchyで`[Copy Ready]`と付いたrootは、対象Sceneへそのままコピーできます。
Worlds SDK導入projectでは`Tools > SabaProps > Water > Configure VRChat World Descriptor`で
`VRCSceneDescriptor`を追加できます。
操作と性能上の注意はpackageの`Documentation~/sample-gallery.md`を参照してください。
