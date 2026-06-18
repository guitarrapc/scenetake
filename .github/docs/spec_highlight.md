# Coloring Specification

Status: **Implemented**

## Motivation

scenario2cast executes commands and records terminal output into cast events. In non-TTY execution, many CLI tools stop emitting ANSI colors, so demos can look flat even when rendered by [agg](https://docs.asciinema.org/manual/agg/).

This spec defines declarative coloring controls that keep demos readable without depending on each tool's TTY/color options.

## Scope

### In scope (v1)

- `highlight` for command output on map-form `run` steps.
- `run-highlight` for typed command text on map-form `run` steps.
- `stderr-color` fallback coloring for stderr text that has no ANSI SGR.
- Colorized `name` comment lines with optional `[style]` prefix.
- Shared 16-color foreground palette (normal + bright).
- ANSI 256-color palette indices via `fg:<0-255>`, `bg:<0-255>`, and `38;5;n` / `48;5;n` SGR literals.
- True-color RGB via `fg:#rrggbb`, `bg:#rrggbb`, `fg:#rgb`, `bg:#rgb`, `fg:r,g,b`, `bg:r,g,b`, and `38;2;r;g;b` / `48;2;r;g;b` SGR literals.
- Style-capable color values: `bold`, `underline`, `background`, `bright` combinations.
- Direct ANSI SGR literal input (for example `1;31`, `\e[1;31m`).
- Range-based output targeting via `at`.
- Warning-and-continue behavior for invalid values and out-of-range targets.

### Out of scope (v1)

- String-form step coloring defaults (for example `- echo "foo"`).
- `settings` defaults for `highlight` ranges.
- Regex selectors.
- Terminal theme in the cast header (`term.theme`). See [spec_scenario.md](spec_scenario.md) `render.theme` and [spec_cast.md](spec_cast.md).
- Relative indices (for example `-1` = last line).

## Coloring Targets

Coloring is split by target type so behavior is explicit and predictable.

| Target | Key | Scope | Applies to |
|---|---|---|---|
| Command output | `highlight` | step | stdout/stderr combined output text for that step |
| Typed command | `run-highlight` | step | Simulated typed command characters |
| stderr fallback | `stderr-color` | settings + step | stderr text that has no ANSI SGR |
| Step comment | `name` with optional `[style]` prefix | step | `# ...` comment line shown before typing |

## Shared Named Colors

All coloring keys can use the same named foreground palette. Named colors map to 16-color ANSI SGR values (normal + bright).

| Name | ANSI SGR |
|---|---|
| `black` | `30` |
| `red` | `31` |
| `green` | `32` |
| `yellow` | `33` |
| `blue` | `34` |
| `magenta` | `35` |
| `cyan` | `36` |
| `white` | `37` |
| `bright-black` (`gray`, `grey`) | `90` |
| `bright-red` | `91` |
| `bright-green` | `92` |
| `bright-yellow` | `93` |
| `bright-blue` | `94` |
| `bright-magenta` | `95` |
| `bright-cyan` | `96` |
| `bright-white` | `97` |

Applied regions are wrapped with open SGR and `\u001b[0m` reset to avoid bleed.

## Style Value Formats

Color-capable keys (`highlight[].color`, `run-highlight`, `stderr-color`, `name` prefix) accept a style string.

Accepted forms:

- Named foreground color: `red`, `bright-cyan` (within the 16-color palette)
- Token composition (space/comma/`+` separated):
  - Style tokens: `bold`, `underline`, `bright`
  - Foreground tokens: `<name>`, `fg:<name>`, `fg:<0-255>`, `fg:#rrggbb`, `fg:#rgb`, `fg:r,g,b`
  - Background tokens: `bg:<name>`, `bg:<0-255>`, `bg:#rrggbb`, `bg:#rgb`, `bg:r,g,b`
- Raw ANSI SGR literal:
  - `1;31`
  - `38;5;196`, `48;5;235` (ANSI 256-color palette)
  - `38;2;255;140;0`, `48;2;40;40;40` (ANSI true color)
  - `\e[1;31m`
  - `\x1b[1;31m`

Special values:

- `none`, `off`, `default`, `reset`: disable style application for that key.

Examples:

- `bold bright-yellow`
- `underline fg:bright-cyan bg:black`
- `fg:196 bg:235` (ANSI 256-color palette)
- `fg:#ff8c00 bg:#282828` (ANSI true color)
- `fg:255,140,0 bg:40,40,40` (ANSI true color, decimal RGB)
- `bright red bg:bright-black`
- `38;5;208` (ANSI 256-color palette)
- `38;2;255;140;0` (ANSI true color)
- `\e[1;4;97;44m`

256-color index values use ANSI SGR `38;5;n` for foreground and `48;5;n` for background, where `n` is 0..255.

True-color values use ANSI SGR `38;2;r;g;b` for foreground and `48;2;r;g;b` for background, where `r`, `g`, and `b` are 0..255. Hex forms accept `#rrggbb` or `#rgb` (shorthand expands each digit to a pair, for example `#f80` → `#ff8800`). Decimal forms accept comma-separated `r,g,b`. SVG rendering: [spec_svg.md](spec_svg.md).

## Keys and Behavior

Coloring keys live on `settings` and map-form `steps`. Key placement and defaults: [spec_scenario.md](spec_scenario.md). Value format: [Style Value Formats](#style-value-formats) below.

### `highlight` (output)

| Field | Required | Description |
|---|---|---|
| `color` | yes | Style string from [Style Value Formats](#style-value-formats). |
| `at` | yes | Range string or list of range strings. |

- Applies only to command output (stdout/stderr merged in capture order).
- Multiple entries are applied in list order.
- On overlap, later writes win.

### `run-highlight` (typed command)

- Optional on map-form `run` steps.
- Value format: [Style Value Formats](#style-value-formats).
- Colors only typed command characters.
- Does not affect prompt, command output, or `name` comment line.

### `stderr-color` (stderr fallback)

- Configurable under both `settings` and step.
- Step value overrides settings value.
- Value format: [Style Value Formats](#style-value-formats).
- Current behavior defaults to `red` when not specified.

Behavior:

1. If stderr already includes ANSI SGR, keep it as-is.
2. Otherwise apply resolved `stderr-color`.

Priority: explicit ANSI in stderr > resolved `stderr-color`.

### `name` color prefix

- Default comment color: `cyan`.
- Prefix form: `"[style] text"`.
- One prefix at start is parsed; additional bracket text is literal display text.
- Prefix style format: [Style Value Formats](#style-value-formats).
- Unknown style falls back to `cyan` with warning.

## Range Grammar (`at`)

Line and column numbers are 1-based.

| Form | Meaning |
|---|---|
| `L` | Entire line `L` |
| `L1-L2` | Entire lines `L1`..`L2` |
| `L-` | Lines `L`..end of output |
| `L:C` | Single character |
| `L:C1-C2` | Columns `C1`..`C2` on one line |
| `L:C-` | Column `C` to EOL |
| `L1-L2:C1-C2` | Same column range across multiple lines |
| `L1-L2:C-` | Same start column to EOL across line span |

Reverse spans are normalized (`9-5` -> `5-9`, `20-10` -> `10-20`).

Informal parse:

```text
at        ::= line_part [ ":" col_part ]
line_part ::= positive_integer
            | positive_integer "-" positive_integer
            | positive_integer "-"
col_part  ::= positive_integer
            | positive_integer "-" positive_integer
            | positive_integer "-"
```

## Validation and Warnings

Coloring errors do not fail the step; behavior is warn-and-continue. Warnings are printed to stderr per [spec_cli.md](spec_cli.md).

- Unknown color/style string: warn and skip that color application path.
- Invalid `at` syntax: warn and skip that `at`.
- Out-of-range start: warn and skip that `at`.
- Partially out-of-range end: apply to available text; warn only when lines are missing.
- Empty `name` display text after removing valid prefix: warn and skip comment line.

## Examples

### Output emphasis

```yaml
- run: git status
  highlight:
    - color: yellow
      at: "4"
    - color: red
      at: "6-10:3-"
```

### Typed command emphasis

```yaml
- run: git log --oneline -3
  run-highlight: "bold bright-cyan"
```

### Background + underline output emphasis

```yaml
- run: printf 'line1\nline2\nline3\n'
  highlight:
    - color: "underline fg:bright-white bg:blue"
      at: "2"
```

### True-color output emphasis

```yaml
- run: printf 'true color\n'
  highlight:
    - color: "fg:#ff8c00 bg:#282828"
      at: "1"
```

### stderr fallback with per-step override

```yaml
settings:
  stderr-color: red

steps:
  - run: echo "default stderr color" 1>&2
  - run: echo "override stderr color" 1>&2
    stderr-color: bright-yellow
```

## Lessons Learned

- Post-hoc coloring is a practical replacement for TTY-dependent CLI color output in reproducible demos.
- Position-based output highlighting is useful but fragile for unstable command layouts; deterministic fixture output helps.
- A unified color palette across output/input/comment/stderr reduces cognitive load.
- Allowing style composition and raw SGR gives advanced users full ANSI control while preserving simple color-name defaults.
- Treating existing stderr ANSI as authoritative avoids clobbering tool-provided intent.

## Related documents

- [spec_scenario.md](spec_scenario.md) — YAML key placement and defaults.
- [spec_cli.md](spec_cli.md) — CLI logging and warning delivery.
- [spec_cast.md](spec_cast.md) — cast file format and recording boundary.
- [spec_svg.md](spec_svg.md) — SVG rendering of cast events.

## Changelog

| Date | Change |
|---|---|
| 2026-06-18 | Added true-color RGB support via `fg:#rrggbb`, `bg:#rrggbb`, `fg:#rgb`, `bg:#rgb`, `fg:r,g,b`, `bg:r,g,b`, `38;2;r;g;b`, and `48;2;r;g;b`. |
| 2026-06-17 | Added 256-color index support via `fg:<0-255>`, `bg:<0-255>`, `38;5;n`, and `48;5;n`. |
| 2026-06-17 | Extended color values to style strings (bold/underline/background/bright) and SGR literal input across `highlight`, `run-highlight`, `stderr-color`, and `name` prefix. |
| 2026-06-17 | Reorganized document as a unified coloring spec (`highlight`, `run-highlight`, `stderr-color`, `name` color prefix). |
| 2026-06-17 | Implemented `stderr-color` behavior with ANSI-preserving fallback semantics. |
| 2026-06-17 | Implemented `run-highlight` for typed command text. |
| 2026-06-17 | Implemented output `highlight` and `name` color prefix behavior. |
