#!/usr/bin/env python3
"""Draw the documentation figures from dumped foliage geometry.

DumpFigures.cs runs the package's mesh generators and writes the geometry of
every tile; this projects and shades it into one SVG per figure.

SVG rather than a raster format for three reasons: the figures stay legible at
any size, they are text and therefore reviewable in a diff, and nothing has to
be embedded to draw them -- no font, no PNG encoder, no image library. The
renderer is a painter's-algorithm rasteriser in a few hundred lines, which is
all these meshes need: a few hundred opaque triangles each.

The shading is not the package's shader and is not trying to be. It is a fixed
studio light that makes silhouette and depth readable, so that what differs
between two tiles is the parameter and nothing else.
"""

from __future__ import annotations

import argparse
import json
import math
import os
import sys
from dataclasses import dataclass
from typing import Iterable

# ---------------------------------------------------------------------------
# Camera
# ---------------------------------------------------------------------------

# Slightly off-axis and slightly above: enough to read depth, not so much that
# heights stop being comparable between tiles.
AZIMUTH = math.radians(26.0)
ELEVATION = math.radians(13.0)

# Where the studio light sits, in world space. Not the scene's sun -- figures
# are lit for legibility, from over the reader's left shoulder.
LIGHT = (-0.45, 0.78, -0.44)


def _basis() -> tuple[tuple[float, float, float], ...]:
    """Right, up and forward of the figure camera."""
    ca, sa = math.cos(AZIMUTH), math.sin(AZIMUTH)
    ce, se = math.cos(ELEVATION), math.sin(ELEVATION)

    forward = (sa * ce, -se, ca * ce)
    right = (ca, 0.0, -sa)
    up = (
        forward[1] * right[2] - forward[2] * right[1],
        forward[2] * right[0] - forward[0] * right[2],
        forward[0] * right[1] - forward[1] * right[0],
    )
    return right, up, forward


RIGHT, UP, FORWARD = _basis()


def dot(a: tuple[float, float, float], b: tuple[float, float, float]) -> float:
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def normalise(v: tuple[float, float, float]) -> tuple[float, float, float]:
    length = math.sqrt(dot(v, v))
    if length < 1e-9:
        return (0.0, 1.0, 0.0)
    return (v[0] / length, v[1] / length, v[2] / length)


LIGHT_DIR = normalise(LIGHT)


# ---------------------------------------------------------------------------
# Layout
# ---------------------------------------------------------------------------

MARGIN = 18.0
TILE_WIDTH = 210.0
TILE_HEIGHT = 250.0
TILE_MIN_WIDTH = 110.0
TILE_GAP = 10.0
TILE_PADDING = 14.0
LABEL_HEIGHT = 42.0
TITLE_HEIGHT = 30.0
CAPTION_LINE = 19.0
FOOTER_HEIGHT = 30.0
MIN_PLATE_WIDTH = 460.0
MAX_COLUMNS = 4

# A metre of plant is drawn at the same size in every tile of a figure -- the
# comparison is the whole point -- but the figures do not share a scale with
# each other: a clover would be four pixels tall next to a sunflower. What each
# figure is worth in metres is stated by its scale bar. The cap only keeps a
# degenerate mesh from asking for an infinite one.
MAX_PIXELS_PER_METRE = 4000.0

SCALE_BAR_STEPS = (0.02, 0.05, 0.1, 0.2, 0.5, 1.0, 2.0)

# Colour ramp for the channel figures: dark blue -> green -> yellow.
RAMP = ((0.20, 0.25, 0.36), (0.30, 0.62, 0.44), (0.96, 0.83, 0.37))


@dataclass
class Tile:
    label: str
    channel: str
    positions: list[float]
    normals: list[float]
    colors: list[float]
    scalars: list[float]
    triangles: list[int]


@dataclass
class Metrics:
    """What a tile is worth stating in numbers, taken from the mesh itself."""

    height: float
    triangles: int


@dataclass
class Triangle:
    depth: float
    points: tuple[tuple[float, float], ...]
    fill: str


