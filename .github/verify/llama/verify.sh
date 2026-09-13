#!/usr/bin/env bash
# Uses the existing Verify job's .NET SDK and glslang; no package restore.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"
PACKAGE="$REPO/Packages/io.github.sabas0ba.sabaprops.llama"
WORK="$REPO/.verify/llama"
mkdir -p "$WORK"
export DOTNET_CLI_HOME="$WORK/dotnet-home"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
for tool in dotnet glslangValidator; do
    if ! command -v "$tool" >/dev/null 2>&1; then
        echo "error: $tool is required" >&2
        exit 1
    fi
done
SDK_ROOT="$(dotnet --list-sdks | tail -1 | sed -E 's/^[^ ]+ \[(.*)\]$/\1/')"
CSC="$(find "$SDK_ROOT" -path '*/bincore/csc.dll' -print -quit)"
RUNTIME="$(dotnet --list-runtimes | awk '/^Microsoft.NETCore.App / { gsub(/[][]/, "", $3); dir=$3 "/" $2 } END { print dir }')"
test -f "$CSC"
test -d "$RUNTIME"
REFERENCES=()
for dll in "$RUNTIME"/*.dll; do REFERENCES+=(-r:"$dll"); done
dotnet "$CSC" -nologo -noconfig -nostdlib+ -langversion:9.0 -target:exe \
    "${REFERENCES[@]}" -out:"$WORK/CoreTests.dll" \
    "$PACKAGE/Editor/Core/"*.cs "$HERE/CoreTests.cs" "$HERE/GgufTests.cs"
cat > "$WORK/CoreTests.runtimeconfig.json" <<'JSON'
{
  "runtimeOptions": {
    "tfm": "net8.0",
    "framework": { "name": "Microsoft.NETCore.App", "version": "8.0.0" },
    "rollForward": "latestMajor"
  }
}
JSON
dotnet "$WORK/CoreTests.dll" "$@"
glslangValidator -D -e main -S frag --target-env vulkan1.0 \
    -I"$PACKAGE/Shaders" -o "$WORK/inference.spv" "$HERE/shader_harness.hlsl"
echo "Llama fragment HLSL: type-check passed (not a GPU numerical test)."
