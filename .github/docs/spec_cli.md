# CLI Specification

Status: **Implemented**

## Motivation

scenetake records scenarios, converts casts to SVG or mp4, and creates starter YAML files. This document defines the CLI behavior shared by feature specifications.

## Commands

| Command | Purpose |
|---|---|
| `scenetake <scenario.yaml> [output]` | Run a scenario. Write `.cast` (default), `.cast` + `.svg`, or `.cast` + `.mp4` |
| `scenetake svg <input.cast> [output]` | Convert an existing cast to SVG |
| `scenetake mp4 <input.cast> [output]` | Convert an existing cast to mp4 (needs ffmpeg. See [spec_mp4.md](spec_mp4.md)) |
| `scenetake png <input.cast> [output]` | Draw one screen of an existing cast as a PNG (see [spec_png.md](spec_png.md)) |
| `scenetake gif <input.cast> [output]` | Convert an existing cast to an animated GIF (needs ffmpeg. See [spec_gif.md](spec_gif.md)) |
| `scenetake capture [options] -- <command>` | Record one command without a scenario file. See Capture below. |
| `scenetake init [--shell bash\|pwsh\|powershell] [scenario.yaml]` | Create a commented starter scenario file |
| `scenetake --help` | Print every command and option, with its range and default |
| `scenetake --version` | Print version |

```bash
scenetake [--verbose] [--strict] [--format cast|svg|mp4|png|gif] [--at TIME] [--font-size N] [--font-family FAMILIES] [--theme NAME] [--window none|macos|windows] [--max-fps N] [--fps N] [--crf N] [--max-size WxH | --size WxH] [--font-file PATH] <scenario.yaml> [output]
scenetake svg [--at TIME] [--font-size N] [--font-family FAMILIES] [--theme NAME] [--window none|macos|windows] [--max-fps N] <input.cast> [output]
scenetake mp4 [--font-size N] [--font-family FAMILIES] [--font-file PATH] [--theme NAME] [--window none|macos|windows] [--fps N] [--crf N] [--max-size WxH | --size WxH] <input.cast> [output]
scenetake png [--at TIME] [--font-size N] [--font-family FAMILIES] [--font-file PATH] [--theme NAME] [--window none|macos|windows] <input.cast> [output]
scenetake gif [--fps N] [--max-size WxH | --size WxH] [--no-loop] [--font-size N] [--font-family FAMILIES] [--font-file PATH] [--theme NAME] [--window none|macos|windows] <input.cast> [output]
scenetake init [--shell bash|pwsh|powershell] [scenario.yaml]
```

`--help` (or `-h`) describes each option, its range, and its default on stdout, then exits `0`. `svg --help`, `mp4 --help`, `png --help`, `gif --help`, and `init --help` list command-specific options. Scenario help explains overrides of YAML `render` settings. Conversion help explains overrides of cast metadata. After an error, stderr shows short usage text and a reference to the command's `--help`. Help and error messages use ASCII only.

## Parsing

- Options may appear in any order before positional arguments on the scenario, `svg`, `mp4`, `png`, and `gif` paths.
- Unknown `-` / `--` options are explicit errors (avoids typos becoming path-not-found errors).
- `--format`, `--at`, `--font-size`, `--font-family`, `--theme`, `--window`, `--max-fps`, `--fps`, `--crf`, `--max-size`, `--size`, and `--font-file` accept `--name=value` or `--name value`.
- Duplicate `--at`, `--font-size`, `--font-family`, `--theme`, `--window`, `--window-title`, `--background`, `--margin`, `--padding`, `--opacity`, `--blur`, `--max-fps`, `--fps`, `--crf`, `--max-size`, `--size`, or `--font-file` is an error. `--size` with `--max-size` is an error too: `--size` already sets the output size.
- Options for the other output format are errors, not ignored:
  - On the scenario path, `--fps`, `--max-size`, and `--size` without `--format mp4` or `gif` fail with `<option> requires --format mp4 or gif`. `--crf`, H.264's quality, needs `--format mp4`: `--crf requires --format mp4`, and with GIF output `--crf applies to mp4: a GIF has no H.264 quality`. `--font-file` without `--format mp4`, `png`, or `gif` fails with `--font-file requires --format mp4, png, or gif`. The `svg` subcommand reports them as unknown options.
  - With GIF output, `--fps` above 50 fails with `--fps for a GIF must be between 1 and 50: <N> (a GIF counts each frame's delay in hundredths of a second)`.
  - `--max-fps` with mp4 or GIF output fails with `--max-fps applies to SVG, use --fps for mp4` (or `for GIF`).
  - `--max-fps` with cast output (no `--format svg`) fails with `--max-fps requires --format svg`, and with PNG output with `--max-fps applies to an animated SVG: a PNG is one screen`.
  - `--at` needs SVG or PNG output: with cast output it fails with `--at requires --format svg or png`, and with mp4 or GIF with `--at applies to SVG and PNG: an mp4 plays the whole recording` (`a GIF plays` for GIF).
  - A still SVG has no animation. With `--at`, `--max-fps` fails on the scenario path, and the `svg` subcommand rejects `--max-fps`, `--loop`, `--no-loop`, `--end-hold`, and `--fade-out` with `<option> has no effect with --at: a still SVG does not play`. On the scenario path the loop and end options are stored in the cast, so they stay allowed.
  - The `png` subcommand rejects `--fps`, `--max-size`, and `--size` (`<option> applies to mp4 and GIF: a PNG is one screen at the terminal's own size`), `--crf` (`applies to mp4`), and `--loop`, `--no-loop`, `--end-hold`, and `--fade-out` (`<option> applies to animation: a PNG is one screen, which --at picks`).
  - The `gif` subcommand rejects `--crf`, `--max-fps`, and `--at` with the messages above.
