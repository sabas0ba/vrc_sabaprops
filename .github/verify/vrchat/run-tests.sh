#!/usr/bin/env bash
#
# Runs the package's tests inside the world verification project, so the
# VRChat branch of FoliageVrcWorld is exercised for real: the EditMode tests
# check the scene is authored correctly, and SabaProps.Foliage.WorldTests
# enters play mode under ClientSim to check the result actually runs as a
# world.
#
# Assembles the project first if it is not there. The Unity Editor comes from
# the host: running it in a container needs a licence, which is the same
# constraint .github/workflows/unity.yml documents.
#
# Usage:
#   UNITY=/path/to/Unity ./run-tests.sh [project-directory]
#
# UNITY may also be a Unity Hub install root, in which case the editor matching
# ProjectVersion.txt is used.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"
PROJECT="${1:-$REPO/build/WorldProject}"

VERSION="$(sed -n 's/^m_EditorVersion: *//p' "$REPO/.github/verify/CIProject/ProjectSettings/ProjectVersion.txt")"
[ -n "$VERSION" ] || { echo "error: could not read the editor version from ProjectVersion.txt" >&2; exit 1; }

resolve_unity() {
    if [ -n "${UNITY:-}" ]; then
        if [ -d "$UNITY" ]; then
            for candidate in "$UNITY/$VERSION/Editor/Unity.exe" "$UNITY/$VERSION/Editor/Unity"; do
                [ -x "$candidate" ] && { printf '%s' "$candidate"; return; }
            done
        fi
        printf '%s' "$UNITY"
        return
    fi

    for root in "/c/Program Files/Unity/Hub/Editor" "$HOME/Unity/Hub/Editor" "/Applications/Unity/Hub/Editor"; do
        for candidate in "$root/$VERSION/Editor/Unity.exe" "$root/$VERSION/Editor/Unity"; do
            [ -x "$candidate" ] && { printf '%s' "$candidate"; return; }
        done
    done
}

UNITY_BIN="$(resolve_unity)"
if [ -z "$UNITY_BIN" ] || [ ! -x "$UNITY_BIN" ]; then
    echo "error: Unity $VERSION was not found; set UNITY to the editor or to the Hub install root" >&2
    exit 1
fi

[ -d "$PROJECT/Packages/com.vrchat.worlds" ] || "$HERE/assemble.sh" "$PROJECT"

RESULTS="$PROJECT/TestResults/results.xml"
LOG="$PROJECT/unity.log"
mkdir -p "$PROJECT/TestResults"
rm -f "$RESULTS" "$LOG"

to_native() {
    if command -v cygpath >/dev/null 2>&1; then
        cygpath -w "$1"
    else
        printf '%s' "$1"
    fi
}

echo "running EditMode tests in $PROJECT"

# The SDK ships its own test assemblies, and two of them fail for reasons that
# have nothing to do with this package (a randomised JSON fuzz case, and one
# that asserts a docs.microsoft.com URL resolves). Filter to ours so the exit
# status means something.
set +e
"$UNITY_BIN" \
    -batchmode \
    -projectPath "$(to_native "$PROJECT")" \
    -runTests -testPlatform EditMode \
    -testFilter "SabaProps.Foliage.CITests;SabaProps.Foliage.WorldTests" \
    -testResults "$(to_native "$RESULTS")" \
    -logFile "$(to_native "$LOG")"
set -e

if [ ! -f "$RESULTS" ]; then
    echo "error: no test results were written; see $LOG" >&2
    grep -E "error CS" "$LOG" | head -20 >&2 || true
    exit 1
fi

python3 - "$RESULTS" <<'PY'
import sys
import xml.etree.ElementTree as ET

root = ET.parse(sys.argv[1]).getroot()
failed = 0

for case in root.iter("test-case"):
    result = case.get("result")
    print(f"  [{result:<7}] {case.get('fullname')}")
    if result != "Passed":
        failed += 1
        message = case.find("failure/message")
        if message is not None and message.text:
            print("      " + message.text.strip()[:1000])

print(f"total={root.get('total')} passed={root.get('passed')} failed={root.get('failed')}")
sys.exit(1 if failed else 0)
PY
