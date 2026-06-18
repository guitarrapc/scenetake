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
- Animated SVG via CSS `animation-delay` row-layer opacity switching.
- Fixed dark terminal theme when `render.theme` is omitted.
- Default `font-size` of 16.
- No cursor rendering in v1.
- Warning-and-continue for unsupported 256-color SGR (`38;5;n`, `48;5;n`) during SVG rendering.
- Failure behavior aligned with `pre`/`post`: cast is retained, incomplete SVG is removed, `post` still runs.
- `svg` subcommand that converts an existing asciinema v2 cast file to SVG.

### Out of scope (v1)

- `--format gif` or agg integration.
- CLI `--font-size` override (use `render.font-size` in YAML or cast header).
- Cursor display and blink animation.
- Full 256-color ANSI palette support in the SVG renderer.
- Light theme presets beyond user-defined `render.theme`.
- SVG output without also writing the cast file (`--format svg` scenario path only; the `svg` subcommand writes SVG only).
- Resize cast events (`"r"`) during `svg` conversion.

### Planned for v2+

- 256-color ANSI support in the SVG renderer.
- CLI `--font-size` override for `scenario2cast svg` (useful for casts without `scenario2cast` header extensions).
- Cursor rendering.

## CLI Contract

```bash
scenario2cast [--verbose] [--format cast|svg] <scenario.yaml> [output]
```

| Invocation | Output |
|---|---|
| `scenario2cast demo.yaml` | `demo.cast` |
| `scenario2cast --format svg demo.yaml` | `demo.cast` + `demo.svg` |
| `scenario2cast --format svg demo.yaml out.svg` | `out.cast` + `out.svg` |
| `scenario2cast --format svg demo.yaml out.cast` | `out.cast` + `out.svg` |

When `[output]` is given, the stem is shared; only the extension differs (`.cast` / `.svg`).

`--format cast` is the default and matches existing behavior.

### `svg` subcommand

Convert an existing cast file without running a scenario:

```bash
scenario2cast svg <input.cast> [output.svg]
```

| Invocation | Output |
|---|---|
| `scenario2cast svg demo.cast` | `demo.svg` |
| `scenario2cast svg demo.cast out.svg` | `out.svg` |
| `scenario2cast svg demo.cast out` | `out.svg` |

The input cast file is read-only. Only `.svg` is written.

#### Input casts

- Any [asciicast v2](https://docs.asciinema.org/manual/asciicast/v2/) file with `version: 2`.
- `width` and `height` are required in the cast header.
- Render metadata comes from the cast header only:
  - `theme` (official header field) when present.
  - `scenario2cast.font-size` when present.
  - Defaults when absent (`font-size: 16`, fixed dark theme).

#### Event handling

- Process `"o"` (output) events for SVG rendering.
- Skip `"i"`, `"m"`, `"r"`, and other event codes with a stderr warning per unsupported code (warn-and-continue).

#### Failure behavior

| Situation | Behavior |
|---|---|
| Cast file not found | Exit non-zero |
| Invalid header JSON / `version` ≠ 2 | Exit non-zero; no SVG written |
| Missing `width` or `height` | Exit non-zero; no SVG written |
| Invalid event line JSON | Exit non-zero; no SVG written |
| SVG render failure | Exit non-zero; partial `.svg` deleted |
| Unsupported event codes | Warning only; continue |

Stderr on success:

```text
Loading: demo.cast
Written: demo.svg  (184 events, 16.7s)
Done: demo.svg
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
    fg: "#d0d0d0"
    bg: "#282c34"
    palette: "#151515:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#d0d0d0:#505050:#ac4142:#7e8e50:#e5b567:#6c99bb:#9f4e85:#7dd6cf:#f5f5f5"
```

### Defaults

When `render` is omitted or partially specified:

| Key | Default |
|---|---|
| `font-size` | `16` |
| `theme` | Fixed dark theme (`fg`, `bg`, `palette` chosen by scenario2cast) |

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
| 256-color (`38;5;n`, `48;5;n`) | No — render as default `theme.fg` / no background, emit stderr warning |

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
- No block cursor in v1.
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
