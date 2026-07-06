#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ]; then
  echo "Usage: $0 <packages-dir>" >&2
  exit 1
fi

packages_dir="$1"
platform_packages=(
  linux-x64
  linux-arm64
  darwin-x64
  darwin-arm64
  win32-x64
  win32-arm64
)

for platform in "${platform_packages[@]}"; do
  echo "Staging @scenetake/${platform}..."
  (
    cd "$packages_dir/$platform"
    npm stage publish --access public --provenance
  )
done

echo "Staging scenetake..."
(
  cd "$packages_dir/scenetake"
  npm stage publish --access public --provenance
)

echo "Staged packages:"
npm stage list @scenetake --json || true
npm stage list scenetake --json
