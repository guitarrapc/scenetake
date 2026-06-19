# SVG Renderer Update Plan

Status: **v1 done** · **v2 window chrome done** · see [spec_svg.md](.github/docs/spec_svg.md) for current behavior

## Goal

Raise the built-in SVG renderer terminal emulation and animated SVG, shared by the `svg` subcommand and `--format svg`. scenetake continues to record commands via **pipes** (no PTY). Rich TUI demos (`copilot --banner`, `sl`) are expected via **external asciinema casts**, not the scenario path.

Reference behavior is documented from `.references/console2svg` (read-only; **no code copy**).

## Completed

| Milestone | Summary |
|-----------|---------|
| **v1 — VT + row-diff SVG** | `Terminal.cs`, `CastReader.cs`, `Svg.cs`; row-diff layers, `--max-fps`, resize viewport, theme/font CLI |
| **v2 — Window chrome** | `render.window` / `--window` / `st:window`; `macos` and `windows` presets; fixed-px chrome; samples `theme-macos.yaml` / `theme-windows.yaml`; `tests/window_chrome_test.cs` |
| **Cast header cleanup** | v2 `scenetake` object no longer read; render metadata on v2 is CLI-only |
| **Specs** | [spec_svg.md](.github/docs/spec_svg.md), [spec_scenario.md](.github/docs/spec_scenario.md), [spec_cli.md](.github/docs/spec_cli.md), [spec_cast.md](.github/docs/spec_cast.md) |

## Remaining / not planned (v3+)

| Item | Notes |
|------|-------|
| PTY / `asciinema rec` in scenario path | Still out of scope; external casts + `svg` subcommand |
| Matrix rain contextual tint | Deferred |
| Crop / command header line in SVG | Deferred |
| `loop` / `video-fps` CLI knobs | Deferred; `--max-fps` covers sampling |
| `linux` window chrome preset | Not in v2 scope |
| `render.window-title` | Title bar is buttons-only by design; custom title is a future opt-in if needed |
| Golden SVG fixtures | Structural asserts in `window_chrome_test.cs` only; full pixel/line golden diffs not added |

No open implementation tasks for v1 or v2 unless a bug is found.

## Design decisions (archive)

### v1

| Topic | Decision |
|-------|----------|
| Target quality | VT emulator + **row-diff** animated SVG (not full-frame `<defs>` dedup) |
| PTY recording | Out of scope — scenario path stays pipe-based |
| Scope | Shared renderer for `svg` + `--format svg` |
| Window chrome (v1) | Plain terminal (8px outer padding only) |

### v2 window chrome

| Topic | Decision |
|-------|----------|
| Default | `none` — chrome only when `render.window` is set |
| Presets | `macos`, `windows` |
| Chrome colors | Inferred from `theme.bg` luminance → dark/light chrome palette; `theme.fg` / `theme.bg` overrides affect terminal content only |
| Title bar text | None — traffic lights / Windows buttons only |
| Config | scenario `render.window` + v3 `st:window` tag + CLI `--window`; priority CLI > cast header > default |
| Chrome geometry | Fixed px (does not scale with `font-size`); see [spec_svg.md](.github/docs/spec_svg.md) |
| Resize | Chrome resizes with viewport; same `viewport-*` animation timing as terminal background |
| Invalid values | Error (scenario / CLI); warn + `none` on invalid cast tag |
| Samples | `theme.yaml` unchanged; added `theme-macos.yaml`, `theme-windows.yaml` |

## Architecture (pointer)

```
CastEvent[] ──► TerminalReplay ──► ReplayFrame[] ──► SvgFrameRenderer ──► animated SVG
```

Modules: `Terminal.cs`, `CastReader.cs`, `Svg.cs`. Behavior: [spec_svg.md](.github/docs/spec_svg.md).

## Rich TUI workflow

```bash
asciinema rec demo.cast
scenetake svg demo.cast --window macos
```

## Lessons learned (planning)

- Row-diff animation shipped instead of full-frame `<defs>` dedup — smaller files for typing-heavy casts; plan originally assumed full-frame.
- Window chrome should use **fixed px** like real OS windows, not `font-size` scaling.
- Title bar fill needs **top-only** corner radius; uniform `<rect rx>` leaves a visible gap above the terminal viewport.
- Chrome layout constants were tuned iteratively (button size, side/top padding, title bar height); document final values in spec, not in this plan.
