# PNG Output Specification

Status: **Implemented**

## Motivation

Some places take no SVG: Zenn uploads take PNG, JPEG, GIF, or WebP, and slides, issue trackers, and chat paste a picture. Often one screen is enough, such as the result of a command. A PNG keeps text sharp, as JPEG does not.

## Scope

- `--format png` on the scenario path writes the cast and `<stem>.png`. The `png` subcommand draws a PNG from an existing cast ([spec_cli.md](spec_cli.md)).
- One screen per PNG. `--at TIME` picks it, and the final screen is the default.
- Not in scope: an animated PNG, and scaling (`--size` and `--max-size` are mp4 options). A larger `--font-size` makes a larger PNG, such as 32 for twice the size.

## What the PNG Shows

- **The screen.** The screen the animated SVG shows at `TIME`, from the same timeline: the last frame shown by then, after the trailing blank alternate-screen restore is trimmed. A time past the end gives the final screen, and a time before the first output an empty terminal without a cursor. A still SVG with the same `--at` shows the same cells ([spec_svg.md](spec_svg.md) → Still SVG).
- **The pixels.** The PNG is drawn by the mp4's rasterizer, so it is the mp4 frame shown at the same time, pixel for pixel, at the size an mp4 has without `--size`: fonts from `font-family` or `--font-file`, soft font glyphs, the drawn block and box characters, the window chrome, the background, opacity, and blur ([spec_mp4.md](spec_mp4.md)). Like that frame, a PNG without a background has no margin, shadow, or rounded corners, and its sides are even.
- **Fonts.** An SVG uses the viewer's fonts. A PNG uses the fonts installed where scenetake runs, and the log names the one it used.
- **ffmpeg.** Only a background image needs it, to decode the `.png`, `.jpg`, `.gif`, or `.webp` file, as for mp4. Any ffmpeg does: libx264 is not needed. Without one the run stops before anything is recorded and says that a color or gradient needs none.

## Encoding

scenetake writes the PNG itself: the signature, `IHDR`, `PLTE` for a palette, one `IDAT`, and `IEND`, each with its CRC-32. It writes no other chunks, so no time or version is stored, and the same pixels give the same file on the same .NET runtime.

| Image | How it is stored |
|---|---|
| 256 colors or fewer | A palette in the order colors first appear, at 1, 2, 4, or 8 bits per pixel for up to 2, 4, 16, or 256 colors. Rows have no filter, as the PNG specification advises for palette images. |
| More colors | 8-bit RGB. Each row takes the filter (None, Sub, Up, Average, or Paeth) whose output has the smallest sum of absolute values. |

zlib comes from .NET (`ZLibStream`, smallest size).

Sizes measured with the samples:

| PNG | Pixels | Colors | Size |
|---|---|---|---|
| `basic`, final screen | 820×488 | over 256 | 61.5KB |
| `basic`, `--font-size 32` | 1632×976 | over 256 | 128.9KB |
| macOS window on a gradient (`basic`) | 884×586 | over 256 | 75.5KB |
| `pty` at 8.5s (matrix rain) | 620×288 | over 256 | 40.1KB |
| `sl` at 5s | 820×362 | 244 | 5.6KB |
| `nyancat` at 4s | 980×528 | 15 | 1.5KB |
| an empty terminal | 820×488 | 1 | 144 bytes |

## Size Limit and Errors

- A frame is limited as an mp4 frame is, (width + 128) × (height + 128) below 2^28, which keeps the RGB frame within one array. The error names the PNG and does not mention ffmpeg.
- On the scenario path the font, the frame size, and the ffmpeg for a background image are checked before `pre`. A PNG render that fails after the recording deletes a PNG from an earlier run and keeps the cast.
- The PNG is written only after it is fully encoded, so a failure leaves no partial file.

## Binary Size

The NativeAOT binary for win-x64 grew by 147KB (4,406,272 to 4,553,216 bytes, 3.3%). zlib (`System.IO.Compression.Native`), which scenetake did not link before, is 114KB of it, and the PNG and still-SVG code 33KB.

## Alternatives Not Chosen

| Idea | Why not |
|---|---|
| PNG through ffmpeg | Every PNG would need ffmpeg, which npm and binary installs do not bring, and the bytes would change with the ffmpeg version. |
| Rasterizing the SVG with another renderer | Another renderer to ship and keep in step. The mp4 rasterizer already draws the same cells, and a PNG that matches the mp4 frame is easy to test. |
| A deflate encoder of scenetake's own | It would save most of zlib's 114KB. zlib compresses better and is maintained by .NET. Not tried yet. |
| The SVG's transparent margin, shadow, and rounded corners | The rasterizer draws opaque RGB frames for video. A PNG that is the mp4 frame keeps one look for raster output. With a background the window has its shadow and corners. |
| JPEG | It blurs text and adds noise around it. |

## Lessons Learned

- **Antialiased text gives most screens more than 256 colors.** Every sample with text came out as RGB. A palette pays off for screens of blocks and few glyphs: `nyancat` needs 15 colors and 1.5KB.

## Cross-Document Notes

- [spec_cli.md](spec_cli.md) — `--format png`, the `png` subcommand, `--at`, `--font-file`, and their errors.
- [spec_svg.md](spec_svg.md) → Still SVG — the same screen as an SVG.
- [spec_mp4.md](spec_mp4.md) — the rasterizer, fonts, window chrome, and background that the PNG shares.
