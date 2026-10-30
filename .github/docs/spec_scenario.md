# Scenario Format Specification

Status: **Implemented**

## Motivation

A scenario file is the single declarative input for scenetake: terminal dimensions, execution environment, recording timing, optional display metadata, setup/teardown, and the command sequence to record. Keeping the YAML structure in one spec lets feature specs focus on behavior.

## File Structure

Top-level keys use kebab-case. Recommended order for readability: metadata → `settings` → `render` → `pre` → `steps` → `post`. Key order does not affect behavior. Keys scenetake does not know, at any level, are ignored without a warning.

```yaml
title: "Demo"
width: 120
height: 24
cwd: /path/to/dir
shell: bash

settings:
  prompt: "$ "
  typing-speed: 0.05

render:
  font-size: 16
  theme:
    preset: dark

pre:
  - dotnet build

steps:
  - echo "Hello"
  - run: git status
    name: "[cyan] status"
    post-delay: 2.0
  - run: matrix 3
    pty: true

post:
  - git clean -fd
```

## Top-Level Metadata

| Key | Default | Description |
|---|---|---|
| `title` | — | Optional cast title |
| `width` | `120` | Terminal columns in the cast header and PTY geometry when a step uses `pty: true`. `1`–`512`, the cast's range; checked before `pre` runs. |
| `height` | `24` | Terminal rows in the cast header and PTY geometry when a step uses `pty: true`. `1`–`512`, checked before `pre` runs. |
| `cwd` | process cwd | Working directory for `pre`, `steps`, and `post` |
| `shell` | `$SHELL` / `pwsh` | Shell for all commands. Values: `bash`, `pwsh`, `powershell`, or a path |

## `settings`

Defaults for all steps. Map-form steps may override individual keys.

| Key | Default | Description |
|---|---|---|
| `prompt` | `$ ` | Prompt shown before each typed command |
| `typing-speed` | `0.05` | Average seconds per typed character |
| `typing-jitter` | `0.015` | Random variance (± seconds) per character |
| `pre-delay` | `0.2` | Pause before typing a step |
| `post-delay` | `0.5` | Pause after output before the next step |
| `execution-duration` | `0.05` | Minimum cast wait after Enter before step output appears (seconds). With default pipe emit, all output is written at `command_start + execution-duration`. |
| `stream-output` | `false` | When `true`, emit pipe/PTY output as timed chunks using wall-clock capture instead of a single instant blob |
| `max-duration` | — | When set, enables stream emit and linearly scales the step output timeline to this ceiling (seconds) |
| `silence-threshold` | `2.0` | Stream mode only: gaps longer than this between chunks are treated as idle (seconds) |
| `max-idle` | `0.2` | Stream mode only: collapsed idle gap duration (seconds) |
| `stderr-color` | `red` | Fallback color when stderr has no ANSI SGR. Value format: [spec_highlight.md](spec_highlight.md) |

### Output timing (pipe and PTY)

By default, pipe steps capture stdout/stderr in parallel during execution but write **one** cast `o` event at `command_start + execution-duration`. The event text is **stdout then stderr** (not chronologically interleaved), matching pre-stream-output merge order and keeping stdout `highlight` line numbers stable. This preserves typed-command playback and avoids choppy SVG/GIF output from read-boundary chunks or progress-bar streams.

**Stream emit** is enabled when a step or `settings` sets `stream-output: true` or `max-duration`. In stream mode, scenetake records chunk arrival times, then applies:

1. Startup offset (align slow first chunk with `execution-duration`; same rule as [spec_pty.md](spec_pty.md))
2. Burst merge (gaps under 50ms share a timestamp)
3. Idle collapse (`silence-threshold` / `max-idle`)
4. Linear compression when `max-duration` is set

PTY steps use the same stream pipeline only when stream emit is enabled **and** the step is not `interactive: true`; otherwise only the existing PTY startup offset applies. Interactive PTY steps use wall-clock chunk times with `PtyLeadingInitFilter` only, unless `normalize-typing: true` is set ([spec_pty.md](spec_pty.md) → Interactive PTY).

Stream emit suits long-running commands (for example `docker build`) where replay pacing matters. Short demos, nested tool stderr, and progress-bar tools (for example `agg`) should usually keep the default instant emit. See Lessons Learned below.


## `render`

Display metadata for the cast header. Written on every run. Header mapping: [spec_cast.md](spec_cast.md). SVG usage: [spec_svg.md](spec_svg.md).

