#!/usr/bin/env bash
#
# Assembles a Unity project that holds the package and the VRChat Worlds SDK
# side by side, so the tests take the SDK-present branch of FoliageVrcWorld
# instead of the "no SDK, skip it" one.
#
# The SDK comes from fetch.sh, which pins it by hash and runs in a container.
# Only the Unity Editor itself is taken from the host.
#
# Re-running refreshes just the directories this script owns. Everything else
# is left alone, because the SDK generates assets of its own on first import
# (Assets/UdonSharp) that Unity's import cache then expects to still be there.
# Pass --clean to start over from nothing.
#
# Usage: assemble.sh [project-directory] [--clean]
#        (default project: <repo>/build/WorldProject)
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"
CIPROJECT="$REPO/.github/verify/CIProject"
PACKAGE="$REPO/Packages/io.github.sabas0ba.sabaprops.foliage"

PROJECT="${1:-$REPO/build/WorldProject}"
VPM="${VPM_DIR:-$REPO/build/vpm}"

if [ "${2:-}" = "--clean" ]; then
    rm -rf "$PROJECT"
fi

if [ ! -d "$VPM/com.vrchat.worlds" ] || [ ! -d "$VPM/com.vrchat.base" ]; then
    echo "fetching the pinned VRChat SDK"
    "$HERE/fetch.sh" "$VPM"
fi

mkdir -p "$PROJECT/Packages" "$PROJECT/ProjectSettings" "$PROJECT/Assets"

# Replace a directory we own, leaving the rest of the project untouched.
replace() {
    local source="$1" target="$2"
    rm -rf "$target"
    cp -r "$source" "$target"
}

cp "$CIPROJECT/ProjectSettings/ProjectVersion.txt" "$PROJECT/ProjectSettings/"

# Not the CI project's manifest: the SDK needs the built-in module set a real
# world project gets from Unity's 3D template.
cp "$HERE/manifest.json" "$PROJECT/Packages/manifest.json"

replace "$CIPROJECT/Assets/Tests" "$PROJECT/Assets/Tests"

# PlayMode tests that need the SDK, so they cannot live in the CI project.
replace "$HERE/Tests" "$PROJECT/Assets/WorldTests"

# Brings ProjectSettings up to what a VRChat world project is expected to
# have. run-tests.sh invokes it; see Setup/FoliageWorldProjectSetup.cs.
replace "$HERE/Setup" "$PROJECT/Assets/WorldSetup"

# Embedded packages resolve against the working tree and pull their own
# registry dependencies, so nothing has to be listed in manifest.json.
replace "$PACKAGE" "$PROJECT/Packages/io.github.sabas0ba.sabaprops.foliage"
replace "$VPM/com.vrchat.base" "$PROJECT/Packages/com.vrchat.base"
replace "$VPM/com.vrchat.worlds" "$PROJECT/Packages/com.vrchat.worlds"

# The SDK ships a scene template that Unity cannot walk.
#
# VRCDefaultWorldSceneTemplatePipeline.cs declares a class named
# DefaultSceneTemplatePipeline, so the file name and the type name disagree,
# MonoScript.GetClass() returns null, and SceneTemplateAsset.CreatePipeline()
# hands that null to Activator.CreateInstance. Unity walks every scene template
# in the project on every scene save, so the exception lands on whatever saved
# the scene -- and the test framework counts an unhandled error log as a
# failure. Every test that saves a scene failed, for a reason none of them has
# anything to do with.
#
# It only reproduces before the project has been through a full test session,
# which is why it stayed hidden until a clean checkout ran this.
#
# Removing the template asset removes the walk. It is an entry in the editor's
# New Scene dialog and nothing else: no test instantiates it, and the SDK code
# under test is untouched.
rm -f \
    "$PROJECT/Packages/com.vrchat.worlds/Editor/VRCSDK/SDK3/VRCDefaultWorldScene.scenetemplate" \
    "$PROJECT/Packages/com.vrchat.worlds/Editor/VRCSDK/SDK3/VRCDefaultWorldScene.scenetemplate.meta"

echo "assembled $PROJECT"
find "$PROJECT" -maxdepth 2 -not -path '*/.*' -not -name Library -not -name Temp | sort
