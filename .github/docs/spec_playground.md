# Playground Specification

Status: **Implemented**

## Motivation

The README shows what scenetake makes, but a reader who wants to try it must install a binary and write a scenario first. A playground on GitHub Pages lets the reader edit a sample scenario in the browser and see the cast and the animated SVG change at once.

The readers are authors of CLIs and libraries who want to show how their tool works, on a README, X, Zenn, or Reddit. Some write their tools by hand, some with an AI assistant, and some have no tool in mind yet. Each one needs to find out two things: what scenetake can show, and whether its output fits the place where they share.

A browser cannot start processes, so it cannot run a scenario's commands. If the playground made up their output, it would be the kind of tool that scenetake's README contrasts itself with: one that simulates output without running commands. The playground instead replays output that real commands printed when the sample was recorded. Where a command has no recorded output, the playground says so.

## Scope

### In scope

- **Scenario tab.** The reader edits a sample scenario, or opens their own with the outputs file that `scenetake --save-outputs` wrote. The playground composes the cast in the browser from the recorded outputs, then renders the SVG from that cast. Edits to these keys take effect: prompt, simulated typing, `name` comments, `run-highlight`, `highlight`, `stderr-color`, output timing keys, `render`, `title`, and terminal size.
- **Cast tab.** The reader converts a local `.cast` file (v2 or v3) to SVG in the browser, as `scenetake svg` does.
- **Render options** that match the CLI flags: `--theme`, `--window`, `--font-size`, `--font-family`, and `--max-fps`.
- Downloads of the `.yaml`, `.cast`, and `.svg` files, and a share URL.
- Static hosting at `https://guitarrapc.github.io/scenetake/`.

### Out of scope

