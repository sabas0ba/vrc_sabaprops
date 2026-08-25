#!/usr/bin/env bash
#
# Compiles DumpFigures.cs against the package's real mesh generators and runs
# it, leaving the geometry in <work>/figures.json.
#
# Split out of render.sh so that it can run either on the host or inside the
# pinned .NET SDK container, unchanged. Every path it touches is derived from
# the two arguments, so the caller decides which world those paths are in:
# render.sh passes host paths when the host has a dotnet, and /repo paths when
# it does not.
#
# Usage: dump.sh <repo-root> <work-dir>
set -euo pipefail

REPO="${1:?usage: dump.sh <repo-root> <work-dir>}"
WORK="${2:?usage: dump.sh <repo-root> <work-dir>}"

PACKAGE="$REPO/Packages/io.github.sabas0ba.sabaprops.foliage"
FIGURES="$REPO/.github/figures"

fail() { printf '\033[31merror: %s\033[0m\n' "$1" >&2; exit 1; }

command -v dotnet >/dev/null 2>&1 || fail "dotnet is required but not on PATH"

mkdir -p "$WORK"

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
    "$FIGURES/DumpFigures.cs" \
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

dotnet "$WORK/DumpFigures.dll" > "$WORK/figures.json" || fail "the geometry dump failed"
