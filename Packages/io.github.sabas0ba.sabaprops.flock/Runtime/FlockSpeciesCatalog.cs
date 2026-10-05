using System.Collections.Generic;
using UnityEngine;

namespace SabaProps.Flock
{
    /// <summary>Environment a preset is meant for. Used to group the presets in menus.</summary>
    public enum FlockHabitat
    {
        Sky = 0,
        Sea = 1,
        Reef = 2,
        Aquarium = 3,
    }

    /// <summary>A built-in species preset.</summary>
    public sealed class FlockPreset
    {
        public FlockHabitat Habitat;
        public FlockSpecies Species;
    }

    /// <summary>
    /// Built-in species. Sizes, wing and fin proportions, wing beat and swimming
    /// frequencies are rounded from typical adult values; colours are simplified
    /// to the regions that remain readable at mid to far distance. Every preset
    /// is generated from these parameters and ships no external model or
    /// texture.
    /// </summary>
    public static class FlockSpeciesCatalog
    {
        private static List<FlockPreset> _presets;

        /// <summary>All presets, birds first. Each call returns the same list; clone a species before editing it.</summary>
        public static IReadOnlyList<FlockPreset> All
        {
            get
            {
                if (_presets == null)
                {
                    _presets = new List<FlockPreset>();
                    AddBirds(_presets);
                    AddSeaFish(_presets);
                    AddReefFish(_presets);
                    AddAquariumFish(_presets);
                    AddAdditionalSpecies(_presets);
                }

                return _presets;
            }
        }

        /// <summary>A copy of the preset with the given id, or null.</summary>
        public static FlockSpecies Create(string id)
        {
            foreach (FlockPreset preset in All)
            {
                if (preset.Species.id == id)
                {
                    return preset.Species.Clone();
                }
            }

            return null;
        }

        private static Color C(int rgb)
        {
            return new Color(
                ((rgb >> 16) & 0xff) / 255f,
                ((rgb >> 8) & 0xff) / 255f,
                (rgb & 0xff) / 255f,
                1f);
        }

        private static void Add(List<FlockPreset> list, FlockHabitat habitat, FlockSpecies species)
        {
            list.Add(new FlockPreset { Habitat = habitat, Species = species });
        }

        private static void AddAdditionalSpecies(List<FlockPreset> list)
        {
            AddMarine(list, "squid", "イカ", 0.4f, FlockBodyShape.Squid, FlockAnimation.Jet, 0xd5c4b9, 0x886f64);
            AddMarine(list, "jellyfish", "クラゲ", 0.3f, FlockBodyShape.Jellyfish, FlockAnimation.Pulse, 0xc4d9e0, 0x849dbb);
            AddMarine(list, "garden-eel", "チンアナゴ", 0.35f, FlockBodyShape.GardenEel, FlockAnimation.Tentacles, 0xdfd5b8, 0x504b3d, true);
            AddMarine(list, "crab", "カニ", 0.16f, FlockBodyShape.Crab, FlockAnimation.Tentacles, 0xa95e3c, 0xd88c5e, true);
            AddMarine(list, "eel", "ウナギ", 0.8f, FlockBodyShape.Eel, FlockAnimation.Undulate, 0x3c4636, 0xa3aa87, false, FlockHabitat.Aquarium);
            AddMarine(list, "urchin", "ウニ", 0.1f, FlockBodyShape.Urchin, FlockAnimation.Static, 0x332337, 0x281a2d, true);
            AddMarine(list, "anemone", "イソギンチャク", 0.18f, FlockBodyShape.Anemone, FlockAnimation.Tentacles, 0x9a7158, 0xc0a880, true);
            AddMarine(list, "oyster", "カキ", 0.15f, FlockBodyShape.Oyster, FlockAnimation.Static, 0x797767, 0xb8b29f, true);
            AddMarine(list, "seahorse", "タツノオトシゴ", 0.15f, FlockBodyShape.Seahorse, FlockAnimation.Undulate, 0xb39a52, 0x867343);

            FlockSpecies s = Fish("flying-fish", "トビウオ", 0.25f, FlockFishBody.Fusiform, 0.18f, 0.12f, FlockCaudalFin.Forked, 0.3f);
            s.bodyShape = FlockBodyShape.FlyingFish; s.pectoralSize = 0.65f;
            s.primary = C(0x397482); s.secondary = C(0xc4d8d7); s.detail = C(0x8dadae); s.extra = C(0x7c9695);
            s.cruiseSpeed = 1.2f; s.beatFrequency = 4f; s.beatAmplitude = 0.05f;
            s.defaultPattern = FlockPattern.Stream; s.defaultCount = 16; s.defaultArea = new Vector3(12f, 2f, 12f);
            Add(list, FlockHabitat.Sea, s);

            s = Bird("chicken", "ニワトリ", 0.45f, 0.35f, FlockWingShape.Rounded, FlockTailShape.Fan, 0.35f);
            s.bodyShape = FlockBodyShape.Chicken; s.grounded = true; s.animation = FlockAnimation.Walk;
            s.primary = C(0xe2d7bc); s.secondary = C(0x796f57); s.accent = C(0xe9ddc3); s.detail = C(0xb83429); s.extra = C(0xc69a49);
            s.cruiseSpeed = 0.35f; s.beatFrequency = 2f; s.beatAmplitude = 0.12f;
            s.defaultPattern = FlockPattern.Wander; s.defaultCount = 4; s.defaultArea = new Vector3(3f, 0.001f, 3f);
            Add(list, FlockHabitat.Sky, s);

            s = Bird("chick", "ヒヨコ", 0.08f, 0.08f, FlockWingShape.Rounded, FlockTailShape.Short, 0.1f);
            s.bodyShape = FlockBodyShape.Chick; s.grounded = true; s.animation = FlockAnimation.Walk;
            s.primary = C(0xe9cf69); s.secondary = C(0xd4b852); s.accent = C(0xf4dc7d); s.detail = C(0x8c6f36); s.extra = C(0xd5a04b);
            s.cruiseSpeed = 0.15f; s.beatFrequency = 3f; s.beatAmplitude = 0.12f;
            s.defaultPattern = FlockPattern.Wander; s.defaultCount = 8; s.defaultArea = new Vector3(1f, 0.001f, 1f);
            Add(list, FlockHabitat.Sky, s);
        }

