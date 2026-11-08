#!/usr/bin/env bash
# Runs the checks that need a published scenetake binary: it records a scenario to cast and SVG, renders SVG from cast
# files, and runs the integration tests that start it (SCENETAKE_BIN). The tests that need only the sources run in the
# workflow instead.
#
# CI runs it on every runner that publishes a binary, inside the containers that build the Linux binaries at their
# C library floors (publish-linux.sh), and on the oldest hosted macOS. It needs bash, curl, and the .NET 10 SDK.
# An integration test skips a case whose tool (matrix, ffmpeg, pwsh) is not on PATH.
#
# Output goes to smoke/ beside the binary. The release packages name their files, so it never reaches an archive.
#
# Usage: smoke-binary.sh <scenetake-binary>
set -euo pipefail

bin="${1:?Usage: smoke-binary.sh <scenetake-binary>}"
root="$(cd "$(dirname "$0")/.." && pwd)"

[[ -f "$bin" ]] || { echo "smoke-binary: $bin not found" >&2; exit 1; }

# Scenario and fixture paths are relative to the repository root, as they are in the tests.
cd "$root"
work="$(dirname "$bin")/smoke"
rm -rf "$work"
mkdir -p "$work"

"$bin" --version

"$bin" --format svg samples/basic.yaml "$work/basic"
test -f "$work/basic.cast"
test -f "$work/basic.svg"

"$bin" svg "$work/basic.cast" "$work/basic-from-cast.svg"
test -f "$work/basic-from-cast.svg"

"$bin" svg samples/highlight.cast "$work/highlight.svg"
test -f "$work/highlight.svg"

for fixture in tests/fixtures/*.cast; do
  stem="$(basename "$fixture" .cast)"
  "$bin" svg "$fixture" "$work/fixture-$stem.svg"
  test -f "$work/fixture-$stem.svg"
done

export SCENETAKE_BIN="$bin"
dotnet run tests/stream_output_test.cs
dotnet run tests/pty_test.cs
dotnet run tests/interactive_pty_test.cs
# Runner images and the containers ship no ffmpeg, so these skip real encodes. The mp4 job runs them with ffmpeg.
dotnet run tests/scenario_render_test.cs
dotnet run tests/mp4_ffmpeg_test.cs
