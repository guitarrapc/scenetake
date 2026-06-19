# SVG Output Specification

Status: **Implemented**

## Motivation

For README and documentation embeds, animated SVG is often preferable to GIF: smaller file size, crisp scaling, and easy web embedding without a player.

External tools such as [agg](https://docs.asciinema.org/manual/agg/) (GIF) and [asg](https://github.com/kingsword09/asg) (SVG) can convert cast files, but requiring a second install and a second command adds friction. Built-in SVG output lets users produce `.cast` and `.svg` in one invocation while keeping cast as the canonical artifact defined in [spec_cast.md](spec_cast.md).

Rich TUI recordings (`copilot --banner`, `sl`, etc.) are expected from **external asciinema casts** converted via the `svg` subcommand. The scenario path records commands via pipes (no PTY).

## Scope

- SVG output via `--format svg` or the `svg` subcommand. CLI: [spec_cli.md](spec_cli.md). Scenario `render:` keys: [spec_scenario.md](spec_scenario.md).
- Built-in C# SVG renderer (no bundled external binary).
- **Row-diff** animated SVG (changed rows only, CSS layer fade) with optional **`--max-fps`** frame sampling (default **off**).
- VT emulator. (see [VT emulator](#vt-emulator)).
- Theme presets `dark` (default) and `light`; default `font-size` of `16`; default `font-family` stack tuned for programming.
- Block cursor (`theme.fg` at 50% opacity, no blink); DECTCEM visibility (`\e[?25h`, `\e[?25l`).
- Warn-and-continue for unsupported cast event codes.
- Resize cast events (`"r"`) during `svg` conversion.
- **Window chrome** (v2): optional `macos` or `windows` frame. Default is plain terminal (no chrome).

Cast file format (header, versions, written event codes): [spec_cast.md](spec_cast.md).

## `svg` Subcommand — Input and Events

- Accepts cast files defined in [spec_cast.md](spec_cast.md) (asciicast v2 or v3, read-only).
- Render metadata resolution: CLI overrides > cast header > defaults. See [spec_cli.md](spec_cli.md).
- Processes `"o"` (output) and `"r"` (resize) events. Silently skips `"i"`, `"m"`, and `"x"`. Other codes warn once (warn-and-continue).
- Invalid or out-of-range resize sizes warn once and are skipped.
- Canvas size is the maximum terminal dimensions across the header and all valid `"r"` events. On shrink, a per-frame viewport clip shows the current terminal size within the fixed canvas (`preserveAspectRatio` on the root `<svg>`).

## Renderer Requirements

### VT emulator

| Category | Support |
|---|---|
| CSI cursor (`A`–`F`, `G`, `H`, `d`) | Yes |
| Erase (`J`, `K`, `X`), insert/delete (`@`, `P`, `L`, `M`, `S`, `T`) | Yes |
| Scroll region (`r`), save/restore (`s`, `u`, `ESC 7/8`) | Yes |
| Alternate screen (`?1049h/l`), cursor visibility (`?25h/l`) | Yes |
| SGR: bold, faint, italic, underline, reverse, 16/256/true color | Yes — `;` and `:` forms |
| Unicode wide chars, surrogate pairs, combining marks, VS | Yes |
| OSC / DCS / charset designations | Skipped (not rendered) |
| Block elements U+2580–U+259F | Yes — SVG rects |

Tests: `tests/terminal_tests.cs`.

### Animation and layout (plain terminal)

- Replay follows cast event timestamps. **`--max-fps`** (default **off**) caps sampling when set.
- Output is self-contained animated SVG (CSS only, no JavaScript).
- **Row-diff** animation: only changed rows are emitted as timed layers (`layer-in` / `layer-out` keyframes). Cursor and viewport resize use separate layers.
- Trailing blank frames after alternate-screen restore are trimmed when the event stream indicates a screen clear.
- Background from `theme.bg`; monospace font at resolved `font-size` and `font-family`.
- Cell metrics: `CharWidthFactor` 0.62, `LineHeightFactor` 1.25.
- Padding when `window` is `none`:
  - **Outer:** 8px transparent margin.
  - **Inner:** horizontal `font-size × 10/16`, vertical `font-size × 4/16`, clamped (horizontal 4–16px, vertical 2–8px).
- Block cursor at the emulator position when visible; hidden by `\e[?25l`.

### Window chrome

Optional OS-style frame for README embeds. Configured via `render.window` in scenario YAML, `st:window` in v3 cast `tags`, or `--window` on CLI. Default: **`none`** (plain terminal above).

| Source | Key / flag | Values |
|--------|------------|--------|
| Scenario | `render.window` | `macos`, `windows` (omit = `none`) |
| Cast header (v3) | `st:window=…` in `tags` | Written when not `none`; omitted otherwise |
| CLI | `--window` | `none`, `macos`, `windows` |

Priority: **CLI > cast header > default `none`**. Unknown values error on scenario path and CLI; invalid cast tag warns once and falls back to `none`.

#### Visual behavior

- Replaces the plain 8px outer margin with a title bar, border, rounded outer frame, and drop shadow.
- **Title bar:** controls only (no title text). macOS: three traffic-light circles (left). Windows: three square buttons (right).
- **Title bar shape:** top corners rounded; bottom edge straight (flush with terminal viewport — avoids a gap between bar and terminal).
- **Chrome colors:** derived from terminal `theme.bg` luminance (light vs dark chrome palette). `theme.preset` and resolved header colors drive this; per-key `theme.fg` / `theme.bg` overrides affect **terminal content only**, not chrome palette selection beyond bg luminance.
- **Resize:** chrome width/height follow viewport `"r"` events; chrome elements share `viewport-*` layer animation timing with the terminal background mask.
- **Inner terminal padding** (around cell content) is unchanged from plain mode.

#### Fixed chrome geometry (px)

Does **not** scale with `font-size` (matches real OS window chrome). Tuned at default `font-size` 16.

| Constant | macOS | Windows |
|----------|-------|---------|
| Title bar height | 34 | 34 |
| Outer margin (shadow) | 12 | 12 |
| Side padding (controls inset) | 14 | 14 |
| Top padding (controls) | 9 | 9 |
| Outer corner radius | 8 | 4 |
| Control size | circles Ø16, gap 10 | squares 17×17, gap 5.5 |

#### Samples

- `samples/theme.yaml` — color demo, no chrome.
- `samples/theme-macos.yaml` — `window: macos`, `preset: dark`.
- `samples/theme-windows.yaml` — `window: windows`, `preset: dark`.

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

## Testing

| Suite | Coverage |
|-------|----------|
| `tests/terminal_tests.cs` | VT emulator (CSI, Unicode, alt screen) |
| `tests/svg_render_test.cs` | Row-diff smoke, marker timing, `--max-fps` |
| `tests/window_chrome_test.cs` | `render.window` / cast tag / CLI resolution; chrome SVG structure |

Run: `dotnet run tests/terminal_tests.cs` (and sibling test scripts). CI runs all three.

## Cross-Document Notes

- [spec_cast.md](spec_cast.md) — cast header, `st:window` tag, v2 header rules.
- [spec_scenario.md](spec_scenario.md) — `render.window`.
- [spec_cli.md](spec_cli.md) — `--window`.
- [spec_highlight.md](spec_highlight.md) — cast-event coloring (header `theme` is separate).
- [plan_svg_update.md](../../plan_svg_update.md) — planning archive and deferred items.

## References

- [agg usage](https://docs.asciinema.org/manual/agg/usage/)
- [asg](https://github.com/kingsword09/asg)

## Lessons Learned

- Row-diff layers keep typing-heavy casts small (~80KB for `basic.cast`); full-screen redraws emit one layer per changed row.
- Frame sampling is optional (`--max-fps`); default off keeps full timing for typing and bulk output (e.g. curl).
- Trailing blank alternate-screen restore frames should be trimmed when the event stream contains screen-clear sequences.
- Window chrome should stay **fixed px** like a real desktop window; scaling chrome with `font-size` looked wrong in README embeds.
- Title bar corners must be rounded on the **top only**; a fully rounded title-bar rect leaves visible gaps where it meets the terminal viewport.
- Chrome control layout (button size, padding, title bar height) needed visual iteration; fixed constants are preferable to formula-based scaling.
