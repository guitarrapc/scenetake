[![Build](https://github.com/guitarrapc/scenetake/actions/workflows/build.yaml/badge.svg)](https://github.com/guitarrapc/scenetake/actions/workflows/build.yaml)

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![NuGet](https://img.shields.io/nuget/v/scenetake.svg?label=scenetake%20nuget)](https://www.nuget.org/packages/scenetake)
[![npm](https://img.shields.io/npm/v/scenetake.svg?label=scenetake%20npm)](https://www.npmjs.com/package/scenetake)

# scenetake

English | [日本語](README-ja.md)

Run commands from a YAML **scenario** and record their output as [asciinema v3 `.cast`](https://docs.asciinema.org/manual/asciicast/v3/). Optionally generate an animated SVG, mp4, or GIF, or a PNG of one screen. No asciinema installation is required. Ordinary steps use simulated typing. Steps with `pty: true` record terminal output in real time.

The scenario in `samples/basic.yaml` runs real commands and generates the `.cast` and `.svg` recordings below. You do not need to type the commands during recording.

```yaml
title: "Basic Demo"
width: 80
height: 24
shell: bash

settings:
  prompt: "$ "
  typing-speed: 0.02 # average seconds per keystroke
  typing-jitter: 0.01 # random variance (±) per keystroke
  pre-delay: 0.4 # pause before typing starts
  post-delay: 1.0 # pause after output before next step

steps:
  - echo "Hello, World!"
  - name: Name of the step
    run: echo "Current directory:"
  - name: Coloring stdout
    run: curl -sI https://google.com
    highlight:
      - color: green
        at:
          - "1"
      - color: gray
        at:
          - "2-12"
  - echo "Wait for 2 seconds..."
  - run: sleep 2
    execution-duration: 1.0
  - echo "stderr output is red by default" 1>&2
  - echo "Done!"
```

| GIF | SVG |
| --- | --- |
| ![](samples/basic.gif) | ![](samples/basic.svg) |

SVG and mp4 output support a custom font, a color theme (below), and these settings for the window around the terminal. Each is a `render` key in the scenario and a flag on the command line:

| `render` key | Flag | What it sets |
| --- | --- | --- |
| `window` | `--window` | A macOS or Windows title bar (`macos` / `windows`) |
| `window-title` | `--window-title` | Text in the title bar. Default: the scenario's `title`. `""` shows none. |
| `background` | `--background` | What fills the space around the window: a color (`"#1e3c72"`), 2–4 colors for a gradient from the top-left corner (`"#1e3c72 #2a5298"`), or a `.png`, `.jpg`, `.gif`, or `.webp` file |
| `margin` | `--margin` | Pixels around the window, 0–200. Default 32 with a background. |
| `padding` | `--padding` | Pixels between the terminal's edge and its text, 0–100 |
| `opacity` | `--opacity` | How much of the terminal's background and title bar covers the background, 0–1. Default 1. Text stays opaque. |
| `blur` | `--blur` | Pixels of blur on the background seen through a window with `opacity` below 1, 0–50 |

Without `background`, only the window shows: the SVG's margin is transparent, and an mp4 is the window alone. A background helps when the output leaves the page it was made for:

- **Videos for X or Reddit.** An mp4 or a GIF with a background looks like a desktop screenshot instead of a cropped terminal. (A GIF made with agg has no window or background.)
- **Fixed-size video.** With `--size 1920x1080`, the background fills the 16:9 frame and the window sits in the middle, instead of padding in the terminal's color.
- **Pages whose color you do not know.** A GitHub README is light or dark depending on the reader. A background looks the same in both.

Demos inside README text usually need none: the page color fits, and the file stays small. An image background is embedded in every SVG, so prefer a WebP or JPEG about 1000–1600 pixels wide.

With an image, `opacity` lets it show through the window, as a terminal with a transparent background does, and `blur` softens what shows. Only the terminal's background and the title bar become translucent; text and cells the program colored stay opaque. Recommended `opacity` stay at 0.85 or more, while a dark one allows about 0.7.

How the demo ends is set the same way:

| `render` key | Flag | What it sets |
| --- | --- | --- |
| `loop` | `--no-loop` / `--loop` | `false` plays the SVG once and stops on the final screen. Default `true`. An mp4 has no loop: the player decides. |
| `end-hold` | `--end-hold` | Seconds the final screen stays before the SVG starts again or the mp4 ends, 0–60. Default: as long as the pause before the last output. |
| `fade-out` | `--fade-out` | Seconds over which the text fades out after `end-hold`, 0–10, so the loop starts again from a clear terminal. The window and the background stay. |

| macOS | Windows |
| --- | --- |
| ![](samples/theme-macos.svg) | ![](samples/theme-windows.svg) |

The color theme is a name in `render.theme.preset` or `--theme`. `fg`, `bg`, and `palette` under `render.theme` change single colors on top of it. Each theme uses the terminal colors its project publishes, and the two macOS themes use macOS's system colors, as Apple publishes no terminal palette:

| Dark background | |
| --- | --- |
| ![](samples/theme-presets/dark.svg) | ![](samples/theme-presets/macos-dark.svg) |
| ![](samples/theme-presets/dracula.svg) | ![](samples/theme-presets/nord.svg) |
| ![](samples/theme-presets/one-dark.svg) | ![](samples/theme-presets/gruvbox-dark.svg) |
| ![](samples/theme-presets/catppuccin-mocha.svg) | ![](samples/theme-presets/tokyo-night.svg) |
| ![](samples/theme-presets/solarized-dark.svg) | ![](samples/theme-presets/github-dark.svg) |
| **Light background** | |
| ![](samples/theme-presets/light.svg) | ![](samples/theme-presets/macos-light.svg) |
| ![](samples/theme-presets/solarized-light.svg) | ![](samples/theme-presets/github-light.svg) |

## Gallery

Each recording below is a scenario in [`samples/showcase`](samples/showcase), run on Linux. [`record.sh`](samples/showcase/record.sh) records them in a container that has the tools ([`Dockerfile`](samples/showcase/Dockerfile)).

| [fastfetch](samples/showcase/fetch.yaml) | [bat, git, and jq](samples/showcase/devtools.yaml) |
| --- | --- |
| ![](samples/showcase/fetch.svg) | ![](samples/showcase/devtools.svg) |
| [**figlet, lolcat, and toilet**](samples/showcase/logo.yaml) | [**sl**](samples/showcase/sl.yaml) |
| ![](samples/showcase/logo.svg) | ![](samples/showcase/sl.svg) |
| [**vim and git diff, typed by `keys`, on an image background**](samples/showcase/vim.yaml) | |
| ![](samples/showcase/vim.svg) | |

[nyancat](samples/showcase/nyancat.yaml), 96 × 26 cells of truecolor animation, stopped by `timeout`:

![](samples/showcase/nyancat.svg)

## Motivation

I wanted to record real commands from a scenario without typing each command by hand. Other tools depend on shell scripts or asciinema, expose execution paths, or simulate output without running commands.

scenetake runs on multiple platforms and writes `.cast` files directly, with optional SVG output.

1. Write the commands in a YAML scenario.
2. Use ordinary steps for simulated typing or `pty: true` for terminal output with real timing.
3. Run the scenario to execute the commands and record their output.

## Quick Start

Download the asset for your OS from GitHub Releases. Place `scenetake` (`scenetake.exe` on Windows) in your preferred folder. The binaries require neither a .NET runtime nor ICU.

| Asset | Runs on |
|---|---|
| `scenetake-linux-amd64.tar.gz`, `scenetake-linux-arm64.tar.gz` | Linux with glibc 2.28+ (RHEL 8, Debian 10, Ubuntu 20.04, and later) |
| `scenetake-linux-musl-amd64.tar.gz`, `scenetake-linux-musl-arm64.tar.gz` | Linux with musl 1.2.3+ (Alpine 3.17 and later) |
| `scenetake-osx-amd64.tar.gz`, `scenetake-osx-arm64.tar.gz` | macOS 12.0+ |
| `scenetake-win-amd64.zip`, `scenetake-win-arm64.zip` | Windows 10 1809+ |

```sh
# .NET global tool (.NET 10 SDK required; .NET 10 runtime required to run)
dotnet tool install -g scenetake

# npm
npm install -g scenetake

# Windows (Scoop)
scoop bucket add guitarrapc https://github.com/guitarrapc/scoop-bucket
scoop install scenetake
```

![](samples/quickstart.svg)

```bash
# On macOS/Linux, add execute permission if needed.
chmod +x ./scenetake

# Create a starter scenario file in the current directory
scenetake init

# Write the starter steps for bash instead of the OS default shell (pwsh on Windows, $SHELL elsewhere)
scenetake init --shell bash

# Run the scenario to generate a cast file
scenetake scenario.yaml

# Generate cast and animated SVG in one command
scenetake --format svg scenario.yaml

# Record one command without a scenario file: writes git.svg and git.cast here (run in a PTY, so colors show)
scenetake capture -- git log --oneline -5
scenetake capture --window macos -o help -- mycli --help

# SVG styling: font, theme, window chrome, frame cap
scenetake --format svg scenario.yaml --font-size 20 --font-family "'Noto Sans Mono', ui-monospace" --theme light --window macos

# A titled window on a gradient, with more room around the text
scenetake --format svg scenario.yaml --window macos --window-title "my-cli demo" --background "#1e3c72 #2a5298" --padding 16

# A translucent window over an image, with the image blurred behind the text
scenetake --format svg scenario.yaml --window macos --background wallpaper.webp --opacity 0.85 --blur 8

# Rows below the content are left out by default (render.fit: height). Fit the width too, keep the whole
# terminal, or cut rows from an edge (render.crop-*)
scenetake --format svg scenario.yaml --fit both
scenetake --format svg scenario.yaml --fit none
scenetake png scenario.cast --crop-top 1

# Keep the final screen for 3 seconds, then fade the text out over 1 second before the loop starts again
scenetake --format svg scenario.yaml --end-hold 3 --fade-out 1

# Convert an existing cast file to SVG (v2 or v3)
scenetake svg scenario.cast

# Same styling flags work on the svg subcommand
scenetake svg scenario.cast --theme light --window windows --max-fps 30

# One screen as a PNG, without ffmpeg: the final screen, or the one shown 2.5 seconds in
scenetake png scenario.cast
scenetake png scenario.cast --at 2.5 --window macos

# Cast and PNG in one command
scenetake --format png scenario.yaml

# A still SVG of the final screen, without animation
scenetake svg scenario.cast --at end

# Convert a cast file to mp4 (H.264, needs ffmpeg) — best for full-screen TUIs and sharing
scenetake mp4 scenario.cast

# Fit X (Twitter) video upload limits
scenetake mp4 scenario.cast --max-size 1280x1024

# Make the mp4 exactly 1920x1080, for video editors and joining clips
scenetake mp4 scenario.cast --size 1920x1080

# Generate cast and mp4 in one command
scenetake --format mp4 scenario.yaml

# Convert a cast file to an animated GIF (needs ffmpeg, any build) — for X, Reddit, and Zenn
scenetake gif scenario.cast

# A smaller GIF for Zenn's 3MB limit: a smaller font keeps text sharp, and fewer frames help full-screen TUIs
scenetake gif scenario.cast --font-size 12 --fps 10

# Generate cast and GIF in one command
scenetake --format gif scenario.yaml

# Show normal pre/post execution logs while generating a cast file
scenetake --verbose scenario.yaml

# Play with asciinema
asciinema play scenario.cast

# Or convert to gif with agg (Linux/macOS) — requires agg 1.6.0+ for v3 cast files
docker run --rm -v "${PWD}:/data" ghcr.io/asciinema/agg /data/scenario.cast /data/scenario.gif --font-size 20 --last-frame-duration 0

# Or convert to gif with agg (Windows PowerShell)
# Write the GIF inside the container and copy it out: Docker Desktop on Windows can corrupt a GIF written straight to a bind mount
$id = docker create -v "$($PWD.Path):/data:ro" ghcr.io/asciinema/agg /data/scenario.cast /tmp/scenario.gif --font-size 20 --last-frame-duration 0
docker start -a $id
docker cp "${id}:/tmp/scenario.gif" scenario.gif
docker rm $id
```

**Usage**

`scenetake --help` lists all commands and options, including ranges and defaults. Use `scenetake svg --help`, `scenetake mp4 --help`, `scenetake png --help`, `scenetake gif --help`, or `scenetake init --help` for command-specific options.

```bash
# Initialize a new scenario file
scenetake init [--shell bash|pwsh|powershell] [scenario.yaml]

# Run scenario to generate cast (default), cast + SVG, cast + mp4, cast + PNG, or cast + GIF
scenetake [--verbose] [--strict] [--format cast|svg|mp4|png|gif] [--save-keys FILE] [--at TIME]
  [--save-outputs FILE] [--replay FILE]
  [--font-size N] [--font-family FAMILIES] [--theme NAME]
  [--window none|macos|windows] [--max-fps N]
  [--fps N] [--crf N] [--max-size WxH | --size WxH] [--font-file PATH]
  scenario.yaml [output]

# Convert an existing cast file to SVG (--at TIME makes a still SVG of one screen)
scenetake svg [--at TIME] [--font-size N] [--font-family FAMILIES] [--theme NAME]
  [--window none|macos|windows] [--max-fps N]
  <input.cast> [output.svg]

# Convert an existing cast file to mp4
scenetake mp4 [--font-size N] [--font-family FAMILIES] [--font-file PATH] [--theme NAME]
  [--window none|macos|windows] [--fps N] [--crf N] [--max-size WxH | --size WxH]
  <input.cast> [output.mp4]

# Draw one screen of an existing cast file as a PNG (the final screen, or the one at --at TIME)
scenetake png [--at TIME] [--font-size N] [--font-family FAMILIES] [--font-file PATH] [--theme NAME]
  [--window none|macos|windows]
  <input.cast> [output.png]

# Convert an existing cast file to an animated GIF (needs ffmpeg)
scenetake gif [--fps N] [--max-size WxH | --size WxH] [--no-loop] [--font-size N] [--font-family FAMILIES]
  [--font-file PATH] [--theme NAME] [--window none|macos|windows]
  <input.cast> [output.gif]
```

`--max-fps` caps SVG animation frame rate (`0` = off, use full cast timing). It applies when `--format svg` is set or on the `svg` subcommand.

`--strict` is for CI. Unless a step sets `continue-on-error: true`, scenetake stops at the first recorded step that exits non-zero. Later steps do not run, and scenetake writes no output except the `--save-keys` file. Existing output files stay, and `post` runs. Scenetake exits `1` unless a `post` command fails, in which case it returns that command's exit code.

The error shows the last lines the step printed. Set `continue-on-error: true` on a step that is meant to fail, such as an error demo.

mp4 options apply with `--format mp4` or the `mp4` subcommand:

- `--fps` sets the frame rate (default `30`, maximum `60`).
- `--crf` sets quality (default `23`). Higher values produce smaller files.
- `--max-size` reduces the video to fit a limit, such as `1280x1024` for X.
- `--size` sets exact dimensions, such as `1920x1080`. See Terminal size and resolution below.
- `--font-file` selects a font file.

`--size` and `--max-size` cannot be used together.

**SVG or mp4**

SVG is usually smallest for typing and short output, often half the size of mp4. It embeds in a README and stays sharp at any zoom. Full-screen TUIs that redraw each frame (`matrix`, `btop`) produce large SVG and GIF files. Use mp4 for these recordings and for sharing on X or Slack.

For example, `matrix --fps 60` at 200×60 for 7.5 seconds produces 17.0MB of SVG or 4.4MB of mp4. With `--max-size 1280x1024`, the mp4 is 2.8MB. Scenetake warns when an SVG exceeds 10MB.

mp4 output runs [ffmpeg](https://ffmpeg.org/) with libx264 as a separate program. Install it with `winget install Gyan.FFmpeg` or `scoop install ffmpeg` (Windows), `brew install ffmpeg` (macOS), or `sudo apt install ffmpeg` (Debian/Ubuntu). Set `SCENETAKE_FFMPEG` to use a specific binary. Mp4 uses fonts installed on your machine: the `font-family` list first, then CJK and symbol fonts. Pass `--font-file` to pin one. `--window` adds the same title bar as SVG, without the outer margin and shadow.

**Terminal size and resolution**

`width` and `height` set the terminal size in columns and rows (`term.cols` and `term.rows` in a cast file). The output's pixel size follows from them. At the default `font-size` of 16:

| Output | Pixel width | Pixel height |
| --- | --- | --- |
| mp4 | `width × 10 + 20` | `height × 20 + 8` |
| mp4 with `--window` | `width × 10 + 20` | `height × 20 + 42` |
| SVG | `width × 9.92 + 36` | `height × 20 + 24` |
| SVG with `--window` | `width × 9.92 + 44` | `height × 20 + 66` |

Tips for picking a size:

- **README:** keep 80 columns. The SVG is about 830 pixels wide, which GitHub shows at its own size. At 120 columns it is shown at about two thirds, with 11-pixel text.
- **X, Reddit, and other 16:9 players:** 80 × 23 is 820×468 as mp4, close to 16:9, so the player shows little border.
- **A 1920×1080 video that people read:** keep the terminal small and raise `--font-size` instead of adding columns. 80 × 22 at `--font-size 36` is 1792×1006, and `--size 1920x1080` centers it in the frame. 190 × 53 at the default size also fills 1920×1080, but its text is as small as on a desktop screen, which is hard to read on a phone.

To fill a video resolution, use the largest `width` × `height` that fits. At `font-size` 16:

| Resolution | `width` × `height` → mp4 size | With `--window` |
| --- | --- | --- |
| 1280×720 (HD) | 126 × 35 → 1280×708 | 126 × 33 → 1280×702 |
| 1280×1024 (X upload limit) | 126 × 50 → 1280×1008 | 126 × 49 → 1280×1022 |
| 1920×1080 (Full HD) | 190 × 53 → 1920×1068 | 190 × 51 → 1920×1062 |
| 2560×1440 (WQHD) | 254 × 71 → 2560×1428 | 254 × 69 → 2560×1422 |
| 3840×2160 (4K) | 382 × 107 → 3840×2148 | 382 × 105 → 3840×2142 |

- The default `80×24` is 820×488 as mp4 and 829.6×504 as SVG. For a 1920×1080 video, raise `--font-size` instead of adding columns. 80 × 22 at `--font-size 36` is 1792×1006, almost centered.
- A row is 20 pixels tall, so the height usually ends a little short of the resolution. `--size` fills the rest (below).
- Text stays sharper when the recording already fits than when `--max-size` or `--size` scales a larger one down.
- A larger `font-size` makes each cell larger, so fewer columns and rows fit. A cell is `font-size × 0.62` wide and `font-size × 1.25` tall, rounded to whole pixels in mp4, and the padding also changes with `font-size`. See Layout in [spec_mp4.md](.github/docs/spec_mp4.md#layout).
- SVG does not blur when scaled, so its pixel size only sets the size it is first shown at.
- A recording that resizes uses its largest size.

`--size WxH` makes the mp4 exactly `W×H`. Both sides must be even. The recorded terminal does not change. Only the video frame around it does.

- A terminal that fits is centered, and the rest is the theme background. 190 × 53 at `--size 1920x1080` gets 6 pixels above and 6 below. The `--window` title bar stays at the top edge.
- A larger terminal is padded to that shape, then scaled down. 200 × 60 (2020×1208) becomes 1920×1080 with about 57 pixels on each side, its text scaled to 89%.
- A smaller terminal is not enlarged. 80 × 24 sits in the middle of the frame. Use a larger `--font-size` for larger text.

**Notes**

- `shell`:
  - Linux/macOS default shell is `$SHELL`, with `bash` as fallback.
  - Windows default shell is `pwsh`, with `powershell` as fallback. On Windows, `shell: bash` uses Git Bash / MSYS `bash` when available.
  - On Windows a path such as `C:/tools/pwsh-preview/pwsh.exe` starts that file. WSL `bash` is not used, even by path.
  - A shell given by name never starts a file from the current directory. Windows skips such a file. On Linux/macOS the run stops with an error, so run scenetake from another directory or give the shell as a full path.
- `cwd` is the working directory for `pre`, `steps`, and `post`. A relative `cwd` is relative to the scenario file's folder, so the scenario runs in the same folder wherever you start scenetake. Without `cwd`, commands run in the folder you start scenetake from. The folder must exist before the run starts.
- `settings` provides defaults for prompt and timing.
- `render` controls cast header display metadata (`st:font-size` tag, `term.theme`) and SVG output. See [.github/docs/spec_cast.md](.github/docs/spec_cast.md) and [.github/docs/spec_svg.md](.github/docs/spec_svg.md).
- `pre` / `post` run setup and teardown commands outside the recording flow. Their stdout/stderr are printed to the CLI, but are never written to the cast file.
- `steps`:
  - Steps are executed for real, so use caution with commands that modify files or affect external systems.
  - Commands that need a terminal can opt into pseudo-terminal capture with `pty: true`. See [PTY recording](#pty-recording).
  - For long-running commands, use `execution-duration` to keep playback readable.
  - For natural replay of long output (`docker build`, staged `sleep`, …), set `stream-output: true` and optionally `max-duration` on that step. Default pipe emit stays instant for typing and short demos.
  - Non-zero step exits warn and recording continues. `continue-on-error: true` suppresses the warning for expected failures. For CI, use `--strict` as described above.

**CJK text in GIFs**

The `ghcr.io/asciinema/agg` image has no CJK font. Characters absent from all available fonts appear as empty cells, including Japanese, Chinese, and Korean text.

Place a CJK font in a folder such as `fonts/`. Pass the folder with `--font-dir`. Add the font family after the Latin monospace font in `--text-font-family`. The first family sets the cell width.

```bash
# Linux/macOS
docker run --rm -v "${PWD}:/data" ghcr.io/asciinema/agg /data/scenario.cast /data/scenario.gif --font-size 20 --last-frame-duration 0 --font-dir /data/fonts --text-font-family "JetBrains Mono,BIZ UDGothic"

# Windows PowerShell
# Copy the font folder into the container: Docker Desktop on Windows fails to read a font from a bind mount (Input/output error)
$id = docker create -v "$($PWD.Path):/data:ro" ghcr.io/asciinema/agg /data/scenario.cast /tmp/scenario.gif --font-size 20 --last-frame-duration 0 --font-dir /fonts --text-font-family "JetBrains Mono,BIZ UDGothic"
docker cp .\fonts "${id}:/fonts"
docker start -a $id
docker cp "${id}:/tmp/scenario.gif" scenario.gif
docker rm $id
```

`BIZ UDGothic` (OFL, available on Google Fonts) is one example. Any CJK font works when you pass its family name.

## Steps as one session

With `shell: bash`, `zsh`, or PowerShell, the steps read like one terminal session. A `cd`, an `export`, a variable, a function, or an alias in one step holds in the steps after it, and in `post`. `pre` works the same way, so `source .venv/bin/activate` there carries into the recording. `sh` (dash, ash, ksh) carries `cd` and variables, but not functions or aliases. Other shells (fish, cmd) start each step afresh.

A few step keys shape the session:

```yaml
steps:
  - run: git clone https://github.com/me/app && cd app && npm install
    show: none              # Run without recording it: setup between scenes
  - run: npm run dev
    background: true        # Start it and go on while it runs: a server for the steps below
    wait: "Local:"          # The next step starts when its output shows this
  - curl -s localhost:5173/api/hello   # Runs in app, where the steps before left off
  - run: figlet "Deploy"
    show: output            # The output without the prompt and the typing
  - clear: true             # Clear the screen without typing a command
    name: "Scene 2"         # Optional comment line at the top
  - ./deploy.sh
```

- `show: none` runs the step without recording it, and takes no time in the recording. If it fails, the recording stops, as with `pre`. Set `continue-on-error: true` if it may fail.
- `background: true` starts the command and leaves it running without recording it. With `wait`, the next step starts once the command prints that text, waiting up to `wait-timeout` seconds (default 30). If it exits first or the text does not come, the recording stops. scenetake ends the command, with everything it started, after the last step and before `post`.
- `show: output` shows the output without the prompt and the typed command: a title, a banner, or a screen that is already open.
- `clear: true` clears the screen. It needs neither `TERM` nor a `clear` command, so it works the same in CI.

See [spec_scenario.md](.github/docs/spec_scenario.md#shell-state-across-steps) → Shell state across steps.

## Change the look without running again

Record once with `--save-outputs`, then tune the typing, comments, highlights, theme, or format with `--replay`. A replay runs no command, so a slow build, an external API, or output that changes every run stays as recorded.

```bash
scenetake --save-outputs demo.outputs.json demo.yaml                 # runs the commands once
scenetake --replay demo.outputs.json --format svg demo.yaml          # runs nothing
scenetake --replay demo.outputs.json --format mp4 --theme dracula demo.yaml
```

- With the YAML unchanged, a replay gives the recorded cast byte for byte.
- A step matches its recorded output by its `run` text. A step whose `run` changed, or a new one, stops the replay with a list of those steps: record again for them.
- `pre`, `post`, `show: none`, and `background` steps do not run in a replay.
- The outputs file hides secrets as the cast does, so it can be committed next to the YAML.

## Record again without noise

Recording again to the same file compares each step with the cast already there. A step that prints what it printed before keeps its earlier output and timing, so a scenario whose output did not change gives the same cast and the same SVG, though PTY output is timed by the clock. Only the steps whose output changed differ, and scenetake lists them:

```text
Compared with the earlier cast: 4 of 5 steps printed the same, 1 changed
  changed: step 3: npm test
```

A step whose output changes on every run, such as a date, a commit hash, or a random ID, can keep what it printed before:

```yaml
steps:
  - run: date
    keep-output: true       # Shows the earlier recording's output, and the command still runs
```

- The earlier recording is the cast at the output path. Commit it next to the SVG, so CI compares with it too. Delete it to record every step afresh.
- `keep-output` keeps the step's colors and timing too. Remove it for one recording to take the new output. A step that fails still warns, and stops the recording with `--strict`. A new `mask` still applies: output that holds a masked text is replaced by what the step prints now.
- A cast recorded in another terminal size is not compared, and neither is a recording that writes `--save-outputs`, which comes from that run alone. scenetake says so.
- A step is found by its `run` text. A step whose `run` changed is recorded afresh.
- A value can often be fixed at its source, which keeps the step current: `GIT_AUTHOR_DATE` and `GIT_COMMITTER_DATE` exported in a `show: none` step give the same commits the same hashes on every recording, and `TZ` fixes the time zone.

See [spec_scenario.md](.github/docs/spec_scenario.md#recording-again) → Recording again.

## Output modes

There are four output modes: ordinary output, stream output, TUI output, and interactive application output. You can switch mode per step.

### Ordinary output

Ordinary steps capture command stdout/stderr and record them with simulated typing. This mode is suitable for one-shot commands and commands that do not require a TTY. Even if output has no color, you can apply color with `highlight` and `stderr-color`. Output appears instantly regardless of how long the real command took to print.

stderr without ANSI colors is red by default. Tools also use stderr for ordinary messages, such as git branch changes, curl progress, and `docker build` output. Set `stderr-color: none` under `settings` or on a step to use the normal text color.

```yaml
steps:
  - name: comment color is cyan
    run: echo hello
  - name: "[yellow] comment color override"
    run: echo world
  - name: run & stdout color default
    run: date
  - name: run color override
    run: date
    run-highlight: "bold underline green"
```

| GIF | SVG |
| --- | --- |
| ![](samples/highlight.gif) | ![](samples/highlight.svg) |

### Stream output

Set `stream-output: true` to replay intermittent output at its recorded timing. `max-duration` limits the output timeline. Stream output can also compress long idle gaps.

```yaml
steps:
  - name: "stream-output (ping, staggered replay)"
    run: ping -n 4 127.0.0.1
    stream-output: true
```

| GIF | SVG |
| --- | --- |
| ![](samples/stream-output.gif) | ![](samples/stream-output.svg) |

### TUI output

To capture terminal UIs or real-time animation (`matrix`, progress bars, etc.), set `pty: true`. PTY mode records the terminal byte stream while the child process is running. PTY size comes from the scenario `width` / `height`.

```yaml
steps:
  - run: matrix --duration 5
    pty: true               # Capture TTY-dependent output in real time; enough when no interaction is needed
```

| GIF | SVG |
| --- | --- |
| ![](samples/pty.gif) | ![](samples/pty.svg) |

> [!WARNING] With `pty: true`, stdout and stderr are merged into one byte stream, so `stderr-color` and pipe-style `highlight` do not apply to that step. See [samples/pty.yaml](samples/pty.yaml) and [spec_pty.md](.github/docs/spec_pty.md).

scenetake runs every command with `COLORTERM=truecolor`, so tools that pick their color depth from the environment will use true color.

Recordings also draw DECDLD soft fonts, but a program behind the PTY cannot detect that. Tell it in the scenario, for example `matrix --mode movie --soft-font`.

A TUI that runs until a key ends it, or a demo that needs a menu choice, can be scripted with `keys`, so the recording needs no one at the keyboard. `timeout` stops a command that never exits: in a PTY it presses Ctrl+C first, so the program can restore the screen.

```yaml
steps:
  - run: vim greet.py
    pty: true
    timeout: 30             # Ctrl+C after 30 seconds
    keys:                   # Written to the PTY in order
      - wait: "greet.py"    # Wait until the output shows this text
        type: "/return\n"   # Text, one character per typing delay, a line break (\n) is Enter
      - sleep: 0.5          # Seconds to wait first
        type: "O# Say hello"
      - key: escape         # enter, tab, up, page-down, f1, ctrl+c, alt+x...
      - type: ":wq\n"
  - run: nyancat
    pty: true
    timeout: 5              # nyancat runs until it is stopped
```

| Kind | Key names (case-insensitive) |
| --- | --- |
| Named | `enter`, `tab`, `space`, `backspace`, `escape` (`esc`), `up`, `down`, `left`, `right`, `home`, `end`, `page-up`, `page-down`, `insert`, `delete`, `f1`–`f12` |
| Combination | `ctrl+` and one letter, such as `ctrl+c`; `alt+` and one ASCII character, such as `alt+x` |

`wait` looks for its text in what the program printed after the previous key, without colors or other escape sequences. Line breaks and runs of spaces count as one space. A program can take longer on another machine, so a `wait` keeps the keys in step where a `sleep` would guess. Without `timeout`, a wait gives up after 10 seconds with a warning.

See [samples/showcase/vim.yaml](samples/showcase/vim.yaml) and [spec_pty.md](.github/docs/spec_pty.md#keys-and-timeout) → Keys and timeout.

### Interactive application output

For interactive applications (`vim`, `copilot`), set `pty: true` and `interactive: true`. This lets you send input to the child process. Add `normalize-typing: true` to adjust typing speed while banners and help output retain their original timing.

```yaml
steps:
  - name: "Copilot CLI with banner"
    run: copilot --banner
    pty: true               # Capture TTY-dependent output in real time
    interactive: true       # For interactive applications
    normalize-typing: true  # Keep banner timing; re-time typed input only for readability

  - name: "edit in vim"
    run: vim README.md
    pty: true               # Capture TTY-dependent output in real time
    interactive: true       # For interactive applications
```

| GIF | SVG |
| --- | --- |
| ![](samples/interactive-pty.gif) | ![](samples/interactive-pty.svg) |

To record the session again without you, save the keys you type with `--save-keys`:

```bash
scenetake --save-keys keys.yaml demo.yaml
```

scenetake writes the keys of each interactive step to `keys.yaml` as `keys` entries: characters typed in a row become one `type`, other keys a `key`, and each pause a `sleep`. Replace `interactive: true` in the step with them, and later runs replay the session. Where a program takes a varying time, change a `sleep` to a `wait` for text it prints.

```yaml
  - run: "vim README.md"
    pty: true
    keys:
      - sleep: 1.2
        type: "Go"
      - sleep: 0.8
        type: "Edited with scenetake"
      - key: escape
      - sleep: 0.6
        type: ":wq\n"
```

> [!NOTE] On Unix, keys typed while a program reads a password are not saved, and a comment marks where they belong. Add the password there after a `wait` for its prompt, such as `wait: "Password:"`: typed earlier, it would reach the terminal before echo is off, and show. Windows cannot tell scenetake when a program reads a password, so on Windows the file holds every key, a password too.

> [!WARNING] With `pty: true`, stdout and stderr are merged into one byte stream, so `stderr-color` and pipe-style `highlight` do not apply to that step. See [samples/interactive-pty.yaml](samples/interactive-pty.yaml) and Interactive PTY in [spec_pty.md](.github/docs/spec_pty.md).

## Scenario Format

Only `steps` is required. All other top-level keys are optional. Unknown keys, such as `hightlight`, produce a warning with a suggested key when available.

The example below runs as a scenario. Its source is [samples/scenario_format.yaml](samples/scenario_format.yaml).

- Field definitions: [spec_scenario.md](.github/docs/spec_scenario.md).
- Color and style syntax: [spec_highlight.md](.github/docs/spec_highlight.md).
- Setup and cleanup: [spec_pre_post.md](.github/docs/spec_pre_post.md).
- More color examples: [samples/highlight.yaml](samples/highlight.yaml).

![](samples/scenario_format.svg)

```yaml
# Scenario format reference — runnable demo (samples/scenario_format.yaml).
# Required: steps only. Everything else is optional.

title: "Scenario Format Demo"  # Optional cast title
width: 80                        # Default: 80
height: 24                       # Default: 24
# cwd: /your/project             # Optional working directory for all commands
shell: bash                      # bash | pwsh | powershell | path. Windows: Git Bash when bash

render:                          # Optional display metadata for cast header and SVG
  font-size: 16
  font-family: ui-monospace, monospace
  theme:
    preset: dark                 # dark, light, nord, dracula, and more, with optional fg / bg / palette overrides
  window: macos                  # none | macos | windows
  window-title: "scenario_format.yaml"  # title bar text; default: the top-level title, "" for none
  background: "#0f2027 #2c5364"  # one color, 2-4 colors for a gradient, or a .png/.jpg/.gif/.webp file
  margin: 24                     # px around the window (default 8, 12 with a window, 32 with a background)
  padding: 12                    # px between the terminal edge and its text (default from font-size)

settings:                        # Defaults for all steps; map-form steps can override per key
  prompt: "$ "
  typing-speed: 0.04             # Average seconds per typed character
  typing-jitter: 0.01            # Random variance (+/- seconds) per character
  pre-delay: 0.5                 # Pause before typing each step
  post-delay: 1.0                # Pause after output before the next step
  execution-duration: 0.1        # Cast wait after command execution
  stderr-color: red              # When stderr has no ANSI SGR sequences
  # stream-output: false         # When true, replay captured chunk times in the cast
  # max-duration: 15             # Cap step output timeline, and also enables stream emit
  # silence-threshold: 2.0       # Stream mode: collapse idle gaps longer than this (seconds)
  # max-idle: 0.2                # Stream mode: collapsed idle gap duration (seconds)
  # mask:                        # Texts to hide wherever the recording shows them, as *
  #   - "internal.example.com"
  # mask-auto: true              # Also hide tokens, secret KEY=value values, URL passwords, secret env values

# Optional setup; runs before steps, not recorded in cast
pre:
  - echo "environment check (not recorded)"

steps:
  # String form - uses settings defaults
  - echo "Hello, World!"

  # Map form - comment line, per-step timing
  - name: "[cyan]Project status"
    run: printf 'name\tversion\nscenetake\t1.0\n'
    post-delay: 1.5

  - name: "Per-step timing overrides"
    run: printf 'typing slowly...\n'
    typing-speed: 0.12
    pre-delay: 0.8
    post-delay: 1.2

  - name: "execution-duration"
    run: sleep 1
    execution-duration: 0.5

  - name: "instant emit (ping, one blob)"
    run: bash -c 'case "$(uname -s)" in MINGW*|MSYS*|CYGWIN*) ping -n 4 127.0.0.1 ;; *) ping -c 4 127.0.0.1 ;; esac'
    execution-duration: 0.2

  - name: "stream-output (ping, staggered replay)"
    run: bash -c 'case "$(uname -s)" in MINGW*|MSYS*|CYGWIN*) ping -n 4 127.0.0.1 ;; *) ping -c 4 127.0.0.1 ;; esac'
    stream-output: true
    silence-threshold: 2.0
    max-idle: 0.2

  - name: "pty demo"
    run: echo "This command runs in a PTY, so output appears immediately and can include interactive features like colors and progress bars."
    pty: true

  - name: "keys and timeout"
    run: read -r -p "Your name? " name; echo "Hello, $name"
    pty: true                    # keys need a PTY to type into
    timeout: 10                  # seconds; then Ctrl+C, as a person would press it
    keys:                        # sleep (seconds), then type (text) or key (enter, up, esc, ctrl+c, f1...)
      - sleep: 0.5
        type: "scenetake"
      - key: enter

  - name: "run-highlight colors the typed command"
    run: printf 'done\n'
    run-highlight: bright-cyan

  - name: "stdout highlight"
    run: printf 'line1 alpha\nline2 beta gamma\nline3\n'
    highlight:
      - color: yellow
        at: "1"
      - color: red
        at: "2:7-10"
      - color: bright-cyan
        at: "2:12-"
      - color: underline fg:bright-white bg:blue
        at: "3"

  - name: "stderr (default red)"
    run: echo "plain stderr" 1>&2

  - name: "stderr-color override"
    run: echo "bright yellow stderr" 1>&2
    stderr-color: bright-yellow

  - echo "Done!"

# Optional teardown; runs last, not recorded
post:
  - echo "demo complete (not recorded)"
```

### Masking secrets

A recording is made to be shared, so scenetake hides secrets before it writes anything: the cast, SVG, mp4, PNG, and GIF never hold them. Each hidden character becomes `*`, so lines keep their width and a full-screen program's screen stays in place.

```yaml
settings:
  mask:
    - "internal.example.com"   # hidden wherever it appears
  mask-auto: true              # the default
```

- `mask` lists texts to hide, such as an internal host name.
- `mask-auto` (on by default) also hides known token formats (`ghp_…`, `github_pat_…`, `AKIA…`, `sk-…`, `xoxb-…`, `npm_…`, and more), the values of pairs whose key names a secret (`API_KEY=…`, `"password": "…"`, `Authorization: Bearer …`), passwords in URLs, JSON Web Tokens, private key blocks, and the values of secret environment variables such as `GITHUB_TOKEN` wherever a command prints them.
- scenetake prints what it hid, such as `Masked 2 secrets: 1 GitHub token, 1 key=value secret`. `mask-auto: false` hides only the listed texts.

## Development

Use `dotnet` for local development, debugging, or publishing.

- .NET 10 SDK (file-based C# app)

```bash
# Local run
dotnet run scenetake.cs -- <scenario.yaml> [output.cast]
dotnet run scenetake.cs -- svg <scenario.cast> [output.svg]

# build
dotnet publish scenetake.cs --self-contained true -p:PublishAot=true -p:StripSymbols=true -p:DebugType=None
```

See [samples/README.md](samples/README.md) for each scenario’s purpose and preview (GIF / SVG).

Regenerate samples cast/svg/gif files (the GIFs use `scenetake gif`, which needs ffmpeg):

```bash
dotnet run samples/regenerate.cs

# Only the GIFs, from the casts as they are
dotnet run samples/regenerate.cs -- --gifs
```

### Release

Before tagging, update `Directory.Build.props` and all npm packages under `npm/packages/` to the same version. The script calculates the version from the latest `v*` git tag. If no tag exists, it uses the current files.

```bash
dotnet run scripts/set_version.cs -- patch   # e.g. v1.3.0 -> 1.3.1
dotnet run scripts/set_version.cs -- minor   # e.g. v1.3.0 -> 1.4.0
dotnet run scripts/set_version.cs -- major   # e.g. v1.3.0 -> 2.0.0
```

Commit the version change. Create a matching tag, such as `v1.4.0`. Push the tag to start the release workflow. The workflow builds native binaries, creates a GitHub Release, and stages `scenetake` and `@scenetake/*` npm packages.

After the workflow finishes, approve the staged npm packages locally with 2FA:

```bash
npm stage list
npm stage approve <stage-id>
```

Staged publishing requires an existing package, so each npm package needs one manual `npm publish` for its first release. This also applies to platform packages added later, such as `@scenetake/linux-x64-musl` and `@scenetake/linux-arm64-musl`. The workflow packs such a package instead of staging it and uploads the tarball as the `npm-first-publish` artifact. The run summary lists the commands:

```bash
gh run download <run-id> --repo guitarrapc/scenetake --name npm-first-publish --dir npm-first-publish
npm publish --access public npm-first-publish/scenetake-linux-x64-musl-<version>.tgz
```

Then set up trusted publishing for the new package on npmjs.com, so that the next release can stage it.

Approve or publish platform packages before the main `scenetake` package.