- For `--fps`, `--crf`, `--max-size`, `--size`, and `--font-file`, a next argument that starts with `--` is not taken as the value: `--font-file --fps 30` fails with `--font-file requires a value`.
- Range errors use plain ASCII ("between 1 and 60"), so every console code page shows them.

## Options

| Option | Commands | Behavior |
|---|---|---|
| `--verbose` | scenario path only | Show successful `pre`/`post` labels and phase markers, and the exception type and stack when an error stops the recording. See [spec_pre_post.md](spec_pre_post.md). |
| `--strict` | scenario path only | For CI. Stop at the first recorded step that exits non-zero without `continue-on-error: true`. Skip later steps, preserve existing output files, and write no cast, SVG or mp4. Run `post`. Exit `1`. If a `post` command fails, return that command's exit code. The `svg` and `mp4` subcommands report it as an unknown option. See [spec_scenario.md](spec_scenario.md) → Step exit codes. |
| `--format cast\|svg\|mp4\|png\|gif` | scenario path only | Default `cast`. `svg` also writes `.svg`, `mp4` also writes `.mp4`, `png` also writes `.png`, and `gif` also writes `.gif`. See [spec_svg.md](spec_svg.md), [spec_mp4.md](spec_mp4.md), [spec_png.md](spec_png.md), and [spec_gif.md](spec_gif.md). |
| `--at TIME` | scenario (`--format svg` or `png`), `svg`, `png` | One screen: the one shown `TIME` seconds into the recording (decimals allowed, `0` or more), or `end` for the final screen. A time past the end gives the final screen, and a time before the first output an empty terminal. With SVG it makes a still SVG without animation ([spec_svg.md](spec_svg.md) → Still SVG). A PNG shows the final screen without it. Not stored in the cast. |
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
| `--fit none\|height\|width\|both` | scenario, `svg`, `mp4`, `png`, `gif` | Move the bottom edge (`height`), the right edge (`width`), or both in to the last row and column the recording shows, the cursor included. Default `none`. The cast keeps the terminal's size. See [spec_svg.md](spec_svg.md) → Fit and crop. |
| `--crop-top N`, `--crop-bottom N`, `--crop-left N`, `--crop-right N` | scenario, `svg`, `mp4`, `png`, `gif` | `0`–`511` cells cut from that edge before `--fit`. Default `0`. A crop that leaves no row or no column is an error, before anything runs on the scenario path and before the render on a cast. |
| `--fade-out N` | scenario, `svg`, `mp4`, `gif` | `0`–`10` seconds over which the terminal's contents fade out after the end hold. Default `0`. With `--no-loop`, or a cast that does not loop, the SVG and the GIF have none, with a warning. An mp4 still fades. |
| `--max-fps N` | scenario (`--format svg` only), `svg` | Optional frame sampling cap for SVG animation. `0` (default) = off (full cast timing). `1`–`120` enables sampling. See [spec_svg.md](spec_svg.md). |
| `--fps N` | scenario (`--format mp4` or `gif`), `mp4`, `gif` | Constant frame rate. mp4: `1`–`60`, default `30`. GIF: `1`–`50`, default `20` ([spec_gif.md](spec_gif.md)). |
| `--crf N` | scenario (`--format mp4`), `mp4` | H.264 quality, `1`–`51`. Default `23`. Higher values make smaller files. `0` (lossless) is not accepted, because x264 then writes the High 4:4:4 Predictive profile, which X and many players reject. |
| `--max-size WxH` | scenario (`--format mp4` or `gif`), `mp4`, `gif` | Downscale to fit within `W×H` (each side `16`–`16384`), keeping the aspect ratio. Never upscales. `1280x1024` fits X video uploads. A scaled GIF blurs its text and can be larger ([spec_gif.md](spec_gif.md) → Lessons Learned). |
| `--size WxH` | scenario (`--format mp4` or `gif`), `mp4`, `gif` | Make the video exactly `W×H` (each side `16`–`16384` and even): pad with the theme background, and scale down a larger terminal. Never upscales. Render only: the terminal's columns and rows are unchanged. Not with `--max-size`. See [spec_mp4.md](spec_mp4.md) → Exact size. |
| `--font-file PATH` | scenario (`--format mp4`, `png`, or `gif`), `mp4`, `png`, `gif` | Primary font file (TTF, OTF, TTC) for mp4, PNG, and GIF, before the `font-family` lookup. See [spec_mp4.md](spec_mp4.md). |