| Key | Default | Description |
|---|---|---|
| `font-size` | `16` | Monospace font size (`1`–`128`) |
| `font-family` | built-in stack | CSS `font-family` value (`1`–`16` families, `256` characters max). If `monospace` / `ui-monospace` is absent, `monospace` is appended. |
| `theme.preset` | `dark` | Built-in preset: `dark` or `light` |
| `theme.fg` | from preset | Optional foreground override, `#rgb` or `#rrggbb` |
| `theme.bg` | from preset | Optional background override, `#rgb` or `#rrggbb` |
| `theme.palette` | from preset | Optional palette override: 16 colors, `#rgb` or `#rrggbb`, separated by `:` |
| `window` | omitted (`none`) | Optional window chrome: `macos` or `windows` |

`settings` controls recording; `render` controls presentation. CLI may override `font-size`, `font-family`, `window`, and theme preset. See [spec_cli.md](spec_cli.md).

## `pre` and `post`

Top-level string arrays of shell commands. Each item is one command string (block scalars allowed). Empty or whitespace-only entries are skipped.

Runtime behavior: [spec_pre_post.md](spec_pre_post.md).

## `steps`

Required. A sequence of commands to execute and record.

### Forms

| Form | Example | Notes |
|---|---|---|
| String | `- echo "hi"` | Uses `settings` defaults |
| Map | `- run: git status` | Allows per-step keys below |

Map-form steps recognize:

| Key | Required | Description |
|---|---|---|
| `run` | yes | Command string. A list or a mapping is an error found before anything runs |
| `name` | no | Comment line before typing. Optional `[style]` prefix: [spec_highlight.md](spec_highlight.md) |
| `typing-speed`, `typing-jitter`, `pre-delay`, `post-delay`, `execution-duration` | no | Override `settings` for this step |
| `stream-output`, `max-duration`, `silence-threshold`, `max-idle` | no | Override `settings` output-timing keys for this step |
| `run-highlight` | no | Style for typed command text |
| `highlight` | no | List of `{ color, at }` for command output |
| `stderr-color` | no | Override `settings.stderr-color` for this step. Ignored when `pty: true` |
| `pty` | no | default `false`. Pseudo-terminal capture for TUI commands. Recording behavior: [spec_pty.md](spec_pty.md) |
| `interactive` | no | default `false`. Requires `pty: true`. Human operates the host terminal until the child exits. Recording behavior: [spec_pty.md](spec_pty.md) → Interactive PTY |
| `normalize-typing` | no | default `false`. Requires `interactive: true`. During host-input windows, re-time visible typing at `typing-speed` and strip Backspace corrections; banner and other program output stay wall-clock ([spec_pty.md](spec_pty.md) → Interactive PTY) |

Coloring value formats, range grammar, and validation: [spec_highlight.md](spec_highlight.md). Coloring does not apply to PTY-captured output.

## Step exit codes

Recorded `steps` (pipe redirect or `pty: true`) observe the child exit code when the command finishes.

| Outcome | Behavior |
|---|---|
| Exit code `0` | No warning; recording continues |
| Non-zero exit | Warning on stderr; recording continues; later steps still run |
| Fatal error (for example PTY spawn failure or output drain timeout) | Recording stops; no cast; `post` runs; exit `1`, or the failing `post` command's code. See [spec_pty.md](spec_pty.md) |

scenetake exits `0` after a successful cast write even when individual steps exited non-zero, unless `pre`, `post`, SVG, or another fatal error occurs. Failed command output can remain in the cast as legitimate demo content.

Contrast with `pre` / `post` (fail-fast): [spec_pre_post.md](spec_pre_post.md). Warning message format: [spec_cli.md](spec_cli.md) → Logging.

## Command Environment

Every command scenetake runs from a scenario (`pre`, `steps` with pipe, PTY, or interactive PTY capture, and `post`) inherits scenetake's own environment with `COLORTERM=truecolor` set. A `COLORTERM` value from the host is replaced, not kept. Other variables pass through unchanged.

Why: commands never draw on the host terminal. They run behind scenetake's pipe or PTY, and the recording is replayed by cast players and the built-in SVG renderer, which support 24-bit color ([spec_svg.md](spec_svg.md)). A tool that cannot query its terminal picks a color depth from environment hints, and hints such as `WT_SESSION` or `TERM_PROGRAM` describe the terminal scenetake was started from. Without a fixed `COLORTERM`, the same scenario recorded true color from one terminal and a 16-color fallback from another. Replacing the host value makes the recording independent of where scenetake runs.