        private static void AddMarine(List<FlockPreset> list, string id, string name, float length,
            FlockBodyShape shape, FlockAnimation animation, int bodyColor, int appendageColor,
            bool anchored = false, FlockHabitat habitat = FlockHabitat.Sea)
        {
            FlockSpecies s = Fish(id, name, length, FlockFishBody.Fusiform, 0.3f, 0.3f, FlockCaudalFin.Rounded, 0.15f);
            s.bodyShape = shape; s.animation = animation;
            s.primary = C(bodyColor); s.secondary = C(appendageColor); s.accent = C(bodyColor);
            s.detail = C(appendageColor); s.extra = C(appendageColor); s.sizeVariance = 0.04f;
            s.beatFrequency = shape == FlockBodyShape.Jellyfish ? 0.6f : animation == FlockAnimation.Jet ? 0.8f : 1.2f;
            s.beatAmplitude = animation == FlockAnimation.Static ? 0f : animation == FlockAnimation.Pulse ? 0.15f : 0.06f;
            s.cruiseSpeed = length * 0.5f;
            s.defaultPattern = anchored ? FlockPattern.Anchored : animation == FlockAnimation.Jet ? FlockPattern.Jet
                : shape == FlockBodyShape.Jellyfish ? FlockPattern.Float : FlockPattern.Wander;
            s.defaultCount = anchored ? 1 : 5;
            float horizontal = Mathf.Max(12f * length, 2f);
            s.defaultArea = new Vector3(horizontal, Mathf.Max(2f * length, 0.5f), horizontal);
            Add(list, habitat, s);
        }

        // ------------------------------------------------------------------
        // Birds
        // ------------------------------------------------------------------

        private static FlockSpecies Bird(
            string id, string name, float length, float wingspan,
            FlockWingShape wing, FlockTailShape tail, float tailLength)
        {
            return new FlockSpecies
            {
                id = id,
                displayName = name,
                category = FlockCategory.Bird,
                animation = FlockAnimation.Flap,
                bodyLength = length,
                wingspan = wingspan / length,
                wingShape = wing,
                tailShape = tail,
                tailLength = tailLength,
                sizeVariance = 0.08f,
            };
        }

