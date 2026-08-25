// Runs the package's own mesh generators and dumps the geometry the
// documentation figures are drawn from.
//
// The figures have to show what the package actually produces, so nothing is
// modelled here: every tile is the output of FoliageMeshBuilder for a species
// whose parameters differ in exactly one place. Rendering is deliberately not
// done here -- this half only needs the generators and the offline shim, the
// other half (render_figures.py) needs no C# at all.
//
// Output is JSON on stdout. See render.sh for how the two halves are joined.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SabaProps.Foliage;
using SabaProps.Foliage.Editors;
using UnityEngine;

internal static class DumpFigures
{
    /// <summary>Which vertex channel a tile is coloured by.</summary>
    private enum Channel
    {
        /// <summary>The mesh's own vertex colours, lit.</summary>
        Albedo,

        /// <summary>UV0.y, the height ratio the shader bends by.</summary>
        HeightRatio,

        /// <summary>UV3.w, how soft the vertex is to wind.</summary>
        Stiffness,
    }

    private sealed class Tile
    {
        public string Label;
        public Mesh Mesh;
        public Channel Channel = Channel.Albedo;

        /// <summary>
        /// The shader's distance shrink, applied here so the figure shows the
        /// same collapse the GPU performs: lerp(root, vertex, shrink).
        /// </summary>
        public float Shrink = 1f;
    }

    private sealed class Figure
    {
        public string Id;
        public string Title;
        public string Caption;
        public List<Tile> Tiles = new List<Tile>();
    }

    private static int Main()
    {
        var figures = new List<Figure>
        {
            SpeciesOverview(),

            GrassFigure("grass-blade-count", "Blade Count", "bladeCount",
                "枚数はそのまま三角形数に効きます。密度で埋めるか、1 株を厚くするかの選択です。",
                new[] { 2, 6, 12, 24 }, (p, v) => p.bladeCount = v),
            GrassFigure("grass-height", "Height", "height",
                "高さ (m)。heightVariance が同じなら、株内のばらつきの比率は保たれます。",
                new[] { 0.25f, 0.6f, 1f, 1.4f }, (p, v) => p.height = v),
            GrassFigure("grass-bend", "Bend", "bend",
                "先端が根元からどれだけ倒れるか。0 で直立、1 を超えると寝そべります。",
                new[] { 0f, 0.45f, 0.9f, 1.4f }, (p, v) => p.bend = v),
            GrassFigure("grass-clump-radius", "Clump Radius", "clumpRadius",
                "根元が散る円の半径 (m)。0 で 1 点から生え、大きいほど株が広がります。",
                new[] { 0f, 0.04f, 0.08f, 0.16f }, (p, v) => p.clumpRadius = v),

            SunflowerFigure("sunflower-head-tilt", "Head Tilt", "headTilt",
                "花の向き (度)。0 で真上、大きくすると正面を向きます。",
                new[] { 0f, 20f, 38f, 70f }, (p, v) => p.headTilt = v),
            SunflowerFigure("sunflower-petal-count", "Petal Count", "petalCount",
                "花弁の枚数。枚数だけが変わり、花芯の大きさは headRadius が決めます。",
                new[] { 6, 10, 15, 24 }, (p, v) => p.petalCount = v),
            SunflowerFigure("sunflower-lean", "Lean", "lean",
                "茎の傾き (m)。頂点が根元からどれだけ横へずれるか。",
                new[] { 0f, 0.18f, 0.45f, 0.8f }, (p, v) => p.lean = v),

            CloverFigure("clover-leaflet-count", "Leaflet Count", "leafletCount",
                "1 株あたりの小葉の枚数。4 枚にすれば四つ葉になります。",
                new[] { 2, 3, 4, 5 }, (p, v) => p.leafletCount = v),
            CloverFigure("clover-notch", "Notch", "notch",
                "先端の切れ込みの深さ。0 で丸葉、大きいほどハート形になります。",
                new[] { 0f, 0.12f, 0.22f, 0.45f }, (p, v) => p.notch = v),

            ReedFigure("reed-spread", "Spread", "spread",
                "先端の開き (m)。小さいほど直立した葦らしい縦のシルエットになります。",
                new[] { 0f, 0.16f, 0.4f, 0.8f }, (p, v) => p.spread = v),
            ReedSpike(),

            MeshSeed(),
            DistanceShrink(),
            MeshChannels(),
        };

        Console.Out.Write(Serialise(figures));
        return 0;
    }

