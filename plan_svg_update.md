# SVG Renderer Update Plan

Status: **v1 implemented** · **v2 window chrome implemented**

## Goal

Raise the built-in SVG renderer to **console2svg-level** terminal emulation and **full-frame** animation, shared by the `svg` subcommand and `--format svg`. scenario2cast continues to record commands via **pipes** (no PTY). Rich TUI demos (`copilot --banner`, `sl`) are expected via **external asciinema casts**, not the scenario path.

Reference behavior is documented from `.references/console2svg` (read-only; **no code copy**).

## Decisions (grill-me summary)

### v1 (done)

| Topic | Decision |
|-------|----------|
| Target quality | console2svg-level VT emulator + row-diff animated SVG |
| PTY recording | **Out of scope** — scenario path stays pipe-based |
| Scope | Shared renderer for `svg` + `--format svg` |
| Window chrome | Plain terminal (8px outer padding only) |
| Tests | Unit tests (CSI / Unicode) + smoke fixtures |
| Samples | Regenerate `samples/*.svg` |
| Implementation order | Emulator → SVG → tests → samples |

### v2 window chrome (planned)

| Topic | Decision |
|-------|----------|
| Default | **`none`** — chrome only when `render.window` is set |
| Presets | **`macos`**, **`windows`** |
| Chrome colors | Auto from `render.theme.preset` (`dark` / `light`); fixed palette per `(window, preset)` pair; `theme.fg` / `theme.bg` overrides affect terminal content only |
| Title bar text | **None** — traffic lights / Windows buttons only |
| Config surface | scenario `render.window` + v3 cast tag + CLI `--window` |
| Priority | **CLI > cast header > default `none`** |
| Cast header | v3 `tags`: `s2c:window=macos` (omit when `none`) |
| v2 cast header | **No scenario2cast-specific fields** — ignore legacy `scenario2cast` block on read; v2 uses `width` / `height` / `theme` only |
| Resize | Chrome resizes with viewport; title bar height fixed (scale with `font-size`) |
| Resize animation | Chrome rects tied to existing `viewport-*` layer show/hide timing |
| Decoration | Rounded corners + light drop shadow (`macos` larger radius; `windows` smaller) |
| Layout | Chrome **replaces** outer 8px padding; inner terminal padding unchanged; shadow margin in metrics |
| Invalid values | **Error** (same strictness as `--theme`) |
| Samples | Keep `theme.yaml` (colors only); add `theme-macos.yaml` / `theme-windows.yaml` (`preset: dark`, same steps as `theme.yaml`) |
| Tests | Resolver unit tests + structural SVG asserts (chrome elements present, dimensions shift) |
| Implementation order | Resolver + cast write/read → `WindowChrome` metrics/draw → tests → samples → specs |

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
| `Terminal.cs` | VT emulator + replay (`TerminalTheme`, `ScreenBuffer`, `AnsiParser`, `TerminalReplay`) |
| `CastReader.cs` | Cast parsing (v2/v3) + render metadata extraction |
| `Svg.cs` | SVG renderer + render settings resolver (`SvgRender`, `SvgFrameRenderer`, `RenderSettingsResolver`) |

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
- Window chrome (`render.window: macos` etc.) — **v2**
- Matrix rain contextual tint, crop, command header line
- `loop` / `video-fps` CLI knobs (fixed sensible defaults)

## v2 — Window chrome

Terminal-style window frame around SVG output for README embeds.

### Configuration

```yaml
render:
  window: macos   # macos | windows (omit or none = plain terminal)
  theme:
    preset: dark    # drives chrome palette (dark / light)
```

CLI: `--window macos|windows|none` on both scenario (`--format svg`) and `svg` subcommand.

Cast write (v3 only): append `s2c:window=macos` to `tags` when not `none`.

### Rendering

```
┌─ shadow margin ─────────────────────────┐
│  ╭─ title bar (buttons only) ───────╮  │
│  │ ● ● ●                            │  │
│  ╰───────────────────────────────────╯  │
│  ┌─ terminal viewport (theme.bg) ───┐  │
│  │  inner padding + cell content    │  │
│  └──────────────────────────────────┘  │
└───────────────────────────────────────┘
```

- `WindowChromeTheme.For(window, preset)` — four built-in palettes (`macos`×2 + `windows`×2).
- `SvgMetrics` gains chrome offsets: title bar height ≈ `fontSize * 1.75`, shadow margin, corner radius per preset.
- `window: none` path unchanged (current `Padding = 8` behavior).
- Chrome `<rect>` / button `<circle>` elements use the same `viewport-*` animation classes as terminal background.

### Cast header cleanup (v2)

Remove v2 `scenario2cast` read support from `CastReader` and drop it from `spec_cast.md`. v2 casts remain readable for events and terminal size; render metadata on v2 is CLI-only.

### Samples

| File | Purpose |
|------|---------|
| `samples/theme.yaml` | Color demo (`preset: light`) — unchanged |
| `samples/theme-macos.yaml` | `window: macos`, `preset: dark`, same steps as `theme.yaml` |
| `samples/theme-windows.yaml` | `window: windows`, `preset: dark`, same steps as `theme.yaml` |

Regenerate via `dotnet run samples/regenerate.cs` after implementation.

### Specs to update

- `.github/docs/spec_scenario.md` — `render.window`
- `.github/docs/spec_cli.md` — `--window`
- `.github/docs/spec_cast.md` — `s2c:window` tag; remove v2 `scenario2cast` object
- `.github/docs/spec_svg.md` — window chrome section (move from “out of scope v1”)

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
