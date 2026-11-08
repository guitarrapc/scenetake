#!/usr/bin/env bash
# Checks that the npm wrapper starts the Linux package that matches the host's C library: the glibc package on this
# host, and the musl package in an Alpine container. Both packages are installed, as an npm that ignores `libc`
# installs them, and each holds a script in place of the binary that prints the package's name.
#
# Usage: test-wrapper.sh   (on a glibc Linux host with node and docker)
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
node_alpine_image="node:22-alpine@sha256:0e1754758c2df4cfa3e68d59e2e1b7374bb7fd9ea6328eaaacf15cc934af8202"

fail() {
  echo "test-wrapper: $*" >&2
  exit 1
}

[[ "$(uname -s)" == Linux ]] || fail "run this on Linux"
case "$(uname -m)" in
  x86_64) arch=x64 ;;
  aarch64) arch=arm64 ;;
  *) fail "no package for $(uname -m)" ;;
esac

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
modules="$tmp/node_modules"

mkdir -p "$modules/scenetake"
cp -R "$root/packages/scenetake/bin" "$root/packages/scenetake/package.json" "$modules/scenetake/"
for pkg in "linux-$arch" "linux-$arch-musl"; do
  dir="$modules/@scenetake/$pkg"
  mkdir -p "$dir"
  cp "$root/packages/$pkg/package.json" "$dir/"
  printf '#!/bin/sh\necho %s\n' "$pkg" > "$dir/scenetake"
  chmod +x "$dir/scenetake"
done

expect() {
  local where="$1" want="$2" got="$3"
  [[ "$got" == "$want" ]] || fail "on $where the wrapper started '$got', expected '$want'"
  echo "ok $where: started $got"
}

expect "this host" "linux-$arch" "$(node "$modules/scenetake/bin/scenetake.js")"
expect "Alpine" "linux-$arch-musl" \
  "$(docker run --rm -v "$tmp:/w:ro" "$node_alpine_image" node /w/node_modules/scenetake/bin/scenetake.js)"