        private static void AddBirds(List<FlockPreset> list)
        {
            FlockSpecies s;

            s = Bird("sparrow", "スズメ", 0.14f, 0.22f, FlockWingShape.Rounded, FlockTailShape.Square, 0.32f);
            s.primary = C(0x8a6a4a); s.secondary = C(0xd8cfc0); s.accent = C(0x7a4a2a); s.detail = C(0x3a2e24); s.extra = C(0x2a2a2a);
            s.beakLength = 0.08f; s.neckLength = 0.05f;
            s.beatFrequency = 14f; s.beatAmplitude = 55f; s.glide = 0.1f; s.cruiseSpeed = 9f;
            s.defaultPattern = FlockPattern.Cruise; s.defaultCount = 40; s.defaultArea = new Vector3(40f, 10f, 40f);
            Add(list, FlockHabitat.Sky, s);

            s = Bird("white-eye", "メジロ", 0.12f, 0.18f, FlockWingShape.Rounded, FlockTailShape.Square, 0.35f);
            s.primary = C(0x7a9a3a); s.secondary = C(0xd8d8b0); s.accent = C(0x8aa640); s.detail = C(0x4a5a2a); s.extra = C(0x2a2a2a);
            s.beakLength = 0.08f;
            s.beatFrequency = 16f; s.beatAmplitude = 55f; s.glide = 0.1f; s.cruiseSpeed = 8f;
            s.defaultPattern = FlockPattern.Cruise; s.defaultCount = 20; s.defaultArea = new Vector3(25f, 6f, 25f);
            Add(list, FlockHabitat.Sky, s);

            s = Bird("starling", "ムクドリ", 0.24f, 0.39f, FlockWingShape.Pointed, FlockTailShape.Short, 0.2f);
            s.primary = C(0x3a3634); s.secondary = C(0x4a4440); s.accent = C(0x6a645e); s.detail = C(0x2a2624); s.extra = C(0xe0a030);
            s.beakLength = 0.1f;
            s.beatFrequency = 12f; s.beatAmplitude = 45f; s.glide = 0.25f; s.cruiseSpeed = 15f;
            s.defaultPattern = FlockPattern.Murmuration; s.defaultCount = 300; s.defaultArea = new Vector3(80f, 30f, 80f);
            Add(list, FlockHabitat.Sky, s);

            s = Bird("swallow", "ツバメ", 0.17f, 0.33f, FlockWingShape.Sickle, FlockTailShape.DeepFork, 0.45f);
            s.primary = C(0x1a2240); s.secondary = C(0xf0ece4); s.accent = C(0x8a2a1a); s.detail = C(0x101830); s.extra = C(0x1a1a1a);
            s.beakLength = 0.04f;
            s.beatFrequency = 8f; s.beatAmplitude = 45f; s.glide = 0.35f; s.cruiseSpeed = 11f;
            s.defaultPattern = FlockPattern.Cruise; s.defaultCount = 25; s.defaultArea = new Vector3(40f, 10f, 40f);
            Add(list, FlockHabitat.Sky, s);

            s = Bird("swift", "アマツバメ", 0.19f, 0.43f, FlockWingShape.Sickle, FlockTailShape.Forked, 0.3f);
            s.primary = C(0x2a2a2c); s.secondary = C(0x3a3a3c); s.accent = C(0x2e2e30); s.detail = C(0x1e1e20); s.extra = C(0x1a1a1a);
            s.beakLength = 0.03f;
            s.beatFrequency = 9f; s.beatAmplitude = 35f; s.glide = 0.4f; s.cruiseSpeed = 18f;
            s.defaultPattern = FlockPattern.Cruise; s.defaultCount = 30; s.defaultArea = new Vector3(60f, 20f, 60f);
            Add(list, FlockHabitat.Sky, s);

            s = Bird("pigeon", "カワラバト", 0.32f, 0.67f, FlockWingShape.Pointed, FlockTailShape.Square, 0.3f);
            s.primary = C(0x8a8e96); s.secondary = C(0x9a9ea6); s.accent = C(0x4a6a6a); s.detail = C(0x3a3c40); s.extra = C(0x3a3a3a);
            s.beakLength = 0.05f; s.neckLength = 0.08f; s.wingTipFraction = 0.2f;
            s.beatFrequency = 8f; s.beatAmplitude = 50f; s.glide = 0.2f; s.cruiseSpeed = 16f;
            s.defaultPattern = FlockPattern.Cruise; s.defaultCount = 40; s.defaultArea = new Vector3(50f, 12f, 50f);
            Add(list, FlockHabitat.Sky, s);

            s = Bird("crow", "ハシブトガラス", 0.56f, 1.0f, FlockWingShape.Slotted, FlockTailShape.Fan, 0.35f);
            s.primary = C(0x141418); s.secondary = C(0x18181c); s.accent = C(0x141418); s.detail = C(0x101014); s.extra = C(0x101010);
            s.beakLength = 0.12f; s.neckLength = 0.06f;
            s.beatFrequency = 4f; s.beatAmplitude = 40f; s.glide = 0.3f; s.cruiseSpeed = 12f;
            s.defaultPattern = FlockPattern.Cruise; s.defaultCount = 12; s.defaultArea = new Vector3(60f, 15f, 60f);
            Add(list, FlockHabitat.Sky, s);

            s = Bird("black-kite", "トビ", 0.6f, 1.6f, FlockWingShape.Slotted, FlockTailShape.Forked, 0.4f);
            s.primary = C(0x6a5038); s.secondary = C(0x7a6048); s.accent = C(0x8a7050); s.detail = C(0x2a2018); s.extra = C(0x3a3a3a);
            s.beakLength = 0.07f; s.wingTipFraction = 0.2f;
            s.beatFrequency = 2.5f; s.beatAmplitude = 25f; s.glide = 0.9f; s.cruiseSpeed = 9f;
            s.defaultPattern = FlockPattern.Thermal; s.defaultCount = 8; s.defaultArea = new Vector3(80f, 30f, 80f);
            Add(list, FlockHabitat.Sky, s);

            s = Bird("sea-eagle", "オジロワシ", 0.85f, 2.2f, FlockWingShape.Slotted, FlockTailShape.Wedge, 0.3f);
            s.primary = C(0x4a3a2a); s.secondary = C(0x3a2e22); s.accent = C(0x9a8a70); s.detail = C(0x201810); s.extra = C(0xe8c040);
            s.beakLength = 0.1f; s.neckLength = 0.08f; s.wingTipFraction = 0.2f;
            s.beatFrequency = 2f; s.beatAmplitude = 22f; s.glide = 0.92f; s.cruiseSpeed = 10f;
            s.defaultPattern = FlockPattern.Thermal; s.defaultCount = 3; s.defaultArea = new Vector3(100f, 40f, 100f);
            Add(list, FlockHabitat.Sky, s);

            s = Bird("gull", "カモメ", 0.44f, 1.1f, FlockWingShape.Long, FlockTailShape.Square, 0.25f);
            s.primary = C(0x9aa4ac); s.secondary = C(0xf4f4f4); s.accent = C(0xf4f4f4); s.detail = C(0x18181a); s.extra = C(0xe0c040);
            s.beakLength = 0.09f; s.neckLength = 0.05f; s.wingTipFraction = 0.2f;
            s.beatFrequency = 3f; s.beatAmplitude = 35f; s.glide = 0.6f; s.cruiseSpeed = 11f;
            s.defaultPattern = FlockPattern.Thermal; s.defaultCount = 20; s.defaultArea = new Vector3(60f, 15f, 60f);
            Add(list, FlockHabitat.Sky, s);

            s = Bird("black-tailed-gull", "ウミネコ", 0.46f, 1.2f, FlockWingShape.Long, FlockTailShape.Square, 0.25f);
            s.primary = C(0x5a6068); s.secondary = C(0xf4f4f4); s.accent = C(0xf4f4f4); s.detail = C(0x141416); s.extra = C(0xe0c040);
            s.beakLength = 0.1f; s.neckLength = 0.05f; s.wingTipFraction = 0.22f;
            s.beatFrequency = 3f; s.beatAmplitude = 35f; s.glide = 0.55f; s.cruiseSpeed = 11f;
            s.defaultPattern = FlockPattern.Cruise; s.defaultCount = 30; s.defaultArea = new Vector3(60f, 15f, 60f);
            Add(list, FlockHabitat.Sky, s);

            s = Bird("tern", "アジサシ", 0.35f, 0.8f, FlockWingShape.Long, FlockTailShape.DeepFork, 0.35f);
            s.primary = C(0xc8ced4); s.secondary = C(0xf6f6f6); s.accent = C(0x141414); s.detail = C(0x505458); s.extra = C(0x202020);
            s.beakLength = 0.12f; s.wingTipFraction = 0.15f;
            s.beatFrequency = 4f; s.beatAmplitude = 40f; s.glide = 0.3f; s.cruiseSpeed = 9f;
            s.defaultPattern = FlockPattern.Cruise; s.defaultCount = 20; s.defaultArea = new Vector3(40f, 10f, 40f);
            Add(list, FlockHabitat.Sky, s);

            s = Bird("albatross", "アホウドリ", 0.9f, 2.2f, FlockWingShape.Long, FlockTailShape.Short, 0.15f);
            s.primary = C(0x3a3a3a); s.secondary = C(0xf4f4f0); s.accent = C(0xf0f0ec); s.detail = C(0x202020); s.extra = C(0xe8a8a0);
            s.beakLength = 0.14f; s.neckLength = 0.06f;
            s.beatFrequency = 1.5f; s.beatAmplitude = 18f; s.glide = 0.97f; s.cruiseSpeed = 14f;
            s.defaultPattern = FlockPattern.Wander; s.defaultCount = 3; s.defaultArea = new Vector3(150f, 20f, 150f);
            Add(list, FlockHabitat.Sky, s);

            s = Bird("mallard", "マガモ", 0.58f, 0.9f, FlockWingShape.Pointed, FlockTailShape.Short, 0.15f);
            s.primary = C(0x7a746a); s.secondary = C(0xb8b4ac); s.accent = C(0x1a5a3a); s.detail = C(0x4a4a48); s.extra = C(0xd8c040);
            s.beakLength = 0.1f; s.neckLength = 0.15f;
            s.beatFrequency = 10f; s.beatAmplitude = 40f; s.glide = 0.05f; s.cruiseSpeed = 20f;
            s.defaultPattern = FlockPattern.VFormation; s.defaultCount = 12; s.defaultArea = new Vector3(60f, 12f, 60f);
            Add(list, FlockHabitat.Sky, s);

            s = Bird("goose", "マガン", 0.7f, 1.5f, FlockWingShape.Pointed, FlockTailShape.Short, 0.15f);
            s.primary = C(0x6a5e50); s.secondary = C(0x8a7e70); s.accent = C(0x5a4e40); s.detail = C(0x3a3228); s.extra = C(0xe0a0a0);
            s.beakLength = 0.08f; s.neckLength = 0.3f;
            s.beatFrequency = 4.5f; s.beatAmplitude = 35f; s.glide = 0.05f; s.cruiseSpeed = 18f;
            s.defaultPattern = FlockPattern.VFormation; s.defaultCount = 25; s.defaultArea = new Vector3(100f, 20f, 100f);
            Add(list, FlockHabitat.Sky, s);

            s = Bird("swan", "オオハクチョウ", 1.5f, 2.4f, FlockWingShape.Pointed, FlockTailShape.Short, 0.12f);
            s.primary = C(0xf2f2ee); s.secondary = C(0xf0f0ec); s.accent = C(0xf2f2ee); s.detail = C(0xe8e8e4); s.extra = C(0xe8d040);
            s.beakLength = 0.07f; s.neckLength = 0.45f;
            s.beatFrequency = 3f; s.beatAmplitude = 30f; s.glide = 0.05f; s.cruiseSpeed = 17f;
            s.defaultPattern = FlockPattern.VFormation; s.defaultCount = 9; s.defaultArea = new Vector3(120f, 20f, 120f);
            Add(list, FlockHabitat.Sky, s);

            s = Bird("crane", "タンチョウ", 1.4f, 2.4f, FlockWingShape.Slotted, FlockTailShape.Short, 0.1f);
            s.primary = C(0xf2f2f0); s.secondary = C(0xf0f0ee); s.accent = C(0x181818); s.detail = C(0x181818); s.extra = C(0x2a2a2a);
            s.beakLength = 0.1f; s.neckLength = 0.45f; s.trailingLegs = true; s.flightFeatherFraction = 0.35f;
            s.beatFrequency = 2.5f; s.beatAmplitude = 30f; s.glide = 0.3f; s.cruiseSpeed = 13f;
            s.defaultPattern = FlockPattern.VFormation; s.defaultCount = 9; s.defaultArea = new Vector3(100f, 30f, 100f);
            Add(list, FlockHabitat.Sky, s);

            s = Bird("egret", "ダイサギ", 0.9f, 1.5f, FlockWingShape.Rounded, FlockTailShape.Short, 0.12f);
            s.primary = C(0xf4f4f2); s.secondary = C(0xf2f2f0); s.accent = C(0xf4f4f2); s.detail = C(0xeaeae8); s.extra = C(0xe0c030);
            s.beakLength = 0.14f; s.neckLength = 0.2f; s.trailingLegs = true;
            s.beatFrequency = 2.5f; s.beatAmplitude = 40f; s.glide = 0.1f; s.cruiseSpeed = 10f;
            s.defaultPattern = FlockPattern.Cruise; s.defaultCount = 6; s.defaultArea = new Vector3(50f, 10f, 50f);
            Add(list, FlockHabitat.Sky, s);

            s = Bird("stork", "コウノトリ", 1.1f, 2.0f, FlockWingShape.Slotted, FlockTailShape.Short, 0.12f);
            s.primary = C(0xf2f2f0); s.secondary = C(0xf0f0ee); s.accent = C(0xf2f2f0); s.detail = C(0x141414); s.extra = C(0xc03a2a);
            s.beakLength = 0.2f; s.neckLength = 0.3f; s.trailingLegs = true; s.wingTipFraction = 0.25f; s.flightFeatherFraction = 0.45f;
            s.beatFrequency = 2.5f; s.beatAmplitude = 28f; s.glide = 0.7f; s.cruiseSpeed = 11f;
            s.defaultPattern = FlockPattern.Thermal; s.defaultCount = 12; s.defaultArea = new Vector3(100f, 40f, 100f);
            Add(list, FlockHabitat.Sky, s);

            s = Bird("flamingo", "フラミンゴ", 1.3f, 1.5f, FlockWingShape.Pointed, FlockTailShape.Short, 0.1f);
            s.primary = C(0xf0a0a8); s.secondary = C(0xf4b8bc); s.accent = C(0xf0a8b0); s.detail = C(0x141414); s.extra = C(0xe88890);
            s.beakLength = 0.1f; s.neckLength = 0.45f; s.trailingLegs = true; s.wingTipFraction = 0.3f; s.flightFeatherFraction = 0.4f;
            s.beatFrequency = 3.5f; s.beatAmplitude = 32f; s.glide = 0.05f; s.cruiseSpeed = 14f;
            s.defaultPattern = FlockPattern.Stream; s.defaultCount = 40; s.defaultArea = new Vector3(100f, 15f, 100f);
            Add(list, FlockHabitat.Sky, s);

            s = Bird("pelican", "モモイロペリカン", 1.6f, 2.9f, FlockWingShape.Slotted, FlockTailShape.Short, 0.1f);
            s.primary = C(0xf4ece8); s.secondary = C(0xf2eae6); s.accent = C(0xf4ece8); s.detail = C(0x1a1a1a); s.extra = C(0xe8c060);
            s.beakLength = 0.28f; s.neckLength = 0.2f; s.wingTipFraction = 0.35f; s.flightFeatherFraction = 0.3f;
            s.beatFrequency = 2f; s.beatAmplitude = 28f; s.glide = 0.6f; s.cruiseSpeed = 12f;
            s.defaultPattern = FlockPattern.VFormation; s.defaultCount = 12; s.defaultArea = new Vector3(120f, 25f, 120f);
            Add(list, FlockHabitat.Sky, s);

            s = Bird("cormorant", "カワウ", 0.82f, 1.3f, FlockWingShape.Pointed, FlockTailShape.Wedge, 0.3f);
            s.primary = C(0x1c1e1c); s.secondary = C(0x2a2a28); s.accent = C(0x2a2a28); s.detail = C(0x141414); s.extra = C(0xd8b040);
            s.beakLength = 0.1f; s.neckLength = 0.25f;
            s.beatFrequency = 5f; s.beatAmplitude = 35f; s.glide = 0.05f; s.cruiseSpeed = 15f;
            s.defaultPattern = FlockPattern.VFormation; s.defaultCount = 20; s.defaultArea = new Vector3(100f, 20f, 100f);
            Add(list, FlockHabitat.Sky, s);

            s = Bird("parakeet", "ワカケホンセイインコ", 0.4f, 0.45f, FlockWingShape.Pointed, FlockTailShape.Long, 0.9f);
            s.primary = C(0x6ac040); s.secondary = C(0x8ad060); s.accent = C(0x5ab838); s.detail = C(0x3a7a2a); s.extra = C(0xc02020);
            s.beakLength = 0.06f;
            s.beatFrequency = 10f; s.beatAmplitude = 45f; s.glide = 0.1f; s.cruiseSpeed = 16f;
            s.defaultPattern = FlockPattern.Cruise; s.defaultCount = 30; s.defaultArea = new Vector3(50f, 12f, 50f);
            Add(list, FlockHabitat.Sky, s);

            s = Bird("bat", "コウモリ (小型)", 0.06f, 0.3f, FlockWingShape.Membrane, FlockTailShape.Short, 0.1f);
            s.primary = C(0x3a2e28); s.secondary = C(0x4a3e36); s.accent = C(0x3a2e28); s.detail = C(0x2a2220); s.extra = C(0x2a2220);
            s.beakLength = 0.02f;
            s.beatFrequency = 12f; s.beatAmplitude = 70f; s.glide = 0f; s.cruiseSpeed = 7f;
            s.defaultPattern = FlockPattern.Tornado; s.defaultCount = 150; s.defaultArea = new Vector3(30f, 20f, 30f);
            Add(list, FlockHabitat.Sky, s);

            s = Bird("flying-fox", "オオコウモリ", 0.3f, 1.2f, FlockWingShape.Membrane, FlockTailShape.Short, 0.05f);
            s.primary = C(0x2a2220); s.secondary = C(0x5a3a28); s.accent = C(0x8a5a30); s.detail = C(0x1e1816); s.extra = C(0x1e1816);
            s.beakLength = 0.05f;
            s.beatFrequency = 3f; s.beatAmplitude = 55f; s.glide = 0.2f; s.cruiseSpeed = 8f;
            s.defaultPattern = FlockPattern.Cruise; s.defaultCount = 30; s.defaultArea = new Vector3(60f, 15f, 60f);
            Add(list, FlockHabitat.Sky, s);
        }

