# Output Size Plan — Overview

Status: **Proposed** (2026-10-29)

Detailed plans:

- [plan_svg_size.md](plan_svg_size.md) — **A.** SVG memory and size
- [plan_mp4.md](plan_mp4.md) — **B.** mp4 output via `ffmpeg`

## Problem

Heavy TUI recordings make SVG and GIF output too large to share. "Heavy" here means apps that redraw most of the screen every frame, such as `matrix`, `cmatrix`, `btop`, and banner animations.

- A cast recorded to share on X fails on file size in both SVG and GIF.
- At 4K terminal sizes, SVG rendering does not finish: it used more than 43GB of RAM before it was stopped.
- Capturing the screen to mp4 with a screen recorder is cheaper than either format.

Typing-centric demos are fine today and must stay fine.

## Measurements

The numbers below were measured on 2026-10-29 with scenetake 1.4.2 on Windows 11. Encoders ran in Docker with an 8GB VM.

- **Recording.** The scenario ran `matrix --duration 5 --fps 60` with `pty: true` under pwsh.
- **GIF.** Made by agg 1.9.0.
- **Video.** Encoded by ffmpeg from raw frames. The frames came from a prototype renderer built on scenetake's VT emulator, with StbTrueTypeSharp drawing the glyphs.
- **CRF.** CRF values are not comparable across codecs.

### matrix 200×60, 60fps, 7.5s (frame 2016×1216)

| Output | Size | Notes |
|---|---|---|
| cast | 15.6MB | |
| SVG, today | 117MB (scenario path: 118MB) | 6.8MB gzipped; peak RAM 4.3–4.8GB before A1, 234MB after |
| SVG, `--max-fps 20` | 25.6MB | |
| SVG, compact prototype | 28MB | 3.4MB gzipped |
| SVG, compact prototype + `--max-fps 20` | 6.2MB | |
| GIF, agg (30fps cap) | 19MB | |
| GIF, ffmpeg palettegen, 30fps | 27MB | |
| H.264 crf 23, medium | 12.5MB | |
| H.264 crf 28, slow | 7.0MB | |
| H.264 crf 23, 30fps | 8.8MB | |
| H.264 crf 23, yuv444 | 13.7MB | |
| HEVC crf 28 | 9.0MB | |
| AV1 (SVT) crf 35 | 14.2MB | |
| VP9 crf 35 | 20.2MB | |
| Animated AVIF | 14.1MB | |
| Animated WebP q75, 30fps | 16MB | |
| H.264 crf 23, scaled to 1280×772, 60fps / 30fps | 6.2MB / 4.6MB | Fits the X API size limit |

### matrix 384×108, 60fps, 7.66s, 460 frames (frame 3840×2160)

| Output | Size |
|---|---|
| cast | 53MB |
| SVG | Before A1: aborted at more than 43GB RAM. After A1: 455MB, with a 673MB peak. |
| GIF, ffmpeg | Out of memory in the 8GB VM |
| H.264 crf 23 / crf 28 slow / crf 23 30fps | 43MB / 23.5MB / 29MB |
| HEVC crf 28 / AV1 crf 35 | 30.3MB / 49.3MB |
| H.264 crf 23, 1920×1080, 60fps | 13.8MB |
| H.264 crf 23, 1280×720, 30fps | 3.9MB |

The prototype renderer produced all 460 4K frames in 4.2s with a peak of 367MB.

### Repository samples

| Sample | SVG | GIF (agg) | mp4 (H.264 crf 23, 30fps) | Compact SVG prototype |
|---|---|---|---|---|
| basic (typing) | **129KB** | 414KB | 216KB | 153KB (+19%) |
| demo | **131KB** | 322KB | 237KB | — |
| pty (small matrix) | 2.37MB | 1.15MB | **511KB** | 656KB |
| interactive-pty | 1.30MB | 1.43MB | **659KB** | 1.06MB |

## Why Output Grows

1. **SVG is a lossless, symbolic format.** Its size is proportional to changed cells × frames. Matrix changes almost every cell every frame, which is about 750,000 glyph draws in 5 seconds at 200×60.
2. **Today's row encoding is wasteful for sparse colored output.**
   - Each `<text>` costs about 113 bytes and carries 1.26 characters on average.
   - Matrix paints its own black background (`#000000`), which differs from the theme background (`#282c34`). That adds a `<rect>` per text run.
   - In the matrix SVG, `<text>` is 57% of the bytes and `<rect>` is 40%.
