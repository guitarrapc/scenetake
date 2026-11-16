# Scenario Format Specification

Status: **Implemented**

## Motivation

A YAML scenario defines terminal dimensions, the execution environment, timing, display metadata, setup, cleanup, and recorded commands. Feature specifications define their behavior.

## File Structure

Top-level keys use kebab-case. Recommended order for readability: metadata → `settings` → `render` → `pre` → `steps` → `post`. Key order does not affect behavior.

### Unknown keys

scenetake warns about unknown keys immediately after reading the file, before other checks. Recording continues, and the warning does not change the exit code. Checks cover the top level, `settings`, `render`, `render.theme`, map-form steps, and `highlight` entries. Each warning identifies the key location and suggests a correction when available:

| Key | Warning |
|---|---|
| Another spelling of a key of the same map (`typing_speed`, `TypingSpeed`, `Settings`) | `did you mean 'typing-speed'?` |
| A key of another map (`pty` under `settings`, `font-size` at the top level, `cwd` in a step) | `it belongs in a step`, `it belongs under render`, `it belongs at the top level` |
| Typo in the same map: `hightlight`, `ptty`, `setting`, `preest`. Allow 1 edit for keys shorter than 5 characters, otherwise 2. Adjacent transpositions count as 1 edit. | `did you mean 'highlight'?` |
| `<<` (YAML merge key) | `YAML merge keys are not supported, so nothing is merged` |

Example: `Warning: unknown key 'hightlight' in step 3 (did you mean 'highlight'?)`. Top-level, `settings`, `render`, and `render.theme` keys follow the classes that read them, through the schema VYaml's source generator emits for each class. Adding a key to those classes requires no separate list. Listing the keys by serializing an empty instance gives the same result but keeps VYaml's serializer in the binary ([spec_install.md](spec_install.md) → Binary size). Step and `highlight` keys use explicit lists.

Unknown keys have no exemption, including compose-style `x-` keys. VYaml skips anchors under unknown keys, so aliases to those anchors fail to parse. Such keys can only contain data that scenetake ignores.

Only the kebab-case spelling takes effect. Another spelling, such as `typing_speed` or `TypingSpeed`, has no effect anywhere, which is what the warning says.

### Unquoted colors

YAML reads `#` after a space as the start of a comment. `background: #1e3c72` is therefore a key with no value, and the scenario used to run without the color and without a word. scenetake warns, with the unknown-key warnings and in the same way:

| Key | Takes | Warning, for `key: #1e3c72` |
|---|---|---|
| `background`, `fg`, `bg`, `palette` | A color | `line N: 'background' has no value: YAML reads '#1e3c72' as a comment. Quote the color: background: "#1e3c72"` |
| `stderr-color`, `run-highlight`, `color` (in `highlight`) | A style, which takes a hex color as `fg:#rrggbb` ([spec_highlight.md](spec_highlight.md)) | `line N: 'color' has no value: YAML reads '#1e3c72' as a comment. Quote the style: color: "fg:#1e3c72"` |

- **When it warns:** the line has one of these keys, a space, then `#` and 3 or 6 hex digits; and the parsed scenario has that key with no value somewhere. The second condition keeps a `#fff` inside a script block (`run: |`) from counting.
- **When it does not:** a quoted color, `key:#1e3c72` (no space, so no comment), and a comment that does not start with a color, such as `palette: # 16 colors go here`.
- The playground lists the warning with its other warnings.

### Value types

A value that cannot be read as the type of its key stops the run before any command runs. The error names the key and the value, for example `'settings.typing-speed' must be a number of seconds, such as 0.5, not '50ms'.` The check covers the top level, `settings`, `render`, and `render.theme`. Each key there takes a number, a whole number, `true` or `false`, text, a list, or a mapping.

A step's timing key that is not a number is a warning. The step then uses the value from `settings`, or the default: `Warning: typing-speed: '50ms' is not a number of seconds (such as 0.5), using 0.05`. A step's flag that is not `true` or `false` also warns.

