---
name: scenetake
description: Make a terminal demo (animated SVG, GIF, mp4, or PNG) of a CLI, TUI, or library for a README, docs, X, Zenn, or a blog by writing a scenetake scenario YAML, recording it, looking at the result, and tuning it. Use when the user asks for a terminal recording, a demo GIF or SVG, or a README demo, or when a scenario.yaml for scenetake needs to be written, fixed, or re-recorded.
---

# scenetake

scenetake runs the commands of a YAML scenario for real, types them as a person would, and records the terminal as an asciinema cast plus an animated SVG, a GIF, an mp4, or a PNG. Because the commands run, the demo shows the tool's real output, and recording again after the tool changes gives an up-to-date demo.

Your job as an agent: write the scenario, record it, look at the result, and tune it until the demo shows the tool well. Never type into a program by hand. Every key is in the scenario.

## Install and check

```bash
npx -y scenetake --version          # needs Node.js, nothing else
dotnet tool install -g scenetake    # or this, with the .NET SDK
```

Binaries for Linux, macOS, and Windows are on https://github.com/guitarrapc/scenetake/releases. GIF and mp4 need ffmpeg on PATH. SVG and PNG need nothing.

## Workflow

1. **Learn the tool.** Run its `--help` and the commands you plan to show. Pick 3 to 6 commands that show what it is for. A demo of 10 to 30 seconds works best: a title card, the commands, an end card with the install line.
2. **Start the file.** `scenetake init --shell bash demo.yaml` writes a starter that records as a finished demo. Replace its cards and commands. Keep its first line, which names the schema for editors. Use `--shell pwsh` for a PowerShell audience.
3. **Record.** `scenetake demo.yaml` writes `demo.cast` and every file in the scenario's `outputs` list. `scenetake --format svg demo.yaml` writes only the cast and an SVG. Add `--strict` to stop at the first command that fails.
4. **Read stderr.** Fix every `Warning:` line: an unknown key or a value scenetake ignored. A `Masked` line says what scenetake hid as `*`. Check that it hid nothing the demo needs. A `Note:` line says what a run did not do, such as comparing with an earlier cast, and needs no fix.
5. **Look at the result.** Render screens as PNG and view them with your image tool:
   - `scenetake png --at end demo.cast final` writes final.png, the last screen.
   - `scenetake png --at 4.5 demo.cast mid` writes the screen 4.5 seconds in, in recording time. The cast is JSON lines `[seconds since the previous event, "o", text]`, so the sum of the first numbers up to an event is its time. A step with a `name` starts with a marker event, `[seconds, "m", "the name"]`, which finds that step.
   - Check: the output fits the width, nothing is cut, colors show, no error text, and each screen stays long enough to read: about 1 second for a one-line answer, 2 to 3 seconds for a screen of output.
   - Each `Written:` line gives that file's length. The cast's is the recording. An SVG's adds the final screen's hold (`end-hold`). A file with `speed` or `length` gives its played length. Aim for an SVG of 30 seconds or less.
6. **Tune without running the commands again.** Record once with `--save-outputs demo.outputs.json`. Then `scenetake --replay demo.outputs.json demo.yaml` applies changes to typing, cards, comments, theme, and speed in a second. A step whose `run` changed needs a new recording.

## A scenario

```yaml
title: "mycli"
width: 80
height: 24
shell: bash

render:
  window: macos
  background: "#1e3c72 #2a5298"
  theme:
    preset: dark
  end-hold: 2

outputs:
  - format: svg                    # demo.svg, for a GitHub README
  - format: svg                    # with the dark one, scenetake prints a <picture> for the README
    theme: github-light
    file: demo-light.svg
  - format: gif                    # for Zenn (up to 3 MB) and X. Needs ffmpeg
    font-size: 12
    fps: 10

settings:
  prompt:
    - text: "{dir}"                # the name of the folder the step starts in
      color: "fg:green"            # name comment lines are cyan
    - " $ "
  typing-speed: 0.04
  post-delay: 1.2

pre:
  - rm -rf demo-project && mkdir demo-project

steps:
  - card:
      - "[bold fg:bright-cyan]mycli"
      - "Fast JSON diffs in your terminal"
    post-delay: 2.5
  - run: cd demo-project && mycli init
    show: none                     # runs between scenes, not recorded. The cd carries to the next steps
  - name: "Compare two files"
    run: mycli diff a.json b.json
    pty: true                      # the tool colors its output only for a terminal
  - name: "Watch mode"
    run: mycli watch .
    pty: true
    timeout: 8                     # Ctrl+C after 8 seconds
    keys:
      - wait: "Watching"           # go on when the program prints this
        type: "q"
  - card: "[fg:yellow]npm install -g mycli"

post:
  - cd .. && rm -rf demo-project   # post starts in the folder the last step left: demo-project
```

Only `steps` is required. The editor schema, https://raw.githubusercontent.com/guitarrapc/scenetake/main/schema/scenario.schema.json, describes every key. The full reference is https://github.com/guitarrapc/scenetake/blob/main/.github/docs/spec_scenario.md.

## Rules that decide whether a demo works

