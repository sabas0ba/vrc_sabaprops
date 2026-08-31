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

mkdir -p "$WORK"

# ---------------------------------------------------------------------------
log "Running the mesh generators"
# ---------------------------------------------------------------------------
# The C# half lives in dump.sh so that it can run either here or inside the
# pinned SDK container without changing. CI has a dotnet and takes the first
# path; a contributor with only a container engine takes the second and gets
# the same compiler, because the image is pinned by digest.
if command -v dotnet >/dev/null 2>&1; then
    "$HERE/dump.sh" "$REPO" "$WORK"
else
    case "$WORK" in
        "$REPO"/*) ;;
        *) fail "VERIFY_WORK_DIR must be inside the repository when dotnet is not installed, so the container can see it" ;;
    esac

    "$REPO/.github/scripts/dotnet.sh" \
        bash /repo/.github/figures/dump.sh /repo "/repo/${WORK#"$REPO"/}"
fi

echo "ok: $(wc -c < "$WORK/figures.json") bytes"

# ---------------------------------------------------------------------------
log "Rendering figures"
# ---------------------------------------------------------------------------
# Keep the same override as verify.sh for a pinned development container.
PYTHON="${VERIFY_PYTHON:-$REPO/.github/scripts/run.sh}"

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
