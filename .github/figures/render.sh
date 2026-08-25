#!/usr/bin/env bash
#
# Regenerates the documentation figures.
#
# Two halves, joined by a JSON file:
#
#   DumpFigures.cs      runs the package's real mesh generators against the
#                       offline shim and dumps the geometry
#   render_figures.py   projects and shades that geometry into one labelled
#                       SVG per figure
#
# The split is deliberate. The half that must not drift from the package is the
# geometry, and it stays inside the package's own code; the half that is only
# presentation needs no C# and runs in the same pinned container as the rest of
# this repository's Python.
#
# Usage:
#   render.sh            write the figures into the package
#   render.sh --check    regenerate into a temporary directory and fail if the
#                        committed figures differ (this is what CI runs)
#
# Requirements: dotnet SDK 8+, and podman or docker for the Python half.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
PACKAGE="$REPO/Packages/io.github.sabas0ba.sabaprops.foliage"

WORK="${VERIFY_WORK_DIR:-$REPO/.verify}/figures"
COMMITTED="$PACKAGE/Documentation~/images/generated"

MODE="${1:-write}"
case "$MODE" in
    write | --check) ;;
    *)
        echo "usage: render.sh [--check]" >&2
        exit 2
        ;;
esac

log() { printf '\n\033[1m== %s\033[0m\n' "$1"; }
fail() { printf '\033[31merror: %s\033[0m\n' "$1" >&2; exit 1; }

command -v dotnet >/dev/null 2>&1 || fail "dotnet is required but not installed"

mkdir -p "$WORK"

# ---------------------------------------------------------------------------
log "Building the geometry dump"
# ---------------------------------------------------------------------------

SDK_ROOT="$(dotnet --list-sdks | tail -1 | sed -E 's/^[^ ]+ \[(.*)\]$/\1/')"
[ -d "$SDK_ROOT" ] || fail "could not determine the .NET SDK root from 'dotnet --list-sdks'"

CSC_DLL="$(find "$SDK_ROOT" -name csc.dll -path '*bincore*' 2>/dev/null | head -1)"
[ -n "$CSC_DLL" ] || fail "could not locate the Roslyn compiler (csc.dll) under $SDK_ROOT"

# Same arrangement as the offline mesh checks in ../verify/verify.sh: the shim
# replaces UnityEngine entirely, so this targets the installed runtime rather
# than the net35 Unity references.
RUNTIME_DIR="$(dotnet --list-runtimes \
    | awk '/^Microsoft.NETCore.App /{ gsub(/[][]/, "", $3); dir=$3 "/" $2 } END { print dir }')"
[ -d "$RUNTIME_DIR" ] || fail "could not locate a Microsoft.NETCore.App shared framework"

RUNTIME_ARGS=()
for name in System.Runtime System.Private.CoreLib System.Collections System.Console System.Linq; do
    RUNTIME_ARGS+=(-r:"$RUNTIME_DIR/$name.dll")
done

dotnet "$CSC_DLL" -nologo -langversion:9.0 -target:exe -nostdlib+ -noconfig \
    "${RUNTIME_ARGS[@]}" \
    -out:"$WORK/DumpFigures.dll" \
    "$REPO/.github/verify/offline/UnityEngineShim.cs" \
    "$HERE/DumpFigures.cs" \
    "$PACKAGE/Runtime/FoliageRandom.cs" \
    "$PACKAGE/Runtime/FoliageSeason.cs" \
    "$PACKAGE/Runtime/FoliageSpecies.cs" \
    "$PACKAGE/Editor/FoliageMeshBuffer.cs" \
    "$PACKAGE/Editor/FoliageMeshBuilder.cs" \
    "$PACKAGE/Editor/FoliageSeasonPass.cs"

cat > "$WORK/DumpFigures.runtimeconfig.json" <<'JSON'
{
  "runtimeOptions": {
    "tfm": "net8.0",
    "framework": { "name": "Microsoft.NETCore.App", "version": "8.0.0" },
    "rollForward": "latestMajor"
  }
}
JSON

# ---------------------------------------------------------------------------
log "Running the mesh generators"
# ---------------------------------------------------------------------------
dotnet "$WORK/DumpFigures.dll" > "$WORK/figures.json" || fail "the geometry dump failed"
echo "ok: $(wc -c < "$WORK/figures.json") bytes"

# ---------------------------------------------------------------------------
log "Rendering figures"
# ---------------------------------------------------------------------------
# All Python in this repository runs in a pinned container; see run.sh.
PYTHON="$REPO/.github/scripts/run.sh"

if [ "$MODE" = "--check" ]; then
    OUT="$WORK/svg"
    rm -rf "$OUT"
else
    OUT="$COMMITTED"
fi

mkdir -p "$OUT"
"$PYTHON" .github/figures/render_figures.py --input "$WORK/figures.json" --out "$OUT"

if [ "$MODE" = "--check" ]; then
    if ! diff -ru "$COMMITTED" "$OUT" >/dev/null 2>&1; then
        diff -ru "$COMMITTED" "$OUT" || true
        fail "the committed figures are out of date; run .github/figures/render.sh and commit the result"
    fi
    echo "ok: committed figures match the generators"
fi
