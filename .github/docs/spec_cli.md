# CLI Specification

Status: **Implemented**

## Motivation

scenetake records scenarios, converts casts to SVG or mp4, and creates starter YAML files. This document defines the CLI behavior shared by feature specifications.

## Commands

| Command | Purpose |
|---|---|
| `scenetake <scenario.yaml> [output]` | Run a scenario. Write `.cast` (default), `.cast` + `.svg`, or `.cast` + `.mp4` |
| `scenetake <scenario.yaml>...` | Record several scenarios, or a wildcard such as `docs/*.yaml`, one after another. See Several scenarios below. |
| `scenetake svg <input.cast> [output]` | Convert an existing cast to SVG |
| `scenetake mp4 <input.cast> [output]` | Convert an existing cast to mp4 (needs ffmpeg. See [spec_mp4.md](spec_mp4.md)) |
| `scenetake png <input.cast> [output]` | Draw one screen of an existing cast as a PNG (see [spec_png.md](spec_png.md)) |
| `scenetake gif <input.cast> [output]` | Convert an existing cast to an animated GIF (needs ffmpeg. See [spec_gif.md](spec_gif.md)) |
| `scenetake capture [options] -- <command>` | Record one command without a scenario file. See Capture below. |
| `scenetake init [--shell bash\|pwsh\|powershell] [scenario.yaml]` | Create a commented starter scenario file |
| `scenetake --help` | Print every command and option, with its range and default |
| `scenetake --version` | Print version |

```bash
scenetake [--verbose] [--strict] [--format cast|svg|mp4|png|gif] [--save-keys FILE] [--save-outputs FILE] [--replay FILE] [--at TIME] [--font-size N] [--font-family FAMILIES] [--theme NAME] [--window none|macos|windows] [--max-fps N] [--fps N] [--crf N] [--max-size WxH] [--size WxH|W:H] [--font-file PATH] <scenario.yaml> [output]
scenetake [options] <scenario.yaml> <scenario.yaml>... | <folder/*.yaml>
scenetake svg [--at TIME] [--font-size N] [--font-family FAMILIES] [--theme NAME] [--window none|macos|windows] [--max-fps N] <input.cast> [output]
scenetake mp4 [--font-size N] [--font-family FAMILIES] [--font-file PATH] [--theme NAME] [--window none|macos|windows] [--fps N] [--crf N] [--max-size WxH] [--size WxH|W:H] <input.cast> [output]
scenetake png [--at TIME] [--font-size N] [--font-family FAMILIES] [--font-file PATH] [--theme NAME] [--window none|macos|windows] <input.cast> [output]
scenetake gif [--fps N] [--max-size WxH] [--size WxH|W:H] [--no-loop] [--font-size N] [--font-family FAMILIES] [--font-file PATH] [--theme NAME] [--window none|macos|windows] <input.cast> [output]
scenetake init [--shell bash|pwsh|powershell] [scenario.yaml]
```

`--help` (or `-h`) describes each option, its range, and its default on stdout, then exits `0`. `svg --help`, `mp4 --help`, `png --help`, `gif --help`, `capture --help`, and `init --help` list command-specific options. Scenario help explains overrides of YAML `render` settings. Conversion help explains overrides of cast metadata. After an error, stderr shows short usage text and a reference to the command's `--help`. Help and error messages use ASCII only.

## Parsing

- Options may appear in any order before positional arguments on the scenario, `svg`, `mp4`, `png`, and `gif` paths.
- Unknown `-` / `--` options are explicit errors (avoids typos becoming path-not-found errors).
- `--format`, `--at`, `--font-size`, `--font-family`, `--theme`, `--window`, `--max-fps`, `--fps`, `--crf`, `--max-size`, `--size`, and `--font-file` accept `--name=value` or `--name value`.
- Duplicate `--at`, `--font-size`, `--font-family`, `--theme`, `--window`, `--window-title`, `--background`, `--margin`, `--padding`, `--opacity`, `--blur`, `--max-fps`, `--fps`, `--crf`, `--max-size`, `--size`, or `--font-file` is an error. `--size WxH` with `--max-size` is an error too: `--size WxH` already sets the output size. `--size W:H` sets only the shape and takes `--max-size`. `--size` is one option in both forms, so `--size 16:9 --size 1920x1080` is a duplicate.
- A second `--format` is an error that says how to get more formats: `--format takes one format. List several under 'outputs:' in the scenario, or record once and convert the cast for each other format, such as: scenetake gif <file>.cast`. It used to replace the first without a word, so `--format svg --format gif` wrote no SVG. The command line takes one format because the format options differ (`--at` for a still, `--fps` and `--size` for a video) and would be ambiguous among several. The scenario's `outputs` list gives each format its own options (Outputs list below).
- Options for the other output format are errors, not ignored:
  - On the scenario path, `--fps`, `--max-size`, and `--size` without `--format mp4` or `gif` fail with `<option> requires --format mp4 or gif`. `--crf`, H.264's quality, needs `--format mp4`: `--crf requires --format mp4`, and with GIF output `--crf applies to mp4: a GIF has no H.264 quality`. `--font-file` without `--format mp4`, `png`, or `gif` fails with `--font-file requires --format mp4, png, or gif`. The `svg` subcommand reports them as unknown options.
  - With GIF output, `--fps` above 50 fails with `--fps for a GIF must be between 1 and 50: <N> (a GIF counts each frame's delay in hundredths of a second)`.
  - `--max-fps` with mp4 or GIF output fails with `--max-fps applies to SVG, use --fps for mp4` (or `for GIF`).
  - `--max-fps` with cast output (no `--format svg`) fails with `--max-fps requires --format svg`, and with PNG output with `--max-fps applies to an animated SVG: a PNG is one screen`.
  - `--at` needs SVG or PNG output: with cast output it fails with `--at requires --format svg or png`, and with mp4 or GIF with `--at applies to SVG and PNG: an mp4 plays the whole recording` (`a GIF plays` for GIF).
  - A still SVG has no animation. With `--at`, `--max-fps` fails on the scenario path, and the `svg` subcommand rejects `--max-fps`, `--loop`, `--no-loop`, `--end-hold`, `--fade-out`, and `--speed` with `<option> has no effect with --at: a still SVG does not play`. On the scenario path the loop, end, and speed options are stored in the cast, so they stay allowed.
  - The `png` subcommand rejects `--fps`, `--max-size`, and `--size` (`<option> applies to mp4 and GIF: a PNG is one screen at the terminal's own size`), `--crf` (`applies to mp4`), and `--loop`, `--no-loop`, `--end-hold`, `--fade-out`, and `--speed` (`<option> applies to animation: a PNG is one screen, which --at picks`).
  - The `gif` subcommand rejects `--crf`, `--max-fps`, and `--at` with the messages above.
  - A value that is neither seconds, `end`, nor an id fails with `invalid --at value: 2s (expected seconds from the start, such as 2.5, end, or a step's id)`. On the scenario path an id that no step has fails before anything runs: `--at nope: no step has this id. Give the step 'id: nope'.` On a cast, an id it does not have fails with `--at nope: no step in the cast has this id (it has title, compare)`, and a cast without ids with `--at nope: the cast has no step ids, which scenetake writes when a step has an id. Give the time in seconds, or record it again`.
  - A still's `Written:` line names its moment: `still, at 2.5s`, `still, final screen`, or `still, at compare (4.56s)`.