# ---------------------------------------------------------------------------
# Shading
# ---------------------------------------------------------------------------


def shade(
    normal: tuple[float, float, float],
    albedo: tuple[float, float, float],
    camera_forward: tuple[float, float, float] = FORWARD,
) -> tuple[float, float, float]:
    """Wrapped diffuse, two-sided, as the package's shader is."""
    n = normalise(normal)

    # The shader draws foliage with culling off, so a triangle facing away from
    # the camera is still visible -- and must be lit as if it faced us.
    if dot(n, camera_forward) > 0.0:
        n = (-n[0], -n[1], -n[2])

    wrapped = max(0.0, min(1.0, dot(n, LIGHT_DIR) * 0.5 + 0.5))
    intensity = 0.42 + 0.68 * wrapped

    return tuple(max(0.0, min(1.0, c * intensity)) for c in albedo)


def ramp(t: float) -> tuple[float, float, float]:
    t = max(0.0, min(1.0, t))
    if t < 0.5:
        low, high, k = RAMP[0], RAMP[1], t * 2.0
    else:
        low, high, k = RAMP[1], RAMP[2], (t - 0.5) * 2.0

    return tuple(low[i] + (high[i] - low[i]) * k for i in range(3))


def to_hex(color: Iterable[float]) -> str:
    return "#" + "".join(f"{int(round(max(0.0, min(1.0, c)) * 255)):02x}" for c in color)


# ---------------------------------------------------------------------------
# Geometry
# ---------------------------------------------------------------------------


def project(tile: Tile) -> tuple[list[tuple[float, float, float]], list[float]]:
    """Every vertex as (screen x, screen y, depth), plus the flat position list."""
    projected = []
    positions = tile.positions

    for i in range(0, len(positions), 3):
        p = (positions[i], positions[i + 1], positions[i + 2])
        projected.append((dot(p, RIGHT), dot(p, UP), dot(p, FORWARD)))

    return projected, positions


def extents(tiles: list[Tile]) -> tuple[float, float, float]:
    """Half-width, lowest and highest projected point across a whole figure."""
    half_width = 0.0
    low = 0.0
    high = 0.0

    for tile in tiles:
        projected, _ = project(tile)
        for x, y, _ in projected:
            half_width = max(half_width, abs(x))
            low = min(low, y)
            high = max(high, y)

    return half_width, low, high


