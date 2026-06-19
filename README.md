[![Build](https://github.com/guitarrapc/scenetake/actions/workflows/build.yaml/badge.svg)](https://github.com/guitarrapc/scenetake/actions/workflows/build.yaml)

# scenetake

English | [日本語](README-ja.md)

Run a YAML **scenario** and record a terminal scene take [asciinema v3 `.cast`](https://docs.asciinema.org/manual/asciicast/v3/), animated `.svg`. You do not need to install or launch `asciinema` to record. Write a YAML scenario with steps, and this tool executes those steps and records the output as `.cast` (and optionally `.svg`) with simulated typing plus real command output.

Sample scenario `samples/basic.yaml` generates `.cast` and `.svg` output like below. You don't have to struggle with typing, and since the commands are actually executed, you can easily create realistic terminal recordings.

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
    run: curl -I https://google.com
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

SVG output supports custom font, light/dark theme, and optional window chrome (`macos` / `windows`).

| macOS | Windows |
| --- | --- |
| ![](samples/theme-macos.svg) | ![](samples/theme-windows.svg) |

## What you can do

| I want to… | How |
|---|---|
| Record a terminal demo from a list of commands (no live typing) | Write a YAML scenario, then `scenetake scenario.yaml` |
| Get an animated SVG for a README or docs | `scenetake --format svg scenario.yaml` |
| Convert an existing asciinema `.cast` to SVG | `scenetake svg recording.cast` |
| Tune font, theme, or window chrome | Set `render` in YAML, or pass `--font-size`, `--font-family`, `--theme`, `--window` |
| Color output when CLI tools stay plain without a TTY | Use `highlight`, `run-highlight`, or `stderr-color` on steps — see [samples/highlight.yaml](samples/highlight.yaml) |
| Record interactive TUIs (`vim`, `htop`, full-screen apps) | Record with [asciinema](https://asciinema.org/), then `scenetake svg recording.cast` |
| Scaffold a starter scenario | `scenetake init` |
| Browse examples and previews | [samples/README.md](samples/README.md) |
| Read full behavior specs | [.github/docs/spec_index.md](.github/docs/spec_index.md) |

**Motivation**

I want terminal demos without the hassle of typing commands into asciinema. That is the motivation behind scenetake. There are various tools in the asciinema ecosystem, but none quite fit: some lean heavily on shell scripts, some require asciinema itself as a dependency, some leak execution paths into the cast output, and some only fake the output rather than running real commands. What I want is something where I can write a scenario plainly, have the listed commands actually executed, and get a cast file generated directly from the real output.

scenetake is a cross-platform tool that records those scenarios as `.cast` files, optionally with `.svg`, without going through asciinema at all.

1. Write the commands to run in the scenario.
2. Generate cast events as if the commands were typed at a steady pace.
3. Execute the commands for real and write their output into the cast.

## Quick Start

Download the asset for your OS from GitHub Releases, then place `scenetake` (or `scenetake.exe` on Windows) where you want.

![](samples/quickstart.svg)

```bash
# On macOS/Linux, add execute permission if needed.
chmod +x ./scenetake

# Create a starter scenario file in the current directory
scenetake init

# Run the scenario to generate a cast file
scenetake scenario.yaml

# Generate cast and animated SVG in one command
scenetake --format svg scenario.yaml

# SVG styling: font, theme, window chrome, frame cap
scenetake --format svg scenario.yaml --font-size 20 --font-family "'Noto Sans Mono', ui-monospace" --theme light --window macos

# Convert an existing cast file to SVG (v2 or v3)
scenetake svg scenario.cast

# Same styling flags work on the svg subcommand
scenetake svg scenario.cast --theme light --window windows --max-fps 30

# Show normal pre/post execution logs while generating a cast file
scenetake --verbose scenario.yaml

# Play with asciinema
asciinema play scenario.cast

# Convert to gif with agg (Linux/macOS) — requires agg 1.6.0+ for v3 cast files
docker run --rm -v "${PWD}:/data" ghcr.io/asciinema/agg /data/scenario.cast /data/scenario.gif --font-size 20 --last-frame-duration 0

# Convert to gif with agg (Windows PowerShell)
docker run --rm -v "$($PWD.Path):/data" ghcr.io/asciinema/agg /data/scenario.cast /data/scenario.gif --font-size 20 --last-frame-duration 0
```

**Usage**

```bash
# Initialize a new scenario file
scenetake init [scenario.yaml]

# Run scenario to generate cast (default) or cast + SVG
scenetake [--verbose] [--format cast|svg]
  [--font-size N] [--font-family FAMILIES] [--theme dark|light]
  [--window none|macos|windows] [--max-fps N]
  scenario.yaml [output]

# Convert an existing cast file to SVG
scenetake svg [--font-size N] [--font-family FAMILIES] [--theme dark|light]
  [--window none|macos|windows] [--max-fps N]
  <input.cast> [output.svg]
```

`--max-fps` caps SVG animation frame rate (`0` = off, use full cast timing). It applies when `--format svg` is set or on the `svg` subcommand.

**Notes**

- `shell`:
  - Linux/macOS default shell is `$SHELL`, with `bash` as fallback.
  - Windows default shell is `pwsh`, with `powershell` as fallback. On Windows, `shell: bash` uses Git Bash / MSYS `bash` when available.
- `settings` provides defaults for prompt and timing.
- `render` controls cast header display metadata (`st:font-size` tag, `term.theme`) and SVG output. See [.github/docs/spec_cast.md](.github/docs/spec_cast.md) and [.github/docs/spec_svg.md](.github/docs/spec_svg.md).
- `pre` / `post` run setup and teardown commands outside the recording flow. Their stdout/stderr are printed to the CLI, but are never written to the cast file.
- `steps`:
  - Steps are executed for real, so use caution with commands that modify files or affect external systems.
  - Interactive commands are not supported. See [Limitations (scenario recording)](#limitations-scenario-recording).
  - For long-running commands, use `execution-duration` to keep playback readable.

## Limitations (scenario recording)

Scenario recording runs commands without a PTY and captures each command's output in one batch after it finishes. It does not support interactive programs, real-time terminal animations, or full-screen TUIs (for example `vim`, `htop`, `sl`, or `copilot --banner`). For those, record with [asciinema](https://asciinema.org/) and convert with `scenetake svg` — the SVG renderer supports rich TUI output from external casts.

## Scenario Format

Only `steps` is required; all other top-level keys are optional. The example below is a runnable scenario — source of truth: [samples/scenario_format.yaml](samples/scenario_format.yaml). For full field definitions see [.github/docs/spec_scenario.md](.github/docs/spec_scenario.md). Coloring and style syntax: [spec_highlight.md](.github/docs/spec_highlight.md). Pre/post behavior: [spec_pre_post.md](.github/docs/spec_pre_post.md). More color examples: [samples/highlight.yaml](samples/highlight.yaml).

![](samples/scenario_format.svg)

```yaml
# Scenario format reference — runnable demo (samples/scenario_format.yaml).
# Required: steps only. Everything else is optional.

title: "Scenario Format Demo"  # Optional cast title
width: 80                        # Default: 120
height: 24                       # Default: 24
# cwd: /your/project             # Optional working directory for all commands
shell: bash                      # bash | pwsh | powershell | path. Windows: Git Bash when bash

render:                          # Optional display metadata for cast header and SVG
  font-size: 16
  font-family: ui-monospace, monospace
  theme:
    preset: dark                 # dark | light; optional fg / bg / palette overrides
  window: macos                  # none | macos | windows

settings:                        # Defaults for all steps; map-form steps can override per key
  prompt: "$ "
  typing-speed: 0.04             # Average seconds per typed character
  typing-jitter: 0.01            # Random variance (+/- seconds) per character
  pre-delay: 0.5                 # Pause before typing each step
  post-delay: 1.0                # Pause after output before the next step
  execution-duration: 0.1        # Cast wait after command execution
  stderr-color: red              # When stderr has no ANSI SGR sequences

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

# Optional teardown; runs after cast is written, not recorded
post:
  - echo "demo complete (not recorded)"
```

## Development

Use `dotnet` for local development, debugging, or publishing.

### Requirements

- .NET 10 SDK (file-based C# app)

```bash
# Local run
dotnet run scenetake.cs -- <scenario.yaml> [output.cast]
dotnet run scenetake.cs -- svg <scenario.cast> [output.svg]

# build
dotnet publish scenetake.cs --self-contained true -p:PublishAot=true -p:StripSymbols=true -p:DebugType=None
```

See [samples/README.md](samples/README.md) for each scenario’s purpose and preview (GIF / SVG).

Regenerate samples cast/svg files:

```bash
dotnet run samples/regenerate.cs
foreach ($file in Get-ChildItem samples/*.cast) {
  docker run --rm -v "$($PWD.Path):/data" ghcr.io/asciinema/agg /data/samples/$($file.BaseName).cast /data/samples/$($file.BaseName).gif  --last-frame-duration 0
}
```
