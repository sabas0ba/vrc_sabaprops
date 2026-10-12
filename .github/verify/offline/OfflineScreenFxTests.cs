// Contract checks on the screen FX package, executed without Unity.
//
// The package is two shaders, one shared include and a table of presets, and
// nothing ties them together at compile time: a preset naming a property the
// shader does not have, or a property with no uniform behind it, fails
// silently in Unity. These checks read the sources and compare the three.
//
// What this can assert: that the property blocks, the uniforms, the presets,
// the menu and the preset reference agree with each other.
//
// What it cannot: that the shaders compile or what they draw. Those stay with
// the Unity EditMode tests in CIProject/Assets/ScreenFxTests.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using SabaProps.ScreenFx.Editors;

internal static class OfflineScreenFxTests
{
    private sealed class Property
    {
        public string declaration;
        public string type;
        public float minimum;
        public float maximum;
        public float[] defaults;
    }

    private static int _failures;
    private static string _current = "-";
    private static string _package;

    private static Dictionary<string, Property> _standard;
    private static Dictionary<string, Property> _lite;

    // Declared in the property block for the keyword toggle only.
    private const string DepthToggle = "_UseDepth";

    // Global set by the VRChat client, not a material property.
    private const string MirrorGlobal = "_VRChatMirrorMode";