def metrics(tile: Tile) -> Metrics:
    positions = tile.positions
    if not positions:
        return Metrics(height=0.0, triangles=0)

    heights = positions[1::3]
    return Metrics(height=max(heights) - min(heights), triangles=len(tile.triangles) // 3)


def triangles_of(tile: Tile, scale: float, origin: tuple[float, float]) -> list[Triangle]:
    projected, _ = project(tile)
    faces: list[Triangle] = []

    for i in range(0, len(tile.triangles), 3):
        indices = tile.triangles[i], tile.triangles[i + 1], tile.triangles[i + 2]

        points = []
        depth = 0.0
        for index in indices:
            x, y, z = projected[index]
            points.append((origin[0] + x * scale, origin[1] - y * scale))
            depth += z

        if tile.channel == "Albedo":
            albedo = average(tile.colors, indices, 3)
            normal = average(tile.normals, indices, 3)
            fill = to_hex(shade(normal, albedo))
        else:
            value = sum(tile.scalars[index] for index in indices) / 3.0
            fill = to_hex(ramp(value))

        faces.append(Triangle(depth=depth / 3.0, points=tuple(points), fill=fill))

    # Painter's algorithm: farthest first. The camera looks along +forward, so a
    # larger projection onto it is farther away.
    faces.sort(key=lambda face: -face.depth)
    return faces


def average(values: list[float], indices: tuple[int, ...], stride: int) -> tuple[float, ...]:
    if not values:
        return tuple(0.0 for _ in range(stride))

    total = [0.0] * stride
    for index in indices:
        base = index * stride
        for channel in range(stride):
            total[channel] += values[base + channel]

    return tuple(component / len(indices) for component in total)


# ---------------------------------------------------------------------------
# SVG
# ---------------------------------------------------------------------------

STYLE = """
  .plate { fill: #f6f7f4; }
  .panel { fill: #ffffff; stroke: #e2e5de; }
  .title { fill: #1b1f19; font-weight: 600; }
  .label { fill: #1b1f19; }
  .muted { fill: #5d6659; }
  .rule  { stroke: #c9cec3; }
  @media (prefers-color-scheme: dark) {
    .plate { fill: #171b15; }
    .panel { fill: #1e2219; stroke: #2f3529; }
    .title, .label { fill: #e8ebe5; }
    .muted { fill: #9aa494; }
    .rule  { stroke: #4a5245; }
  }
"""

FONT = "system-ui, -apple-system, 'Noto Sans JP', 'Hiragino Sans', Meiryo, sans-serif"


def number(value: float) -> str:
    text = f"{value:.1f}"
    return text[:-2] if text.endswith(".0") else text


def escape(text: str) -> str:
    return (
        text.replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
        .replace('"', "&quot;")
    )


def advance(char: str) -> float:
    """Width of one character in em, near enough for laying out a caption."""
    return 1.0 if ord(char) > 0x2E7F else 0.55


def wrap(text: str, width: float, size: float) -> list[str]:
    """Break a caption to fit, counting CJK as full width and ASCII as half."""
    limit = width / size
    lines: list[str] = []
    current = ""
    used = 0.0

    for char in text:
        cost = advance(char)
        if used + cost > limit and current:
            lines.append(current)
            current, used = "", 0.0

        current += char
        used += cost

    if current:
        lines.append(current)

    return lines


def scale_bar_length(scale: float) -> float:
    """A round number of metres that draws about 70 px wide."""
    target = 70.0 / scale
    return min(SCALE_BAR_STEPS, key=lambda step: abs(math.log(step / target)))


def text_width(text: str, size: float) -> float:
    """Rough advance width, counting CJK as full width and ASCII as half."""
    return sum(advance(char) for char in text) * size


# ---------------------------------------------------------------------------
# Documentation hero
# ---------------------------------------------------------------------------

HERO_WIDTH = 1200.0
HERO_HEIGHT = 675.0
HERO_CAMERA = (0.0, 2.4, -7.5)
HERO_TARGET = (0.0, 0.65, 4.0)
HERO_FOCAL = HERO_WIDTH / (2.0 * math.tan(math.radians(48.0) / 2.0))
HERO_CENTRE = (HERO_WIDTH / 2.0, HERO_HEIGHT * 0.48)
HERO_FOG = (0.63, 0.72, 0.64)


def cross(a: tuple[float, float, float], b: tuple[float, float, float]) -> tuple[float, float, float]:
    return (
        a[1] * b[2] - a[2] * b[1],
        a[2] * b[0] - a[0] * b[2],
        a[0] * b[1] - a[1] * b[0],
    )


HERO_FORWARD = normalise(tuple(HERO_TARGET[i] - HERO_CAMERA[i] for i in range(3)))
HERO_RIGHT = normalise(cross((0.0, 1.0, 0.0), HERO_FORWARD))
HERO_UP = normalise(cross(HERO_FORWARD, HERO_RIGHT))


def hero_project(point: tuple[float, float, float]) -> tuple[float, float, float]:
    relative = tuple(point[i] - HERO_CAMERA[i] for i in range(3))
    depth = dot(relative, HERO_FORWARD)
    if depth < 1e-4:
        depth = 1e-4

    return (
        HERO_CENTRE[0] + dot(relative, HERO_RIGHT) * HERO_FOCAL / depth,
        HERO_CENTRE[1] - dot(relative, HERO_UP) * HERO_FOCAL / depth,
        depth,
    )


def hero_triangles(tile: Tile) -> list[Triangle]:
    projected = []
    for i in range(0, len(tile.positions), 3):
        projected.append(hero_project(tuple(tile.positions[i : i + 3])))

    faces: list[Triangle] = []
    for i in range(0, len(tile.triangles), 3):
        indices = tile.triangles[i], tile.triangles[i + 1], tile.triangles[i + 2]
        points = tuple((projected[index][0], projected[index][1]) for index in indices)
        depth = sum(projected[index][2] for index in indices) / 3.0

        albedo = average(tile.colors, indices, 3)
        normal = average(tile.normals, indices, 3)
        lit = shade(normal, albedo, HERO_FORWARD)

        fog = max(0.0, min(0.34, (depth - 9.0) / 24.0))
        colour = tuple(lit[channel] + (HERO_FOG[channel] - lit[channel]) * fog for channel in range(3))
        faces.append(Triangle(depth=depth, points=points, fill=to_hex(colour)))

    faces.sort(key=lambda face: -face.depth)
    return faces


def render_hero(figure: dict) -> str:
    tile_data = figure["tiles"][0]
    tile = Tile(
        label=tile_data["label"],
        channel=tile_data["channel"],
        positions=tile_data["positions"],
        normals=tile_data["normals"],
        colors=tile_data["colors"],
        scalars=tile_data["scalars"],
        triangles=tile_data["triangles"],
    )

    out = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{number(HERO_WIDTH)}" '
        f'height="{number(HERO_HEIGHT)}" viewBox="0 0 {number(HERO_WIDTH)} {number(HERO_HEIGHT)}" '
        f'role="img" aria-label="{escape(figure["title"])}">',
        "<defs>",
        '<linearGradient id="sky" x1="0" y1="0" x2="0" y2="1">'
        '<stop offset="0" stop-color="#dce9ed"/><stop offset="1" stop-color="#f2f3dc"/>'
        "</linearGradient>",
        '<linearGradient id="ground" x1="0" y1="0" x2="0" y2="1">'
        '<stop offset="0" stop-color="#9caf78"/><stop offset="1" stop-color="#4f6945"/>'
        "</linearGradient>",
        '<radialGradient id="field-shadow">'
        '<stop offset="0" stop-color="#243620" stop-opacity="0.34"/>'
        '<stop offset="1" stop-color="#243620" stop-opacity="0"/>'
        "</radialGradient>",
        "</defs>",
        f'<rect width="{number(HERO_WIDTH)}" height="{number(HERO_HEIGHT)}" fill="url(#sky)"/>',
        '<circle cx="985" cy="118" r="43" fill="#fff8cb" opacity="0.74"/>',
        '<path d="M0 320 C190 275 365 310 535 292 C745 270 930 305 1200 255 L1200 410 L0 410 Z" '
        'fill="#a8b88b" opacity="0.68"/>',
        f'<rect y="340" width="{number(HERO_WIDTH)}" height="{number(HERO_HEIGHT - 340)}" '
        'fill="url(#ground)"/>',
        '<ellipse cx="600" cy="585" rx="565" ry="130" fill="url(#field-shadow)"/>',
    ]

    for face in hero_triangles(tile):
        path = " ".join(
            f"{'M' if i == 0 else 'L'}{number(x)},{number(y)}" for i, (x, y) in enumerate(face.points)
        )
        out.append(f'<path d="{path}Z" fill="{face.fill}"/>')

    out.extend(
        [
            '<path d="M0 640 C250 612 430 660 650 630 C870 600 1035 650 1200 620 L1200 675 L0 675 Z" '
            'fill="#405b3d" opacity="0.34"/>',
            '<rect x="0.5" y="0.5" width="1199" height="674" rx="8" fill="none" '
            'stroke="#31402e" stroke-opacity="0.22"/>',
            "</svg>",
        ]
    )
    return "\n".join(out) + "\n"


