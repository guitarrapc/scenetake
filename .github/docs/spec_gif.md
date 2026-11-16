# GIF Output Specification

Status: **Implemented**

## Motivation

X, Reddit, and Zenn take GIFs, and Zenn takes no video. A GIF plays everywhere without a player. scenetake used to leave GIFs to agg, a second tool that most readers ran in Docker. Now the frames scenetake already draws for mp4 become a GIF in one command.

## Scope

- `--format gif` on the scenario path writes the cast and `<stem>.gif`. The `gif` subcommand converts an existing cast ([spec_cli.md](spec_cli.md)).
- ffmpeg makes the GIF. Any build does: palettegen, paletteuse, and the GIF encoder are in every ffmpeg, and libx264 is not needed.
- Not in scope: a GIF encoder of scenetake's own, and dithering.

## What the GIF Shows

- **The frames.** The mp4's frames ([spec_mp4.md](spec_mp4.md)): frame `k` shows the screen at `k / fps`, through the same loop as the SVG, with the end hold and the fade-out. So the GIF draws the fonts, the window chrome, the background, opacity, and blur as the mp4 does.
- **Frame rate.** `--fps` takes `1`–`50`, default `20`. A GIF stores each frame's delay in hundredths of a second, and browsers slow delays under 2 hundredths down to 10, so 50 is the highest rate a GIF plays at. At 20 every delay is 5 hundredths.
- **Size.** The terminal's own size, as an mp4 without options. `--max-size` scales down to fit, and `--size` pads to an exact size, as for mp4.
- **Loop.** The GIF loops (`NETSCAPE2.0` with no count) unless the scenario's `render.loop`, the cast's `st:loop`, or `--no-loop` says it plays once. Then the loop extension is left out, and, as an SVG that plays once, the GIF has no fade-out and stops on its final screen. A fade-out set anyway gives a warning.
- **Warning.** A GIF over 3MB, which Zenn does not take, gets a warning: a smaller `--font-size` makes it smaller and keeps the text sharp, a lower `--fps` helps a full-screen program, and X and Reddit take mp4.

## Encoding

The frames are drawn twice and piped to ffmpeg each time:

1. `palettegen=stats_mode=full`: one palette of up to 256 colors for the whole GIF, with one entry kept for transparency.
2. `paletteuse=dither=none:diff_mode=rectangle`: each frame mapped to that palette. ffmpeg stores only the rectangle that changed since the previous frame, and pixels that did not change as transparent.

- **Why two passes.** In one pass, ffmpeg's `split` holds every frame in memory until the palette is known: about 1.4GB for a one-minute 80×24 demo at 20fps. Drawing the frames again costs less than that. A sample takes 0.5 to 1.2 seconds in all.
- **Why one palette.** A terminal uses a few theme colors and the shades that antialiasing adds, so one palette for the whole GIF loses little, and the frames share it.
- **Why no dithering.** Dithering speckles flat backgrounds and the edges of text. A gradient background bands a little instead.
- The palette and the GIF are temporary files next to the output until ffmpeg succeeds, as for mp4. Ctrl+C removes them.

## Measurements

Sizes against the agg GIFs that `samples/` held before (agg 1.6 with its own font size, so the pixel sizes differ):

| Sample | scenetake `gif` | agg |
|---|---|---|
| `basic` | 820×488, 376.6KB | 787×560, 428.4KB |
| `highlight` | 820×488, 343.0KB | 787×560, 472.0KB |
| `pty` | 620×288, 673.5KB | 595×336, 759.7KB |
| `stream-output` | 820×488, 91.0KB | 787×560, 111.1KB |
| `theme-macos` | 884×586, 83.9KB | 787×560, 121.9KB |

The sample GIFs now come from `scenetake gif`: `samples/regenerate.cs` converts every cast, and `-- --gifs` converts them alone. The 14 that agg made went from 5.60MB to 4.69MB in all, and they gained the window chrome and the background that agg does not draw. `translucent.gif` is new: agg could not draw its blurred image. A heavy full-screen program can still come out larger than agg's GIF: an earlier test of `matrix` at 200×60 and 30fps through ffmpeg gave 27MB against agg's 19MB ([spec_mp4.md](spec_mp4.md) → Alternatives Not Chosen). Such recordings belong in mp4.

What makes a GIF smaller (`basic` unless noted):

| Option | Size |
|---|---|
| none (20fps, 820×488) | 376.6KB |
| `--fps 10` / `--fps 30` | 369.0KB / 383.0KB |
| `--font-size 12` (576×366) | 251.6KB |
| `--max-size 640x480` (640×380) | 450.6KB |
| `--max-size 410x244` | 227.3KB |
| `pty`: `--fps 10` / `--font-size 12` | 510.2KB / 462.8KB, from 673.5KB |

## Binary Size

The GIF output adds 25KB to the NativeAOT binary for win-x64 (4,553,216 to 4,578,304 bytes): it reuses the mp4's frames and ffmpeg process, and ffmpeg does the encoding.

## Lessons Learned

- **Scaling a GIF down can make it larger.** Scaled text gets blended colors that compress worse: `basic` at 640×380 was 450.6KB against 376.6KB at its own size, and its text was blurred. A smaller `--font-size` draws smaller text sharply and made the smallest GIF that still reads well, so the warning and the help suggest it, not `--max-size`.
- **The frame rate matters little for typing.** A typing demo changes a few cells between keystrokes, and an unchanged frame costs a few bytes, so 10 to 30 fps changed `basic` by under 4%. A full-screen program changes the screen every frame, so there it matters: `pty` was 24% smaller at 10fps.
- **A GIF's delays are hundredths of a second.** 30fps alternates delays of 3 and 4. 20fps gives an even 5.

## Alternatives Not Chosen

| Idea | Why not |
|---|---|
| A GIF encoder of scenetake's own | A palette quantizer and LZW are several hundred lines to keep. ffmpeg is already needed for mp4, and its palette and rectangle diff already make GIFs smaller than agg's. |
| Dropping unchanged frames (`mpdecimate`) for variable delays | Unchanged frames cost a few bytes each. Dropping the last ones would also drop the end hold, because a GIF's last delay comes from its last frame. |
| One pass with `split` | It holds every frame in memory ([Encoding](#encoding)). |
| Dithering (`sierra2_4a`, ffmpeg's default) | It speckles text edges and flat backgrounds. |

## Cross-Document Notes

- [spec_cli.md](spec_cli.md) — `--format gif`, the `gif` subcommand, `--fps`, `--max-size`, `--size`, `--font-file`, `--loop`, and their errors.
- [spec_mp4.md](spec_mp4.md) — the frames, fonts, window chrome, background, and ffmpeg lookup that the GIF shares.
- [spec_svg.md](spec_svg.md) — the loop, end hold, and fade-out that the GIF follows.
