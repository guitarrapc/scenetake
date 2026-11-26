# Coloring Specification

Status: **Implemented**

## Motivation

scenetake executes commands and records terminal output into cast events. In non-TTY execution, many CLI tools stop emitting ANSI colors, so demos can look flat even when rendered by [agg](https://docs.asciinema.org/manual/agg/).

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
- `highlight` on `pty: true` steps, by the lines the program writes ([PTY output](#pty-output) below).
- Warning-and-continue behavior for invalid values and out-of-range targets.

### Out of scope (v1)

- String-form step coloring defaults (for example `- echo "foo"`).
- `settings` defaults for `highlight` ranges.
- `stderr-color` on `pty: true` steps, where stdout and stderr are one stream, and `highlight` on `interactive: true` steps, whose screen a person draws. Both warn and do nothing. See [spec_pty.md](spec_pty.md).
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

#### PTY output

`highlight` on a `pty: true` step colors the output as the program writes it to the terminal:

- **Lines.** A line ends at a line feed, and a carriage return just before it ends the line too. A carriage return alone, as a progress bar writes it to redraw its line, stays in its line: `10%\r99%\r\n` is one line. A pipe step turns a lone carriage return into a line break, a PTY step does not, so the lines are the ones the terminal shows. A full-screen program moves the cursor instead of writing lines, and a highlight there follows what it writes, not the screen's rows.
- **Columns** count the characters as written, escape sequences included, as for a pipe step. Colored output holds sequences the scenario does not show, so a whole line (`at: "3"`) is the dependable range. On Windows the PTY writes a program's colors again in its own form (`ESC[0m` comes back as `ESC[m`, a space can move before it), so the columns of colored output differ between Windows and Linux.
- **The program's colors.** The style opens again after each color sequence the program writes inside the range, so the program's own colors and resets do not end the highlight, and what the style does not set (a highlight of `bg:blue` keeps the program's red text) stays the program's. Where the range ends, the program's colors at that point come back after the reset, so the rest of the line keeps them.
- **Reads.** A style is not closed and opened again where one read of the output ends and the next begins, as a terminal keeps a color from one event to the next. The output joined is the same text however the reads fell, so recording again finds a step that printed the same and keeps its earlier events, as for a step without a highlight ([spec_scenario.md](spec_scenario.md) → Recording again).
- **Replay.** The outputs file keeps the output without the highlight, and `--replay` and the playground color it as the scenario says then.
- **Not on interactive steps.** `interactive: true` with `highlight` warns and records the step without it: a person draws that screen, so it has no lines to point at.

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
- A step's own `stderr-color` with `pty: true` warns and does nothing: a terminal gets stdout and stderr as one stream. The `settings` value is a default for the steps that can use it, so it does not warn.

Behavior:

1. If stderr already includes ANSI SGR, keep it as-is.
2. Otherwise apply resolved `stderr-color`.

Priority: explicit ANSI in stderr > resolved `stderr-color`.

`none`, `off`, `default`, and `reset` use the normal text color. Tools also print ordinary messages on stderr, including git branch changes, curl progress, and `docker build` output. Red can make these messages look like failures. The README and starter template explain `stderr-color: none`.

### `name` color prefix

- Default comment color: `cyan`.
- Prefix form: `"[style] text"`.
- One prefix at start is parsed. Additional bracket text is literal display text.
- Prefix style format: [Style Value Formats](#style-value-formats).
- Unknown style falls back to `cyan` with warning.
- A `card` step's lines take the same prefix, without the `# ` and without a default color: a line without one takes the terminal's foreground, and an unknown style is a warning that leaves it plain ([spec_scenario.md](spec_scenario.md) → Card steps).

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

Columns count the characters of the output as written, escape sequences included. A character is what a person sees as one, an extended grapheme cluster, as typing types one: an emoji, an emoji with a skin tone, a flag, and a letter with its accent are one column each, and so is a kanji, although it takes two cells on the screen. A style never starts or ends inside a character, nor inside an escape sequence: a change that falls inside one waits for the first character after it, on the same line or a later one. A color written inside a sequence would end it early, and the rest of it, such as a window title, would show as text that secret masking skipped ([spec_scenario.md](spec_scenario.md) → Masking secrets). A sequence ends where the masker ends it, or with the output when nothing ends it. In a streamed step the highlighted text is cut back into its reads at visible characters, counted by the same rules across reads, so the color that each read closes and opens again stays outside a sequence too.

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

Coloring errors do not fail the step. Behavior is warn-and-continue. Warnings are printed to stderr per [spec_cli.md](spec_cli.md).

- Unknown color/style string: warn and skip that color application path.
- Invalid `at` syntax: warn and skip that `at`.
- Out-of-range start: warn and skip that `at`.
- Partially out-of-range end: apply to available text. Warn only when lines are missing.
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
- Position-based output highlighting is useful but fragile for unstable command layouts. Deterministic fixture output helps.
- A unified color palette across output/input/comment/stderr reduces cognitive load.
- Allowing style composition and raw SGR gives advanced users full ANSI control while preserving simple color-name defaults.
- Treating existing stderr ANSI as authoritative avoids clobbering tool-provided intent.
- **`highlight` on a `pty: true` step did nothing, without a word** (until 2026-11-23). PTY output was written to the cast as raw bytes, and the key was read but never applied. Seven of the eight showcase demos run with `pty: true`, for the programs' own colors, and none could point at a line. Pipe output gets the highlight on text whose bare carriage returns became line breaks and whose color closes at every read. Neither fits a PTY: a progress bar would gain lines, and the closes would fall where the reads fell, so recording again would find every highlighted step changed.
- **A highlight painted inside an escape sequence showed a secret** (2026-11-19). Highlights are painted after masking, by column. A range that started inside a window title wrote a color there, the cast filter ended the title at its ESC, and the rest of the title, which the masker had skipped, showed in clear. Columns still count escape sequences, so committed scenarios keep their ranges. A streamed step had the same leak where its reads were cut back apart: the splitter read `ESC 7 ESC ] 0 ; A` as one sequence up to the first letter, counted the rest of the title as visible, and closed its color inside it.
- **Columns counted UTF-16 code units** (until 2026-11-27). An emoji was two columns, and a range whose edge fell between its two halves wrote the color between them. The cast writer then wrote each half as U+FFFD. Columns now count grapheme clusters, the unit typing already used for one keystroke. Ranges over ASCII and CJK text count as before, so the committed samples kept theirs. Screen cells, where a kanji is two, were not chosen: every range after Japanese text would have moved.
- **Each read's part was found by a walk from the start** (2026-11-26). A streamed step's highlighted output is cut back into its reads, and each part was found by walking the highlighted text from its first character, which took time in reads × length: 870ms for 4,000 reads. One walk now goes on from where the part before it stopped, with the colors set so far, and takes 2.6ms. A test compares every part with a walk from the start.

## Related documents

- [spec_scenario.md](spec_scenario.md) — YAML key placement and defaults.
- [spec_cli.md](spec_cli.md) — CLI logging and warning delivery.
- [spec_cast.md](spec_cast.md) — cast file format and recording boundary.
- [spec_svg.md](spec_svg.md) — SVG rendering of cast events.

## Changelog

| Date | Change |
|---|---|
| 2026-11-27 | `at` columns count grapheme clusters, not UTF-16 code units: an emoji, a flag, or a letter with its accent is one column, and a style never splits one. |
| 2026-11-23 | `highlight` colors `pty: true` output by line, over the program's own colors. A step's `stderr-color` with `pty: true` and `highlight` with `interactive: true` warn that they do nothing. |
| 2026-11-19 | A highlight no longer starts or ends a style inside an escape sequence, also where a streamed step is cut back into its reads. Those reads now keep the sequences after the last visible character, and a read after a cut opens 256-color, RGB, and colon-form colors again. |
| 2026-06-18 | Added true-color RGB support via `fg:#rrggbb`, `bg:#rrggbb`, `fg:#rgb`, `bg:#rgb`, `fg:r,g,b`, `bg:r,g,b`, `38;2;r;g;b`, and `48;2;r;g;b`. |
| 2026-06-17 | Added 256-color index support via `fg:<0-255>`, `bg:<0-255>`, `38;5;n`, and `48;5;n`. |
| 2026-06-17 | Extended color values to style strings (bold/underline/background/bright) and SGR literal input across `highlight`, `run-highlight`, `stderr-color`, and `name` prefix. |
| 2026-06-17 | Reorganized document as a unified coloring spec (`highlight`, `run-highlight`, `stderr-color`, `name` color prefix). |
| 2026-06-17 | Implemented `stderr-color` behavior with ANSI-preserving fallback semantics. |
| 2026-06-17 | Implemented `run-highlight` for typed command text. |
| 2026-06-17 | Implemented output `highlight` and `name` color prefix behavior. |