def render(figure: dict) -> str:
    if figure.get("layout") == "Hero":
        return render_hero(figure)

    tiles = [
        Tile(
            label=tile["label"],
            channel=tile["channel"],
            positions=tile["positions"],
            normals=tile["normals"],
            colors=tile["colors"],
            scalars=tile["scalars"],
            triangles=tile["triangles"],
        )
        for tile in figure["tiles"]
    ]

    columns = min(MAX_COLUMNS, len(tiles))
    rows = (len(tiles) + columns - 1) // columns

    half_width, low, high = extents(tiles)

    scale = MAX_PIXELS_PER_METRE
    if half_width > 1e-6:
        scale = min(scale, (TILE_WIDTH - TILE_PADDING * 2) / (half_width * 2.0))
    if high - low > 1e-6:
        scale = min(scale, (TILE_HEIGHT - TILE_PADDING * 2) / (high - low))

    # One scale for the whole figure -- otherwise neighbouring tiles could not
    # be compared -- but the tile itself is only as large as that drawing needs,
    # so a figure of clovers is not four sheets of empty paper.
    label_width = max(text_width(tile.label, 12.5) for tile in tiles) + 16.0
    tile_width = max(TILE_MIN_WIDTH, label_width, min(TILE_WIDTH, half_width * 2.0 * scale + TILE_PADDING * 2))
    tile_height = max(TILE_MIN_WIDTH, min(TILE_HEIGHT, (high - low) * scale + TILE_PADDING * 2))

    grid_width = columns * tile_width + (columns - 1) * TILE_GAP

    # A narrow grid still gets a plate wide enough to set the caption on, with
    # the tiles centred in it.
    content_width = max(grid_width, MIN_PLATE_WIDTH)
    grid_left = MARGIN + (content_width - grid_width) / 2.0

    # The ground circle marks y = 0 without leaving the tile, whatever the
    # figure's scale is.
    ground = min(half_width * 1.35, (tile_width / 2.0 - 6.0) / scale)

    caption = wrap(figure["caption"], content_width, 12.5)

    width = MARGIN * 2 + content_width
    height = (
        MARGIN * 2
        + TITLE_HEIGHT
        + rows * (tile_height + LABEL_HEIGHT)
        + (rows - 1) * TILE_GAP
        + FOOTER_HEIGHT
        + len(caption) * CAPTION_LINE
    )

    out: list[str] = []
    out.append(
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{number(width)}" height="{number(height)}" '
        f'viewBox="0 0 {number(width)} {number(height)}" role="img" '
        f'aria-label="{escape(figure["title"])}">'
    )
    out.append(f"<style>{STYLE}</style>")
    out.append(f'<rect class="plate" width="{number(width)}" height="{number(height)}"/>')
    out.append(f'<g font-family="{FONT}">')
    out.append(
        f'<text class="title" x="{number(MARGIN)}" y="{number(MARGIN + 14)}" '
        f'font-size="15">{escape(figure["title"])}</text>'
    )

    for index, tile in enumerate(tiles):
        column = index % columns
        row = index // columns

        left = grid_left + column * (tile_width + TILE_GAP)
        top = MARGIN + TITLE_HEIGHT + row * (tile_height + LABEL_HEIGHT + TILE_GAP)

        out.extend(draw_tile(tile, left, top, tile_width, tile_height, scale, low, ground))

    bar = MARGIN + TITLE_HEIGHT + rows * (tile_height + LABEL_HEIGHT) + (rows - 1) * TILE_GAP
    out.extend(draw_scale_bar(MARGIN, bar, scale))

    if tiles[0].channel != "Albedo":
        out.extend(draw_ramp_legend(width - MARGIN, bar))

    for line, text in enumerate(caption):
        out.append(
            f'<text class="muted" x="{number(MARGIN)}" y="{number(bar + FOOTER_HEIGHT + line * CAPTION_LINE)}" '
            f'font-size="12.5">{escape(text)}</text>'
        )

    out.append("</g></svg>")
    return "\n".join(out) + "\n"


