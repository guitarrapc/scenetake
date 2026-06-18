# SVG Output Specification

Status: **Implemented**

## Motivation

For README and documentation embeds, animated SVG is often preferable to GIF: smaller file size, crisp scaling, and easy web embedding without a player.

External tools such as [agg](https://docs.asciinema.org/manual/agg/) (GIF) and [asg](https://github.com/kingsword09/asg) (SVG) can convert cast files, but requiring a second install and a second command adds friction. Built-in SVG output lets users produce `.cast` and `.svg` in one invocation while keeping cast as the canonical artifact defined in [spec_cast.md](spec_cast.md).

Rich TUI recordings (`copilot --banner`, `sl`, etc.) are expected from **external asciinema casts** converted via the `svg` subcommand. The scenario path records commands via pipes (no PTY). See [plan_svg_update.md](../../plan_svg_update.md).

## Scope

- SVG output via `--format svg` or the `svg` subcommand. CLI: [spec_cli.md](spec_cli.md). Scenario `render:` keys: [spec_scenario.md](spec_scenario.md).
- Built-in C# SVG renderer (no bundled external binary).
- **Row-diff** animated SVG (changed rows only, CSS layer fade) with **12 fps** frame sampling (console2svg-style `ReduceFrames` + `SpreadCollapsedFrameTimes`).
- VT emulator at console2svg reference level (see [VT emulator](#vt-emulator)).
- Theme presets `dark` (default) and `light`; default `font-size` of `16`; default `font-family` stack tuned for programming.
- Block cursor (`theme.fg` at 50% opacity, no blink); DECTCEM visibility (`\e[?25h`, `\e[?25l`).
- Warn-and-continue for unsupported cast event codes.
- Resize cast events (`"r"`) during `svg` conversion.
- Window chrome (macOS / Windows frame): **out of scope v1** (planned v2).

Cast file format (header, versions, written event codes): [spec_cast.md](spec_cast.md).

## `svg` Subcommand — Input and Events

- Accepts cast files defined in [spec_cast.md](spec_cast.md) (asciicast v2 or v3, read-only).
- Render metadata resolution: CLI overrides > cast header > defaults. See [spec_cli.md](spec_cli.md).
- Processes `"o"` (output) and `"r"` (resize) events. Silently skips `"i"`, `"m"`, and `"x"`. Other codes warn once (warn-and-continue).
- Invalid or out-of-range resize sizes warn once and are skipped.
- Canvas size is the maximum terminal dimensions across the header and all valid `"r"` events. On shrink, a per-frame viewport clip shows the current terminal size within the fixed canvas (`preserveAspectRatio` on the root `<svg>`).

## Renderer Requirements

### VT emulator

| Category | v1 support |
|---|---|
| CSI cursor (`A`–`F`, `G`, `H`, `d`) | Yes |
| Erase (`J`, `K`, `X`), insert/delete (`@`, `P`, `L`, `M`, `S`, `T`) | Yes |
| Scroll region (`r`), save/restore (`s`, `u`, `ESC 7/8`) | Yes |
| Alternate screen (`?1049h/l`), cursor visibility (`?25h/l`) | Yes |
| SGR: bold, faint, italic, underline, reverse, 16/256/true color | Yes — `;` and `:` forms |
| Unicode wide chars, surrogate pairs, combining marks, VS | Yes |
| OSC / DCS / charset designations | Skipped (not rendered) |
| Block elements U+2580–U+259F | Yes — SVG rects |

Implementation: `Terminal.cs` (AnsiParser, ScreenBuffer, TerminalReplay). Tests: `tests/terminal_tests.cs`.

### Animation and visuals

- Replay follows cast event timestamps; frame optimizer caps sampling at **12 fps** while preserving the latest visual change within each interval.
- Output is self-contained animated SVG (CSS only, no JavaScript).
- **Row-diff** animation: only changed rows are emitted as timed layers (`layer-in` / `layer-out` keyframes). Cursor and viewport resize use separate layers.
- Trailing blank frames after alternate-screen restore are trimmed when the event stream indicates a screen clear.
- Background from `theme.bg`; monospace font at resolved `font-size` and `font-family`.
- Layout uses fixed cell metrics (`CharWidthFactor` 0.62, `LineHeightFactor` 1.25).
- Layout padding:
  - **Outer:** 8px transparent margin.
  - **Inner:** horizontal `font-size × 10/16`, vertical `font-size × 4/16`, clamped (horizontal 4–16px, vertical 2–8px).
- Block cursor at the emulator position when visible; hidden by `\e[?25l`.

## Failure Behavior

### Scenario path (`--format svg`)

| Phase | On failure |
|---|---|
| `pre` | Fail-fast; no cast or SVG. See [spec_pre_post.md](spec_pre_post.md). |
| Cast write | Fail; SVG not attempted. |
| SVG render | Cast retained; partial `.svg` deleted; exit non-zero; `post` still runs. |
| `post` | Fail-fast; cast (and SVG if written) remain. See [spec_pre_post.md](spec_pre_post.md). |

### `svg` subcommand

| Situation | Behavior |
|---|---|
| Cast not found, invalid header, missing/out-of-range terminal size, invalid event JSON | Exit non-zero; no SVG |
| SVG render failure | Exit non-zero; partial `.svg` deleted |
| Unsupported event codes, invalid resize | Warning only; continue |

The cast file is never modified by the `svg` subcommand.

## Cross-Document Notes

- [spec_cast.md](spec_cast.md) — cast header, versions, event stream.
- [spec_scenario.md](spec_scenario.md) — `render:` YAML keys.
- [spec_cli.md](spec_cli.md) — commands, options.
- [spec_highlight.md](spec_highlight.md) — cast-event coloring (header `theme` is separate).
- [plan_svg_update.md](../../plan_svg_update.md) — design decisions and migration notes.

## References

- [agg usage](https://docs.asciinema.org/manual/agg/usage/)
- [asg](https://github.com/kingsword09/asg)

## Lessons Learned

- Row-diff layers keep typing-heavy casts small (~80KB for `basic.cast`); full-screen redraws still work but emit one layer per changed row.
- Frame sampling at 12 fps (with pending-frame coalescing) prevents one defs/frame per keystroke; match console2svg `ReduceFrames` order: reduce, then spread collapsed times.
- Trailing blank alternate-screen restore frames should be trimmed when the event stream contains screen-clear sequences.
