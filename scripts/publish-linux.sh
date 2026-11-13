#!/usr/bin/env bash
# Publishes scenetake as a NativeAOT binary for a Linux runtime identifier inside a container that has the oldest C
# library the release supports, then checks the binary and runs smoke-binary.sh in that same container:
#
#   linux-x64, linux-arm64             AlmaLinux 8, glibc 2.28: the oldest glibc .NET 10 supports (RHEL 8)
#   linux-musl-x64, linux-musl-arm64   Alpine 3.17, musl 1.2.3: the floor MiniPty builds its musl files at
#
# The linker binds each glibc symbol to the newest version that the glibc it links against has. Linked on the runner
# (Ubuntu 24.04, glibc 2.39), the binary needed GLIBC_2.38 and did not start on RHEL 8 or Debian 12. Linked against
# the oldest glibc, every symbol binds to a version that glibc already has.
#
# The images have no ICU, so the binary's start there also shows it needs none (InvariantGlobalization). The SDK runs
# invariant there for the same reason.
#
# The container is of the host's own architecture, never emulated. Files go in and out with `docker cp`, so the build
# needs no bind mount and the binary is owned by the caller.
#
# Usage: publish-linux.sh <rid> <output-dir>
set -euo pipefail

usage="Usage: publish-linux.sh <linux-x64|linux-arm64|linux-musl-x64|linux-musl-arm64> <output-dir>"
rid="${1:?$usage}"
out_dir="${2:?$usage}"
root="$(cd "$(dirname "$0")/.." && pwd)"

glibc_image="almalinux:8@sha256:2d0f2eb2a83cc09dbc2eb92fade4dbe8f709628149e81aa00aec70630406603b"
musl_image="alpine:3.17@sha256:8fc3dacfb6d69da8d44e42390de777e48577085db99aa4e4af35f483eb08b989"

fail() {
  echo "publish-linux: $*" >&2
  exit 1
}

# Each setup installs clang for the NativeAOT link, binutils for readelf, what dotnet-install.sh and the SDK need,
# bash and curl for the smoke scenarios, and a font, without which mp4 rendering stops before it starts ffmpeg.
case "$rid" in
  linux-x64 | linux-arm64)
    image="$glibc_image"
    setup="dnf install -y -q clang binutils tar gzip findutils dejavu-sans-mono-fonts"
    ;;
  linux-musl-x64 | linux-musl-arm64)
    image="$musl_image"
    setup="apk add --no-cache bash curl ca-certificates libgcc libstdc++ libintl krb5-libs libssl3 zlib clang build-base binutils font-dejavu"
    ;;
  *)
    fail "unsupported runtime identifier: $rid"
    ;;
esac

daemon_arch="$(docker version --format '{{.Server.Arch}}')"
case "$rid:$daemon_arch" in
  *-x64:amd64 | *-arm64:arm64) ;;
  *) fail "$rid must be built on a Docker host of its own architecture, not under emulation (this one is $daemon_arch)" ;;
esac

stage="$(mktemp -d)"
cid=""
cleanup() {
  [[ -n "$cid" ]] && docker rm -f "$cid" >/dev/null 2>&1 || true
  rm -rf "$stage"
}
trap cleanup EXIT

# The app and its includes, the props it builds with, and what the smoke reads.
(
  cd "$root"
  cp ./*.cs Directory.Build.props README.md .editorconfig "$stage/"
  cp -R samples tests scripts "$stage/"
)

cid="$(docker create "$image" sh -c "
  set -e
  $setup
  # AlmaLinux 8 has curl 7.61: --retry-all-errors needs 7.71, and --retry alone does not retry error 52 (empty reply).
  # Retry the download explicitly, with a fresh output file on each attempt, and never execute a failed download.
  attempt=1
  until curl -sSfL --connect-timeout 30 --max-time 120 https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh; do
    if [ \"\$attempt\" -ge 5 ]; then
      echo 'publish-linux: failed to download dotnet-install.sh after 5 attempts' >&2
      exit 1
    fi
    echo \"publish-linux: dotnet-install.sh download failed (attempt \$attempt/5); retrying\" >&2
    sleep \"\$((attempt * 2))\"
    attempt=\$((attempt + 1))
  done
  bash /tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet
  export PATH=/usr/share/dotnet:\$PATH DOTNET_ROOT=/usr/share/dotnet DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
  export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
  cd /src
  dotnet publish scenetake.cs -c Release -r '$rid' --self-contained true -p:PublishAot=true -p:StripSymbols=true -p:DebugType=None -o /out
  bash scripts/check-binary-floor.sh '$rid' /out
  if find /usr /lib -name 'libicuuc.so*' 2>/dev/null | grep -q .; then
    echo 'publish-linux: the image has ICU, so a start there does not show that scenetake needs none' >&2
    exit 1
  fi
  env -u DOTNET_SYSTEM_GLOBALIZATION_INVARIANT /out/scenetake --version
  bash scripts/smoke-binary.sh /out/scenetake
")"
docker cp "$stage/." "$cid:/src"
docker start -a "$cid"

mkdir -p "$out_dir"
docker cp "$cid:/out/scenetake" "$out_dir/scenetake"
echo "Published $rid to $out_dir/scenetake"