    private static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("run with the path of the screen FX package");
            return 1;
        }

        _package = args[0];
        _standard = ReadProperties("Runtime/Shaders/SabaScreenFx.shader");
        _lite = ReadProperties("Runtime/Shaders/SabaScreenFxLite.shader");

        Run("every property has a uniform and every uniform a property", PropertiesMatchUniforms);
        Run("the Lite shader declares a subset with identical declarations", LiteIsASubset);
        Run("every property defaults to no effect inside its range", DefaultsAreInRange);
        Run("preset ids are unique and described", PresetsAreNamed);
        Run("presets only set properties the shader has, inside their ranges", PresetValuesAreValid);
        Run("every preset changes something", PresetsDifferFromDefaults);
        Run("every preset has a menu entry for both shaders", MenuCoversThePresets);
        Run("the preset reference lists exactly the presets", ReferenceListsThePresets);

        Console.WriteLine(_failures == 0 ? "screen FX checks passed" : $"{_failures} screen FX check(s) failed");
        return _failures == 0 ? 0 : 1;
    }

    private static string Read(string relativePath) => File.ReadAllText(Path.Combine(_package, relativePath));

    private static float Number(string text) => float.Parse(text, CultureInfo.InvariantCulture);

    private static Dictionary<string, Property> ReadProperties(string relativePath)
    {
        var pattern = new Regex(
            @"^\s*(?:\[[^\]]*\]\s*)*(_\w+)\s*\(""[^""]*"",\s*(Range\((-?[\d.]+),\s*(-?[\d.]+)\)|Color|Vector|Float)\)\s*=\s*\(?([^)]*)\)?\s*$");
        var properties = new Dictionary<string, Property>();
        foreach (string line in Read(relativePath).Split('\n'))
        {
            Match match = pattern.Match(line.TrimEnd('\r'));
            if (!match.Success)
            {
                continue;
            }

            var property = new Property { declaration = line.Trim(), type = match.Groups[2].Value };
            if (property.type.StartsWith("Range"))
            {
                property.type = "Range";
                property.minimum = Number(match.Groups[3].Value);
                property.maximum = Number(match.Groups[4].Value);
            }

            string[] parts = match.Groups[5].Value.Split(',');
            property.defaults = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                property.defaults[i] = Number(parts[i].Trim());
            }

            properties.Add(match.Groups[1].Value, property);
        }

        return properties;
    }

    private static void PropertiesMatchUniforms()
    {
        var uniforms = new HashSet<string>();
        foreach (Match match in Regex.Matches(
            Read("Runtime/Shaders/SabaScreenFxCore.cginc"), @"^(?:float|float4|fixed4)\s+(_\w+);", RegexOptions.Multiline))
        {
            uniforms.Add(match.Groups[1].Value);
        }

        Require(_standard.Count > 40, $"only {_standard.Count} properties were parsed from the shader");
        foreach (string name in _standard.Keys)
        {
            Require(name == DepthToggle || uniforms.Contains(name), $"{name} is a property without a uniform");
        }

        foreach (string name in uniforms)
        {
            Require(name == MirrorGlobal || _standard.ContainsKey(name), $"{name} is a uniform without a property");
        }
    }

    private static void LiteIsASubset()
    {
        Require(_lite.Count > 20, $"only {_lite.Count} properties were parsed from the Lite shader");
        foreach (KeyValuePair<string, Property> pair in _lite)
        {
            if (!_standard.TryGetValue(pair.Key, out Property standard))
            {
                Fail($"{pair.Key} exists only in the Lite shader");
            }
            else
            {
                Require(standard.declaration == pair.Value.declaration, $"{pair.Key} is declared differently in the two shaders");
            }
        }
    }

    // The amount that switches each block on. A material created from the
    // shader alone must draw nothing.
    private static readonly string[] Amounts =
    {
        "_Wobble", "_Haze", "_DoubleVision", "_Blur", "_RadialBlur", "_Chromatic", "_LensDrops", "_Frost",
        "_Splat", "_Exposure", "_FogVeil", "_FogDensity", "_Glare", "_Vignette", "_Pulse", "_Particle",
        "_SpeedLines", "_Caustics", "_Grain", "_Blink", "_BlinkAuto",
    };

    private static void DefaultsAreInRange()
    {
        foreach (KeyValuePair<string, Property> pair in _standard)
        {
            Property property = pair.Value;
            if (property.type == "Range")
            {
                Require(
                    property.defaults[0] >= property.minimum && property.defaults[0] <= property.maximum,
                    $"{pair.Key} defaults outside its range");
            }
        }

        foreach (string amount in Amounts)
        {
            Require(_standard.ContainsKey(amount) && _standard[amount].defaults[0] == 0f, $"{amount} does not default to 0");
        }

        Require(_standard["_Saturation"].defaults[0] == 1f, "_Saturation does not default to 1");
        Require(_standard["_Contrast"].defaults[0] == 1f, "_Contrast does not default to 1");
        Require(_standard["_Tint"].defaults[3] == 0f, "_Tint does not default to zero amount");
    }

    private static void PresetsAreNamed()
    {
        var ids = new HashSet<string>();
        Require(ScreenFxPresets.All.Length >= 13, "fewer presets than the conditions the package documents");
        foreach (ScreenFxPreset preset in ScreenFxPresets.All)
        {
            Require(ids.Add(preset.id), $"duplicate preset id {preset.id}");
            Require(Regex.IsMatch(preset.id, "^[A-Z][A-Za-z]+$"), $"{preset.id} cannot be used as a method and asset name");
            Require(!string.IsNullOrEmpty(preset.displayName), $"{preset.id} has no display name");
            Require(!string.IsNullOrEmpty(preset.description), $"{preset.id} has no description");
            Require(ScreenFxPresets.Find(preset.id) == preset, $"Find does not return {preset.id}");
        }
    }

    private static void PresetValuesAreValid()
    {
        foreach (ScreenFxPreset preset in ScreenFxPresets.All)
        {
            var seen = new HashSet<string>();
            foreach (ScreenFxValue value in preset.values)
            {
                string where = $"{preset.id}.{value.property}";
                Require(seen.Add(value.property), $"{where} is set twice");
                if (!_standard.TryGetValue(value.property, out Property property))
                {
                    Fail($"{where} is not a shader property");
                    continue;
                }

                switch (value.kind)
                {
                    case ScreenFxValueKind.Float:
                        Require(property.type == "Range" || property.type == "Float", $"{where} is not a float property");
                        if (property.type == "Range")
                        {
                            Require(
                                value.value.x >= property.minimum && value.value.x <= property.maximum,
                                $"{where} = {value.value.x} is outside [{property.minimum}, {property.maximum}]");
                        }

                        break;
                    case ScreenFxValueKind.Color:
                        Require(property.type == "Color", $"{where} is not a colour property");
                        foreach (float component in new[] { value.value.x, value.value.y, value.value.z, value.value.w })
                        {
                            Require(component >= 0f && component <= 1f, $"{where} has a component outside [0, 1]");
                        }

                        break;
                    default:
                        Require(property.type == "Vector", $"{where} is not a vector property");
                        break;
                }
            }
        }
    }

    private static void PresetsDifferFromDefaults()
    {
        foreach (ScreenFxPreset preset in ScreenFxPresets.All)
        {
            bool active = false;
            bool activeInLite = false;
            foreach (ScreenFxValue value in preset.values)
            {
                bool isAmount = Array.IndexOf(Amounts, value.property) >= 0 && value.value.x != 0f;
                isAmount |= value.property == "_Tint" && value.value.w > 0f;
                isAmount |= (value.property == "_Saturation" || value.property == "_Contrast") && value.value.x != 1f;
                active |= isAmount;
                activeInLite |= isAmount && _lite.ContainsKey(value.property);
            }

            Require(active, $"{preset.id} switches no block on");
            Require(activeInLite, $"{preset.id} draws nothing with the Lite shader");
        }
    }

    private static void MenuCoversThePresets()
    {
        string menu = Read("Editor/ScreenFxMenu.cs");
        foreach (ScreenFxPreset preset in ScreenFxPresets.All)
        {
            foreach (string lite in new[] { "false", "true" })
            {
                Require(
                    menu.Contains($"Create(\"{preset.id}\", {lite}, command)"),
                    $"no menu entry creates {preset.id} with lite = {lite}");
            }
        }

        int entries = Regex.Matches(menu, @"=> Create\(""").Count;
        Require(entries == ScreenFxPresets.All.Length * 2, $"{entries} menu entries for {ScreenFxPresets.All.Length} presets");
    }

    private static void ReferenceListsThePresets()
    {
        var listed = new List<string>();
        foreach (Match match in Regex.Matches(
            Read("Documentation~/elements.md"), @"^\| ([A-Z][A-Za-z]+) \|", RegexOptions.Multiline))
        {
            listed.Add(match.Groups[1].Value);
        }

        foreach (ScreenFxPreset preset in ScreenFxPresets.All)
        {
            int count = listed.FindAll(name => name == preset.displayName).Count;
            Require(count == 1, $"{preset.displayName} is listed {count} times in elements.md");
        }

        foreach (string name in listed)
        {
            Require(ScreenFxPresets.Find(name) != null, $"elements.md lists {name}, which is not a preset");
        }
    }

    private static void Run(string name, Action check)
    {
        _current = name;
        int before = _failures;
        try
        {
            check();
        }
        catch (Exception ex)
        {
            Fail($"threw {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }

        Console.WriteLine(_failures == before ? $"ok: {name}" : $"FAILED: {name}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            Fail(message);
        }
    }

    private static void Fail(string message)
    {
        _failures++;
        Console.Error.WriteLine($"  [{_current}] {message}");
    }
}
