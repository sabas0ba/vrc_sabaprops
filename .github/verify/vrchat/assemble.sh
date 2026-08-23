#!/usr/bin/env bash
#
# Assembles a Unity project that holds the package and the VRChat Worlds SDK
# side by side, so the EditMode tests take the SDK-present branch of
# FoliageVrcWorld instead of the "no SDK, skip it" one.
#
# The SDK comes from fetch.sh, which pins it by hash and runs in a container.
# Only the Unity Editor itself is taken from the host.
#
# Usage: assemble.sh [project-directory]   (default: <repo>/build/WorldProject)
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"
CIPROJECT="$REPO/.github/verify/CIProject"
PACKAGE="$REPO/Packages/com.sabaprops.foliage"

PROJECT="${1:-$REPO/build/WorldProject}"
VPM="${VPM_DIR:-$REPO/build/vpm}"

if [ ! -d "$VPM/com.vrchat.worlds" ] || [ ! -d "$VPM/com.vrchat.base" ]; then
    echo "fetching the pinned VRChat SDK"
    "$HERE/fetch.sh" "$VPM"
fi

rm -rf "$PROJECT"
mkdir -p "$PROJECT/Packages" "$PROJECT/ProjectSettings" "$PROJECT/Assets"

cp "$CIPROJECT/ProjectSettings/ProjectVersion.txt" "$PROJECT/ProjectSettings/"
cp -r "$CIPROJECT/Assets/Tests" "$PROJECT/Assets/Tests"

# Not the CI project's manifest: the SDK needs the built-in module set a real
# world project gets from Unity's 3D template.
cp "$HERE/manifest.json" "$PROJECT/Packages/manifest.json"

# Embedded packages resolve against the working tree and pull their own
# registry dependencies, so nothing has to be listed in manifest.json.
cp -r "$PACKAGE" "$PROJECT/Packages/com.sabaprops.foliage"
cp -r "$VPM/com.vrchat.base" "$PROJECT/Packages/com.vrchat.base"
cp -r "$VPM/com.vrchat.worlds" "$PROJECT/Packages/com.vrchat.worlds"

echo "assembled $PROJECT"
find "$PROJECT" -maxdepth 2 -not -path '*/.*' | sort
