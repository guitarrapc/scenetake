# Cast Output Specification

Status: **Implemented**

## Motivation

The `.cast` file is scenetake's canonical artifact. README embeds, [agg](https://docs.asciinema.org/manual/agg/) GIFs, built-in SVG, and `asciinema play` all consume the same recording. Aligning with [asciicast v3](https://docs.asciinema.org/manual/asciicast/v3/) keeps scenetake compatible with the current asciinema ecosystem while older v2 files remain readable for conversion.

## Scope

### In scope

- Cast files produced by the scenario path (always asciicast v3).
- Cast files accepted as read-only input (asciicast v2 or v3) by the `svg` subcommand. CLI: [spec_cli.md](spec_cli.md).
- Header metadata mapped from scenario YAML and CLI overrides.
- Event codes and timing semantics scenetake writes and understands.
- What is and is not recorded into the cast.

### Out of scope

- SVG renderer behavior — [spec_svg.md](spec_svg.md).
- YAML key placement and recording timing defaults — [spec_scenario.md](spec_scenario.md).
- ANSI coloring value formats — [spec_highlight.md](spec_highlight.md). Coloring appears in cast events but is defined there.
- `pre` / `post` runtime semantics — [spec_pre_post.md](spec_pre_post.md).

## Version Policy

| Direction | Versions | Notes |
|---|---|---|
| Write (scenario path) | v3 only | v2 cast files are not produced. |
| Read (`svg` subcommand) | v2, v3 | v1 and unknown versions are rejected. |

