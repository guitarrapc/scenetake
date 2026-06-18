# Cast Output Specification

Status: **Implemented**

## Motivation

The `.cast` file is scenario2cast's canonical artifact. README embeds, [agg](https://docs.asciinema.org/manual/agg/) GIFs, built-in SVG, and `asciinema play` all consume the same recording. Aligning with [asciicast v3](https://docs.asciinema.org/manual/asciicast/v3/) keeps scenario2cast compatible with the current asciinema ecosystem while older v2 files remain readable for conversion.

## Scope

### In scope

- Cast files produced by the scenario path (always asciicast v3).
- Cast files accepted as read-only input (asciicast v2 or v3) by the `svg` subcommand. CLI: [spec_cli.md](spec_cli.md).
- Header metadata mapped from scenario YAML and CLI overrides.
- Event codes and timing semantics scenario2cast writes and understands.
- What is and is not recorded into the cast.

### Out of scope

- SVG renderer behavior — [spec_svg.md](spec_svg.md).
- YAML key placement and recording timing defaults — [spec_scenario.md](spec_scenario.md).
- ANSI coloring value formats — [spec_highlight.md](spec_highlight.md); coloring appears in cast events but is defined there.
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
  "env": { "SHELL": "/bin/bash" },
  "tags": ["s2c:font-size=16", "s2c:font-family=ui-monospace, \"Cascadia Mono\", monospace"]
}
```

| Field | Source | Notes |
|---|---|---|
| `version` | fixed `3` | |
| `term.cols`, `term.rows` | scenario `width`, `height` | Valid range `1`–`512` each. |
| `term.type` | fixed `xterm-256color` | |
| `term.theme` | scenario `render.theme` (+ CLI preset override) | Resolved hex; follows asciicast v3 `term.theme`. |
| `timestamp` | deterministic from YAML | See [spec_scenario.md](spec_scenario.md) → Determinism. |
| `title` | scenario `title` | May be empty. |
| `env.SHELL` | resolved shell | `TERM` is represented by `term.type`. |
| `tags` | `s2c:font-size=N`, `s2c:font-family=…` | SVG font size (`1`–`128`) and CSS `font-family` string. Unknown tags are ignored by external tools. |

On read, v2 headers use top-level `width`, `height`, and `theme` instead. Render metadata defaults apply unless overridden by CLI or header fields: v3 `s2c:font-size` / `s2c:font-family` in `tags`; v2 `scenario2cast.font-size` / `scenario2cast.font-family`. Invalid header values warn once and fall back to defaults when read by the `svg` subcommand.

## Event Stream

### Codes scenario2cast writes

| Code | When | Notes |
|---|---|---|
| `o` | Output to terminal | Prompt, typed characters, command output, styled comment lines. ANSI from [spec_highlight.md](spec_highlight.md). |
| `r` | Terminal resize | When produced by scenario content. Data: `"{cols}x{rows}"`. |
| `m` | Step marker | Emitted immediately before a `name` comment when the step has `name`. Label is plain display text (style prefix stripped). |
| `x` | Session end | Always the final event; data `"0"`. Does not reflect individual step exit codes. |

Codes not written: `i` (keyboard input).

### Event time

- **v3 (write):** events use relative intervals (seconds since the previous event). Intervals are quantized to millisecond precision; rounding error is carried forward so long recordings do not drift.
- **v2 (read):** timestamps are absolute (seconds from session start).
- **v3 (read):** intervals are accumulated to absolute time for internal consumers.

v3 comment lines (`# ...`) are ignored on read.

### Recording boundary

Only `steps` content is recorded.

| Phase | In cast? |
|---|---|
| `pre` | No — stdout/stderr appear in the CLI only. |
| `steps` | Yes — simulated typing, command output, markers, comments. |
| `post` | No — runs after the cast file is written. |

Step command exit codes are captured during execution but do not stop later steps and do not determine scenario2cast's exit code. See [spec_pre_post.md](spec_pre_post.md).

## External Tool Compatibility

Cast files are intended as standard asciicast v3 input for `asciinema play`, [agg](https://docs.asciinema.org/manual/agg/) (GIF), [asg](https://github.com/kingsword09/asg) (SVG), and similar tools. External tools ignore unknown `tags`.

**agg** requires **1.6.0 or later** for v3 casts. Older releases and the `kayvan/agg` Docker image (agg 1.4.0) support only asciicast v2. Use `ghcr.io/asciinema/agg` for v3 casts.

Adding v2-only header fields (such as top-level `width` / `height`) to v3 files does not restore compatibility with pre-1.6.0 agg: those tools still mis-handle v3 relative intervals.

## Cross-Document Notes

- [spec_scenario.md](spec_scenario.md) — YAML input, execution order, determinism.
- [spec_cli.md](spec_cli.md) — output paths, `--font-size`, `--theme`, `svg` subcommand.
- [spec_highlight.md](spec_highlight.md) — ANSI coloring written into `o` events.
- [spec_pre_post.md](spec_pre_post.md) — `pre` / `post` vs recording; cast write timing.
- [spec_svg.md](spec_svg.md) — how the built-in renderer consumes cast events.

## Lessons Learned

- Cast is the stable interchange format; SVG and GIF are derived views. Keeping cast spec separate from renderers avoids duplicating header rules in every output spec.
- v3 adoption in downstream tools is not uniform; documenting minimum agg version prevents false "invalid cast" reports when the file is valid v3.
- Top-level v2 compatibility fields on a v3 file parse in old agg but produce broken GIFs because event timing semantics differ.
