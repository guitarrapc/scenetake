# mp4 Output Specification

Status: **Implemented**

## Motivation

Full-screen TUIs (`matrix`, `btop`, banner animations) redraw most cells every frame. SVG is a lossless, symbolic format whose size grows with changed cells × frames, and GIF is limited by its 256-color palette and 10ms frame delays. Both get too large to share.

| Recording | SVG | GIF (agg) | mp4 |
|---|---|---|---|
| `matrix --fps 60`, 200×60, 7.5s | 17.0MB (117MB before the encoding work in spec_svg.md) | 19MB | 4.4MB; 2.8MB with `--max-size 1280x1024` |
| Same, 384×108 (4K frame) | 53.2MB (455MB before) | — | 14.9MB |
| `samples/basic.cast` (typing) | **46KB** | 414KB | 83KB |
| `samples/git.cast` (typing) | **31KB** | — | 184KB |

X (Twitter) accepts H.264 mp4, not SVG. SVG stays the right format for typing demos embedded in a README. mp4 is for heavy output and sharing.

Where recordings get shared (checked 2026-10-29):

| Destination | Limit |
|---|---|
| X video (API upload) | H.264 High profile, yuv420p, up to 60fps, up to 1280×1024 |
| X GIF | 15MB, up to 1280×1080, up to 350 frames, up to 300M total pixels |
| GitHub attachments | Video (mp4, mov, webm): 10MB on free plans, 100MB on paid plans. Images and GIF: 10MB. |