```yaml
title: "Demo"
width: 80
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
| `width` | `80` | Terminal columns in the cast header and PTY geometry when a step uses `pty: true`. `1`–`512`, the cast's range. Checked before `pre` runs. |
| `height` | `24` | Terminal rows in the cast header and PTY geometry when a step uses `pty: true`. `1`–`512`, checked before `pre` runs. |
| `cwd` | process cwd | Working directory for `pre`, `steps`, and `post`. Relative paths, including `..`, use the scenario file's folder, independent of the caller's location. Without `cwd`, commands use the caller's folder. The directory must exist before `pre`. An error names the resolved directory if it is missing. |
| `shell` | `$SHELL` / `pwsh` | Shell for all commands. Values: `bash`, `pwsh`, `powershell`, or a path |

On Linux and macOS, `pwsh` and `powershell` use PowerShell command arguments, including when selected by a path or by `$SHELL`. Other Unix shells use `-lc`. The requested executable name or path is preserved for both pipe and PTY commands, including `pre` and `post`.

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

1. Startup offset (align slow first chunk with `execution-duration`. Same rule as [spec_pty.md](spec_pty.md))
2. Burst merge (gaps under 50ms share a timestamp)
3. Idle collapse (`silence-threshold` / `max-idle`)
4. Linear compression when `max-duration` is set

PTY steps use the same stream pipeline only when stream emit is enabled **and** the step is not `interactive: true`. Otherwise only the existing PTY startup offset applies. Interactive PTY steps use wall-clock chunk times with `PtyLeadingInitFilter` only, unless `normalize-typing: true` is set ([spec_pty.md](spec_pty.md) → Interactive PTY).

Stream emit suits long-running commands (for example `docker build`) where replay pacing matters. Short demos, nested tool stderr, and progress-bar tools (for example `agg`) should usually keep the default instant emit. See Lessons Learned below.


## `render`

Display metadata for the cast header. Written on every run. Header mapping: [spec_cast.md](spec_cast.md). SVG usage: [spec_svg.md](spec_svg.md).

| Key | Default | Description |
|---|---|---|
| `font-size` | `16` | Monospace font size (`1`–`128`) |
| `font-family` | built-in stack | CSS `font-family` value (`1`–`16` families, `256` characters max). If `monospace` / `ui-monospace` is absent, `monospace` is appended. |
| `theme.preset` | `dark` | A named color theme: see Color themes below |
| `theme.fg` | from preset | Optional foreground override, `#rgb` or `#rrggbb` |
| `theme.bg` | from preset | Optional background override, `#rgb` or `#rrggbb` |
| `theme.palette` | from preset | Optional palette override: 16 colors, `#rgb` or `#rrggbb`, separated by `:` |
| `window` | omitted (`none`) | Optional window chrome: `macos` or `windows` |
| `window-title` | the top-level `title` | Text in the window's title bar. `""` shows none. Drawn only with `window`. |
| `background` | omitted (transparent) | One color, 2–4 colors for a gradient, or a `.png`, `.jpg`, `.gif`, or `.webp` file relative to the scenario file's folder. An image needs `--format svg` or `mp4`. |
| `margin` | 8; 12 with `window`; 32 with `background` | Pixels around the terminal or window, `0`–`200` |
| `padding` | from `font-size` | Pixels between the terminal's edge and its text, `0`–`100` |
| `opacity` | `1` | How much of the terminal's default background and the title bar covers `background`, `0`–`1`. Text and cells with their own background stay opaque. Without `background`, a warning, and the window stays opaque. |
| `blur` | `0` | Pixels of Gaussian blur on the background seen through a window with `opacity` below 1, `0`–`50` |
| `loop` | `true` | `false` plays the SVG once and stops on its final screen. An mp4 has no loop: the player decides. |
| `end-hold` | the pause before the last output | Seconds the final screen stays before the SVG starts again or the mp4 ends, `0`–`60` |
| `fade-out` | `0` | Seconds over which the terminal's contents fade out after `end-hold`, `0`–`10`. The terminal's background, the window, and `background` stay. An SVG that does not loop has no fade, with a warning. |

### Color themes

`theme.preset` and `--theme` take one of these names. Case does not matter. `fg`, `bg`, and `palette` change single colors on top of the theme. The cast header stores the resolved colors, so a player that does not know scenetake shows the same colors.