    // ----------------------------------------------------------------------
    // Figures
    // ----------------------------------------------------------------------

    private static Figure SpeciesOverview()
    {
        var figure = new Figure
        {
            Id = "species-overview",
            Title = "収録している 6 種",
            Caption = "すべて既定パラメータ、同じ縮尺です。ひまわりと葦が草の 2 倍前後の背丈になります。",
        };

        AddTile(figure, "Grass Clump", FoliageSpeciesKind.GrassClump);
        AddTile(figure, "Clover", FoliageSpeciesKind.Clover);
        AddTile(figure, "Sunflower", FoliageSpeciesKind.Sunflower);
        AddTile(figure, "Reed", FoliageSpeciesKind.Reed);
        AddTile(figure, "Small Flower", FoliageSpeciesKind.SmallFlower);
        AddTile(figure, "Weed", FoliageSpeciesKind.Weed);

        return figure;
    }

    private static Figure ReedSpike()
    {
        var figure = new Figure
        {
            Id = "reed-spike",
            Title = "Reed: Spike",
            Caption = "最も高いブレードの先に付く穂。長さは spikeLength (m) です。",
        };

        figure.Tiles.Add(Reed("spike = off", p => p.spike = false));
        figure.Tiles.Add(Reed("spike = on", p => p.spike = true));
        figure.Tiles.Add(Reed("spikeLength = 0.28", p =>
        {
            p.spike = true;
            p.spikeLength = 0.28f;
        }));

        return figure;
    }

    private static Figure MeshSeed()
    {
        var figure = new Figure
        {
            Id = "mesh-seed",
            Title = "Mesh Seed",
            Caption =
                "同じパラメータでも meshSeed が違えば別の形になります。"
                + "逆に同じ値なら、いつどの PC で作り直しても同じメッシュです。",
        };

        foreach (int seed in new[] { 1, 2, 3, 4 })
        {
            figure.Tiles.Add(new Tile
            {
                Label = "meshSeed = " + seed.ToString(CultureInfo.InvariantCulture),
                Mesh = FoliageMeshBuilder.Build(
                    new FoliageSpecies { kind = FoliageSpeciesKind.GrassClump, meshSeed = seed }),
            });
        }

        return figure;
    }

    private static Figure DistanceShrink()
    {
        var figure = new Figure
        {
            Id = "distance-shrink",
            Title = "Distance Shrink",
            Caption =
                "シェーダーが遠景で行う縮退です。要素ごとに UV3.xyz の根元へ畳まれるので、"
                + "株が丸ごと小さくなるのではなく、疎になりながら消えていきます。",
        };

        Mesh mesh = FoliageMeshBuilder.Build(
            new FoliageSpecies { kind = FoliageSpeciesKind.GrassClump, meshSeed = 1 });

        foreach (float shrink in new[] { 1f, 0.66f, 0.33f, 0f })
        {
            figure.Tiles.Add(new Tile
            {
                Label = "shrink = " + shrink.ToString("0.##", CultureInfo.InvariantCulture),
                Mesh = mesh,
                Shrink = shrink,
            });
        }

        return figure;
    }

    private static Figure MeshChannels()
    {
        var figure = new Figure
        {
            Id = "mesh-channels",
            Title = "メッシュのチャンネル規約",
            Caption =
                "左は UV0.y（根元 0 / 先端 1）、右は UV3.w（風に対する柔らかさ）です。"
                + "風の揺れ量は UV0.y を Bend Falloff 乗した値と UV3.w の積なので、"
                + "茎は動かず花弁だけがそよぎます。",
        };

        Mesh mesh = FoliageMeshBuilder.Build(
            new FoliageSpecies { kind = FoliageSpeciesKind.Sunflower, meshSeed = 3 });

        figure.Tiles.Add(new Tile { Label = "UV0.y", Mesh = mesh, Channel = Channel.HeightRatio });
        figure.Tiles.Add(new Tile { Label = "UV3.w", Mesh = mesh, Channel = Channel.Stiffness });

        return figure;
    }

