"""Compare generated LOD0 horizontal spans and height, excluding wind padding.

These are bounds measurements, not foliage occupancy or exact radial maxima.
The final local AABB in Unity's text mesh asset is the whole-mesh AABB.
"""

import argparse
from pathlib import Path
import re


BOUNDS = re.compile(
    r"m_LocalAABB:\s*\n\s*m_Center: \{[^}]+\}\s*\n"
    r"\s*m_Extent: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}"
)
WIND_PADDING = 0.45


def spans(path: Path) -> tuple[float, float, float]:
    matches = BOUNDS.findall(path.read_text(encoding="utf-8"))
    if not matches:
        raise ValueError(f"No mesh bounds in {path}")
    result = tuple(2 * (float(value) - WIND_PADDING) for value in matches[-1])
    if any(value <= 0 for value in result):
        raise ValueError(f"Invalid mesh bounds in {path}: {result}")
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("before", type=Path)
    parser.add_argument("after", type=Path)
    args = parser.parse_args()
    previous = sorted(args.before.glob("*_LOD0.asset"))
    if not previous:
        parser.error("before directory contains no LOD0 meshes")
    print("Species,X span ratio,Z span ratio,Height ratio")
    for path in previous:
        old = spans(path)
        new = spans(args.after / path.name)
        ratio = tuple(current / baseline for current, baseline in zip(new, old))
        print(f"{path.stem.removesuffix('_LOD0')},"
              f"{ratio[0]:.3f},{ratio[2]:.3f},{ratio[1]:.3f}")


if __name__ == "__main__":
    main()
