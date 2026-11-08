#!/usr/bin/env bash
# Checks that a published scenetake binary starts on the oldest platform the release supports, from the binary alone:
#
#   linux-x64, linux-arm64             glibc 2.28: no symbol version above GLIBC_2.28
#   linux-musl-x64, linux-musl-arm64   musl: links musl's libc, never glibc
#   osx-x64, osx-arm64                 macOS 12.0: scenetake and minipty_spawn_helper both declare minos 12.0
#
# Each binary must also link only what every installation of its OS has: the C library on Linux, and libraries under
# /usr/lib and /System/Library on macOS. A library from a package manager is a dependency the archive does not carry.
# MiniPty's shim must be linked into the binary, not published beside it, because the archives do not ship it.
#
# Usage: check-binary-floor.sh <rid> <publish-dir>
set -euo pipefail

glibc_floor=2.28
macos_floor=12.0

usage="Usage: check-binary-floor.sh <rid> <publish-dir>"
rid="${1:?$usage}"
dir="${2:?$usage}"

fail() {
  echo "check-binary-floor: $*" >&2
  exit 1
}

# Tool output is captured before it is matched: under pipefail, `readelf | grep -q` fails when grep exits first.
check_linux() {
  local bin="$dir/scenetake" expected header needed lib highest top
  [[ -f "$bin" ]] || fail "$bin not found"
  [[ ! -e "$dir/libminipty_unix.so" ]] || fail "$dir has libminipty_unix.so, so MiniPty's shim was not linked in"

  case "$rid" in
    *-x64) expected="X86-64" ;;
    *-arm64) expected="AArch64" ;;
  esac
  header="$(readelf -h "$bin")"
  [[ "$header" == *"Machine:"*"$expected"* ]] || fail "$bin is not built for $expected"

  needed="$(readelf -d "$bin" | sed -n 's/.*(NEEDED).*\[\(.*\)\]/\1/p')"
  [[ -n "$needed" ]] || fail "$bin links no shared library, so it was not linked against a C library"
  for lib in $needed; do
    case "$rid:$lib" in
      linux-musl-*:libc.musl-*.so.1) ;;
      linux-musl-*:*) fail "$bin links $lib; a musl build links only musl's libc" ;;
      *:libc.so.6 | *:libm.so.6 | *:libdl.so.2 | *:libpthread.so.0 | *:librt.so.1 | *:ld-linux-*.so.*) ;;
      *) fail "$bin links $lib, which is not part of glibc" ;;
    esac
  done
  local libs="${needed//$'\n'/ }"

  case "$rid" in
    linux-musl-*)
      [[ "$needed" == *"libc.musl-"* ]] || fail "$bin does not link musl's libc"
      echo "ok $rid: links only musl's libc ($libs)"
      ;;
    *)
      highest="$(readelf -V "$bin" | grep -o 'Name: GLIBC_[0-9][0-9.]*' | sed 's/Name: GLIBC_//' | sort -uV | tail -1)"
      [[ -n "$highest" ]] || fail "$bin requires no versioned glibc symbol, so it was not linked against glibc"
      top="$(printf '%s\n%s\n' "$glibc_floor" "$highest" | sort -V | tail -1)"
      [[ "$top" == "$glibc_floor" ]] || fail "$bin requires GLIBC_$highest, above the floor $glibc_floor"
      echo "ok $rid: highest glibc version required is $highest (floor $glibc_floor); links $libs"
      ;;
  esac
}

check_macos() {
  local file path minos libs lib
  [[ ! -e "$dir/libminipty_unix.dylib" ]] || fail "$dir has libminipty_unix.dylib, so MiniPty's shim was not linked in"
  for file in scenetake minipty_spawn_helper; do
    path="$dir/$file"
    [[ -f "$path" ]] || fail "$path not found"
    # otool prints one LC_BUILD_VERSION per Mach-O file.
    minos="$(otool -l "$path" | awk '$1 == "minos" { print $2 }' | sort -u | tr '\n' ' ')"
    [[ "$minos" == "$macos_floor " ]] || fail "$file declares minos ${minos:-none}, expected $macos_floor"
    # The first line names the file itself; the rest are the libraries it loads.
    libs="$(otool -L "$path" | tail -n +2 | awk '{ print $1 }')"
    for lib in $libs; do
      case "$lib" in
        /usr/lib/* | /System/Library/*) ;;
        *) fail "$file loads $lib, which macOS does not ship" ;;
      esac
    done
  done
  echo "ok $rid: scenetake and minipty_spawn_helper declare minos $macos_floor and load only system libraries"
}

case "$rid" in
  linux-x64 | linux-arm64 | linux-musl-x64 | linux-musl-arm64) check_linux ;;
  osx-x64 | osx-arm64) check_macos ;;
  *) fail "unsupported runtime identifier: $rid" ;;
esac
