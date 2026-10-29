# mp4 Output Specification

Status: **Implemented**

## Motivation

Full-screen TUIs (`matrix`, `btop`, banner animations) redraw most cells every frame. SVG is a lossless, symbolic format whose size grows with changed cells × frames, and GIF is limited by its 256-color palette and 10ms frame delays. Both get too large to share.

| Recording | SVG | GIF (agg) | mp4 |
|---|---|---|---|
| `matrix --fps 60`, 200×60, 7.5s | 26.9MB (117MB before the row encoding in spec_svg.md) | 19MB | 9.8MB; 4.8MB with `--max-size 1280x1024` |
| Same, 384×108 (4K frame) | 96.7MB (455MB before) | — | 31.7MB |
| `samples/basic.cast` (typing) | **108KB** | 414KB | 194KB |

X (Twitter) accepts H.264 mp4, not SVG. SVG stays the right format for typing demos embedded in a README. mp4 is for heavy output and sharing.

No practical H.264 encoder exists for .NET, and operating-system encoders differ per platform; Linux has none. scenetake therefore draws frames itself and pipes them to an external `ffmpeg`. ffmpeg runs as a separate program and is not linked, so scenetake's license and binaries are unaffected by the ffmpeg build.

## Scope

- **Entry points:** `--format mp4` on the scenario path and the `mp4` subcommand for existing casts. CLI details: [spec_cli.md](spec_cli.md).
- **Output:** H.264 High profile, yuv420p, BT.709, constant frame rate, `+faststart`.
- **Rendering:** the same VT emulator and cell color rules as [spec_svg.md](spec_svg.md). Glyphs are drawn by StbTrueTypeSharp (Unlicense OR MIT, pure C#, works under Native AOT).
- **Window chrome:** `--window macos|windows` draws the SVG title bar and buttons ([Window chrome](#window-chrome)).
- **Not supported yet:**
  - color emoji, ligatures, and audio;
  - codecs other than H.264.

## Rendering

### Timing

- Frame `k` shows the screen after every event with time ≤ `k / fps`. States shorter than one frame are not shown.
- The video length is the SVG loop period, so it includes the final hold.
- Like the SVG, `--format mp4` renders the cast it just wrote, so it gives the same file as `scenetake mp4` on that cast with the same options.
- Like the SVG, nothing is shown before the first event, not even the cursor.
- Like the SVG, a trailing blank alternate-screen restore is dropped, so a TUI recording ends on the app's last screen.

### Layout

- **Cells:** the SVG cell metrics rounded to whole pixels: width `font-size × 0.62`, height `font-size × 1.25`, baseline `font-size × 0.8`. That is 10×20 with a baseline of 13 at 16px.
- **Padding:** the SVG inner padding, filled with the theme background. There is no transparent outer margin, because video has no alpha.
- **Even sizes:** width and height are rounded up to even numbers, because yuv420p requires it.
- **Canvas:** the largest terminal size across resize events, as in the SVG.

### Colors and Cursor

- Cell colors use the same rules as the SVG: reverse video, bold and faint intensity, and the matrix-rain tint.
- The cursor is a block in the theme foreground at 50% opacity, following DECTCEM.

### Fonts

- **Lookup order:**
  1. `--font-file`, if given.
  2. Each family in `font-family` that is installed. The generic `monospace` and `ui-monospace` mean the operating system's default monospace fonts.
  3. Fallbacks for CJK, symbols, braille, and emoji outlines (for example BIZ UDGothic and Segoe UI Symbol on Windows, Hiragino on macOS, Noto Sans CJK on Linux).

  Each character uses the first font that has it. Characters no font has draw as an outlined box.
- **Installed fonts** are found by reading only each file's headers.
- **mp4 depends on the machine.** The same cast renders with the fonts installed where scenetake runs, whereas an SVG uses the viewer's fonts. Pass `--font-file` to pin the font.
- **Bold and italic** use real faces when the family has them; otherwise they are synthesized (thickened strokes, slanted rows). Variable fonts always use synthetic bold.
- **Procedural glyphs:** box drawing (U+2500–U+257F, except the diagonals), block elements (U+2580–U+259F), and braille (U+2800–U+28FF) are drawn as shapes that reach the cell edges.

### Window chrome

`--window macos` and `--window windows` (or `st:window` in the cast header) draw the SVG title bar above the terminal:

- **Same geometry and colors as the SVG:**
  - a 34px bar with a separator line;
  - the bar color follows the light or dark terminal background;
  - macOS has three traffic-light circles on the left, Windows three square buttons on the right;
  - sizes are fixed pixels and do not scale with `font-size`.
- **Left out:** the SVG's outer margin, drop shadow, rounded corners, and outer border. Video has no transparency, so the margin would need some made-up background color. The bar instead runs flush with the top edge of the frame, the way terminal recorders usually draw a window bar.
- **Resizing:** the bar spans the full canvas width and does not follow terminal resize events.

### Downscaling

`--max-size WxH` fits the video inside `W×H`, keeping the aspect ratio and even sizes, and never enlarges it. Frames are drawn at the normal font size, then scaled down with a lanczos filter.

## Encoding

| Setting | Value | Why |
|---|---|---|
| Codec | libx264, High profile, yuv420p | X requires H.264 High with yuv420p, and it plays in every browser and on phones. |
| Quality | `--crf`, default `23` | x264's default. Higher values make smaller files. |
| Preset | `slow` | 2–14% smaller than `medium` for about 15% more time. `veryslow` saves about 20% more but takes 3× as long and raises the H.264 level. |
| Frame rate | `--fps`, default `30`, max `60` | 30fps keeps terminal motion smooth. For `matrix --fps 60` it is 30% smaller than 60fps. X allows up to 60. |
| Color | BT.709 conversion and BT.709 tags | Untagged output (ffmpeg's default BT.601 conversion) shifts theme colors in players that assume BT.709. |
| Container | mp4 with `+faststart` | Playback and upload processing can start before the whole file arrives. |

## ffmpeg

- **Locating ffmpeg.** The `SCENETAKE_FFMPEG` environment variable names the binary. Otherwise scenetake uses `ffmpeg` on `PATH`.
- **Required encoder.** scenetake checks for the libx264 encoder before rendering. When ffmpeg is missing or lacks libx264, the error ends with install hints:
  - Windows: `winget install Gyan.FFmpeg`, or `scoop install ffmpeg`;
  - macOS: `brew install ffmpeg`;
  - Debian/Ubuntu: `sudo apt install ffmpeg`;
  - Fedora: RPM Fusion's `ffmpeg`, because the default `ffmpeg-free` has no libx264.
- **Partial files.** scenetake encodes to a hidden temporary file in the output folder and renames it when ffmpeg succeeds. On failure or Ctrl+C, ffmpeg is stopped and the temporary file is deleted, so a partial `.mp4` is never left behind.

## Failure Behavior

### Scenario path (`--format mp4`)

| Phase | On failure |
|---|---|
| ffmpeg missing, no libx264, or no usable font | Checked before `pre`: error, nothing recorded, exit `1` |
| `pre` | Unchanged ([spec_pre_post.md](spec_pre_post.md)) |
| Cast write | Fail; mp4 is not attempted |
| mp4 render or encode | Cast is kept; no `.mp4`; exit non-zero; `post` still runs |
| `post` | Unchanged |

### `mp4` subcommand

| Situation | Behavior |
|---|---|
| Cast errors | Same as the `svg` subcommand |
| ffmpeg missing or no libx264 | Error with install hints; exit `1` |
| Font not found | Error naming `--font-file`; exit `1` |
| Encode failure | Error with the end of ffmpeg's stderr; no output file; exit `1` |

The cast file is never modified by the `mp4` subcommand.

## Testing

| Suite | Coverage | Needs |
|---|---|---|
| `tests/mp4_render_test.cs` | Layout, colors, cursor, procedural glyphs, frame timing, trailing trim, matrix tint, option parsing, ffmpeg arguments | An installed font (prints `skip` without one) |
| `tests/mp4_ffmpeg_test.cs` | Option rules, missing ffmpeg, no libx264, encode failure with no leftover file, scenario path keeps the cast, real encodes checked with `ffprobe` | `SCENETAKE_BIN`; real encodes also need `ffmpeg` and `ffprobe` on `PATH` |

CI runs both suites on all six platforms; GitHub runner images ship no ffmpeg, so real encodes are skipped there. A separate Linux job installs ffmpeg and runs real encodes with the Native AOT binary.

## Cross-Document Notes

- [spec_cli.md](spec_cli.md): `--format mp4`, the `mp4` subcommand, mp4 options.
- [spec_svg.md](spec_svg.md): the VT emulator, color rules, cell metrics, and timing that mp4 shares.
- [spec_cast.md](spec_cast.md): `render` metadata in the cast header (font size, font family, theme).

## Lessons Learned

- **Color.** ffmpeg's default RGB→YUV conversion is BT.601 and adds no color tags. A player decoding as BT.709 showed the default theme background `#282c34` as (38, 44, 52) instead of (40, 44, 52). Converting and tagging as BT.709 makes it exact.
- **Variable fonts.** Windows 11's Cascadia Mono and Cascadia Code are variable fonts. stb_truetype draws only their default instance, so bold has to be synthesized.
- **Box drawing from fonts breaks.** Font box-drawing and block glyphs, including double lines, leave gaps between rows at a 1.25 line height. Drawing them as shapes fixed TUI borders.
- **Small text.** Text drawn at 16px and downscaled 50% is clearly more legible than text drawn at 8px, because stb_truetype does not hint glyphs.
- **ffmpeg builds differ.** Fedora 41's `ffmpeg-free` has no libx264, only `libopenh264` and hardware encoders. BtbN's LGPL builds also lack it. Checking up front gives a clear error instead of a failed encode.
- **Trailing-blank check cost.** Hashing the whole screen after every event accounted for about 6.6s of a 10.5s 4K render. A parse-only pass shows whether the final screen is blank, and only then does the full check run. 4K rendering dropped to 3.9s.
- **Encoding dominates.** A 4K (3860×2168), 30fps, 7.7s matrix recording takes about 17s end to end, most of it in x264.