`--format mp4` and the `mp4` subcommand also accept `--font-size`, `--font-family`, `--theme`, and `--window` with the same priority as SVG (CLI > cast header > default). `--window` draws the title bar and buttons without the SVG margin and shadow (see [spec_mp4.md](spec_mp4.md) → Window chrome). `--format png`, `--format gif`, and the `png` and `gif` subcommands accept them with the same priority, and the other style options too.

**Environment.** `SCENETAKE_FFMPEG` names the ffmpeg binary for mp4 and GIF output, and for decoding a PNG's background image. Otherwise `ffmpeg` on `PATH` is used. mp4 needs libx264, a GIF any build, and a PNG without a background image no ffmpeg.

`SCENETAKE_OUTPUTS` names a file for the scenario path to write the outputs file to, after the cast and from the same run: what each step printed, which the browser playground replays ([spec_playground.md](spec_playground.md) → Outputs File). A relative path is resolved against the current folder. A failed write is an error, as for the cast, and `post` runs. The variable is for maintainers: `samples/regenerate.cs` sets it, and `--help` does not list it.

`init` accepts `--shell bash|pwsh|powershell` (`--shell value` or `--shell=value`, once) and an optional output path positional. No other flags. Another shell value, a missing value, or a second `--shell` is an error that writes nothing.

## Output Paths

**Scenario path.** `[output]` sets a shared stem. Extensions differ (`.cast`, plus `.svg` with `--format svg`, `.mp4` with `--format mp4`, `.png` with `--format png`, or `.gif` with `--format gif`). With no `[output]`, stem matches the scenario path without extension. Passing `out.cast`, `out.svg`, `out.mp4`, `out.png`, or `out.gif` yields the same stem.

**`svg`, `mp4`, `png`, and `gif` subcommands.** With no `[output]`, writes alongside the input as `<stem>.svg`, `<stem>.mp4`, `<stem>.png`, or `<stem>.gif`. With `[output]`, uses that path's stem and directory (extension optional).

**Checks.** Before commands run, scenetake rejects output paths that are directories or refer to the input file. The scenario path checks these before `pre`. For example, `scenetake mp4 clip.mp4` would overwrite its input, and failed rendering deletes the output.

Path comparison ignores case because any OS can use a volume that ignores case. Additional checks depend on the platform:

