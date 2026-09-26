#!/usr/bin/env bash
#
# Regenerates the liquid package's bundled sample (Samples~/LiquidDemo, with the
# demo and comparison scenes) from the generator, in the world verification project.
#
# The sample is generated rather than hand-edited: this script replaces the
# package in the project with the working tree's copy, deletes the previously
# generated assets, runs LiquidSampleScene.CreateForExport in batch mode, and
# copies the scene, its materials and the serialized Udon programs it points
# at into the package.
#
# Generated assets get new GUIDs on every export. That is why the old ones are
# deleted first: loading them would keep settings from an older generator.
#
# Usage:
#   UNITY=/path/to/Unity ./export-liquid-sample.sh [project-directory]
#
# The project must already be assembled and configured (run-tests.sh does both).
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"
PROJECT="${1:-$REPO/build/WorldProject}"
PACKAGE="$REPO/Packages/io.github.sabas0ba.sabaprops.liquid"
SAMPLE="$PACKAGE/Samples~/LiquidDemo"

VERSION="$(sed -n 's/^m_EditorVersion: *//p' "$REPO/.github/verify/CIProject/ProjectSettings/ProjectVersion.txt")"
UNITY_BIN="${UNITY:-}"
if [ -z "$UNITY_BIN" ]; then
    for root in "/c/Program Files/Unity/Hub/Editor" "$HOME/Unity/Hub/Editor" "/Applications/Unity/Hub/Editor"; do
        for candidate in "$root/$VERSION/Editor/Unity.exe" "$root/$VERSION/Editor/Unity"; do
            [ -x "$candidate" ] && { UNITY_BIN="$candidate"; break 2; }
        done
    done
fi
[ -n "$UNITY_BIN" ] && [ -x "$UNITY_BIN" ] || { echo "error: Unity $VERSION was not found; set UNITY" >&2; exit 1; }
[ -d "$PROJECT/Packages/com.vrchat.worlds" ] || { echo "error: $PROJECT is not assembled; run run-tests.sh first" >&2; exit 1; }

to_native() {
    if command -v cygpath >/dev/null 2>&1; then cygpath -wa "$1"; else printf '%s' "$1"; fi
}

rm -rf "$PROJECT/Packages/io.github.sabas0ba.sabaprops.liquid"
cp -r "$PACKAGE" "$PROJECT/Packages/io.github.sabas0ba.sabaprops.liquid"
rm -rf "$PROJECT/Assets/SabaProps/Liquid" "$PROJECT/Assets/SabaProps/Liquid.meta"

LOG="$PROJECT/liquid-sample-export.log"
echo "generating the sample scene in $PROJECT"
"$UNITY_BIN" -batchmode -quit \
    -projectPath "$(to_native "$PROJECT")" \
    -executeMethod SabaProps.Liquid.Editors.LiquidSampleScene.CreateForExport \
    -logFile "$(to_native "$LOG")"

GENERATED="$PROJECT/Assets/SabaProps/Liquid"
for scene in LiquidDemo LiquidComparison; do
    [ -f "$GENERATED/Samples/$scene.unity" ] || { echo "error: $scene was not generated; see $LOG" >&2; exit 1; }
done

rm -rf "$SAMPLE"
mkdir -p "$SAMPLE/Assets/SabaProps" "$SAMPLE/Assets/SerializedUdonPrograms"
cp -r "$GENERATED" "$SAMPLE/Assets/SabaProps/Liquid"
cp "$PROJECT/Assets/SabaProps/Liquid.meta" "$SAMPLE/Assets/SabaProps/Liquid.meta"

# A behaviour in the scene points at Assets/SerializedUdonPrograms/<guid>.asset,
# named after the GUID of its UdonSharpProgramAsset in the package.
copied=0
for meta in "$PACKAGE"/Runtime/*.asset.meta; do
    guid="$(sed -n 's/^guid: *//p' "$meta")"
    program="$PROJECT/Assets/SerializedUdonPrograms/$guid.asset"
    if [ -f "$program" ]; then
        cp "$program" "$program.meta" "$SAMPLE/Assets/SerializedUdonPrograms/"
        copied=$((copied + 1))
    fi
done

echo "exported $SAMPLE ($copied serialized programs)"
