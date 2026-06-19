# CLI Specification

Status: **Implemented**

## Motivation

scenetake exposes a small command-line surface: record scenarios to cast (and optionally SVG), convert existing casts to SVG, and scaffold starter YAML. Centralizing CLI behavior here keeps feature specs focused on runtime semantics.

## Commands

| Command | Purpose |
|---|---|
| `scenetake <scenario.yaml> [output]` | Run a scenario; write `.cast` (default) or `.cast` + `.svg` |
| `scenetake svg <input.cast> [output]` | Convert an existing cast to SVG |
| `scenetake init [scenario.yaml]` | Create a commented starter scenario file |
| `scenetake --help` | Print usage |
| `scenetake --version` | Print version |

```bash
scenetake [--verbose] [--format cast|svg] [--font-size N] [--font-family FAMILIES] [--theme dark|light] [--window none|macos|windows] [--max-fps N] <scenario.yaml> [output]
scenetake svg [--font-size N] [--font-family FAMILIES] [--theme dark|light] [--window none|macos|windows] [--max-fps N] <input.cast> [output]
scenetake init [scenario.yaml]
```

Subcommands accept `-h` / `--help` for subcommand-specific usage.

## Parsing

- Options may appear in any order before positional arguments on the scenario and `svg` paths.
- Unknown `-` / `--` options are explicit errors (avoids typos becoming path-not-found errors).
- `--format`, `--font-size`, `--font-family`, `--theme`, `--window`, and `--max-fps` accept `--name=value` or `--name value`.
- Duplicate `--font-size`, `--font-family`, `--theme`, `--window`, or `--max-fps` is an error.

## Options

| Option | Commands | Behavior |
|---|---|---|
| `--verbose` | scenario path only | Show successful `pre`/`post` labels and phase markers. See [spec_pre_post.md](spec_pre_post.md). |
| `--format cast\|svg` | scenario path only | Default `cast`. `svg` also writes `.svg`. See [spec_svg.md](spec_svg.md). |
| `--font-size N` | scenario, `svg` | `1`–`128`. Scenario path: overrides `render.font-size` in the written cast header and SVG. `svg`: render-only override (CLI > cast header > default `16`). See [spec_cast.md](spec_cast.md). |
| `--font-family FAMILIES` | scenario, `svg` | CSS `font-family` string (`1`–`10` families, `256` characters max). Scenario path: overrides `render.font-family` in the written cast header and SVG. `svg`: render-only override (CLI > cast header > default stack). See [spec_cast.md](spec_cast.md) and [spec_svg.md](spec_svg.md). |
| `--theme dark\|light` | scenario, `svg` | Scenario path: overrides `render.theme.preset`; YAML `fg` / `bg` / `palette` still merge; cast header stores resolved hex. `svg`: render-only override (CLI > cast header > default `dark`). See [spec_cast.md](spec_cast.md). |
| `--window none\|macos\|windows` | scenario, `svg` | Scenario path: overrides `render.window` in the written cast header (and in the SVG when `--format svg`). `svg`: render-only override (CLI > cast header > default `none`). See [spec_cast.md](spec_cast.md) and [spec_svg.md](spec_svg.md). |
| `--max-fps N` | scenario (`--format svg` only), `svg` | Optional frame sampling cap for SVG animation. `0` (default) = off (full cast timing). `1`–`120` enables sampling. See [spec_svg.md](spec_svg.md). |

`init` accepts only an optional output path positional; no other flags.

## Output Paths

**Scenario path.** `[output]` sets a shared stem; extensions differ (`.cast`, and `.svg` when `--format svg`). With no `[output]`, stem matches the scenario path without extension. Passing `out.cast` or `out.svg` yields the same stem.

**`svg` subcommand.** With no `[output]`, writes alongside the input as `<stem>.svg`. With `[output]`, uses that path's stem and directory (extension optional).

## Logging

Progress and status go to stderr (`Loading:`, `Written:`, `Done:`). File paths in these messages are shown relative to the current working directory, using `/` separators; when a path cannot be relativized (for example, another drive on Windows), the full path is shown instead. Step `running:` lines are always shown on the scenario path.

**`pre` / `post`.** stdout and stderr are forwarded after each command exits (live streaming not required in v1). Failure prints phase, full command text, and exit code unconditionally. See [spec_pre_post.md](spec_pre_post.md).

**Warnings.** Non-fatal issues print `Warning: …` to stderr and execution continues. Feature-specific warn-and-continue rules: [spec_highlight.md](spec_highlight.md), [spec_svg.md](spec_svg.md).

**Errors.** Fatal issues print `Error: …` to stderr and exit non-zero.

## Exit Codes

| Outcome | Code |
|---|---|
| Success | `0` |
| Parse / validation error, missing file, cast/SVG write failure | `1` (or command-specific for `pre`/`post`) |
| Failed `pre` or `post` command | Failed command's exit code |

Recorded step exit codes do not affect the process exit code. See [spec_pre_post.md](spec_pre_post.md).

## Init

- Default output path: `scenario.yaml` in the current directory.
- Fails if the target file already exists.
- Generated template follows [spec_scenario.md](spec_scenario.md).

## Cross-Document Notes

- [spec_scenario.md](spec_scenario.md) — scenario YAML structure and defaults.
- [spec_cast.md](spec_cast.md) — cast file format.
- [spec_svg.md](spec_svg.md) — SVG renderer, `svg` subcommand event handling.
- [spec_pre_post.md](spec_pre_post.md) — `pre`/`post` execution and failure behavior.
- [spec_highlight.md](spec_highlight.md) — coloring validation warnings.

## Lessons Learned

- Unknown options should fail explicitly; otherwise mistyped flags are misread as file paths.
- Default logs should stay focused on cast content; `--verbose` is the right place for successful `pre`/`post` labels.