| Platform | Same-file checks and limits |
|---|---|
| Windows | Compare volume serial numbers and file indices, even while another process holds a file open. This detects symbolic links, junctions, hard links, mapped drives, subst drives, and device or UNC paths (`\\?\`, `\??\`). |
| Windows volumes with serial number `0`, including WSL shares | Compare the final paths from opened files. Treat `wsl$` and `wsl.localhost` as the same server. Matching indices in one folder identify hard links when both names report multiple links. Hard links across folders are not detected. |
| Unix | Follow symbolic links, including links within targets. Apply `..` after preceding links. Hard links are not detected. |



The output folder must exist as well. When the scenario has `pre` commands, the folder is checked after `pre`, which may create it. Every other check runs again then, because `pre` may also put a directory, or a link to the input, where an output goes. A failure then stops the run before the recording, and `post` runs because `pre` succeeded. Without `pre` commands the folder is checked before anything runs.

## Logging

Progress and status use stderr (`Loading:`, `Written:`, `Done:`). Paths use `/` separators and are relative to the current directory where possible. Paths on another Windows drive, for example, remain absolute. Scenario runs always show step `running:` lines.

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
| `--strict` and a recorded step exited non-zero without `continue-on-error: true` | `1`, with nothing written, after `post` has run (a failing `post` returns its own code) |

Without `--strict`, recorded step exit codes do not affect the process exit code. See [spec_scenario.md](spec_scenario.md) → Step exit codes.

## Capture

`scenetake capture [options] -- <command>` (or `scenetake capture [options] "<command>"`) records one command without a scenario file.

- **What it records.** The scenario a file with one step would hold: `title` and `run` are the command, the step runs with `pty: true` unless `--pipe` is given, and `--width`, `--height`, `--shell`, and `--timeout` set `width`, `height`, `shell`, and the step's `timeout`. The scenario path then records it as it records a file: the typed command, its output, masking, and every format and render option. The scenario's text seeds the typing, so the same capture types the same way.
- **Its own options.** `--width N` and `--height N` (terminal columns and rows, `1`–`512`, default 80 × 24), `--shell NAME`, `--pipe`, `--timeout N` (seconds above 0), and `-o` / `--output NAME`, which names the files as the scenario path's `[output]` does. The scenario path's options all apply too.
- **Defaults.** It writes an SVG and the cast next to it when no `--format` is given, because it is for a quick image. The files go to the current folder, named after the command without its folder and extension: `git status` writes `git.cast` and `git.svg`, `./build.sh` writes `build.*`, and a command that leaves no name writes `capture.*`.
- **The command.** After `--` each word is the caller's shell's word, and capture quotes a word again when it holds a space, a quote, or another character a shell reads, for the shell that runs it: single quotes for POSIX shells (`'it'\''s'`) and PowerShell (`'it''s'`), double quotes for cmd. Without `--` the one argument is the command as it is typed and run, like a step's `run`. Both forms, or more than one argument, are an error.
- **Why a PTY by default.** A one-command capture is usually a tool's colored output, which most tools print only to a terminal. A command that waits for input in a PTY needs `--timeout`, or a scenario with `keys`.
- **Why it is a scenario.** Building the one-step scenario keeps every option, the masking, the outputs file, and the cast the same as for a file, with no second recording path. A capture that needs more, such as several commands, typing settings, or keys, becomes a scenario file.

## Init

- Default output path: `scenario.yaml` in the current directory.
- Fails if the target file already exists.
- Generated template follows [spec_scenario.md](spec_scenario.md).
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
- **Setup may create the output folder.** Check that folder after `pre` when setup commands exist. Check ffmpeg, fonts, and frame size before `pre` to avoid failures after a long recording. Setup therefore cannot install ffmpeg or the only usable font.
- **Unix link targets need full resolution.** A target may contain further links and `..`. Unix applies `..` after resolving preceding links. This matters for macOS paths such as `/var`, which links to `/private/var`.
- **Link limits must cover the whole path.** A per-link depth limit allowed repeated traversal to double work at each level. A chain 22 links deep took 9 seconds in Linux under WSL. A shared limit of 40 links bounds the walk.
- **Windows identity checks need no read access.** Opening files for reading failed when another process denied read sharing. Opening with no access rights still permits identity checks. WSL shares report volume serial number `0` and use Linux inodes as indices. Files on different Linux filesystems can therefore have matching indices.
- **Device paths must retain their prefix.** Adding `\\?\` to a `\??\` path made it invalid. Identity lookup failed, and failed rendering deleted the input through that spelling. Device paths now remain unchanged.
- **Windows must resolve its own links.** Relative targets through junctions resolve against the opened path, rather than the junction target. Targets starting with a separator resolve against the current drive. Manual traversal therefore rejected valid outputs. File indices handle normal volumes. WSL shares need final paths from opened files because their serial number is `0`. Comparing only written paths missed Windows links to WSL inputs and allowed overwrites.
- **Help must describe command-specific behavior.** Previously, `--help` printed only usage lines on stderr, leaving option details in the README. Help now describes each option on stdout, which supports pagers. Scenario `--theme` retains YAML color overrides. Conversion `--theme` replaces cast colors, so the commands need different descriptions. A test checks help coverage against each parser's accepted options.