3. **Memory grows with events × screen size.**
   - `TerminalReplay.BuildFrames` clones the whole `ScreenBuffer`, both the main and alternate grids, on every event that changes the screen.
   - Row layers keep those clones alive until the SVG string is complete.
   - The result is 4.3GB at 200×60 and more than 43GB at 384×108.
4. **GIF has hard limits.**
   - It has a 256-color palette and lossless LZW compression.
   - Frame delays have 10ms granularity, and browsers slow down very short delays, so 60fps cannot be represented.
   - X caps GIFs at 15MB and 350 frames.
5. **Video wins through lossy perceptual coding and inter-frame prediction.** Matrix stays expensive even for video: 4K at 60fps runs about 24–45Mbps, which is what a screen recorder would also produce.

## Decision

Two tracks.

- **A. Keep SVG as the README format.** Typing demos are smallest as SVG.
  - Fix memory first, then shrink the encoding.
  - This does not solve heavy TUIs. Compact 4K60 matrix is still estimated at around 100MB, and X does not accept SVG.
- **B. Add mp4 output for heavy TUIs and sharing.**
  - scenetake renders frames with its own VT emulator and pipes raw RGB to an external `ffmpeg`.
  - `ffmpeg` is the only new tool, and nothing else in scenetake requires it.

Guidance once both tracks land:

- **SVG:** typing and short output.
- **mp4:** full-screen TUIs, long recordings, and sharing on X or Slack.

## Not Pursued

| Idea | Why not |
|---|---|
| Built-in video encoder (no `ffmpeg`) | No practical managed H.264 encoder exists. OS encoders (Media Foundation, VideoToolbox) are per-platform, and Linux has none. |
| HEVC, AV1, VP9, WebP, or AVIF as the default | No clear size win at tested settings. They are slower, and X requires H.264. They can be offered later through the same pipe. |
| ffmpeg GIF instead of agg | It was larger than agg in this test (27MB vs 19MB) and still has the GIF limits. |
| `.svgz` | It renders only when the server sends `Content-Encoding: gzip`. File-size limits and viewer memory apply to the decompressed SVG anyway. |
| Turning on `--max-fps` by default | Full timing matters for typing and bulk output (lesson in [spec_svg.md](../spec_svg.md)). The size warning in A3 suggests it instead. |

## Order and Dependencies

| Order | Work | Reason |
|---|---|---|
| 1 | A1. Streaming replay (memory) — **done 2026-10-29** | Small change with byte-identical output that removes the out-of-memory failure. It also gives B a replay stepper (`TerminalReplay.EnumerateCaptures`). |
| 2 | B0–B4. mp4 output — **done 2026-10-29** | Main fix for the reported use case. |
| 3 | A2. Compact row encoding — **done 2026-10-29** | Independent of B, and only changes SVG row output. |
| 4 | A3. Size warning — **done 2026-10-29** | Comes after B so the warning can recommend mp4. |
| 5 | B5. Window chrome in mp4 — **done 2026-10-29** | Not needed for v1. |
| 6 | A4. Compact animation CSS and row reuse — **done 2026-10-29** | Added after measuring that CSS and scrolled rows were most of a typing SVG. |
| 7 | A5. Further size reduction — **S0–S6 done 2026-10-29** | Measured candidates; the ones with no added playback cost went in: typing −13 to −20%, TUI −56%, matrix −25%. See [plan_svg_size.md](plan_svg_size.md#a5-further-size-reduction-investigation). |

Shared touch points:

- `Terminal.cs` (`TerminalReplay`): changed by A1, used by B1.
- `Svg.cs` color helpers: moved by B1. `Svg.cs` row encoding: changed by A2.
- Land B1 and A2 one after the other, not in parallel branches.

## Platform Limits (checked 2026-10-29)

| Destination | Limit |
|---|---|
| X video (API upload) | H.264 High profile, yuv420p, up to 60fps, up to 1280×1024 |
| X GIF | 15MB, up to 1280×1080, up to 350 frames, up to 300M total pixels |
| GitHub attachments | Video (mp4, mov, webm): 10MB on free plans, 100MB on paid plans. Images and GIF: 10MB. |

Sources:

- [X API: Media best practices](https://docs.x.com/x-api/media/quickstart/best-practices.md)
- [GitHub Docs: Attaching files](https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/attaching-files)
