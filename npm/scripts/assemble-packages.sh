#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ]; then
  echo "Usage: $0 <release-assets-dir>" >&2
  exit 1
fi

assets_dir="$1"
root_dir="$(cd "$(dirname "$0")/.." && pwd)"
packages_dir="$root_dir/packages"

extract_linux() {
  local archive="$1"
  local dest="$2"
  rm -rf "$dest"
  mkdir -p "$dest"
  tar -xzf "$archive" -C "$dest"
  chmod +x "$dest/scenetake"
}

extract_darwin() {
  local archive="$1"
  local dest="$2"
  rm -rf "$dest"
  mkdir -p "$dest"
  tar -xzf "$archive" -C "$dest"
  chmod +x "$dest/scenetake" "$dest/minipty_spawn_helper"
}

extract_windows() {
  local archive="$1"
  local dest="$2"
  rm -rf "$dest"
  mkdir -p "$dest"
  unzip -o "$archive" -d "$dest"
}

copy_package_json() {
  local platform="$1"
  local dest="$2"
  cp "$packages_dir/$platform/package.json" "$dest/package.json"
}

if [ -n "${RUNNER_TEMP:-}" ]; then
  stage_dir="${RUNNER_TEMP}/scenetake-npm-packages"
else
  stage_dir="$(mktemp -d -t scenetake-npm-packages.XXXXXX)"
fi
rm -rf "$stage_dir"
mkdir -p "$stage_dir"

extract_linux "$assets_dir/scenetake-linux-amd64.tar.gz" "$stage_dir/linux-x64"
copy_package_json linux-x64 "$stage_dir/linux-x64"

extract_linux "$assets_dir/scenetake-linux-arm64.tar.gz" "$stage_dir/linux-arm64"
copy_package_json linux-arm64 "$stage_dir/linux-arm64"

extract_darwin "$assets_dir/scenetake-osx-amd64.tar.gz" "$stage_dir/darwin-x64"
copy_package_json darwin-x64 "$stage_dir/darwin-x64"

extract_darwin "$assets_dir/scenetake-osx-arm64.tar.gz" "$stage_dir/darwin-arm64"
copy_package_json darwin-arm64 "$stage_dir/darwin-arm64"

extract_windows "$assets_dir/scenetake-win-amd64.zip" "$stage_dir/win32-x64"
copy_package_json win32-x64 "$stage_dir/win32-x64"

extract_windows "$assets_dir/scenetake-win-arm64.zip" "$stage_dir/win32-arm64"
copy_package_json win32-arm64 "$stage_dir/win32-arm64"

mkdir -p "$stage_dir/scenetake"
if [ ! -f "$packages_dir/scenetake/bin/scenetake.js" ]; then
  echo "Error: missing $packages_dir/scenetake/bin/scenetake.js" >&2
  exit 1
fi
cp "$packages_dir/scenetake/package.json" "$stage_dir/scenetake/package.json"
cp "$packages_dir/scenetake/README.md" "$stage_dir/scenetake/README.md"
cp -r "$packages_dir/scenetake/bin" "$stage_dir/scenetake/bin"

echo "$stage_dir"
