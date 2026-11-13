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
| `scenetake init [--shell bash\|pwsh\|powershell] [scenario.yaml]` | Create a commented starter scenario file |
| `scenetake --help` | Print every command and option, with its range and default |
| `scenetake --version` | Print version |

```bash
scenetake [--verbose] [--strict] [--format cast|svg|mp4] [--font-size N] [--font-family FAMILIES] [--theme dark|light] [--window none|macos|windows] [--max-fps N] [--fps N] [--crf N] [--max-size WxH | --size WxH] [--font-file PATH] <scenario.yaml> [output]
scenetake svg [--font-size N] [--font-family FAMILIES] [--theme dark|light] [--window none|macos|windows] [--max-fps N] <input.cast> [output]
scenetake mp4 [--font-size N] [--font-family FAMILIES] [--font-file PATH] [--theme dark|light] [--window none|macos|windows] [--fps N] [--crf N] [--max-size WxH | --size WxH] <input.cast> [output]
scenetake init [--shell bash|pwsh|powershell] [scenario.yaml]
```

`--help` (or `-h`) describes each option, its range, and its default on stdout, then exits `0`. `svg --help`, `mp4 --help`, and `init --help` list command-specific options. Scenario help explains overrides of YAML `render` settings. Conversion help explains overrides of cast metadata. After an error, stderr shows short usage text and a reference to the command's `--help`. Help and error messages use ASCII only.

## Parsing

- Options may appear in any order before positional arguments on the scenario, `svg`, and `mp4` paths.
- Unknown `-` / `--` options are explicit errors (avoids typos becoming path-not-found errors).
- `--format`, `--font-size`, `--font-family`, `--theme`, `--window`, `--max-fps`, `--fps`, `--crf`, `--max-size`, `--size`, and `--font-file` accept `--name=value` or `--name value`.
- Duplicate `--font-size`, `--font-family`, `--theme`, `--window`, `--max-fps`, `--fps`, `--crf`, `--max-size`, `--size`, or `--font-file` is an error. `--size` with `--max-size` is an error too: `--size` already sets the output size.
- Options for the other output format are errors, not ignored:
  - On the scenario path, `--fps`, `--crf`, `--max-size`, `--size`, and `--font-file` without `--format mp4` fail with `<option> requires --format mp4`. The `svg` subcommand reports them as unknown options.
  - `--max-fps` with mp4 output fails with `--max-fps applies to SVG; use --fps for mp4`.
  - `--max-fps` with cast output (no `--format svg`) fails with `--max-fps requires --format svg`.
- For `--fps`, `--crf`, `--max-size`, `--size`, and `--font-file`, a next argument that starts with `--` is not taken as the value: `--font-file --fps 30` fails with `--font-file requires a value`.
- Range errors use plain ASCII ("between 1 and 60"), so every console code page shows them.

## Options

| Option | Commands | Behavior |
|---|---|---|
| `--verbose` | scenario path only | Show successful `pre`/`post` labels and phase markers, and the exception type and stack when an error stops the recording. See [spec_pre_post.md](spec_pre_post.md). |
| `--strict` | scenario path only | For CI. Stop at the first recorded step that exits non-zero without `continue-on-error: true`. Skip later steps, preserve existing output files, and write no cast, SVG or mp4. Run `post`. Exit `1`. If a `post` command fails, return that command's exit code. The `svg` and `mp4` subcommands report it as an unknown option. See [spec_scenario.md](spec_scenario.md) → Step exit codes. |
| `--format cast\|svg\|mp4` | scenario path only | Default `cast`. `svg` also writes `.svg`, and `mp4` also writes `.mp4`. See [spec_svg.md](spec_svg.md) and [spec_mp4.md](spec_mp4.md). |
| `--font-size N` | scenario, `svg` | `1`–`128`. Scenario path: overrides `render.font-size` in the written cast header and SVG. `svg`: render-only override (CLI > cast header > default `16`). See [spec_cast.md](spec_cast.md). |
| `--font-family FAMILIES` | scenario, `svg` | CSS `font-family` string (`1`–`16` families, `256` characters max). Scenario path: overrides `render.font-family` in the written cast header and SVG. `svg`: render-only override (CLI > cast header > default stack). See [spec_cast.md](spec_cast.md) and [spec_svg.md](spec_svg.md). |
| `--theme dark\|light` | scenario, `svg` | Scenario path: overrides `render.theme.preset`. YAML `fg` / `bg` / `palette` still merge. Cast header stores resolved hex. `svg`: render-only override (CLI > cast header > default `dark`). See [spec_cast.md](spec_cast.md). |
| `--window none\|macos\|windows` | scenario, `svg` | Scenario path: overrides `render.window` in the written cast header (and in the SVG when `--format svg`). `svg`: render-only override (CLI > cast header > default `none`). See [spec_cast.md](spec_cast.md) and [spec_svg.md](spec_svg.md). |
| `--max-fps N` | scenario (`--format svg` only), `svg` | Optional frame sampling cap for SVG animation. `0` (default) = off (full cast timing). `1`–`120` enables sampling. See [spec_svg.md](spec_svg.md). |
| `--fps N` | scenario (`--format mp4`), `mp4` | Constant mp4 frame rate, `1`–`60`. Default `30`. |
| `--crf N` | scenario (`--format mp4`), `mp4` | H.264 quality, `1`–`51`. Default `23`. Higher values make smaller files. `0` (lossless) is not accepted, because x264 then writes the High 4:4:4 Predictive profile, which X and many players reject. |
| `--max-size WxH` | scenario (`--format mp4`), `mp4` | Downscale to fit within `W×H` (each side `16`–`16384`), keeping the aspect ratio. Never upscales. `1280x1024` fits X video uploads. |
| `--size WxH` | scenario (`--format mp4`), `mp4` | Make the video exactly `W×H` (each side `16`–`16384` and even): pad with the theme background, and scale down a larger terminal. Never upscales. Render only: the terminal's columns and rows are unchanged. Not with `--max-size`. See [spec_mp4.md](spec_mp4.md) → Exact size. |
| `--font-file PATH` | scenario (`--format mp4`), `mp4` | Primary font file (TTF, OTF, TTC) for mp4, before the `font-family` lookup. See [spec_mp4.md](spec_mp4.md). |

`--format mp4` and the `mp4` subcommand also accept `--font-size`, `--font-family`, `--theme`, and `--window` with the same priority as SVG (CLI > cast header > default). `--window` draws the title bar and buttons without the SVG margin and shadow (see [spec_mp4.md](spec_mp4.md) → Window chrome).

**Environment.** `SCENETAKE_FFMPEG` names the ffmpeg binary for mp4 output. Otherwise `ffmpeg` on `PATH` is used.

`init` accepts `--shell bash|pwsh|powershell` (`--shell value` or `--shell=value`, once) and an optional output path positional. No other flags. Another shell value, a missing value, or a second `--shell` is an error that writes nothing.

## Output Paths

**Scenario path.** `[output]` sets a shared stem. Extensions differ (`.cast`, plus `.svg` with `--format svg` or `.mp4` with `--format mp4`). With no `[output]`, stem matches the scenario path without extension. Passing `out.cast`, `out.svg`, or `out.mp4` yields the same stem.

**`svg` and `mp4` subcommands.** With no `[output]`, writes alongside the input as `<stem>.svg` or `<stem>.mp4`. With `[output]`, uses that path's stem and directory (extension optional).

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