A command that needs another value sets it itself, for example `COLORTERM= tool` in bash or `$env:COLORTERM = ''; tool` in pwsh.

## Execution Order

1. Read the YAML file and resolve the render settings.
2. Check the terminal size, a step `run` given as a list or a mapping, step keys that contradict each other, a `cwd` with a NUL character (the OS would read the folder before it), the outputs (their folder too when there are no `pre` commands), and for `--format mp4` ffmpeg, a usable font, and the frame size ([spec_cli.md](spec_cli.md) → Output Paths, [spec_mp4.md](spec_mp4.md)). Any failure stops the run before anything executes.
3. Derive the deterministic seed and timestamp, and resolve the shell. A shell that cannot be found stops the run before anything executes: on Windows any shell, on Unix one given by an absolute path with no `..` in it. A shell with a NUL character is refused too, on every platform and whether it is a name or a path, because the OS reads it only up to the NUL and would start a different file. On Unix a shell given by name or by a relative path is started as written, so `$0` and the shell's own messages in the recording keep that name. A missing one fails at the first command.
4. Execute `pre` commands.
5. Run every output check again, the folder included, since `pre` may have changed what is there. A failure skips steps 6–8 and still runs `post`.
6. Execute and record `steps`.
7. Write the cast file (including `render` metadata). An error in step 6 or 7 skips the rest and still runs `post`.
8. If `--format svg` or `--format mp4`, render it from the written cast. See [spec_svg.md](spec_svg.md) and [spec_mp4.md](spec_mp4.md).
9. Execute `post` commands.
10. Report success or failure.

## Determinism

Deterministic seed and timestamp are derived from the whole YAML file (normalized line endings). Any change to the file may change cast metadata and typing jitter, even for keys that do not produce cast events (such as `pre`/`post`).

## Init Template

`scenetake init` creates a commented starter file following this structure. Optional sections (`render`, `pre`, `post`, coloring examples) should be present but commented so users discover features without enabling them by default.

## Cross-Document Notes

- [spec_pre_post.md](spec_pre_post.md) — `pre`/`post` recording exclusion and failure behavior.
- [spec_pty.md](spec_pty.md) — `pty: true` capture behavior and platform support.
- [spec_highlight.md](spec_highlight.md) — coloring semantics and style strings.
- [spec_cast.md](spec_cast.md) — cast header mapping, event stream, recording boundary.
- [spec_svg.md](spec_svg.md) — SVG renderer.
- [spec_cli.md](spec_cli.md) — CLI overrides and `init` command.

## Lessons Learned

- Separating `settings` (recording) from `render` (presentation) keeps cast behavior independent of SVG output.
- String-form steps cover simple demos; map-form steps carry per-command timing, coloring, and optional `pty` without a second schema.
- Recorded step non-zero exits warn but do not stop the scenario; setup/teardown and infrastructure failures use different rules ([spec_pre_post.md](spec_pre_post.md), [spec_pty.md](spec_pty.md)).
- **Default instant emit is required for good playback.** Replaying every pipe read chunk made typing hard to see, caused choppy SVG/GIF output, and broke progress-bar tools that emit many `\r`-based lines (for example `agg`). Capture still reads stdout/stderr in parallel; cast text merges stdout then stderr, and only event timing changes.
- **`execution-duration` is a presentation knob**, not wall-clock duration. It controls how long after Enter the first output appears, not how long the shell command runs.
- **Stream emit is opt-in** (`stream-output: true` or `max-duration`). Idle-collapse defaults are lenient (`silence-threshold: 2.0`) so only clearly idle gaps shrink when stream mode is on.
- **Host terminal hints leak into recordings.** `matrix` decides true color from `COLORTERM`, `TERM`, `WT_SESSION`, or `TERM_PROGRAM`. When scenetake ran from a pwsh without `WT_SESSION`, `matrix` printed `warning: true color not supported` into the cast and recorded 16 colors; with `WT_SESSION` set, the same scenario recorded true color. Users reasonably expect a Windows Terminal session to have it, but not every shell shown in Windows Terminal does. Setting `COLORTERM=truecolor` for every command removed that dependency (see Command Environment).