v2 and v3 differ in header shape and event time semantics (see [Event time](#event-time) below). Official references: [asciicast v3](https://docs.asciinema.org/manual/asciicast/v3/), [asciicast v2](https://docs.asciinema.org/manual/asciicast/v2/).

## Header

Every cast file written by the scenario path includes render metadata in the header, whether or not SVG is generated. Values come from scenario `render:` with CLI overrides on the scenario path. YAML keys: [spec_scenario.md](spec_scenario.md) → `render`.

```json
{
  "version": 3,
  "term": {
    "cols": 80,
    "rows": 24,
    "type": "xterm-256color",
    "theme": { "fg": "#d0d0d0", "bg": "#282c34", "palette": "..." }
  },
  "timestamp": 1701960613,
  "title": "My Demo",
  "env": { "SHELL": "bash" },
  "tags": ["st:font-size=16", "st:font-family=ui-monospace, \"Cascadia Mono\", monospace"]
}
```

| Field | Source | Notes |
|---|---|---|
| `version` | fixed `3` | |
| `term.cols`, `term.rows` | scenario `width`, `height` | Valid range `1`–`512` each. |
| `term.type` | fixed `xterm-256color` | |
| `term.theme` | scenario `render.theme` (+ CLI preset override) | Resolved hex. Follows asciicast v3 `term.theme`. |
| `timestamp` | deterministic from YAML | See [spec_scenario.md](spec_scenario.md) → Determinism. |
| `title` | scenario `title` | May be empty. |
| `env.SHELL` | resolved shell's file name | Name only, without its folder and without `.exe`: `bash`, `pwsh`, `cmd`, also when the scenario gives a path. A cast is shared, and the folder is often under the user's home (`C:\Users\<name>\scoop\...`). `TERM` is represented by `term.type`. |
| `tags` | `st:font-size=N`, `st:font-family=…`, `st:window=macos\|windows` | SVG render metadata. `st:window` is omitted when window chrome is off. Unknown tags are ignored by external tools. |

On read, v2 headers use top-level `width`, `height`, and `theme` only. Render metadata defaults apply unless overridden by CLI: v3 `st:font-size` / `st:font-family` / `st:window` in `tags`. Invalid `st:` render tags warn once each and fall back to defaults. An invalid `theme` color, or a palette that is not 16 colors, falls back to the defaults without a warning. A missing or out-of-range terminal size is an error. This holds wherever a cast is read: the `svg` and `mp4` subcommands, and the scenario path's SVG and mp4 output.

## Event Stream

### Codes scenetake writes

| Code | When | Notes |
|---|---|---|
| `o` | Output to terminal | Prompt, typed characters, command output, styled comment lines. ANSI from [spec_highlight.md](spec_highlight.md). |
| `r` | Terminal resize | When produced by scenario content. Data: `"{cols}x{rows}"`. |
| `m` | Step marker | Emitted immediately before a `name` comment when the step has `name`. Label is plain display text (style prefix stripped). |
| `x` | Session end | Always the final event. Data `"0"`. Does not reflect individual step exit codes. |

Codes not written: `i` (keyboard input).

### Event time

- **v3 (write):** events store seconds since the previous event, rounded to milliseconds. Rounding error carries forward to prevent drift. The sum of intervals equals the event's absolute time rounded to a millisecond. Each interval therefore equals the difference between two rounded absolute times. A fractional-millisecond shift can change intervals by 1ms ([spec_scenario.md](spec_scenario.md) → Determinism).
- **v2 (read):** timestamps are absolute seconds from session start.
- **v3 (read):** readers sum intervals to obtain absolute times.

v3 comment lines (`# ...`) are ignored on read.

### Sequences left out

`o` events omit OSC sequences (`ESC ]` … `BEL` or `ESC \`) except those for colors. These sequences draw no text in terminals or scenetake's renderers. Several can expose details about the recording machine:

| OSC | Carries |
|---|---|
| `0`, `1`, `2` (titles) | ConPTY sets the title to the shell's full path. Vim puts the edited file's folder in it. |
| `7`, `9;9` (working directory) | Host name and folder, from shells set up for Windows Terminal, VS Code, and others. |
| `133`, `633` (shell integration) | Prompt marks, and in VS Code's form the working directory. |
| `8` (hyperlinks) | The link target, such as `file://host/home/user/…` from `ls --hyperlink`. The link text stays. |
| `52` (clipboard) | Clipboard content. |

Color OSCs remain: `4`, `5`, `10`–`19`, `104`, `105`, `110`–`119`. Scenetake removes all other OSCs, including sequences without a number. It tracks sequences across PTY event boundaries. Empty events are removed, except the final event, which preserves recording length. Only 7-bit sequences are recognized. Visible command output, including `pwd` and prompts with paths, remains unchanged.

### Recording boundary

Only `steps` content is recorded.

| Phase | In cast? |
|---|---|
| `pre` | No — stdout/stderr appear in the CLI only. |
| `steps` | Yes — simulated typing, command output, markers, comments. |
| `post` | No — runs last and is never recorded. |

Step exit codes during recording: [spec_scenario.md](spec_scenario.md) → Step exit codes.

## External Tool Compatibility

Cast files are intended as standard asciicast v3 input for `asciinema play`, [agg](https://docs.asciinema.org/manual/agg/) (GIF), [asg](https://github.com/kingsword09/asg) (SVG), and similar tools. External tools ignore unknown `tags`.

**agg** requires **1.6.0 or later** for v3 casts. Older releases and the `kayvan/agg` Docker image (agg 1.4.0) support only asciicast v2. Use `ghcr.io/asciinema/agg` for v3 casts.

Adding v2-only header fields (such as top-level `width` / `height`) to v3 files does not restore compatibility with pre-1.6.0 agg: those tools still mis-handle v3 relative intervals.

## Cross-Document Notes

- [spec_scenario.md](spec_scenario.md) — YAML input, execution order, determinism.
- [spec_cli.md](spec_cli.md) — output paths, `--font-size`, `--theme`, `svg` subcommand.
- [spec_highlight.md](spec_highlight.md) — ANSI coloring written into `o` events.
- [spec_pre_post.md](spec_pre_post.md) — `pre` / `post` vs recording. Cast write timing.
- [spec_svg.md](spec_svg.md) — how the built-in renderer consumes cast events.

## Lessons Learned

- Cast is the stable interchange format. SVG and GIF are derived views. Keeping cast spec separate from renderers avoids duplicating header rules in every output spec.
- v3 adoption in downstream tools is not uniform. Documenting minimum agg version prevents false "invalid cast" reports when the file is valid v3.
- Top-level v2 compatibility fields on a v3 file parse in old agg but produce broken GIFs because event timing semantics differ.
- **A broken GIF is not always the cast or agg.** Docker Desktop 28.5.1 with WSL2 sometimes corrupted GIFs written by agg directly to Windows bind mounts. The files contained unrelated byte runs of about 4 KiB. Viewers then showed noise bands and stale glyphs that neither the SVG nor a single-frame agg render had. agg's output was byte-identical across runs when written inside the container and copied out with `docker cp`, so the README's Windows agg commands do that.
- **Invisible metadata exposed local paths.** Windows casts stored the full shell path in `env.SHELL` and ConPTY titles, often under `C:\Users\<name>\scoop\...`. Players and renderers did not show these paths. Linux headers kept the shell name as written, and bash PTY sessions set no title. Headers now store only the shell name, and casts omit non-color OSCs. This preserves the rendered output. Relative paths would add no display value, and a title path may name the shell installation rather than the working directory.
- **Validation must accept generated metadata.** The default font stack has 11 families, but validation allowed only 10. Every conversion warned and used the same default stack instead. The limit is now 16. A test checks the default stack.
