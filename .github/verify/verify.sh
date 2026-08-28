#!/usr/bin/env bash
#
# Verify the package without a Unity installation.
#
# What this proves:
#   * the Runtime assembly compiles against REAL UnityEngine reference
#     assemblies (Unity's own UnityEngine.Modules NuGet package)
#   * the Editor assembly compiles, with its UnityEngine usage checked against
#     those same real assemblies
#   * the shader's HLSL type-checks in all four shader_feature combinations
#   * mesh generation RUNS, and the geometry it produces holds up: topology,
#     finiteness, determinism, the UV3/COLOR channels the shader reads, and the
#     wind-joint rules that stop a plant coming apart. See offline/.
#   * the documentation figures still match what the generators produce, and
#     the site renders, with no raw Markdown left in the text, no broken
#     internal links and no missing images
#
# What this does NOT prove:
#   * UnityEditor API signatures. UnityEditor.dll is not redistributable, so
#     `UnityEditorStub.cs` stands in for it and is written by hand.
#   * that Unity's surface shader generator accepts the #pragma configuration,
#     or that the generated variants compile. Only the shader's own code is
#     checked here.
#   * anything that needs the editor at runtime: scattering (it raycasts),
#     asset writing, the sample scene, the VRChat world path.
#   * numeric agreement with Unity's own maths. The offline run uses a
#     reimplementation, so it asserts structure rather than exact values.
#
# Closing those gaps needs a real Unity install; see README.md.
#
# Requirements: dotnet SDK 8+, glslangValidator, curl, unzip, and podman or
# docker. Python is not required on the host: every script that needs it runs
# in the pinned container that .github/scripts/run.sh starts.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
PACKAGE="${1:-$REPO/Packages/io.github.sabas0ba.sabaprops.foliage}"
TREE_PACKAGE="${TREE_PACKAGE:-$REPO/Packages/io.github.sabas0ba.sabaprops.trees}"

WORK="${VERIFY_WORK_DIR:-$REPO/.verify}"
REFS="$WORK/refs"
OUT="$WORK/out"

UNITY_REFS_VERSION="2021.3.33"
NETFX_REFS_VERSION="1.0.3"

mkdir -p "$REFS" "$OUT"

log() { printf '\n\033[1m== %s\033[0m\n' "$1"; }
fail() { printf '\033[31merror: %s\033[0m\n' "$1" >&2; exit 1; }

for tool in dotnet curl unzip glslangValidator; do
    command -v "$tool" >/dev/null 2>&1 || fail "$tool is required but not installed"
done

# ---------------------------------------------------------------------------
log "Fetching reference assemblies"
# ---------------------------------------------------------------------------

fetch_nupkg() {
    local id="$1" version="$2" dest="$3"
    if [ -d "$dest" ]; then
        echo "cached: $id $version"
        return
    fi
    local url="https://api.nuget.org/v3-flatcontainer/${id}/${version}/${id}.${version}.nupkg"
    echo "downloading $id $version"
    curl -sS --fail --max-time 300 -o "$REFS/$id.nupkg" "$url"
    unzip -q -o "$REFS/$id.nupkg" -d "$dest"
    rm -f "$REFS/$id.nupkg"
}

# Unity publishes its own UnityEngine reference assemblies to NuGet.
fetch_nupkg unityengine.modules "$UNITY_REFS_VERSION" "$REFS/unity"
fetch_nupkg microsoft.netframework.referenceassemblies.net472 "$NETFX_REFS_VERSION" "$REFS/netfx"

# UnityEngine.Modules stores its entries with mode 000, and unzip faithfully
# reproduces that. Root does not care, an ordinary CI user cannot read a single
# DLL. Applied outside fetch_nupkg so a restored cache is fixed up too.
chmod -R u+rwX "$REFS"

UNITY_DIR="$REFS/unity/lib/net35"
NETFX_DIR="$REFS/netfx/build/.NETFramework/v4.7.2"

[ -f "$UNITY_DIR/UnityEngine.CoreModule.dll" ] || fail "UnityEngine reference assemblies missing"
[ -f "$NETFX_DIR/mscorlib.dll" ] || fail ".NET Framework reference assemblies missing"

