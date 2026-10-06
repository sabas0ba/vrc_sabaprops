using UnityEngine;

namespace SabaProps.ScreenFx.Editors
{
    public enum ScreenFxValueKind
    {
        Float,
        Color,
        Vector,
    }

    /// <summary>One material property a preset moves away from the shader default.</summary>
    public struct ScreenFxValue
    {
        public string property;
        public ScreenFxValueKind kind;
        public Vector4 value;

        public ScreenFxValue(string property, ScreenFxValueKind kind, Vector4 value)
        {
            this.property = property;
            this.kind = kind;
            this.value = value;
        }
    }

    public sealed class ScreenFxPreset
    {
        public readonly string id;
        public readonly string displayName;
        public readonly string description;
        public readonly ScreenFxValue[] values;

        public ScreenFxPreset(string id, string displayName, string description, params ScreenFxValue[] values)
        {
            this.id = id;
            this.displayName = displayName;
            this.description = description;
            this.values = values;
        }
    }

    /// <summary>
    /// The bundled conditions. Each one lists only the properties it changes;
    /// everything else stays at the shader default, which is "no effect".
    /// Plain data over UnityEngine value types, so the offline checks can load it.
    /// </summary>
    public static class ScreenFxPresets
    {
        private static ScreenFxValue F(string property, float value) =>
            new ScreenFxValue(property, ScreenFxValueKind.Float, new Vector4(value, 0f, 0f, 0f));

        private static ScreenFxValue C(string property, float r, float g, float b, float a) =>
            new ScreenFxValue(property, ScreenFxValueKind.Color, new Vector4(r, g, b, a));

        private static ScreenFxValue V(string property, float x, float y, float z) =>
            new ScreenFxValue(property, ScreenFxValueKind.Vector, new Vector4(x, y, z, 0f));

