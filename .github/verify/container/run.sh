#!/usr/bin/env bash
set -euo pipefail

# dotfiles の開発シェルを入口に保ち、検証に必要な追加ツールだけを優先する。
export PATH="/opt/sabaprops/tools/bin:$PATH"
export SABAPROPS_PYTHON_INTERPRETER=/opt/sabaprops/tools/bin/python3

cd /repo
export DOTNET_CLI_HOME=/repo/.verify/dotnet-home
mkdir -p "$DOTNET_CLI_HOME"

# 取得済みアーカイブを再利用し、展開前にリポジトリの固定ハッシュを検証する。
mkdir -p .verify/vpm
while read -r name version sha url; do
  case "$name" in
    '' | \#*) continue ;;
  esac
  archive=".verify/vpm/$name-$version.zip"
  if [ ! -f "$archive" ]; then
    curl --fail --location --retry 3 -o "$archive.part" "$url"
    mv "$archive.part" "$archive"
  fi
  printf '%s  %s\n' "$sha" "$archive" | sha256sum --check
  mkdir -p ".verify/vpm/$name"
  unzip -q -o "$archive" -d ".verify/vpm/$name"
done <.github/verify/vrchat/packages.lock

# Windows から archive として搬入した場合も実行属性を揃える。
find .github -name '*.sh' -exec chmod u+x {} +
exec bash .github/verify/verify.sh