- For the options that take a value, such as `--fps`, `--crf`, `--max-size`, `--size`, `--font-file`, `--font-size`, `--font-family`, `--theme`, `--window`, and `--max-fps`, a next argument that starts with `--` is not taken as the value: `--font-file --fps 30` fails with `--font-file requires a value`. The look options read their values alone until 2026-11-26, so `--font-family --theme nord demo.yaml` took `--theme` as the font and `nord` as the scenario.
- Range errors use plain ASCII ("between 1 and 60"), so every console code page shows them.

## Options

| Option | Commands | Behavior |
|---|---|---|
| `--verbose` | scenario path only | Show successful `pre`/`post` labels and phase markers, and the exception type and stack when an error stops the recording. See [spec_pre_post.md](spec_pre_post.md). |
| `--strict` | scenario path only | For CI. Stop at the first recorded step that exits non-zero without `continue-on-error: true`. Skip later steps, preserve existing output files, and write no cast, SVG or mp4 (only the `--save-keys` file). Run `post`. Exit `1`. If a `post` command fails, return that command's exit code. The `svg` and `mp4` subcommands report it as an unknown option. See [spec_scenario.md](spec_scenario.md) → Step exit codes. |
| `--save-keys FILE` | scenario path only | Write the keys typed in the `interactive: true` steps to FILE as `keys` entries that replay them. Written even when the recording stops at a later step. A scenario without an interactive step is an error before `pre`. `capture` reports it as an unknown option. On Windows a password typed in a step is saved too. See [spec_pty.md](spec_pty.md) → Saving the keys of an interactive step. |
| `--save-outputs FILE` | scenario path only | Write the outputs file: what each step printed in this run, as JSON, from the same run as the cast and only when the cast is written. Secrets and OSC text are hidden as in the cast. `capture` reports it as an unknown option. See Replay below and [spec_playground.md](spec_playground.md) → Outputs file. |
| `--replay FILE` | scenario path only | Compose the cast from an outputs file instead of running the commands. See Replay below. With `--save-outputs` or `--save-keys` it is an error, since nothing runs. `capture` reports it as an unknown option. |
| `--rerun` | scenario path only | Run every step, also one with `rerun: false` whose output the earlier cast has ([spec_scenario.md](spec_scenario.md) → Recording again). With `--replay` it is an error, since nothing runs. `capture` reports it as an unknown option. |
| `--format cast\|svg\|mp4\|png\|gif` | scenario path only | Default `cast`. `svg` also writes `.svg`, `mp4` also writes `.mp4`, `png` also writes `.png`, and `gif` also writes `.gif`. See [spec_svg.md](spec_svg.md), [spec_mp4.md](spec_mp4.md), [spec_png.md](spec_png.md), and [spec_gif.md](spec_gif.md). |
| `--at TIME` | scenario (`--format svg` or `png`), `svg`, `png` | One screen: the one shown `TIME` seconds into the recording (decimals allowed, `0` or more), `end` for the final screen, or a step's `id` for the screen right after that step's output ([spec_scenario.md](spec_scenario.md) → Step ids). A time past the end gives the final screen, and a time before the first output an empty terminal. With SVG it makes a still SVG without animation ([spec_svg.md](spec_svg.md) → Still SVG). A PNG shows the final screen without it. Not stored in the cast. |
| `--font-size N` | scenario, `svg` | `1`–`128`. Scenario path: overrides `render.font-size` in the written cast header and SVG. `svg`: render-only override (CLI > cast header > default `16`). See [spec_cast.md](spec_cast.md). |
| `--font-family FAMILIES` | scenario, `svg` | CSS `font-family` string (`1`–`16` families, `256` characters max). Scenario path: overrides `render.font-family` in the written cast header and SVG. `svg`: render-only override (CLI > cast header > default stack). See [spec_cast.md](spec_cast.md) and [spec_svg.md](spec_svg.md). |
| `--theme NAME` | scenario, `svg`, `mp4` | A named color theme ([spec_scenario.md](spec_scenario.md) → Color themes). An unknown name is an error that lists the names. Scenario path: overrides `render.theme.preset`. YAML `fg` / `bg` / `palette` still merge. Cast header stores resolved hex. `svg`: render-only override (CLI > cast header > default `dark`). See [spec_cast.md](spec_cast.md). |
| `--window none\|macos\|windows` | scenario, `svg` | Scenario path: overrides `render.window` in the written cast header (and in the SVG when `--format svg`). `svg`: render-only override (CLI > cast header > default `none`). See [spec_cast.md](spec_cast.md) and [spec_svg.md](spec_svg.md). |
| `--window-title TEXT` | scenario, `svg`, `mp4` | Text in the window's title bar. `""` or `--window-title=` shows none. Scenario path: overrides `render.window-title` in the written cast header. `svg`, `mp4`: overrides `st:window-title`, then the cast's `title`. See [spec_svg.md](spec_svg.md) → Window title, background, margin, and padding. |
| `--background VALUE` | scenario, `svg`, `mp4`, `png`, `gif` | One color, 2–4 colors for a gradient (separated by spaces or commas), or a `.png`, `.jpg`, `.gif`, or `.webp` file relative to the current folder. Colors are stored in the cast header; an image is not, so an image with cast output (no `--format svg`, `mp4`, `png`, or `gif`) is an error. A PNG and a GIF decode an image with ffmpeg, as mp4 does. |
| `--margin N` | scenario, `svg`, `mp4` | `0`–`200` px around the terminal or window. Default 8, 12 with a window, 32 with a background. mp4 has a margin only with a background. |
| `--padding N` | scenario, `svg`, `mp4` | `0`–`100` px between the terminal's edge and its text. Default from the font size. |
| `--opacity N` | scenario, `svg`, `mp4` | `0`–`1`, how much of the terminal's default background and the title bar covers the background. Default `1`. Below 1 without a background, a warning, and the window stays opaque. See [spec_svg.md](spec_svg.md) → Translucent window: opacity and blur. |
| `--blur N` | scenario, `svg`, `mp4` | `0`–`50` px of Gaussian blur on the background seen through a translucent window. Default `0`. Without an opacity below 1, a warning, and no blur. |
| `--no-loop`, `--loop` | scenario, `svg`, `gif` | `--no-loop` plays the SVG or the GIF once and stops on its final screen. `--loop` loops, also when the scenario's `render.loop` or the cast's `st:loop` is false. One of them, once. `mp4` rejects both: `--no-loop applies to SVG: the player decides whether an mp4 plays again`. On the scenario path the value is stored in the cast with any `--format`. |
| `--end-hold N` | scenario, `svg`, `mp4` | `0`–`60` seconds the final screen stays before the SVG starts again or the mp4 ends. Decimals allowed. Default: as long as the pause before the last output, as agg's GIF with `--last-frame-duration 0` holds it. |
| `--fit none\|height\|width\|both` | scenario, `svg`, `mp4`, `png`, `gif` | Move the bottom edge (`height`), the right edge (`width`), or both in to the last row and column the recording shows, the cursor included. Default `height`: no empty rows below the content. `none` draws the whole terminal. The cast keeps the terminal's size. See [spec_svg.md](spec_svg.md) → Fit and crop. |
| `--crop-top N`, `--crop-bottom N`, `--crop-left N`, `--crop-right N` | scenario, `svg`, `mp4`, `png`, `gif` | `0`–`511` cells cut from that edge before `--fit`. Default `0`. A crop that leaves no row or no column is an error, before anything runs on the scenario path and before the render on a cast. |
| `--fade-out N` | scenario, `svg`, `mp4`, `gif` | `0`–`10` seconds over which the terminal's contents fade out after the end hold. Default `0`. With `--no-loop`, or a cast that does not loop, the SVG and the GIF have none, with a warning. An mp4 still fades. |
| `--speed N` | scenario, `svg`, `mp4`, `gif` | `0.25`–`4` times as fast as recorded, decimals allowed, and a trailing `x` (`1.5x`) is read too. Default `1`. On the scenario path it is stored in the cast as `st:speed`, and `--speed 1` on a subcommand plays a cast at its recorded speed. Each `Written:` line of an SVG, mp4, or GIF gives its length and the speed (`3.2s at 2x`), and the cast's line adds the length at the speed (`5.7s, 2.8s at 2x`). See [spec_svg.md](spec_svg.md#playback-speed) → Playback speed. |
| `--length N` | scenario, `svg`, `mp4`, `gif` | At most `1`–`600` seconds, decimals allowed, and a trailing `s` (`15s`) is read too. On the scenario path it is stored in the cast as `st:length`. With `--speed` it is an error, and either one on a subcommand replaces the other from the cast. With `--at` on the `svg` command it is an error, since a still does not play. A cast without step ranges plays as recorded with a warning, and so does a recording that cannot fit, at its shortest. Each `Written:` line of an SVG, mp4, or GIF gives its length (`15.0s, length 15s`). See [spec_svg.md](spec_svg.md#length) → Length. |
| `--max-fps N` | scenario (`--format svg` only), `svg` | Optional frame sampling cap for SVG animation. `0` (default) = off (full cast timing). `1`–`120` enables sampling. See [spec_svg.md](spec_svg.md). |
| `--fps N` | scenario (`--format mp4` or `gif`), `mp4`, `gif` | Constant frame rate. mp4: `1`–`60`, default `30`. GIF: `1`–`50`, default `20` ([spec_gif.md](spec_gif.md)). Without `--fps`, a speed above 1 raises the default by the same factor, rounded up, to at most 60 and 50: 60 and 40 at 2x ([spec_svg.md](spec_svg.md#playback-speed) → Playback speed). |
| `--crf N` | scenario (`--format mp4`), `mp4` | H.264 quality, `1`–`51`. Default `23`. Higher values make smaller files. `0` (lossless) is not accepted, because x264 then writes the High 4:4:4 Predictive profile, which X and many players reject. |
| `--max-size WxH` | scenario (`--format mp4` or `gif`), `mp4`, `gif` | Downscale to fit within `W×H` (each side `16`–`16384`), keeping the aspect ratio. Never upscales. `1280x1024` fits X video uploads. A scaled GIF blurs its text and can be larger ([spec_gif.md](spec_gif.md) → Lessons Learned). |
| `--size WxH` | scenario (`--format mp4` or `gif`), `mp4`, `gif` | Make the video exactly `W×H` (each side `16`–`16384` and even): pad with the theme background, and scale down a larger terminal. Never upscales. Render only: the terminal's columns and rows are unchanged. Not with `--max-size`. Without `--size`, an mp4 wider than 3:1 or taller than 1:3 is padded to that shape, and a GIF keeps its own. See [spec_mp4.md](spec_mp4.md) → Exact size and Shape for video sites. A value that is not `WxH` says `W:H` is the shape form, since `16x9` meant as a shape is out of range. |
| `--size W:H` | scenario (`--format mp4` or `gif`), `mp4`, `gif` | Pad the video to the shape `W:H` (each side a whole number from `1` to `16384`, such as `16:9`, `9:16`, `1:1`, or `1920:1080`) at the terminal's own size, centered as `--size WxH` pads. Takes `--max-size`, which then scales the padded frame. Unquoted in YAML (`size: 16:9`), it is text. See [spec_mp4.md](spec_mp4.md) → Shape. |
| `--font-file PATH` | scenario (`--format mp4`, `png`, or `gif`), `mp4`, `png`, `gif` | Primary font file (TTF, OTF, TTC) for mp4, PNG, and GIF, before the `font-family` lookup. See [spec_mp4.md](spec_mp4.md). |

`--format mp4` and the `mp4` subcommand also accept `--font-size`, `--font-family`, `--theme`, and `--window` with the same priority as SVG (CLI > cast header > default). `--window` draws the title bar and buttons without the SVG margin and shadow (see [spec_mp4.md](spec_mp4.md) → Window chrome). `--format png`, `--format gif`, and the `png` and `gif` subcommands accept them with the same priority, and the other style options too.

**Environment.** `SCENETAKE_FFMPEG` names the ffmpeg binary for mp4 and GIF output, and for decoding a PNG's background image. Otherwise `ffmpeg` on `PATH` is used. mp4 needs libx264, a GIF any build, and a PNG without a background image no ffmpeg.

`SCENETAKE_OUTPUTS` names a file for the scenario path to write the outputs file to, after the cast and from the same run: what each step printed, which the browser playground replays ([spec_playground.md](spec_playground.md) → Outputs File). A relative path is resolved against the current folder. A failed write is an error, as for the cast, and `post` runs. The variable is for maintainers: `samples/regenerate.cs` sets it, and `--help` does not list it.

`init` accepts `--shell bash|pwsh|powershell` (`--shell value` or `--shell=value`, once) and an optional output path positional. No other flags. Another shell value, a missing value, or a second `--shell` is an error that writes nothing.

## Output Paths

**Scenario path.** `[output]` sets a shared stem. Extensions differ (`.cast`, plus `.svg` with `--format svg`, `.mp4` with `--format mp4`, `.png` with `--format png`, or `.gif` with `--format gif`). With no `[output]`, stem matches the scenario path without extension. Passing `out.cast`, `out.svg`, `out.mp4`, `out.png`, or `out.gif` yields the same stem. An `outputs` entry writes `<stem>.<format>`, or its `file`, which is relative to the stem's folder (Outputs list below).

**`svg`, `mp4`, `png`, and `gif` subcommands.** With no `[output]`, writes alongside the input as `<stem>.svg`, `<stem>.mp4`, `<stem>.png`, or `<stem>.gif`. With `[output]`, uses that path's stem and directory (extension optional).

**Checks.** Before commands run, scenetake rejects output paths that are directories or refer to the input file. The scenario path checks these before `pre`. For example, `scenetake mp4 clip.mp4` would overwrite its input, and failed rendering deletes the output.

Path comparison ignores case because any OS can use a volume that ignores case. Additional checks depend on the platform:

| Platform | Same-file checks and limits |
|---|---|
| Windows | Compare volume serial numbers and file indices, even while another process holds a file open. This detects symbolic links, junctions, hard links, mapped drives, subst drives, and device or UNC paths (`\\?\`, `\??\`). |
| Windows volumes with serial number `0`, including WSL shares | Compare the final paths from opened files. Treat `wsl$` and `wsl.localhost` as the same server. Matching indices in one folder identify hard links when both names report multiple links. Hard links across folders are not detected. |
| Unix | Follow symbolic links, including links within targets. Apply `..` after preceding links. Hard links are not detected. |



The output folder must exist as well, for the cast, every rendered output, and the `--save-keys` and `--save-outputs` files. When the scenario has `pre` commands, the folder is checked after `pre`, which may create it. Every other check runs again then, because `pre` may also put a directory, or a link to the input, where an output goes. A failure then stops the run before the recording, and `post` runs because `pre` succeeded. Without `pre` commands the folder is checked before anything runs.

## Logging

Progress and status use stderr (`Loading:`, `Written:`, `Done:`). Paths use `/` separators and are relative to the current directory where possible. Paths on another Windows drive, for example, remain absolute. On Linux and macOS, a path that leaves the current directory is also tried with its symbolic links followed, and the shorter form is shown. The system gives the current directory with its links followed, so a file in it, named through a link, would otherwise read as a climb to the root and back. Scenario runs always show step `running:` lines, and `running (show: none):` for a step that is not recorded. A background step shows `starting (background):`, `ready after N s: "text"` when it has `wait`, and `stopped (background):` when it is ended after the steps. A shell that carries no state between commands, or only the folder and variables (the sh family), gets one `Note:` line after `Using shell:` ([spec_scenario.md](spec_scenario.md) → Shell state across steps). A replay shows `Replaying: <file>  (recorded by scenetake <version> on <os> with <shell>, <W>x<H>). No command runs.` after `Loading:`, and neither `Using shell:` nor `running:` lines (Replay below).

Each `Written:` line gives the file's size in pixels, so a reader can tell what the demo fits, such as a README or a 1920x1080 video, without opening it:

```
Written: basic.cast  (185 events, 18.1s, as SVG 830x504 px, as mp4 820x488)
Written: basic.svg  (830x504 px, 18.1s)
Written: basic.mp4  (820x488, 30fps, 543 frames, 62.7KB, font: Cascadia Mono)
```

- The SVG's size is its `width` and `height`: the size a page first shows it at, rounded to whole pixels. An SVG scales without blur. The time is its loop.
- The cast's line gives the SVG and mp4 sizes only when the cast is the only output. They are computed from the font size, the terminal size, the window, the padding, the margin, and the background, without rendering. An mp4 too large to encode is left out, and `scenetake mp4` reports why.
- The mp4's size is its encoded frame: no margin or shadow, even sides, and `--size` or `--max-size` applied.
- **Why.** The size decides where a demo fits: a README shows about 830 pixels at its own size, and a video player expects 16:9. It was visible only after opening the file.

After the cast's `Written:` line, a recording and a replay print the time per step: a row for each shown step with its time in the cast and its typing, output, and pauses, then the final screen and the total ([spec_scenario.md](spec_scenario.md) → Time per step). `capture` prints none.

**`pre` / `post`.** stdout and stderr are forwarded after each command exits (live streaming not required in v1). Failure prints phase, full command text, and exit code unconditionally. See [spec_pre_post.md](spec_pre_post.md).

**Warnings.** Non-fatal issues print `Warning: …` to stderr and execution continues. Feature-specific warn-and-continue rules: [spec_highlight.md](spec_highlight.md), [spec_svg.md](spec_svg.md), [spec_scenario.md](spec_scenario.md) → Step exit codes and → Unknown keys.

**Large SVG.** After writing an SVG over 10MB, scenetake prints the warning below. The file is kept and the exit code does not change. 10MB is GitHub's limit for image attachments, and browsers slow down on SVGs that large.

```
Warning: SVG is 17.0MB (over 10MB). Heavy output such as full-screen TUIs is much smaller as mp4 (--format mp4) or as SVG with --max-fps 20
```

- The `svg` subcommand suggests `scenetake mp4 <cast>` instead of `--format mp4`.
- When `--max-fps` is already set, the warning suggests "a lower `--max-fps`".

**Sizes** in logs and warnings use decimal units (1MB = 1,000,000 bytes).

**Recorded `steps`.** When a step command exits non-zero, scenetake prints:

- `Warning: step exited <code>: <run>`
- `Warning: step exited <code> (<name>): <run>` when the step has a `name`

A step with `continue-on-error: true` prints no exit warning. With `--strict`, an unexpected non-zero exit produces an error and stops recording. The error includes the last 20 output lines because no cast is written. Escape sequences are removed, and lines redrawn with `\r` show their final text:

```
Error: --strict stopped at step 3, which exited 2: make build
  | make: *** No rule to make target 'build'.  Stop.
Nothing was written. Set 'continue-on-error: true' on a step that is meant to fail.
```

Semantics (without `--strict` the recording continues and the process exit code stays `0`): [spec_scenario.md](spec_scenario.md) → Step exit codes.

**Errors.** Fatal issues print `Error: …` to stderr and exit non-zero.

**Cast read errors.** A cast line that is not JSON is reported as `invalid JSON at line <n>: <reason>`. The position suffix System.Text.Json adds (`LineNumber: 0 | BytePositionInLine: …`) is left out: it counts within the one line it was given, so it contradicted `<n>`.

**A scenario given to `svg` or `mp4`.** The command rejects `.yaml` or `.yml` input whose first non-blank character is not `{`. This check precedes cast parsing, ffmpeg lookup, and any output changes. A cast saved with a `.yaml` extension still converts.

The error identifies the input as a scenario and gives the recording command:

```text
Error: demo.yaml is a scenario, and svg converts a cast file. To record the scenario with an SVG, run: scenetake --format svg demo.yaml
```

Previously, cast parsing reported a JSON error before the hint. Failed conversion could also delete `demo.svg` or `demo.mp4` from an earlier scenario recording.

## Exit Codes

| Outcome | Code |
|---|---|
| Success | `0` |
| Parse / validation error, missing file, cast/SVG/mp4 write failure, ffmpeg missing or without libx264 | `1` (or command-specific for `pre`/`post`) |
| Failed `pre` or `post` command | Failed command's exit code |
| `--strict` and a recorded step exited non-zero without `continue-on-error: true` | `1`, with nothing written but the `--save-keys` file, after `post` has run (a failing `post` returns its own code) |
| The `--save-keys` file could not be written | `1`, after the other outputs |
| Several scenarios, and one was not recorded (it failed, or Ctrl+C stopped the run) | `1`, after the others were recorded |

Without `--strict`, recorded step exit codes do not affect the process exit code. See [spec_scenario.md](spec_scenario.md) → Step exit codes.

## Outputs list

A scenario's top-level `outputs` lists the files a recording writes besides the cast, so one run gives every place a demo goes its own file:

```yaml
outputs:
  - format: svg                    # demo.svg, for the README
  - format: svg
    theme: github-light
    file: demo-light.svg           # with the dark one, the CLI prints a <picture> for a README
  - format: gif                    # for Zenn, under 3 MB
    font-size: 12
    fps: 10
  - format: mp4                    # for X
    max-size: 1280x1024
  - format: png
    file: demo-title.png
    at: 1                          # one screen, such as a title card, for a page's preview image
```

- **When it applies.** Without `--format`, the scenario path writes the cast and every entry, and so does `--replay`. With `--format`, the command line wins as it does over other YAML keys: only that file is written, and stderr says `Note: --format svg writes only that file: the scenario's outputs are not written.` capture builds its own scenario and has no list. The playground lists `outputs` as not applied: it shows one SVG with its own options.
- **An entry.** `format` (svg, mp4, gif, or png) is required, and `cast` is an error, because every recording writes the cast. `file` names the file, relative to the folder of the output stem. Its extension may be left out, and another format's extension is an error. Any other dot is part of the name: `file: demo.light` writes `demo.light.svg`. Without `file`, an entry writes `<stem>.<format>`, so two entries of one format need a `file` each. Every other key is a command-line option of the same name without `--`, with the same values, ranges, and format rules: the look (`theme`, `window`, `window-title`, `background`, `margin`, `padding`, `opacity`, `blur`, `fit`, `crop-top`, `crop-bottom`, `crop-left`, `crop-right`, `font-size`, `font-family`, `loop`, `end-hold`, `fade-out`, `speed`, `length`) and the format's options (`at`, `max-fps`, `fps`, `crf`, `max-size`, `size`, `font-file`). `loop` takes `true` or `false`. A file (`font-file`, an image `background`) is found from the scenario's folder, as `render.background`'s image is. A key an entry does not read is an unknown-key warning.
- **The look.** The cast is drawn as the scenario and the command line set it. An entry's keys go on top for that file only, as `scenetake svg --theme ...` puts its options on a cast's. An entry's `theme` replaces the scenario's theme with its `fg`, `bg`, and `palette`, so a light copy of a dark demo takes the light theme's own colors.
- **Checks before anything runs.** Each entry is read as `--format FORMAT` with its options, so an error is the command line's, naming the entry and its keys instead of the options: `outputs entry 3 (gif): 'crf' applies to mp4: a GIF has no H.264 quality`. Then the paths are checked as the `--format` output's are, two entries that write one file are an error, and each entry's ffmpeg, font, and frame are checked as for `--format`. An output that would write over a background image that an output is drawn on is an error, for `--format` and the `png` and `gif` commands too: rendering would replace the image, and a failed render would remove it.
- **Writing.** The entries are drawn from the cast as written, in order. One that fails removes its file, says why with its entry's name, and the others are still written. The run then exits `1`. `Done:` lists the cast and every file.
- **Light and dark.** When the files include an animated SVG on a dark theme and one on a light theme (as the window chrome tells them apart, by the background's lightness), stderr ends with a `<picture>` that a GitHub README uses to show the one that matches the reader's color scheme. Its `alt` is the scenario's `title`.
- **Why a list in the YAML, not several `--format` values.** Each place takes another format with other options: a GIF under Zenn's 3 MB needs a smaller font and fewer frames, an mp4 for X a size limit, a light README image another theme. Command-line options apply to one format at a time. In the scenario, the options sit with the format they shape, and a CI run that records the scenario writes every file.

## Several scenarios

```bash
scenetake docs/demos/*.yaml
scenetake --strict --format svg intro.yaml usage.yaml
```

- **What counts as a scenario.** The first positional argument is always a scenario. After it, an argument that ends with `.yaml` or `.yml`, or holds a `*` or `?`, is another scenario. Any other argument is the output name, which one scenario file takes (`scenetake demo.yaml out`). With several scenarios or a wildcard, an output name is an error, because each scenario is written next to its YAML.
- **Wildcards.** scenetake expands `*` and `?` in the file name itself, because PowerShell and cmd pass `docs/*.yaml` on as it is. Bash and zsh expand it before scenetake sees it, and both give the same list. A wildcard in a folder name, and `**`, are errors. Matches come in ordinal order, and a file named twice is recorded once. Paths are compared as written, after the Windows device prefixes (`\\?\`, `\\.\`, `\??\`) are taken off: a link, a volume GUID path, or a name in another case on a volume that ignores case is another name. Such a file is recorded once for each name, and two such names that write one cast meet the check below only when their written paths match ignoring case. A name that starts with a dot is left out unless the pattern starts with one, as in a shell. On Windows a pattern matches in any case, as the file system does. A pattern that finds nothing is an error, and so is a folder that cannot be listed. One that finds one file is an ordinary run. A file whose own name holds `*` or `?`, which Unix allows, is that file, as a shell lists it, and each scenario's own process gets it that way too. The `?` of a Windows device path (`\\?\C:\...`) is part of the path, since Windows allows neither character in a name.
- **Checks before anything runs.** Every file is found first. `--save-keys`, `--save-outputs`, `--replay`, and `SCENETAKE_OUTPUTS` name one file, so with several scenarios each is an error: `--save-outputs names one file, and 2 scenarios were given: record them one at a time`. Two scenarios that write one cast, such as `demo.yaml` and `demo.yml`, are an error too, compared ignoring case as other output paths are. Nothing is recorded when a check fails.
- **Recording.** The scenarios are recorded one after another, in the order given, each with the rest of the command line, as `scenetake <options> <scenario>` would record it alone. Each writes what it writes alone: the cast and its `--format` output or `outputs` list. Its own lines (`Loading:`, `Done:`) are printed as they are, with an empty line between scenarios. A scenario that fails, such as at a `--strict` step, does not stop the others.
- **The last line.** `Recorded 4 scenarios.`, or `Recorded 3 of 4 scenarios. Failed: docs/demos/b.yaml`. The exit code is `1` when one was not recorded.
- **Ctrl+C.** The scenario being recorded gets it too and stops as it would alone. No other scenario starts, and the last line says `Stopped by Ctrl+C: recorded 1 of 4 scenarios. Not recorded: ...`.
- **Why a process for each scenario.** A recording keeps state for its run: the shell state folder, the background steps and their Ctrl+C handler, and the fonts that `pre` may install. A process of its own leaves nothing of one scenario to the next, such as a server that a background step started, and the recording code stays as it is. Starting the program again takes tens of milliseconds, against seconds of recording. The program started is the running one: the native binary, or the app host of `dotnet run` and of the dotnet tool.
- **Why nothing is skipped.** A scenario whose YAML has not changed still needs recording when the product it shows has changed, and the YAML cannot tell. Recording an unchanged demo again gives the same files byte for byte ([spec_scenario.md](spec_scenario.md) → Recording again), so recording every scenario and comparing the result with git shows only the demos that changed. README → CI shows that workflow.
- **Why one after another.** Steps are typed with real delays, and an interactive step takes the terminal.

## Capture

`scenetake capture [options] -- <command>` (or `scenetake capture [options] "<command>"`) records one command without a scenario file.

- **What it records.** The scenario a file with one step would hold: `title` and `run` are the command, the step runs with `pty: true` unless `--pipe` is given, and `--width`, `--height`, `--shell`, and `--timeout` set `width`, `height`, `shell`, and the step's `timeout`. The style options that are `render` keys (`--font-size`, `--font-family`, `--theme` as `theme.preset`, `--window`, `--window-title`, `--background`, `--margin`, `--padding`, `--opacity`, `--blur`, `--no-loop` / `--loop`, `--end-hold`, `--fade-out`, `--speed`, `--length`, `--fit`, and `--crop-*`) are written under `render`, and still apply as options. The scenario path then records it as it records a file: the typed command, its output, masking, and every format and render option. Each step's typing is seeded by the step ([spec_scenario.md](spec_scenario.md) → Determinism), so the same capture types the same way.
- **Its own options.** `--width N` and `--height N` (terminal columns and rows, `1`–`512`, default 80 × 24), `--shell NAME`, `--pipe`, `--timeout N` (seconds above 0), `-o` / `--output NAME`, which names the files as the scenario path's `[output]` does, and `--save-scenario FILE`. The scenario path's options all apply too.
- **Going on as a scenario.** `--save-scenario FILE` writes that scenario to FILE (UTF-8 without BOM), with the editor schema's line first, as `init` writes it ([spec_scenario.md](spec_scenario.md) → Editor schema), before the command runs, so a capture that fails still leaves it to fix. The recording is named after FILE and goes next to it, as `scenetake --format svg FILE` would write it, and that command then records the same SVG and compares with the cast already there ([spec_scenario.md](spec_scenario.md) → Recording again). An image background is written relative to FILE's folder. FILE's folder is made if it is missing. An existing FILE is an error before anything runs, as for `init`, and the error gives the command that records it. `--max-fps`, `--at`, and the mp4 and GIF options are not `render` keys and stay options.
- **`--strict` on one command.** A command that exits non-zero writes nothing but a `--save-scenario` file, exits `1`, and the error speaks of the command: `--strict stopped: the command exited 2: …`, then `Without --strict, the recording keeps a command that fails.` `capture --help` describes `--strict` and `--verbose` the same way. A capture has no step to set `continue-on-error` on, no `pre` or `post`, and no interactive step for `--save-keys`, and the scenario's text for both options named all of them.
- **Defaults.** It writes an SVG and the cast next to it when no `--format` is given, because it is for a quick image. The files go to the current folder, named after the command without its folder and extension: `git status` writes `git.cast` and `git.svg`, `./build.sh` writes `build.*`, and a command that leaves no name writes `capture.*`.
- **The command.** After `--` each word is the caller's shell's word, and capture quotes a word again when it holds a space, a quote, or another character a shell reads, for the shell that runs it: single quotes for POSIX shells (`'it'\''s'`) and PowerShell (`'it''s'`), double quotes for cmd. Some punctuation only one shell reads, and a word with it is quoted for that shell alone: `@name` (splatting) and `--%` for PowerShell, `^` for cmd, and `=` at the start for zsh, which reads `=ls` as the path of `ls`. `npx @scope/tool` stays as it is everywhere. Without `--` the one argument is the command as it is typed and run, like a step's `run`. Both forms, or more than one argument, are an error.
- **Why a PTY by default.** A one-command capture is usually a tool's colored output, which most tools print only to a terminal. A command that waits for input in a PTY needs `--timeout`, or a scenario with `keys`.
- **Why it is a scenario.** Building the one-step scenario keeps every option, the masking, the outputs file, and the cast the same as for a file, with no second recording path. A capture that needs more, such as several commands, typing settings, or keys, becomes a scenario file with `--save-scenario`.
- **Why `--save-scenario`.** capture built the scenario and dropped it, so the evaluation on 2026-11-18 found no way from a capture to a scenario: someone who wanted a comment or a second command started again from `init`. A scenario is what scenetake records from, so the first try should leave one. It is opt-in, because a capture is often a throwaway image, and a file written every time would pile up next to the outputs.

## Replay

`scenetake --save-outputs demo.outputs.json demo.yaml` records as usual and also writes the outputs file. `scenetake --replay demo.outputs.json demo.yaml` then writes the cast, and the `--format` output, with each step's output taken from that file. No command runs.

- **Why.** Tuning how a demo looks, its typing, comments, highlights, theme, window, and format, took a full run each time: a slow build, an external API, or output that changes each run (times, IDs) came again with every try. A replay takes a fraction of a second and gives the same output every time.
- **The same replay as the playground.** Steps match recorded outputs as in the playground ([spec_playground.md](spec_playground.md) → Matching steps to recorded outputs): by `run` text and kind (pipe, PTY, interactive), the n-th such step taking the n-th recording. With the YAML unchanged, the cast and the SVG are the recording's byte for byte. The cast header names the shell the outputs file was recorded with.
- **What applies.** Every key that changes how a step looks or when: the prompt, typing, `name`, highlights, `stderr-color`, output timing, `render`, `title`, the step order, a removed step, and every render option. A pipe step replays at any terminal size. A PTY or interactive step recorded at another size gets `Warning: step N was recorded in a 80x24 terminal, but this one is 100x30, so the output may not fit`.
- **What does not run.** `pre`, `post`, `show: none` steps, and `background` steps run nothing, and `cwd` is not checked. stderr says `Note: pre and post do not run in a replay.` when the scenario has them. A `shell` other than the recording's gets a warning. `--strict` applies to the recorded exit codes.
- **A step without a recording stops the replay**, unlike the playground, which shows a placeholder line: a file meant to be shared must not hold made-up output. The error lists each such step with its number, kind, and `run`, and nothing is written. A step whose `run` changed needs a new recording with `--save-outputs`.
- **Masking.** The outputs file was masked when it was recorded. The replay masks again with the scenario's rules and this machine's environment and personal details ([spec_scenario.md](spec_scenario.md#masking-personal-details) → Masking personal details), which hides what the YAML adds, such as a new `settings.mask` text, and changes nothing else.
- **Errors.** An outputs file that cannot be read or is not one: `cannot read the outputs file <path>: <reason>`. An output that would write over the file being replayed is refused. `--replay` with `--save-outputs` or `--save-keys` is refused, since nothing runs and no one types.
- **`SCENETAKE_OUTPUTS`** still names an outputs file for `samples/regenerate.cs`, which sets it for every sample. `--save-outputs` wins when both are given.

## Init

- Default output path: `scenario.yaml` in the current directory.
- Fails if the target file already exists.
- Generated template follows [spec_scenario.md](spec_scenario.md), written as UTF-8 without a BOM. Its first line names the editor schema of this version ([spec_scenario.md](spec_scenario.md) → Editor schema).
- `--shell` picks the shell the template's steps are written for, and the file sets `shell:` to it. Without `--shell` they are written for the OS default shell (pwsh on Windows, `$SHELL` elsewhere), and `shell:` stays commented out. See spec_scenario.md → Init Template.

## Cross-Document Notes

- [spec_scenario.md](spec_scenario.md) — scenario YAML structure and defaults.
- [spec_cast.md](spec_cast.md) — cast file format.
- [spec_svg.md](spec_svg.md) — SVG renderer, `svg` subcommand event handling.
- [spec_mp4.md](spec_mp4.md) — mp4 renderer, ffmpeg, `mp4` subcommand failure behavior.
- [spec_pre_post.md](spec_pre_post.md) — `pre`/`post` execution and failure behavior.
- [spec_highlight.md](spec_highlight.md) — coloring validation warnings.

## Lessons Learned

- Unknown options should fail explicitly. Otherwise mistyped flags are misread as file paths.
- Default logs should stay focused on cast content. `--verbose` is the right place for successful `pre`/`post` labels.
- Reject options for another output format. This catches `--fps` on SVG runs and `--max-fps` on mp4 runs immediately.
- **Setup may create the output folder.** Check that folder after `pre` when setup commands exist. Check ffmpeg, fonts, and frame size before `pre` to avoid failures after a long recording. Setup therefore cannot install ffmpeg or the only usable font. The check after `pre` left out the `--save-outputs` file until 2026-11-26: with a missing folder the whole recording ran, then writing the file failed and no output was rendered.
- **Unix link targets need full resolution.** A target may contain further links and `..`. Unix applies `..` after resolving preceding links. This matters for macOS paths such as `/var`, which links to `/private/var`.
- **Link limits must cover the whole path.** A per-link depth limit allowed repeated traversal to double work at each level. A chain 22 links deep took 9 seconds in Linux under WSL. A shared limit of 40 links bounds the walk.
- **Windows identity checks need no read access.** Opening files for reading failed when another process denied read sharing. Opening with no access rights still permits identity checks. WSL shares report volume serial number `0` and use Linux inodes as indices. Files on different Linux filesystems can therefore have matching indices.
- **Device paths must retain their prefix.** Adding `\\?\` to a `\??\` path made it invalid. Identity lookup failed, and failed rendering deleted the input through that spelling. Device paths now remain unchanged.
- **Windows must resolve its own links.** Relative targets through junctions resolve against the opened path, rather than the junction target. Targets starting with a separator resolve against the current drive. Manual traversal therefore rejected valid outputs. File indices handle normal volumes. WSL shares need final paths from opened files because their serial number is `0`. Comparing only written paths missed Windows links to WSL inputs and allowed overwrites.
- **Help must describe command-specific behavior.** Previously, `--help` printed only usage lines on stderr, leaving option details in the README. Help now describes each option on stdout, which supports pagers. Scenario `--theme` retains YAML color overrides. Conversion `--theme` replaces cast colors, so the commands need different descriptions. A test checks help coverage against each parser's accepted options.
- **An output's look goes on the settings read back from the cast** (2026-11-20). The scenario path draws every file from the cast it wrote, and took from the run's own settings only an image background and `--max-fps`, which the cast does not store. An outputs entry's theme or font applied to the run's settings would have changed nothing. Entries are applied after the cast is read, as the `svg` command applies its options.
- **A second .yaml was the output name** (2026-11-20). Before several scenarios, `scenetake a.yaml b.yaml` took `b.yaml` as the output stem: it recorded a.yaml into b.cast, over b's own recording, without a word. A second argument that ends with `.yaml` or `.yml` is now another scenario.
- **Paths are compared as written** (2026-11-20). Telling one file's names apart by the file itself was tried: the path of the opened file on Windows, the name as its folder lists it elsewhere. Each review round found another name it got wrong. A link writes its cast under its own name but opened as its target, a case-sensitive NTFS folder holds two files that differ in case, and a name ending in a dot lost it when normalized. Written paths, with the device prefixes taken off, are predictable, and the names they miss only record a file twice or skip the shared-cast check.
- **Each scenario's process must not expand its path again** (2026-11-20). The first version gave each scenario's process the full path and expanded it as a pattern. On Unix a file named `a?.yaml` matched itself and `ab.yaml`, so each process started the batch again, without end, until the machine ran out of memory. A `\\?\` path or a folder with `?` in it failed every scenario. A file that exists by the name given is now that file.
- **PowerShell passes a wildcard on as it is** (2026-11-20). `scenetake docs/*.yaml` worked in bash and found no file in PowerShell and cmd, which leave the expansion to the program. scenetake matches the file name itself, in its own way on every OS, rather than with the file system's pattern, whose Windows rules differ.
- **An entry is read by the command-line parser** (2026-11-20). Its keys are turned into `--format FORMAT --KEY=VALUE` and read as a command line, so an entry takes every rule of its format with no second list of rules to keep in step. The errors are rewritten to name the keys (`'crf'` for `--crf`).
- **Each output read the cast again** (2026-11-26). The outputs are rendered from the written cast, and each entry read and parsed it again, 26ms for a 4.4MB cast. It is read once for all entries. A cast that fails to read is still tried again by the next entry, so each entry reports the error as before. The sizes on the cast's line, when the cast is the only output, each replayed the recording to find the canvas, and now share one replay.
- **Messages named files by a climb to the root and back** (2026-11-20). macOS gives the current directory as `/private/var/...` while a path through `/var/...` keeps the link, so a file in the current directory read as `../../../../var/.../bg.png`. Two outputs-list tests checked the short name and failed only on the macOS runners. The relative path is now tried between the followed forms too.