        // ------------------------------------------------------------------
        // Fish
        // ------------------------------------------------------------------

        private static FlockSpecies Fish(
            string id, string name, float length, FlockFishBody body,
            float depth, float width, FlockCaudalFin caudal, float caudalSize)
        {
            return new FlockSpecies
            {
                id = id,
                displayName = name,
                category = FlockCategory.Fish,
                animation = body == FlockFishBody.Ray ? FlockAnimation.RayWave : FlockAnimation.Undulate,
                bodyLength = length,
                fishBody = body,
                bodyDepth = depth,
                bodyWidth = width,
                caudalFin = caudal,
                caudalSize = caudalSize,
                sizeVariance = 0.12f,
                glide = 0f,
                beatAmplitude = 0.08f,
            };
        }

        private static void AddSeaFish(List<FlockPreset> list)
        {
            FlockSpecies s;

            s = Fish("sardine", "マイワシ", 0.2f, FlockFishBody.Fusiform, 0.2f, 0.12f, FlockCaudalFin.Forked, 0.25f);
            s.primary = C(0x2a4a6a); s.secondary = C(0xe4e8ec); s.accent = C(0x1a2a3a); s.detail = C(0xa8b4bc); s.extra = C(0x6a7a88);
            s.pattern = FlockColorPattern.Spots; s.patternCount = 7; s.sheen = 0.8f;
            s.dorsalHeight = 0.07f; s.analHeight = 0.03f; s.pectoralSize = 0.08f;
            s.beatFrequency = 3.5f; s.cruiseSpeed = 1.5f;
            s.defaultPattern = FlockPattern.BaitBall; s.defaultCount = 300; s.defaultArea = new Vector3(20f, 10f, 20f);
            Add(list, FlockHabitat.Sea, s);

            s = Fish("anchovy", "カタクチイワシ", 0.13f, FlockFishBody.Fusiform, 0.15f, 0.1f, FlockCaudalFin.Forked, 0.25f);
            s.primary = C(0x3a5a6a); s.secondary = C(0xdce4e8); s.accent = C(0xc8d4dc); s.detail = C(0xb0bcc4); s.extra = C(0x7a8a94);
            s.pattern = FlockColorPattern.LateralStripe; s.patternCount = 1; s.sheen = 0.9f;
            s.dorsalHeight = 0.06f; s.analHeight = 0.03f; s.pectoralSize = 0.07f;
            s.beatFrequency = 4f; s.cruiseSpeed = 1.2f;
            s.defaultPattern = FlockPattern.Stream; s.defaultCount = 300; s.defaultArea = new Vector3(20f, 6f, 20f);
            Add(list, FlockHabitat.Sea, s);

            s = Fish("mackerel", "マサバ", 0.4f, FlockFishBody.Fusiform, 0.2f, 0.14f, FlockCaudalFin.Forked, 0.25f);
            s.primary = C(0x2a6a6a); s.secondary = C(0xe8ecec); s.accent = C(0x10202a); s.detail = C(0x8a9aa0); s.extra = C(0x4a5a60);
            s.pattern = FlockColorPattern.BackBars; s.patternCount = 9; s.sheen = 0.6f;
            s.dorsalHeight = 0.07f; s.analHeight = 0.04f; s.pectoralSize = 0.08f;
            s.beatFrequency = 3f; s.beatAmplitude = 0.07f; s.cruiseSpeed = 2.5f;
            s.defaultPattern = FlockPattern.Stream; s.defaultCount = 150; s.defaultArea = new Vector3(40f, 10f, 40f);
            Add(list, FlockHabitat.Sea, s);

            s = Fish("jack-mackerel", "マアジ", 0.3f, FlockFishBody.Fusiform, 0.25f, 0.12f, FlockCaudalFin.Forked, 0.25f);
            s.primary = C(0x5a7a70); s.secondary = C(0xe0e4de); s.accent = C(0xc8c070); s.detail = C(0xa0a898); s.extra = C(0x8a9a70);
            s.pattern = FlockColorPattern.LateralStripe; s.patternCount = 1; s.sheen = 0.7f;
            s.dorsalHeight = 0.08f; s.analHeight = 0.05f; s.pectoralSize = 0.1f;
            s.beatFrequency = 3.5f; s.cruiseSpeed = 2f;
            s.defaultPattern = FlockPattern.Stream; s.defaultCount = 150; s.defaultArea = new Vector3(30f, 8f, 30f);
            Add(list, FlockHabitat.Sea, s);

            s = Fish("bluefin-tuna", "クロマグロ", 2.0f, FlockFishBody.Torpedo, 0.26f, 0.2f, FlockCaudalFin.Lunate, 0.3f);
            s.primary = C(0x1a2a4a); s.secondary = C(0xd0d8e0); s.accent = C(0x2a3a5a); s.detail = C(0x3a4a5a); s.extra = C(0x2a3a4a);
            s.sheen = 0.5f;
            s.dorsalHeight = 0.1f; s.analHeight = 0.06f; s.pectoralSize = 0.12f;
            s.beatFrequency = 1.2f; s.beatAmplitude = 0.03f; s.cruiseSpeed = 4f;
            s.defaultPattern = FlockPattern.Stream; s.defaultCount = 20; s.defaultArea = new Vector3(80f, 20f, 80f);
            Add(list, FlockHabitat.Sea, s);

            s = Fish("skipjack", "カツオ", 0.6f, FlockFishBody.Torpedo, 0.26f, 0.2f, FlockCaudalFin.Lunate, 0.28f);
            s.primary = C(0x2a3a5a); s.secondary = C(0xe0e4ec); s.accent = C(0x3a4458); s.detail = C(0x4a5468); s.extra = C(0x3a4458);
            s.pattern = FlockColorPattern.BellyStripes; s.patternCount = 4; s.sheen = 0.6f;
            s.dorsalHeight = 0.09f; s.analHeight = 0.05f; s.pectoralSize = 0.1f;
            s.beatFrequency = 2f; s.beatAmplitude = 0.04f; s.cruiseSpeed = 4f;
            s.defaultPattern = FlockPattern.Stream; s.defaultCount = 60; s.defaultArea = new Vector3(60f, 15f, 60f);
            Add(list, FlockHabitat.Sea, s);

            s = Fish("yellowtail", "ブリ", 0.9f, FlockFishBody.Fusiform, 0.22f, 0.14f, FlockCaudalFin.Forked, 0.28f);
            s.primary = C(0x3a5a78); s.secondary = C(0xe8ecec); s.accent = C(0xd8c040); s.detail = C(0x8a9a70); s.extra = C(0xc8b040);
            s.pattern = FlockColorPattern.LateralStripe; s.patternCount = 1; s.sheen = 0.6f;
            s.dorsalHeight = 0.07f; s.analHeight = 0.05f; s.pectoralSize = 0.09f;
            s.beatFrequency = 2f; s.beatAmplitude = 0.05f; s.cruiseSpeed = 3f;
            s.defaultPattern = FlockPattern.Stream; s.defaultCount = 60; s.defaultArea = new Vector3(50f, 15f, 50f);
            Add(list, FlockHabitat.Sea, s);

            s = Fish("barracuda", "オオカマス", 1.0f, FlockFishBody.Elongated, 0.12f, 0.09f, FlockCaudalFin.Forked, 0.2f);
            s.primary = C(0x5a6a74); s.secondary = C(0xdce0e4); s.accent = C(0x3a4650); s.detail = C(0x3a4048); s.extra = C(0x2a3038);
            s.pattern = FlockColorPattern.VerticalBars; s.patternCount = 12; s.sheen = 0.6f;
            s.dorsalHeight = 0.06f; s.analHeight = 0.05f; s.pectoralSize = 0.06f;
            s.beatFrequency = 1.5f; s.beatAmplitude = 0.05f; s.cruiseSpeed = 1.2f;
            s.defaultPattern = FlockPattern.Tornado; s.defaultCount = 120; s.defaultArea = new Vector3(25f, 20f, 25f);
            Add(list, FlockHabitat.Sea, s);

            s = Fish("bigeye-trevally", "ギンガメアジ", 0.6f, FlockFishBody.Fusiform, 0.32f, 0.13f, FlockCaudalFin.Forked, 0.3f);
            s.primary = C(0x6a7a84); s.secondary = C(0xe4e8ea); s.accent = C(0x4a5a64); s.detail = C(0x4a5a64); s.extra = C(0x3a4650);
            s.sheen = 0.8f;
            s.dorsalHeight = 0.1f; s.analHeight = 0.08f; s.pectoralSize = 0.14f;
            s.beatFrequency = 2f; s.cruiseSpeed = 1.5f;
            s.defaultPattern = FlockPattern.Tornado; s.defaultCount = 250; s.defaultArea = new Vector3(30f, 25f, 30f);
            Add(list, FlockHabitat.Sea, s);

            s = Fish("salmon", "サケ", 0.7f, FlockFishBody.Fusiform, 0.22f, 0.13f, FlockCaudalFin.Truncate, 0.2f);
            s.primary = C(0x3a5a5a); s.secondary = C(0xdcdcd8); s.accent = C(0x2a3a3a); s.detail = C(0x6a7a7a); s.extra = C(0x4a5a5a);
            s.pattern = FlockColorPattern.Spots; s.patternCount = 8; s.sheen = 0.6f;
            s.dorsalHeight = 0.08f; s.analHeight = 0.05f; s.pectoralSize = 0.08f;
            s.beatFrequency = 2f; s.beatAmplitude = 0.07f; s.cruiseSpeed = 1.5f;
            s.defaultPattern = FlockPattern.Stream; s.defaultCount = 40; s.defaultArea = new Vector3(40f, 8f, 40f);
            Add(list, FlockHabitat.Sea, s);

            s = Fish("red-seabream", "マダイ", 0.5f, FlockFishBody.Fusiform, 0.38f, 0.14f, FlockCaudalFin.Forked, 0.25f);
            s.primary = C(0xd06a6a); s.secondary = C(0xf0c8c0); s.accent = C(0x6ab0d0); s.detail = C(0xd88080); s.extra = C(0xc86060);
            s.pattern = FlockColorPattern.Spots; s.patternCount = 10; s.sheen = 0.4f;
            s.dorsalHeight = 0.12f; s.analHeight = 0.06f; s.pectoralSize = 0.12f;
            s.beatFrequency = 2f; s.cruiseSpeed = 1f;
            s.defaultPattern = FlockPattern.Cruise; s.defaultCount = 30; s.defaultArea = new Vector3(30f, 8f, 30f);
            Add(list, FlockHabitat.Sea, s);

            s = Fish("manta", "オニイトマキエイ", 2.0f, FlockFishBody.Ray, 0.12f, 0.9f, FlockCaudalFin.Whip, 0.3f);
            s.primary = C(0x1e2226); s.secondary = C(0xe8e8e8); s.accent = C(0x2a2e32); s.detail = C(0x1e2226); s.extra = C(0x1e2226);
            s.pectoralSize = 1.1f; s.dorsalHeight = 0.03f; s.analHeight = 0f;
            s.beatFrequency = 0.3f; s.beatAmplitude = 0.15f; s.cruiseSpeed = 1.2f;
            s.defaultPattern = FlockPattern.FloorGlide; s.defaultCount = 3; s.defaultArea = new Vector3(40f, 10f, 40f);
            Add(list, FlockHabitat.Sea, s);

            s = Fish("eagle-ray", "マダラトビエイ", 1.2f, FlockFishBody.Ray, 0.14f, 0.8f, FlockCaudalFin.Whip, 1.5f);
            s.primary = C(0x2a3038); s.secondary = C(0xf0f0f0); s.accent = C(0xe8e8e8); s.detail = C(0x2a3038); s.extra = C(0x1e2226);
            s.pattern = FlockColorPattern.Spots; s.patternCount = 12;
            s.pectoralSize = 1.0f; s.dorsalHeight = 0.03f; s.analHeight = 0f;
            s.beatFrequency = 0.5f; s.beatAmplitude = 0.18f; s.cruiseSpeed = 1.2f;
            s.defaultPattern = FlockPattern.Stream; s.defaultCount = 8; s.defaultArea = new Vector3(40f, 10f, 40f);
            Add(list, FlockHabitat.Sea, s);

            s = Fish("reef-shark", "ツマグロ", 1.6f, FlockFishBody.Torpedo, 0.18f, 0.16f, FlockCaudalFin.Heterocercal, 0.3f);
            s.primary = C(0x7a848c); s.secondary = C(0xe8e8e4); s.accent = C(0x5a646c); s.detail = C(0x3a4046); s.extra = C(0x4a5258);
            s.sheen = 0.1f;
            s.dorsalHeight = 0.12f; s.analHeight = 0.04f; s.pectoralSize = 0.2f;
            s.beatFrequency = 0.8f; s.beatAmplitude = 0.08f; s.cruiseSpeed = 1.2f;
            s.defaultPattern = FlockPattern.Wander; s.defaultCount = 5; s.defaultArea = new Vector3(40f, 10f, 40f);
            Add(list, FlockHabitat.Sea, s);
        }

