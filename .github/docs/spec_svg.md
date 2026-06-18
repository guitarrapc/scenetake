# SVG Output Specification

Status: **Implemented**

## Motivation

scenario2cast generates asciinema v2 cast files. For README and documentation embeds, animated SVG is often preferable to GIF: smaller file size, crisp scaling, and easy web embedding without a player.

External tools such as [agg](https://docs.asciinema.org/manual/agg/) (GIF) and [asg](https://github.com/kingsword09/asg) (SVG) can convert cast files, but requiring a second install and a second command adds friction. Built-in SVG output lets users produce `.cast` and `.svg` in one invocation while keeping cast as the canonical artifact.

## Scope

- SVG output via `--format svg` or the `svg` subcommand. CLI: [spec_cli.md](spec_cli.md). Scenario `render:` keys: [spec_scenario.md](spec_scenario.md).
- Cast header: official `theme` plus `scenario2cast.font-size` extension.
- Render metadata is written to the cast header on every run, regardless of output format.
- Built-in C# SVG renderer (no bundled external binary).
- ANSI SGR: 16-color, 256-color (xterm palette; indices 0–15 from `theme.palette`), true color (`38;2` / `48;2` and colon forms); `bold`, `underline`, `bright`.
- Animated SVG driven by cast event timestamps; no JavaScript required.
- Theme presets `dark` (default) and `light`; default `font-size` of `16`.
- Block cursor (`theme.fg` at 50% opacity, no blink); DECTCEM visibility (`\e[?25h`, `\e[?25l`).
- Warn-and-continue for malformed extended color SGR and unsupported cast event codes.
- Resize cast events (`"r"`) during `svg` conversion.

## Cast Header Contract

Every cast file includes render metadata in the header, whether or not SVG is generated. Values come from scenario `render:` (with CLI overrides on the scenario path).

```json
{
  "version": 2,
  "width": 80,
  "height": 24,
  "theme": { "fg": "#d0d0d0", "bg": "#282c34", "palette": "..." },
  "scenario2cast": { "font-size": 16 }
}
```

- `theme` follows the [asciicast v2](https://docs.asciinema.org/manual/asciicast/v2/) optional header format.
- `scenario2cast.font-size` is a scenario2cast extension (not in the official schema).
- Terminal coloring in cast events: [spec_highlight.md](spec_highlight.md). Header `theme` sets canvas defaults for rendering.

## `svg` Subcommand — Input and Events

- Accepts asciicast v2 with required header `width` and `height` (`1`–`512` each). The input cast is read-only.
- Render metadata comes from the cast header; CLI overrides are render-only. See [spec_cli.md](spec_cli.md).
- Processes `"o"` (output) and `"r"` (resize) events. Skips `"i"`, `"m"`, and other codes with a stderr warning (warn-and-continue).
- Invalid or out-of-range resize sizes warn once and are skipped.
- Canvas size is the maximum terminal dimensions across the header and all valid `"r"` events. On shrink, content outside the new bounds is discarded. Playback shows the current terminal size within that fixed canvas so embed layout stays stable (`preserveAspectRatio` on the root `<svg>`).

## Renderer Requirements

### ANSI

| Category | v1 support |
|---|---|
| 16-color fg/bg (`30`–`37`, `40`–`47`, bright variants) | Yes |
| `bold`, `underline`, `bright` | Yes |
| 256-color (`38;5;n`, `48;5;n`, colon form) | Yes — palette 0–15 from `theme.palette`; 16–231 cube; 232–255 grayscale (same formula as [agg `theme.rs`](https://github.com/asciinema/agg/blob/main/src/theme.rs)) |
| True color (`38;2`, `48;2`, colon form; mixed `;` / `:` delimiters) | Yes — RGB 0–255 |
| Invalid extended color | Warn once per type; render as default fg / no background |

Warn-and-continue matches [spec_highlight.md](spec_highlight.md).

### Animation and visuals

- Replay follows cast event timestamps; short keystroke intervals must remain visible in browsers.
- Output is self-contained animated SVG (CSS only, no JavaScript).
- Background from `theme.bg`; monospace font at `scenario2cast.font-size`.
- Block cursor at the emulator position when visible; hidden by `\e[?25l`.

## Failure Behavior

### Scenario path (`--format svg`)

| Phase | On failure |
|---|---|
| `pre` | Fail-fast; no cast or SVG. See [spec_pre_post.md](spec_pre_post.md). |
| Cast write | Fail; SVG not attempted. |
| SVG render | Cast retained; partial `.svg` deleted; exit non-zero; `post` still runs. |
| `post` | Fail-fast; cast (and SVG if written) remain. See [spec_pre_post.md](spec_pre_post.md). |

Execution order: [spec_scenario.md](spec_scenario.md).

### `svg` subcommand

| Situation | Behavior |
|---|---|
| Cast not found, invalid header, missing/out-of-range `width`/`height`, invalid event JSON | Exit non-zero; no SVG |
| SVG render failure | Exit non-zero; partial `.svg` deleted |
| Unsupported event codes, invalid resize, malformed color SGR | Warning only; continue |

The cast file is never modified by the `svg` subcommand.

## Cross-Document Notes

- [spec_scenario.md](spec_scenario.md) — `render:` YAML keys and execution order.
- [spec_cli.md](spec_cli.md) — commands, options, logging, exit codes.
- [spec_highlight.md](spec_highlight.md) — cast-event coloring (header `theme` is separate).

Cast files remain valid asciinema v2 input for agg, asg, asciinema play, and similar tools. External tools may ignore `scenario2cast` header extensions.

## References

- [asciicast v2](https://docs.asciinema.org/manual/asciicast/v2/)
- [agg usage](https://docs.asciinema.org/manual/agg/usage/)
- [asg](https://github.com/kingsword09/asg)

## Lessons Learned

- Percentage-based CSS keyframes skip narrow opacity windows (~20ms typing intervals). Timing must follow cast timestamps directly.
- First-frame diffing must treat the initial screen as empty; otherwise every row spawns a spurious layer.