# `dotnet --list-sdks` prints "<version> [<sdk root>]"; that root is the only
# reliable way to find Roslyn across distro packages and setup-dotnet installs.
SDK_ROOT="$(dotnet --list-sdks | tail -1 | sed -E 's/^[^ ]+ \[(.*)\]$/\1/')"
[ -d "$SDK_ROOT" ] || fail "could not determine the .NET SDK root from 'dotnet --list-sdks'"

CSC_DLL="$(find "$SDK_ROOT" -name csc.dll -path '*bincore*' 2>/dev/null | head -1)"
[ -n "$CSC_DLL" ] || fail "could not locate the Roslyn compiler (csc.dll) under $SDK_ROOT"

# The Unity assemblies target net35, so the BCL references must be .NET
# Framework too - mixing in .NET 8's corelib would duplicate System.Object.
BCL=(-r:"$NETFX_DIR/mscorlib.dll" -r:"$NETFX_DIR/System.dll" -r:"$NETFX_DIR/System.Core.dll")

UNITY_ARGS=()
for dll in "$UNITY_DIR"/*.dll; do UNITY_ARGS+=(-r:"$dll"); done

# CS1701/1702: assembly version unification between net35 and net472 refs.
COMMON=(-nostdlib+ -noconfig -langversion:9.0 -nowarn:1701,1702 -target:library -nologo)

csc() { dotnet "$CSC_DLL" "$@"; }

# All Python in this repository runs in a pinned container; see run.sh.
PYTHON="$REPO/.github/scripts/run.sh"

# ---------------------------------------------------------------------------
log "Compiling Runtime assembly (real UnityEngine references)"
# ---------------------------------------------------------------------------
mapfile -t RUNTIME_SOURCES < <(find "$PACKAGE/Runtime" -name '*.cs' | sort)
[ "${#RUNTIME_SOURCES[@]}" -gt 0 ] || fail "no Runtime sources found under $PACKAGE"

csc "${COMMON[@]}" "${BCL[@]}" "${UNITY_ARGS[@]}" \
    -out:"$OUT/SabaProps.Foliage.Runtime.dll" "${RUNTIME_SOURCES[@]}"
echo "ok: ${#RUNTIME_SOURCES[@]} file(s)"

if [ -d "$TREE_PACKAGE/Runtime" ]; then
    mapfile -t TREE_RUNTIME_SOURCES < <(find "$TREE_PACKAGE/Runtime" -name '*.cs' | sort)
    csc "${COMMON[@]}" "${BCL[@]}" "${UNITY_ARGS[@]}" \
        -r:"$OUT/SabaProps.Foliage.Runtime.dll" \
        -out:"$OUT/SabaProps.Trees.Runtime.dll" "${TREE_RUNTIME_SOURCES[@]}"
    echo "ok: ${#TREE_RUNTIME_SOURCES[@]} tree runtime file(s)"
fi

# ---------------------------------------------------------------------------
log "Compiling UnityEditor stub"
# ---------------------------------------------------------------------------
csc "${COMMON[@]}" "${BCL[@]}" "${UNITY_ARGS[@]}" \
    -out:"$OUT/UnityEditor.dll" "$HERE/UnityEditorStub.cs"
echo "ok"

# ---------------------------------------------------------------------------
log "Compiling Editor assembly (real UnityEngine references + stub)"
# ---------------------------------------------------------------------------
mapfile -t EDITOR_SOURCES < <(find "$PACKAGE/Editor" -name '*.cs' | sort)
[ "${#EDITOR_SOURCES[@]}" -gt 0 ] || fail "no Editor sources found under $PACKAGE"

csc "${COMMON[@]}" "${BCL[@]}" "${UNITY_ARGS[@]}" \
    -r:"$OUT/SabaProps.Foliage.Runtime.dll" -r:"$OUT/UnityEditor.dll" \
    -out:"$OUT/SabaProps.Foliage.Editor.dll" "${EDITOR_SOURCES[@]}"
echo "ok: ${#EDITOR_SOURCES[@]} file(s)"

if [ -d "$TREE_PACKAGE/Editor" ]; then
    mapfile -t TREE_EDITOR_SOURCES < <(find "$TREE_PACKAGE/Editor" -name '*.cs' | sort)
    csc "${COMMON[@]}" "${BCL[@]}" "${UNITY_ARGS[@]}" \
        -r:"$OUT/SabaProps.Foliage.Runtime.dll" \
        -r:"$OUT/SabaProps.Foliage.Editor.dll" \
        -r:"$OUT/SabaProps.Trees.Runtime.dll" \
        -r:"$OUT/UnityEditor.dll" \
        -out:"$OUT/SabaProps.Trees.Editor.dll" "${TREE_EDITOR_SOURCES[@]}"
    echo "ok: ${#TREE_EDITOR_SOURCES[@]} tree editor file(s)"
fi

# ---------------------------------------------------------------------------
log "Compiling the documentation capture tool"
# ---------------------------------------------------------------------------
# .github/figures/capture/ is not shipped, so nothing else would ever compile
# it -- and it is exactly the kind of code that rots quietly, because it is run
# by hand every few releases. Compiled here against the same real UnityEngine
# references as the package.
CAPTURE="$REPO/.github/figures/capture/FoliageDocsCapture.cs"
if [ -f "$CAPTURE" ]; then
    csc "${COMMON[@]}" "${BCL[@]}" "${UNITY_ARGS[@]}" \
        -r:"$OUT/SabaProps.Foliage.Runtime.dll" \
        -r:"$OUT/SabaProps.Foliage.Editor.dll" \
        -r:"$OUT/UnityEditor.dll" \
        -out:"$OUT/SabaProps.Foliage.DocsCapture.dll" "$CAPTURE"
    echo "ok"
else
    echo "skipped: no capture tool"
fi

# ---------------------------------------------------------------------------
log "Compiling CI EditMode tests"
# ---------------------------------------------------------------------------
# These run for real inside Unity via .github/workflows/unity.yml. Compiling
# them here catches typos long before a Unity runner is spun up.
TEST_DIR="$HERE/CIProject/Assets/Tests"
if [ -d "$TEST_DIR" ]; then
    mapfile -t TEST_SOURCES < <(find "$TEST_DIR" -name '*.cs' | sort)
    if [ "${#TEST_SOURCES[@]}" -gt 0 ]; then
        csc "${COMMON[@]}" "${BCL[@]}" "${UNITY_ARGS[@]}" \
            -r:"$OUT/SabaProps.Foliage.Runtime.dll" \
            -r:"$OUT/SabaProps.Foliage.Editor.dll" \
            -r:"$OUT/SabaProps.Trees.Runtime.dll" \
            -r:"$OUT/SabaProps.Trees.Editor.dll" \
            -r:"$OUT/UnityEditor.dll" \
            -out:"$OUT/SabaProps.Foliage.CITests.dll" "${TEST_SOURCES[@]}"
        echo "ok: ${#TEST_SOURCES[@]} file(s)"
    else
        echo "skipped: no test sources"
    fi
else
    echo "skipped: no CI project"
fi

# ---------------------------------------------------------------------------
log "Type-checking shader HLSL"
# ---------------------------------------------------------------------------
SHADER_DIR="$PACKAGE/Runtime/Shaders"
SHADER="$SHADER_DIR/SabaFoliage.shader"
[ -f "$SHADER" ] || fail "shader not found at $SHADER"

"$PYTHON" .github/verify/extract_shader_body.py "$SHADER" "$OUT/shader_body.hlsl"
cp "$HERE/shader_harness.hlsl" "$OUT/shader_harness.hlsl"

# Every combination of the shader's shader_feature keywords.
variants=(
    ""
    "-D_ALPHATEST_ON"
    "-D_DISTANCEFADE_ON"
    "-D_ALPHATEST_ON -D_DISTANCEFADE_ON"
)

for variant in "${variants[@]}"; do
    label="${variant:-(no keywords)}"
    # -o keeps the SPIR-V in the work dir; without it glslang drops vert.spv
    # into the current working directory.
    # shellcheck disable=SC2086
    if glslangValidator -D -e main -S vert --target-env vulkan1.0 \
        -o "$OUT/shader.spv" \
        -I"$SHADER_DIR" -I"$OUT" $variant "$OUT/shader_harness.hlsl" >/dev/null; then
        echo "ok: $label"
    else
        # Re-run without silencing so the error text reaches the log.
        # shellcheck disable=SC2086
        glslangValidator -D -e main -S vert --target-env vulkan1.0 \
            -o "$OUT/shader.spv" \
            -I"$SHADER_DIR" -I"$OUT" $variant "$OUT/shader_harness.hlsl" || true
        fail "shader variant failed: $label"
    fi
done

# ---------------------------------------------------------------------------
log "Running mesh generation (no Unity)"
# ---------------------------------------------------------------------------
# Everything above proves the package compiles. This runs it: the real mesh
# generators against a small runnable stand-in for UnityEngine's maths, so a
# pull request exercises the code that produces geometry rather than only
# parsing it. Structural properties only — see offline/UnityEngineShim.cs for
# why, and .github/workflows/unity.yml for the tier that has real Unity.
OFFLINE="$HERE/offline"
OFFLINE_OUT="$OUT/offline"
mkdir -p "$OFFLINE_OUT"

# Targets the .NET runtime that is present, not net35 like the steps above:
# these assemblies have to execute, and the shim replaces UnityEngine entirely.
# `dotnet --list-runtimes` prints "<name> <version> [<path>]"; the last
# Microsoft.NETCore.App entry is the newest installed shared framework, and its
# assemblies are what this executable both compiles against and runs on.
RUNTIME_DIR="$(dotnet --list-runtimes \
    | awk '/^Microsoft.NETCore.App /{ gsub(/[][]/, "", $3); dir=$3 "/" $2 } END { print dir }')"
[ -d "$RUNTIME_DIR" ] || fail "could not locate a Microsoft.NETCore.App shared framework"

RUNTIME_ARGS=()
for name in System.Runtime System.Private.CoreLib System.Collections System.Console System.Linq; do
    RUNTIME_ARGS+=(-r:"$RUNTIME_DIR/$name.dll")
done

csc -nologo -langversion:9.0 -target:exe -nostdlib+ -noconfig \
    "${RUNTIME_ARGS[@]}" \
    -out:"$OFFLINE_OUT/OfflineMeshTests.dll" \
    "$OFFLINE/UnityEngineShim.cs" \
    "$OFFLINE/OfflineMeshTests.cs" \
    "$PACKAGE/Runtime/FoliageRandom.cs" \
    "$PACKAGE/Runtime/FoliageSeason.cs" \
    "$PACKAGE/Runtime/FoliageSpecies.cs" \
    "$PACKAGE/Editor/FoliageMeshBuffer.cs" \
    "$PACKAGE/Editor/FoliageMeshBuilder.cs" \
    "$PACKAGE/Editor/FoliageSeasonPass.cs"

cat > "$OFFLINE_OUT/OfflineMeshTests.runtimeconfig.json" <<'JSON'
{
  "runtimeOptions": {
    "tfm": "net8.0",
    "framework": { "name": "Microsoft.NETCore.App", "version": "8.0.0" },
    "rollForward": "latestMajor"
  }
}
JSON

dotnet "$OFFLINE_OUT/OfflineMeshTests.dll" || fail "offline mesh checks failed"

# ---------------------------------------------------------------------------
log "Checking the documentation figures"
# ---------------------------------------------------------------------------
# The figures in the documentation are drawn from the output of the generators
# above, and committed. Regenerating them here is what stops the two from
# drifting: a change to a mesh generator that nobody re-rendered fails the pull
# request instead of leaving the documentation showing last month's shapes.
"$REPO/.github/figures/render.sh" --check

# ---------------------------------------------------------------------------
log "Rendering documentation"
# ---------------------------------------------------------------------------
# The docs site is generated from the same Markdown the repository ships, by a
# hand-written converter. Building it here means a document that trips the
# converter fails the pull request rather than the deploy.
# Built into a copy of the site, not into the working tree: the link check
# resolves references to the listing page and the shared stylesheet, so those
# have to be sitting where the deployed site would have them.
rm -rf "$OUT/site"
mkdir -p "$OUT/site"
cp -r "$REPO/Website/." "$OUT/site/"
rm -rf "$OUT/site/docs"

"$PYTHON" .github/scripts/build_docs.py --repo "$REPO" --out "$OUT/site"
"$PYTHON" .github/scripts/check_docs.py --repo "$REPO" --out "$OUT/site"

# ---------------------------------------------------------------------------
log "Validating manifests"
# ---------------------------------------------------------------------------
"$PYTHON" .github/scripts/check_package.py "$REPO" "$PACKAGE"
if [ -d "$TREE_PACKAGE" ]; then
    "$PYTHON" .github/scripts/check_package.py "$REPO" "$TREE_PACKAGE"
fi

log "All checks passed"
