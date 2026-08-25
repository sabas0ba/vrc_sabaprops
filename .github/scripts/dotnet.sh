#!/usr/bin/env bash
#
# Runs a command inside a pinned .NET SDK container.
#
# The companion to run.sh, which does the same for this repository's Python.
# Between them, the only tool a contributor needs on the host to regenerate the
# documentation figures is a container engine.
#
# Usage: dotnet.sh <command> [args...]
#
#   .github/scripts/dotnet.sh dotnet --version
#   .github/scripts/dotnet.sh bash /repo/.github/figures/dump.sh /repo /repo/.verify/figures
#
# The repository is mounted at /repo and that is the working directory.
# Arguments are passed through untouched, so they must already be paths as the
# container sees them -- unlike run.sh, which rewrites them, because the
# commands run here take paths inside compiler argument strings where a
# rewriting pass would have to understand the argument syntax to be correct.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"

# Pinned by digest, not by tag: mcr.microsoft.com/dotnet/sdk:8.0 as of
# 2026-08-25, which is SDK 8.0.424. The version matters — verify.sh compiles
# with the Roslyn that ships inside this SDK, and a floating tag would change
# the compiler under a check whose whole purpose is to be reproducible.
IMAGE="mcr.microsoft.com/dotnet/sdk@sha256:237133a0ea20cffcfaa92588e1c8a56d58fe99f44da72dcff35aeb017119abcf"

if [ "$#" -lt 1 ]; then
    echo "usage: dotnet.sh <command> [args...]" >&2
    exit 2
fi

ENGINE="${CONTAINER_ENGINE:-}"
if [ -z "$ENGINE" ]; then
    for candidate in podman docker; do
        if command -v "$candidate" >/dev/null 2>&1; then
            ENGINE="$candidate"
            break
        fi
    done
fi

if [ -z "$ENGINE" ]; then
    echo "error: neither podman nor docker was found; set CONTAINER_ENGINE" >&2
    exit 1
fi

# Git Bash rewrites arguments that look like absolute paths into Windows ones,
# which would mangle both the mount point and every /repo path passed through.
export MSYS2_ARG_CONV_EXCL='*'
export MSYS_NO_PATHCONV=1

host_path() {
    if command -v cygpath >/dev/null 2>&1; then
        cygpath -w "$1"
    else
        printf '%s' "$1"
    fi
}

RUN_ARGS=(--rm -v "$(host_path "$REPO"):/repo" -w /repo)

# Nothing compiled here reaches the network: the reference assemblies come from
# the work directory verify.sh already populated, and the sources are the
# repository's own. Denying the socket keeps it that way.
if [ "${CONTAINER_NETWORK:-none}" = "none" ]; then
    RUN_ARGS+=(--network=none)
fi

# The SDK writes a first-run marker and a NuGet cache under $HOME, and $HOME in
# this image is /root, which is not writable when the container is not running
# as root. Pointing them at /tmp costs a cold NuGet cache per run and nothing
# else -- the compile takes no packages.
RUN_ARGS+=(
    -e DOTNET_CLI_HOME=/tmp
    -e DOTNET_CLI_TELEMETRY_OPTOUT=1
    -e DOTNET_NOLOGO=1
    -e DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
    -e XDG_DATA_HOME=/tmp
)

# Rootless podman already maps container root onto the invoking user, and
# passing --user actively breaks it: the uid is mapped again into the user
# namespace and lands on a subuid that owns none of the mounted files. Docker
# runs as real root, so it has to be told. Same reasoning as run.sh.
if [ "$ENGINE" = "docker" ] && command -v id >/dev/null 2>&1; then
    case "$(uname -s)" in
        MINGW* | MSYS* | CYGWIN*) ;;
        *) RUN_ARGS+=(--user "$(id -u):$(id -g)") ;;
    esac
fi

exec "$ENGINE" run "${RUN_ARGS[@]}" "$IMAGE" "$@"
