# Plan A — SVG Memory and Size

Status: **Proposed** (2026-10-29)

Background, measurements, and ordering: [plan_output_size.md](plan_output_size.md).

## Goals

- Rendering memory proportional to the output size, not to events × screen size. 4K casts must render.
- Smaller SVG for full-screen and colored output, with no visual change and no regression for typing demos.
- Tell users when SVG is the wrong format for a recording.

## Non-Goals

- Making SVG competitive with video for heavy TUIs.
- Changing timing semantics, the `--max-fps` default (off), or the row-layer animation model in [spec_svg.md](../spec_svg.md).

## A1. Streaming Replay (Memory)

Status: **Implemented** (2026-10-29). See [What Was Built](#what-was-built) and [Results](#results). The rest of this section is the original proposal.

### Before

| Step | What it holds |
|---|---|
| `TerminalReplay.BuildFrames` | A `List<ReplayFrame>`. Each frame owns a `ScreenBuffer.Clone()` of both the main and alternate grids. |
| `OptimizeFrames` (`--max-fps`) and `TrimTrailingBlankRestore` | The whole frame list. |
| `SvgFrameRenderer.BuildLayers` | One `AnimLayer` per changed row, each pointing at a whole frame snapshot. Every snapshot stays alive until `BuildLayeredSvg` returns. |
| `BuildLayeredSvg` → `File.WriteAllText` | One `StringBuilder`, then a `string`. The output exists two to three times in memory. |

Result: matrix 200×60 peaks at 4.3GB, and 384×108 exceeded 43GB.

### Change

1. **Row layers keep only their encoded row.**
   - When a layer is created, encode its row into an in-memory UTF-8 buffer (pooled chunks) and keep only the offset and length.
   - The matrix-rain tint looks only at same-row neighbors, so a row is self-contained.
2. **Frames become a stream.**
   - Replay events and hand each changed state straight to the layer builder, with no list of full snapshots.
   - The `--max-fps` stages need only bounded lookahead:
     - timing normalization needs nothing;
     - frame reduction needs one pending frame;
     - spreading collapsed times needs one run of equal times;
     - trailing blank trim needs the trailing run of blank frames.
   - Implement them as stages over the stream. `IsLikelyFullClear` needs only the previous frame.
3. **Snapshot only the active grid** and cursor state, not both grids.
4. **Write once.**
   - The `<style>` block needs every layer's timing, so the head and style go out first and the buffered row fragments follow in show order.
   - Stream to a temporary file and keep the existing rule that a partial `.svg` is deleted on failure.
   - Peak memory is then about the output size plus one screen.

### Constraints

- **The output must be byte-identical** to today's for every `samples/*.cast` and `tests/fixtures/*.cast`, with and without `--max-fps 20`, on both the scenario path and the `svg` subcommand. This step is a pure refactor, so any difference is a bug.
- Compare on the same OS. `AppendLine` writes the platform newline, so SVG bytes already differ between Windows and Linux.

### Acceptance

- Byte-identical check passes. Render with the old and new code and compare SHA-256.
- matrix 200×60 at 60fps (`svg` subcommand): peak working set at most 1GB, down from 4.3GB.
- matrix 384×108 completes. Record size, time, and peak memory in the PR.
- Sample render time stays within ±10% of today.

### What Was Built

It differs from the proposal in one way. Instead of lookahead stages over one stream, the replay runs twice.

1. **Timeline pass.** `TerminalReplay.BuildFrames` replays the events and records only metadata for each changed state: time, capture ordinal, viewport size, signature, and whether the screen is blank. The existing trailing-blank trim and `--max-fps` stages run on that list unchanged.
2. **Draw pass.** `SvgFrameRenderer.BuildLayers` replays the events again through the same `TerminalReplay.EnumerateCaptures` and stops at each selected ordinal.
   - It compares rows against one copy of each row's active layer and one copy of the previous frame.
   - It encodes each new row layer, including its `<g>` wrapper, straight into UTF-8 chunks.

Why two passes:

- The selection logic stays exactly as it was, which made byte-identical output easy to guarantee.
- Replaying is cheaper than the clones it replaces.

The head and `<style>` are built after the draw pass and written ahead of the encoded rows. The file is opened only after rendering succeeds.

### Results

Rendered with a harness that calls `SvgRender.WriteSvg` the way the `svg` subcommand and the scenario path do. Windows 11, .NET 10, Release build.

| Check | Before | After |
|---|---|---|
| Byte-identical: 16 sample/fixture casts × (`svg`, `--max-fps 20`, scenario path) | — | 48 of 48 identical |
| Byte-identical: matrix 200×60 × 3 modes | — | 3 of 3 identical |
| matrix 200×60, 117MB SVG: peak / time | 4.8GB / 5.8s | 234MB / 2.6s |
| matrix 200×60 `--max-fps 20`, 25.6MB SVG: peak / time | 4.4GB / 3.9s | 139MB / 2.0s |
| matrix 384×108: SVG / peak / time | Aborted above 43GB | 455MB / 673MB / 22s |
| matrix 384×108 `--max-fps 20`: SVG / peak / time | — | 85MB / 314MB / 16s |
| All 16 small casts in one process: peak | 298MB | 64MB |

Tests:

- All unit suites pass.
- `stream_output_test`, `pty_test`, and `interactive_pty_test` pass against a Native AOT win-x64 build.
- New test: `WriteSvgMatchesRenderToString` checks that the streamed file equals the in-memory render, including resize shrink, `--max-fps`, and output larger than one chunk.

### Observation to Resolve Separately

The scenario path and the `svg` subcommand produce different SVGs from the same recording.

- Matrix example: 118MB with 8,402 keyframes on the scenario path, and 117MB with 719 keyframes from the `svg` subcommand.
- Cause: the scenario path uses an unrounded loop duration (7.701573s), while the cast stores rounded times (7.702s). Keyframe percentages then deduplicate differently.
- This is not a size problem by itself. Compare like with like in acceptance checks, and decide later whether to unify the two.

**Resolved (2026-10-29).** The scenario path now renders SVG and mp4 from the cast it just wrote, not from the in-memory events. Two things differed:

- Event times: the cast stores millisecond intervals.
- Markers: the cast reader drops them, so a trailing marker no longer stretches the loop.

The scenario-path output is therefore byte-identical to `scenetake svg|mp4` on the written cast. `tests/scenario_render_test.cs` checks this with default settings, `--max-fps`, and render overrides, plus mp4 when ffmpeg is available. Before the fix, the three SVG cases failed.

## A2. Compact Row Encoding

Status: **Implemented** (2026-10-29). See [A2 Results](#a2-results). The rest of this section is the original proposal.

### Prototype Results

These numbers come from a scratch copy of `Svg.cs` with an environment-variable switch to a compact encoder. All runs used the `svg` subcommand.

| Cast | Today | Compact |
|---|---|---|
| matrix 200×60, 60fps | 117MB | 28MB |
| matrix 200×60, `--max-fps 20` | 25.6MB | 6.2MB |
| `samples/pty.cast` | 2.37MB | 0.66MB |
| `samples/interactive-pty.cast` | 1.30MB | 1.06MB |
| `samples/basic.cast` | 129KB | 153KB (+19%) |

Headless Edge screenshots of the today and compact matrix SVGs were pixel-identical. Those screenshots were not pinned to an exact animation time, so A2 needs the stronger check under [Visual Verification](#visual-verification).

The basic regression comes from per-glyph positions, which cost more than `textLength` runs for prose. That is why the encoding below is a per-row hybrid.

### Changes

1. **Background spans.**
   - Emit one `<rect>` per maximal run of the same resolved background in the row, independent of foreground and style runs.
   - The row's rects come before its text.
2. **Hybrid text encoding, chosen per row.** Encode each row both ways and keep the shorter:
   - **Run form (today):** one `<text>` per style run, with `textLength`.
   - **Glyph-list form:** one `<text>` per distinct style in the row (fg, bold, italic, underline). An `x` list gives each glyph's cell position, spaces are omitted, and there is no `textLength`.

   Prose rows pick the run form, and sparse colored rows (rain, gauges, meters) pick the glyph-list form. No row grows compared with today.
3. **Drop default attributes.**
   - Remove `font-weight="normal"` and `lengthAdjust="spacing"`, which are the SVG defaults.
   - Replace the per-rect `class="bg"` with one CSS selector that sets `shape-rendering: crispEdges` on row-layer rects.
4. **One CSS rule per keyframe instead of per layer** (optional; measure first).
   - Use `<g class="layer kN">` with one `.kN { animation: … }` rule, instead of `.layer-i` rules.
   - Matrix would go from 20,058 rules to at most 719, saving about 1MB, roughly 4% of the compact output.
   - Tests parse `layer-(\d+)`, so their helpers need updating.

### Constraints

- **Cells whose text is more than one UTF-16 code unit never join an `x` list.** That covers surrogate pairs, combining marks, variation selectors, and ZWJ sequences. SVG assigns `x` values per addressable character, and browsers do not agree on how to count those. Emit such cells as their own `<text>`.
- **Block elements (U+2580–U+259F)** keep today's per-glyph `textLength` + `lengthAdjust="spacingAndGlyphs"` encoding.
- **Color resolution is unchanged:** reverse, bold and faint intensity, and the matrix-rain tint. If B1 has landed, use the shared helper.
- **XML escaping and `white-space: pre`** are unchanged.
- **Layer timing, keyframes, cursor, viewport, and chrome markup** are unchanged, apart from item 4 if adopted.

### Tests

- **Text decoder helper.** Add a test helper that rebuilds each layer's visible row text from either form, mapping positions back to columns. Text assertions in `tests/svg_render_test.cs` (curl output, matrix tint, copilot banner) use it instead of raw `Contains` on markup, so tests do not depend on which form a row picks.
- **New cases:**
  - Glyph-list form is chosen for a sparse colored row.
  - Run form is chosen for prose.
  - Multi-code-unit cells never appear in an `x` list.
  - Background spans merge across foreground changes.
  - Over all fixtures, no row is longer than its run form.
- **Size table.** Render every sample and fixture, print a before/after table, and fail if any file grows by more than 1% compared with A1 output.

### Visual Verification

Run this manually before merging, for every sample, every fixture, and the matrix casts.

- Screenshot old and new SVG at several fixed times in headless Chromium or Edge.
- Pin the time with a wrapper page that sets a negative `animation-delay` and `animation-play-state: paused`.
- Compare the pixels. Identical output is expected. Sub-pixel anti-aliasing differences are acceptable only where italic glyphs overhang into the next cell.

### Acceptance

- matrix 200×60 at 60fps (`svg` subcommand): at most 30MB, down from 117MB.
- `samples/pty.cast`: at most 0.7MB, down from 2.37MB.
- No sample or fixture grows by more than 1% compared with A1.
- Visual verification passes.
- `samples/*.svg` are regenerated in the same PR (`dotnet run samples/regenerate.cs`).

### A2 Results

Built as proposed, including item 4: row layers that share a keyframe share one rule, `.layer-1,.layer-5 { animation:… }`. Each row layer is also written on one line. Separately, a crash on cells holding combining marks (present in 1.4.x) was found by a new test and fixed.

Sizes from the `svg` subcommand:

| Cast | A1 | A2 | Change |
|---|---|---|---|
| `samples/basic.cast` | 129.8KB | 107.9KB | −16.9% |
| `samples/demo.cast` | 131.4KB | 110.7KB | −15.7% |
| `samples/git.cast` | 89.7KB | 68.3KB | −23.8% |
| `samples/highlight.cast` | 172.5KB | 141.3KB | −18.1% |
| `samples/interactive-pty.cast` | 1.31MB | 0.96MB | −26.5% |
| `samples/pty.cast` | 2.39MB | 0.61MB | −74.4% |
| `samples/scenario_format.cast` | 432.9KB | 361.8KB | −16.4% |
| `samples/theme*.cast`, `stream-output`, `quickstart`, fixtures | — | — | −3.5% to −15.5% |
| matrix 200×60 | 117.1MB | 26.9MB | −77.0% |
| matrix 200×60, `--max-fps 20` | 25.6MB | 5.9MB | −76.9% |
| matrix 384×108 | 454.9MB | 96.7MB | −78.7% |

- Every sample and fixture shrank, with and without `--max-fps 20`. The acceptance targets (matrix at most 30MB, pty at most 0.7MB, no growth) are met.
- The `<style>` block of the 200×60 matrix SVG went from 1.19MB to 0.36MB.
- Peak memory for 200×60 went from 234MB to 143MB, because there is less to buffer.

#### Visual Verification Results

**Setup.**

- **Casts:** 19 SVGs, from 16 samples and fixtures plus `--max-fps 20` renders of `pty`, `interactive-pty`, and matrix 200×60.
- **Times:** 5 times each, at 15/35/55/75/95% of the loop.
- **Rendering:** headless Edge, with the time pinned by page CSS (`animation-delay: -Ts; animation-play-state: paused`) and a scratch profile.
- **Retries:** headless Edge sometimes captures a frame before painting finishes. Any nonzero comparison was retaken up to twice and the smallest count kept.

| Comparison with A1 | Differing pixels |
|---|---|
| A2 forced to the run form for every row | 0 in 91 of 95 shots; 5–7px in 4 shots of the matrix-style casts (`pty`, matrix 200×60), from merged background rects |
| A2 as shipped (shorter form per row) | 0 in 66 of 95 shots. The rest were 31–6,374px (at most 1.5% of the frame, in interactive-pty), only where glyph-list rows are on screen. |

**Cause.** The run form lets the browser spread glyphs with `textLength`, which puts them up to "cell width − font advance" right of their cell (about 0.3px with Cascadia Mono at 16px). The glyph-list form places each glyph exactly at its cell. Zoomed side-by-side crops of the largest differences (interactive-pty, basic) show no visible change. This replaces the original expectation of identical output.

## A3. Size Warning

Status: **Implemented** (2026-10-29).

What was built:

- **Message.** The final text is in [spec_cli.md](../spec_cli.md) → Logging. It names the size and suggests mp4 and `--max-fps`:
  - the `svg` subcommand suggests `scenetake mp4 <cast>`; the scenario path suggests `--format mp4`;
  - when `--max-fps` is already set, it suggests a lower value instead of 20.
- **Threshold.** Over 10,000,000 bytes.
- **Sizes.** Logs and warnings now use decimal MB. The mp4 `Written:` line used to divide by 1024², so docs that quoted it understated mp4 sizes by about 5%. Those numbers were corrected (for example matrix 200×60: 9.4MB → 9.8MB).
- **When it fires.** After A2, the 200×60 matrix SVG (26.9MB) warns; `--max-fps 20` (5.9MB) and every repository sample stay below the threshold.

Original proposal:

After writing an SVG larger than 10MB (the GitHub image attachment limit), print:

```
Warning: SVG is 28.1MB; full-screen output is much smaller as mp4 (--format mp4) or with --max-fps 20
```

- Until B lands, the message mentions only `--max-fps`.
- This is a warning only: the file is kept and the exit code does not change.

## A4. Compact Animation CSS and Row Reuse

Status: **Implemented** (2026-10-29). This was added after A1–A3.

After A2, measuring the samples showed where typing and TUI SVGs still spent bytes:

- **CSS was about 40%.** Each layer and each cursor move had its own keyframes, written as 6-decimal ranges with a sub-millisecond fade, plus its own `animation:` rule.
- **Scrolled rows were 7–36%.** Output that scrolls was rewritten row by row.

What changed:

1. **Keyframes.**
   - Step-end keyframes with only the switch points, for example `@keyframes k3{4.969%{opacity:1}5.245%{opacity:0}}`.
   - Offsets use just enough decimals to resolve 1ms of the loop.
   - Loop length, timing, and repetition are set once on `.l`.
2. **Classes instead of per-layer rules.**
   - Each keyframe has one class rule (`.k3{animation-name:k3}`), and elements carry `class="l k3"`.
   - Row wrappers are written at output time, once keyframe names are known.
3. **Cursor.** The cursor is a single `<rect class="l kN c">` without a wrapper group.
4. **Row reuse.** A row whose content already appeared in an earlier layer, on another line, becomes `<use href="#rN" y="dy">`.
   - Candidates are found by hashing the row's cells, then confirmed by re-encoding the row at the earlier line and comparing bytes.
   - Rows shorter than a `<use>` are written out.

| Cast | After A2 | After A4 |
|---|---|---|
| basic (typing) | 107.9KB | 56.0KB (−48%) |
| git (typing) | 68.3KB | 36.4KB (−47%) |
| scenario_format (typing) | 361.8KB | 182.7KB (−50%) |
| interactive-pty (TUI) | 961.6KB | 525.9KB (−45%) |
| pty (small matrix) | 611.8KB | 564.0KB (−8%) |
| matrix 200×60 | 26.9MB | 26.4MB (−2%) |
| matrix 384×108 | 96.7MB | 95.7MB (−1%) |

Every sample and fixture shrank, between −21% and −50% apart from the matrix casts. The 13 committed sample SVGs went from 2.61MB to 1.64MB in total.

Visual check: same method as A2, comparing A2 with A4 on 19 SVGs at 5 times each.

- 90 of 95 shots were pixel-identical.
- 3 differed by at most 1–3 levels of 255 (anti-aliasing rounding).
- 2 fell exactly on a layer switch in the short fixtures (0.4s and 0.8s loops). There, A2's 1ms fade shows both the old and new layer, while A4 switches at that instant.
- Rows reused with `<use>` rendered identically.

Tests: `tests/svg_render_test.cs` decodes the new markup, including `<use>`, and adds checks for compact keyframes, row reuse, and the cursor rect. `EchoOutputNoDuplicateCommandLine` used to pass with zero matches: its time no longer fell where the line is on screen. It now requires exactly one visible copy at 8.5s.

## A5. Further Size Reduction (Investigation)

Status: **S0–S6 implemented** (2026-10-29). See [What Was Built](#what-was-built-s0s6). X1–X3 and S2c were not built. The rest of this section is the investigation.

### Where the Bytes Go After A4

| Part | basic (typing) | interactive-pty (TUI) | matrix 200×60 |
|---|---|---|---|
| Keyframes and their class rules | 32% | 9% | 0.2% |
| Cursor rects | 22% | 1.5% | 0% |
| `<use>` for reused rows | 12% | 3% | 0% |
| `fill` on `<text>` | 4% | 13% | 27% |
| `x` / `y` on `<text>` | 5% | 19% | 41% |
| `textLength` + `lengthAdjust` on block elements (█ ▀ ▄ …) | — | 32% | 0% |
| `<text>` tags | 4% | 11% | 23% |
| Glyphs themselves | 7% | 5% | 2.5% |

### Method

- **Prototypes.** Each candidate was prototyped by rewriting A4 output. The baseline is A4 output with LF line endings.
- **Visual check.** Every candidate was compared in headless Edge 156 at 5 pinned times per sample. The SVG was shown through `<img>`, as GitHub does.
- **Playback cost.** Measured in the same browser over the DevTools protocol, with each SVG playing for 6s through `<img>`:
  - requestAnimationFrame (rAF) intervals;
  - main-thread busy time.
- **Not checked.** Firefox and Safari were not checked.

### Candidates

Size changes are against the A4 output with LF line endings.

| ID | Change | Typing | interactive-pty | pty / matrix 200×60 | Visual (Chromium) | Playback cost |
|---|---|---|---|---|---|---|
| S0 | Always write `\n`, not `Environment.NewLine` | −1.7% on Windows | −0.4% | −0.2% / −0.1% | Same | None |
| S1 | Block elements as `<rect>`, adjacent same-color cells merged | 0% | **−50.5%** | 0% | Intended change, see [S1](#s1-block-elements-as-rects) | Slightly lower |
| S2 | Row-local coordinates: one CSS `transform` class per row, `<text>` without `y` | −2% to +1% (stream-output +4%) | −8.2% | −15.2% / **−17.5%** | Same, except underline anti-aliasing (1–2 of 255 levels) | None |
| S2c | S2 plus cell units (`scale(cellWidth)`, integer `x`) | −3% to +1% (stream-output +6%) | −14.8% | −22.9% / **−25.5%** | Anti-aliasing differs on some glyph edges (for example `<`, up to 47 of 255 levels); not visible | matrix 200×60 about 1.5× slower per frame |
| S3 | Most common text color inherited from the group; other colors as classes | −2.5 to −4.7% | −7.4% | −7.8% / −8.6% | Same | None |
| S4 | Cursor inside the row layer with the same timing, as `<use href="#c" x y/>` | **−6.5 to −10.4%** | −0.6% | −0.7% | Same | None |
| S5 | A layer with one element carries the classes itself, with no `<g>` | −0.2 to −2.3% | −0.1% | 0% | Same | None |
| S6 | Shorter keyframe names (1–2 letters instead of `k123`) | about −4% (estimate) | −1.1% | −0.6% | Same | None |
| X1 | One cursor element moved by keyframed `translate` | −14 to −24% | −1.4% | −1.5% / 0% | Same | pty main thread +50–70%, frame rate held |
| X2 | Shared timeline: one animated custom property `--f`, each layer's show/hide as numbers | −13 to −20% | −4.6% | −2.1% / −0.1% | Same | Frame drops on pty and interactive-pty |
| X3 | Per-cell lifetimes for matrix-like output | — | — | at most −14% | Not built | — |

**Combined.** S0–S5 together, against today's output written on Windows. S4 was measured on its own and added; the rest were measured as one rewrite.

| Sample | Combined change |
|---|---|
| Typing samples | −14% to −21% (stream-output −8%, because S2 makes it larger) |
| interactive-pty | −55% |
| pty | −24% (−32% with S2c) |
| matrix 200×60 | −26% (−34% with S2c) |

S6 would add about 4 points on typing. Playback cost was the same as today for basic, pty, and interactive-pty.

### Recommendation

1. **Do first: S0, S1, S3, S4, S5, S6.** Each is local to the encoder and does not add playback cost.
2. **Then S2.** Choose per layer between row-local and absolute coordinates, whichever is shorter, so that sparse rows such as stream-output do not grow. With row-local content, row reuse no longer needs re-encoding at another line.
3. **Hold S2c.** It slows heavy matrix SVGs. Its 1.6px font scaled ×9.92 also needs checks in Firefox and Safari.
4. **Do not adopt X1 or X2 as they are.**
   - Both are large wins for typing. Both add playback cost on busy recordings, and X2 drops frames there.
   - X2 with the animation on the root `svg` was cheaper than today for basic (81 vs 105 ms/s of main thread). It still dropped frames on pty (p95 50ms).
   - Adopting either one would need a rule that limits it to light recordings.
5. **Do not pursue X3.** Only 14% of matrix 200×60 glyphs (5% for pty) carry over unchanged, glyph and color, into the next layer of their row. Per-cell timing would cost more than it saves.

### S1. Block Elements as Rects

- **What it changes.** Font block glyphs are stretched horizontally with `textLength` to fill the cell width. Vertically they keep the font's own height. This leaves visible seams between cells and rows, for example in the Copilot CLI input box. Exact rects remove the seams.
- **Consistency with mp4.** The mp4 rasterizer already draws block elements this way. It draws shades (░ ▒ ▓) as 25%, 50%, and 75% alpha, which an SVG rect can do with `fill-opacity`.
- **Saving.** Half of interactive-pty's size, because one merged rect replaces a run of about 100-byte `<text>` elements.

### Lessons Learned

- **Fill precedence.** A CSS rule such as `text{fill:…}` overrides every `fill="…"` presentation attribute. A default color must be inherited from an ancestor attribute or style, not set by a rule on `text`.
- **`<` in `<style>`.** A literal `<` in the SVG `<style>`, such as `@property` `syntax: "<integer>"`, is an XML error. Inline in HTML it silently drops the stylesheet. Escape it as `&lt;`.
- **Inline SVG is not a reliable baseline.**
  - Inline in an HTML page, the X2 + X1 combination changed text anti-aliasing (up to about 90 of 255 levels). Through `<img>` it was pixel-identical, presumably because inline SVG gets separate compositing.
  - Visual checks must use `<img>`.
- **Minimum font size.** Chromium's minimum font size setting enlarges HTML text but not SVG text inside `<img>`, including text drawn at 1.6px and scaled up.
- **Cost of custom-property animation.** Animating a custom property costs far more main-thread time than animating `opacity`. X2 with the animation on every layer tripled main-thread time on typing (basic: about 110 → 335 ms/s).
- **Cost of a moving cursor.** Moving one element by keyframes (X1) added main-thread time on busy recordings in every form tried: `translate`, `transform`, and CSS `x`/`y`. Many cursor elements that only switch `opacity` cost nothing extra.
- **matrix 200×60 is past what a browser can play.** In headless Edge it plays at 1–2 frames per second today, with or without GPU. Size work on it matters for the file limit, not for playback.

### What Was Built (S0–S6)

All of S0–S6 went in, with S2 chosen per layer.

- **S2 (row-local).** Every changed row is encoded both ways and the shorter is kept.
  - A row gets its class only once its layers' would-be savings pay for the CSS rule.
  - Charging the rule to each layer on its own kept typing rows absolute, because one layer saves about 10 bytes.
- **S3 (colors).** The default color is the theme's foreground, known before encoding starts. Every other color becomes a class on first use; encoding streams, so how often a color recurs is not known in advance.
- **S4 (cursor).** The cursor joins a row layer only on its own row, so that layer's content cannot cover it. Positions without such a layer keep their own keyframe.
- **S5 (no wrapper).** Only single-`<text>` layers lose their `<g>`. A rect given a class would lose the crisp-edge rule (`rect:not([class])`).
- **S6 (short names).** Rows, colors, and keyframes share one pool of 1–2 character names. Keyframes are named last, most used first.
- **S1 (block rects).** Shades (░ ▒ ▓) became translucent rects too, matching mp4.

| Output | A4 (LF) | S0–S6 |
|---|---|---|
| basic (typing) | 55.0KB | 45.7KB (−16.9%) |
| git (typing) | 35.9KB | 31.1KB (−13.4%) |
| scenario_format (typing) | 179.6KB | 146.6KB (−18.3%) |
| theme (typing, light) | 17.0KB | 13.6KB (−19.9%) |
| interactive-pty (TUI) | 523.7KB | 232.3KB (−55.6%) |
| pty (small matrix) | 562.6KB | 432.0KB (−23.2%) |
| matrix 200×60 | 26.4MB | 19.7MB (−25.3%) |
| matrix 200×60, `--max-fps 20` | 5.8MB | 4.3MB (−25.2%) |
| matrix 384×108 | 95.7MB | 70.7MB (−26.1%) |

- **Samples.** The 13 committed sample SVGs went from 1.63MB to 1.11MB. Every fixture shrank too.
- **Render time.** matrix 200×60 went from 3.0s to 4.0s, and 384×108 from 25s to 26s.

**Visual check.** Each of the 16 samples and fixtures was compared with A4 through `<img>` in headless Edge, at 5 times each:

| Result | Shots | Cause |
|---|---|---|
| Pixel-identical | 68 of 80 | — |
| At most 2 of 255 levels | 8 | Underline anti-aliasing and single pixels |
| Intended change | 3 | interactive-pty block elements |
| Glyphs moved by under a pixel | 1 | An interactive-pty row switched from glyph-list to run form, because shorter color markup changed which form is shorter |

**Playback cost.** Measured the same way as the candidates:

- basic, pty, and interactive-pty held 60fps, with the same main-thread time as A4.
- matrix 200×60 stayed within run-to-run noise. It plays at 1–1.5 frames per second either way.

## Docs to Update When Done

- **[spec_svg.md](../spec_svg.md):**
  - the hybrid encoding and the memory model;
  - Lessons Learned: about 113 bytes per `<text>`, a background rect per run when apps paint their own background, keyframe deduplication depending on loop-duration precision, and the code-unit pitfall for `x` lists.
- **[spec_cli.md](../spec_cli.md):** the size warning.
- **README.md and README-ja.md:** what to do when an SVG gets large (`--max-fps`, or mp4 once B lands).

## Open Questions

1. Is 10MB the right warning threshold?
2. ~~Should the scenario path and the `svg` subcommand share one loop-duration precision?~~ Yes. Both now render the written cast (see [Observation to Resolve Separately](#observation-to-resolve-separately)).