        private static void AddReefFish(List<FlockPreset> list)
        {
            FlockSpecies s;

            s = Fish("anthias", "キンギョハナダイ", 0.1f, FlockFishBody.Fusiform, 0.32f, 0.12f, FlockCaudalFin.Lunate, 0.3f);
            s.primary = C(0xf08040); s.secondary = C(0xf4a060); s.accent = C(0xc060c0); s.detail = C(0xf0a040); s.extra = C(0xf07030);
            s.pattern = FlockColorPattern.LateralStripe; s.patternCount = 1; s.sheen = 0.2f;
            s.dorsalHeight = 0.1f; s.analHeight = 0.08f; s.pectoralSize = 0.12f;
            s.beatFrequency = 5f; s.cruiseSpeed = 0.4f;
            s.defaultPattern = FlockPattern.Cruise; s.defaultCount = 150; s.defaultArea = new Vector3(6f, 3f, 6f);
            Add(list, FlockHabitat.Reef, s);

            s = Fish("blue-damselfish", "ソラスズメダイ", 0.07f, FlockFishBody.Fusiform, 0.38f, 0.14f, FlockCaudalFin.Forked, 0.25f);
            s.primary = C(0x2a6ae0); s.secondary = C(0x4a8af0); s.accent = C(0x2a6ae0); s.detail = C(0x3a7ae8); s.extra = C(0xe8d040);
            s.sheen = 0.3f;
            s.dorsalHeight = 0.1f; s.analHeight = 0.08f; s.pectoralSize = 0.12f;
            s.beatFrequency = 5f; s.cruiseSpeed = 0.35f;
            s.defaultPattern = FlockPattern.Cruise; s.defaultCount = 100; s.defaultArea = new Vector3(5f, 2.5f, 5f);
            Add(list, FlockHabitat.Reef, s);

            s = Fish("fusilier", "タカサゴ", 0.3f, FlockFishBody.Fusiform, 0.25f, 0.12f, FlockCaudalFin.Forked, 0.3f);
            s.primary = C(0x3a6ab0); s.secondary = C(0xd8e4f0); s.accent = C(0xe8d040); s.detail = C(0x6a8ab0); s.extra = C(0xd04a4a);
            s.pattern = FlockColorPattern.LateralStripe; s.patternCount = 1; s.sheen = 0.4f;
            s.dorsalHeight = 0.07f; s.analHeight = 0.05f; s.pectoralSize = 0.09f;
            s.beatFrequency = 3.5f; s.cruiseSpeed = 1.2f;
            s.defaultPattern = FlockPattern.Stream; s.defaultCount = 200; s.defaultArea = new Vector3(25f, 8f, 25f);
            Add(list, FlockHabitat.Reef, s);

            s = Fish("butterflyfish", "チョウチョウウオ", 0.15f, FlockFishBody.Disc, 0.75f, 0.1f, FlockCaudalFin.Truncate, 0.2f);
            s.primary = C(0xf0d030); s.secondary = C(0xf4e060); s.accent = C(0x1a1a1a); s.detail = C(0xf0c020); s.extra = C(0xf0e080);
            s.pattern = FlockColorPattern.EyeBar; s.patternCount = 1;
            s.dorsalHeight = 0.2f; s.analHeight = 0.15f; s.pectoralSize = 0.1f;
            s.beatFrequency = 3f; s.beatAmplitude = 0.05f; s.cruiseSpeed = 0.3f;
            s.defaultPattern = FlockPattern.Wander; s.defaultCount = 10; s.defaultArea = new Vector3(6f, 3f, 6f);
            Add(list, FlockHabitat.Reef, s);

            s = Fish("palette-tang", "ナンヨウハギ", 0.2f, FlockFishBody.Disc, 0.55f, 0.12f, FlockCaudalFin.Truncate, 0.22f);
            s.primary = C(0x2a4ad0); s.secondary = C(0x3a60e0); s.accent = C(0x101828); s.detail = C(0x1a2a80); s.extra = C(0xf0d020);
            s.pattern = FlockColorPattern.Patches; s.patternCount = 2;
            s.dorsalHeight = 0.12f; s.analHeight = 0.1f; s.pectoralSize = 0.1f;
            s.beatFrequency = 3f; s.beatAmplitude = 0.06f; s.cruiseSpeed = 0.4f;
            s.defaultPattern = FlockPattern.Cruise; s.defaultCount = 20; s.defaultArea = new Vector3(8f, 3f, 8f);
            Add(list, FlockHabitat.Reef, s);

            s = Fish("moorish-idol", "ツノダシ", 0.18f, FlockFishBody.Disc, 0.7f, 0.08f, FlockCaudalFin.Truncate, 0.18f);
            s.primary = C(0xf4f2ea); s.secondary = C(0xf4f0e0); s.accent = C(0x141414); s.detail = C(0xf0d040); s.extra = C(0x1a1a1a);
            s.pattern = FlockColorPattern.VerticalBars; s.patternCount = 2;
            s.dorsalHeight = 1.0f; s.analHeight = 0.25f; s.pectoralSize = 0.1f;
            s.beatFrequency = 2.5f; s.beatAmplitude = 0.05f; s.cruiseSpeed = 0.3f;
            s.defaultPattern = FlockPattern.Wander; s.defaultCount = 6; s.defaultArea = new Vector3(8f, 3f, 8f);
            Add(list, FlockHabitat.Reef, s);

            s = Fish("clownfish", "カクレクマノミ", 0.08f, FlockFishBody.Fusiform, 0.38f, 0.15f, FlockCaudalFin.Rounded, 0.22f);
            s.primary = C(0xf06a1a); s.secondary = C(0xf07a2a); s.accent = C(0xf8f8f4); s.detail = C(0xf06a1a); s.extra = C(0xf06a1a);
            s.pattern = FlockColorPattern.VerticalBars; s.patternCount = 3;
            s.dorsalHeight = 0.12f; s.analHeight = 0.08f; s.pectoralSize = 0.12f;
            s.beatFrequency = 4f; s.cruiseSpeed = 0.2f;
            s.defaultPattern = FlockPattern.Wander; s.defaultCount = 6; s.defaultArea = new Vector3(1.5f, 0.8f, 1.5f);
            Add(list, FlockHabitat.Reef, s);
        }