def draw_tile(
    tile: Tile,
    left: float,
    top: float,
    width: float,
    height: float,
    scale: float,
    low: float,
    ground: float,
) -> list[str]:
    out = [
        f'<rect class="panel" x="{number(left)}" y="{number(top)}" '
        f'width="{number(width)}" height="{number(height)}" rx="6"/>'
    ]

    # The world origin sits on the tile's baseline, so every tile in a figure
    # shares one ground plane and one scale.
    baseline = top + height - TILE_PADDING + low * scale
    origin = (left + width / 2.0, baseline)

    out.append(ground_ellipse(origin, scale, ground))

    for face in triangles_of(tile, scale, origin):
        path = " ".join(
            f"{'M' if i == 0 else 'L'}{number(x)},{number(y)}" for i, (x, y) in enumerate(face.points)
        )
        out.append(f'<path d="{path}Z" fill="{face.fill}"/>')

    measured = metrics(tile)
    centre = left + width / 2.0

    out.append(
        f'<text class="label" x="{number(centre)}" y="{number(top + height + 17)}" '
        f'font-size="12.5" text-anchor="middle">{escape(tile.label)}</text>'
    )
    out.append(
        f'<text class="muted" x="{number(centre)}" y="{number(top + height + 33)}" '
        f'font-size="11" text-anchor="middle">'
        f'{measured.height:.2f} m ・ {measured.triangles} tris</text>'
    )
    return out


