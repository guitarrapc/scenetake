# CLI Specification

Status: **Implemented**

## Motivation

scenetake exposes a small command-line surface: record scenarios to cast (and optionally SVG or mp4), convert existing casts to SVG or mp4, and scaffold starter YAML. Centralizing CLI behavior here keeps feature specs focused on runtime semantics.

## Commands

| Command | Purpose |
|---|---|
| `scenetake <scenario.yaml> [output]` | Run a scenario; write `.cast` (default), `.cast` + `.svg`, or `.cast` + `.mp4` |
| `scenetake svg <input.cast> [output]` | Convert an existing cast to SVG |
| `scenetake mp4 <input.cast> [output]` | Convert an existing cast to mp4 (needs ffmpeg; see [spec_mp4.md](spec_mp4.md)) |
| `scenetake init [scenario.yaml]` | Create a commented starter scenario file |
| `scenetake --help` | Print usage |
| `scenetake --version` | Print version |

```bash
scenetake [--verbose] [--format cast|svg|mp4] [--font-size N] [--font-family FAMILIES] [--theme dark|light] [--window none|macos|windows] [--max-fps N] [--fps N] [--crf N] [--max-size WxH] [--font-file PATH] <scenario.yaml> [output]
scenetake svg [--font-size N] [--font-family FAMILIES] [--theme dark|light] [--window none|macos|windows] [--max-fps N] <input.cast> [output]
scenetake mp4 [--font-size N] [--font-family FAMILIES] [--font-file PATH] [--theme dark|light] [--window none|macos|windows] [--fps N] [--crf N] [--max-size WxH] <input.cast> [output]
scenetake init [scenario.yaml]
```

Subcommands accept `-h` / `--help` for subcommand-specific usage.

## Parsing

- Options may appear in any order before positional arguments on the scenario, `svg`, and `mp4` paths.
- Unknown `-` / `--` options are explicit errors (avoids typos becoming path-not-found errors).
- `--format`, `--font-size`, `--font-family`, `--theme`, `--window`, `--max-fps`, `--fps`, `--crf`, `--max-size`, and `--font-file` accept `--name=value` or `--name value`.
- Duplicate `--font-size`, `--font-family`, `--theme`, `--window`, `--max-fps`, `--fps`, `--crf`, `--max-size`, or `--font-file` is an error.
- Options for the other output format are errors, not ignored:
  - On the scenario path, `--fps`, `--crf`, `--max-size`, and `--font-file` without `--format mp4` fail with `<option> requires --format mp4`. The `svg` subcommand reports them as unknown options.
  - `--max-fps` with mp4 output fails with `--max-fps applies to SVG; use --fps for mp4`.
  - `--max-fps` with cast output (no `--format svg`) fails with `--max-fps requires --format svg`.
- For `--fps`, `--crf`, `--max-size`, and `--font-file`, a next argument that starts with `--` is not taken as the value: `--font-file --fps 30` fails with `--font-file requires a value`.
- Range errors use plain ASCII ("between 1 and 60"), so every console code page shows them.

## Options

