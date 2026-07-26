#!/usr/bin/env bash
#
# Compile-verify the package without a Unity installation.
#
# What this proves:
#   * the Runtime assembly compiles against REAL UnityEngine reference
#     assemblies (Unity's own UnityEngine.Modules NuGet package)
#   * the Editor assembly compiles, with its UnityEngine usage checked against
#     those same real assemblies
#   * the shader's HLSL type-checks in all four shader_feature combinations
#
# What this does NOT prove:
#   * UnityEditor API signatures. UnityEditor.dll is not redistributable, so
#     `UnityEditorStub.cs` stands in for it and is written by hand.
#   * that Unity's surface shader generator accepts the #pragma configuration,
#     or that the generated variants compile. Only the shader's own code is
#     checked here.
#
# Closing those two gaps needs a real Unity install; see README.md.
#
# Requirements: dotnet SDK 8+, glslangValidator, curl, unzip, python3.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
PACKAGE="${1:-$REPO/Packages/com.sabaprops.foliage}"

WORK="${VERIFY_WORK_DIR:-$REPO/.verify}"
REFS="$WORK/refs"
OUT="$WORK/out"

UNITY_REFS_VERSION="2021.3.33"
NETFX_REFS_VERSION="1.0.3"

mkdir -p "$REFS" "$OUT"

log() { printf '\n\033[1m== %s\033[0m\n' "$1"; }
fail() { printf '\033[31merror: %s\033[0m\n' "$1" >&2; exit 1; }

for tool in dotnet curl unzip python3 glslangValidator; do
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

# ---------------------------------------------------------------------------
log "Compiling Runtime assembly (real UnityEngine references)"
# ---------------------------------------------------------------------------
mapfile -t RUNTIME_SOURCES < <(find "$PACKAGE/Runtime" -name '*.cs' | sort)
[ "${#RUNTIME_SOURCES[@]}" -gt 0 ] || fail "no Runtime sources found under $PACKAGE"

csc "${COMMON[@]}" "${BCL[@]}" "${UNITY_ARGS[@]}" \
    -out:"$OUT/SabaProps.Foliage.Runtime.dll" "${RUNTIME_SOURCES[@]}"
echo "ok: ${#RUNTIME_SOURCES[@]} file(s)"

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

python3 "$HERE/extract_shader_body.py" "$SHADER" "$OUT/shader_body.hlsl"
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
log "Validating manifests"
# ---------------------------------------------------------------------------
python3 - "$REPO" "$PACKAGE" <<'PY'
import json
import os
import sys

repo, package = sys.argv[1], sys.argv[2]
problems = []

manifest_path = os.path.join(package, "package.json")
with open(manifest_path, encoding="utf-8") as handle:
    manifest = json.load(handle)

for field in ("name", "displayName", "version", "unity", "description"):
    if not manifest.get(field):
        problems.append(f"package.json is missing '{field}'")

package_id = os.path.basename(package.rstrip("/"))
if manifest.get("name") != package_id:
    problems.append(f"package.json name '{manifest.get('name')}' != folder '{package_id}'")

version = manifest.get("version", "")
if version and not all(part.isdigit() for part in version.split("-")[0].split(".")):
    problems.append(f"version '{version}' is not numeric dotted form")

changelog = os.path.join(package, "CHANGELOG.md")
if os.path.exists(changelog):
    with open(changelog, encoding="utf-8") as handle:
        if f"[{version}]" not in handle.read():
            problems.append(f"CHANGELOG.md has no entry for version {version}")

with open(os.path.join(repo, "source.json"), encoding="utf-8") as handle:
    source = json.load(handle)

if manifest.get("name") not in (source.get("packages") or []):
    problems.append(f"{manifest.get('name')} is not listed in source.json packages")

# Every non-tilde file Unity imports needs a .meta, or GUIDs churn per install.
missing = []
for root, dirs, files in os.walk(package):
    dirs[:] = [d for d in dirs if not d.endswith("~") and not d.startswith(".")]
    for name in dirs:
        target = os.path.join(root, name)
        if not os.path.exists(target + ".meta"):
            missing.append(os.path.relpath(target, repo))
    for name in files:
        if name.endswith(".meta") or name.startswith("."):
            continue
        if os.path.splitext(name)[1].lower() not in (".cs", ".asmdef", ".shader", ".cginc", ".hlsl", ".json", ".md"):
            continue
        target = os.path.join(root, name)
        if not os.path.exists(target + ".meta"):
            missing.append(os.path.relpath(target, repo))

problems.extend(f"missing .meta for {path}" for path in sorted(missing))

if problems:
    for problem in problems:
        print(f"error: {problem}", file=sys.stderr)
    sys.exit(1)

print(f"ok: {manifest['name']} {version}")
PY

log "All checks passed"
