# Coloring Specification

Status: **Implemented**

## Motivation

scenario2cast executes commands and writes asciinema v2 cast output. In non-TTY execution, many CLI tools stop emitting ANSI colors, so demos can look flat even when rendered by [agg](https://docs.asciinema.org/manual/agg/).

This spec defines declarative coloring controls that keep demos readable without depending on each tool's TTY/color options.

## Scope

### In scope (v1)

- `highlight` for command output on map-form `run` steps.
- `run-highlight` for typed command text on map-form `run` steps.
- `stderr-color` fallback coloring for stderr text that has no ANSI SGR.
- Colorized `name` comment lines with optional `[color]` prefix.
- Shared 16-color foreground palette (normal + bright).
- Range-based output targeting via `at`.
- Warning-and-continue behavior for invalid values and out-of-range targets.

### Out of scope (v1)

- String-form step coloring defaults (for example `- echo "foo"`).
- `settings` defaults for `highlight` ranges.
- Background colors, style modifiers (bold/underline), regex selectors.
- Terminal theme control (`theme` in cast header).
- Relative indices (for example `-1` = last line).

## Coloring Targets

Coloring is split by target type so behavior is explicit and predictable.

| Target | Key | Scope | Applies to |
|---|---|---|---|
| Command output | `highlight` | step | stdout/stderr combined output text for that step |
| Typed command | `run-highlight` | step | Simulated typed command characters |
| stderr fallback | `stderr-color` | settings + step | stderr text that has no ANSI SGR |
| Step comment | `name` with optional `[color]` prefix | step | `# ...` comment line shown before typing |

## Shared Color Names

All coloring keys use the same foreground palette.

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

## YAML Contract

```yaml
settings:
  stderr-color: red

steps:
  - name: "[yellow] review status"
    run: git status
    run-highlight: bright-cyan
    highlight:
      - color: yellow
        at: "4"
      - color: red
        at: "6-10:3-"
```

### `highlight` (output)

| Field | Required | Description |
|---|---|---|
| `color` | yes | One of [Shared Color Names](#shared-color-names). |
| `at` | yes | Range string or list of range strings. |

- Applies only to command output (stdout/stderr merged in capture order).
- Multiple entries are applied in list order.
- On overlap, later writes win.

### `run-highlight` (typed command)

- Optional on map-form `run` steps.
- Colors only typed command characters.
- Does not affect prompt, command output, or `name` comment line.

### `stderr-color` (stderr fallback)

- Configurable under both `settings` and step.
- Step value overrides settings value.
- Current behavior defaults to `red` when not specified.

Behavior:

1. If stderr already includes ANSI SGR, keep it as-is.
2. Otherwise apply resolved `stderr-color`.

Priority: explicit ANSI in stderr > resolved `stderr-color`.

### `name` color prefix

- Default comment color: `cyan`.
- Prefix form: `"[color] text"`.
- One prefix at start is parsed; additional bracket text is literal display text.
- Unknown color falls back to `cyan` with warning.

## Range Grammar (`at`)

Line and column numbers are 1-based.

| Form | Meaning |
|---|---|
| `L` | Entire line `L` |
| `L1-L2` | Entire lines `L1`..`L2` |
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
col_part  ::= positive_integer
            | positive_integer "-" positive_integer
            | positive_integer "-"
```

## Validation and Warnings

Coloring errors do not fail the step; behavior is warn-and-continue.

- Unknown color name: warn and skip that color application path.
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
  run-highlight: bright-cyan
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
- Treating existing stderr ANSI as authoritative avoids clobbering tool-provided intent.

## Related documents

- [asciicast v2](https://docs.asciinema.org/manual/asciicast/v2/) — cast event format.
- [agg usage](https://docs.asciinema.org/manual/agg/usage/) — rendering and theme options.

## Changelog

| Date | Change |
|---|---|
| 2026-06-17 | Reorganized document as a unified coloring spec (`highlight`, `run-highlight`, `stderr-color`, `name` color prefix). |
| 2026-06-17 | Implemented `stderr-color` behavior with ANSI-preserving fallback semantics. |
| 2026-06-17 | Implemented `run-highlight` for typed command text. |
| 2026-06-17 | Implemented output `highlight` and `name` color prefix behavior. |