| Option | Commands | Behavior |
|---|---|---|
| `--verbose` | scenario path only | Show successful `pre`/`post` labels and phase markers, and the exception type and stack when an error stops the recording. See [spec_pre_post.md](spec_pre_post.md). |
| `--format cast\|svg\|mp4` | scenario path only | Default `cast`. `svg` also writes `.svg`, and `mp4` also writes `.mp4`. See [spec_svg.md](spec_svg.md) and [spec_mp4.md](spec_mp4.md). |
| `--font-size N` | scenario, `svg` | `1`–`128`. Scenario path: overrides `render.font-size` in the written cast header and SVG. `svg`: render-only override (CLI > cast header > default `16`). See [spec_cast.md](spec_cast.md). |
| `--font-family FAMILIES` | scenario, `svg` | CSS `font-family` string (`1`–`16` families, `256` characters max). Scenario path: overrides `render.font-family` in the written cast header and SVG. `svg`: render-only override (CLI > cast header > default stack). See [spec_cast.md](spec_cast.md) and [spec_svg.md](spec_svg.md). |
| `--theme dark\|light` | scenario, `svg` | Scenario path: overrides `render.theme.preset`; YAML `fg` / `bg` / `palette` still merge; cast header stores resolved hex. `svg`: render-only override (CLI > cast header > default `dark`). See [spec_cast.md](spec_cast.md). |
| `--window none\|macos\|windows` | scenario, `svg` | Scenario path: overrides `render.window` in the written cast header (and in the SVG when `--format svg`). `svg`: render-only override (CLI > cast header > default `none`). See [spec_cast.md](spec_cast.md) and [spec_svg.md](spec_svg.md). |
| `--max-fps N` | scenario (`--format svg` only), `svg` | Optional frame sampling cap for SVG animation. `0` (default) = off (full cast timing). `1`–`120` enables sampling. See [spec_svg.md](spec_svg.md). |
| `--fps N` | scenario (`--format mp4`), `mp4` | Constant mp4 frame rate, `1`–`60`. Default `30`. |
| `--crf N` | scenario (`--format mp4`), `mp4` | H.264 quality, `1`–`51`. Default `23`. Higher values make smaller files. `0` (lossless) is not accepted, because x264 then writes the High 4:4:4 Predictive profile, which X and many players reject. |
| `--max-size WxH` | scenario (`--format mp4`), `mp4` | Downscale to fit within `W×H` (each side `16`–`16384`), keeping the aspect ratio; never upscales. `1280x1024` fits X video uploads. |
| `--font-file PATH` | scenario (`--format mp4`), `mp4` | Primary font file (TTF, OTF, TTC) for mp4, before the `font-family` lookup. See [spec_mp4.md](spec_mp4.md). |

`--format mp4` and the `mp4` subcommand also accept `--font-size`, `--font-family`, `--theme`, and `--window` with the same priority as SVG (CLI > cast header > default). `--window` draws the title bar and buttons without the SVG margin and shadow (see [spec_mp4.md](spec_mp4.md) → Window chrome).

**Environment.** `SCENETAKE_FFMPEG` names the ffmpeg binary for mp4 output; otherwise `ffmpeg` on `PATH` is used.

`init` accepts only an optional output path positional; no other flags.

## Output Paths

**Scenario path.** `[output]` sets a shared stem; extensions differ (`.cast`, plus `.svg` with `--format svg` or `.mp4` with `--format mp4`). With no `[output]`, stem matches the scenario path without extension. Passing `out.cast`, `out.svg`, or `out.mp4` yields the same stem.

**`svg` and `mp4` subcommands.** With no `[output]`, writes alongside the input as `<stem>.svg` or `<stem>.mp4`. With `[output]`, uses that path's stem and directory (extension optional).

**Checks.** Every output is checked before anything runs, on the scenario path before `pre`:

