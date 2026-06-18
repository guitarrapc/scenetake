# SVG Output Specification

Status: **Implemented**

## Motivation

scenario2cast generates asciinema v2 cast files. For README and documentation embeds, animated SVG is often preferable to GIF: smaller file size, crisp scaling, path-based rendering, and easy web embedding without a player.

External tools such as [agg](https://docs.asciinema.org/manual/agg/) (GIF) and [asg](https://github.com/kingsword09/asg) (SVG) can convert cast files, but requiring a second install and a second command adds friction. This spec defines built-in SVG output so users can produce `.cast` and `.svg` in one CLI invocation while keeping cast as the canonical artifact. Users who prefer external converters can continue to use them.

## Scope

### In scope (v1)

- `--format svg` CLI option that writes both `.cast` and `.svg`.
- `--format cast` remains the default (backward compatible).
- Top-level `render:` section in scenario YAML for display metadata.
- Cast header fields: official `theme` plus `scenario2cast` extension for `font-size`.
- Render metadata is written to the cast header on every run, regardless of `--format`.
- C# SVG renderer inside scenario2cast (no bundled external binary).
- 16-color foreground/background ANSI SGR, plus `bold`, `underline`, and `bright`.
- 256-color ANSI SGR (`38;5;n`, `48;5;n`) using the xterm palette: indices 0–15 from `theme.palette`, 16–231 cube, 232–255 grayscale (same formula as [agg `theme.rs`](https://github.com/asciinema/agg/blob/main/src/theme.rs)).
- True-color ANSI SGR (`38;2;r;g;b`, `48;2;r;g;b`) with RGB components 0–255 (semicolon and colon forms).
- Colon-form extended color SGR (`38:5:n`, `48:5:n`, `38:2:r:g:b`, `48:2:r:g:b`); mixed `;` / `:` delimiters in one SGR are normalized for `m` commands.
- Animated SVG via CSS `animation-delay` row-layer opacity switching.
- Default `dark` theme preset when `render.theme` is omitted.
- Default `font-size` of 16.
- CLI `--font-size` override for scenario runs and the `svg` subcommand (`1`–`128`).
- Block cursor rendering driven by cursor position changes (`theme.fg` at 50% opacity; no blink).
- DECTCEM cursor visibility (`\e[?25h`, `\e[?25l`).
- Theme presets `dark` and `light` via `render.theme.preset` or CLI `--theme`.
- Warning-and-continue for malformed extended color SGR (invalid true-color RGB, invalid 256 index, unknown modes) during SVG rendering.
- Failure behavior aligned with `pre`/`post`: cast is retained, incomplete SVG is removed, `post` still runs.
- `svg` subcommand that converts an existing asciinema v2 cast file to SVG.
- Resize cast events (`"r"`) during `svg` conversion.

```bash
scenario2cast [--verbose] [--format cast|svg] [--font-size N] [--theme dark|light] <scenario.yaml> [output]
```

| Invocation | Output |
|---|---|
| `scenario2cast demo.yaml` | `demo.cast` |
| `scenario2cast --format svg demo.yaml` | `demo.cast` + `demo.svg` |
| `scenario2cast --font-size 20 demo.yaml` | `demo.cast` (header `font-size: 20`) |
| `scenario2cast --format svg --font-size 20 demo.yaml` | `demo.cast` + `demo.svg` (both use `20`) |
| `scenario2cast --format svg demo.yaml out.svg` | `out.cast` + `out.svg` |
| `scenario2cast --format svg demo.yaml out.cast` | `out.cast` + `out.svg` |

`--font-size` accepts `1`–`128`. On the scenario path it overrides `render.font-size` for the written cast header and any SVG rendered in the same run. Duplicate `--font-size` is an error.

`--theme` selects a built-in preset (`dark` or `light`). On the scenario path it overrides `render.theme.preset`; individual `fg` / `bg` / `palette` keys in YAML still merge on top. The cast header stores resolved hex colors only. Duplicate `--theme` is an error.

When `[output]` is given, the stem is shared; only the extension differs (`.cast` / `.svg`).

`--format cast` is the default and matches existing behavior.

### `svg` subcommand

Convert an existing cast file without running a scenario:

```bash
scenario2cast svg [--font-size N] [--theme dark|light] <input.cast> [output.svg]
```

| Invocation | Output |
|---|---|
| `scenario2cast svg demo.cast` | `demo.svg` |
| `scenario2cast svg --font-size 20 demo.cast` | `demo.svg` (render-only override) |
| `scenario2cast svg demo.cast out.svg` | `out.svg` |
| `scenario2cast svg demo.cast out` | `out.svg` |

The input cast file is read-only. Only `.svg` is written.

`--font-size` overrides the cast header for SVG rendering only; the cast file is not modified. Precedence: CLI > `scenario2cast.font-size` in the cast header > default `16`.

`--theme` overrides the cast header `theme` for SVG rendering only; the cast file is not modified. Precedence: CLI preset > cast header `theme` > default `dark`.

#### Input casts

- Any [asciicast v2](https://docs.asciinema.org/manual/asciicast/v2/) file with `version: 2`.
- `width` and `height` are required in the cast header (`1`–`512` each).
- Render metadata comes from the cast header only:
  - `theme` (official header field) when present.
  - `scenario2cast.font-size` when present.
  - Defaults when absent (`font-size: 16`, `dark` preset).

#### Event handling

- Process `"o"` (output) events for SVG rendering.
- Process `"r"` (resize) events: update the terminal model to `{COLS}x{ROWS}`; invalid or out-of-range sizes (`1`–`512`) warn once and are skipped.
- Skip `"i"`, `"m"`, and other event codes with a stderr warning per unsupported code (warn-and-continue).

SVG canvas size is the maximum terminal width and height seen across the cast header and all valid `"r"` events. On shrink, cell content outside the new bounds is discarded.

The SVG `viewBox` uses this maximum size. During playback, an animated viewport mask and matching background rectangle switch to the current terminal size at each resize timestamp, so the visible terminal window grows or shrinks within the fixed coordinate system. Embed with a fixed HTML `width`/`height` and `preserveAspectRatio` (already set on the root `<svg>`) to keep page layout stable.

#### Failure behavior

| Situation | Behavior |
|---|---|
| Cast file not found | Exit non-zero |
| Invalid header JSON / `version` ≠ 2 | Exit non-zero; no SVG written |
| Missing `width` or `height` | Exit non-zero; no SVG written |
| `width` or `height` out of range (`1`–`512`) | Exit non-zero; no SVG written |
| Invalid event line JSON | Exit non-zero; no SVG written |
| SVG render failure | Exit non-zero; partial `.svg` deleted |
| Unsupported event codes | Warning only; continue |

Stderr on success:

```text
 Loading: /abs/path/demo.cast
 Written: /abs/path/demo.svg  (184 events, 16.7s)
 Done: /abs/path/demo.svg
```

## Execution Order

1. Resolve scenario settings, shell, cwd, deterministic seed, and deterministic timestamp.
2. Execute `pre` commands.
3. Execute and record `steps` into in-memory cast events.
4. Write the cast file (including render metadata in the header).
5. If `--format svg`, render SVG from the in-memory events and resolved render metadata.
6. Execute `post` commands.
7. Report final success or failure.

`post` runs even when SVG rendering fails, matching the rationale for running `post` after a cast write failure in the [pre/post spec](spec_pre_post.md): teardown should still run, and the cast recording remains useful.

## YAML Contract — `render:`

Display metadata lives in a dedicated top-level `render:` section, separate from `settings` (which controls recording behavior such as prompt and typing speed).

```yaml
title: "Basic Demo"
width: 80
height: 24

render:
  font-size: 16
  theme:
    preset: dark
    fg: "#d0d0d0"
    bg: "#282c34"
    palette: "#151515:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#d0d0d0:#505050:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#f5f5f5"
```

Presets: `dark` (default) and `light`. Set `preset:` to choose a base theme; optional `fg`, `bg`, and `palette` override individual keys.

### Defaults

When `render` is omitted or partially specified:

| Key | Default |
|---|---|
| `font-size` | `16` |
| `theme` | `dark` preset (`fg`, `bg`, `palette`) |

### Rationale

- `settings` is for how the session is recorded; `render` is for how the terminal is presented in SVG (and described in the cast header).
- `font-size` is not part of the official asciinema v2 header schema; it is stored under a `scenario2cast` extension object to avoid colliding with standard fields.

## Cast Header Contract

Every cast file includes render metadata in the header, whether or not `--format svg` is used. This keeps the cast self-describing for scenario2cast and allows downstream tools to read display intent. Tools that do not understand extension fields should ignore them.

```json
{
  "version": 2,
  "width": 80,
  "height": 24,
  "timestamp": 1702967308,
  "title": "Basic Demo",
  "env": { "SHELL": "...", "TERM": "xterm-256color" },
  "theme": {
    "fg": "#d0d0d0",
    "bg": "#282c34",
    "palette": "..."
  },
  "scenario2cast": {
    "font-size": 16
  }
}
```

- `theme` follows the [asciicast v2](https://docs.asciinema.org/manual/asciicast/v2/) optional header format (`fg`, `bg`, `palette`).
- `scenario2cast.font-size` is a scenario2cast-specific extension.

Terminal coloring in cast events (`highlight`, `run-highlight`, etc.) is defined in [spec_highlight.md](spec_highlight.md). Theme in the cast header controls the terminal canvas defaults for rendering; it is not a duplicate of per-step coloring.

## SVG Renderer Behavior (v1)

### ANSI support

| SGR | v1 support |
|---|---|
| 16-color foreground (`30`–`37`, `90`–`97`) | Yes |
| 16-color background (`40`–`47`, `100`–`107`) | Yes |
| `bold`, `underline`, `bright` | Yes |
| 256-color (`38;5;n`, `48;5;n`) | Yes — semicolon or colon form (`38:5:n`); palette 0–15 from `theme.palette`; 16–231 xterm cube; 232–255 grayscale |
| True color (`38;2;r;g;b`, `48;2;r;g;b`) | Yes — semicolon or colon form; mixed delimiters normalized; RGB 0–255; `bold` does not alter RGB |
| Invalid true-color RGB, 256 index, or malformed extended color | Warn once per type; render as default `theme.fg` / no background |

This matches the warn-and-continue philosophy used elsewhere in scenario2cast (see [spec_highlight.md](spec_highlight.md)).

### Animation

- Replay cast output events through a terminal state model.
- Capture a frame only when the visible screen changes; merge consecutive identical frames.
- Diff each frame into row-level layers; when a row changes, hide the previous layer and show the new one at the cast timestamp.
- Use CSS `animation-delay` with cast event times (not percentage keyframes) so short keystroke intervals remain visible in browsers.
- Encode row layers as SVG groups with `layer-in` / `layer-out` opacity animations (no JavaScript required).
- Timing follows cast event timestamps.

### Visual defaults

- Terminal background from `theme.bg`.
- Block cursor at the emulator cursor position when visible (`\e[?25l` hides it).
- Cursor visibility follows cast timestamps via row-layer-style show/hide; each position stays lit until the cursor moves or is hidden.
- Cursor uses `theme.fg` at 50% opacity (solid, no blink — matches agg/GIF export behavior).
- Monospace font stack; size from `scenario2cast.font-size`.

## Failure Behavior

### Scenario path (`--format svg`)

| Phase | On failure |
|---|---|
| `pre` | Fail-fast; cast and SVG are not written (unchanged). |
| `steps` | N/A — step exit codes do not stop recording (unchanged). |
| Cast write | Fail; SVG is not attempted. |
| SVG render | Cast file remains; partial `.svg` is deleted; exit non-zero; `post` still runs. |
| `post` | Fail-fast; cast (and SVG if written) remain (unchanged). |

Stderr on success with `--format svg`:

```text
Written: demo.cast  (142 events, 12.3s)
Written: demo.svg
Done: demo.cast, demo.svg
```

### `svg` subcommand

See [Failure behavior](#failure-behavior) under `svg` subcommand above. The cast file is never modified.

## Relationship to External Tools

- Cast files produced by scenario2cast remain valid asciinema v2 input for agg, asg, asciinema play, and similar tools.
- Built-in SVG output is a convenience path, not a replacement for the cast format or the wider asciinema ecosystem.
- External tools may ignore `scenario2cast` header extensions and apply their own CLI options (for example agg `--font-size`).

## Cross-Document Notes

- [spec_highlight.md](spec_highlight.md) lists cast-header `theme` control as out of scope for the coloring spec. That scope moves here: `render.theme` defines the terminal canvas for SVG and is mirrored into the cast header.
- [spec_pre_post.md](spec_pre_post.md) failure semantics for `post` after cast write inform SVG failure handling.

## References

- [asciicast v2](https://docs.asciinema.org/manual/asciicast/v2/) — cast header and event format.
- [agg usage](https://docs.asciinema.org/manual/agg/usage/) — external GIF conversion.
- [asg](https://github.com/kingsword09/asg) — external SVG conversion (reference architecture).

## Lessons learned

- Full-screen frame swapping with percentage-based `@keyframes` fails for cast typing intervals (~20ms): browsers skip narrow opacity windows. Row-level layers with `animation-delay` tied to cast timestamps reproduce typing reliably.
- Row-layer diffing must compare against an empty initial screen, not `null`, or the first frame creates spurious layers for every terminal row.
