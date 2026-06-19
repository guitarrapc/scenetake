# Scenario Format Specification

Status: **Implemented**

## Motivation

A scenario file is the single declarative input for scenario2cast: terminal dimensions, execution environment, recording timing, optional display metadata, setup/teardown, and the command sequence to record. Keeping the YAML structure in one spec lets feature specs focus on behavior.

## File Structure

Top-level keys use kebab-case. Recommended order for readability: metadata → `settings` → `render` → `pre` → `steps` → `post`. Key order does not affect behavior.

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

post:
  - git clean -fd
```

## Top-Level Metadata

| Key | Default | Description |
|---|---|---|
| `title` | — | Optional cast title |
| `width` | `120` | Terminal columns in the cast |
| `height` | `24` | Terminal rows in the cast |
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
| `execution-duration` | `0.05` | Cast wait after command execution |
| `stderr-color` | `red` | Fallback color when stderr has no ANSI SGR. Value format: [spec_highlight.md](spec_highlight.md) |

## `render`

Display metadata for the cast header. Written on every run. Header mapping: [spec_cast.md](spec_cast.md). SVG usage: [spec_svg.md](spec_svg.md).

| Key | Default | Description |
|---|---|---|
| `font-size` | `16` | Monospace font size (`1`–`128`) |
| `font-family` | built-in stack | CSS `font-family` value (`1`–`10` families, `256` characters max). If `monospace` / `ui-monospace` is absent, `monospace` is appended. |
| `theme.preset` | `dark` | Built-in preset: `dark` or `light` |
| `theme.fg` | from preset | Optional foreground override (hex) |
| `theme.bg` | from preset | Optional background override (hex) |
| `theme.palette` | from preset | Optional 16-color palette override (colon-separated hex) |
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
| `run` | yes | Command string |
| `name` | no | Comment line before typing. Optional `[style]` prefix: [spec_highlight.md](spec_highlight.md) |
| `typing-speed`, `typing-jitter`, `pre-delay`, `post-delay`, `execution-duration` | no | Override `settings` for this step |
| `run-highlight` | no | Style for typed command text |
| `highlight` | no | List of `{ color, at }` for command output |
| `stderr-color` | no | Override `settings.stderr-color` for this step |

Coloring value formats, range grammar, and validation: [spec_highlight.md](spec_highlight.md).

## Execution Order

1. Resolve settings, shell, cwd, deterministic seed, and timestamp from the YAML file.
2. Execute `pre` commands.
3. Execute and record `steps`.
4. Write the cast file (including `render` metadata).
5. If `--format svg`, render SVG. See [spec_svg.md](spec_svg.md).
6. Execute `post` commands.
7. Report success or failure.

## Determinism

Deterministic seed and timestamp are derived from the whole YAML file (normalized line endings). Any change to the file may change cast metadata and typing jitter, even for keys that do not produce cast events (such as `pre`/`post`).

## Init Template

`scenario2cast init` creates a commented starter file following this structure. Optional sections (`render`, `pre`, `post`, coloring examples) should be present but commented so users discover features without enabling them by default.

## Cross-Document Notes

- [spec_pre_post.md](spec_pre_post.md) — `pre`/`post` recording exclusion and failure behavior.
- [spec_highlight.md](spec_highlight.md) — coloring semantics and style strings.
- [spec_cast.md](spec_cast.md) — cast header mapping, event stream, recording boundary.
- [spec_svg.md](spec_svg.md) — SVG renderer.
- [spec_cli.md](spec_cli.md) — CLI overrides and `init` command.

## Lessons Learned

- Separating `settings` (recording) from `render` (presentation) keeps cast behavior independent of SVG output.
- String-form steps cover simple demos; map-form steps carry per-command timing and coloring without a second schema.
