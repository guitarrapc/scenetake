# SVG Renderer Update Plan

Status: **Implemented**

## Goal

Raise the built-in SVG renderer to **console2svg-level** terminal emulation and **full-frame** animation, shared by the `svg` subcommand and `--format svg`. scenario2cast continues to record commands via **pipes** (no PTY). Rich TUI demos (`copilot --banner`, `sl`) are expected via **external asciinema casts**, not the scenario path.

Reference behavior is documented from `.references/console2svg` (read-only; **no code copy**).

## Decisions (grill-me summary)

| Topic | Decision |
|-------|----------|
| Target quality | console2svg-level VT emulator + full-frame SVG |
| PTY recording | **Out of scope** — scenario path stays pipe-based |
| Scope | Shared renderer for `svg` + `--format svg` |
| Animation | **Full-frame** with visual-hash `<defs>` dedup |
| Window chrome | **v2** — v1 is plain terminal (padding only) |
| Tests | Unit tests (CSI / Unicode) + golden cast fixtures |
| Samples | Regenerate `samples/*.svg` in v1 |
| Implementation order | Emulator → SVG → tests → samples |

## Architecture

```
CastEvent[] ──► TerminalReplay ──► ReplayFrame[] (ScreenBuffer snapshots)
                                        │
                                        ▼
                              SvgFrameRenderer ──► animated SVG
```

### New modules

| File | Responsibility |
|------|----------------|
| `Terminal/TerminalTheme.cs` | Map `ResolvedTheme` → palette + default colors |
| `Terminal/ScreenBuffer.cs` | Cell grid, scroll region, alt screen, scrollback, wide chars |
| `Terminal/AnsiParser.cs` | CSI / ESC / Unicode (console2svg feature set) |
| `Terminal/TerminalReplay.cs` | Replay cast events → timed frame list |
| `Svg/SvgFrameRenderer.cs` | Full-frame SVG, CSS keyframes, defs dedup |
| `SvgRender.cs` | `WriteSvg`, theme presets, render settings resolver |

### VT emulator scope (v1)

Match console2svg reference coverage:

- CSI: cursor (`A`–`F`, `G`, `H`, `d`), erase (`J`, `K`, `X`), insert/delete (`@`, `P`, `L`, `M`, `S`, `T`), scroll region (`r`), save/restore (`s`, `u`, `ESC 7/8`)
- Private: alternate screen `?1049h/l`, cursor visibility `?25h/l`
- SGR: bold, faint, italic, underline, reverse, 16/256/true color (`;` and `:` forms)
- Unicode: wide chars, surrogate pairs, combining marks, variation selectors, zero-width skip
- Skip: OSC, DCS, charset designations; caret-notation OSC from PTY echo
- Block elements (U+2580–U+259F): rect-based drawing in SVG

### Animation

- One frame per screen change (after each output/resize event batch)
- CSS `@keyframes` with **percentage** timing from cast timestamps (not row-layer opacity)
- Duplicate visual frames share one `<defs>` entry via FNV hash
- Collapse identical consecutive frames; trim trailing blank alt-screen restore frames
- Final frame hold: one minimum frame interval after last event

### Out of scope (v1)

- PTY / `asciinema rec` integration in scenario path
- Window chrome (`render.window: macos` etc.)
- Matrix rain contextual tint, crop, command header line
- `loop` / `video-fps` CLI knobs (fixed sensible defaults)

## Testing

| Layer | Approach |
|-------|----------|
| Emulator | `tests/terminal_tests.cs` — CSI, Unicode, alt screen (console2svg test matrix, own assertions) |
| E2E | `tests/fixtures/*.cast` smoke + optional golden casts for TUI |

Run: `dotnet run tests/terminal_tests.cs`

## Migration

- Replace row-layer SVG output entirely (breaking visual change)
- Regenerate: `dotnet run samples/regenerate.cs`
- Update `.github/docs/spec_svg.md`

## Rich TUI workflow (documented)

```bash
asciinema rec demo.cast
scenario2cast svg demo.cast
```
