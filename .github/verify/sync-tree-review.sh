#!/usr/bin/env bash
# Publish already-generated verification assets locally; no Unity is launched.
# Run inside the project development container after EditMode tests complete.
set -euo pipefail

repo="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo"
source_dir=.verify/local-unity-trees/Assets/SabaProps/TreesBundledDemo
sample_dir=Packages/io.github.sabas0ba.sabaprops.trees/Samples~/TreesDemo
review_dir=.verify/local-unity-vine

test -f "$source_dir/SeasonalTreesDemo.unity"
test -d "$review_dir/Assets/SabaProps/TreesBundledDemo"
if [ -e "$review_dir/Temp/UnityLockfile" ]; then
  echo 'Close the review project before synchronizing it.' >&2
  exit 1
fi

# Existing references must remain valid before any sample is overwritten.
while IFS= read -r -d '' meta; do
  relative="${meta#"$source_dir/"}"
  if [ ! -f "$sample_dir/$relative" ]; then
    echo "Unexpected generated asset: $relative" >&2
    exit 1
  fi
  expected="$(rg '^guid:' "$sample_dir/$relative" | tr -d '\r')"
  actual="$(rg '^guid:' "$meta" | tr -d '\r')"
  if [ "$expected" != "$actual" ]; then
    echo "GUID mismatch: $relative" >&2
    exit 1
  fi
done < <(find "$source_dir" -type f -name '*.meta' -print0)

cp -a "$source_dir/." "$sample_dir/"
find "$sample_dir" -type f \( -name '*.asset' -o -name '*.unity' \
  -o -name '*.mat' -o -name '*.meta' \) \
  -exec sed -i -e 's/\r$//' -e 's/[[:blank:]]*$//' {} +
cp -a "$sample_dir/." "$review_dir/Assets/SabaProps/TreesBundledDemo/"
cp -a Packages/io.github.sabas0ba.sabaprops.trees/Editor/. \
  "$review_dir/Packages/io.github.sabas0ba.sabaprops.trees/Editor/"
cp -a Packages/io.github.sabas0ba.sabaprops.trees/Runtime/. \
  "$review_dir/Packages/io.github.sabas0ba.sabaprops.trees/Runtime/"
echo 'Tree samples and review project synchronized; existing GUIDs preserved.'
