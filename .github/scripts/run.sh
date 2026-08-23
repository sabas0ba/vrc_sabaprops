#!/usr/bin/env bash
#
# Runs one of this repository's Python scripts inside a container.
#
# The scripts use nothing but the standard library, so what this buys is not
# dependency isolation but a fixed interpreter: the same Python builds the
# listing and the docs on a maintainer's laptop, on a CI runner, and in a year.
# There is deliberately no fallback to whatever `python3` happens to be on the
# host — a silent fallback is how the reproducibility gets lost.
#
# Usage: run.sh <script> [args...]
#
#   .github/scripts/run.sh .github/scripts/build_docs.py --out Website
#
# The repository is mounted at /repo and that is the working directory, so
# repo-relative arguments work unchanged. Absolute paths that point inside the
# repository are rewritten to their container equivalent, which keeps callers
# that build paths from $PWD working; anything outside the repository is not
# visible and the script will say so rather than fail obscurely.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"

# Pinned by digest, not by tag: python:3.12-slim as of 2026-08-23.
IMAGE="docker.io/library/python@sha256:2c941e860699f878900b0edc2403613c234d4b32eda3cc9fa7036991a2a63c4a"

if [ "$#" -lt 1 ]; then
    echo "usage: run.sh <script> [args...]" >&2
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
# which would mangle the container-side mount point.
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

# On Linux the container would otherwise write root-owned files into the work
# tree. Windows engines map the user themselves, and passing a Windows uid
# through would only confuse them.
case "$(uname -s)" in
    MINGW* | MSYS* | CYGWIN*) ;;
    *)
        if command -v id >/dev/null 2>&1; then
            RUN_ARGS+=(--user "$(id -u):$(id -g)")
        fi
        ;;
esac

# Forwarded so build_listing.py can authenticate against the GitHub API. Passed
# by name, so the value never appears in the command line or the logs.
if [ -n "${GITHUB_TOKEN:-}" ]; then
    RUN_ARGS+=(-e GITHUB_TOKEN)
fi

# Absolute paths into the repository become their container equivalent. A
# caller that assembled a path from $PWD would otherwise hand the container a
# location it cannot see.
ARGS=()
for arg in "$@"; do
    case "$arg" in
        "$REPO")
            ARGS+=("/repo")
            ;;
        "$REPO"/*)
            ARGS+=("/repo/${arg#"$REPO"/}")
            ;;
        /* | [A-Za-z]:[\\/]*)
            echo "error: '$arg' is outside the repository, so the container cannot see it" >&2
            exit 1
            ;;
        *)
            ARGS+=("$arg")
            ;;
    esac
done

exec "$ENGINE" run "${RUN_ARGS[@]}" "$IMAGE" python3 "${ARGS[@]}"
