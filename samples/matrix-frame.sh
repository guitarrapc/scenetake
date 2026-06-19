#!/usr/bin/env bash
# Print one deterministic "matrix rain" screen for samples/matrix.yaml.
# Green columns with a bright-white head and green trail — exercises SVG matrix rain tint.
set -euo pipefail

frame="${1:-0}"
width=80
height=20

for ((row = 0; row < height; row++)); do
  for ((col = 0; col < width; col++)); do
    head=$(((frame * 2 + col * 3) % height))
    if ((row == head)); then
      ch=$((65 + (col + frame) % 26))
      printf '\033[37m%c' "$ch"
    elif ((row > head && row - head < 12)); then
      ch=$((65 + (row + col + frame) % 26))
      printf '\033[32m%c' "$ch"
    else
      printf ' '
    fi
  done
  printf '\033[0m\n'
done