        public static readonly ScreenFxPreset[] All =
        {
            new ScreenFxPreset("Rain", "Rain", "雨。落下する雨筋と、視界に付いて流れる水滴。",
                F("_Particle", 0.8f), C("_ParticleColor", 0.75f, 0.85f, 1f, 0.35f),
                F("_ParticleSize", 0.04f), F("_ParticleDensity", 9f), V("_ParticleVelocity", 0.15f, 3.5f, 0f),
                F("_ParticleStretch", 14f),
                F("_LensDrops", 0.45f), F("_LensDropSlide", 0.35f),
                F("_Saturation", 0.85f), F("_Exposure", -0.3f),
                C("_FogColor", 0.55f, 0.6f, 0.66f, 1f), F("_FogVeil", 0.08f)),

            new ScreenFxPreset("Storm", "Storm", "豪雨。斜めに吹き付ける雨、暗い視界、多量の水滴。",
                F("_Particle", 1f), C("_ParticleColor", 0.75f, 0.85f, 1f, 0.4f),
                F("_ParticleSize", 0.04f), F("_ParticleDensity", 14f), V("_ParticleVelocity", 1.2f, 5f, 0f),
                F("_ParticleStretch", 18f),
                F("_LensDrops", 0.8f), F("_LensDropSlide", 0.7f),
                F("_Saturation", 0.7f), F("_Exposure", -0.8f), F("_Blur", 0.08f), F("_Wobble", 0.03f),
                C("_FogColor", 0.4f, 0.45f, 0.5f, 1f), F("_FogVeil", 0.2f)),

            new ScreenFxPreset("Snow", "Snow", "雪。揺れながらゆっくり落ちる雪片と、わずかに白い空気。",
                F("_Particle", 0.8f), C("_ParticleColor", 1f, 1f, 1f, 0.9f),
                F("_ParticleSize", 0.12f), F("_ParticleDensity", 6f), V("_ParticleVelocity", 0.1f, 0.35f, 0f),
                F("_ParticleSway", 0.6f),
                C("_Tint", 0.9f, 0.95f, 1f, 0.4f), F("_Exposure", 0.2f), F("_Saturation", 0.85f),
                C("_FogColor", 0.9f, 0.93f, 0.96f, 1f), F("_FogVeil", 0.08f)),

            new ScreenFxPreset("Blizzard", "Blizzard", "吹雪。横殴りの雪、白い遮蔽、縁の凍結。",
                F("_Particle", 1f), C("_ParticleColor", 1f, 1f, 1f, 0.9f),
                F("_ParticleSize", 0.1f), F("_ParticleDensity", 12f), V("_ParticleVelocity", 2.2f, 0.9f, 0f),
                F("_ParticleStretch", 2f),
                F("_Frost", 0.35f), F("_Saturation", 0.6f), F("_Blur", 0.1f),
                C("_FogColor", 0.88f, 0.92f, 0.96f, 1f), F("_FogVeil", 0.45f), F("_FogNoise", 0.4f)),

            new ScreenFxPreset("Speed", "Speed", "高速移動。放射状のぶれ、集中線、周辺の色ずれ。",
                F("_RadialBlur", 0.6f), F("_SpeedLines", 0.7f), F("_Chromatic", 0.25f),
                F("_Vignette", 0.25f), F("_VignetteRadius", 0.9f)),

            new ScreenFxPreset("Drunk", "Drunk", "酩酊。ゆっくりした揺れ、二重像、ぼけ、重いまぶた。",
                F("_Wobble", 0.7f), F("_WobbleSpeed", 0.8f), F("_DoubleVision", 0.5f),
                F("_Blur", 0.25f), F("_Chromatic", 0.2f), F("_Saturation", 1.25f),
                F("_BlinkAuto", 0.25f), F("_Vignette", 0.2f)),

            new ScreenFxPreset("Underwater", "Underwater", "水中。青緑の吸収、揺らぎ、漂う粒子、コースティクス。",
                C("_Tint", 0.25f, 0.65f, 0.8f, 0.85f),
                F("_Wobble", 0.25f), F("_WobbleSpeed", 1.4f), F("_WobbleScale", 3f),
                F("_Blur", 0.15f), F("_Chromatic", 0.15f),
                C("_FogColor", 0.02f, 0.2f, 0.3f, 1f), F("_FogVeil", 0.25f), F("_FogDensity", 0.07f),
                F("_Caustics", 0.35f),
                F("_Particle", 0.35f), C("_ParticleColor", 0.8f, 0.95f, 1f, 0.25f),
                F("_ParticleSize", 0.05f), V("_ParticleVelocity", 0.05f, -0.15f, 0f), F("_ParticleSway", 0.5f),
                F("_Vignette", 0.3f)),

            new ScreenFxPreset("Mud", "Mud", "泥の中。茶色の濁り、強いぼけ、視界に付着して垂れる泥。",
                C("_Tint", 0.45f, 0.3f, 0.15f, 0.9f), F("_Exposure", -1f), F("_Saturation", 0.6f),
                C("_FogColor", 0.16f, 0.1f, 0.05f, 1f), F("_FogVeil", 0.7f), F("_FogDensity", 0.9f),
                F("_Blur", 0.6f), F("_Wobble", 0.1f), F("_WobbleSpeed", 0.4f),
                F("_Splat", 0.75f), C("_SplatColor", 0.2f, 0.13f, 0.07f, 1f)),

            new ScreenFxPreset("Dark", "Dark", "暗がり。低露出、色の喪失、狭い視野、ノイズ。",
                F("_Exposure", -2.2f), F("_Saturation", 0.45f), F("_Contrast", 0.9f),
                C("_Tint", 0.7f, 0.8f, 1f, 0.5f),
                F("_Vignette", 0.85f), F("_VignetteRadius", 0.35f), F("_VignetteSoftness", 0.7f),
                F("_Grain", 0.35f),
                C("_FogColor", 0f, 0f, 0f, 1f), F("_FogDensity", 0.12f)),

            new ScreenFxPreset("Tension", "Tension", "緊張。心拍に合わせて狭まる視野、周辺のぼけ、硬い階調。",
                F("_Vignette", 0.6f), F("_VignetteRadius", 0.55f), F("_Pulse", 0.8f), F("_PulseRate", 1.6f),
                F("_Chromatic", 0.2f), F("_Saturation", 0.7f), F("_Contrast", 1.2f), F("_Grain", 0.15f),
                F("_Blur", 0.25f), F("_BlurEdge", 1f)),

            new ScreenFxPreset("Heat", "Heat", "熱気。立ち上る陽炎、暖色の空気、わずかなまぶしさ。",
                F("_Haze", 0.7f), C("_Tint", 1f, 0.85f, 0.65f, 0.5f), F("_Exposure", 0.3f),
                F("_Saturation", 1.1f), F("_Glare", 0.15f), C("_GlareColor", 1f, 0.8f, 0.5f, 1f),
                F("_Blur", 0.1f), F("_BlurEdge", 1f),
                F("_Vignette", 0.25f), C("_VignetteColor", 0.6f, 0.2f, 0f, 1f)),

            new ScreenFxPreset("Cold", "Cold", "冷気。縁から広がる霜、青白い色調。",
                F("_Frost", 0.55f), C("_Tint", 0.75f, 0.88f, 1f, 0.6f),
                F("_Saturation", 0.7f), F("_Contrast", 1.05f), F("_Blur", 0.05f),
                F("_Vignette", 0.3f), C("_VignetteColor", 0.8f, 0.9f, 1f, 1f)),

            new ScreenFxPreset("Humid", "Humid", "高湿度。白く曇る視界、細かい結露、弱い陽炎。",
                C("_FogColor", 0.85f, 0.88f, 0.85f, 1f), F("_FogVeil", 0.22f),
                F("_Blur", 0.18f), F("_Haze", 0.12f), F("_Saturation", 0.9f), F("_Glare", 0.08f),
                F("_LensDrops", 0.35f), F("_LensDropScale", 18f), F("_LensDropSlide", 0.02f)),

            new ScreenFxPreset("Fog", "Fog", "霧。流れるむらのある白い遮蔽と、低い彩度・階調。",
                C("_FogColor", 0.72f, 0.76f, 0.78f, 1f), F("_FogVeil", 0.5f), F("_FogDensity", 0.12f),
                F("_FogNoise", 0.5f), F("_Saturation", 0.75f), F("_Contrast", 0.85f), F("_Blur", 0.06f)),

            new ScreenFxPreset("Glare", "Glare", "まぶしさ。光源方向で強まる白飛びと、にじみ。",
                F("_Glare", 0.8f), C("_GlareColor", 1f, 0.97f, 0.88f, 1f),
                V("_GlareDirection", 0.3f, 0.8f, 0.5f), F("_GlareFocus", 6f),
                F("_Exposure", 1.2f), F("_Contrast", 0.8f), F("_Saturation", 0.8f), F("_Blur", 0.12f),
                F("_Vignette", 0.35f), C("_VignetteColor", 1f, 1f, 1f, 1f), F("_VignetteRadius", 0.7f)),

            new ScreenFxPreset("Sandstorm", "Sandstorm", "砂嵐。横に流れる砂粒、黄褐色の遮蔽、ざらつき。",
                F("_Particle", 1f), C("_ParticleColor", 0.85f, 0.7f, 0.45f, 0.5f),
                F("_ParticleSize", 0.06f), F("_ParticleDensity", 12f), V("_ParticleVelocity", 3.5f, 0.3f, 0f),
                F("_ParticleStretch", 6f),
                C("_Tint", 1f, 0.8f, 0.55f, 0.7f),
                C("_FogColor", 0.75f, 0.6f, 0.4f, 1f), F("_FogVeil", 0.55f), F("_FogDensity", 0.15f),
                F("_FogNoise", 0.5f), F("_Grain", 0.25f), F("_Blur", 0.08f)),

            new ScreenFxPreset("Smoke", "Smoke", "煙。暗い流動する遮蔽、舞い上がる火の粉。",
                C("_FogColor", 0.18f, 0.18f, 0.18f, 1f), F("_FogVeil", 0.6f), F("_FogNoise", 0.8f),
                F("_Saturation", 0.5f), F("_Blur", 0.15f), F("_Haze", 0.2f),
                F("_Particle", 0.3f), C("_ParticleColor", 1f, 0.5f, 0.1f, 0.9f),
                F("_ParticleSize", 0.04f), V("_ParticleVelocity", 0.1f, -0.6f, 0f), F("_ParticleSway", 0.7f),
                F("_Vignette", 0.4f)),

            new ScreenFxPreset("Fire", "Fire", "火災。強い陽炎、橙色の光、火の粉。",
                F("_Haze", 0.5f), C("_Tint", 1f, 0.7f, 0.45f, 0.6f), F("_Exposure", 0.2f),
                F("_Glare", 0.2f), C("_GlareColor", 1f, 0.55f, 0.2f, 1f),
                C("_FogColor", 0.3f, 0.16f, 0.08f, 1f), F("_FogVeil", 0.25f), F("_FogNoise", 0.6f),
                F("_Particle", 0.5f), C("_ParticleColor", 1f, 0.55f, 0.15f, 1f),
                F("_ParticleSize", 0.04f), F("_ParticleDensity", 8f),
                V("_ParticleVelocity", 0.15f, -0.9f, 0f), F("_ParticleSway", 0.8f),
                F("_Vignette", 0.4f), C("_VignetteColor", 0.7f, 0.2f, 0f, 1f)),

            new ScreenFxPreset("Drowsy", "Drowsy", "眠気。周期的に落ちるまぶた、ぼけ、暗い視界。",
                F("_Blink", 0.15f), F("_BlinkAuto", 0.9f), F("_BlinkRate", 0.22f),
                F("_Blur", 0.3f), F("_Saturation", 0.8f), F("_Exposure", -0.3f),
                F("_Vignette", 0.45f), F("_Wobble", 0.12f), F("_WobbleSpeed", 0.4f)),

            new ScreenFxPreset("Dizzy", "Dizzy", "めまい。速い揺れ、二重像、色ずれ。",
                F("_Wobble", 0.5f), F("_WobbleSpeed", 2.2f), F("_DoubleVision", 0.35f),
                F("_RadialBlur", 0.2f), F("_Chromatic", 0.3f), F("_Vignette", 0.3f)),

            new ScreenFxPreset("Damage", "Damage", "負傷。心拍に合わせて脈打つ赤い縁、色の喪失。",
                F("_Vignette", 0.7f), C("_VignetteColor", 0.6f, 0f, 0f, 1f), F("_VignetteRadius", 0.5f),
                F("_Pulse", 0.6f), F("_PulseRate", 1.3f),
                F("_Saturation", 0.6f), F("_Chromatic", 0.3f), F("_Blur", 0.2f), F("_BlurEdge", 1f)),

            new ScreenFxPreset("Poison", "Poison", "毒。緑がかった色調、うねり、強い色ずれ。",
                C("_Tint", 0.65f, 1f, 0.6f, 0.6f), F("_Wobble", 0.35f), F("_Chromatic", 0.45f),
                F("_Saturation", 1.3f), F("_DoubleVision", 0.2f),
                F("_Vignette", 0.45f), C("_VignetteColor", 0.1f, 0.3f, 0f, 1f),
                F("_Pulse", 0.4f), F("_PulseRate", 0.8f)),

            new ScreenFxPreset("Dream", "Dream", "夢・回想。周辺のぼけ、淡い色、白い縁。",
                F("_Blur", 0.2f), F("_BlurEdge", 0.8f), F("_Glare", 0.25f),
                F("_Saturation", 0.55f), F("_Contrast", 0.85f), C("_Tint", 1f, 0.95f, 0.85f, 0.6f),
                F("_Vignette", 0.5f), C("_VignetteColor", 1f, 1f, 1f, 1f)),

            new ScreenFxPreset("Faint", "Faint", "失神寸前。閉じかけたまぶた、狭い視野、色の喪失。",
                F("_Blink", 0.55f), F("_Vignette", 0.9f), F("_VignetteRadius", 0.25f),
                F("_Saturation", 0.2f), F("_Blur", 0.5f), F("_Exposure", -1f),
                F("_Pulse", 0.5f), F("_PulseRate", 0.9f), F("_DoubleVision", 0.3f)),
        };

        public static ScreenFxPreset Find(string id)
        {
            foreach (ScreenFxPreset preset in All)
            {
                if (preset.id == id)
                {
                    return preset;
                }
            }

            return null;
        }
    }
}
