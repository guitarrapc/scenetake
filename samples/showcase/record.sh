#!/usr/bin/env bash
# Records the showcase samples in a Linux container that has the tools they run (see Dockerfile), with the outputs
# file that the playground replays. Run it on Linux or in WSL with Docker.
#
# Usage: bash samples/showcase/record.sh <folder with a linux-x64 scenetake> [name...]
#   bash samples/showcase/record.sh ~/scenetake-linux            # every samples/showcase/*.yaml
#   bash samples/showcase/record.sh ~/scenetake-linux fetch sl   # only fetch.yaml and sl.yaml
set -euo pipefail

here=$(cd "$(dirname "$0")" && pwd)
bin=$(cd "${1:?usage: record.sh <folder with a linux-x64 scenetake> [name...]}" && pwd)
shift

if [ $# -eq 0 ]; then
  for scenario in "$here"/*.yaml; do
    name=${scenario##*/}
    set -- "$@" "${name%.yaml}"
  done
fi

# The legacy builder needs no buildx context, which a broken Docker context file can block.
DOCKER_BUILDKIT=0 docker build --quiet --tag scenetake-showcase "$here" > /dev/null

for name in "$@"; do
  echo "=== $name" >&2
  # The fixed hostname is what fastfetch shows in its title. The commands run in /home/demo/app, the image's project.
  docker run --rm --hostname scenetake \
    --volume "$bin:/opt/scenetake:ro" \
    --volume "$here:/showcase" \
    --env SCENETAKE_OUTPUTS="/showcase/$name.outputs.json" \
    scenetake-showcase \
    /opt/scenetake/scenetake --format svg "/showcase/$name.yaml" "/showcase/$name"
done
