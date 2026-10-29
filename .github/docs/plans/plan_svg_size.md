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

## A2. Compact Row Encoding

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

## A3. Size Warning

After writing an SVG larger than 10MB (the GitHub image attachment limit), print:

```
Warning: SVG is 28.1MB; full-screen output is much smaller as mp4 (--format mp4) or with --max-fps 20
```

- Until B lands, the message mentions only `--max-fps`.
- This is a warning only: the file is kept and the exit code does not change.

## Docs to Update When Done

- **[spec_svg.md](../spec_svg.md):**
  - the hybrid encoding and the memory model;
  - Lessons Learned: about 113 bytes per `<text>`, a background rect per run when apps paint their own background, keyframe deduplication depending on loop-duration precision, and the code-unit pitfall for `x` lists.
- **[spec_cli.md](../spec_cli.md):** the size warning.
- **README.md and README-ja.md:** what to do when an SVG gets large (`--max-fps`, or mp4 once B lands).

## Open Questions

1. Is 10MB the right warning threshold?
2. Should the scenario path and the `svg` subcommand share one loop-duration precision?