| Name | Background | Source of the palette (MIT) |
|---|---|---|
| `dark` | dark | scenetake's own |
| `light` | light | scenetake's own |
| `macos-dark` | dark | mbadolato/iTerm2-Color-Schemes, `windowsterminal/Apple System Colors.json` |
| `macos-light` | light | mbadolato/iTerm2-Color-Schemes, `windowsterminal/Apple System Colors Light.json` |
| `dracula` | dark | dracula/windows-terminal, `dracula.json` |
| `nord` | dark | nordtheme/xresources, `src/nord` |
| `one-dark` | dark | joshdick/onedark.vim, `term/One Dark.Xresources` |
| `gruvbox-dark` | dark | morhetz/gruvbox-contrib, `xresources/gruvbox-dark.xresources` |
| `catppuccin-mocha` | dark | catppuccin/windows-terminal, `mocha.json` |
| `tokyo-night` | dark | folke/tokyonight.nvim, `extras/windows_terminal/tokyonight_night.json` |
| `solarized-dark` | dark | altercation/solarized, `xresources/solarized` |
| `solarized-light` | light | altercation/solarized, `xresources/solarized`, with the light base tones that file gives |
| `github-dark` | dark | @primer/primitives 11.10.0, `--ansi-*` with `fgColor-default` and `bgColor-default` |
| `github-light` | light | @primer/primitives 11.10.0, the same tokens of the light theme |

- **Why the published palettes.** A reader who picks `nord` expects the Nord they use in their terminal. Each palette is the project's own terminal definition, taken in ANSI order, not a version made up from the theme's colors.
- **macOS has no published palette.** Apple publishes no palette file for Terminal.app. The macOS themes are "Apple System Colors" from iTerm2-Color-Schemes: macOS's system colors (`#ff453a` red, `#0a84ff` blue, `#32d74b` green) as a terminal palette, on `#1e1e1e` or white. They were chosen over that collection's "Terminal Basic", Terminal.app's default profile, whose light palette (`#990000` red, `#0000b2` blue) looks dated next to the `macos` window chrome.
- **Solarized's bright colors are gray.** Solarized defines the bright eight as its base tones, so `\e[92m` (bright green) is a gray in both Solarized themes, and bright black is the background in `solarized-dark`. That is how Solarized looks in any terminal.
- The window chrome follows the background's lightness, as for `dark` and `light` ([spec_svg.md](spec_svg.md) → Window chrome).
- `samples/theme-presets/` has one preview per theme: `palette.cast` (text styles, the 16 colors as text, then as backgrounds) rendered with `--theme NAME --window macos --window-title NAME`. `samples/regenerate.cs` renders them, and `-- --theme-presets` renders them alone. `tests/svg_render_test.cs` checks each theme's colors and that every preview is current.

`settings` controls recording. `render` controls presentation. The CLI may override each key, the theme only as a preset. See [spec_cli.md](spec_cli.md).

## `pre` and `post`

Top-level string arrays of shell commands. Each item is one command string (block scalars allowed). Empty or whitespace-only entries are skipped.

Runtime behavior: [spec_pre_post.md](spec_pre_post.md).

## `steps`

Required. A sequence of commands to execute and record. An empty step is skipped. A scenario with no command to record (no `steps` key, a misspelled one such as `step:`, or only empty steps) is an error found before anything runs.

Each typed character produces one cast event. A character is an extended grapheme cluster. Examples include emoji, skin tone modifiers, ZWJ sequences, and letters with combining marks. Each cluster appears as one keystroke.

### Forms

| Form | Example | Notes |
|---|---|---|
| String | `- echo "hi"` | Uses `settings` defaults. Quote a command that contains `: ` (`- "echo key: value"`): YAML reads it as a mapping otherwise |
| Map | `- run: git status` | Allows per-step keys below |

A step that is a YAML list (`- [echo, hi]`) is an error found before anything runs.

Map-form steps recognize:

| Key | Required | Description |
|---|---|---|
| `run` | yes | Command string. A missing `run`, or a list or a mapping under it, is an error found before anything runs. The error for a missing `run` names the keys the step has, so an unquoted `- echo key: value` shows up as the key `echo key` |
| `name` | no | Comment line before typing. Optional `[style]` prefix: [spec_highlight.md](spec_highlight.md) |
| `typing-speed`, `typing-jitter`, `pre-delay`, `post-delay`, `execution-duration` | no | Override `settings` for this step |
| `stream-output`, `max-duration`, `silence-threshold`, `max-idle` | no | Override `settings` output-timing keys for this step |
| `run-highlight` | no | Style for typed command text |
| `highlight` | no | List of `{ color, at }` for command output |
| `stderr-color` | no | Override `settings.stderr-color` for this step. Ignored when `pty: true` |
| `pty` | no | default `false`. Pseudo-terminal capture for TUI commands. Recording behavior: [spec_pty.md](spec_pty.md) |
| `interactive` | no | default `false`. Requires `pty: true`. Human operates the host terminal until the child exits. Recording behavior: [spec_pty.md](spec_pty.md) → Interactive PTY |
| `continue-on-error` | no | Default `false`. Allow a non-zero step exit without a warning, including with `--strict`. Use for expected failures. See Step exit codes. |
| `normalize-typing` | no | Default `false`. Requires `interactive: true`. Adjust visible typing to `typing-speed` during host-input windows. Remove Backspace corrections. Other output keeps wall-clock timing ([spec_pty.md](spec_pty.md) → Interactive PTY). |
| `keys` | no | Requires `pty: true`, and does not apply to `interactive: true`. A list of entries that scenetake writes to the PTY while the command runs, in order. Each entry has `sleep` (seconds to wait first), `type` (text), or `key` (a key name), or `sleep` with one of the other two. Behavior and key names: [spec_pty.md](spec_pty.md#keys-and-timeout) → Keys and timeout. |
| `timeout` | no | Seconds, above 0. When the command is still running after that time, scenetake stops it: in a PTY with Ctrl+C first, in a pipe by ending its process tree. A stop by timeout is not a failure: no exit-code warning, and `--strict` goes on. Does not apply to `interactive: true`. See [spec_pty.md](spec_pty.md#keys-and-timeout) → Keys and timeout. |

Coloring value formats, range grammar, and validation: [spec_highlight.md](spec_highlight.md). Coloring does not apply to PTY-captured output.

## Step exit codes

Recorded `steps` (pipe redirect or `pty: true`) observe the child exit code when the command finishes.

| Outcome | Behavior |
|---|---|
| Exit code `0` | No warning. Recording continues |
| Non-zero exit | Warning on stderr. Recording continues. Later steps still run |
| Non-zero exit, step sets `continue-on-error: true` | No warning. Recording continues, with `--strict` too |
| Non-zero exit with `--strict` | Recording stops at this step. Later steps do not run. No cast. `post` runs. Exit `1`, or the failing `post` command's code |
| Fatal error (for example PTY spawn failure or output drain timeout) | Recording stops. No cast. `post` runs. Exit `1`, or the failing `post` command's code. See [spec_pty.md](spec_pty.md) |

scenetake exits `0` after a successful cast write even when individual steps exited non-zero, unless `--strict`, `pre`, `post`, SVG, or another fatal error stops it. Failed command output can remain in the cast as legitimate demo content.

With `--strict`, the first non-zero step exit stops recording unless the step sets `continue-on-error: true`. Later steps do not run. No cast, SVG, or mp4 is written, so existing outputs remain. `post` still runs. The error identifies the step, its exit code, and its last output lines. See [spec_cli.md](spec_cli.md).

Contrast with `pre` / `post` (fail-fast): [spec_pre_post.md](spec_pre_post.md). Warning message format: [spec_cli.md](spec_cli.md) → Logging.

## Command Environment

Every command scenetake runs from a scenario (`pre`, `steps` with pipe, PTY, or interactive PTY capture, and `post`) inherits scenetake's own environment with `COLORTERM=truecolor` set. A `COLORTERM` value from the host is replaced, not kept. Other variables pass through unchanged.

The recording uses a pipe or PTY. Cast players and the built-in SVG renderer support 24-bit color ([spec_svg.md](spec_svg.md)). Tools can select color depth from environment hints when they cannot query the terminal. Host values such as `WT_SESSION` and `TERM_PROGRAM` describe the caller's terminal. Previously, these hints caused the same scenario to record true color from one terminal and 16 colors from another. A fixed `COLORTERM` removes that dependency.

A command that needs another value sets it itself, for example `COLORTERM= tool` in bash or `$env:COLORTERM = ''; tool` in pwsh.

### Output encoding on Windows

Windows pipe output, including `pre` and `post`, uses the same decoding regardless of the caller's terminal code page. Each child has its own console with the system OEM code page, such as 932 on Japanese Windows. Tools can write either UTF-8 or that console code page. Git, MSYS tools, and most cross-platform CLIs use UTF-8. Cmd, ping, and other Windows console tools use the console code page. PowerShell also uses it unless configured otherwise.

- scenetake reads each line as UTF-8 when it is valid UTF-8, and in the OEM code page when it is not. Text in a legacy double-byte code page is almost never valid UTF-8, and ASCII reads the same in both. A BOM at the start of the output is skipped.
- With `shell: pwsh` or `powershell`, by name or path, scenetake prefixes the command with `[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()`. PowerShell and child commands sharing its console then write UTF-8, preserving characters absent from the OEM code page. This affects only the child console. Recorded typing, `running:` lines, and errors retain the original command. Commands that must begin a script, such as `using namespace`, cannot follow this prefix. Put them in a script file instead.
- cmd and Git Bash receive no prefix. Their Windows console tools write the OEM code page, which the fallback decodes. Characters outside that code page are already lost.

PTY steps need none of this: ConPTY translates everything to UTF-8. On Linux and macOS scenetake reads pipes in the encoding .NET picks for the process, as before.

## Execution Order

1. Read the YAML file. Warn about unknown keys. Resolve render settings.
2. Validate the scenario and outputs (see Pre-execution checks below). Stop on failure before any command runs.
3. Derive the deterministic seed and timestamp. Resolve the shell (see Shell resolution below).
4. Execute `pre` commands.
5. Repeat all output checks, including the folder check, because `pre` may change the outputs. On failure, skip recording and rendering, but run `post`.
6. Execute and record `steps`.
7. Write the cast file, including `render` metadata. If recording or writing fails, skip rendering, but run `post`.
8. Render SVG or mp4 when selected. See [spec_svg.md](spec_svg.md) and [spec_mp4.md](spec_mp4.md).
9. Execute `post` commands.
10. Report success or failure.

### Pre-execution checks

Before `pre`, scenetake checks:

- Value types of the top-level, `settings`, `render`, and `render.theme` keys (see Value types above).
- Terminal dimensions.
- At least one command to record.
- Step structure: no lists, missing `run`, or `run` values that are lists or mappings.
- Conflicting step keys.
- Host stdin and stdout for interactive steps ([spec_pty.md](spec_pty.md) → Interactive PTY).
- An existing `cwd` without NUL characters. The OS would truncate a path at NUL.
- Output paths, including their folder when there are no `pre` commands ([spec_cli.md](spec_cli.md) → Output Paths).
- ffmpeg, a usable font, and frame dimensions for mp4 ([spec_mp4.md](spec_mp4.md)).

### Shell resolution

NUL characters are invalid in shell names and paths on every platform. The OS would truncate the value and start a different executable.

On Unix:

- A missing absolute path without `..` fails before commands run.
- Names and relative paths remain as written, preserving `$0` and shell messages. A missing executable fails at the first command.
- A shell name matching a file in the current directory causes an error. .NET would otherwise start that file before searching PATH.

On Windows:

- Any missing shell fails before commands run.
- A shell path starts that file. For pwsh, powershell, cmd, and bash, trailing dots or spaces are removed as Windows does.
- The filename selects arguments: `-Command` for pwsh and powershell, `/c` for cmd, `-lc` for bash, and `-c` otherwise.
- Shell names use PATH for pwsh, powershell, and cmd.
- bash lookup checks Git and MSYS folders under Program Files and local app data, then PATH.
- Lookup excludes the current directory and ProgramData. WSL bash is never used, including by path.

## Determinism

Deterministic seed and timestamp are derived from the whole YAML file (normalized line endings). Any change to the file may change cast metadata and typing jitter, even for keys that do not produce cast events (such as `pre`/`post`).

Instant pipe output produces identical casts across runs if commands print identical output. PTY, interactive, and stream output use wall-clock timing. Their output events and the interval to the next event can differ between runs.

After each such step, recording resumes at the next whole millisecond. This keeps subsequent timing stable after that first interval. Without this adjustment, fractional milliseconds could change later typing intervals by 1ms. Cast intervals use differences between rounded absolute times ([spec_cast.md](spec_cast.md) → Event time).

## Init Template

`scenetake init` creates a commented starter file following this structure. Optional sections (`render`, `pre`, `post`, coloring examples) should be present but commented so users discover features without enabling them by default.

Template commands must run in the selected shell. `scenetake init --shell bash|pwsh|powershell` selects the syntax and writes the corresponding `shell:` value. Without `--shell`, the template uses pwsh on Windows or `$SHELL` elsewhere, with `shell:` commented. It uses shared commands such as `echo` where possible. The stderr example uses `[Console]::Error.WriteLine(...)` for PowerShell and `echo ... 1>&2` for bash. A comment shows the alternative syntax.

## Cross-Document Notes

- [spec_pre_post.md](spec_pre_post.md) — `pre`/`post` recording exclusion and failure behavior.
- [spec_pty.md](spec_pty.md) — `pty: true` capture behavior and platform support.
- [spec_highlight.md](spec_highlight.md) — coloring semantics and style strings.
- [spec_cast.md](spec_cast.md) — cast header mapping, event stream, recording boundary.
- [spec_svg.md](spec_svg.md) — SVG renderer.
- [spec_cli.md](spec_cli.md) — CLI overrides and `init` command.

## Lessons Learned

- Separating `settings` (recording) from `render` (presentation) keeps cast behavior independent of SVG output.
- String-form steps cover simple demos. Map-form steps carry per-command timing, coloring, and optional `pty` without a second schema.
- Recorded step non-zero exits warn but do not stop the scenario. Setup/teardown and infrastructure failures use different rules ([spec_pre_post.md](spec_pre_post.md), [spec_pty.md](spec_pty.md)).
- **Default instant emit is required for good playback.** Replaying every pipe read chunk made typing hard to see, caused choppy SVG/GIF output, and broke progress-bar tools that emit many `\r`-based lines (for example `agg`). Capture still reads stdout/stderr in parallel. Cast text merges stdout then stderr, and only event timing changes.
- **`execution-duration` is a presentation knob**, not wall-clock duration. It controls how long after Enter the first output appears, not how long the shell command runs.
- **Stream emit is opt-in** (`stream-output: true` or `max-duration`). Idle-collapse defaults are lenient (`silence-threshold: 2.0`) so only clearly idle gaps shrink when stream mode is on.
- **Host terminal hints leak into recordings.** `matrix` decides true color from `COLORTERM`, `TERM`, `WT_SESSION`, or `TERM_PROGRAM`. When scenetake ran from a pwsh without `WT_SESSION`, `matrix` printed `warning: true color not supported` into the cast and recorded 16 colors. With `WT_SESSION` set, the same scenario recorded true color. Users reasonably expect a Windows Terminal session to have it, but not every shell shown in Windows Terminal does. Setting `COLORTERM=truecolor` for every command removed that dependency (see Command Environment).
- **Windows shell paths must start the specified file.** Previously, known shell filenames triggered name lookup, bypassing wrappers and alternate installations. Missing paths could start the PATH copy, while valid pwsh paths failed without a PATH copy. A trailing dot in `C:/WINDOWS/system32/cmd.exe.` also bypassed name detection. Cmd received `-c`, interpreted its path as switches, and created folders such as `-c` during `pre`. Starting the full path with Windows separators and removing the trailing dot fixes both cases.
- **Shell lookup must exclude the current directory.** A cloned project could contain `pwsh`, `pwsh.exe`, or executable `bash` files that ran before `pre`. Windows lookup now excludes the current directory. On Unix, .NET checks it before PATH for pipe commands, while PTY commands search PATH. Unix therefore rejects conflicting files. Selecting an absolute PATH executable would change `$0` and shell messages. Git/MSYS lookup also excludes ProgramData because other users can create folders there.
- **Windows pipe decoding must follow the child console.** Previously, decoding used the caller's code page, while children used the OEM code page. Japanese Windows tests on 2026-11-13 showed corrupt Git Bash and git text from cp932 terminals. From UTF-8 terminals, pwsh and cmd text was corrupt instead. PowerShell also replaced emoji with `??` in both cases. UTF-8 decoding with an OEM fallback and UTF-8 PowerShell output produced identical, correct casts from both terminals. The fallback uses `MultiByteToWideChar` because .NET lacks code page 932 without `System.Text.Encoding.CodePages`. An initial `DllImport` without `CharSet.Unicode` marshaled output as one-byte characters and returned no text. Native function pointers with `char*` avoid that marshaling.
- **Invalid steps need explicit errors.** YAML interprets `- echo key: value` as a mapping with key `echo key`. Previously, missing `run` caused silent omission. A misspelled `step:` also produced an empty cast with exit `0`. Both now fail before commands run. Missing-`run` errors list the parsed keys.
- **Typing must preserve grapheme clusters.** UTF-16 code-unit typing split non-BMP emoji into surrogates, which cast writing replaced with U+FFFD on every OS. Grapheme-cluster typing also preserves modifiers, ZWJ sequences, and combining marks. Commands without these characters retain identical typing events.
- **Missing `cwd` errors need the resolved path.** Setup cannot create `cwd` because `pre` already runs there. Previously, OS errors named neither `cwd` nor its base folder. Validation before `pre` now identifies both.
- **Recorded failures need an explicit CI policy.** Default exit `0` can hide broken demos. `--strict` stops unexpected failures and exits `1`. Later commands may depend on the failed command. Existing recordings must remain available for comparison. An earlier version wrote all outputs before failing, which lost that protection. Without `--strict`, one run still shows all failures. `continue-on-error: true` permits expected failures.
- **Wall-clock steps changed unrelated cast lines.** Repeated recordings with one stream step and one PTY step differed on 26–39 of 140 lines. Most differences were 1ms changes in later typing. Rounding after these steps reduced differences to 2–4 lines: their output and the following interval. Instant pipe steps already have stable timing.
- **Unknown keys hid sample errors.** Warnings found `Post:` in `samples/demo.yaml`, where cleanup never ran. YAML also parsed two `printf` steps in each theme sample as mappings because they contained `: `. Missing-`run` validation exposed those omitted steps for 16- and 256-color output. Unknown keys produce warnings so newer scenarios can still run on older versions.
- **Another key spelling took effect while the warning said it had none.** VYaml converts each YAML key to kebab case before it compares it with the class's keys, unless the options name kebab case themselves. scenetake parsed with the default options, so under `settings`, `render`, and the top level, `typing_speed: 0.3` changed the typing speed. The unknown-key warning said scenetake did not read it. A step key, read from a plain map, never took effect. The playground found this on 2026-11-15, when a scenario written the way an AI assistant often writes one was pasted into it. The parse now names kebab case in the options, so one spelling works everywhere.
- **The YAML library's type errors named neither the key nor the type.** `typing-speed: 50ms` gave `Cannot detect a scalar value as double : Scalar 50ms at Line: 9, Col: 9`, and the line was the one after the value. Units such as `ms` and `s` are a common guess. When the typed parse fails, scenetake reads the file again as plain maps and finds the first value that does not fit its key's type. The key types come from the schema that VYaml generates for the unknown-key check. A step value used to fall back to the default with no message, and it now warns.
- **Native AOT needs VYaml's `object` formatter.** `Dictionary<object, object?>` parsed under `dotnet run` but failed after publishing because its formatter required reflection metadata. Reading as `object` uses an AOT-compatible formatter with the same maps. A published-binary test protects the warning behavior.
- **Relative `cwd` must follow the scenario location.** Previously, `cwd: work/repo` referred to different folders when the caller changed directories. Relative paths now use the scenario file's folder. This affects only relative `cwd` values when scenetake starts elsewhere. Without `cwd`, commands still use the caller's folder. The interactive sample relies on this to edit that folder's `README.md`.
- **The starter template must match its shell.** Bash redirection caused a ParserError in Windows pwsh, and Windows lacked `printf`. The template now uses commands for the default shell. `--shell` supports other choices, including Git Bash on Windows and PowerShell on Unix. The generated file names the selected shell.
- **Unix PowerShell needs PowerShell arguments.** Unix launches previously passed `-lc`, which PowerShell rejected with exit code 64. Pipe and PTY steps now pass `-NoLogo -NoProfile -Command`.
- **The default width was too wide for a README.** Without `width`, a scenario got 120 columns: an SVG 1226 pixels wide (2.4 : 1), which a GitHub README of about 830 pixels showed at two thirds, with 11-pixel text. The default is now 80 × 24, the classic terminal size: 830 × 504 as SVG and 820 × 488 as mp4, shown at its own size. Every sample sets its size, so none changed.
