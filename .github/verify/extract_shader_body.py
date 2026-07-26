#!/usr/bin/env python3
"""Extract the CGPROGRAM body of a ShaderLab file into a standalone .hlsl file.

`shader_harness.hlsl` includes the result and calls every entry point, which
lets glslang type-check the shader's own code without Unity. The #pragma lines
are commented out: they configure Unity's surface shader generator and mean
nothing to a plain HLSL compiler.
"""

from __future__ import annotations

import argparse
import re
import sys


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("shader", help="path to the .shader file")
    parser.add_argument("output", help="path to write the extracted body to")
    args = parser.parse_args()

    try:
        with open(args.shader, "r", encoding="utf-8") as handle:
            source = handle.read()
    except OSError as exc:
        print(f"error: cannot read {args.shader}: {exc}", file=sys.stderr)
        return 1

    blocks = re.findall(r"CGPROGRAM(.*?)ENDCG", source, re.S)
    if not blocks:
        print(f"error: no CGPROGRAM block in {args.shader}", file=sys.stderr)
        return 1

    if len(blocks) > 1:
        print(f"error: {len(blocks)} CGPROGRAM blocks found; the harness expects one", file=sys.stderr)
        return 1

    lines = []
    pragmas = 0

    for line in blocks[0].splitlines():
        if line.strip().startswith("#pragma"):
            pragmas += 1
            lines.append("// " + line.strip())
        else:
            lines.append(line)

    with open(args.output, "w", encoding="utf-8") as handle:
        handle.write("\n".join(lines) + "\n")

    print(f"extracted {len(lines)} lines ({pragmas} #pragma lines commented out) -> {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