- Running commands, including `pre` and `post`.
- mp4, PNG, and GIF output. mp4 and GIF need ffmpeg and system fonts ([spec_mp4.md](spec_mp4.md), [spec_gif.md](spec_gif.md)), and a PNG needs system fonts ([spec_png.md](spec_png.md)). The page gives the command for each place to share instead ([Both tabs](#both-tabs)).
- `--strict` and `init`.

## Architecture

The playground is a .NET WebAssembly build of the same source files that the CLI compiles. The parts that start processes are left out.

| Part | In the playground |
|---|---|
| Scenario parsing, unknown-key warnings, style and highlight parsing | Same code as the CLI |
| Composition of typing, comments, and output; cast writing; OSC filter | Same code as the CLI |
| Cast reading, VT emulator, SVG renderer | Same code as the CLI |
| Command execution (pipe, PTY, interactive PTY), `pre`, `post`, shell resolution | Replaced by a replay of the outputs file |
| mp4 rasterizer and ffmpeg | Not included |

- **Why shared source.** A second implementation in JavaScript would drift from the CLI. The playground is only worth showing if it renders what the CLI writes.
- **What this asks of the CLI source.** The code that the playground needs is the core library, `src/Scenetake.Core`: `Scenario.cs`, `Highlight.cs`, `CastComposer.cs`, `StepChunks.cs`, `StepOutputs.cs`, and `PlaygroundRender.cs`, with the renderer files. The CLI, `src/Scenetake`, keeps the entry point and the code that starts processes, and passes a step runner to the shared recording loop. The playground passes the replay instead.
- **Why a library.** The CLI started as a file-based app, `scenetake.cs`. Functions declared among its top-level statements are local to the entry point, so the playground compiled the shared files by linking each one, and each test program listed the files it included. Every new shared file meant editing those lists. A library that the CLI, the playground, and the tests reference replaced them.
- **Project.** `src/Scenetake.Playground/Scenetake.Playground.csproj` (`Microsoft.NET.Sdk.WebAssembly`) references the core library. The .NET runtime runs in a Web Worker, so typing never waits for a render.
- **Privacy.** Scenario YAML and cast files are processed in WASM and never leave the browser.

## Outputs File

`samples/<name>.outputs.json` holds what each step's command printed during the run that wrote `samples/<name>.cast`.

### Producing

- `--save-outputs FILE`, or the environment variable `SCENETAKE_OUTPUTS`, makes the scenario path write the outputs file there, from the same run as the cast. The file is written only when the cast is written. `samples/regenerate.cs` sets the variable for every sample. A sample recorded by hand, such as `password-prompt`, sets it by hand. The CLI's `--replay` reads the same file ([spec_cli.md](spec_cli.md) → Replay), and the page opens a reader's own with **Open files…** ([Scenario tab](#scenario-tab)).
- The showcase samples in `samples/showcase/` run tools that a Windows maintainer seldom has, such as fastfetch, bat, sl, and nyancat. `samples/showcase/record.sh` records them on Linux in a container built from `samples/showcase/Dockerfile`, with a Linux scenetake and `SCENETAKE_OUTPUTS`. The container sees `samples/` read-only around the showcase folder, so a showcase scenario can name a file there: `vim.yaml` names `../background.webp`. `regenerate.cs` reads only `samples/*.yaml`, so it never tries to run them. The playground publishes both folders into one sample folder, and the publish fails when two samples share a name.
- **Why a container.** It holds the tools at known versions and a small git project with fixed commit dates, so a new recording differs only where the tools changed. The README gallery shows these samples.
- **Why the same run.** A cast and an outputs file from two runs can disagree: `curl` headers and `git log` dates change between runs. The playground would then not reproduce the committed sample.
- **Secrets are hidden before the file is written** ([spec_scenario.md](spec_scenario.md) → Masking secrets): the outputs file holds each step's output as the cast shows it, with `*` for each hidden character, so a secret never reaches the browser. The replay masks again with the scenario's rules, which changes nothing in a recorded step and hides what an edited YAML adds, such as a new `mask` text. The browser has no environment and no machine to read, so values of secret environment variables and personal details ([spec_scenario.md](spec_scenario.md#masking-personal-details) → Masking personal details) are hidden only when the CLI records. Each step's `run` text stays as the YAML has it: the replay finds steps by it.

### Contents

| Field | Holds |
|---|---|
| `version` | Format version, `1` |
| `scenetake` | The scenetake version that recorded the file |
| `os` | `windows`, `linux`, or `macos` |
| `shell` | The resolved shell's file name, the same value as the cast's `env.SHELL` ([spec_cast.md](spec_cast.md) → Header) |
| `width`, `height` | The terminal size of the recording |
| `endFolder` | Only when the prompt shows `{dir}`: the folder the last step left, which the closing prompt shows with `end-prompt: true`, masked as it showed it. It is kept without `end-prompt` too, so a replay may turn it on ([spec_scenario.md](spec_scenario.md) → Prompt) |
| `steps` | One entry for each step that ran, in order |

Each step entry:

| Field | Holds |
|---|---|
| `run` | The command text as typed |
| `maskedRun` | Only when masking changed `run`: the text after masking. A replay types it as the step's command, and it identifies the step in the cast's `st:steps` tag, because a replay may not know the secret ([spec_scenario.md](spec_scenario.md#recording-again) → Recording again) |
| `kind` | `pipe`, `pty`, or `interactive` |
| `exitCode` | The command's exit code |
| `folder` | Only when the prompt shows `{dir}`: the folder name its prompt showed, masked as it showed it. A replay and the playground draw the same prompt from it, and show `~` for a file without it |
| `cursor` | Non-interactive PTY steps on Windows only: the start cursor that scenetake reported to ConPTY, as `row` and `column` ([spec_pty.md](spec_pty.md)) |
| `chunks` | Output in arrival order (below) |
| `end` | Interactive steps only: where the step's output ends, in seconds since the command started |

Each chunk, by step kind:

| Kind | Chunk fields | Holds |
|---|---|---|
| `pipe` | `ticks`, and `out` or `err` | Time since the command started in 100 ns ticks, and the stdout or stderr text as decoded |
| `pty` | `ticks`, `data` | Time in ticks, and the bytes (base64) after the leading terminal setup is removed ([spec_pty.md](spec_pty.md) → Recording behavior) |
| `interactive` | `time`, `data` | The output as timed in the cast, in seconds since the command started, and its bytes (base64) |

- **Exact times.** Times are stored without rounding, so a replay computes the same event times as the run did.
- **PTY output after the setup filter.** The setup that a shell or ConPTY writes before the command's own output is removed before the file is written, so the file never holds it. Timing keys such as `execution-duration` still apply in a replay, because they act after that filter.
- **Nothing that the cast leaves out.** The file is committed and shared like the cast, so it follows the cast's rules. In OSC sequences that the cast leaves out ([spec_cast.md](spec_cast.md) → Sequences left out), each character of the text is replaced with `x`. The introducer, the number, and the terminator stay. The cast's filter therefore drops the same text from a replay as from the run ([Lessons Learned](#lessons-learned)).
- **No typed keys.** For an interactive step, the file holds the step's output as the cast has it, already timed, and not the input log. On Windows the input log holds every key, including keys typed at a password prompt ([spec_pty.md](spec_pty.md) → Interactive PTY). Re-timing the output would need those keys.
- The command text in `run` is not masked: the cast shows it as typed.
- Output of `pre` and `post` is not stored, because it is never recorded.

## Replay

The playground composes a cast the way the scenario path does. The only difference is that each step takes its output from the outputs file instead of running a command.

### Matching steps to recorded outputs

- A step matches a recorded step that has the same `run` text and the same kind. The kind is `interactive` when the step sets `interactive: true`, `pty` when it sets only `pty: true`, and `pipe` otherwise.
- The n-th step with a given text and kind takes the n-th recorded step with that text and kind. When the scenario has more such steps than the recording, each extra step takes the last recorded one.
- **Why order counts.** The commands of a sample carry state: `git status` before `git commit` prints something different from `git status` after it. Reusing the last recorded output for an extra step lets a reader duplicate a step and still see output.
- Only the `run` text and the kind decide a match. Edits to typing, highlights, names, timing keys, or render keys never lose a match.
- **A `show: none` step, or a `background: true` one, is not in the outputs file**, and the replay skips it: it runs nothing, takes no recorded output, takes no time, and the step notes leave it out. What a later step got from a background server is in that step's recorded output. A step with `show: output` is recorded and matched as any other. A `clear: true` step and a `card` step need no recording: the replay writes the same clear and the same card as the CLI ([spec_scenario.md](spec_scenario.md) → steps).
- The shell state that the CLI carries from step to step ([spec_scenario.md](spec_scenario.md) → Shell state across steps) is already in each recorded output, such as a `ls` after a hidden `cd`, so the replay needs none.

### Steps without recorded output

- A step that matches nothing is typed as usual. One faint line follows: `# no recorded output in the playground`. Its exit code is `0`.
- A note under the preview names each of these steps, so the reader can tell which output is real.
- **Why a line in the terminal.** Without it, a downloaded cast or SVG would show the command as if it had printed nothing.
- **Why so short.** The line has 38 characters, so it does not wrap in a 40-column terminal. The first wording had 62 characters and wrapped in a 60-column scenario.

### Output recorded under other conditions

Recorded PTY output is drawn for the terminal that it ran in. The step still replays, with a note under the preview, when:

- the scenario's `width` or `height` differs from the recorded size, or
- the recording comes from Windows and the cursor at the start of the step differs from the recorded `cursor`.

Pipe output is plain text and replays at any size without a notice.

### Keys that have no effect

The UI lists these keys as not applied when the scenario sets them.

| Key | Why it has no effect |
|---|---|
| `shell`, when it names another shell than the recording's | The output comes from the recording's shell. The cast's `env.SHELL` is the recording's `shell`. Every sample sets its own shell, which is not listed. |
| `cwd` | No command runs. |
| `pre`, `post` | No command runs. Their output is never in a cast. |
| Timing and typing keys of an interactive step | Its output is stored already timed ([Contents](#contents)). The UI does not list these. |
| `keys`, `timeout` | No command runs to type into, wait for, or stop. The recorded output already holds what they did. The playground still draws the typing delays of `type`, so later steps type as on the CLI. A `wait` draws none. The UI does not list these. |
| `keep-output` | There is no earlier cast in the browser to keep the output of, so each step shows its recorded output ([spec_scenario.md](spec_scenario.md#recording-again) → Recording again). The cast the playground writes has the same `st:steps` tag as the CLI's. |
| `rerun: false` | The playground runs nothing, and has no earlier cast, so each step shows its recorded output. |
| `outputs` | The browser shows one SVG, drawn with the render options on the page. The CLI writes the files listed ([spec_cli.md](spec_cli.md) → Outputs list). |

### Exit codes, warnings, and errors

- A recorded non-zero exit code warns as in the CLI, unless the step sets `continue-on-error: true` ([spec_scenario.md](spec_scenario.md) → Step exit codes).
- Each warning that the CLI would write to stderr for the scenario goes to the warnings list instead. This includes unknown keys, invalid styles, cast header warnings, and the SVG size warning.
- An error that stops the CLI before any command runs stops the render. Examples are a YAML syntax error, an invalid terminal size, a step key error, and an invalid render option. The playground shows the error, and the preview keeps the last good result.

## Equivalence with the CLI

- For each sample, a replay of `<name>.outputs.json` with the unmodified `<name>.yaml` gives the committed `<name>.cast` byte for byte. The SVG equals `<name>.svg`.
- Each step's typing comes from a random sequence seeded by the step, and the timestamp is fixed, as in the CLI ([spec_scenario.md](spec_scenario.md) → Determinism). The same edit therefore changes the typing in the same way in both.
- The SVG is rendered from the cast text as written, as on the scenario path ([spec_svg.md](spec_svg.md) → Animation and layout).
- **Why.** This equivalence is the test that the playground shows scenetake and not an approximation of it. It also catches an outputs file that a YAML edit made stale.

## UI

### Render options

A panel under the active tab's preview sets `--theme`, `--window`, `--font-size`, `--max-fps`, `--font-family`, `--window-title`, `--background`, `--margin`, `--padding`, `--fit`, `--opacity`, `--blur`, `--loop` / `--no-loop`, `--end-hold`, `--fade-out`, `--speed`, and `--length`. `--fit` sits with `--margin` and `--padding`, which also set the size of the image. `--background` takes colors or an image, as on the CLI. Images are under Background images below. `--opacity` and `--blur` sit after it, because they act only on a background, with a line on what they do. The three ending options, `--speed`, and `--length` come last, with a line on what they do. `--speed` and `--length` together are an error, as on the CLI, and either one replaces the other from the YAML or the cast ([spec_svg.md](spec_svg.md) → Length). `--loop` is a select of from YAML, yes, and no. `--theme` lists every color theme, grouped by dark and light background ([spec_scenario.md](spec_scenario.md) → Color themes). One set of controls serves both tabs and moves with the tab. Each control is empty by default, which keeps the value that the scenario or the cast sets. An empty control says where that value comes from: "from YAML" on the Scenario tab and "from cast" on the Cast tab. `--max-fps` is the exception: neither the YAML nor the cast sets it, so its empty value is the default, 0. The labels used to say "scenario" on both tabs, and the number fields showed the defaults (`16`), which differ from the result when the YAML sets its own size. On the Scenario tab the values are stored in the cast header, as on the scenario path. On the Cast tab a value overrides the cast header, and the header overrides the default, as with the `svg` command ([spec_cli.md](spec_cli.md)).

Below 960 px the panel comes after the editor and starts closed, unless a share URL sets an option.

**Background presets.** Under `--background`, swatches fill the field with a color or gradient: none, Midnight, Ocean, Azure, Sky, Mint, Grape, Sunset, and Graphite. The value lands in the field, where it can be read and copied, and the swatch that matches the field is pressed. A line under them gives the format with examples, and the YAML form in quotes.

**Background images.** A browser page reads no files, so the page gives the runtime each image with its file name, and a background in the YAML or the panel names one. A path finds an image by its file name: a scenario written for the CLI names `../background.webp` from its own folder, and the same YAML works here unchanged.

- The samples' images, listed under `images` in `samples/index.json`, load before the first render, because the default sample or a shared YAML may name one. Each gets a swatch that shows the image.
- **Image…** adds a `.png`, `.jpg`, `.gif`, or `.webp` file of at most 8 MB from the reader's computer. It gets a swatch, and its name goes in the field. A file with the name of a loaded image replaces it.
- An image stays in the browser tab. A share URL names it but does not hold it: an image is far over the URL's 16384 characters, and a local file may be private. A link that names an image the reader has not loaded shows `background image not loaded: night.webp (loaded: background.webp)`, and choosing that file with **Image…** shows the demo.

**Why presets and a format line.** The field takes a format nobody guesses from its name: `#rrggbb` values separated by spaces. In YAML a bare `#` starts a comment, so `background: #1e3c72` reads as no value. That gives a warning ([spec_scenario.md](spec_scenario.md) → Unquoted colors), and the format line shows the quoted form before anyone needs it.

**Why under the preview.** The options are second to the demo and the YAML. Above both columns, they were the first thing under the tabs on every screen, and a phone showed a settings menu before anything to try. Under the preview, a change shows next to the result.

### Scenario tab

| Feature | Behavior |
|---|---|
| Sample selector | Lists each sample in `samples/` or `samples/showcase/` that has both a `.yaml` file and an `.outputs.json` file, with a short note on what it shows, such as `pty — a full-screen TUI in a PTY`. A sample without a note shows its name only. Selecting a sample loads both files. The default is `welcome`, listed first; the others follow in name order. Reset loads the sample's YAML again. |
| Your own files | **Open files…**, or files dropped on the editor, open the reader's scenario YAML and the outputs file that `scenetake --save-outputs` wrote for it, told apart by `.json` and `.yaml` / `.yml`, or by a leading `{`. The selector then shows `your file — <name>.outputs.json`, and Reset loads the opened YAML again. An outputs file alone replays into the YAML in the editor. A YAML alone replays the loaded sample's recording, and the step notes say to open its outputs file too. A file that is not an outputs file is an error that names it. The files are read in the browser and never uploaded. Choosing a sample leaves them. **Why:** the reader tunes the look of their own demo with a live preview, after one run of the commands. Dropped files open this way instead of CodeMirror inserting their text. |
| First screen | `samples/showcase/welcome.yaml`: the scenetake logo (`oh-my-logo SCENETAKE dawn --filled`: block letters, cyan to blue) and a colored `git log --graph`, in a macOS window on `background.webp`. Its YAML is short and commented, so a reader sees what each key does. **Why:** it was `basic`, plain text on the default theme, which showed little of what scenetake makes. A `toilet` and `lolcat` banner came first; oh-my-logo's filled letters stand out more, with a shorter command. The first screen is also the link preview ([Link preview](#link-preview-ogp)). |
| YAML editor | CodeMirror 5 in YAML mode. Without CodeMirror (a blocked CDN), a plain text area. |
| Live render | Composes the cast and the SVG 500 ms after the last edit, at once after a paste. Editing never waits for a render. A result that arrives after a newer edit is dropped, and the newer state is rendered. |
| Preview | Shows the SVG in an `<img>` element, as GitHub shows an SVG in a README. It fits the SVG to its width, but not so far that the terminal text is smaller than the editor's 13 px below 600 px, or 8 px on a wider screen: past that, the preview scrolls sideways. Replay restarts the animation. **Actual size** shows the SVG at its own width, and the preview scrolls sideways. **Full screen**, or a tap on the preview, shows the demo on the whole screen from its start (see below). The status line shows the render time, the cast and SVG sizes, and how long the SVG plays, as the `svg` command's Written line gives it: `plays 12.0s`, `plays 8.0s at 1.5x`, or `plays 15.0s, length 15s`. The length is one loop with the speed or length, end-hold, and fade-out applied, so the author sees what `--speed` or `--length` did without timing the preview. |
| Step notes | Appear only for the steps that differ from a plain replay: a step with no recording, and a step whose recording replays in another terminal size or cursor position. Each line gives the step's number and the first line of its run text. A sample as published has none. |
| Recording info | The note under the editor gives the scenetake version, the shell, the OS, and the terminal size of the outputs file |
| Messages | The error, the warnings, and the keys that are not applied |
| Downloads | `.yaml`, `.cast`, and `.svg` |

### Cast tab

| Feature | Behavior |
|---|---|
| Input | A file picker, drag and drop, or pasted text, v2 or v3. The committed sample casts, including `resize.cast`, can also be selected, with the same notes as the Scenario tab. Samples recorded from the keyboard, such as Copilot CLI and vim in `interactive-pty`, are only here, and the Scenario tab's note points to them. Opening the tab with no input selects the current sample's cast. |
| Output | The SVG preview with **Actual size** and **Full screen**, the warnings, and a download of the `.svg` file |

### Both tabs

- The header and footer follow the [seiton playground](https://guitarrapc.github.io/seiton/)'s. **Why:** the two playgrounds come from the same author, so a reader who knows one finds the same frame in the other.
- **Header.** A full-width bar centers the title and the version badge, which links to the release. Unlike seiton's, it has no logo: scenetake has none. Under them, one sentence says what scenetake is for: a terminal demo of a CLI or library, for a README, X, Zenn, or Reddit. A second, smaller line, that the page renders in the browser, is left out below 600 px. The tabs sit under the bar.
- **Footer.** The GitHub mark, the copyright and MIT license, and a theme switch, separated by bars. The switch cycles through system, light, and dark, and shows the mode as an icon. Below 600 px the text comes first and the mark and the switch share a row under it, because the bars ended up at the ends of wrapped lines.
- **Copy link** copies a share URL.
- Below 960 px the layout has one column: the preview, the editor, the step notes and warnings, then the render options. The notes come after the editor, so a note that appears while the reader types does not move the editor down. Below 600 px the header and the toolbars are tighter, each toolbar fits on one row, and the preview runs to both edges of the screen. On a 375 × 812 screen the preview starts at 188 px and the YAML at 684 px, 10 px lower than before the header followed seiton's. The `basic` SVG shows its text at 13 px, the editor's size, with 44 of its 80 columns in view (43 on a 360 px screen), and the preview scrolls sideways to the rest, as the editor does.
- **Full screen** fills the whole screen with the demo, with no margin, and scales it to fit, larger than its own size on a large screen. On an upright phone (portrait, below 600 px) the demo is turned a quarter turn clockwise and takes the screen's long side, so the reader turns the phone to the left, as for a video. There, `basic` shows all 80 columns with its 16 px text at 11.9 px, where the page shows 44 columns at 13 px. A phone already held sideways shows it unturned. A tap, Esc, or Back closes the view and stays on the page. Below 600 px **Full screen** replaces **Actual size**.
- Below the tabs, the page tells how to install scenetake, and which output and command fit each place to share:

| Where | What it takes | Command on the page |
|---|---|---|
| GitHub README | Animated SVG | `scenetake --format svg` |
| X, Reddit | mp4 or GIF, not SVG | `scenetake --format mp4`; for X, `scenetake mp4 --max-size 1280x1024`. Or a GIF, `scenetake gif`. |
| Zenn | Uploads of PNG, JPEG, GIF, or WebP up to 3 MB, not SVG | One screen as a PNG, `scenetake png scenario.cast`, with `--at` for an earlier screen ([spec_png.md](spec_png.md)). For motion, `scenetake gif scenario.cast`, and over 3 MB a smaller `--font-size` ([spec_gif.md](spec_gif.md)). |

Below 600 px each place is a block instead of a table row, so a command takes the screen's width: in three columns the agg command that the page showed before `scenetake gif` wrapped to 14 lines on a 375 px screen, and as a block it took 5.

**Why `scenetake gif` and not agg.** The page used to give agg's command in Docker for GIFs. scenetake makes the GIF itself now, from the frames it draws for mp4, smaller than agg's for the samples ([spec_gif.md](spec_gif.md)), so a reader needs one tool. The README names agg as another way, with no commands: a reader who needs one tool should not read two.

## Site Layout

| Path | Holds |
|---|---|
| `index.html`, `main.js`, `share.js`, `style.css` | The page |
| `favicon.svg`, `ogp.png` | The icon, and the image that X and other sites show for a shared link to the page |
| `worker.js` | The Web Worker that runs the .NET runtime |
| `_framework/` | The .NET runtime and assemblies |
| `samples/index.json` | `{"scenarios": [...], "casts": [...], "images": [...]}`: the samples that have an outputs file, the sample casts, and the background images. Written at publish. |
| `samples/<name>.yaml`, `.outputs.json`, `.cast` | Copied from `samples/` and `samples/showcase/` at publish |
| `samples/<image>` | The `PlaygroundImage` items of the playground project, such as `background.webp`. They are listed one by one, because `samples/` also holds each sample's GIF. |

### Link preview (OGP)

A link to the page shows a large image on X, Slack, Discord, and other sites, as a link to the seiton playground does.

- `ogp.png` is the page itself at 1200 × 670 in the dark theme: the first screen, `welcome`, with its YAML next to its demo, without the version badge. The image says what the page does without words: a short YAML gives this demo.
- `src/Scenetake.Playground/take-ogp.mjs` takes it with Playwright from the served page, as seiton's `take-ogp.mjs` does. It pauses every animation of the SVG at one moment (14 s by default, when `welcome` has printed everything and its 15 s loop has not started again), so the same page gives the same image byte for byte. An SVG in an `<img>` plays on its own clock, so without the pause the frame would depend on when the screenshot runs. The status line under the preview is hidden too: it gives the render time, and two runs gave different images until it was hidden.
- The meta tags give `summary_large_image`, the image's size, and alt text. The image URL is absolute and has `?v=`, which is raised when the image changes, because sites cache the image by URL.

## Interop API

`worker.js` calls these exports. Results are JSON strings. The cast and SVG of the last render are taken as UTF-8 bytes, so a large SVG is never escaped into JSON or copied into a JavaScript string.

| Function | Parameters | Returns |
|---|---|---|
| `LoadOutputs` | `(outputsJson)` | The outputs file's `scenetake`, `os`, `shell`, `width`, `height`, and step count, or `error`. Later renders replay this file. |
| `RenderScenario` | `(yaml, optionsJson)` | `steps` (`index`, `run`, `kind`, `recorded`, `notices`), `warnings`, `notApplied`, `castBytes`, `svgBytes`, or `error` |
| `RenderCast` | `(castText, optionsJson)` | `warnings`, `svgBytes`, or `error` |
| `SetImage` | `(name, bytes)` | `{}`, or `error` for a name with a folder or without an image extension, an empty image, or one over 8 MB. Keeps the image for later renders under its file name; the same name replaces it. |
| `TakeCast`, `TakeSvg` | none | The last render's cast or SVG as UTF-8 bytes |
| `GetProductVersion` | none | The version string, or `unknown` |

- No exported function throws. An exception that crosses the WASM boundary stops the runtime until the page is reloaded ([Lessons Learned](#lessons-learned)). Errors are returned as values. If the runtime stops anyway, the page says so and asks for a reload.
- The keys of the options JSON match the CLI flags: `theme`, `window`, `fontSize`, `fontFamily`, `maxFps`, `windowTitle`, `background`, `margin`, `padding`, `opacity`, and `blur`. An invalid value is an error. The valid ranges are the same as in the CLI ([spec_cli.md](spec_cli.md) → Options).

## Share URL

- The URL hash holds JSON with `v` (`1`), `s` (the sample whose outputs it replays), `y` (the YAML), `t` (`cast` when the Cast tab is active), and `o` (the render options that are set). The JSON is deflated with zlib (`CompressionStream('deflate')`) and base64url-encoded.
- The URL holds only the sample's name, not its outputs file. An outputs file can be large: `pty` records full-screen PTY output. The page fetches the file from the site. A sample that no longer exists falls back to the default sample.
- A hash over 16384 characters is not written; the page suggests downloading the YAML instead.
- Cast tab input is not shared. Local cast files can be large, and they can be private.
- The reader's own outputs file is not shared, for the same reasons. With one loaded, Copy link copies nothing and says to share the downloaded `.yaml` with the `.outputs.json`.
- A background image is not shared either, for the same reasons. The options keep its name ([Background images](#render-options)).

## Deployment

- `.github/workflows/playground.yaml`: a push of a `v*` tag, or `workflow_dispatch` for an existing tag, runs `tests/Scenetake.Core.Tests/PlaygroundReplayTests.cs`, publishes the playground, and deploys it with `actions/deploy-pages`. The site shows that release's version. Its samples and outputs files come from the same tag.
- `.github/workflows/build.yaml` builds the playground without AOT on every push and pull request, so a change to the core library that breaks only the WASM build fails there and not at the release.
- GitHub Pages needs a `.nojekyll` file, and the build emits both fingerprinted and non-fingerprinted asset names, as in seiton. The repository's Pages source must be **GitHub Actions**.
- The build is trimmed and AOT-compiled, with invariant globalization. The transfer size of `_framework/` with Brotli was 2.4 MB for 1.5.0, within the 2–5 MB range that the seiton playground measured.

## Verification

`tests/Scenetake.Core.Tests/PlaygroundReplayTests.cs`:

| Test | Checks |
|---|---|
| `SamplesReplayToCommittedCasts` | For each sample with an outputs file, in `samples/` and `samples/showcase/`, the outputs file and the YAML give the committed `.cast` and `.svg` byte for byte, and every step is recorded. The replay has only the project's `PlaygroundImage` images, as the page does, so a sample that names an image the playground does not publish fails. |
| `RecordedScenarioReplaysToSameCast` | With `SCENETAKE_BIN`, the CLI records `tests/fixtures/playground-replay.yaml` (instant and streamed pipe output, stderr, highlights, a repeated command, and OSC titles in pipe and PTY output) with `SCENETAKE_OUTPUTS`. The replay gives its cast and SVG byte for byte, and the outputs file holds no OSC text. |
| `MaskedOscGivesSameFilteredEvents`, `MaskKeepsColorOscAndLength`, `MaskCarriesStateAcrossChunks`, `OutputsFileMasksPrivateOsc` | Masking keeps color OSCs and lengths, follows OSCs split between chunks, and gives the cast's filter the same result as the original bytes |
| `OutputsFileRoundTripsExactly` | Times, bytes, cursors, and text survive a write and a read exactly; lines end with LF |
| `NthOccurrenceTakesNthRecording`, `ExtraOccurrenceTakesLastRecording`, `UnrecordedStepGetsPlaceholderLine`, `KindMismatchGetsPlaceholderLine`, `EditsBesidesRunKeepTheMatch`, `PtySizeMismatchGivesNotice` | The matching rules and notices |
| `RecordedExitCodeWarns`, `ErrorsAreValuesNotExceptions`, `NotAppliedKeysAreListed`, `CastTabAppliesOverrides` | Warnings, errors as values, not-applied keys, and the Cast tab's options |
| `LengthAppliesInThePlayground` | `--length` is kept in the Scenario tab's cast and drawn as the `svg` command draws it, replaces a cast's speed on the Cast tab, and gives the CLI's errors with `--speed` or out of range |
| `PlayedLengthIsShown` | The status line's length is the `svg` command's: as recorded, at a speed, with end-hold and fade-out, and fitted to a length. An error gives none |
| `BackgroundImagesComeFromThePage` | An image named in the YAML or the option, by name or by a Unix or Windows path, is embedded in the SVG and kept out of the cast, on both tabs. A missing image's error names the loaded ones. |

The browser page itself was checked by hand on 2026-11-15: the runtime starts in the worker, `basic` renders in about 140 ms and `pty` in about 60 ms, edits and option changes re-render, a share URL restores the sample, YAML, and options, and the page fits a 375 px screen in dark and light themes.

## Cross-Document Notes

- [spec_scenario.md](spec_scenario.md) — the keys that a replay honors, step exit codes, and determinism.
- [spec_cast.md](spec_cast.md) — the cast format, and the sequences that the outputs file masks.
- [spec_svg.md](spec_svg.md) — the renderer that both tabs use.
- [spec_cli.md](spec_cli.md) — the render flags that the options mirror, and `SCENETAKE_OUTPUTS`.
- [spec_pty.md](spec_pty.md) — the PTY setup filter and start cursor, and the interactive input log that the outputs file never holds.

## Lessons Learned

### Carried over from the seiton playground

- An unhandled exception that crosses the WASM interop boundary stops the runtime for the rest of the page's life. Each export catches every exception and returns an error value.
- A synchronous WASM call blocks the thread that makes it. Here the runtime runs in a Web Worker instead of the page's main thread, so typing never waits, and the page sends a new render only after the previous one answers.
- On GitHub Pages, the `_framework/` folder needs `.nojekyll`. Pages has no import map rewriting, so assets need both fingerprinted and plain names.
- `Microsoft.NET.Sdk.WebAssembly` (`wasm-experimental`) was chosen over Blazor WebAssembly. The trimmed AOT transfer size is 1–5 MB with it, against 5–15 MB with Blazor.

### Building this playground

- **Removing OSC text from the outputs file would have changed the replayed cast.** The cast's filter removes an event that it empties, but it keeps an event that was already empty. A PTY chunk that held only a window title would become an empty chunk in the outputs file, and the replay would keep an empty event where the run had none. Replacing the text with `x` keeps every chunk's length and every byte that the filter decides on, so the filter removes the same events from both.
- **PTY chunks are stored after the setup filter, not as read.** Stored before it, they would hold the ConPTY title with the shell's path. Stored as the cast has them, already timed, `execution-duration` and the output timing keys would stop working in a replay. Between the two, the replay starts the timing from the same chunks that the run did.
- **The committed samples were not all in step with their YAML.** `demo.cast` came from an earlier version of `demo.yaml`: its timestamp, which then came from the YAML text, differed. The equivalence test catches this for every sample that has an outputs file.
- **Samples that need a person, Docker, or a clean tree are recorded by hand.** `interactive-pty` and `password-prompt` need keys typed at a real terminal, `demo` runs `scenetake` from `PATH` and Docker, and `git` shows the working tree's state. Each gets an outputs file only when it is recorded again, and until then the playground does not list it.

### Using it as its readers would

On 2026-11-15 the page was used as four readers: an author who writes a CLI by hand and shares on a README, a library author who writes on Zenn, an author who builds with an AI assistant and posts on X, and a reader who opens a shared link on a phone.

- **A shared link on a phone showed the YAML, not the demo.** The open options bar and the editor came first, and the preview started about 1600 px down. Fitted to a 375 px screen, an 80-column terminal had text about 6 px high. A reader who follows a link from X is often on a phone. The preview now comes first below 960 px, with text as large as the editor's on a phone.
- **A list of every step said almost nothing.** The UI first listed each step with a "recorded" or "not recorded" tag. Every step of a sample as published is recorded, so the list repeated the YAML and took space under the preview. Only the steps that differ from a plain replay are named now. The placeholder line in the preview already shows a missing recording, and the note gives the step's number.
- **Closing the options was not enough.** With the options bar closed, it still stood between the tabs and the preview, the header took four lines, and the toolbars wrapped. The preview started at 417 px and the YAML at 744 px, below the first screen of a phone. Someone who opens the page to try it should see the demo and the YAML first. The options moved under the preview, and the phone header and toolbars were tightened.
- **Fitted to an upright phone, a whole terminal is too small to read.** `basic` is 829.6 × 504 px with 16 px text. In the page it showed at 0.40 of that, with text about 6 px high. Making the preview much larger in the page would push the YAML out of the first screen again, and **Actual size** cut off the right half of the terminal.
- **No web standard gives a minimum text size, so the page sets its own.** WCAG has no minimum, and its Reflow criterion (1.4.10) exempts content that needs a two-dimensional layout, such as video, which a terminal recording is. The guidelines that give a number give more than a fitted terminal reaches: Lighthouse counts text under 12 px as too small on mobile, and Apple's guidelines set 11 pt as the minimum. An 80-column terminal at 8 px is 415 px wide with its padding, wider than most phones.
- **On a phone, the demo's text has to be as large as the editor's.** An 8 px floor came first, with the page view as an overview: it kept 74 of `basic`'s 80 columns in view on a 375 px phone. The demo still read as too small, because the reader sees it next to the YAML at 13 px. Now the floor below 600 px is the editor's size, and the preview scrolls sideways as the editor does. Its first 44 columns hold the prompt and most commands. The YAML starts lower, at 679 px instead of 516 px on a 375 × 812 screen, but stays on the first screen. **Full screen** shows the whole terminal: sideways and with no margin, it shows that text at 11.9 px. An 80 × 24 terminal (1.65 : 1) is less wide for its height than a phone held sideways (about 2.2 : 1), so the phone's short side sets that size. Wider screens keep the 8 px floor. There the result column fits `basic` at 9.4 px or more, and **Actual size** is one click away.
- **An explicit min-width let the flex layout shrink the SVG.** An image in a flex row does not shrink below its own size while its min-width is `auto`. Once the image had a min-width for the text floor, **Actual size** showed the SVG at the column's width instead of its own, until the image was given `flex: none`.
- **Only a README takes the SVG.** Zenn takes uploads of PNG, JPEG, GIF, or WebP up to 3 MB. X takes GIF and H.264 mp4, and Reddit takes GIF and mp4. The page made only SVG and never said so, so three of the four readers could not tell whether scenetake fit. The page now gives the output and command for each place.
- **The first demo looked like it had failed.** `basic.yaml` ran `curl -I`, whose progress meter goes to stderr, so the README's first recording showed a red table of zeros. It now runs `curl -sI`, which prints the same headers.
- **A pasted AI-style scenario found a CLI bug.** `typing_speed` under `settings` took effect while the warning said it had no effect, and `typing-speed: 50ms` gave the YAML library's message without the key ([spec_scenario.md](spec_scenario.md) → Lessons Learned).
- **Sample names did not say what they show.** `pty` and `stream-output` mean little to a new reader, and the Copilot CLI demo, which shows an AI assistant driven from the keyboard, was only in the Cast tab's list as `interactive-pty`. Each sample now has a note, and the Scenario tab points to the Cast tab for these demos.