- **Quote** a command that contains `: ` (`run: 'echo "answer: $a"'`) and every color that starts with `#` (`"#1e3c72"`). YAML reads `: ` as a mapping and `#` after a space as a comment. Inside double quotes YAML reads `\` as an escape, so `"\(.name)"` fails there. A command with backslashes, or with both kinds of quotes, is easiest as a block, which YAML takes as written:

  ```yaml
  steps:
    - run: |-
        jq -r '.[] | "\(.name): \(.city)"' users.json
  ```
- **Choose the step kind by the output.** A plain step runs in a pipe: plain text, shown at once, and `highlight` can color its lines. `pty: true` runs in a terminal: colors, progress bars, and full-screen programs. `stream-output: true` plays a pipe step's output at its real pace.
- **TUIs and prompts** need `pty: true`, `keys`, and a `timeout`. In `keys`, use `wait: "text"` for the text the program prints before the next key, not a long `sleep`: a `wait` goes on as soon as the text shows. `type` types text, and a line break in it is Enter. `key` sends one key: `enter`, `escape`, `tab`, `up`, `down`, `ctrl+c`, `f1`. To find the text to wait for, record the program alone first: `scenetake capture --timeout 3 -- mycli watch .`, then `scenetake png --at end mycli.cast` and look.
- **Never use `interactive: true`.** It waits for a person at the keyboard. Write `keys` instead.
- **Setup and cleanup** go in `pre` and `post`, or in a step with `show: none` between scenes. With `shell: bash`, `zsh`, or `pwsh`, a `cd`, an `export`, an alias, or a function carries to the next steps, and on into `post`: `post` starts in the folder the last step left, so a cleanup of that folder needs `cd ..` first. Use the carry to keep typed commands short: define a function in a `show: none` step and type its name.
- **A slow or costly step** (a build, a deploy, an AI CLI that charges per run): `rerun: false` shows its output from the earlier cast without running it, once a recording has it. Its `cd` and `export` then do not happen, so put a `cd` in a step of its own. `--rerun` runs every step again. Keep `rerun: false` on an interactive step the user recorded by hand: CI then records the rest without a terminal.
- **A server or a watcher** that later steps call: a step with `background: true` and `wait: "Listening on"`. It is not recorded and stops at the end.
- **Cards** (`- card: [lines]`) show centered text on a cleared screen and run nothing: a title at the start, an install line or a URL at the end. Lines take a `[style]` prefix such as `[bold fg:bright-cyan]`. `- clear: true` clears the screen between scenes.
- **Masking is on.** Tokens, `KEY=value` secrets, passwords in URLs, and the recording machine's user name, home folder, computer name, and git name and email become `*`. A demo repository that shows commits should set its own identity and dates in `pre`, such as `git -c user.name=Demo -c user.email=demo@example.com commit`, with `GIT_AUTHOR_DATE` and `GIT_COMMITTER_DATE` set.
- **Keep it short.** Cut steps first. Then `render.length: 20` plays every output in at most 20 seconds, and an outputs entry's `length` fits one file, such as a 15-second cut for X: waits in output shrink first, then pauses, then typing, each only down to what stays readable, so you need not tune `typing-speed` and the delays. A demo that cannot fit plays at its shortest with a warning that says how long that is. `speed` (a factor such as 1.5) is the other way, not with `length`.
- **The look:** `render.window` (`macos`, `windows`), `render.background` (colors or an image), `render.theme.preset` (`dark`, `light`, `nord`, `dracula`, `github-dark`, `github-light`, and more: `scenetake --help` lists them). By default the image leaves out empty rows below the output (`fit: height`).
- **The shell decides what the demo looks like.** On Windows, `shell: bash` is Git Bash. A demo for Linux users looks right when it is recorded on Linux or in CI.

## Where each file goes

| Place | Format | Notes |
| --- | --- | --- |
| GitHub README | animated SVG | A dark and a light SVG in `outputs` make scenetake print a `<picture>` that follows the reader's theme |
| Zenn, a blog | GIF | Zenn takes up to 3 MB. scenetake warns above it. A smaller `font-size` and `fps: 10` make it smaller |
| X, Reddit | mp4 or GIF | X takes an mp4 up to 140 seconds on a free account and a GIF up to 15 MB. `max-size: 1280x1024` fits X and only scales down. A phone shows the video narrow, so a narrower terminal (`width: 60`) keeps the text larger |
| A preview image | PNG | `at: 1` in an outputs entry, such as the title card |

## When something goes wrong

| Symptom | Fix |
| --- | --- |
| `Mapping values are not allowed` | Quote the command that contains `: ` |
| `unknown key 'x' (did you mean 'y'?)` | Use the key it names. Keys are kebab-case: `typing-speed` |
| A color is missing and a warning says YAML read it as a comment | Quote the color: `"#1e3c72"` |
| The output has no colors | Add `pty: true`, or the tool's `--color=always` flag |
| A step waits 10 s and warns that `wait` gave up | The program never printed that text after the previous key. Look at a PNG of that moment and wait for text it does print |
| `keys requires pty: true` | Add `pty: true` to the step |
| The step's command failed (`step exited 1`) | Run it in a terminal first. `continue-on-error: true` marks a failure the demo shows on purpose |
| Text is cut at the right edge | Raise `width`, or make the output narrower |
| The demo shows old output after the tool changed | `--replay` runs no command, and a step with `rerun: false` does not run. Record without `--replay`, with `--rerun` |
