#!/usr/bin/env bash
set -euo pipefail

# Stages every package for a maintainer to approve with 2FA.
# Staged publishing works only for a package that is already on the registry. A package that is not there yet
# is packed into <first-publish-dir> instead, so the maintainer can publish that tarball once by hand.
# The tarball is packed here, on Linux, because a package assembled on Windows loses the binary's executable bit.

if [ "$#" -lt 1 ] || [ "$#" -gt 2 ]; then
  echo "Usage: $0 <packages-dir> [first-publish-dir]" >&2
  exit 1
fi

packages_dir="$1"
first_publish_dir="${2:-${packages_dir%/}-first-publish}"
platform_packages=(
  linux-x64
  linux-arm64
  linux-x64-musl
  linux-arm64-musl
  darwin-x64
  darwin-arm64
  win32-x64
  win32-arm64
)
not_on_registry=()

# Returns 0 when the registry has the package and 1 when it answers 404.
# Any other failure stops the script, because skipping the package then would hide it from the release.
package_exists() {
  local name="$1"
  local output
  if output="$(npm view "$name" name 2>&1)"; then
    return 0
  fi
  if grep -q 'E404' <<<"$output"; then
    return 1
  fi
  printf '%s\n' "$output" >&2
  echo "Error: could not check whether $name is on the registry" >&2
  exit 1
}

stage_package() {
  local name="$1"
  local dir="$2"
  if ! package_exists "$name"; then
    echo "::warning::$name is not on the registry, so it cannot be staged. Packed it into $first_publish_dir for a first publish by hand."
    mkdir -p "$first_publish_dir"
    (
      cd "$dir"
      npm pack --pack-destination "$first_publish_dir"
    )
    not_on_registry+=("$name")
    return
  fi
  echo "Staging $name..."
  (
    cd "$dir"
    npm stage publish --access public --provenance
  )
}

for platform in "${platform_packages[@]}"; do
  stage_package "@scenetake/${platform}" "$packages_dir/$platform"
done

stage_package scenetake "$packages_dir/scenetake"

echo "Staged packages:"
npm stage list --json || true

if [ "${#not_on_registry[@]}" -gt 0 ]; then
  echo "Not staged, because they are not on the registry: ${not_on_registry[*]}"
  echo "Publish each tarball in $first_publish_dir with 'npm publish --access public <tarball>' before you approve scenetake."
fi
