# Plan B — mp4 Output

Status: **B0–B4 and B6 implemented** (2026-10-29). B5 (window chrome) is open. The shipped behavior is in [spec_mp4.md](../spec_mp4.md); see [What Was Built](#what-was-built).

Background, measurements, and ordering: [plan_output_size.md](plan_output_size.md).

## Goals

- scenetake writes H.264 mp4 that X, GitHub, and Slack accept without re-encoding, both from the scenario path and from existing casts.
- Heavy TUIs at large terminal sizes, from 200×60 up to 4K, render with bounded memory.
- The video looks like the SVG output: same theme, cell grid, cursor, and colors.
- The only extra tool is `ffmpeg`. Cast and SVG output keep working without it.

## Non-Goals (v1)

- A built-in encoder.
- Audio, color emoji, ligatures, and transparent backgrounds.
- HEVC, AV1, WebM, or GIF output. The pipe makes these easy later.

## Architecture

```
cast events ──► VT emulator (ScreenBuffer / AnsiParser, existing)
                  │  advance to t = k / fps
                  ▼
                frame rasterizer (new: StbTrueTypeSharp glyphs + procedural box/block cells)
                  │  RGB24 frame, one reusable buffer
                  ▼
                ffmpeg stdin ──► libx264, yuv420p ──► <out>.mp4 (temp file, renamed on success)
```

- **scenetake draws the frames itself.**
  - It reuses the emulator the SVG path already depends on, so both outputs see the same screen state.
  - It needs no agg, Docker, or headless browser.
  - The prototype drew 460 4K frames in 4.2s with a peak of 367MB.
- **ffmpeg** is standard OSS, packaged by winget, Scoop, Homebrew, and apt.
  - scenetake runs it as a separate process and does not link to it.
  - scenetake's MIT license and binaries are therefore unaffected by ffmpeg's GPL or LGPL build.
- **[StbTrueTypeSharp](https://github.com/StbSharp/StbTrueTypeSharp)** is a C# port of stb_truetype, licensed Unlicense OR MIT.
  - It is pure managed code with no native dependencies.
  - It should work under Native AOT on all six RIDs (to be confirmed in B0).

## Proposed CLI

```bash
scenetake [--format cast|svg|mp4] [render options] [mp4 options] <scenario.yaml> [output]
scenetake mp4 [render options] [mp4 options] <input.cast> [output.mp4]
```

### Render Options

The existing `--font-size`, `--font-family`, `--theme`, and `--window` apply, with the same priority as today: CLI > cast header > default.

### mp4 Options

| Option | Default | Range | Why |
|---|---|---|---|
| `--fps N` | `30` | `1`–`60` | Constant frame rate. For matrix, going from 60 to 30fps cut 12.5MB to 8.8MB. X caps at 60. |
| `--crf N` | `23` | `0`–`51` | x264's default quality. `28` with `-preset slow` was about 44% smaller in the matrix test (12.5MB → 7.0MB). |
| `--max-size WxH` | none | | Downscales to fit, keeping the aspect ratio. Never upscales. `1280x1024` fits the X API limit. |
| `--font-file PATH` | none | | Primary font file (TTF, OTF, or TTC). Overrides the `font-family` lookup for mp4. |

### Rules

- **Option scope.**
  - `--max-fps` stays SVG-only.
  - The mp4 options are errors unless the command is `--format mp4` or the `mp4` subcommand.
  - This mirrors today's `--max-fps` rule in [spec_cli.md](../spec_cli.md).
- **Locating ffmpeg.** Use the `SCENETAKE_FFMPEG` environment variable (a path) if set, otherwise `ffmpeg` on `PATH`.
- **Output paths** follow the `svg` rules: a shared stem with an `.mp4` extension.
- **Scenario YAML** gets no new keys in v1. The existing `render:` keys apply.
- **Naming.** `mp4` mirrors `svg`, so format names match subcommand names. The earlier discussion used `video`, but `mp4` keeps the symmetry and leaves room for `webm` or `gif` later.

## Encoding Defaults

The ffmpeg input is `-f rawvideo -pix_fmt rgb24 -s WxH -r FPS -i -`.

- **Scaling (only when `--max-size` applies):** `-vf scale=…:flags=lanczos`.
- **Encode:** `-c:v libx264 -preset medium -crf 23 -pix_fmt yuv420p`.
- **Color:** BT.709 conversion and tags.
- **Container:** `-movflags +faststart -f mp4`.

| Choice | Reason |
|---|---|
| H.264 High, yuv420p | X requires it, and it plays everywhere: browsers, GitHub, Slack, phones. yuv444 is sharper for colored text (13.7MB vs 12.5MB) but many players reject it. |
| `+faststart` | Puts the index at the front, so playback starts early and upload processing is faster. |
| BT.709 matrix and tags | ffmpeg converts RGB with BT.601 by default, while many players assume BT.709 for HD, so theme colors can shift. Verify the decoded theme background is within ±2 per channel. |
| Even dimensions | yuv420p requires them. Pad odd sizes by one pixel of theme background. |
| `-preset medium` | `slow` with crf 28 gave 7.0MB, but the preset's own effect was not isolated. B0 measures presets and `-tune animation`. |

## Rendering Rules (Match SVG)

### Layout

| Item | Rule |
|---|---|
| Cell | `round(font-size × 0.62)` × `round(font-size × 1.25)`, which is 10×20 at 16px. These are the SVG metrics rounded to whole pixels so edges stay crisp. |
| Baseline | `round(font-size × 0.8)` from the top of the row. |
| Padding | The SVG inner padding, filled with the theme background. There is no transparent outer margin, because video has no alpha. |
| Canvas | Largest size across resize events (`TerminalReplay.ResolveCanvasSize`). After a shrink, the area outside the viewport is theme background. |

### Colors and Cursor

- **Colors:** same resolution as SVG (reverse, bold and faint intensity, matrix-rain tint), through the shared helper from B1.
- **Cursor:** a block in the theme foreground at 50% opacity that follows DECTCEM (`\e[?25h` / `\e[?25l`).

### Timing

- Frame `k` shows the state after all events with `time ≤ k / fps`.
- The frame count is `ceil(loopDuration × fps)`. That is the same loop period as SVG and agg, including the final hold.
- States shorter than one frame are dropped, as with `--max-fps`.
- Unchanged frames are sent again. H.264 encodes them almost for free.

### Glyphs

- **Font lookup.**
  - Use `--font-file` if given. Otherwise try the `font-family` families in order against installed fonts.
  - The generic names `ui-monospace` and `monospace` map to a per-OS default list.
  - Search these directories:
    - Windows: `%WINDIR%\Fonts`, `%LOCALAPPDATA%\Microsoft\Windows\Fonts`
    - macOS: `/System/Library/Fonts`, `/Library/Fonts`, `~/Library/Fonts`
    - Linux: `/usr/share/fonts`, `/usr/local/share/fonts`, `~/.local/share/fonts`, `~/.fonts`
  - Try well-known file names first.
  - A full scan reads only each file's table directory and `name` table, never the whole file.
- **Per-glyph fallback.** Primary font, then a CJK font, then a symbol font, then a tofu box. Initial candidates:
  - CJK: Yu Gothic or MS Gothic on Windows, Hiragino Sans on macOS, Noto Sans CJK on Linux.
  - Symbols: Segoe UI Symbol, DejaVu Sans.
- **Bold.**
  - Use a static bold face when the family has one; otherwise use synthetic bold (dilate the alpha mask by about 1px at 16px).
  - stb_truetype renders only the default instance of a variable font. Windows 11's Cascadia fonts appear to be variable, so synthetic bold may be the common path there (to be confirmed in B0).
- **Italic and underline.**
  - Italic uses the italic face if present, otherwise a shear.
  - Underline is a line below the baseline.
- **Wide characters.** Center the glyph across two cells, and scale it down if it is wider.
- **Box drawing (U+2500–U+257F) and block elements (U+2580–U+259F)** are drawn procedurally, edge to edge. Font glyphs leave gaps between rows at 1.25 line height and break TUI borders. Braille (U+2800–U+28FF, used by `btop` graphs) can follow later.
- **Color emoji are not supported.** stb_truetype cannot read color glyph tables. If the font has a monochrome outline, use it; otherwise draw a tofu box. Document this.
- **Glyph cache** keyed by (text, face, bold, italic).
- **Dirty rows.** Redraw only rows that changed since the previous frame. This matters for 4K typing demos.

## Phases

### B0. Spikes (Before Fixing the CLI)

1. **Native AOT.** Publish with StbTrueTypeSharp for all six RIDs. Expect zero new AOT or trim warnings, and record the binary size change.
2. **Fonts.** Check that the default stack resolves to a monospace face plus CJK fallback on Windows 11, macOS, Ubuntu 24.04, and the GitHub runner images. Check variable-font behavior (Cascadia).
3. **ffmpeg builds.** Check whether libx264 is present in each:
   - winget `Gyan.FFmpeg`, Scoop `ffmpeg`, Homebrew, Ubuntu apt;
   - Fedora's default `ffmpeg-free`, which is expected to lack it;
   - BtbN LGPL builds, which lack it.

   Decide the error message, and whether to support a fallback encoder (`libopenh264`, `h264_mf`, `h264_videotoolbox`).
4. **Encoder settings.**
   - Compare presets and `-tune animation` / `stillimage`.
   - Check BT.709 color accuracy.
   - Check chroma blur on red and blue text at yuv420p.
5. **Small text.** For `--max-size`, compare downscaling against rendering at a smaller font size, because unhinted stb text gets soft at about 8px.

Append short findings to this plan and adjust the defaults before B4.

#### B0 Findings (2026-10-29)

**Native AOT.** A probe app using StbTrueTypeSharp 1.26.13 published as Native AOT for win-x64 with no warnings. The other five RIDs are left to CI, which already publishes all six.

**Fonts.**

| OS | Primary monospace | CJK fallback | Symbols, braille, emoji |
|---|---|---|---|
| Windows 11 | Cascadia Mono / Cascadia Code are **variable fonts** (single file, `fvar`). Consolas has static Regular, Bold, Italic, and Bold Italic. | BIZ UDGothic and MS Gothic (fixed pitch), Yu Gothic, Meiryo | Segoe UI Symbol has braille and monochrome emoji. Segoe UI Emoji has COLR plus `glyf` base outlines. |
| Ubuntu 24.04 (Docker, with `fonts-dejavu` and `fonts-noto-cjk`) | DejaVu Sans Mono: static Book, Bold, and Oblique | Noto Sans Mono CJK (CFF outlines, which stb_truetype loads) | DejaVu Sans has braille. |
| macOS | Not checked locally; CI covers it. | | |

- Cascadia has box drawing, blocks, and braille, but no CJK.
- Consolas lacks braille, and Consolas Bold lacks `━`. Per-glyph fallback is required either way.

**ffmpeg.**

| Build | libx264 |
|---|---|
| Ubuntu 24.04 apt (6.1.1) | Yes |
| Fedora 41 `ffmpeg-free` (7.1.2) | **No.** Only `libopenh264` and hardware encoders. |
| Gyan (winget, Scoop), Homebrew | Yes, per their published build configs (not run locally) |
| BtbN LGPL builds | No, because x264 is GPL |

**Encoder settings.** 30fps, crf 23, yuv420p:

| Preset / tune | matrix 200×60 | basic |
|---|---|---|
| `medium` | 8.78MB, 3.9s | 216KB, 0.9s |
| `slow` | 8.56MB (−2%), 4.6s | 186KB (−14%), 1.0s |
| `veryslow` | 6.83MB (−22%), 12.1s | 170KB (−21%), 1.7s |
| `medium -tune animation` | 9.37MB (+7%) | 207KB (−4%) |
| `medium -tune stillimage` | 9.38MB (+7%) | 237KB (+10%) |

**Color.** The theme background `#282c34` is (40, 44, 52), decoded the way a BT.709 player does.

- Untagged output with ffmpeg's default BT.601 conversion decodes as (38, 44, 52).
- BT.709 conversion with BT.709 tags decodes as (40, 44, 52), exact.
- `ffprobe` reports High profile, yuv420p, and BT.709 tags.

**Chroma.** Colored text in the highlight sample looks nearly identical after yuv420p.

**Small text.** The scenario-format sample was compared two ways:

- rendered at 16px and downscaled 50% with lanczos;
- rendered at 8px directly.

The downscaled frame is clearly more legible, because unhinted 8px glyphs are thin and uneven.

#### Decisions for v1

| Topic | Decision | Why |
|---|---|---|
| Preset | `slow` | 2–14% smaller than `medium` for about 15% more time. `veryslow` saves more but takes 3× longer and uses more reference frames, which raises the H.264 level. |
| Tune | None | Neither tune helped consistently. |
| Color | BT.709 conversion and tags | Exact theme colors. |
| `--max-size` | Downscale with lanczos from the normal font size | Legibility. |
| Missing libx264 | Error with an install hint (Fedora: RPM Fusion `ffmpeg`). No fallback encoder in v1. | `libopenh264` has no CRF mode, and Fedora's may be a stub. |
| Box drawing | Procedural for light and heavy lines, dashes, half lines, and rounded corners. Double lines (U+2550–U+256C) and diagonals use the font. | Double-line joins need many special cases. |
| Block elements and braille | Procedural | Edge-to-edge cells; `btop` graphs use braille. |
| Bold | Static bold face if the family has one; otherwise synthetic. Always synthetic for variable fonts. | stb_truetype renders only a variable font's default instance. |
| Open questions 1–4, 6 | Subcommand `mp4`; default 30fps; no named presets (README documents `--max-size 1280x1024`); one format per run; no bundled font. | As proposed. Easy to revisit. |

### B1. Shared Pieces (Refactor, No Behavior Change)

- Move color resolution out of `SvgFrameRenderer` into one shared static class used by both renderers:
  - `ResolveForeground`, `ResolveTintedForeground`, `ResolveBackground`, and `ApplyIntensity`;
  - hex color parsing.
- SVG output stays byte-identical.
- Add a replay stepper ("advance the emulator to time t"). Reuse A1's stepper if it has landed; otherwise write a small one here.

### B2. Rasterizer

- New files included from `scenetake.cs`, for example:
  - `Raster.cs`: frame drawing;
  - `FontResolver.cs`: discovery and fallback.
- Follow the rendering rules above, with pixel tests that do not need ffmpeg.

### B3. ffmpeg Pipe

- **Process.** Start ffmpeg with `ProcessStartInfo.ArgumentList` and no shell. Write frames to stdin. Drain stderr asynchronously and keep its tail for error messages.
- **Probe.** Run `ffmpeg -hide_banner -encoders` once to check for libx264.
- **Output file.** Write to a temporary file next to the output, rename it on success, and delete it on failure. This matches the SVG partial-file rule.
- **Errors.** If ffmpeg exits early, the next stdin write fails. Report ffmpeg's stderr tail instead of a bare broken-pipe error.
- **Cancellation.** On Ctrl+C, kill ffmpeg and delete the temporary file.

### B4. CLI and Scenario Path

- Add `--format mp4`, the `mp4` subcommand, the options above, and help text. Validation and duplicate rules follow [spec_cli.md](../spec_cli.md).
- The scenario path checks for ffmpeg and libx264 before running `pre`, so a long recording does not fail at the end.
- Log line example:

  ```
  Written: out.mp4 (1280x772, 30fps, 226 frames, 4.6MB)
  ```

### B5. Window Chrome (After v1)

- Support `macos` and `windows` chrome in mp4: title bar and controls.
- Video has no transparency, so the outer margin, rounded corners, and shadow need a decision. One option is to drop the margin and shadow and draw the title bar flush with the frame edge.
- Until B5 lands, warn once and render without chrome.

### B6. Docs, CI, and Samples

- **Specs.**
  - Add `spec_mp4.md` covering WHAT, WHY, and lessons learned, following the document-spec policy.
  - Update [spec_cli.md](../spec_cli.md) and [spec_index.md](../spec_index.md).
  - Add a cross-reference from [spec_svg.md](../spec_svg.md).
- **README.md and README-ja.md.**
  - Add guidance on choosing SVG or mp4.
  - Add ffmpeg install commands per OS.
  - Add a "Sharing on X" recipe:

    ```bash
    scenetake mp4 --max-size 1280x1024 demo.cast
    ```
- **CI (`build.yaml`).**
  - Install ffmpeg where the runner lacks it.
  - Smoke-test `mp4` on `tests/fixtures/*.cast` with the published AOT binary.
  - Check codec, profile, pixel format, fps, and duration with `ffprobe`.
- **Samples.** Do not commit mp4 files, because every regeneration would add binary churn.

## What Was Built

Changes from the proposal:

- **Replay.**
  - `ReplayStepper` (in `Terminal.cs`) applies events up to a time. It uses the same rules as the SVG capture replay, so a sampled frame matches the SVG at the same moment.
  - `TerminalReplay.FindTrailingBlankCutoff` makes the video end on the same screen as the SVG. It first runs a parse-only pass and does the full check only when the final screen is blank.
- **Shared colors.** The color rules moved into `CellColors` (`Terminal.cs`). SVG output stayed byte-identical (48 of 48 sample and fixture renders).
- **Rasterizer.** `Raster.cs` holds the font catalog, the per-glyph fallback chain, synthetic bold and italic, the procedural glyphs, and dirty-row redraw.
- **Double lines are procedural too.** B0 planned to take double lines from the font, but they showed the same row gaps as single lines.
- **Encoding.** `Mp4.cs` holds the frame loop, ffmpeg discovery and the libx264 probe, the pipe, and the temporary-file rename.
- **Preflight.** The scenario path checks ffmpeg and fonts before `pre` runs.
- **CI.** All six platform jobs run the render tests and the CLI and failure tests; failure tests use a fake ffmpeg. A new Linux job installs ffmpeg and runs real encodes with the Native AOT binary.

### Results

Measured with the Native AOT win-x64 build, and in Docker (Ubuntu 24.04, ffmpeg 6.1.1, DejaVu Sans Mono).

| Check | Result |
|---|---|
| matrix 200×60, defaults | 2020×1208, 30fps, 9.8MB, 5.1s (prototype estimate 8.8MB; within ±15%) |
| Same with `--max-size 1280x1024` | 1280×764, 4.8MB |
| Same with `--fps 60` | 13.9MB |
| matrix 384×108 | 3860×2168, 31.7MB, 16.6s |
| `samples/basic.cast` | 194KB; decoded theme background (40, 44, 52), exact |
| ffprobe | h264 High, yuv420p, BT.709 tags, constant frame rate |
| Native AOT binary (win-x64) | +225KB (4.28MB → 4.51MB), no new warnings |
| Tests | `mp4_render_test` 16 passed; `mp4_ffmpeg_test` 9 passed on Linux (6 on Windows, where real encodes skip without ffmpeg); every existing suite still passes |

Not verified locally: macOS fonts, and the Native AOT build on the five non-Windows platforms. CI covers both.

## Failure Behavior

### Scenario Path (`--format mp4`)

| Phase | On failure |
|---|---|
| ffmpeg missing or without libx264 | Before `pre`: error with an install hint; nothing is recorded; exit 1 |
| `pre` | Unchanged ([spec_pre_post.md](../spec_pre_post.md)) |
| Cast write | Fail; mp4 is not attempted |
| mp4 render or encode | Cast is kept; temporary file is deleted; exit non-zero; `post` still runs |
| `post` | Unchanged |

### `mp4` Subcommand

| Situation | Behavior |
|---|---|
| Cast errors | Same as the `svg` subcommand |
| ffmpeg missing or without libx264 | Error with an install hint; exit 1 |
| Encode failure | ffmpeg's stderr tail is shown; no output file; exit non-zero |

## Tests

- **`tests/mp4_render_test.cs`** (no ffmpeg needed):
  - Layout: frame size, padding, even dimensions.
  - Colors: background and foreground pixels, reverse video, and matrix-tint parity with SVG.
  - Cursor: 50% blend.
  - Procedural cells: `█` fills its cell exactly, and box-drawing lines reach the cell edges.
  - Wide glyphs span two cells.
  - Timing: the state shown at `k / fps`, and the frame count.
- **Font tests:**
  - lookup order, `--font-file`, and CJK fallback;
  - when the OS lacks a font, the test prints a notice and skips instead of failing.
- **`tests/mp4_ffmpeg_test.cs`** (needs ffmpeg; CI installs it):
  - End to end through the subcommand and the scenario path, with `ffprobe` stream checks.
  - Failure paths:
    - a fake `SCENETAKE_FFMPEG` that exits 1;
    - missing ffmpeg shows the install hint;
    - a partial file is removed.
- **CLI parsing:** mp4 options outside mp4 mode, and duplicates.

## Acceptance

- **Matrix 200×60 at 60fps, defaults.**
  - H.264 High, yuv420p, 30fps.
  - Size within ±15% of the prototype (8.8MB).
- **Same cast with `--max-size 1280x1024`.** At most 5MB, and it uploads to X as is (checked manually).
- **384×108 cast.** Completes with a peak of at most 1GB, and the run time is dominated by ffmpeg.
- **Samples.** `basic` and `demo` are legible at the default font size. The decoded theme background is within ±2 per channel.
- **Native AOT.** Publishing on all six RIDs adds no warnings. The PR records the binary size change.
- **Without ffmpeg.** Cast and SVG output do not regress, and mp4 gives a clear error.

## Risks

| Risk | Mitigation |
|---|---|
| Installed fonts differ by OS, so the same cast renders differently per machine. SVG leaves fonts to the viewer instead. | `--font-file`. Consider bundling an OFL monospace font later (about 0.3–0.6MB per face). |
| Unhinted rasterization looks soft at small sizes. | Check in B0. Choose between rendering at the final size and downscaling based on the results. |
| Some ffmpeg builds lack libx264 (LGPL builds, Fedora default). | Probe up front and give a clear message. Possibly add fallback encoders (B0). |
| Raw pipe bandwidth at 4K is about 25MB per frame. | Measure in B0. Dirty-row redraw keeps rendering cheap. |
| yuv420p blurs saturated colored text. | Accept for compatibility. Revisit if B0 shows it hurts legibility. |

## Open Questions

1. Should the subcommand be called `mp4` or `video`?
2. Should the default fps be fixed at 30, or follow the recording (for example 60 for `matrix --fps 60`)?
3. Should there be named presets (`--preset x`), or just a documented `--max-size 1280x1024`?
4. Should `--format svg,mp4` write both outputs in one run?
5. Which fallback encoders, if any, when libx264 is missing?
6. Should scenetake bundle a font so output is the same on every machine?