        private static void AddAquariumFish(List<FlockPreset> list)
        {
            FlockSpecies s;

            s = Fish("medaka-orange", "ヒメダカ", 0.035f, FlockFishBody.Fusiform, 0.2f, 0.14f, FlockCaudalFin.Truncate, 0.2f);
            s.primary = C(0xe0a050); s.secondary = C(0xf0d0a0); s.accent = C(0xe0a050); s.detail = C(0xe8b870); s.extra = C(0xe8b870);
            s.sheen = 0.2f;
            s.dorsalHeight = 0.04f; s.analHeight = 0.08f; s.pectoralSize = 0.1f;
            s.beatFrequency = 5f; s.beatAmplitude = 0.07f; s.cruiseSpeed = 0.06f;
            s.defaultPattern = FlockPattern.Cruise; s.defaultCount = 20; s.defaultArea = new Vector3(0.5f, 0.12f, 0.25f);
            Add(list, FlockHabitat.Aquarium, s);

            s = Fish("medaka-wild", "クロメダカ", 0.035f, FlockFishBody.Fusiform, 0.2f, 0.14f, FlockCaudalFin.Truncate, 0.2f);
            s.primary = C(0x6a6a58); s.secondary = C(0xc8c4b0); s.accent = C(0x4a4a40); s.detail = C(0x8a8a78); s.extra = C(0x8a8a78);
            s.pattern = FlockColorPattern.LateralStripe; s.patternCount = 1; s.sheen = 0.3f;
            s.dorsalHeight = 0.04f; s.analHeight = 0.08f; s.pectoralSize = 0.1f;
            s.beatFrequency = 5f; s.beatAmplitude = 0.07f; s.cruiseSpeed = 0.06f;
            s.defaultPattern = FlockPattern.Cruise; s.defaultCount = 20; s.defaultArea = new Vector3(0.5f, 0.12f, 0.25f);
            Add(list, FlockHabitat.Aquarium, s);

            s = Fish("goldfish", "ワキン", 0.1f, FlockFishBody.Fusiform, 0.38f, 0.2f, FlockCaudalFin.Forked, 0.35f);
            s.primary = C(0xf04a1a); s.secondary = C(0xf08040); s.accent = C(0xf04a1a); s.detail = C(0xf06030); s.extra = C(0xf06030);
            s.sheen = 0.3f;
            s.dorsalHeight = 0.12f; s.analHeight = 0.06f; s.pectoralSize = 0.12f;
            s.beatFrequency = 3f; s.cruiseSpeed = 0.08f;
            s.defaultPattern = FlockPattern.Wander; s.defaultCount = 8; s.defaultArea = new Vector3(0.5f, 0.2f, 0.25f);
            Add(list, FlockHabitat.Aquarium, s);

            s = Fish("ryukin", "リュウキン", 0.1f, FlockFishBody.Disc, 0.6f, 0.3f, FlockCaudalFin.Veil, 0.8f);
            s.primary = C(0xe83a1a); s.secondary = C(0xf06a3a); s.accent = C(0xf4f0ea); s.detail = C(0xf05a2a); s.extra = C(0xf05a2a);
            s.pattern = FlockColorPattern.Patches; s.patternCount = 3; s.sheen = 0.3f;
            s.dorsalHeight = 0.25f; s.analHeight = 0.1f; s.pectoralSize = 0.12f;
            s.beatFrequency = 2f; s.beatAmplitude = 0.06f; s.cruiseSpeed = 0.05f;
            s.defaultPattern = FlockPattern.Wander; s.defaultCount = 6; s.defaultArea = new Vector3(0.5f, 0.2f, 0.25f);
            Add(list, FlockHabitat.Aquarium, s);

            s = Fish("koi", "錦鯉 (紅白)", 0.6f, FlockFishBody.Fusiform, 0.25f, 0.18f, FlockCaudalFin.Truncate, 0.25f);
            s.primary = C(0xf4f2ee); s.secondary = C(0xf4f2ee); s.accent = C(0xd83a1a); s.detail = C(0xf0eee8); s.extra = C(0xf0eee8);
            s.pattern = FlockColorPattern.Patches; s.patternCount = 4; s.sheen = 0.2f;
            s.dorsalHeight = 0.08f; s.analHeight = 0.04f; s.pectoralSize = 0.15f;
            s.beatFrequency = 1.5f; s.beatAmplitude = 0.06f; s.cruiseSpeed = 0.25f;
            s.defaultPattern = FlockPattern.Wander; s.defaultCount = 10; s.defaultArea = new Vector3(4f, 0.7f, 3f);
            Add(list, FlockHabitat.Aquarium, s);

            s = Fish("neon-tetra", "ネオンテトラ", 0.03f, FlockFishBody.Fusiform, 0.22f, 0.1f, FlockCaudalFin.Forked, 0.25f);
            s.primary = C(0x3a3a40); s.secondary = C(0xd83a3a); s.accent = C(0x30b0ff); s.detail = C(0xd0d0d8); s.extra = C(0xd0d0d8);
            s.pattern = FlockColorPattern.LateralStripe; s.patternCount = 1; s.sheen = 0.3f;
            s.dorsalHeight = 0.08f; s.analHeight = 0.08f; s.pectoralSize = 0.08f;
            s.beatFrequency = 5f; s.cruiseSpeed = 0.08f;
            s.defaultPattern = FlockPattern.Cruise; s.defaultCount = 40; s.defaultArea = new Vector3(0.5f, 0.2f, 0.2f);
            Add(list, FlockHabitat.Aquarium, s);

            s = Fish("zebra-danio", "ゼブラダニオ", 0.04f, FlockFishBody.Elongated, 0.18f, 0.1f, FlockCaudalFin.Forked, 0.25f);
            s.primary = C(0xd8d0b0); s.secondary = C(0xe8e4d0); s.accent = C(0x2a3a8a); s.detail = C(0xd8d0b0); s.extra = C(0xd8d0b0);
            s.pattern = FlockColorPattern.HorizontalStripes; s.patternCount = 4; s.sheen = 0.2f;
            s.dorsalHeight = 0.06f; s.analHeight = 0.06f; s.pectoralSize = 0.08f;
            s.beatFrequency = 6f; s.cruiseSpeed = 0.1f;
            s.defaultPattern = FlockPattern.Cruise; s.defaultCount = 25; s.defaultArea = new Vector3(0.5f, 0.2f, 0.2f);
            Add(list, FlockHabitat.Aquarium, s);

            s = Fish("guppy", "グッピー", 0.03f, FlockFishBody.Fusiform, 0.22f, 0.12f, FlockCaudalFin.Veil, 0.6f);
            s.primary = C(0x9aa0a0); s.secondary = C(0xd0d0c8); s.accent = C(0x2a2a2a); s.detail = C(0x3a6ad0); s.extra = C(0xf06a2a);
            s.pattern = FlockColorPattern.Spots; s.patternCount = 3;
            s.dorsalHeight = 0.1f; s.analHeight = 0.05f; s.pectoralSize = 0.08f;
            s.beatFrequency = 4f; s.cruiseSpeed = 0.06f;
            s.defaultPattern = FlockPattern.Wander; s.defaultCount = 20; s.defaultArea = new Vector3(0.5f, 0.2f, 0.2f);
            Add(list, FlockHabitat.Aquarium, s);

            s = Fish("angelfish", "エンゼルフィッシュ", 0.08f, FlockFishBody.Disc, 0.75f, 0.08f, FlockCaudalFin.Truncate, 0.3f);
            s.primary = C(0xd8d4c8); s.secondary = C(0xe8e4d8); s.accent = C(0x1a1a1a); s.detail = C(0xc8c4b8); s.extra = C(0xc8c4b8);
            s.pattern = FlockColorPattern.VerticalBars; s.patternCount = 3; s.sheen = 0.3f;
            s.dorsalHeight = 0.7f; s.analHeight = 0.7f; s.pectoralSize = 0.08f;
            s.beatFrequency = 2f; s.beatAmplitude = 0.04f; s.cruiseSpeed = 0.06f;
            s.defaultPattern = FlockPattern.Wander; s.defaultCount = 6; s.defaultArea = new Vector3(0.5f, 0.25f, 0.2f);
            Add(list, FlockHabitat.Aquarium, s);

            s = Fish("discus", "ディスカス", 0.15f, FlockFishBody.Disc, 0.85f, 0.12f, FlockCaudalFin.Rounded, 0.15f);
            s.primary = C(0xb04a2a); s.secondary = C(0xc86a3a); s.accent = C(0x3a8ab0); s.detail = C(0xb05a3a); s.extra = C(0xb05a3a);
            s.pattern = FlockColorPattern.HorizontalStripes; s.patternCount = 6;
            s.dorsalHeight = 0.1f; s.analHeight = 0.1f; s.pectoralSize = 0.08f;
            s.beatFrequency = 1.5f; s.beatAmplitude = 0.04f; s.cruiseSpeed = 0.06f;
            s.defaultPattern = FlockPattern.Wander; s.defaultCount = 6; s.defaultArea = new Vector3(0.6f, 0.3f, 0.3f);
            Add(list, FlockHabitat.Aquarium, s);
        }
    }
}