    // ----------------------------------------------------------------------
    // Per-species figure builders
    // ----------------------------------------------------------------------

    private static Figure GrassFigure<T>(
        string id, string title, string parameter, string caption, T[] values, Action<GrassParams, T> apply)
    {
        return Sweep(id, "Grass Clump: " + title, parameter, caption, values,
            (label, value) => Grass(label, p => apply(p, value)));
    }

    private static Figure SunflowerFigure<T>(
        string id, string title, string parameter, string caption, T[] values, Action<SunflowerParams, T> apply)
    {
        return Sweep(id, "Sunflower: " + title, parameter, caption, values,
            (label, value) => Sunflower(label, p => apply(p, value)));
    }

    private static Figure CloverFigure<T>(
        string id, string title, string parameter, string caption, T[] values, Action<CloverParams, T> apply)
    {
        return Sweep(id, "Clover: " + title, parameter, caption, values,
            (label, value) => Clover(label, p => apply(p, value)));
    }

    private static Figure ReedFigure<T>(
        string id, string title, string parameter, string caption, T[] values, Action<ReedParams, T> apply)
    {
        return Sweep(id, "Reed: " + title, parameter, caption, values,
            (label, value) => Reed(label, p => apply(p, value)));
    }

    private static Figure Sweep<T>(
        string id, string title, string parameter, string caption, T[] values, Func<string, T, Tile> build)
    {
        var figure = new Figure { Id = id, Title = title, Caption = caption };

        foreach (T value in values)
        {
            string label = parameter + " = " + Format(value);
            figure.Tiles.Add(build(label, value));
        }

        return figure;
    }

    private static Tile Grass(string label, Action<GrassParams> apply)
    {
        var species = new FoliageSpecies { kind = FoliageSpeciesKind.GrassClump, meshSeed = 1 };
        apply(species.grass);
        return new Tile { Label = label, Mesh = FoliageMeshBuilder.Build(species) };
    }

    private static Tile Sunflower(string label, Action<SunflowerParams> apply)
    {
        var species = new FoliageSpecies { kind = FoliageSpeciesKind.Sunflower, meshSeed = 3 };
        apply(species.sunflower);
        return new Tile { Label = label, Mesh = FoliageMeshBuilder.Build(species) };
    }

    private static Tile Clover(string label, Action<CloverParams> apply)
    {
        var species = new FoliageSpecies { kind = FoliageSpeciesKind.Clover, meshSeed = 2 };
        apply(species.clover);
        return new Tile { Label = label, Mesh = FoliageMeshBuilder.Build(species) };
    }

    private static Tile Reed(string label, Action<ReedParams> apply)
    {
        var species = new FoliageSpecies { kind = FoliageSpeciesKind.Reed, meshSeed = 4 };
        apply(species.reed);
        return new Tile { Label = label, Mesh = FoliageMeshBuilder.Build(species) };
    }

    private static void AddTile(Figure figure, string label, FoliageSpeciesKind kind)
    {
        figure.Tiles.Add(new Tile
        {
            Label = label,
            Mesh = FoliageMeshBuilder.Build(new FoliageSpecies { kind = kind, meshSeed = 1 }),
        });
    }

