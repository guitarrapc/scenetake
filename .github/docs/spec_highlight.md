# Output Highlight Specification

Status: **Implemented**

## Motivation

scenario2cast runs commands and writes their stdout/stderr into asciinema v2 cast files. Many CLI tools suppress ANSI color when stdout is not a TTY, so demos often look plain even though the final GIF could render colors via [agg](https://docs.asciinema.org/manual/agg/).

Post-processing command output with declarative highlights lets authors color specific lines or columns **without** depending on each tool’s color flags or a pseudo-TTY. Highlights are stored as standard ANSI SGR sequences in cast `"o"` events; agg applies them using the chosen terminal theme palette.

## Scope

### In scope (v1)

- `highlight` on **map-form** steps that include `run:`.
- Named **16-color** foreground palette (ANSI normal + bright).
- Positional ranges via `at`: full lines, line spans, and column spans (including multi-line column bands).
- Multiple highlight entries per step; multiple `at` strings per entry.
- Warning-and-continue on invalid or out-of-range targets.

### Out of scope (v1)

- Highlights on **string-form** steps (e.g. `- echo "foo"`). Planned for a later version.
- Global defaults under `settings`.
- Background colors, bold/underline modifiers, or regex-based matching.
- Terminal theme (`theme` in cast header); that remains separate from per-output ANSI highlights.
- Relative line indices (e.g. `-1` = last line).

## YAML shape

Map-form steps may include `highlight` alongside existing keys (`typing-speed`, `post-delay`, etc.):

```yaml
steps:
  - run: git status
    post-delay: 1.5
    highlight:
      - color: yellow
        at: "4"
      - color: red
        at:
          - "6-10:3-"
```

| Field   | Required | Description                                      |
|---------|----------|--------------------------------------------------|
| `color` | yes      | One of the [color names](#color-names) below.    |
| `at`    | yes      | A range string, or a list of range strings.      |

`highlight` applies only to **command output** for that step (stdout and stderr combined, in capture order). It does not affect the simulated prompt or keystroke typing events.

## Color names

Lowercase names map to ANSI foreground SGR codes (8 normal + 8 bright). v1 supports **foreground only**.

| Normal (0–7) | Bright (8–15)                          |
|--------------|----------------------------------------|
| `black`      | `bright-black` (aliases: `gray`, `grey`) |
| `red`        | `bright-red`                           |
| `green`      | `bright-green`                         |
| `yellow`     | `bright-yellow`                        |
| `blue`       | `bright-blue`                          |
| `magenta`    | `bright-magenta`                       |
| `cyan`       | `bright-cyan`                          |
| `white`      | `bright-white`                         |

Each applied span is wrapped with the appropriate SGR open code and `\u001b[0m` reset so adjacent regions do not bleed color.

## Range grammar (`at`)

Line and column numbers are **1-based**. Logical lines are split on `\n` or `\r\n`; line numbers ignore the line-break characters themselves.

### Forms

| Form          | Meaning                                              |
|---------------|------------------------------------------------------|
| `L`           | Entire line `L`.                                     |
| `L1-L2`       | Entire lines `L1` through `L2` inclusive.          |
| `L:C`         | Single character at line `L`, column `C`.            |
| `L:C1-C2`     | Line `L`, columns `C1` through `C2` inclusive.       |
| `L:C-`        | Line `L`, column `C` through end of line (excl. NL). |
| `L1-L2:C1-C2` | Lines `L1`–`L2`, each line columns `C1`–`C2`.       |
| `L1-L2:C-`    | Lines `L1`–`L2`, each line column `C` through EOL.   |

**Omitted end:**

- Omitting the line span (`L` alone) → whole line.
- Omitting the column end (`L:C-`) → from column `C` to end of line.

**Reverse spans:** If `L1 > L2` or `C1 > C2`, the endpoints are **normalized** (e.g. `9-5` → `5-9`, `20-10` → `10-20`). No error; this keeps hand-authored ranges forgiving.

**No relative indices:** Negative line or column numbers are invalid.

### Parse rule (informal)

```
at        ::= line_part [ ":" col_part ]
line_part ::= positive_integer
            | positive_integer "-" positive_integer
col_part  ::= positive_integer
            | positive_integer "-" positive_integer
            | positive_integer "-"
```

## Application behavior

1. Run the command and capture output (same as today: stdout then stderr, normalized for cast).
2. If `highlight` is absent or empty, write output unchanged.
3. Otherwise apply each highlight entry **in list order**. Within one entry, apply each `at` string in order.
4. **Overlaps:** Later entries (and later `at` strings within the same entry) **win** over earlier ones where they overlap.
5. Insert ANSI codes into the output text, then write the result to the cast event stream (with existing newline normalization for asciinema).

### Out-of-range and partial ranges

Behavior is **warn and continue**; the step must not fail.

| Situation | Behavior |
|-----------|----------|
| Range starts **after** all output (e.g. line 20 when only 10 lines exist; or column 50 on a 10-character line) | Emit a **warning** to stderr; **skip** coloring for that `at` string. |
| Range starts inside output but **extends past** the end (line span, column span, or EOL) | Color from the valid start through the **actual end of output** on that line or last available line; no warning for truncation. |
| Line span partially exists (e.g. `8-12` but only 10 lines) | Apply to lines 8–10; **warn** once that lines 11–12 were missing. |
| Invalid syntax or unknown color name | **Warn**; skip that highlight entry (or that `at` string, depending on what failed—syntax/color is per entry). |

Warnings should name the step (command text or step index) and the offending `at` / `color` value so authors can fix scenarios.

## Examples

### Git status — heading and changed paths

```yaml
- run: git status
  post-delay: 1.5
  highlight:
    - color: yellow
      at: "4"
    - color: red
      at: "6-10:3-"
```

Line numbers depend on `git status` layout; authors should target stable output (e.g. fixed repo state) or generous ranges.

### Multi-line column band (diff --stat style)

```yaml
- run: git diff --stat HEAD~1
  highlight:
    - color: cyan
      at: "1"
    - color: green
      at: "2-6:1-24"
    - color: yellow
      at: "2-6:25-"
```

### Same color, several places

```yaml
- run: git log --oneline -5
  highlight:
    - color: bright-yellow
      at:
        - "1:1-7"
        - "3:1-7"
        - "5:1-7"
```

### Multiline command output

```yaml
- run: |
    printf 'line1 alpha\nline2 beta gamma\nline3\n'
  highlight:
    - color: red
      at: "1:7-"
    - color: green
      at: "2:7-10"
    - color: blue
      at: "2-3"
```

## Lessons learned (design-time)

- **TTY vs post-hoc color:** Redirected stdout breaks most tools’ auto-color; post-hoc highlighting keeps scenarios portable and deterministic.
- **Line numbers are fragile** for commands like `git status` unless the repo state is fixed; positional highlighting trades robustness for zero dependency on tool-specific color flags. Authors accept that trade-off for demo scripts.
- **Unified `at` grammar** (`2-5:10-20` = same columns on each line) matches common terminal output alignment better than separate line/column keys.
- **Reverse span normalization** (`9-5` → `5-9`) costs little and avoids annoying errors when endpoints are typed in either order.

## Future considerations (not v1)

### `name` on `run` map steps

A `name` key may be added for cross-step references or markers. A possible convention: **default highlight in `cyan` for the line(s) associated with that name** (exact mapping TBD when `name` lands). Explicit `highlight` on the same step would override or merge per overlap rules—TBD.

### Highlighting the command line, not output

Use case: emphasize what was typed while leaving raw command output uncolored.

- Today, typing and output are separate cast events; `highlight` only targets **post-execution output**.
- Reserving **line `0`** (or another sentinel) for “the command line” is tempting but **ambiguous**: `run: |` multiline blocks are valid YAML and may not correspond to a single terminal row. There is no guarantee the executed command fits one line.
- Likely needs a **separate mechanism** (e.g. `highlight-input: true` or `highlight: command`) rather than overloading output line `0`. Deferred until `name` / input-highlight design is clearer.

### stderr default red

**Idea:** Always render stderr spans in red (or `bright-red`) without per-step `highlight`.

| Pros | Cons |
|------|------|
| Matches common terminal conventions; errors stand out in GIFs. | Harder to highlight stderr selectively; breaks demos that intentionally print informative messages to stderr. |
| Zero YAML for the common case. | Today stdout and stderr are merged before cast write; splitting for styling affects ordering semantics. |

**Recommendation:** Do **not** auto-red stderr in v1. If added later, prefer an opt-in step or settings flag (e.g. `stderr-color: red`) rather than global always-on behavior, unless user feedback strongly favors default red.

## Related documents

- [asciicast v2](https://docs.asciinema.org/manual/asciicast/v2/) — `"o"` event format and optional header `theme`.
- [agg usage](https://docs.asciinema.org/manual/agg/usage/) — GIF rendering and `--theme` / `--bold-is-bright`.

## Changelog

| Date       | Change |
|------------|--------|
| 2026-06-17 | Initial spec from scenario2cast highlight design discussion. |
| 2026-06-17 | Implemented in `scenario2cast.cs` (v1). Per-line `byte[]` paint buffers; ANSI inserted once when rendering. |
