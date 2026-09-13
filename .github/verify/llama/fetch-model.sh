#!/usr/bin/env bash
# Downloads only fixed test data, never executes code from the model repository.
set -euo pipefail
REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
DEST="$REPO/.verify/llama/models"
case "${1:-stories260K}" in
  stories260K) FILE=stories260K.gguf; HASH=270cba1bd5109f42d03350f60406024560464db173c0e387d91f0426d3bd256d ;;
  stories15M-q4_0) FILE=stories15M-q4_0.gguf; HASH=66967fbece6dbe97886593fdbb73589584927e29119ec31f08090732d1861739 ;;
  *) echo 'Choose stories260K or stories15M-q4_0' >&2; exit 1 ;;
esac
REV=499bc8821c6b12b4e53c5bffcb21ec206f212d81
mkdir -p "$DEST"
if ! test -f "$DEST/$FILE" || ! echo "$HASH  $DEST/$FILE" | sha256sum --check --status; then
  TMP="$(mktemp "$DEST/download.XXXXXX")"
  trap 'rm -f "$TMP"' EXIT
  curl --fail --location --retry 2 --connect-timeout 20 --max-time 180 \
    "https://huggingface.co/ggml-org/models-moved/resolve/$REV/tinyllamas/$FILE" --output "$TMP"
  echo "$HASH  $TMP" | sha256sum --check
  mv "$TMP" "$DEST/$FILE"
fi
echo "$HASH  $DEST/$FILE" | sha256sum --check