    private static string Format<T>(T value)
    {
        if (value is float number)
        {
            return number.ToString("0.###", CultureInfo.InvariantCulture);
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    // ----------------------------------------------------------------------
    // Serialisation
    // ----------------------------------------------------------------------

    private static string Serialise(List<Figure> figures)
    {
        var json = new StringBuilder();
        json.Append("{\"figures\":[");

        for (int i = 0; i < figures.Count; i++)
        {
            if (i > 0)
            {
                json.Append(',');
            }

            Append(json, figures[i]);
        }

        json.Append("]}\n");
        return json.ToString();
    }

    private static void Append(StringBuilder json, Figure figure)
    {
        json.Append('{');
        AppendString(json, "id", figure.Id);
        json.Append(',');
        AppendString(json, "title", figure.Title);
        json.Append(',');
        AppendString(json, "caption", figure.Caption);
        json.Append(",\"tiles\":[");

        for (int i = 0; i < figure.Tiles.Count; i++)
        {
            if (i > 0)
            {
                json.Append(',');
            }

            Append(json, figure.Tiles[i]);
        }

        json.Append("]}");
    }

    private static void Append(StringBuilder json, Tile tile)
    {
        Mesh mesh = tile.Mesh;
        Vector3[] positions = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Color[] colors = mesh.colors;
        int[] triangles = mesh.triangles;

        var uv0 = new List<Vector2>();
        mesh.GetUVs(0, uv0);

        var uv3 = new List<Vector4>();
        mesh.GetUVs(3, uv3);

        json.Append('{');
        AppendString(json, "label", tile.Label);
        json.Append(',');
        AppendString(json, "channel", tile.Channel.ToString());

        json.Append(",\"positions\":[");
        for (int i = 0; i < positions.Length; i++)
        {
            Vector3 p = positions[i];

            // The shader collapses every element onto its own root, which is
            // what UV3.xyz stores; doing it here keeps the figure honest.
            if (tile.Shrink < 1f && i < uv3.Count)
            {
                var root = new Vector3(uv3[i].x, uv3[i].y, uv3[i].z);
                p = Vector3.Lerp(root, p, tile.Shrink);
            }

            AppendNumbers(json, i > 0, p.x, p.y, p.z);
        }

        json.Append("],\"normals\":[");
        for (int i = 0; i < normals.Length; i++)
        {
            AppendNumbers(json, i > 0, normals[i].x, normals[i].y, normals[i].z);
        }

        json.Append("],\"colors\":[");
        for (int i = 0; i < colors.Length; i++)
        {
            AppendNumbers(json, i > 0, colors[i].r, colors[i].g, colors[i].b);
        }

        json.Append("],\"scalars\":[");
        if (tile.Channel != Channel.Albedo)
        {
            for (int i = 0; i < positions.Length; i++)
            {
                float value = tile.Channel == Channel.HeightRatio
                    ? (i < uv0.Count ? uv0[i].y : 0f)
                    : (i < uv3.Count ? uv3[i].w : 0f);

                AppendNumbers(json, i > 0, value);
            }
        }

        json.Append("],\"triangles\":[");
        for (int i = 0; i < triangles.Length; i++)
        {
            if (i > 0)
            {
                json.Append(',');
            }

            json.Append(triangles[i].ToString(CultureInfo.InvariantCulture));
        }

        json.Append("]}");
    }

    private static void AppendNumbers(StringBuilder json, bool comma, params float[] values)
    {
        foreach (float value in values)
        {
            if (comma)
            {
                json.Append(',');
            }

            comma = true;

            // Five decimals is far below the resolution of the rendered figure
            // and keeps the intermediate file from carrying float noise that
            // would differ between runs of the same generator.
            json.Append(value.ToString("0.#####", CultureInfo.InvariantCulture));
        }
    }

    private static void AppendString(StringBuilder json, string key, string value)
    {
        json.Append('"').Append(key).Append("\":");
        AppendString(json, value);
    }

    private static void AppendString(StringBuilder json, string value)
    {
        json.Append('"');

        foreach (char c in value ?? string.Empty)
        {
            switch (c)
            {
                case '"':
                    json.Append("\\\"");
                    break;
                case '\\':
                    json.Append("\\\\");
                    break;
                case '\n':
                    json.Append("\\n");
                    break;
                default:
                    if (c < 0x20)
                    {
                        json.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        json.Append(c);
                    }

                    break;
            }
        }

        json.Append('"');
    }
}