def ground_ellipse(origin: tuple[float, float], scale: float, radius: float) -> str:
    """The y = 0 plane, drawn as the circle the camera actually sees."""
    points = []
    for step in range(33):
        angle = step / 32.0 * math.tau
        p = (math.cos(angle) * radius, 0.0, math.sin(angle) * radius)
        x = origin[0] + dot(p, RIGHT) * scale
        y = origin[1] - dot(p, UP) * scale
        points.append(f"{number(x)},{number(y)}")

    return f'<polyline class="rule" fill="none" stroke-width="0.8" points="{" ".join(points)}"/>'


def draw_scale_bar(left: float, top: float, scale: float) -> list[str]:
    metres = scale_bar_length(scale)
    length = metres * scale
    y = top + 12

    label = f"{metres:g} m"
    return [
        f'<path class="rule" stroke-width="1.2" fill="none" '
        f'd="M{number(left)},{number(y - 4)}L{number(left)},{number(y + 4)}'
        f'M{number(left)},{number(y)}L{number(left + length)},{number(y)}'
        f'M{number(left + length)},{number(y - 4)}L{number(left + length)},{number(y + 4)}"/>',
        f'<text class="muted" x="{number(left + length + 8)}" y="{number(y + 4)}" '
        f'font-size="12">{escape(label)}</text>',
    ]


def draw_ramp_legend(right: float, top: float) -> list[str]:
    width, height = 120.0, 8.0
    left = right - width
    y = top + 8

    out = [f'<text class="muted" x="{number(left - 8)}" y="{number(y + 8)}" '
           f'font-size="12" text-anchor="end">0</text>']

    steps = 24
    for step in range(steps):
        t = step / (steps - 1.0)
        out.append(
            f'<rect x="{number(left + width * step / steps)}" y="{number(y)}" '
            f'width="{number(width / steps + 0.6)}" height="{number(height)}" fill="{to_hex(ramp(t))}"/>'
        )

    out.append(
        f'<text class="muted" x="{number(right + 4)}" y="{number(y + 8)}" font-size="12">1</text>'
    )
    return out


# ---------------------------------------------------------------------------
# Entry point
# ---------------------------------------------------------------------------


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", required=True, help="geometry dumped by DumpFigures.cs")
    parser.add_argument("--out", required=True, help="directory to write the SVG files into")
    args = parser.parse_args()

    with open(args.input, encoding="utf-8") as handle:
        document = json.load(handle)

    os.makedirs(args.out, exist_ok=True)

    written = []
    for figure in document["figures"]:
        path = os.path.join(args.out, figure["id"] + ".svg")
        with open(path, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(render(figure))
        written.append(figure["id"])

    print(f"wrote {len(written)} figure(s) to {args.out}")
    for name in written:
        print(f"  {name}.svg")

    return 0


if __name__ == "__main__":
    sys.exit(main())
