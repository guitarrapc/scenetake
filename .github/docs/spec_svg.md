# SVG Output Specification

Status: **Implemented**

## Motivation

For README and documentation embeds, animated SVG is often preferable to GIF: smaller file size, crisp scaling, and easy web embedding without a player.

External tools such as [agg](https://docs.asciinema.org/manual/agg/) (GIF) and [asg](https://github.com/kingsword09/asg) (SVG) can convert cast files, but requiring a second install and a second command adds friction. Built-in SVG output lets users produce `.cast` and `.svg` in one invocation while keeping cast as the canonical artifact defined in [spec_cast.md](spec_cast.md).

Rich TUI recordings (`copilot --banner`, `sl`, `matrix`, etc.) work on the scenario path when a step sets `pty: true` ([spec_pty.md](spec_pty.md)), or from **external asciinema casts** converted via the `svg` subcommand.

## Scope

- SVG output via `--format svg` or the `svg` subcommand. CLI: [spec_cli.md](spec_cli.md). Scenario `render:` keys: [spec_scenario.md](spec_scenario.md).
- Built-in C# SVG renderer (no bundled external binary).
- **Row-diff** animated SVG (changed rows only, each switched on and off by step-end CSS keyframes) with optional **`--max-fps`** frame sampling (default **off**).
- VT emulator. (see [VT emulator](#vt-emulator)).
- Theme presets `dark` (default) and `light`; default `font-size` of `16`; default `font-family` stack tuned for programming.
- Block cursor (`theme.fg` at 50% opacity, no blink); DECTCEM visibility (`\e[?25h`, `\e[?25l`).
- Warn-and-continue for unsupported cast event codes.
- Resize cast events (`"r"`) during `svg` conversion.
- **Window chrome** (v2): optional `macos` or `windows` frame. Default is plain terminal (no chrome).

Cast file format (header, versions, written event codes): [spec_cast.md](spec_cast.md).

## `svg` Subcommand — Input and Events

- Accepts cast files defined in [spec_cast.md](spec_cast.md) (asciicast v2 or v3, read-only).
- Render metadata resolution: CLI overrides > cast header > defaults. See [spec_cli.md](spec_cli.md).
- Processes `"o"` (output) and `"r"` (resize) events. Silently skips `"i"`, `"m"`, and `"x"`. Other codes warn once (warn-and-continue).
- Invalid or out-of-range resize sizes warn once and are skipped.
- Canvas size is the maximum terminal dimensions across the header and all valid `"r"` events. On shrink, a per-frame viewport clip shows the current terminal size within the fixed canvas (`preserveAspectRatio` on the root `<svg>`).

## Renderer Requirements

### VT emulator

| Category | Support |
|---|---|
| CSI cursor (`A`–`F`, `G`, `H`, `d`) | Yes |
| Erase (`J`, `K`, `X`), insert/delete (`@`, `P`, `L`, `M`, `S`, `T`) | Yes |
| Scroll region (`r`), save/restore (`s`, `u`, `ESC 7/8`) | Yes |
| Alternate screen (`?1049h/l`), cursor visibility (`?25h/l`) | Yes |
| SGR: bold, faint, italic, underline, reverse, 16/256/true color | Yes — `;` and `:` forms |
| Unicode wide chars, surrogate pairs, combining marks, VS | Yes |
| OSC, DCS other than soft fonts | Skipped (not rendered) |
| Soft fonts (DECDLD), G0–G3 designations, SI/SO, LS2/LS3, SS2/SS3 | Yes — soft font glyphs are drawn from their bitmaps; other sets (DEC line drawing) draw as ASCII (see [Soft fonts](#soft-fonts)) |
| Block elements U+2580–U+259F | Yes — rects covering their exact part of the cell, as in mp4 (see [Row encoding](#row-encoding)) |

Tests: `tests/terminal_tests.cs`.

### Soft fonts

A program can load its own glyphs into the terminal with a DECDLD string and draw them with plain ASCII codes, as `matrix --mode movie` does. Without soft font support the DCS was skipped, so a recording showed the ASCII codes (`#`, `2`, `=`) instead of the glyphs.

- **Loading.** `ESC P Pfn;Pcn;Pe;Pcmw;Pss;Pt;Pcmh;Pcss { name glyph;glyph… ESC \`.
  - Glyphs load from code 0x20 + Pcn, at 0x21 at the lowest for a 94-character set (Pcss 0).
  - The matrix is Pcmw × Pcmh pixels, with Pcmw 2–4 the VT220 widths 5–7. A size given as 0 comes from the data.
  - Pe 0 replaces the font of that name, 1 only the codes loaded, 2 drops every font first.
  - Other DCS strings (Sixel, DECRQSS, tmux passthrough) stay skipped.
- **Selecting.** `ESC (`, `)`, `*`, `+` (94-character sets) and `ESC -`, `.`, `/` (96-character sets) name a set into G0–G3. The name may carry an intermediate, as in `ESC ( SP @`. SI, SO, LS2, and LS3 pick the set for GL, and SS2 and SS3 for one character. A font is used wherever a designation names it, whether the designation comes before or after the font loads. DECSC and DECRC save and restore the designations and GL with the cursor, and RIS drops every font.
- **One font, two byte streams.** Both draw the same cells:

  | Path | Font name | Set | Characters |
  |---|---|---|---|
  | Unix, or the out-of-band ConPTY | as the program wrote it (` @`) | G0 (`ESC ( SP @`), then `ESC ( B` | plain ASCII |
  | The in-box Windows ConPTY | rewritten to `@` | G1 (`ESC ) @`) | each run between SO and SI |

- **Cells.** A cell keeps the glyph it was written with, and the code as its text. A code the font does not define draws blank, as on DEC terminals. Glyphs are compared by bitmap, so the same font loaded twice draws the same.
- **Drawing.** The matrix is stretched to the cell whatever its size, as terminals do: 12×24 becomes 9.92×20 at 16px. The SVG draws each glyph as one path in `<defs>` and each cell as a `<use>` of it ([Row encoding](#row-encoding)). mp4 scales the bitmap ([spec_mp4.md](spec_mp4.md) → Fonts).
- **Whether a program sends a soft font is up to the program.** It sees scenetake's PTY, not the host terminal, so it cannot confirm that the recording draws soft fonts. The in-box ConPTY answers the device attributes query (DA1) itself, and on Unix nothing answers it. The scenario tells the program instead: `run: matrix --mode movie --soft-font`. matrix 1.2.0 has no `--soft-font`, and under scenetake it uses the soft font only on Windows with `WT_SESSION` set.

### Animation and layout (plain terminal)

- Replay follows cast event timestamps. **`--max-fps`** (default **off**) caps sampling when set.
- **The scenario path renders the cast it just wrote**, with its millisecond times, markers dropped, and the header's render settings, plus `--max-fps` from the CLI. `scenetake --format svg` therefore writes the same SVG as `scenetake svg` on the resulting cast.
- Output is self-contained animated SVG (CSS only, no JavaScript).
- Animations **loop indefinitely**; one cycle matches agg GIF timing with `--last-frame-duration 0`: cast playback through the last output event, then hold the final frame for the preceding output-to-output idle gap before restarting at `t = 0`.
- **Row-diff** animation: only changed rows are emitted as timed layers. Cursor and viewport resize use separate layers. Row encoding: [Row encoding](#row-encoding).
- **Layer timing.**
  - Every animated element has class `l` (hidden, with one shared rule for loop length, step-end timing, and looping) and a keyframe class.
  - Each keyframe lists only its switch points, for example `@keyframes x{4.969%{opacity:1}5.245%{opacity:0}}`. Offsets have just enough decimals to resolve 1ms of the loop.
  - With step-end timing, a layer and its replacement switch at the same offset, with no fade.
  - Keyframes are shared by identical show and hide times, so a full-screen redraw costs one keyframe.
  - **Class names are short and lowercase.** Rows, colors, and keyframes draw from one pool, so names never collide. The first 959 names are one or two characters. Rows and colors take names as they first appear. Keyframes are named last, most used first. Keyframes nothing refers to are not written.
  - Names are lowercase because HTML in quirks mode matches class selectors case-insensitively, so `.a` and `.A` would collide in an SVG inlined in such a page.
  - **Screens replaced at the same cast time are dropped.** Without `--max-fps`, when several screens share one cast time, only the last is drawn. The others would be shown and hidden at the same offset, so they are never visible.
- Trailing blank frames after alternate-screen restore are trimmed when the event stream indicates a screen clear.
- Background from `theme.bg`; monospace font at resolved `font-size` and `font-family`.
- Cell metrics: `CharWidthFactor` 0.62, `LineHeightFactor` 1.25.
- Padding when `window` is `none`:
  - **Outer:** 8px transparent margin.
  - **Inner:** horizontal `font-size × 10/16`, vertical `font-size × 4/16`, clamped (horizontal 4–16px, vertical 2–8px).
- Block cursor at the emulator position when visible; hidden by `\e[?25l`.
- **Matrix rain tint:** ANSI bright white (`palette` index 7) horizontally adjacent to green (`2` or `10`) on the same row renders as bright green (`10`). Helps `cmatrix`-style casts where white highlights sit in green columns. Vertical neighbors are ignored so output on the line below a `run-highlight` command is not tinted.

### Row encoding

Each row layer is one line. It holds the row's background rects, then its block elements, then its soft font glyphs, then its text. Lines end with `\n` on every OS.

- **Background.** One `<rect>` covers each run of cells with the same non-theme background, whatever the foreground or style. A single CSS rule gives these rects crisp edges.
- **Block elements** (U+2580–U+259F) are rects covering their exact part of the cell, as the mp4 rasterizer draws them:
  - halves, eighths, and quadrants;
  - shades (░ ▒ ▓) as 25%, 50%, and 75% `fill-opacity`;
  - parts of one color and height that touch along the row merge into one rect.
- **Soft font glyphs** ([Soft fonts](#soft-fonts)) are one `<use href="#id" x>` per cell, at the text baseline like a `<text>`, so row-local content needs no `y`. The color is a fill class, or inherited for the theme's foreground.
  - Each glyph is one `<path>` in `<defs>`: its pixels as rects in the glyph's own pixels, scaled to the cell and moved up by the baseline offset. One path per glyph keeps anti-aliased rects free of seams.
  - Ids are given on first use. They skip `c` (the cursor) and names starting with `r` (reused rows are `r0`, `r1`, …).
- **Text.** Each row uses whichever of two encodings is shorter:
  - **Run form:** one `<text>` per run of identical style, spread to the run's cells with `textLength`. This is small for prose and typing.
  - **Glyph-list form:** one `<text>` per foreground and weight/slant, with an `x` for each glyph and spaces left out. This is small for sparse colored output such as matrix rain, gauges, and TUI chrome.
- **Rows with underlined cells** always use the run form, because the underline also runs under spaces between words. Underlined spaces are drawn with their underline up to the row's last underlined character that is not a space, as in mp4. Spaces after it are left out like any other trailing spaces. An underlined block element or soft font glyph puts an underlined space in the run over its cell, so the underline also runs under it, as in mp4.
- **Colors.** Text in the theme's foreground has no fill of its own; it inherits one from the group around all rows. Any other text color is a class with one CSS rule. Rects keep `fill` attributes, so that the crisp-edge rule still matches them.
- **Row-local coordinates.** A row layer may draw its content with the baseline at y=0, so its `<text>` elements carry no `y`. A per-row class (`transform: translate(0, Ypx)`) then moves the layer to its row.
  - Each layer keeps whichever form is shorter.
  - A row gets its class once the bytes its layers would have saved pay for its CSS rule.
- **A layer of one `<text>`** carries the layer's classes on the `<text>` itself, without a `<g>`.
- **Empty layers** (a row that went blank) draw nothing and are left out.
- **Multi-unit cells.** In the glyph-list form, a cell whose text is more than one UTF-16 unit (surrogate pair, combining mark) gets its own `<text>`. SVG gives one `x` per addressable character, and browsers count those differently. The run form needs no split, because its `textLength` is the width of the run's cells, not its length in UTF-16 units.
- **Characters XML forbids.** U+FFFE and U+FFFF are written as U+FFFD, because one of them makes the whole SVG unreadable.
- **No default attributes** (`font-weight="normal"`, `lengthAdjust="spacing"`) are written.
- **Scrolled rows are reused.** When a row's content already appeared in an earlier layer on another line (output scrolled), the layer is a `<use>` of that earlier content instead of a copy.
  - The first copy gets an `id`.
  - Row-local content is compared byte for byte as it is, and the `<use>` takes the new row's class.
  - Absolute content is re-encoded at the earlier line, compared, and shifted with `y="dy"`.
  - Rows shorter than a `<use>` are written out.
- **The cursor** is one `<rect id="c">` in `<defs>`, and each position is a `<use href="#c" x y>` of it.
  - A position shown and hidden together with a row layer on its own row goes last inside that layer, with no timing of its own.
  - Other positions carry their own keyframe class.

The glyph-list form puts each glyph at its cell's left edge. The run form lets the browser spread glyphs with `textLength`, which can place them up to "cell width − font advance" apart. That is a sub-pixel shift for the default fonts (about 0.3px with Cascadia Mono at 16px), so rows look the same either way.

| Cast (`svg` subcommand) | 1.4.2 | Row encoding | + timing and reuse | + classes, block rects, row-local | + unseen frames dropped, lowercase names |
|---|---|---|---|---|---|
| `samples/basic.cast` (typing) | 130KB | 108KB | 56KB | 46KB | 46KB (−65% overall) |
| `samples/scenario_format.cast` (typing) | 433KB | 362KB | 183KB | 147KB | 147KB (−66%) |
| `samples/interactive-pty.cast` (TUI) | 1.31MB | 0.96MB | 0.53MB | 0.23MB | 0.23MB (−82%) |
| `samples/pty.cast` (small matrix) | 2.39MB | 0.61MB | 0.56MB | 0.43MB | 0.41MB (−83%) |
| `matrix --fps 60`, 200×60, 7.5s | 117MB | 26.9MB | 26.4MB | 19.7MB | 17.0MB (−85%) |
| Same, `--max-fps 20` | 25.6MB | 5.9MB | 5.8MB | 4.3MB | 4.4MB (−83%) |

Every sample got smaller at each step except the last, where lowercase names cost typing samples, interactive-pty, and matrix with `--max-fps 20` a little.

- **Timing and reuse** took 41–50% off typing and TUI samples, where CSS was about 40% of the file and scrolled rows 7–36%. It took 2–8% off matrix output, where glyphs dominate.
- **Classes, block rects, and row-local coordinates** took another:
  - 13–20% off typing samples;
  - 56% off interactive-pty, half of which was block elements as stretched glyphs;
  - 23–26% off matrix output, from fill classes and row-local `y`.
- **Dropping frames that are never visible** took 5% off pty and 14% off matrix 200×60 (25% at 384×108). **Lowercase names** fit fewer names in one character, which added 0.3–0.9% to typing samples and 0.3% to interactive-pty and to matrix with `--max-fps 20`. With class names masked, the typing samples and that matrix file are byte-identical. In interactive-pty the longer names also moved 3 rows from row-local to absolute coordinates.

Sizes are of LF files.

### Memory

- Rendering memory stays near the output size, not events × screen size. The replay timeline holds no screen contents, and each row layer is encoded as soon as it is created.
- Rows scrolled off the top are dropped, because nothing renders scrollback.
- Measured with a full-screen `matrix --fps 60` recording on Windows 11:

  | Terminal | SVG | Peak memory |
  |---|---|---|
  | 200×60 | 17.0MB | 116MB (was 4.8GB) |
  | 384×108 | 53.2MB | 272MB (did not finish before: over 43GB) |

### Window chrome

Optional OS-style frame for README embeds. Configured via `render.window` in scenario YAML, `st:window` in v3 cast `tags`, or `--window` on CLI. Default: **`none`** (plain terminal above).

| Source | Key / flag | Values |
|--------|------------|--------|
| Scenario | `render.window` | `macos`, `windows` (omit = `none`) |
| Cast header (v3) | `st:window=…` in `tags` | Written when not `none`; omitted otherwise |
| CLI | `--window` | `none`, `macos`, `windows` |

Priority: **CLI > cast header > default `none`**. Unknown values error on scenario path and CLI; invalid cast tag warns once and falls back to `none`.

#### Visual behavior

- Replaces the plain 8px outer margin with a title bar, border, rounded outer frame, and drop shadow.
- **Title bar:** controls only (no title text). macOS: three traffic-light circles (left). Windows: three square buttons (right).
- **Title bar shape:** top corners rounded; bottom edge straight (flush with terminal viewport — avoids a gap between bar and terminal).
- **Chrome colors:** derived from terminal `theme.bg` luminance (light vs dark chrome palette). `theme.preset` and resolved header colors drive this; per-key `theme.fg` / `theme.bg` overrides affect **terminal content only**, not chrome palette selection beyond bg luminance.
- **Resize:** chrome width/height follow viewport `"r"` events; chrome elements share `viewport-*` layer animation timing with the terminal background mask.
- **Inner terminal padding** (around cell content) is unchanged from plain mode.
- **mp4** draws the same title bar and controls but leaves out the outer margin, shadow, rounded corners, and border, because video has no transparency. See [spec_mp4.md](spec_mp4.md) → Window chrome.

#### Fixed chrome geometry (px)

Does **not** scale with `font-size` (matches real OS window chrome). Tuned at default `font-size` 16.

| Constant | macOS | Windows |
|----------|-------|---------|
| Title bar height | 34 | 34 |
| Outer margin (shadow) | 12 | 12 |
| Side padding (controls inset) | 14 | 14 |
| Top padding (controls) | 9 | 9 |
| Outer corner radius | 8 | 4 |
| Control size | circles Ø16, gap 10 | squares 17×17, gap 5.5 |

#### Samples

- `samples/theme.yaml` — color demo, no chrome.
- `samples/theme-macos.yaml` — `window: macos`, `preset: dark`.
- `samples/theme-windows.yaml` — `window: windows`, `preset: dark`.

## Failure Behavior

### Scenario path (`--format svg`)

| Phase | On failure |
|---|---|
| Output path a directory, the output is the scenario file, or the output folder missing when there is no `pre` | Checked before `pre`: error, nothing recorded, exit `1`. |
| `pre` | Fail-fast; no cast or SVG. See [spec_pre_post.md](spec_pre_post.md). |
| An output check fails after `pre` (folder missing, or `pre` put a directory or a link to the scenario where an output goes) | Error before the recording; `post` runs; exit `1`, or the failing `post` command's code. |
| Cast write | Error; SVG not attempted; `post` runs; exit `1`, or the failing `post` command's code. |
| SVG render | Cast retained; partial `.svg` deleted, or an earlier one kept while another program holds it open without delete sharing (Windows); exit non-zero; `post` still runs. |
| Written SVG over 10MB | Warning suggesting `--format mp4` or `--max-fps` ([spec_cli.md](spec_cli.md) → Logging); files kept, exit code unchanged. |
| `post` | Fail-fast; cast (and SVG if written) remain. See [spec_pre_post.md](spec_pre_post.md). |

### `svg` subcommand

| Situation | Behavior |
|---|---|
| The output is the input file (for example `scenetake svg image.svg`) | Error before the input is read; the input is untouched; exit `1` |
| Output folder missing, or output path is a directory | Error naming the path; exit `1` |
| Cast not found | Exit `1`; an earlier `.svg` of the same name is kept |
| Invalid header, missing/out-of-range terminal size, invalid event JSON | Exit non-zero; no SVG written; an earlier `.svg` of the same name is deleted, unless another program holds it open without delete sharing (Windows) |
| SVG render failure | Exit non-zero; partial `.svg` deleted, or an earlier one kept while another program holds it open without delete sharing (Windows) |
| Unsupported event codes, invalid resize | Warning only; continue |
| Written SVG over 10MB | Warning suggesting mp4 or `--max-fps` ([spec_cli.md](spec_cli.md) → Logging); file kept, exit `0` |

The input file is never modified by the `svg` subcommand.

## Testing

| Suite | Coverage |
|-------|----------|
| `tests/terminal_tests.cs` | VT emulator (CSI, Unicode, alt screen). Soft fonts: both byte streams, a DECDLD split across chunks, a designation before the load, undefined codes, the matrix size from the data, erase control, other DCS strings skipped, glyph cells against text cells, DECSC/DECRC, RIS, single shifts |
| `tests/svg_render_test.cs` | Row-diff smoke, marker timing, `--max-fps`, matrix rain tint, streamed file output matches in-memory render, row encoding choice (glyph list vs run, underline, multi-unit cells, background spans, shared keyframe rules), combining marks, block and shade rects, LF line endings, inherited foreground and fill classes, single-text layers, row-local content in every layer form, short lowercase class names, unused keyframes left out, dropped zero-length frames, run `textLength` in cells, underlined spaces and block elements, plain block elements kept out of run text, block-only rows decoded to their row, XML-invalid characters, scenario theme color validation, cast warning prefix, soft font glyphs as `<use>`s of `<defs>` paths (path pixels, scale, position, color), both soft font byte streams giving the same SVG, underlined glyphs, glyph ids. Text checks decode every layer form back to row text, color, and row, and cursor checks cover cursors inside row layers and with their own timing, so a layer or cursor drawn on the wrong line fails. |
| `tests/scenario_render_test.cs` | Scenario path (`--format svg` / `--format mp4`) output is byte-identical to `scenetake svg` / `scenetake mp4` on the written cast (default, `--max-fps`, render overrides). Needs `SCENETAKE_BIN`; mp4 also needs ffmpeg. |
| `tests/window_chrome_test.cs` | `render.window` / cast tag / CLI resolution; chrome SVG structure |

Run: `dotnet run tests/terminal_tests.cs` (and sibling test scripts). CI runs all four.

**Encoding changes are also checked by hand**, because tests read markup, not pixels.

- **Look.** Render every sample and fixture with the old and new code. Screenshot both through `<img>` in headless Chromium, at 5 points of the loop. Pin the time inside the SVG's own `<style>` (`animation-delay: -Ts; animation-play-state: paused`), then compare pixels.
- **Playback cost.** Play each SVG for a few seconds through `<img>` and read the requestAnimationFrame intervals and the main-thread time over the DevTools protocol.

## Cross-Document Notes

- [spec_mp4.md](spec_mp4.md) — mp4 output for full-screen TUIs and sharing; reuses this emulator, the cell color rules (including the matrix rain tint), the cell metrics, and the loop timing.
- [spec_cast.md](spec_cast.md) — cast header, `st:window` tag, v2 header rules.
- [spec_scenario.md](spec_scenario.md) — `render.window`.
- [spec_cli.md](spec_cli.md) — `--window`.
- [spec_highlight.md](spec_highlight.md) — cast-event coloring (header `theme` is separate).

## References

- [agg usage](https://docs.asciinema.org/manual/agg/usage/)
- [asg](https://github.com/kingsword09/asg)

## Alternatives Not Chosen

| Idea | Why not |
|---|---|
| Competing with video for full-screen TUIs | SVG is lossless and symbolic, so its size grows with changed cells × frames. Even compact, 4K 60fps matrix is about 53MB, and X does not accept SVG. Those recordings go to mp4 ([spec_mp4.md](spec_mp4.md)). |
| `.svgz` | It renders only when the server sends `Content-Encoding: gzip`. File-size limits and viewer memory apply to the decompressed SVG anyway. |
| `--max-fps` on by default | Full timing matters for typing and bulk output such as curl. The size warning suggests it instead. |
| One cursor element moved by keyframes | 14–24% smaller typing SVGs. But moving one element with `translate`, `transform`, or CSS `x`/`y` added 50–70% main-thread time on busy recordings in headless Edge. Many cursor elements that only switch `opacity` cost nothing extra. |
| One shared timeline (an animated custom property `--f`, each layer's show and hide as numbers) | 13–20% smaller typing SVGs. Animated on every layer, it tripled main-thread time (basic: about 110 → 335 ms/s). Animated once on the root, it was cheaper for typing, but it dropped frames on pty and interactive-pty (p95 33–50ms). |
| Per-cell lifetimes for matrix-like output | Only 14% of matrix 200×60 glyphs (5% for pty) carry over unchanged, glyph and color, into the next layer of their row. Per-cell timing would cost more than it saves. |
| Soft font glyphs as private-use characters in the cell text | No renderer would need to change, but Nerd Font icons and Powerline symbols use the same range and would be drawn as soft font glyphs. |
| scenetake naming itself to commands (`TERM_PROGRAM=scenetake`) so they use their soft font | It worked for matrix on Windows and in WSL. But each program would have to know scenetake by name, and it would replace the host's `TERM_PROGRAM` for every command. A program flag such as matrix's `--soft-font` keeps both tools independent. |
| Soft font glyphs as a web font in a `data:` URI | It would keep the compact glyph-list form for matrix rain. But it needs a font writer, and the browser's font rasterizer would not match the mp4's bitmap scaling. Not tried. |
| Cell-unit coordinates (`scale(cellWidth)` per row, integer `x`) | Another 8 points off matrix output. But matrix 200×60 played about 1.5× slower per frame, and text drawn at 1.6px and scaled up is unchecked in Firefox and Safari. Chromium does not apply its minimum font size to SVG inside `<img>`. |

## Lessons Learned

- Row-diff layers keep typing-heavy casts small (46KB for `basic.cast`). Full-screen redraws emit one layer per changed row.
- **Where the bytes went.** Before the row encoding change, a full-screen matrix SVG spent about 113 bytes per `<text>` for 1.26 characters on average. Because matrix paints its own black background, it also had a background `<rect>` per text run. Those two were 57% and 40% of the file.
- **Choose the encoding per row.** A glyph-list encoding applied to every row made the typing demo 19% larger, because prose is cheaper as runs. Choosing per row made every sample smaller.
- **Grouped animation rules.** With one animation rule per row layer, the `<style>` block of the 200×60 matrix SVG was 1.19MB. Grouping row layers by keyframe cut it to 0.36MB. (Later replaced by one class per keyframe; see below.)
- **Combining marks.** A cell holding a combining mark (for example a decomposed `é`) crashed rendering in 1.4.x, because the block-element check assumed a second UTF-16 unit was a low surrogate.
- **Render from the cast, not from memory.** The scenario path used to render the in-memory events, whose times are not rounded to milliseconds and which still include markers. The same recording then gave a different SVG than `scenetake svg` on its cast. For a matrix recording that was 8,402 keyframes against 719, and 118MB against 117MB before the row encoding change. Reading the written cast back makes the cast the single source for every derived output.
- Frame sampling is optional (`--max-fps`); default off keeps full timing for typing and bulk output (e.g. curl).
- Trailing blank alternate-screen restore frames should be trimmed when the event stream contains screen-clear sequences.
- Window chrome should stay **fixed px** like a real desktop window; scaling chrome with `font-size` looked wrong in README embeds.
- Title bar corners must be rounded on the **top only**; a fully rounded title-bar rect leaves visible gaps where it meets the terminal viewport.
- Chrome control layout (button size, padding, title bar height) needed visual iteration; fixed constants are preferable to formula-based scaling.
- Snapshotting the whole screen (both main and alternate grids) on every changed event made memory grow with events × screen size: 4.8GB for a 200×60 full-screen TUI, and more than 43GB at 384×108.
  - Replaying the events twice, once to build the timeline and once to draw the selected frames, costs less than keeping snapshots.
  - It also cut render time from 5.8s to 2.6s at 200×60.
- **Animation CSS dominated typing demos.** After the row encoding change, about 40% of a typing SVG was CSS: one keyframe per layer with 6-decimal ranges and a millisecond fade, plus one `animation:` rule per layer and per cursor move. Step-end keyframes with only switch points, ms-level precision, one shared `.l` rule, and a class per keyframe halved typing and TUI SVGs.
- **Scrolling repeats rows.** Output that scrolls rewrites every row it moves. Reusing the first copy with `<use>` removed 7–36% of typing and TUI samples (interactive-pty most). It needs a byte-exact check, because the matrix-rain tint depends on neighbors. It also needs a CSS selector that still matches inside `<use>` content (`rect:not([class])`, not `g.layer>rect`).
- **A CSS rule on `text` overrides `fill` attributes.** Setting the default color with `text{fill:…}` turned every colored line into the default color. The default has to be inherited from an ancestor instead.
- **Block elements: rects, done properly.**
  - Before #38, blocks were already rects, but only the halves and the full block had their own shape. Every other block glyph, including ▘ and ▔, filled the whole cell. #38 switched to font glyphs stretched with `textLength` and `lengthAdjust`.
  - Font glyphs keep the font's height, so input boxes in TUIs showed seams between rows and cells. They also cost about 100 bytes per cell, half of interactive-pty.
  - Rects sized per glyph, as mp4 draws them, fix both.
- **Choosing row-local coordinates layer by layer did not work at first.** Charging the row's CSS rule to each layer meant typing layers, which save about 10 bytes each, never paid for it. Adding up the savings per row until they pay for the rule fixed it.
- **Check rendering through `<img>`.**
  - Screenshots of an SVG inlined in HTML changed text anti-aliasing with some animation setups, presumably because of separate compositing. Through `<img>`, as GitHub shows it, the same files were pixel-identical.
  - The run/glyph-list choice can flip when attribute lengths change. That shifts glyphs by under a pixel, with no visible change.
- **A literal `<` in `<style>` is an XML error**, for example `@property` `syntax: "<integer>"`. Inlined in HTML, the stylesheet is silently dropped instead. Escape it as `&lt;`.
- **TUIs write one screen several times within a millisecond.** In matrix 200×60, 2,711 layers had a zero-length keyframe, 2.7MB of a 19.7MB SVG that was never visible.
- **Scrollback that nothing reads still costs memory.** The screen kept every row scrolled off the top. A cast that scrolls 300,000 lines peaked at 1,050MB for a 2KB SVG. Dropping those rows brought it to 144MB, with the same output.
- **Render time.** Two changes took matrix 200×60 from 3.3s to 2.3s and 384×108 from 23s to 14.8s, with byte-identical output. First, each changed row is encoded row-local, and that pass also measures the absolute form. The absolute form is then encoded only when it is kept or when the reuse check compares it with an earlier absolute layer, instead of for every row. Second, the drawing pass no longer hashes every screen again. With unseen frames dropped, they take 1.7s and 10.1s (Windows 11).
- **The in-box ConPTY re-encodes soft fonts.** matrix writes its font as ` @` and designates it into G0. Through the in-box ConPTY of Windows 11 (build 26200), the same font arrived renamed to `@`, designated into G1, with every run of its codes between SO and SI, and the host answered DA1 itself. Through the out-of-band ConPTY 1.25, the bytes arrived as written, as on Unix. Handling designations and shifts in general covers both.
- **`ESC ( SP @` left an `@` on screen.** A designation was read as exactly one byte after `(`, so a set name with an intermediate printed its final byte.
- **Rows with no text can show seams outside `<img>`.** Row-local layers move to fractional baselines, 24.8px at 16px.
  - A layer with no `<text>`, such as a row of soft font glyphs or block elements on a black background, showed a faint line between rows when the SVG was opened directly or inlined in HTML in Edge. Through `<img>` the same file showed none.
  - Rounding the row translates removed the lines in the inlined test. It would also move every text baseline by up to half a pixel, so it is not done.
- **Soft font SVGs are larger.** A 60×16, 3s matrix recording was 420KB with the soft font, against 273KB with `--mode kana`. A glyph costs a `<use>` of about 30 bytes, where the glyph-list form spends one character and an `x`. The one-cell glyphs also give matrix twice as many columns.
- **matrix 200×60 is past what a browser can play.** It plays at 1–2 frames per second in headless Edge, with or without GPU. Shrinking it matters for file-size limits, not for playback.