- it must not be a directory;
- it must not be the input file. `scenetake mp4 clip.mp4` would otherwise name the input as its own output, and a failed render deletes its output. Paths are compared ignoring case, because a volume can ignore case on any OS. On Windows two existing files with the same volume serial number and file index are the same file, which catches symbolic links, junctions, hard links, mapped and subst drives, and `\\?\`, `\??\` and UNC spellings, also while another process holds the file open. On a volume that reports serial number 0 for every file, such as the WSL file share, the final paths Windows gives the two open files are compared instead, with the share's two server names (`wsl$` and `wsl.localhost`) counted as one. Two names in one folder with the same file index there, both reporting more than one link, are one file, a hard link. A hard link in another folder on such a volume is not detected. On Unix links on each path are followed, including links inside a link's target, so a symbolic link to the input counts as the input, and a `..` in a target is applied after the links before it. A hard link is not detected there.

The output folder must exist as well. When the scenario has `pre` commands, the folder is checked after `pre`, which may create it. Every other check runs again then, because `pre` may also put a directory, or a link to the input, where an output goes. A failure then stops the run before the recording, and `post` runs because `pre` succeeded. Without `pre` commands the folder is checked before anything runs.

## Logging

Progress and status go to stderr (`Loading:`, `Written:`, `Done:`). File paths in these messages are shown relative to the current working directory, using `/` separators; when a path cannot be relativized (for example, another drive on Windows), the full path is shown instead. Step `running:` lines are always shown on the scenario path.

**`pre` / `post`.** stdout and stderr are forwarded after each command exits (live streaming not required in v1). Failure prints phase, full command text, and exit code unconditionally. See [spec_pre_post.md](spec_pre_post.md).

**Warnings.** Non-fatal issues print `Warning: …` to stderr and execution continues. Feature-specific warn-and-continue rules: [spec_highlight.md](spec_highlight.md), [spec_svg.md](spec_svg.md), [spec_scenario.md](spec_scenario.md) → Step exit codes.

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

Semantics (recording continues, process exit code stays `0`): [spec_scenario.md](spec_scenario.md) → Step exit codes.

**Errors.** Fatal issues print `Error: …` to stderr and exit non-zero.

## Exit Codes

| Outcome | Code |
|---|---|
| Success | `0` |
| Parse / validation error, missing file, cast/SVG/mp4 write failure, ffmpeg missing or without libx264 | `1` (or command-specific for `pre`/`post`) |
| Failed `pre` or `post` command | Failed command's exit code |

Recorded step exit codes do not affect the process exit code. See [spec_scenario.md](spec_scenario.md) → Step exit codes.

## Init

- Default output path: `scenario.yaml` in the current directory.
- Fails if the target file already exists.
- Generated template follows [spec_scenario.md](spec_scenario.md).

## Cross-Document Notes

- [spec_scenario.md](spec_scenario.md) — scenario YAML structure and defaults.
- [spec_cast.md](spec_cast.md) — cast file format.
- [spec_svg.md](spec_svg.md) — SVG renderer, `svg` subcommand event handling.
- [spec_mp4.md](spec_mp4.md) — mp4 renderer, ffmpeg, `mp4` subcommand failure behavior.
- [spec_pre_post.md](spec_pre_post.md) — `pre`/`post` execution and failure behavior.
- [spec_highlight.md](spec_highlight.md) — coloring validation warnings.

## Lessons Learned

- Unknown options should fail explicitly; otherwise mistyped flags are misread as file paths.
- Default logs should stay focused on cast content; `--verbose` is the right place for successful `pre`/`post` labels.
- Options that only affect another output format should fail rather than be ignored, so `--fps` on an SVG run or `--max-fps` on an mp4 run is caught immediately.
- Requiring the output folder before `pre` broke scenarios whose `pre` creates it, so with `pre` commands the folder is checked after `pre`. ffmpeg, a usable font (the `--font-file`, or an installed family or fallback), and the frame size are still checked before `pre`, so a long recording does not fail at the end. So `pre` cannot be what installs ffmpeg or the only font.
- Following a link to its target is not enough on Unix. The target comes back as the link wrote it, may pass through another link, and may hold a `..` that Unix applies after the links before it. macOS temporary folders sit behind such a link (`/var` to `/private/var`), so the same-file check walks the target again part by part.
- One count of 40 links covers the whole walk. A depth limit per link let a crafted chain, whose links each pass through the one before twice, double the work at each level: 9 s for a chain 22 deep (Linux in WSL).
- On Windows, opening a file to read its file index fails while another process holds it without read sharing, and the check then fell back to the paths alone. It opens the file with no access rights, which sharing does not block. The WSL file share reports serial number 0 for every file and uses the Linux inode as the index, so two files on different Linux file systems looked like one.
- The `\\?\` prefix that lets the file index lookup take long paths was also put in front of a path in the `\??\` device form, which made it invalid. The lookup failed, and a failed render deleted the input through that spelling. Every device form is now used as it is.
- Windows links are not walked by hand. Windows resolves a relative link target against the path as it was opened, so through a junction it names another file than a walk from the junction's target does, and the walk refused outputs that were not the input. A target that starts with a separator, resolved against the current drive, did the same. The file index finds every link without a walk, except on the WSL file share, which reports serial number 0. Comparing only the paths as written there let a Windows link to a file on the share overwrite the input, so on such a volume the final paths Windows gives the two open files are compared.