Sources: [X API: Media best practices](https://docs.x.com/x-api/media/quickstart/best-practices.md), [GitHub Docs: Attaching files](https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/attaching-files).

No practical H.264 encoder exists for .NET, and operating-system encoders differ per platform; Linux has none. scenetake therefore draws frames itself and pipes them to an external `ffmpeg`. ffmpeg runs as a separate program and is not linked, so scenetake's license and binaries are unaffected by the ffmpeg build.

## Scope

- **Entry points:** `--format mp4` on the scenario path and the `mp4` subcommand for existing casts. CLI details: [spec_cli.md](spec_cli.md).
  - The name `mp4` mirrors `svg`, so format names match subcommand names and leave room for `webm` or `gif` later. `video` was considered.
- **Samples:** no mp4 files are committed to `samples/`, because every regeneration would add binary churn.
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
- Like the SVG, nothing is shown before the first event, not even the cursor. A cast with no events differs: its SVG shows the cursor throughout, while the mp4 shows only the background.
- Like the SVG, a trailing blank alternate-screen restore is dropped, so a TUI recording ends on the app's last screen.

### Layout

- **Cells:** the SVG cell metrics rounded to whole pixels: width `font-size × 0.62`, height `font-size × 1.25`, baseline `font-size × 0.8`. That is 10×20 with a baseline of 13 at 16px.
- **Padding:** the SVG inner padding, filled with the theme background. There is no transparent outer margin, because video has no alpha.
- **Even sizes:** width and height are rounded up to even numbers, because yuv420p requires it.
- **Canvas:** the largest terminal size across resize events, as in the SVG.
- **Size limits:** two limits come from ffmpeg and x264, and both are checked before recording.
  - ffmpeg accepts a raw frame only while (width + 128) × (height + 128) is below 2^28, about 16000×16000. A 512×512 terminal is within it at `--font-size 36` and over it from 37. The error suggests a smaller `--font-size` or terminal size. `--max-size` cannot help, because it scales the frame after ffmpeg reads it.
  - x264 accepts at most 16384 pixels on each side of the encoded video. 512 columns are 15904 pixels wide at `--font-size 50` and 16416 from 51. `--max-size` scales the video below the limit, and the error suggests it.

### Colors and Cursor

- Cell colors use the same rules as the SVG: reverse video, bold and faint intensity, and the matrix-rain tint.
- The cursor is a block in the theme foreground at 50% opacity, following DECTCEM.

### Fonts

- **Lookup order:**
  1. `--font-file`, if given.
  2. Each family in `font-family` that is installed. The generic `monospace` and `ui-monospace` mean the operating system's default monospace fonts.
  3. Fallbacks for CJK, symbols, braille, and emoji outlines (for example BIZ UDGothic and Segoe UI Symbol on Windows, Hiragino on macOS, Noto Sans CJK on Linux).

  Each character uses the first font that has it. Characters no font has draw as an outlined box.
- **Installed fonts** are found by reading only each file's headers. A file is skipped when it cannot be read or one of its tables lies outside the file. A face is skipped when stb_truetype could not use it:
  - it has no outlines stb_truetype draws (bitmap-only or CFF2), or no `head`, `hhea`, or `hmtx` table;
  - its cmap has no Unicode subtable (symbol fonts such as Symbol and Wingdings).

  The render scans again when it starts, after the steps, so a font that `pre` or the steps install is used. The check before `pre` still needs one usable font, so `pre` cannot install the only one. A font reached through a symbolic link loads like the file it points to.
- **Loading a face** checks what stb_truetype reads without bounds checks: the fields it reads from `head`, `hhea`, and `maxp`, the lengths of `hmtx` and `loca`, and every `loca` entry against the end of `glyf`. A face that fails is skipped like a missing one. A glyph id from the cmap at or past maxp's glyph count reads as missing. Damaged glyph outlines and cmap subtables are not checked otherwise.
- **`--font-file`** is an error when the file has no usable face, or when any face it would draw with (regular, bold, italic, bold-italic) fails to load, instead of falling back to another font. That check does not keep the file mapped, so a `pre` command can replace the font file. The render releases every font file it loaded when it ends, whether it succeeds or fails, so `post` can replace or delete them too.
- **Memory.** Font files are memory-mapped, so a face is never read whole. Loading reads the table directory, the fields it checks, every `loca` entry, and the cmap encoding records, plus the header and indexes of a CFF face. A glyph lookup reads only what it touches.
- **The log** names the regular face of the first family in the chain whose regular face loads. A family whose regular file fails the load checks draws no regular text, so it is passed over. When no regular face loads, the log names the first family.
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
| Quality | `--crf`, default `23`, range `1`–`51` | x264's default. Higher values make smaller files. `0` (lossless) is rejected: x264 then writes the High 4:4:4 Predictive profile, which X and many players reject. |
| Preset | `slower` | Smaller than `slow` on every recording tested, at equal or higher SSIM. `veryslow` was larger on typing and took 1.4–2.1× as long. |
| Psychovisual tuning | Off (`-psy 0`) | It spends bits on texture detail; terminal frames are flat color and sharp text. Off was smaller at equal or higher SSIM and looked the same side by side. |
| Motion search range | 128 pixels (`merange`; `slower` searches 16) | A scroll moves text by whole rows, 20 pixels at the default font size, so a 16-pixel search missed scrolls and coded the moved text again. A wider search also finds the same character elsewhere in recent frames, which redrawn TUIs repeat. 15–45% smaller at the same CRF. SSIM rose, except on interactive-pty, where it fell by 0.00003. Typing encodes as fast as before; matrix 200×60 takes 8.0s instead of 3.3s. |
| Keyframes | At most every 30 seconds | A keyframe redraws the whole screen while the frames between are tiny. In a typing demo, x264's default interval (250 frames, 8.3s at 30fps) added a keyframe that alone was 21% of the file. 30 seconds keeps seeking cheap. |
| Reference frames | 8, capped so the stream stays within level 5.2 | 8 references at 4K would need level 6.0, which many phone and TV decoders lack. 4K gets 5. |
| Frame rate | `--fps`, default `30`, max `60` | 30fps keeps terminal motion smooth. For `matrix --fps 60` it is 28% smaller than 60fps (4.4MB vs 6.1MB). X allows up to 60. |
| Color | BT.709 conversion and BT.709 tags | Untagged output (ffmpeg's default BT.601 conversion) shifts theme colors in players that assume BT.709. |
| Container | mp4 with `+faststart` | Playback and upload processing can start before the whole file arrives. |

## ffmpeg

- **Locating ffmpeg.** The `SCENETAKE_FFMPEG` environment variable names the binary. Otherwise scenetake uses `ffmpeg` on `PATH`.
- **Required encoder.** scenetake checks for the libx264 encoder before rendering. An ffmpeg that does not answer within 15 seconds is an error. When ffmpeg is missing or lacks libx264, the error ends with install hints:
  - Windows: `winget install Gyan.FFmpeg`, or `scoop install ffmpeg`;
  - macOS: `brew install ffmpeg`;
  - Debian/Ubuntu: `sudo apt install ffmpeg`;
  - Fedora: RPM Fusion's `ffmpeg`, because the default `ffmpeg-free` has no libx264.
- **Output path.** The output folder must exist, the output path must not be a directory, and it must not be the input file. These are checked before rendering. On the scenario path all three are checked before `pre`, except the folder when `pre` has commands, and all three again after `pre`, which may create the folder, or put a directory or a link to the scenario where an output goes ([spec_cli.md](spec_cli.md) → Output Paths).
- **Earlier output.** A file already at the output path is deleted when encoding starts, so a failed encode or a Ctrl+C never leaves a video from an earlier run. Font and frame-size errors found by the checks before `pre`, or by the `mp4` subcommand, keep it. On the scenario path any render failure after the recording deletes it, because it no longer matches the new cast. On the `mp4` subcommand a cast that cannot be read deletes it, as with SVG.
- **Partial files.** scenetake encodes to a temporary file in the output folder, named `.<output name>.<id>.partial`, and renames it when ffmpeg succeeds. On failure or Ctrl+C, ffmpeg is stopped and the temporary file is deleted, so a partial `.mp4` is not left behind. A crash inside stb_truetype, on a font whose glyph data is damaged, would still leave one. Ctrl+C ends scenetake about one second later, with the platform's interrupt exit code (130 on Unix, `0xC000013A` on Windows).

## Failure Behavior

### Scenario path (`--format mp4`)

| Phase | On failure |
|---|---|
| Output path a directory, the output is the scenario file, or the output folder missing when there is no `pre` | Checked before `pre`: error, nothing recorded, exit `1` |
| ffmpeg missing, no libx264, no usable font, or a frame over the size limits | Checked before `pre`: error, nothing recorded, exit `1` |
| `pre` | Unchanged ([spec_pre_post.md](spec_pre_post.md)) |
| An output check fails after `pre` (folder missing, or `pre` put a directory or a link to the scenario where an output goes) | Error before the recording; `post` runs; exit `1`, or the failing `post` command's code |
| Cast write | Error; mp4 is not attempted; `post` runs; exit `1`, or the failing `post` command's code |
| mp4 render or encode | Cast is kept; no `.mp4`, and one from an earlier run is deleted; exit non-zero; `post` still runs |
| Ctrl+C while encoding | Cast is kept; no `.mp4` (an earlier one was deleted when encoding started) or temporary file; ffmpeg stopped; `post` does not run |
| `post` | Unchanged |

### `mp4` subcommand

| Situation | Behavior |
|---|---|
| The output is the input file (for example `scenetake mp4 clip.mp4`) | Error before anything is read; the input is untouched; exit `1` |
| Cast errors | Same as the `svg` subcommand: a cast that is not found keeps an `.mp4` from an earlier run, and a cast that cannot be read deletes it |
| ffmpeg missing or no libx264 | Error with install hints; exit `1` |
| Output folder missing, or output path is a directory | Error naming the path, before rendering; exit `1` |
| Font not found | Error naming `--font-file`; an `.mp4` from an earlier run is kept; exit `1` |
| Frame over the size limits | Error suggesting a smaller `--font-size` or terminal, and `--max-size` when a side is over x264's 16384; an `.mp4` from an earlier run is kept; exit `1` |
| Encode failure | Error with the end of ffmpeg's stderr; no output file (an earlier one was deleted when encoding started); exit `1` |
| Ctrl+C while encoding | No output or temporary file; ffmpeg stopped |

The input file is never modified by the `mp4` subcommand.

## Testing

| Suite | Coverage | Needs |
|---|---|---|
| `tests/mp4_render_test.cs` | Layout, colors, cursor, procedural glyphs (also at font sizes 1–3), frame timing, trailing trim, matrix tint, option parsing, ffmpeg arguments, both size limits, the ffmpeg probe timeout, output path checks, font files that are malformed, symbol-only, unreadable (Unix only), fail to load, or have short tables, out-of-range glyph ids, every `--font-file` face loading, a valid font file accepted and not held open by the check or after the render, a font reached through a symbolic link, the installed fonts rescanned, the log naming a font that loads, and the default fonts passing the table checks | An installed font (prints `skip` without one). Three tests also skip when their premise fails: the unreadable-file test when it runs as a user who can read any file, such as root, the bold-face test when the installed font cannot make a regular and bold collection, and the log test when the copied font's own family does not load. The release after the render can fail only on Windows, which refuses to replace a mapped file. The link test skips without permission to make symbolic links. |
| `tests/mp4_ffmpeg_test.cs` | Option rules, missing ffmpeg, no libx264, encode failure with no leftover file, scenario path keeps the cast and still runs `post`, an earlier `.mp4` is gone while ffmpeg runs and kept on a font or frame-size error, outputs never replace the input (also through a directory link, a file link, a link whose target passes through another link, a `..` in a link's target on Unix, and on Windows `\\?\` and `\??\` spellings, also while the input is held open without read sharing), the output folder checked after `pre`, or before it when there is no `pre`, and the rest before it, every output check run again after `pre` (`pre` puts a directory, or a link to the scenario, where the cast goes), a relative link target reached through a directory link resolved as each OS resolves it, scenario errors found before `pre` (YAML the parser rejects, a missing shell, a list as `run`), step keys that contradict each other found before `pre`, `post` run when the cast write fails or the recording stops (an interactive step whose output is redirected, a PTY command holding a NUL), an error rather than an unhandled exception when a step removes the scenario's working folder, SVG failures reported while the output is held open, a font that `pre` or a step installs used by the render (Linux only), ffmpeg, font, terminal size, and frame size checked before `pre`, real encodes checked with `ffprobe` and for the motion search range x264 records in the stream, SIGINT mid-encode leaves no files, an earlier `.mp4` included (Unix only) | `SCENETAKE_BIN`; real encodes also need `ffmpeg` and `ffprobe` on `PATH`. Without permission to make links, the link cases print a note and are left out. The font-install test skips without a TrueType font with outlines and a Windows family name under `/usr/share/fonts` or `/usr/local/share/fonts`. |
| `tests/scenario_render_test.cs` | `--format mp4` gives the same file as the `mp4` subcommand on the written cast. The scenario puts an event 0.3ms past a frame edge, so rendering from the in-memory events would differ | `SCENETAKE_BIN`, `ffmpeg` |

CI runs these suites on all six platforms. GitHub runner images ship no ffmpeg, so real encodes are skipped there. A separate Linux job installs ffmpeg and runs the three suites with real encodes and the Native AOT binary.

## Cross-Document Notes

- [spec_cli.md](spec_cli.md): `--format mp4`, the `mp4` subcommand, mp4 options.
- [spec_svg.md](spec_svg.md): the VT emulator, color rules, cell metrics, and timing that mp4 shares.
- [spec_cast.md](spec_cast.md): `render` metadata in the cast header (font size, font family, theme).

## Alternatives Not Chosen

Sizes are for `matrix --fps 60` at 200×60, 7.5s, 60fps unless noted. CRF values are not comparable across codecs.

| Idea | Why not |
|---|---|
| Built-in encoder, no ffmpeg | No practical managed H.264 encoder exists. OS encoders (Media Foundation, VideoToolbox) differ per platform, and Linux has none. |
| HEVC, AV1, VP9, animated WebP or AVIF | No clear size win at tested settings: H.264 crf 23 was 12.5MB, against HEVC crf 28 9.0MB, AV1 crf 35 14.2MB, VP9 crf 35 20.2MB, AVIF 14.1MB, and WebP q75 at 30fps 16MB. They are slower, and X requires H.264. The pipe can add them later. |
| yuv444 | Sharper colored text, but larger (13.7MB vs 12.5MB), and many players reject it. Colored text looked nearly identical after yuv420p. |
| GIF from ffmpeg | Larger than agg (27MB vs 19MB at 30fps) and still limited to 256 colors and 10ms frame delays. X caps GIFs at 15MB and 350 frames. |
| `-tune animation` or `stillimage` | Neither helped consistently. `animation` was 7% larger on matrix and 4% smaller on typing; `stillimage` was 7% and 10% larger. |
| A fallback encoder when libx264 is missing | `libopenh264` has no CRF mode, and Fedora's build may be a stub. scenetake errors with an install hint instead. |
| Named presets (`--preset x`) | `--max-size 1280x1024`, documented in the README, covers X. |
| Both formats in one run (`--format svg,mp4`) | One format per run. Run the `svg` or `mp4` subcommand on the written cast for the other. |
| Frame rate following the recording | Fixed default 30fps: 28% smaller than 60fps for matrix and still smooth. `--fps` raises it. |
| A bundled font | About 0.3–0.6MB per face. `--font-file` pins the font when output must match across machines. |
| Exhaustive motion search (`esa`, `tesa`) | Not smaller for the time. On matrix 200×60 at 30fps and CRF 23, `esa` over 64 pixels gave 4.3MB in 13.2s and `tesa` 4.3MB in 23.0s, against `umh` over 128 pixels, 4.4MB in 8.0s. |
| A wider motion search (192 or 256 pixels) | Matrix 200×60 at 30fps shrank to 4.0MB and 3.7MB but took 12.8s and 17.9s. Typing gained under 1% at 192. |
| 16 reference frames | With the 128-pixel search, matrix 200×60 at 30fps was 3.8MB instead of 4.4MB but took 14.4s instead of 8.0s, and typing was about 1% larger at equal SSIM. 4K stays capped at 5 by level 5.2. |
| Other x264 options | Measured with the 16-pixel search, at equal SSIM over CRF 20–29: `subme 11` was 2–4% smaller at 1.2× the time. `aq-mode`, `aq-strength`, deblocking, 16 B-frames, no scene cuts, and a 250-frame lookahead were within 3% on average or mixed between recordings. A chroma QP offset moved bits between chroma and luma, and SSIM and PSNR disagreed on it. Turning off the macroblock tree was 16–27% larger on typing and TUIs. |

## Lessons Learned

- **Native AOT.** StbTrueTypeSharp 1.26.13 publishes under Native AOT with no warnings. It grew the win-x64 binary by about 225KB (4.28MB → 4.51MB).
- **Per-glyph fallback is needed even for common fonts.** Consolas has no braille, and Consolas Bold has no `━`. Cascadia has box drawing, blocks, and braille, but no CJK.
- **Color.** ffmpeg's default RGB→YUV conversion is BT.601 and adds no color tags. A player decoding as BT.709 showed the default theme background `#282c34` as (38, 44, 52) instead of (40, 44, 52). Converting and tagging as BT.709 makes it exact.
- **Variable fonts.** Windows 11's Cascadia Mono and Cascadia Code are variable fonts. stb_truetype draws only their default instance, so bold has to be synthesized.
- **Box drawing from fonts breaks.** Font box-drawing and block glyphs, including double lines, leave gaps between rows at a 1.25 line height. Drawing them as shapes fixed TUI borders.
- **Small text.** Text drawn at 16px and downscaled 50% is clearly more legible than text drawn at 8px, because stb_truetype does not hint glyphs.
- **ffmpeg builds differ.** Fedora 41's `ffmpeg-free` has no libx264, only `libopenh264` and hardware encoders. BtbN's LGPL builds also lack it. Checking up front gives a clear error instead of a failed encode.
- **Trailing-blank check cost.** Hashing the whole screen after every event accounted for about 6.6s of a 10.5s 4K render. A parse-only pass shows whether the final screen is blank, and only then does the full check run. 4K rendering dropped to 3.9s.
- **Encoding dominates.** A 4K (3860×2168), 30fps, 7.7s matrix recording takes about 17s end to end, most of it in x264.
- **Ctrl+C reaches ffmpeg too.** A console Ctrl+C (or a terminal's SIGINT to the process group) also reaches ffmpeg, which then finalizes its output on its own.
  - The cleanup must therefore stop ffmpeg and remove the temporary file itself, from the Ctrl+C handler: once that handler returns, the process ends, so `finally` blocks never run.
  - On Windows with a Scoop shim, the process scenetake waits for is the shim. The real ffmpeg can still hold the file for a moment after the shim exits, so a single delete attempt failed now and then and left a `.partial` (1 in 3 tries). Retrying the delete for up to 3 seconds made all 10 later tries clean.
- **stb_truetype trusts the file.** It reads tables through raw pointers without bounds checks. A font whose `glyf` table pointed past the end of the file crashed scenetake with an access violation, so no cleanup ran and a `.partial` file stayed. The font scan therefore checks each table's offset and length against the file size. A table inside the file can still be too short: an empty `loca` at the end of the file crashed the same way on non-ASCII text, and silently drew no text on ASCII. Loading a face therefore also checks the table lengths and every `loca` entry.
- **Symbol fonts.** `stbtt_InitFont` fails on a font whose cmap has only a symbol subtable (Symbol, Wingdings, Marlett). Before the scan checked for a Unicode subtable, such a font in `font-family` aborted the render instead of falling back to the next family.
- **Fallback memory.** A character no font has, such as a Powerline symbol without a Nerd Font, is looked up in every fallback font. Reading each file into memory raised the peak working set from 17MB to 109MB on Windows 11. With memory-mapped files it peaks at about 18MB, about 1MB above plain ASCII (17MB). Most of that comes from checking each fallback face's `loca` table when it loads.
- **Tiny font sizes.** At the smallest font sizes a line is wider than the cell: a heavy line at font sizes 1–2, and a double line up to 5. At 1–2 drawing it wrote past the glyph's mask and failed the render, so lines and fills are clipped to the cell.
- **A hung ffmpeg.** Reading ffmpeg's stdout to the end blocks until ffmpeg closes it, normally when it exits, so a timed wait after the read never times out. An ffmpeg that hung for 25 seconds took 25 seconds and was reported as having no libx264. The probe reads stdout asynchronously, so its 15-second timeout holds. It also closes ffmpeg's stdin, so a wrapper that waits for input finishes instead of hanging.
- **The input as its own output.** Without an output path, the output takes the input's name with the new extension. So `scenetake mp4 clip.mp4` named the input as its output, and the failed render deleted the input. Outputs are checked against the input before anything else runs.
- **ffmpeg's size limits are far below .NET's.** The first size check only kept the RGB frame within one array, about 715 million pixels. x264 rejects a side over 16384, and ffmpeg a raw frame from about 258 to 264 million pixels depending on its shape, so frames between those passed the check and failed only after the recording.
- **`pre` may replace the font.** Keeping the `--font-file` mapped from the check before `pre` locked it on Windows, so a `pre` that copied a new font over it failed. The check releases the file. The render's own mapping also outlived the render, so a `post` that restored or deleted the font failed the same way. The render releases its fonts when it ends.
- **Font links.** FileInfo reports a symbolic link's own size, so a font installed as a link (Ubuntu's font package ships several) failed the table checks and was skipped, while the log still named it. The size comes from the opened file, and the log passes over a family whose regular face fails to load, as **The log** above describes.
- **Ctrl+C skips cleanup that waits for a failure.** Deleting an earlier `.mp4` in the catch block missed Ctrl+C, which ends the process from its handler. Deleting it when encoding starts covers both.
- **Draining frames on Windows.** A stand-in ffmpeg that drained stdin with `more >nul` stopped reading after about 65,500 lines and waited: with lines of 1, 3, 4, 5 or 300 bytes, 65,533 lines went through and 65,534 hung. Lines of 2 bytes still went through at 140,000. A dark background such as `#0a0a0a` puts that many LF bytes in the frames, so the render, which has no timeout there, hung. The tests drain stdin with PowerShell instead.
- **Testing Ctrl+C.** A non-interactive shell starts background jobs with SIGINT ignored, and programs inherit that. A naive `cmd & kill -INT $!` test therefore never reaches the handler; use job control (`set -m`) or start the process directly. On Windows, Ctrl+C was sent with `AttachConsole` + `GenerateConsoleCtrlEvent` to a child in its own console. That is too fragile for CI, so the automated test is Unix only.
- **Encoder defaults for terminal frames (2026-10-29).** The first defaults (`slow`, psy on, x264's keyframe every 250 frames) were revised after measuring where the bytes went.
  - In the typing demo, keyframes were 46% of the video data, while the frames between were tens of bytes. There were three: the blank first frame (0.8KB), a scene cut at 6.0s (46.8KB), and the 250-frame interval at 14.3s (42.1KB). Raising the interval alone removed the last one and saved 21% (200.6KB → 158.5KB).
  - The same frames were then encoded with several settings and compared by SSIM against a lossless reference:

    | Recording | Before | `slower`, psy off, keyframe ≤ 30s | SSIM before → after |
    |---|---|---|---|
    | basic (typing) | 200.6KB | 98.4KB (−51%) | 0.99708 → 0.99733 |
    | interactive-pty (TUI) | 636.6KB | 463.9KB (−27%) | 0.99742 → 0.99737 |
    | pty (small matrix) | 586.9KB | 435.2KB (−26%) | 0.97270 → 0.97467 |
    | matrix 200×60 | 10.0MB | 8.0MB (−20%) | 0.94244 → 0.94761 |
    | matrix 384×108 (refs capped to 5) | 32.1MB | 26.4MB (−18%) | 0.94132 → 0.94566 |

  - With psy on and the same rare keyframes, `veryslow` was 31% smaller than the old default on typing, against 46% for `slower`, and took 1.4–2.1× as long as `slower` in repeated timings.
  - Skipping duplicate frames (`mpdecimate`, variable frame rate) saved less and lowered SSIM.
  - `--crf 25` with the new settings is another 8–19% smaller at roughly the old SSIM. That trade is left to the user rather than made the default.
- **Motion search range (2026-10-30).** x264's `slower` searches motion within 16 pixels, and one row of text is 20 pixels at the default font size, so it missed scrolls.
  - In the typing demo, the frames after a scroll took 1.0–3.3KB each with the 16-pixel search and 0.2–0.8KB with 32 pixels.
  - Sizes at CRF 23 from the `mp4` subcommand, and the time x264 took on the same frames read from a lossless file:

    | Recording | 16 pixels | 128 pixels | Encode time |
    |---|---|---|---|
    | basic (typing) | 98.4KB | 83.3KB (−15%) | 0.8s, unchanged |
    | git (typing) | 226.0KB | 183.7KB (−19%) | — |
    | interactive-pty (TUI) | 463.9KB | 261.0KB (−44%) | 1.8s → 1.9s |
    | pty (small matrix) | 435.2KB | 310.0KB (−29%) | — |
    | matrix 200×60 | 8.0MB | 4.4MB (−45%) | 3.3s → 8.0s |
    | matrix 384×108 | 26.4MB | 14.9MB (−44%) | 10.3s → 16.6s |

  - At equal SSIM, from sizes over CRF 20–29 compared against a lossless render, the 128-pixel search saved 17–21% on typing, 31% on pty, 42% on interactive-pty, and 51% on matrix.
  - The saving grows with the row height. On basic it was 12% at font size 12 (15-pixel rows, inside the old range), 17% at 16, and 72% at 24 (30-pixel rows). On interactive-pty it was 40%, 42%, and 63%.
  - Matrix redraws most characters in place every frame, so its saving does not come from scrolls. It kept growing with the range, which fits the encoder copying the same character from elsewhere on screen.
  - A range just above one row went wrong with exhaustive search: `tesa` over 24 pixels, the `placebo` preset's search, made basic 55% larger (98.4KB → 152.0KB). `umh` over 24 pixels saved only 2–4%.
  - The comparison has to match frames by number. The lossless reference was ffv1 in Matroska, which rounds timestamps to milliseconds, so matching by time paired neighboring frames, and matrix PSNR barely moved with CRF (32.1dB at CRF 20, 32.0dB at 23; 43.3dB and 41.4dB once frames were paired by number).
