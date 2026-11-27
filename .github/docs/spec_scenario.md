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

A step's timing key that is not a number is a warning. The step then uses the value from `settings`, or the default: `Warning: typing-speed: '50ms' is not a number of seconds (such as 0.5), using 0.05`. A step reads a number as `settings` does: `0,05` (a decimal comma, which YAML reads as text) is not 5, `(1)` is not -1, and infinity is not a time. A step's flag that is not `true` or `false` also warns.

### Commands that contain `: `

YAML reads `: ` in an unquoted value as the start of another mapping. A command such as `run: read -p "Deploy? " a; echo "answer: $a"` is then a YAML error, which stops the run before any command runs. After the library's message, scenetake shows the line and the same line with the value in single quotes, each `'` inside doubled:

```text
Error: cannot read demo.yaml: Mapping values are not allowed in this context at Line: 5, Col: 43, Idx: 92
  5 |     run: read -p "Deploy? " a; echo "answer: $a"
A command that contains ": " needs quotes. Put it in single quotes, with each ' inside written as '':
      run: 'read -p "Deploy? " a; echo "answer: $a"'
```

An unquoted list item, `- echo key: value`, parses as a mapping instead, and the step error names its key (Forms below).

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
| `cwd-as` | — | The path the recording shows for `cwd`, or for the caller's folder without it, wherever a command prints it, such as `~/my-app`. The same on every machine. The prompt's `{dir}` there shows its last name. See [Paths in the recording](#paths-in-the-recording). |
| `shell` | `$SHELL` / `pwsh` | Shell for all commands. Values: `bash`, `pwsh`, `powershell`, or a path |
| `outputs` | — | The files a recording writes besides the cast, each with its own format and options, such as a dark and a light SVG, a GIF, and an mp4. Written when no `--format` is given. See [spec_cli.md](spec_cli.md) → Outputs list. |

On Linux and macOS, `pwsh` and `powershell` use PowerShell command arguments, including when selected by a path or by `$SHELL`. Other Unix shells use `-lc`. The requested executable name or path is preserved for both pipe and PTY commands, including `pre` and `post`.

## `settings`

Defaults for all steps. Map-form steps may override individual keys.

| Key | Default | Description |
|---|---|---|
| `prompt` | `$ ` | Prompt shown before each typed command: text, or a list of parts with a color each. `{dir}` shows the folder the step starts in. See Prompt below. |
| `typing-speed` | `0.05` | Average seconds per typed character |
| `typing-jitter` | `0.015` | Random variance (± seconds) per character |
| `pre-delay` | `0.2` | Pause before typing a step, with its prompt on the screen. See Pauses below. |
| `post-delay` | `0.5` | Pause after output before the next step's comment and prompt |
| `end-prompt` | `false` | `true` ends the recording with the prompt after the last output, as a terminal does. By default it ends on the last output. See Pauses below. |
| `execution-duration` | `0.05` | Minimum cast wait after Enter before step output appears (seconds). With default pipe emit, all output is written at `command_start + execution-duration`. |
| `stream-output` | `false` | When `true`, emit pipe/PTY output as timed chunks using wall-clock capture instead of a single instant blob |
| `max-duration` | — | When set, enables stream emit and linearly scales the step output timeline to this ceiling (seconds) |
| `silence-threshold` | `2.0` | Stream mode only: gaps longer than this between chunks are treated as idle (seconds) |
| `max-idle` | `0.2` | Stream mode only: collapsed idle gap duration (seconds) |
| `stderr-color` | `red` | Fallback color when stderr has no ANSI SGR. Value format: [spec_highlight.md](spec_highlight.md) |
| `mask` | — | A list of texts to hide wherever the recording shows them. See Masking secrets below. |
| `mask-auto` | `true` | Also hide what looks like a secret: known token formats, secret `KEY=value` pairs, URL passwords, and secret environment values. See Masking secrets below. |
| `mask-personal` | `true` | Also hide who records: the computer's name, and the git name and email, which `hostname` and `git log` print. The user name and the home folder's path show as they are. See Masking personal details below. |

### Pauses

From one step's output to the next step's typing:

1. The step pauses for its `post-delay` after its output.
2. The next step's comment line and prompt show, as a shell draws its prompt when a command ends.
3. That step's own `pre-delay` passes with the prompt on the screen, as a person pauses before typing. Then it types.

- **Steps without a prompt.** A `show: output` step's `pre-delay` passes after its comment, before its output. A clear step and a card pause for their `pre-delay` before they draw, and then for their `post-delay`.
- **The first step** shows its comment and prompt 0.5 s after the recording starts, then pauses for its `pre-delay`.
- **The end.** The recording ends on the last output: the cursor is hidden as soon as the output ends, and no prompt follows. With `end-prompt: true`, the closing prompt shows after the last step's `post-delay` instead. Either way the final screen stays 2 × (`post-delay` + `pre-delay`) of the last step after its output: an event that draws nothing ends the recording, and the render holds the screen as long again as the pause before it. An `end-hold` counts from when the final screen shows instead ([spec_svg.md](spec_svg.md) → Ending). A card at the end stays as it is ([Card steps](#card-steps)).
- **Why the output ends it by default** (2026-11-22). Once the closing prompt came right after `post-delay`, it held most of the final screen, 1 to 4 s in the samples, and the eye stopped on `$ █` instead of the result. The final screen is the one viewers look at longest, and the default image of a PNG and `--at end`, where the prompt was one more row. A looping SVG or GIF shows the end by starting again. The line under the output is not left with a lone cursor, which reads as a command still running. `end-prompt` keeps the terminal's ending for a demo that needs it, such as one that ends by quitting a full-screen program, where the restored screen without a prompt looks like the moment before the program starts.
- **Why the prompt comes before `pre-delay`** (2026-11-22). `pre-delay` used to pass before the next step's comment and prompt, and before the closing prompt. The line under a finished output then held only the cursor, which reads as a command that is still running. With `post-delay: 0` and `pre-delay: 0.5`, the welcome demo seemed to wait after its logo and at its end. A `pre-delay` set in a step also applied before the next step instead of its own.
- **Why the times stay.** The pauses add up as before, and the final screen stays as long, so a demo without a `pre-delay` set in a step plays as long as before, and only its comments and prompts show earlier. A recording made before 2026-11-22 keeps its old layout until it is recorded again or made again with `--replay`.

### Prompt

`prompt` is the text before each typed command, and at the end of the recording with `end-prompt: true`. It is text, as before, or a list of parts, each text or a mapping with `text` and an optional `color`. A color is a style as `highlight` takes one ([spec_highlight.md](spec_highlight.md)), and each colored part ends with a reset.

```yaml
settings:
  prompt: "{dir} $ "              # text, with the folder's name
  # or parts, each with its own color:
  prompt:
    - text: "demo"
      color: "bold fg:green"
    - " "
    - text: "{dir}"
      color: "fg:blue"
    - " $ "
```

- **`{dir}`** is the name of the folder the step starts in: its last name, `~` for the home folder, and `/` for the root, as bash's `\W` shows it. With `cwd-as`, the folder the steps start in shows the last name of what `cwd-as` gives: `my-app` for `~/my-app` ([Paths in the recording](#paths-in-the-recording)). The rest of the path never appears, so a recording names no user folder. Both `HOME` and the user profile are `~`, because PowerShell on Windows takes the profile as its home while Git Bash takes `HOME`, which may name another folder.
- **Which folder.** With a shell that carries the folder (Shell state across steps below), `{dir}` follows each `cd` in `pre`, in a shown step, and in a `show: none` step. The prompt is drawn before its step runs, so a step's own `cd` moves the next prompt. The closing prompt shows where the last step left. A folder that a step removed is not returned to, so the next prompt shows the scenario's `cwd`, as the next command starts there. The shell tells scenetake that the folder is gone, because a Git Bash path such as `/tmp/x` has no Windows path to look up. A shell that carries no state starts every command in `cwd` (or the caller's folder), and `{dir}` always shows that.
- **Replay and the playground.** When the prompt shows `{dir}`, the outputs file keeps each step's folder name and the closing one, masked as the prompt shows them (`folder`, `endFolder`, [spec_playground.md](spec_playground.md) → Outputs file), and a replay draws the same prompts. A file without them shows `~`. Without `{dir}`, the file keeps no folder names.
- **Errors and warnings.** A value that is neither text nor a list, a part that is neither text nor a mapping, a mapping without `text`, and a `color` that is not text are errors found before anything runs. An unknown style is a warning, and its part is drawn plain. A misspelled part key is an unknown-key warning.
- **The prompt is masked** as the recording's other text is, without counting it again at each step.
- **Width.** A prompt whose width changes from step to step starts each command at another column. On Windows a PTY step starts where the recording's cursor is, so ConPTY still draws it in place. A `keep-output` step whose PTY output would start elsewhere than in the earlier cast shows its new output (Recording again below).
- **Why `{dir}` and no other placeholder.** Since steps carry their folder, a prompt that shows it makes `cd my-app` visible as it would be in a terminal. A user name or a host name would name the person who recorded the demo, which scenetake keeps out of a published cast (`spec_cast.md`), and the prompt's text can name a fixed one.

### Output timing (pipe and PTY)

By default, pipe steps capture stdout/stderr in parallel during execution but write **one** cast `o` event at `command_start + execution-duration`. The event text is **stdout then stderr** (not chronologically interleaved), matching pre-stream-output merge order and keeping stdout `highlight` line numbers stable. This preserves typed-command playback and avoids choppy SVG/GIF output from read-boundary chunks or progress-bar streams.

Output with a NUL in it is kept in both modes, and draws nothing for the NUL, as a terminal and the cast's player do: `find -print0` shows the names run together. Until 2026-11-27 the one-event mode dropped the whole stdout of a step that printed a NUL, without a word, and stream mode kept it. asciinema writes the bytes as they come, and console2svg's emulator skips a NUL, so neither drops the output.

**Stream emit** is enabled when a step or `settings` sets `stream-output: true` or `max-duration`. In stream mode, scenetake records chunk arrival times, then applies:

1. Startup offset (align slow first chunk with `execution-duration`. Same rule as [spec_pty.md](spec_pty.md))
2. Burst merge (gaps under 50ms share a timestamp)
3. Idle collapse (`silence-threshold` / `max-idle`)
4. Linear compression when `max-duration` is set

PTY steps use the same stream pipeline only when stream emit is enabled **and** the step is not `interactive: true`. Otherwise only the existing PTY startup offset applies. Interactive PTY steps use wall-clock chunk times with `PtyLeadingInitFilter` only, unless `normalize-typing: true` is set ([spec_pty.md](spec_pty.md) → Interactive PTY).

Stream emit suits long-running commands (for example `docker build`) where replay pacing matters. Short demos, nested tool stderr, and progress-bar tools (for example `agg`) should usually keep the default instant emit. See Lessons Learned below.

### Masking secrets

A recording is made to be shared, and a command can print a secret by accident: `cat .env`, `env`, a verbose log, an error that echoes a connection string. scenetake hides secrets before anything is written: the cast, the SVG, the mp4, the PNG, the GIF, and what the playground replays from its outputs file never show them.

```yaml
settings:
  mask:
    - "internal.example.com"   # hidden wherever it appears
  mask-auto: true              # the default
```

- **What is hidden.** Every text in `mask`, and with `mask-auto` (on unless it is `false`):

  | Kind | Example | What is hidden |
  |---|---|---|
  | Known token formats | `ghp_…`, `github_pat_…`, `glpat-…`, `xoxb-…`, `AKIA…`, `sk-…`, `sk_live_…`, `AIza…`, `npm_…`, `pypi-…`, `hf_…`, `dckr_pat_…` | The part after the prefix |
  | A pair whose key names a secret | `API_KEY=…`, `"password": "…"`, `db.password = '…'`, `--password=…`, `Authorization: Bearer …` | The value. After `Bearer`, `Basic`, `Token`, `Bot`, or `Digest`, the credential that follows. |
  | A password in a URL | `postgres://app:…@db/app` | The password |
  | A Bearer token | `Bearer eyJ…` (16 or more characters) | The token |
  | A JSON Web Token | `eyJ….eyJ….…` | The payload and the signature |
  | A private key | `-----BEGIN … PRIVATE KEY-----` | The lines between BEGIN and END |
  | A secret environment variable | the value of `GITHUB_TOKEN`, `OPENAI_API_KEY`, `DB_PASSWORD` | Its value, wherever a command prints it, when it has 8 or more characters |

- **A key names a secret** when its last word is `password`, `passwd`, `passphrase`, `secret`, `token`, `apikey`, `credential(s)`, `privatekey`, `accesskey`, `secretkey`, or `authorization`, or it ends in `key` after `api`, `access`, `private`, `secret`, `signing`, `encryption`, `account`, `master`, `client`, or `subscription`. Words split at `_`, `-`, `.`, and camelCase. `token_endpoint`, `PASSWORD_FILE`, `max_tokens`, `PUBLIC_KEY`, and `KEY` do not name a secret.
- **A value is not a secret** when it is shorter than 4 characters, all digits, already stars, `null`, `true`, `false`, `none`, or `undefined`, or starts with `$`, `%`, `{`, or `<`, as references and placeholders do: `TOKEN=$GITHUB_TOKEN` stays. A quoted value with spaces is a sentence and stays.
- **How it is hidden.** Each hidden character becomes `*`, two for a wide character, so every line keeps its width and a full-screen program's screen stays in place. A token keeps its prefix and a pair keeps its key, so a reader sees what was hidden: `GITHUB_TOKEN=****`, `ghp_****`.
- **Where.** The output of every step (pipe, PTY, and interactive), the typed command, and the step's comment line and marker. pre and post are not recorded.
- **A secret split by color codes or across reads is found.** Each step's output is read as one stream (stdout and stderr each as their own). Color codes, an erase to the end of the line (`\e[K`, which grep writes around each match it colors), control strings that start with ESC (OSC, DCS, SOS, PM, APC), and controls that draw nothing, such as BEL, are skipped, so the text on either side is read as one run. Line breaks, cursor moves, and most other sequences end a run of text: words that a full-screen program draws in different places are not joined into one. A cursor move to the right counts as spaces, because ConPTY writes a run of spaces that way.
- **A control string ends at the first byte where the cast filter, the SVG renderer, or the terminal that plays the cast ends it.** That is BEL, CAN, SUB, ST, or the ESC of the next sequence, and for a string whose bytes the cast keeps, also a C1 control. The text after it is searched, even when an ST comes later. A streamed pipe step puts the reads of both streams in time order and colors a stderr read that has no colors of its own. A sequence that one read leaves open then ends where that color or the other stream's reads end it, before the next read of its stream, and the masker ends it there too. Bytes of the other stream that end nothing, such as a line feed inside a CSI, run inside the sequence in the cast without ending it, and the masker reads on through them. A string that nothing ends is read as text when the cast keeps its bytes (DCS, SOS, PM, APC, and color OSCs): the program was most likely cut off, and what follows is its own output. Not searched: the OSCs that the cast drops (titles, hyperlink targets, the clipboard, marks), and the data of a string that ends, such as a Sixel image or a soft font. No player draws that data, and image data would match a token format by chance, so the cast file keeps it as it is. A step's `highlight` is painted after masking and never writes a color inside a sequence ([spec_highlight.md](spec_highlight.md)).
- **The CLI says what it hid**, by kind, such as `Masked 3 secrets: 2 key=value secrets, 1 GitHub token`, so stars in a recording are never a surprise. With `mask-auto: false`, only the `mask` texts are hidden. Each secret counts once: a run of hidden text is one secret, of the kind that found its first character first, and a text counts again only when it differs. `GITHUB_TOKEN=ghp_…` is found by the GitHub token rule and the key=value rule, and a command that prints it shows it twice, typed and printed: one GitHub token.
- **The playground.** The outputs file holds the masked output, so a secret that masking finds never leaves the recording machine. The playground masks again with the same rules, which hides what an edited YAML adds, such as a new `mask` text. It has no environment, so the masking of environment values happens only when the CLI records. The outputs file keeps each step's `run` text as the YAML has it, because a replay finds the step by that text. A replay, in the playground or with `--replay`, types each command as the outputs file holds it (`maskedRun`) and masks that again, because a secret that `pre` or an earlier step exported is known only to the recording. The file masks the text after the step ran, so it also hides a secret that the step itself exports, which the recording typed before it knew.

**Why on by default.** Console2Svg masks secrets by default as well. A demo that shows a real token is a leak that cannot be taken back once the SVG is in a README. A false positive costs a few stars, which `mask-auto: false` turns off.

**Tests.** `tests/Scenetake.Core.Tests/SecretMaskTests.cs` checks each rule, the texts each must leave alone, chunk and color splits, control strings that end early or never (also between streamed pipe reads), wide characters, and that the cast and outputs file of a recording hold no secret. It puts every fake token together at run time, so no file holds a whole token that a secret scanner would report. `tests/Scenetake.Core.Tests/PlaygroundReplayTests.cs` replays every committed sample with masking on, which would show a false positive as a changed cast.

**Why no regular expressions.** `mask` takes plain texts, and the automatic rules are hand-written scanners. .NET's regular expression engine would add hundreds of kilobytes to the Native AOT binary, which links none today. A secret that is known before the recording, which a pattern would describe, is usually in the environment or can be listed in `mask`.

### Masking personal details

The prompt never names the machine that records (`{dir}` shows a folder's name, Prompt above), but commands do, without being asked: `git log` prints the author's name and email, and a child shell's prompt, `hostname`, or the address of a local server prints the computer's name. `mask-personal` hides them, as `*` like a secret. It is on unless it is `false`, and it does not depend on `mask-auto`. The user name and the home folder's path show as they are (Why the user name and the home folder show, below).

```yaml
settings:
  mask-personal: false         # show the computer name and git identity
```

- **What is hidden.** The details of the machine that records, collected when the recording starts:

  | Kind | From |
  |---|---|
  | Computer name | The machine's name, and on Windows also its DNS host name, which `hostname` prints: the other is cut to 15 characters |
  | Git name and git email | `user.name` and `user.email` of the global and system git config and of the command scope (`GIT_CONFIG_COUNT` or `GIT_CONFIG_PARAMETERS` in the environment, which the steps get too), as git reads them in the scenario's `cwd` (an `includeIf` for that folder applies), and `GIT_AUTHOR_NAME`, `GIT_AUTHOR_EMAIL`, `GIT_COMMITTER_NAME`, and `GIT_COMMITTER_EMAIL` |

- **Where.** Wherever the masking of secrets looks: every step's output, the typed command, comments, and cards. It is found across reads and color codes as a secret is.
- **As a whole word, case ignored.** A detail is hidden where the characters on either side are not ASCII letters, digits, `_`, or `-`: `ada@adas-laptop:~$`, `adas-laptop.local`, `Author: Ada Lovelace`. Git name `ann` in `annual`, `ann-marie`, or `ann_x` is another word and stays. Case is ignored, because Windows gives the computer name in upper case while `hostname` prints it as it was set.
- **A published name stays.** A git name inside a web address or after a leading `@` stays: `https://github.com/ada`, `github.com/ada/tool`, `git@github.com:ada/tool.git`, `@ada/tool`, `@ada`. There it is the name the author publishes under, which a demo shows on purpose. A word is in an address when the text before it, back to a space, a quote, or a bracket, holds `://`, or starts with a host name that has a dot (`github.com`) before a `/` or `:`. A path is not an address: `/home/ada`, `C:/Users/ada`, `./ada`, `~/ada`, `D:\src\ada`, and a file URL, `file:///C:/Users/ada`, which names a path too. A computer name and an email are not published names, and are hidden in an address too.
- **What is not hidden.** The user name and the home folder. A name shorter than 3 characters, one that is all digits, and names that name no person, which containers, CI runners, and cloud images use: `root`, `admin`, `administrator`, `user`, `guest`, `nobody`, `demo`, `test`, `dev`, `runner`, `runneradmin`, `ubuntu`, `debian`, `vscode`, `node`, `codespace`, `gitpod`, `docker`, `vagrant`, `ec2-user`, `app`, `api`, `web`, and `localhost`. Hidden, they would star words that demos print for other reasons, such as `npm run dev` or the `root` folder, and a recording in CI would differ from one made locally. An email is always hidden. A repository's own `user.name` and `user.email` are not: a demo repository may set an identity on purpose. Add one to `mask` to hide it.
- **A git name that is also the user name** is hidden as a git name, in a path too: the masker cannot tell which one a word is.
- **The CLI says what it hid**, on its own line, by kind: `Masked personal details: computer name, git email (settings.mask-personal: false shows them)`. It names no value: a CI log may be public.
- **Replay and the playground.** `--replay` masks again with the personal details of the machine that replays, as it does with its environment. The playground has no machine to read, and the outputs file was masked when it was recorded.
- **Collecting.** One `git config` process reads the git identity, from git on PATH, never from the current directory. Without git, or when it fails, there is no git detail to hide. A git older than 2.26 has no `--show-scope`, and then the global config alone is read.

**Why on by default.** A recording is made to be shared, and the details leak without a sign: the author looks at the demo, not at the author line of a commit or the host in a local address. Console2Svg also masks the git committer by default.

**Why a published name stays.** For many developers the git name is also their GitHub account. Hidden everywhere, it would star `github.com/<name>/<tool>` in an end card or in `git remote -v`, which the author shows on purpose.

**Why the user name and the home folder show** (2026-11-28). They were hidden too, the user name as stars wherever it stood as a word from 2026-11-21, and the home folder's path as `~` from 2026-11-24. Paths name them in nearly every recording, through `pwd`, an error, a stack trace, or `TEMP`. Hiding them did little that the author could not choose better: `cwd-as` shows the demo's folder as the same path on every machine, and `mask` hides the user name wherever it shows. Whether the name shows is the author's call, so scenetake no longer makes it.

**Why whole words and a list of generic names, not every occurrence.** A name is often a short common word. Hiding `ann` in `annual`, or `dev` in `npm run dev` on a machine named `dev`, would make stars appear where nothing personal was. The list keeps out the names that come with a container or a CI runner. A personal name that is also a common word still stars that word: the CLI's line names the kind, and `mask-personal: false` turns it off.

### Paths in the recording

Commands print full paths without being asked: `pwd`, an error that names a file, a stack trace, a tool's JSON. Each names a folder of the machine that records, so one scenario shows `/c/Users/<name>/src/my-app` on one machine and `/home/runner/work/my-app/my-app` on a CI runner. The folder the steps start in can show as a stand-in instead:

```yaml
cwd: ./demo
cwd-as: ~/my-app     # pwd prints ~/my-app here, on every machine
```

- **`cwd-as` gives what the folder the steps start in shows as**: `cwd`, or the caller's folder without it. It holds with `mask-personal: false` too, since it is the scenario's choice and no detail of the machine. The prompt's `{dir}` there shows its last name. A folder above it, the home folder included, shows as it is. A blank `cwd-as`, or one with a line break, is an error found before anything runs.
- **Every spelling.** A Windows folder is found in each way a command writes it: `C:\Users\Ada\demo`, `C:/Users/Ada/demo` (Git Bash's `pwd -W`, Node.js), Git Bash's `/c/Users/Ada/demo`, and JSON's `C:\\Users\\Ada\\demo`. The stand-in's `/` and `\` are written as that spelling writes them: `cwd-as: ~/my-app` shows as `~\my-app` where PowerShell prints the folder, and as `~\\my-app` in JSON. The 8.3 form of the folder, such as `C:\Users\ADALOV~1\demo`, is found too. On Unix, so is the folder with its links resolved, which `pwd` prints when the shell reads the folder from the system: `/private/var/...` for `/var/...` on macOS. Case is ignored, as for the personal details.
- **Only a whole path.** A path is the folder or a path under it, and it stands apart: no letter, digit, `_`, `-`, `.`, `~`, `/`, or `\` before it, and no name going on after it. For `/work/demo`, `/work/demo2`, `/work/demo.old`, WSL's `/mnt/c/work/demo`, `\\?\C:\work\demo`, and `file:///C:/work/demo` are not the folder, and show as they are. A root folder (`/`, `C:\`) has no stand-in: every path starts with it.
- **Where.** Wherever masking looks: every step's output, the typed command, comments, and cards. A colored path keeps its colors, and a path split across two reads is found, as a secret is.
- **A full-screen program keeps the path.** On the alternate screen, where vim, less, and htop draw, a path keeps its width and shows as it is. So does a path that a cursor move cuts.
- **Replay and the playground.** The outputs file holds the stand-ins as the recording showed them. `--replay` finds the folders of the machine that replays, which an outputs file recorded elsewhere does not hold, so a path recorded without a stand-in stays as it was recorded. The playground has no machine.
- **Typing.** A step's typing comes from its run text after masking (Determinism below), so a run that names `cwd` types the same on every machine with `cwd-as`. A run that names the home folder types as its path is on each machine, and types differently once, compared with a cast recorded from 2026-11-21 to 2026-11-28.

**Why a stand-in, and not a container or a folder made for the recording** (2026-11-24). The evaluation of 2026-11-23 found that a scenario cannot hold its stage, and `pwd` printed `/c/Users/**********/AppData/Local/Temp/...`. A container makes the paths the same, but asks every author for Docker. A folder that scenetake made for the recording would not help: it would be under the temp folder, as in `/c/Users/<name>/AppData/Local/Temp/scenetake-k3j2/demo`, which still names the machine and changes with every run, so each recording would differ from the last. A stand-in needs neither, and gives the same path on every machine.

**Why the width changes here.** The masking of secrets keeps each line's width, so that a full-screen program's columns stay where they were. A stand-in is shorter than its path. Line output does not mind: ConPTY on Windows 11 passes a program's line output through as written, a progress line redrawn after `\r` included (checked 2026-11-24 with a PTY step), so each draw is shortened alike. A full-screen program places text by column and redraws only the cells that changed, so a shorter path would put its later draws in the wrong cells. There the width stays.

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
| `speed` | `1` | How many times as fast as recorded the SVG, mp4, and GIF play, `0.25`–`4`. The cast keeps the recorded times. `end-hold` and `fade-out` stay seconds of the output. See [spec_svg.md](spec_svg.md#playback-speed) → Playback speed. |
| `length` | as recorded | The most seconds the SVG, mp4, and GIF play, `1`–`600`: waits in output shrink first, then pauses, then typing, each down to a floor. A shorter recording stays as it is. The cast keeps the recorded times. Not with `speed`. See [spec_svg.md](spec_svg.md#length) → Length. |
| `fit` | `height` | `none`, `height`, `width`, or `both`: move the bottom edge (`height`), the right edge (`width`), or both in to the last row or column the recording shows, the cursor included, so no empty rows or columns are drawn. The cast keeps `width` and `height`. See [spec_svg.md](spec_svg.md) → Fit and crop. |
| `crop-top`, `crop-bottom`, `crop-left`, `crop-right` | `0` | Cells cut from that edge before `fit`, `0`–`511`. A crop that leaves no row or no column is an error before anything runs. |

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
- `samples/theme-presets/` has one preview per theme: `palette.cast` (text styles, the 16 colors as text, then as backgrounds) rendered with `--theme NAME --window macos --window-title NAME`. `samples/regenerate.cs` renders them, and `-- --theme-presets` renders them alone. `tests/Scenetake.Core.Tests/SvgRenderTests.cs` checks each theme's colors and that every preview is current.

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
| `id` | no | A name for `--at` and an outputs entry's `at`, which then show the screen right after this step's output. A letter or `_`, then letters, digits, `-`, and `_`. Not shown in the demo. See Step ids below |
| `typing-speed`, `typing-jitter`, `pre-delay`, `post-delay`, `execution-duration` | no | Override `settings` for this step |
| `stream-output`, `max-duration`, `silence-threshold`, `max-idle` | no | Override `settings` output-timing keys for this step |
| `run-highlight` | no | Style for typed command text |
| `highlight` | no | List of `{ color, at }` for command output, by line and column. With `pty: true`, by the lines the program writes, over its own colors. A warning with `interactive: true`, which it does not color |
| `stderr-color` | no | Override `settings.stderr-color` for this step. A warning with `pty: true`, where stdout and stderr are one stream |
| `pty` | no | default `false`. Pseudo-terminal capture for TUI commands. Recording behavior: [spec_pty.md](spec_pty.md) |
| `interactive` | no | default `false`. Requires `pty: true`. Human operates the host terminal until the child exits. Recording behavior: [spec_pty.md](spec_pty.md) → Interactive PTY |
| `continue-on-error` | no | Default `false`. Allow a non-zero step exit without a warning, including with `--strict`. Use for expected failures. See Step exit codes. |
| `normalize-typing` | no | Default `false`. Requires `interactive: true`. Adjust visible typing to `typing-speed` during host-input windows. Remove Backspace corrections. Other output keeps wall-clock timing ([spec_pty.md](spec_pty.md) → Interactive PTY). |
| `keys` | no | Requires `pty: true`, and does not apply to `interactive: true`. A list of entries that scenetake writes to the PTY while the command runs, in order. Each entry has `sleep` (seconds to wait first), `wait` (text to wait for in the output), `type` (text), or `key` (a key name), and not both `type` and `key`. `--save-keys` writes these entries from what a person types in an interactive step. Behavior and key names: [spec_pty.md](spec_pty.md#keys-and-timeout) → Keys and timeout. |
| `timeout` | no | Seconds, above 0. When the command is still running after that time, scenetake stops it: in a PTY with Ctrl+C first, in a pipe by ending its process tree. After a pipe step's stop, its output is read for 2 s more at most: a process the kill missed, such as a daemon, can keep the pipe open for as long as it runs. A stop by timeout is not a failure: no exit-code warning, and `--strict` goes on. Does not apply to `interactive: true`. See [spec_pty.md](spec_pty.md#keys-and-timeout) → Keys and timeout. |
| `show` | no | Default `all`. What the recording shows of the step: `all` (the prompt, the typed command, and the output), `output` (the comment line and the output, without the prompt and the typing), or `none` (nothing). Case does not matter. See What a step shows below. |
| `background` | no | Default `false`. `true` starts the command and goes on while it runs, such as a server that later steps call. It is not recorded, and stops before `post`. See Background steps below. |
| `wait` | no | Requires `background: true`. Text the command prints when it is ready, such as `Listening on`. The next step starts when the output shows it. |
| `wait-timeout` | no | Requires `wait`. Seconds above 0 to wait for the text. Default `30`. A value above about 24 days waits that long, as `timeout` does. It used to stop the run with an error from the runtime. |
| `keep-output` | no | Default `false`. `true` shows the step's output from the earlier cast at the output path, even when the command now prints something else. The command still runs. See Recording again below. |
| `rerun` | no | Default `true`. `false` shows the step's output from the earlier cast at the output path without running the command, when that cast has the step. `--rerun` runs it. See Recording again below. |

Two kinds of step run no command and have no `run`: `- clear: true` (Clear steps below) and `- card: ...` (Card steps below).

Coloring value formats, range grammar, and validation: [spec_highlight.md](spec_highlight.md). How `highlight` reads PTY output: [spec_highlight.md](spec_highlight.md#pty-output) → PTY output.

### What a step shows

| `show` | Prompt and typing | Output | Time in the cast | For |
|---|---|---|---|---|
| `all` | Yes | Yes | Yes | A command the viewer watches being typed, as before |
| `output` | No | Yes | Yes | A title or a banner (`figlet`, `printf`), or a program that is already open on the screen |
| `none` | No | No | None | A step that prepares the next ones between scenes: create a file, clone a repository, `cd`, `export` |

- A `show: none` step runs as `pre` does, between the steps around it. Its folder, variables, functions, and aliases carry to the next step (Shell state across steps below). It is not in the cast or the outputs file, and the playground skips it.
- **A failing `show: none` step stops the recording,** with or without `--strict`, unless it sets `continue-on-error: true`. No cast is written, and `post` runs. The steps after it rely on what it prepares, and the recording would not show why they fail. The error names the step and its last output lines.
- The keys that change only how a step looks are errors with `show: none`: `name`, `run-highlight`, `highlight`, `stderr-color`, `pre-delay`, `post-delay`, `execution-duration`, the output-timing keys, `keep-output`, and `rerun`, as the cast holds no output of the step to show instead. `run-highlight` is an error with `show: output`, which types nothing. `pty`, `keys`, `timeout`, `interactive`, and `continue-on-error` work with every value.
- A scenario whose steps all show nothing has no steps to record, which is an error.

### Background steps

A step with `background: true` starts its command and lets the next steps run while it does: a development server that a later `curl` calls, a file watcher, a mock API. The recording shows nothing of it, and it takes no time in the cast.

```yaml
steps:
  - run: npm run dev
    background: true
    wait: "Local:"          # the next step starts when the output shows this
    wait-timeout: 60        # default 30 seconds
  - curl -s localhost:5173/api/hello
```

- **Start.** The command runs as a pipe step does, with the shell state so far (Shell state across steps below): it starts in the folder and with the variables of the steps before it. What it changes itself does not carry, because it has not ended. Its stdin is closed, so it never reads the keyboard.
- **Ready.** Without `wait`, the next step starts at once. With `wait`, scenetake reads stdout and stderr as they come and starts the next step when either shows the text, compared as a `keys` entry's `wait` compares ([spec_pty.md](spec_pty.md#wait)): without escape sequences, with runs of spaces and line breaks as one space. stderr shows `ready after 1.2 s: "Local:"`.
- **Not ready.** When the command exits before the text, or the text does not come within `wait-timeout`, the recording stops, as for a failing `show: none` step: no cast, `post` runs, exit `1`. The error says which (`exited 1 before it printed "Local:"`, `did not print "Local:" within 30 s`) and shows the command's last output lines. With `continue-on-error: true` it is a warning instead, and the steps go on.
- **Stop.** After the last step, before the cast is written, scenetake ends each background command with every process it started, and stderr shows `stopped (background): ...`. It also ends them before `post` when the recording stops with an error, and when scenetake itself is stopped with Ctrl+C. A command that exited by itself with a non-zero code before the end gets a warning, unless the step sets `continue-on-error: true`.
- **Keys.** A background step takes `wait`, `wait-timeout`, and `continue-on-error` only. Every other step key, `name` and `show` included, is an error: the step is never typed or recorded, and has no terminal of its own.
- **Why `wait` is 30 seconds by default** and not the 10 of a `keys` wait: a server or a build watcher compiles before it listens, which takes longer than a prompt takes to appear.
- **Why the stop is not graceful.** A pipe has no terminal to press Ctrl+C in, so the processes are ended, as a pipe step's `timeout` ends them. A command that must clean up on exit does that in `post`.

### Clear steps

A step `- clear: true` clears the screen and moves the cursor to the top left, as `clear` does, without running a command. It writes `ESC [H ESC [2J` into the cast, so it works without `TERM`, on every OS, and in the playground. It takes `name`, which prints a comment line at the top of the cleared screen, `id` (Step ids below), and `pre-delay` and `post-delay`. Any other step key, `run` with `clear`, or a value other than `true` is an error.

- **Why not `run: clear`.** That types the command, and `clear` fails in a pipe without `TERM`, as in CI. With `show: output` it would still depend on the host's terminal database, and on Windows on the shell.

### Card steps

A `card` step shows lines of text centered on a cleared screen, without running a command: a title before the demo, a scene's heading, or an install line and a URL at the end.

```yaml
steps:
  - card:
      - "[bold fg:bright-cyan]my-app"
      - "Fast JSON diffs in your terminal"
      - ""
      - "[fg:yellow]npm i -g my-app"
    post-delay: 2.5
  - my-app diff a.json b.json
  - card: "Get it: npm i -g my-app"
```

- **The lines.** `card` is a line of text or a list of lines. A YAML block (`card: |`) gives one line per line of the block. A number is text as YAML reads it, so `1.10` shows as `1.1`: quote such a line. An empty entry (`""` or `~`) is an empty line that keeps its row. Each line may start with a `[style]` prefix, as `name` takes one ([spec_highlight.md](spec_highlight.md)). Without one, a line takes the terminal's foreground. An unknown style is a warning, and the line is drawn without it. The spaces around a line are left out, because a card centers it.
- **The screen.** The card hides the cursor, clears the screen, and draws each line in the middle of the window below. A wide character takes two columns, and a combining mark or a character the terminal draws nothing for, such as a zero-width joiner, none. The text is masked as the recording's other text is.
- **The window it is centered in.** The part of the terminal that the output shows: the terminal less the scenario's crop, with the edges that fit moves in where the rest of the recording ends, found as the render finds them ([spec_svg.md](spec_svg.md), `fit: height` is the default). Where fit moves an edge in, the window keeps a blank row or column on each side of the largest card. So the canvas that fit keeps does not grow for a card, the card sits in its middle, and a crop does not cut it. With `fit: none`, the window is the terminal less the crop. The CLI and the playground pass the scenario's fit and crop, the CLI with `--fit` and `--crop-*` on top. An outputs entry with its own fit or crop draws the cast as it was recorded.
- **A still of a card stays centered.** A still (PNG or `--at`) keeps only the rows and columns that its own screen uses, which would end at the card's text. So the card puts a bold space, which shows nothing, in the window's last cell, which keeps its last row and column.
- **Time.** The card stays for `post-delay`, 2 seconds by default instead of the settings' value, because a title and an install line take longer to read than a command's output. `pre-delay` passes before the card shows, as before a clear step.
- **After it.** The next shown step starts on a cleared screen with the cursor shown again, as after a clear step. A `show: none` or background step between them runs while the card stays. A card that ends the scenario stays for its `post-delay`, and no prompt follows it: an event that changes nothing ends the recording then, because the render holds the last screen only for the gap before it.
- **Keys and errors.** A card takes `id`, `pre-delay`, and `post-delay` only. `run` or `clear` with `card`, another step key, a value that is not text, a card with no text, a line wider than the terminal, more lines than its rows, and a control character in a line are errors found before anything runs. A list entry that YAML reads as a mapping, an unquoted line with `: ` in it, gets a hint to quote the line. A card that the crop would cut, with more lines than the rows the crop leaves or a line wider than its columns, is an error too, found once the command line's crop is known. A scenario of cards alone is a recording, unlike one of clear steps alone.
- **Why centered.** A card is a screen of its own, a title or an end screen, and the middle of the screen is where a reader looks for one.
- **Why in the window the output shows, not the whole terminal.** Centered in all 24 rows, a card in a demo that used 6 of them made fit keep 13 rows, and the card sat near the bottom of the canvas. Centered in all 80 columns with `fit: both`, it sat at the right edge, and with `crop-top` its first line was cut. The window is known only after the last step, so each card's event is drawn then.
- **The window comes from the render's own fit** (2026-11-20). The rows in use were first taken from the cursor after each event. A step that writes a low row and moves the cursor home in one event, as a full-screen program does, was missed: fit kept the row, and the card sat high.
- **Why a step and not `show: output` with `printf`.** A card centers by itself at any terminal size, takes styles without escape codes, works the same in bash and PowerShell, and needs no recording, so the playground and `--replay` show it too.

### Step ids

A step's `id` names it for `--at` and an outputs entry's `at`, which then show one screen of that step instead of a time in seconds:

```yaml
steps:
  - card: ["[bold]my-app"]
    id: title
  - npm install
  - id: compare
    run: my-app diff a.json b.json
outputs:
  - format: svg              # the demo
  - format: png
    at: compare              # the screen right after the diff, for a preview image
    file: demo-result
```

- **The screen.** For a command, the screen right after its output, before its `post-delay` and the next step's comment and prompt. For a card, the card. For a clear step, the cleared screen with its comment line.
- **Where it works.** The scenario path (`--format svg` or `png` with `--at compare`), an outputs entry's `at`, `--replay`, and the `svg` and `png` commands on the cast: the cast keeps each id and its moment in its `st:ids` tag ([spec_cast.md](spec_cast.md)), so the cast alone resolves it.
- **The rules.** An id is a letter or `_`, then letters, digits, `-`, and `_`, as GitHub Actions takes a step's id. So `--at` reads a number as seconds and `end` as the final screen, never as an id, and a shell needs no quotes around it. `end` in any case is not an id. Ids are case-sensitive, and two steps with one id are an error. A `show: none` step and a background step have no screen, so an `id` there is an error.
- **Errors before anything runs.** An `at` that names an id no step has stops the run before `pre`: `--at nope: no step has this id. Give the step 'id: nope'.`, named after its outputs entry when it comes from one. On a cast alone, an id the cast does not have names the ids it has, and a cast without `st:ids`, recorded before ids or without them, says to give seconds or record it again.
- **Why an id, and not the step's number or its `name`** (2026-11-24). The number, as the time per step shows it, moves when a step is added or removed, and an `at` in `outputs` would then show another step's screen without a word. A `name` always prints a comment line in the demo, so a step without one could not be named, and a reworded comment would break the `at`. An id shows nothing, stays when other steps change, and a missing or repeated one is an error.

## Step exit codes

Recorded `steps` (pipe redirect or `pty: true`) observe the child exit code when the command finishes.

| Outcome | Behavior |
|---|---|
| Exit code `0` | No warning. Recording continues |
| Non-zero exit | Warning on stderr. Recording continues. Later steps still run |
| Non-zero exit, step sets `continue-on-error: true` | No warning. Recording continues, with `--strict` too |
| Non-zero exit with `--strict` | Recording stops at this step. Later steps do not run. No cast. `post` runs. Exit `1`, or the failing `post` command's code |
| Non-zero exit of a `show: none` step without `continue-on-error: true` | Recording stops at this step, with or without `--strict`. The rest as for `--strict` |
| A `background: true` step that exits before its `wait` text, or does not print it in time, without `continue-on-error: true` | The same |
| Fatal error (for example PTY spawn failure or output drain timeout) | Recording stops. No cast. `post` runs. Exit `1`, or the failing `post` command's code. See [spec_pty.md](spec_pty.md) |

scenetake exits `0` after a successful cast write even when individual steps exited non-zero, unless `--strict`, `pre`, `post`, SVG, or another fatal error stops it. Failed command output can remain in the cast as legitimate demo content.

With `--strict`, the first non-zero step exit stops recording unless the step sets `continue-on-error: true`. Later steps do not run. No cast, SVG, or mp4 is written, so existing outputs remain. `post` still runs. The error identifies the step, its exit code, and its last output lines. See [spec_cli.md](spec_cli.md).

Contrast with `pre` / `post` (fail-fast): [spec_pre_post.md](spec_pre_post.md). Warning message format: [spec_cli.md](spec_cli.md) → Logging.

## Command Environment

Every command scenetake runs from a scenario (`pre`, `steps` with pipe, PTY, or interactive PTY capture, and `post`) inherits scenetake's own environment with `COLORTERM=truecolor` set. A `COLORTERM` value from the host is replaced, not kept. Other variables pass through unchanged.

The recording uses a pipe or PTY. Cast players and the built-in SVG renderer support 24-bit color ([spec_svg.md](spec_svg.md)). Tools can select color depth from environment hints when they cannot query the terminal. Host values such as `WT_SESSION` and `TERM_PROGRAM` describe the caller's terminal. Previously, these hints caused the same scenario to record true color from one terminal and 16 colors from another. A fixed `COLORTERM` removes that dependency.

A command that needs another value sets it itself, for example `COLORTERM= tool` in bash or `$env:COLORTERM = ''; tool` in pwsh.

**Input.** A pipe step, `pre`, and `post` get no input: their stdin is closed, so a command that reads it, such as `read` or a tool that asks `[y/N]`, gets the end of input at once, as in CI. A PTY step gets only its `keys`, and an interactive step the host terminal ([spec_pty.md](spec_pty.md)). A step that must answer a prompt uses `pty: true` with `keys`.

## Shell state across steps

Each command, in `pre`, `steps`, and `post`, runs in a shell of its own, as always. With bash, zsh, and PowerShell, what one command leaves in its shell carries to the next one, in order from the first `pre` command to the last `post` command. The sh family (sh, dash, ash, ksh, ksh93, mksh) carries the folder and the variables:

| Carried | bash | zsh | sh family | PowerShell |
|---|---|---|---|---|
| Folder | `cd`, `pushd` | `cd`, `pushd` | `cd` | `Set-Location` |
| Environment variables, set, changed, or removed | `export`, `unset` | `export`, `unset` | `export`, `unset` | `$env:NAME = ...`, `Remove-Item env:NAME` |
| Shell variables | `NAME=value`, arrays | `NAME=value`, arrays, associative arrays | `NAME=value` | `$name = ...` in the session, and changed `$...Preference` variables |
| Functions | `name() { ...; }`, exported ones stay exported | `name() { ...; }` | not carried | `function Name { ... }` |
| Aliases | `alias name=...` | `alias`, `alias -g`, `alias -s` | not carried | `Set-Alias` |

So `source .venv/bin/activate`, `nvm use 20`, and `. ./env.ps1` work for the steps after them, as in a terminal.

- **Not carried:** shell options (`set -e`, `shopt`, `setopt`), traps, background jobs (`&`), history, the directory stack, and PowerShell modules from `Import-Module`. Variables the shell sets by itself, such as `RANDOM`, `SECONDS`, `PIPESTATUS`, `pipestatus`, `LASTEXITCODE`, and `$Matches`, are not carried either.
- **Only what the commands changed.** Each command starts as a fresh shell, with its login files. The state is the difference between that fresh shell and the shell when the command ended, added up over the commands. What the login files set is never carried, so it cannot pile up.
- **A failed command still carries.** As in a terminal, `cd app && false` leaves the next step in `app`. A command stopped by `timeout` carries nothing: what it changed before the stop is dropped, so a stop never leaves the next step somewhere unplanned. A command that replaces its shell (`exec`) or sets its own EXIT trap (bash, zsh, the sh family) also carries nothing. So does a PowerShell command that ends with a terminating error, such as `throw` or an error under `-ErrorAction Stop`, or with `exit` (How it works, below).
- **A background command** gets the state so far and carries none back: it has not ended when the next step starts.
- **A folder that no longer exists:** the next command starts in the scenario's `cwd` (or the caller's folder), and stderr shows `Warning: could not return to <folder>: it no longer exists or cannot be entered`.
- **Child shells are not affected.** A bash script that a step runs reads the caller's own `BASH_ENV`, not scenetake's, and a zsh that a step starts reads the user's startup files from the user's `ZDOTDIR`. Both see the exported variables only, as in a terminal.
- **Aliases in bash** expand in the steps after the step that defines one. bash does not expand aliases in a shell that runs a command unless `expand_aliases` is set, so scenetake sets it then. Only the scenario's aliases expand: the aliases that the login files define are removed in those steps, unless a step defined one of the same name. A command runs as it did before a step defined an alias, and a demo does not depend on the author's own aliases. Before a step defines an alias, nothing changes. zsh expands aliases in every shell, so nothing changes there.
- **Why the sh family carries no functions or aliases.** dash and ash have no command that lists functions. An alias must be defined before the line that uses it, and the restore shares the command's first line (below). stderr says so once at the start when the scenario has more than one command: `Note: in dash, cd and variables carry over to the next step, but functions and aliases do not. ...`.
- **Secrets.** A secret-looking variable that a command exports, such as `export API_KEY=...` in a `show: none` step, is hidden in later output as one scenetake started with is ([Masking secrets](#masking-secrets)). The state lives in a temporary folder that only the user can read, removed when scenetake exits.
- **Other shells** (fish, cmd, nu, tcsh) start each command afresh, as before. stderr says so once at the start when the scenario has more than one command: `Note: in fish, each command starts in a new shell: ...`.

How it works, briefly:

- **bash** reads a script through `BASH_ENV` after its login files, before the command. The script notes the fresh shell, restores the state, and sets an EXIT trap that writes `declare -p` (for each name in bash 3), `declare -f`, `alias -p`, and the folder. scenetake compares the two and writes the next script. The command's text is unchanged, so the line numbers in its error messages (`bash: line 2: ...`) stay the user's.
- **zsh** gets `ZDOTDIR` set to a folder of scenetake's startup files. Each reads the user's file of the same name from the user's `ZDOTDIR` (or `HOME`), then points `ZDOTDIR` back, so zsh comes back for the next one. After the last login file (`.zlogin`) the script notes the fresh shell, gives `ZDOTDIR` back to the user, restores the state, and sets an EXIT trap that writes `typeset -p`, the `functions` and alias tables, and the folder. The command's text is unchanged, so its error lines (`zsh:2: command not found: ...`) stay the user's. The restore comes after every login file because macOS's `/etc/zprofile` runs `path_helper`, which puts the system folders first in `PATH` again: a `PATH` that a step set would lose to it if restored earlier. A shell that is not a login shell, as on Windows, or a startup file that turns off `rcs`, ends the chain early, and the state is restored there.
- **The sh family** gets `. '<state>/restore.sh'; ` before the command, on its first line, so the line numbers in its error messages (`dash: 2: ...`, `ksh93[2]: ...`) stay the user's. The script notes the fresh shell, restores the state, and sets an EXIT trap that writes `set`, `export -p`, and the folder. A shell started as `sh` reads no `BASH_ENV` even when it is bash, as `/bin/sh` is on macOS, so it gets this way too.
- **PowerShell** runs the command as a script block of its own, read from a file, between the restore and the save, with the state in a CliXml file. Nothing catches around the command: a `try` or a `trap` would make an error that stops one statement, such as `1/0`, stop the whole command. The exit code is made again from `$?` after the command's last line, as `-Command` makes it, so `exit 7`, a failing native command (`1`), `Write-Error` (`1`), and `throw` (`1`) end as before. Errors show the command's own lines, as they do when it runs alone: `Exception: boom` in pwsh, `At line:1 char:1` and `+ throw 'boom'` in Windows PowerShell. Windows PowerShell's `Write-Error` is the exception. It names the block that called it and the line of that call, so it shows the command followed by the line that reads `$?`, and a line of scenetake's that names no folder.

### Output encoding on Windows

Windows pipe output, including `pre` and `post`, uses the same decoding regardless of the caller's terminal code page. Each child has its own console with the system OEM code page, such as 932 on Japanese Windows. Tools can write either UTF-8 or that console code page. Git, MSYS tools, and most cross-platform CLIs use UTF-8. Cmd, ping, and other Windows console tools use the console code page. PowerShell also uses it unless configured otherwise.

- scenetake reads each line as UTF-8 when it is valid UTF-8, and in the OEM code page when it is not. Text in a legacy double-byte code page is almost never valid UTF-8, and ASCII reads the same in both. A BOM at the start of the output is skipped.
- With `shell: pwsh` or `powershell`, by name or path, scenetake prefixes the command with `[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new()`. PowerShell and child commands sharing its console then write UTF-8, preserving characters absent from the OEM code page. This affects only the child console. Recorded typing, `running:` lines, and errors retain the original command. The command runs as a script block of its own (Shell state across steps, above), so it can begin with `using namespace`.
- cmd and Git Bash receive no prefix. Their Windows console tools write the OEM code page, which the fallback decodes. Characters outside that code page are already lost.

PTY steps need none of this: ConPTY translates everything to UTF-8. On Linux and macOS scenetake reads pipes in the encoding .NET picks for the process, as before.

## Execution Order

1. Read the YAML file. Warn about unknown keys. Resolve render settings.
2. Validate the scenario and outputs (see Pre-execution checks below). Stop on failure before any command runs.
3. Resolve the shell (see Shell resolution below).
4. Execute `pre` commands. From here on, each command gets the shell state of the ones before it (Shell state across steps).
5. Repeat all output checks, including the folder check, because `pre` may change the outputs. On failure, skip recording and rendering, but run `post`.
6. Execute `steps`. Record those that show something, and run those with `show: none` without recording them. Start `background: true` steps and leave them running. After the last step, end the background commands.
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
- Lookup excludes the current directory and ProgramData. A relative PATH entry, such as `.`, names the current directory too and is skipped, as the lookups for git and ffmpeg skip it. WSL bash is never used, including by path.

## Time per step

After the cast's `Written:` line, a recording and a replay print where the cast's time goes, a row for each shown step:

```
Written: basic.cast  (188 events, 16.7s)
   step   time  typing  output  pauses
      1   2.5s     0.5     0.0     2.0  echo "Hello, World!"
      2   2.2s     0.6     0.1     1.5  Name of the step
      3   2.3s     0.7     0.1     1.5  Coloring stdout
      4   2.2s     0.7     0.0     1.5  echo "Wait for 2 seconds..."
      5   2.7s     0.3     1.0     1.4  sleep 2
      6   2.5s     1.0     0.0     1.5  echo "stderr output is red by default" 1>&2
      7   0.9s     0.4     0.0     0.5  echo "Done!"
          1.4s                     1.4  final screen
  total  16.7s     4.2     1.3    11.2
```

- **A step's time** runs from where it begins, with its comment line, prompt, or `pre-delay`, to where the next shown step begins, so its `post-delay` is its own (Pauses above). The first step holds the 0.5 s before its prompt. The last step ends with its output, and the final screen holds the rest of the cast, as a pause.
- **The parts.** Typing runs from the first typed character to the command's start, Enter included: `typing-speed` and the command's length set it. Output runs from the command's start to the end of its output: `execution-duration` for a pipe step, and for a PTY or interactive step the command's own time, which `max-duration`, `silence-threshold`, and `max-idle` shorten. Pauses are the rest: `pre-delay`, `post-delay`, and a comment line. A card's and a clear step's time are pauses.
- **Which steps.** Every step the cast shows: commands, cards, and clear steps, numbered by their place in `steps`. A `show: none` step and a background step take no time in the cast and get no row. A step is named by its `name`, or else by its `run` text cut at 60 characters, and a card by its first line, after masking.
- **Tenths that add up.** Each row's parts add up to its time, and `total` is the length on the cast's `Written:` line. Each part is rounded down, and the tenths left over go to the parts that lost the most.
- **Columns, not sentences** (2026-11-24). The first table wrote each row as `2.5s = 0.5 + 0.0 + 2.0` under a `Time per step: typing + output + pauses` line, and ended with `In all: typing 4.2s, output 1.3s, pauses 9.8s, final screen 1.4s`. The user found it wordy. Headed columns name each part once, and `total` is one more row in them.
- **As recorded.** The times are the cast's, as its `Written:` line is. With `speed` or `length`, the outputs play faster, and their own `Written:` lines say how long.
- **Not for `capture`.** It records one command, whose time the cast's line already gives.
- **A length that cannot be kept** gets a warning that names, by their numbers here, the steps that take longest at the shortest pace, split the same way ([spec_svg.md](spec_svg.md) → Length).
- **Why** (2026-11-23). The cast's line gave the total only, and the warning of a length that cannot be kept said `Leave out steps or output` without saying which. The split tells the author what to change: `basic.yaml`'s 16.7 s are 9.8 s of pauses, which `post-delay` and `pre-delay` set, and a slow build shows as one step's output. An agent that writes a scenario reads the run's output, not the SVG, so the table comes with every recording instead of behind an option.

## Determinism

Each step types from a random sequence of its own. Its seed is the step as recording again finds it (Recording again below): its `run` text after masking, how it runs (pipe, `pty`, or `interactive`), what it shows, and how many steps before it are the same. A step's `keys` draw their typing delays from the same sequence, after its typed command. Each step starts on a whole millisecond. The cast header's `timestamp` is fixed at `1700000000` (2023-11-14 UTC).

So a comment, the title, `render`, `outputs`, `settings` other than the typing keys, `pre`, `post`, quoting, and key order change no typing. An edit to one step's `run` changes the typing of that step alone. `typing-speed` and `typing-jitter` scale the same random numbers, so they change the delays of every step they apply to.

- **Why a sequence for each step.** The seed was a hash of the whole YAML text, and one sequence ran through every step. A comment added to `samples/basic.yaml` changed 153 of its cast's 189 lines (2026-11-21): the header's timestamp and every typed interval. A new command in one step moved the sequence for every step after it. Recording again kept each step's output, but the typing around it changed, so a diff showed the whole cast.
- **Why the text after masking.** The cast is published. A seed from a secret would let anyone who guesses the secret draw the same delays and check them against the cast. Two secrets of one length now type the same.
- **Why each step starts on a whole millisecond.** The cast stores intervals between times rounded to milliseconds. A step that typed longer than before moved the fraction of a millisecond every later step started at, and the rounding of their intervals by 1 ms with it.
- **What a step's seed still depends on.** A `run` text that masking changes on one machine and not on another, such as a computer name that `mask-personal` hides, types differently on the two, as recording again does not find the step on the other machine either. A path under `cwd` with `cwd-as` shows the same on every machine ([Paths in the recording](#paths-in-the-recording)), so it types the same. A path under the home folder shows as it is, so it types differently on another machine. Two steps that type the same command count apart, so the second does not repeat the first one's typing.

Instant pipe output produces identical casts across runs if commands print identical output. PTY, interactive, and stream output use wall-clock timing. Their output events and the interval to the next event can differ between runs, unless the run records again to an earlier cast (Recording again below).

After each such step, recording resumes at the next whole millisecond. This keeps subsequent timing stable after that first interval. Without this adjustment, fractional milliseconds could change later typing intervals by 1ms. Cast intervals use differences between rounded absolute times ([spec_cast.md](spec_cast.md) → Event time).

## Recording again

A demo is recorded again and again: after a tool update, in CI, or after a change to one step. Each recording used to differ from the one before even when nothing changed. PTY output is timed by the wall clock, so a step that printed the same text moved by a few milliseconds, and so did the SVG. Output that changes on every run, such as a date, a commit hash, or a random ID, changed it too. A diff appeared on every recording, and the real changes were hard to find.

So a recording compares each step with the cast that is already at its output path, written by an earlier recording:

| The step | What the cast gets |
|---|---|
| Prints the same as in the earlier cast | The earlier events, with their timing |
| Prints something else | This run's events |
| Prints something else, and sets `keep-output: true` | The earlier events, with their timing |
| Is not in the earlier cast (new, or its `run` changed) | This run's events |

- **The same output gives the same files.** A scenario recorded again with nothing changed gives the same cast byte for byte, and so the same SVG. Only the steps whose output changed differ. An edit to a comment, the look, or another step leaves a step's typing as it was too (Determinism above).
- **What is compared.** The step's output as the cast shows it, after masking and coloring, joined across events, so a PTY that splits the same text into other reads, even inside a character, still prints the same. A new `highlight` or `stderr-color` changes the output, so the step takes this run's events. A `stream-output` pipe step is compared without colors and carriage returns, because each read is colored by itself and the reads fall differently on each run. Its `highlight` and `stderr-color` count with the output-timing keys below instead.
- **What the YAML times still applies.** The prompt, typing, comments, and delays come from the YAML, so a new typing speed takes effect while each step's output keeps its timing. A step whose output-timing keys changed (`execution-duration`, `stream-output`, `max-duration`, `silence-threshold`, `max-idle`, `normalize-typing`, `timeout`, and for a step that types keys, its typing and `keys`) takes this run's timing, or the new key would have no effect.
- **A step is found** by its `run` text after masking, whatever its line endings, by pipe, pty, or interactive, and by `show`, in order: the second step that runs the same command finds the second one. A step that moved keeps its timing.
- **`keep-output: true`** is for a step whose output changes on every run and does not need to stay current. The command still runs, because the steps after it may need what it does. A failing command still warns, and stops the recording with `--strict`, so a broken command is not hidden behind an old output. A kept step keeps its earlier colors too: remove the key for one recording to take a new `highlight`. Pipe output, which appears at once, takes this run's time.
- **When `keep-output` shows the new output.** The step shows what it printed now, and the CLI says why, when the earlier output holds text that is masked now, so a mask added after a leak hides it, and when a PTY step on Windows starts at another place on the screen than its earlier output did, because ConPTY wrote rows for the old place.
- **The CLI says what happened**, after the cast's `Written:` line: `Compared with the earlier cast: 4 of 5 steps printed the same, 1 changed`, then each changed and new step. A first recording says nothing, unless a step sets `keep-output`, which then has nothing to keep.
- **Starting afresh.** Delete the cast. A cast is also not compared, and the CLI says so once, when it has no tag below (an earlier scenetake or another tool wrote it), when it was recorded in another terminal size, whose PTY output would not fit, and when an edit added, removed, or moved its events, so the tag no longer fits them. Changed text alone is kept. A scenario whose steps print nothing to compare, cards and hidden steps alone, writes no tag and says nothing.
- **`rerun: false`** shows the step's output from the earlier cast without running its command: a slow step whose result stays on disk (a build, an install), a step that costs money or changes something (a deploy, an AI CLI's answer), or an interactive step recorded once by hand. The step is found as any step is, so it runs when the earlier cast does not have it, after its `run` changed, and on a first recording. It also runs when its earlier output holds text that is masked now, and when ConPTY would start it at another place on the screen, as `keep-output` then shows the new output. The CLI says which steps did not run (`  not run (rerun: false): ...`, and `1 not run (rerun: false)` in the comparison's line) and why a step with the key ran.
  - **What does not happen.** The command does not run, so its `cd`, `export`, alias, and function do not carry to the next steps, and its files are not made again. A step whose effects the next steps need keeps them only where they last (files left by the earlier recording), so a `cd` goes in a step of its own. It has no exit code, so it never fails.
  - **Its output.** Output that the clock timed keeps its earlier timing, and output that appears at once takes this run's time, as for a step that prints the same. Its colors are the earlier ones. `--rerun` runs it for one recording, which takes new highlights, timing keys, and output.
  - **Interactive steps in CI.** A recording without a terminal stops before `pre` when an interactive step would run. An interactive step with `rerun: false` whose output the earlier cast has does not count, so a scenario with a step recorded by hand records again in CI. The check finds the step as the recording does, with the secrets known before `pre`, so a step whose run text holds a secret that `pre` exports counts as one that runs. A step whose earlier output holds text that a mask added since then hides counts as one that runs too, as the recording runs it. Until 2026-11-27 the check let it pass, and the step failed after `pre` and the steps before it had run.
  - **`--rerun`** runs every step, as if none set `rerun: false`. With `--replay`, which runs nothing, it is an error.
  - **An outputs file runs every step**, because the file comes from one run with the cast (below).
- **`--replay`** compares the same way, and runs nothing, so `rerun` changes nothing there. The playground has no earlier cast, and lists `keep-output` and `rerun: false` as not applied.
- **An outputs file turns the comparison off.** A recording with `--save-outputs` or `SCENETAKE_OUTPUTS` is not compared, and `keep-output` has no effect, so the cast and the file come from one run and a replay of the file gives the cast. The CLI says so when a cast was already there.

**Where the steps are.** The cast's `st:steps` tag holds the cast's event count and where its last event is, which show an edit, and for each recorded step a hash of how it is found, a hash of its output-timing keys, whether the clock times its output, its first event and event count, and its start and end in milliseconds ([spec_cast.md](spec_cast.md) → Header). Players ignore it. The cast is published, so neither hash can confirm a guess at a masked secret or at typed text: `run` and `keys` are hashed after masking, and typed text counts only by its length.

**Why the cast, not the outputs file.** The cast is written on every recording, next to the SVG, and is usually committed with it, so CI can compare without another file. The outputs file is written only with `--save-outputs`, holds PTY output as base64, which a diff cannot show, and repeats what the cast holds. A step's events in the cast are also what the viewer sees, which is the right thing to compare.

**Why `keep-output` and not a pattern to replace.** A replacement such as `in *s` → `in 0.42s` keeps the rest of a step current, but needs a pattern language (no regular expressions: they would add hundreds of kilobytes to the Native AOT binary), can change the width of a full-screen program's lines, and makes the author find each changing value. `keep-output` covers the common case, a step that changes as a whole, with one key. A value can often be fixed at its source instead: `GIT_AUTHOR_DATE` and `GIT_COMMITTER_DATE` exported in a `show: none` step give the same commits the same hashes on every recording, and `TZ` fixes the time zone.

**Not chosen.** Timing PTY output by its text alone needs no earlier cast, but TUI motion and spinners would no longer play at real speed. Rounding output times to a grid still flips at the grid's edges. Comparing SVGs cannot tell a timing difference from a change.

**Why a step says `rerun: false`, and scenetake does not choose.** Running only the steps that changed, as a build tool does, would run a step without what the steps before it did: the folder a `cd` entered, a file an editor saved, a commit. scenetake cannot save the files between steps, so the step would print something else without a word, such as an empty `git diff` after a `vim` step that did not run. `--only N` and `--from N` have the same gap. The author knows which steps the next ones need, and which cost time or money, so the scenario says it, and every other step runs as before.

## Editor schema

`schema/scenario.schema.json` describes the scenario format as a JSON Schema (draft-07). An editor checks a scenario with it while the file is written: it completes keys and values, shows each key's description on hover, and marks a misspelled key or a wrong value before anything runs. VS Code's YAML extension and other editors that use yaml-language-server find the schema from a comment in the file:

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/guitarrapc/scenetake/v<version>/schema/scenario.schema.json
```

- **Who writes the line.** `init` and `capture --save-scenario` start their file with it, for their own version. Every scenario in `samples/` starts with it too, so a reader who copies one gets the checks, and the README's copy of `samples/scenario_format.yaml` shows it. `scripts/set_version.cs` moves those lines to the new version's tag. A scenario written by hand can take the line from one of those. An editor that does not read the comment can map the URL to the scenario files in its settings. No comment seeds the typing (Determinism below), so adding the line, or moving it to a later tag, changes no recording.
- **Why the version's tag.** The schema at a tag lists the keys that version reads. One on `main` would complete keys that an older binary warns about as unknown. A version that has no tag yet, such as a development build, names a URL that is not found, and the editor then checks nothing.
- **What it marks.** What scenetake stops at before anything runs or warns about, as far as a schema can say it: an unknown key at any level, a value of the wrong type or out of range, a name that is not a theme, a window, a fit, a show value, a format, or a key name, a step without `run` that is neither a card nor a clear step, a key that a card, clear, background, `show: none`, or `show: output` step does not take, `keys` without `pty: true`, `interactive: true` without `pty: true` or with `keys` or `timeout`, `normalize-typing: true` without `interactive: true`, `wait` without `background: true`, `wait-timeout` without `wait`, and an outputs entry's option that its format does not take (`crf` on a GIF, `at` on an mp4). A message for a key that does not apply comes from `errorMessage`, which yaml-language-server shows and other validators ignore.
- **What it takes as scenetake does.** A name in any case (`show: None`, `key: Enter`, `format: SVG`), because scenetake reads names so. A number where text goes: a `run`, a `wait` or `type` entry, a card line, a prompt part, a `mask` text, a `pre` or `post` command. A highlight `at` as a number or a list. An outputs entry's `speed: 2x`, `at: end`, `max-size: 400X300`, and `size: 16:9`, as the command line takes them. A flag set to `false` needs nothing else: `interactive: false` without `pty` is no mistake.
- **Where it is stricter.** A quoted number or a quoted `true` or `false` is marked everywhere. Under `settings` and `render` scenetake refuses it, while a step or an outputs entry reads it. One rule is easier to follow than two.
- **What it does not check.** What needs the file system or the recording: that `cwd`, a background image, or a font file exists, a card line wider than the terminal, a crop that leaves nothing, a `speed` written as text above 4, and whether a style names a known color. scenetake reports these when it runs.
- **Why draft-07.** yaml-language-server and the other editors check draft-07 in full, while their support for later drafts varies.
- **Kept in step with the code.** `tests/Scenetake.Tests/ScenarioSchemaTests.cs` compares the schema's keys with the classes and lists that the unknown-key check reads (Unknown keys above), and its names with the code's themes, windows, fit modes, show values, formats, and key names, including the any-case patterns. It checks that every key has a description, that every sample, every fixture, and every scenario block of the READMEs and the agent skill passes, and that the fixtures for errors found before the run and a list of common mistakes are marked. A key added to the code and not to the schema fails the test.

### Agent skill

`skills/scenetake/SKILL.md` tells an AI coding agent how to make a demo with scenetake: learn the tool, start from `init`, record, read the warnings, look at PNG stills of the recording, tune with `--replay`, and pick the outputs for each destination. `npx skills add guitarrapc/scenetake` installs it for the agents that tool supports.

- **Why a skill.** An agent that is asked for a demo writes the scenario, and the 2026-11-18 evaluation saw one new to scenetake make the mistakes a person makes, such as an unquoted `: `. The skill puts the rules that decide whether a demo works in front of it, with a way to look at the result: an agent cannot watch an SVG play, so it draws screens with `png --at`.
- **Why it rules out `interactive: true`.** The step waits for a person at the keyboard, which the agent is not.
- **Not in the binary.** The schema and the skill are files in the repository, read from GitHub. Printing them from the CLI would add their text to every binary.

## Init Template

`scenetake init` creates a commented starter file. Its first line names the editor schema (Editor schema above). Recorded unchanged, it is a demo that looks like one: `render` sets a `macos` window with the title, a gradient `background`, and an `end-hold`, `outputs` writes an SVG next to the cast, the prompt shows the folder in color (`{dir}`), and the steps are a title `card`, a command with a comment line, the stderr example, a `pty: true` step, and an end `card` with an install line. The reader replaces the commands and the cards' text. Commented, to take up: the theme's `fg` and `bg`, the font, `fit`, a light SVG, a GIF, and an mp4 in `outputs`, `settings.mask` and `mask-auto`, `pre`, `post`, a highlight example, and a step that answers a prompt with `keys` and `wait`. The file is UTF-8 without a BOM.

- **Why the starter looks finished.** It was a tour of coloring options with the look commented out, so its first recording was a plain terminal, and a reader learned the window, the background, and the cards only from the README. A starter that records as a finished demo shows the look first, and its comments show what else there is. Only the SVG output is live, so the first recording needs no ffmpeg.

The prompt step stays a comment: a live one would hold up the first recording wherever its keys go astray, such as in a shell whose prompt prints differently. Its comment names `interactive: true` and `--save-keys` for keys typed by hand.

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
- **A CRLF can fall between two reads.** Stream emit normalizes the line endings of each read, so a read ending in CR and a next one starting with LF gave two line breaks, where the joined output of the instant path gives one (found 2026-11-26). The LF after a CR that ended the previous read is now dropped.
- **Masking allocated about 30 bytes per byte of output** (2026-11-26). Every step's output is masked, as `mask-auto` is on by default. The text it searches kept five lists, one entry per character each: the character, its first byte, its byte count, its width, and whether it is on the alternate screen. Each list was then copied to an array. Now only the character and its first byte are kept, the alternate screen gets an array only when output uses it, and the byte count and width are read again from the bytes for the few characters replaced. The bytes between replaced characters are copied in one piece, not one by one. 4MB of colored log output took 121MB instead of 200MB and 88ms instead of 114ms, and 59MB instead of 131MB with nothing to hide (in process).
- **Every `:` made a string** (2026-11-26). The search for `key=value` and `key: value` read the key before each `=` and `:` into a string, then split it into words with a list, a string builder, and a string per word, also for the `12:00` of a timestamp. Keys and words are now read where they are. The 4MB log took 92MB instead of 121MB, and 31MB instead of 59MB with nothing to hide (in process).
- **A background command's output was kept after its wait** (2026-11-27). A background step reads its command's output so that a full pipe never stops the command. Once the wait is over, or when the step has no wait, nothing reads that output again. Each read was still decoded, searched for the wait text, and kept as the tail for an error. Those reads are now dropped as they come. With a command that printed 300MB while a 4-second step ran, scenetake's own CPU time went from 1.94s to 0.58s (AOT, Windows 11, median of 3).
- **Each kept step played the earlier cast from its start** (2026-11-27). A PTY step that keeps its earlier output, or does not run because of `rerun: false`, is checked against where its earlier output started. That place is found by playing the earlier cast up to the step. Steps ask in order, so the cast is now played on from the step before. For 40 steps over the casts of nyancat and pty, it went from 65ms to 13ms and from 22ms to 6.6ms (in process).
- **`Test-Path` for each variable** (2026-11-27). After each pwsh command, the state save checked whether each variable the shell started with still exists, with one `Test-Path env:NAME` each. On Windows with 113 variables that took 7.5ms in a fresh pwsh, and `[Environment]::GetEnvironmentVariable` takes 4.4ms. A 12-step pwsh scenario took about 5.3s either way, because starting pwsh is most of a step.
- **A removed variable came back empty in pwsh** (until 2026-11-27). The restore removed a variable with `[Environment]::SetEnvironmentVariable(name, $null)`. PowerShell passes `$null` to a string parameter as an empty string. .NET 8 removed a variable set to "", and .NET 9 and later keep it empty, so from pwsh 7.5 on, a variable the shell started with that a step removed was empty, not gone, in every later step. `[NullString]::Value` passes a real null. Windows PowerShell 5.1 removed it either way. The pwsh test now removes a variable the shell starts with.
- **Names that differ only in case were one variable in pwsh** (until 2026-11-28). The state notes the variables the shell starts with, and the ones a step sets, in PowerShell tables, which match keys in any case. On Linux and macOS, `sct_case` and `SCT_CASE` are two variables, and a table kept only one of the two names, which one depending on the run. A removal of one of them was lost in some runs, a removal of both brought one back, and a step that set both passed on only one. On those systems the tables now match names exactly. Windows has one variable for both names, and its tables are unchanged.
- **Host terminal hints leak into recordings.** `matrix` decides true color from `COLORTERM`, `TERM`, `WT_SESSION`, or `TERM_PROGRAM`. When scenetake ran from a pwsh without `WT_SESSION`, `matrix` printed `warning: true color not supported` into the cast and recorded 16 colors. With `WT_SESSION` set, the same scenario recorded true color. Users reasonably expect a Windows Terminal session to have it, but not every shell shown in Windows Terminal does. Setting `COLORTERM=truecolor` for every command removed that dependency (see Command Environment).
- **Windows shell paths must start the specified file.** Previously, known shell filenames triggered name lookup, bypassing wrappers and alternate installations. Missing paths could start the PATH copy, while valid pwsh paths failed without a PATH copy. A trailing dot in `C:/WINDOWS/system32/cmd.exe.` also bypassed name detection. Cmd received `-c`, interpreted its path as switches, and created folders such as `-c` during `pre`. Starting the full path with Windows separators and removing the trailing dot fixes both cases.
- **Shell lookup must exclude the current directory.** A cloned project could contain `pwsh`, `pwsh.exe`, or executable `bash` files that ran before `pre`. Windows lookup now excludes the current directory. Until 2026-11-26 it still followed a relative PATH entry such as `.` there, which the git lookup already skipped. On Unix, .NET checks it before PATH for pipe commands, while PTY commands search PATH. Unix therefore rejects conflicting files. Selecting an absolute PATH executable would change `$0` and shell messages. Git/MSYS lookup also excludes ProgramData because other users can create folders there.
- **Windows pipe decoding must follow the child console.** Previously, decoding used the caller's code page, while children used the OEM code page. Japanese Windows tests on 2026-11-13 showed corrupt Git Bash and git text from cp932 terminals. From UTF-8 terminals, pwsh and cmd text was corrupt instead. PowerShell also replaced emoji with `??` in both cases. UTF-8 decoding with an OEM fallback and UTF-8 PowerShell output produced identical, correct casts from both terminals. A background step reads its output the same way since 2026-11-26: it read UTF-8 only, so a `wait` for non-ASCII text that cmd wrote in the console's code page never matched. The fallback uses `MultiByteToWideChar` because .NET lacks code page 932 without `System.Text.Encoding.CodePages`. An initial `DllImport` without `CharSet.Unicode` marshaled output as one-byte characters and returned no text. Native function pointers with `char*` avoid that marshaling.
- **Invalid steps need explicit errors.** YAML interprets `- echo key: value` as a mapping with key `echo key`. Previously, missing `run` caused silent omission. A misspelled `step:` also produced an empty cast with exit `0`. Both now fail before commands run. Missing-`run` errors list the parsed keys.
- **A shell command is the usual YAML error.** In the evaluation on 2026-11-18, both the author and an agent new to scenetake wrote `run: ... echo "answer: $a"` unquoted and got only the library's `Mapping values are not allowed in this context` with a line and a column. Someone who comes to scenetake for a shell command does not expect YAML to read `: ` inside it. The error now shows the line quoted as it should be (Commands that contain `: `).
- **Pipe steps read scenetake's own input.** A step such as `read -p "Continue? [y/N] "` waited for a key, while the prompt went to the captured output and showed nowhere, so the run looked hung. CI has no input, so the same scenario finished there with an empty answer. The stdin of pipe steps, `pre`, and `post` is now closed, so a local run records what CI records. Found in the 2026-11-18 evaluation, and reproduced with an input pipe that stays open: the run waited until it was killed.
- **The mask count counted finders and places.** One `GITHUB_TOKEN=ghp_…` line, typed and printed, gave `Masked 4 secrets: 2 GitHub token, 2 key=value secret`. The GitHub token rule hides what follows `ghp_`, the key=value rule then hides the prefix, and each counted the characters it added. Both places counted too. A reader took it as four leaks. A secret is now a run of hidden text, counted once per text.
- **Typing must preserve grapheme clusters.** UTF-16 code-unit typing split non-BMP emoji into surrogates, which cast writing replaced with U+FFFD on every OS. Grapheme-cluster typing also preserves modifiers, ZWJ sequences, and combining marks. Commands without these characters retain identical typing events.
- **Missing `cwd` errors need the resolved path.** Setup cannot create `cwd` because `pre` already runs there. Previously, OS errors named neither `cwd` nor its base folder. Validation before `pre` now identifies both.
- **Recorded failures need an explicit CI policy.** Default exit `0` can hide broken demos. `--strict` stops unexpected failures and exits `1`. Later commands may depend on the failed command. Existing recordings must remain available for comparison. An earlier version wrote all outputs before failing, which lost that protection. Without `--strict`, one run still shows all failures. `continue-on-error: true` permits expected failures.
- **Wall-clock steps changed unrelated cast lines.** Repeated recordings with one stream step and one PTY step differed on 26–39 of 140 lines. Most differences were 1ms changes in later typing. Rounding after these steps reduced differences to 2–4 lines: their output and the following interval. Instant pipe steps already have stable timing.
- **Unknown keys hid sample errors.** Warnings found `Post:` in `samples/demo.yaml`, where cleanup never ran. YAML also parsed two `printf` steps in each theme sample as mappings because they contained `: `. Missing-`run` validation exposed those omitted steps for 16- and 256-color output. Unknown keys produce warnings so newer scenarios can still run on older versions.
- **Another key spelling took effect while the warning said it had none.** VYaml converts each YAML key to kebab case before it compares it with the class's keys, unless the options name kebab case themselves. scenetake parsed with the default options, so under `settings`, `render`, and the top level, `typing_speed: 0.3` changed the typing speed. The unknown-key warning said scenetake did not read it. A step key, read from a plain map, never took effect. The playground found this on 2026-11-15, when a scenario written the way an AI assistant often writes one was pasted into it. The parse now names kebab case in the options, so one spelling works everywhere.
- **The YAML library's type errors named neither the key nor the type.** `typing-speed: 50ms` gave `Cannot detect a scalar value as double : Scalar 50ms at Line: 9, Col: 9`, and the line was the one after the value. Units such as `ms` and `s` are a common guess. When the typed parse fails, scenetake reads the file again as plain maps and finds the first value that does not fit its key's type. The key types come from the schema that VYaml generates for the unknown-key check. A step value used to fall back to the default with no message, and it now warns.
- **Native AOT needs VYaml's `object` formatter.** `Dictionary<object, object?>` parsed under `dotnet run` but failed after publishing because its formatter required reflection metadata. Reading as `object` uses an AOT-compatible formatter with the same maps. A published-binary test protects the warning behavior.
- **The profile's 8.3 name showed the user name.** Windows writes `TEMP` as `C:\Users\ADALOV~1\AppData\Local\Temp` when the name is longer than 8 characters, and `$env:TEMP` in PowerShell and `cygpath -w /tmp` in Git Bash print it. `mask-personal` looked for the whole name, so the first six letters showed (found 2026-11-24). The 8.3 name became a home folder name, and the path showed as `~`, until the user name stopped being hidden on 2026-11-28 (Masking personal details).
- **A file URL counted as a web address.** The rule that keeps a published name in an address took `file:///C:/Users/ada` for one, because of its `://`, and showed the user name. A file URL is a path, and a git name in it is starred.
- **Relative `cwd` must follow the scenario location.** Previously, `cwd: work/repo` referred to different folders when the caller changed directories. Relative paths now use the scenario file's folder. This affects only relative `cwd` values when scenetake starts elsewhere. Without `cwd`, commands still use the caller's folder. The interactive sample relies on this to edit that folder's `README.md`.
- **A step that does not run takes this run's time for output that appears at once** (2026-11-21). Its earlier events were first put at their offsets in whole milliseconds, which ended the step on a whole millisecond. A run ends a step with instant output at its start plus `execution-duration`, so the next step started 1 ms off the earlier cast, and a recording with nothing changed was not the same file.
- **The schema's values were found by running scenetake, not from the tables** (2026-11-21). The tables give one spelling for each value, and scenetake reads more: `window: MacOS`, `key: Enter`, `wait: 123`, `card: 1.10`, `at: 1`, `pty: "true"` in a step, and `font-size: "12"` in an outputs entry all record without a warning, while `typing-speed: "0.05"` under `settings` stops the run. A schema built from the tables alone would have marked scenarios that record. Each value form was tried with the binary first.
- **A key's presence is not its value.** JSON Schema's `dependencies` applies when a key is present, so the first schema marked `interactive: false` without `pty`, which scenetake reads as no interactive step. The conditions on a flag now hold only when it is `true`, as scenetake checks them.
- **The starter template must match its shell.** Bash redirection caused a ParserError in Windows pwsh, and Windows lacked `printf`. The template now uses commands for the default shell. `--shell` supports other choices, including Git Bash on Windows and PowerShell on Unix. The generated file names the selected shell.
- **The starter template fell behind the features.** The 2026-11-18 evaluation found it described `render` as SVG metadata for `--format svg`, and showed none of the window, the background, `fit`, masking, or `keys`, though each worked for mp4, PNG, and GIF too. Someone who started from `init` never met them. It was also written with a BOM, unlike every other file scenetake writes, and `cat` showed the stray bytes.
- **Unix PowerShell needs PowerShell arguments.** Unix launches previously passed `-lc`, which PowerShell rejected with exit code 64. Pipe and PTY steps now pass `-NoLogo -NoProfile -Command`.
- **The default width was too wide for a README.** Without `width`, a scenario got 120 columns: an SVG 1226 pixels wide (2.4 : 1), which a GitHub README of about 830 pixels showed at two thirds, with 11-pixel text. The default is now 80 × 24, the classic terminal size: 830 × 504 as SVG and 820 × 488 as mp4, shown at its own size. Every sample sets its size, so none changed.
- **A scenario could not be written as a terminal session.** Each step ran in a fresh shell, so `cd /` then `pwd` printed the caller's folder, `export` and `alias` were gone in the next step, and `source .venv/bin/activate` did nothing for later steps (checked 2026-11-18). Authors wrote `cd app && ...` in every step, which the viewer saw. None of the 53 scenarios in the repository changed folder or environment between steps, so carrying the state changed none of them.
- **bash reads `BASH_ENV` after its login files** with `-lc`, in Git Bash and on Linux, so a restored `PATH`, such as a virtual environment's, stays ahead of what `/etc/profile` adds. The EXIT trap runs after a lone external command too: with a trap set, bash does not `exec` the command in place of itself.
- **PowerShell needed the exit code made again.** A save after the command changed `-Command`'s exit code: a failing native command and `Write-Error` exited `0`, because the save was the last statement. `$?` read right after the user's last line, and `exit 1` when it is false, give the codes a plain run gives. Saving from the `PowerShell.Exiting` event left the exit code alone, but the event's action raced the process exit and saved in one case of six.
- **PowerShell's error display depends on the line layout.** With the user's command on a line of its own inside `try {`, `throw 'boom'` showed a `Line | 2 |` block instead of `Exception: boom`. With the restore on the first line and the save after the last, every runtime error in nine cases showed as in a plain run.
- **PowerShell steps ran inside a `try`** (until 2026-11-28). The command ran inside the wrapper's `try { }`, with the save in its `finally`. Windows PowerShell writes the line each error is on, so `throw 'boom'` showed the wrapper's text with part of the state folder's path, and `Write-Error` showed the whole wrapper with the paths of its scripts. A syntax error showed the wrapper's text in both shells. And a `try` anywhere above a command makes an error that stops one statement stop the whole command: `[int]::Parse('x'); 'after'` printed `after` and exited `0` alone, but stopped and exited `1` in a step. A `trap` does the same. The command now runs as a script block of its own, read from a file, parsed before the restore, with nothing around it. Of 19 cases of errors, exit codes, `return`, `exit`, and `using namespace`, both shells showed and exited as they do for the command alone, except two kinds. Windows PowerShell's `Write-Error` names the block that called it, so it shows the line that reads `$?` and a line of the wrapper, without paths. A command cut off, such as `if (`, shows its syntax error at its own line, where pwsh alone prints nothing and Windows PowerShell alone gives no position. The cost is the `finally`: a step that ends with a terminating error or `exit` passes on none of its own changes, and the next step gets the state of the steps before it.
- **bash 5.2 and bash 3.2 dump values differently.** bash 5.2 writes a value with a line break as `$'a\nb'`, on one line. bash 3.2, macOS's `/bin/bash`, writes the break inside double quotes. The dump is read as statements that end at a line break outside quotes.
- **bash 3.2 writes `declare -p` without names as `NAME=value`.** It leaves out the attributes, such as `-x` for an exported variable, and no line starts with `declare`, so on macOS no variable carried, while the folder, the functions, and the aliases did. The tests passed on Linux and Windows, and the first CI run on macOS found it (2026-11-18). bash 3 now gets `declare -p` for each name that `compgen -v` lists, which writes `declare -x NAME="value"` as bash 5 does. The same run showed that bash 3.2 names one line less in the errors of a `-c` command (`bash: line 1:` for the second line), in a plain run too. So the tests compare the line in a step's error with the line that the same shell names for the same lines without scenetake.
- **ksh93 writes its error lines another way when an EXIT trap is set.** A plain `ksh93 -c` writes `sh: line 2: ...`, and with the state's EXIT trap it writes `sh[2]: ...`. The number is the same.
- **Ending the process tree missed Git Bash's processes.** With the state's EXIT trap set, bash runs a command such as `sleep 60` in a child, and Git Bash's fork emulation leaves that process with a parent that has already exited. `Kill(entireProcessTree)` follows parents, so it ended bash and missed `sleep`, which held the pipes open: a pipe step stopped by `timeout` after 1 second waited 30 for its `sleep 30`, and a background `sleep 60` outlived scenetake (2026-11-18). On Windows a stopped pipe step and every background command now run in a job object, which holds every process started inside it, whatever its parent, and ends them all. The job also ends them when scenetake exits. A PTY step was not affected: Ctrl+C and the closed pseudo console reach every process attached to it.
- **zsh's `zshexit` hook missed a lone external command; the EXIT trap does not** (zsh 5.9, 2026-11-18). Without an EXIT trap, `zsh -lc '/bin/false'` runs the command in place of itself, and no hook runs after it. With a trap set, zsh waits for the command and runs the trap, as bash does. Neither runs after `exec`.
- **zsh writes the dump differently inside a function.** `typeset -p` in a function writes each global as `typeset -g`, and `argv` as the function's arguments, so the dump before the command and the one in the EXIT trap are both written from the same function. A tied pair such as `PATH` and `path` is two statements, `export -T PATH path=( ... )` and `typeset -g -aT PATH path=( ... )`. Both are restored together, which zsh reads without an error.
- **The user's zsh startup files are read at the top level, not from a function.** A file read from a function makes its `typeset` statements local to that function, so `typeset -U path` or `typeset FOO=bar` in `.zprofile` would vanish when the function returns. Many users' `.zshenv` moves `ZDOTDIR` to another folder, so each of scenetake's files takes the user's `ZDOTDIR` as the last file left it before pointing it back.
- **The sh family writes quotes three ways.** A quote in a value comes out as `'it'"'"'s'` (dash, busybox ash), `'it'\''s'` (mksh, bash as sh), or `$'it\'s'` (ksh93). dash and ash write a line break inside single quotes. Each shell reads its own `set` output back, so a statement is restored as written, and only a secret's value for masking is unquoted.
- **The login files' aliases must not start expanding.** Git Bash's login files define aliases such as `ls`, which a shell that runs a command does not expand. Setting `expand_aliases` whenever an alias exists would have changed what `ls` prints in every Git Bash recording, so it is set only after a step defines an alias.
- **Turning expansion on for a step's alias turned it on for the login files' aliases too** (2026-11-20). In a Git Bash terminal, whose `TERM` is `xterm-256color`, the login files also define `node` as `winpty node.exe`. After a step defined any alias, `node` in a later pipe or background step went through winpty, which needs a console, and failed with `stdin is not a tty`: a background `node` server never became ready. The login files' aliases are now removed whenever expansion is on, so only the scenario's aliases expand.
- **Kept events land at whole milliseconds from the step's start** (2026-11-18). The cast writer carries rounding error from interval to interval, so each interval is the difference of two rounded absolute times. Placing the earlier events at their earlier offsets in whole milliseconds, and going on from the earlier step's end, gives the earlier file byte for byte. Offsets in fractions of a millisecond moved intervals by 1 ms.
- **A step is compared while it is composed, not after all of them** (2026-11-19). Comparing the finished recording left the later steps laid out on output the cast did not show: a `name` comment took its line break from the discarded output, and ConPTY started the next step at the discarded output's row, which drew it on the wrong line and reported it as changed.
- **Each step's output is filtered as it is composed** (2026-11-19). Filtering only the finished recording let an escape sequence left open at the end of a step reach the next step's text, and the steps after it were laid out on events the cast would not hold. The closing prompt now depends on whether a step was shown, not on what it printed, so a first step that leaves nothing in the cast does not change it.
- **A replay takes the masked `run` from the outputs file** (2026-11-19). A secret that `pre` exported is known only while recording, so a replay that masked by itself hashed the plain text and did not find the step. The file holds `maskedRun` only when masking changed the text. The replay types that text too: masking the YAML's `run` again typed the secret in clear.
- **The outputs file is not merged with the earlier one** (2026-11-19). Keeping the earlier entries of unchanged steps was tried, so that a replay would give the cast. A file left from another recording then put old output, or text masked since, back into the new file, entries are found by other rules than cast steps, and a replay after a YAML edit still differed by 1 ms from the kept events. A recording that writes the file now comes from one run.
- **A time on exactly half a millisecond took its side from floating-point noise** (2026-11-19). Event times are whole microseconds, so about one in a thousand sits on x.500 ms, and the noise came from the previous event's time, which is a fraction in a first recording and a whole millisecond in a kept step. An unchanged recording then moved a line by 1 ms. The writer now removes the noise and rounds halves up.
- **The OSC filter drops events, so the steps' event indices follow it.** An event that held only a ConPTY title is removed after the steps are composed, which moved every later step's first event. The filter reports where each event went, and the step ranges are taken through that map before the cast is compared and written.
- **A step that moved is 1 ms off a fresh recording, not off the earlier cast.** After a new step before it, a kept step keeps every interval of the earlier cast, its first output included, while a fresh recording of the same output can round its first event 1 ms the other way. The comparison is with the earlier cast, so that is the one to match.
- **The masker read a control string up to its ST, and terminals end it earlier** (2026-11-19). The cast filter ends an OSC at CAN, SUB, or the next ESC, and the terminal that plays the cast (avt in asciinema player) ends any string there or at a C1 control. So the text after a cut-off title or DCS was shown unmasked, and a DCS that nothing ended hid the rest of the step from the masker while the cast kept its bytes. The SVG renderer also drew the data of SOS, PM, and APC strings as text, which the masker skipped. The masker also ended a run of text at every DCS, which no player draws, so a secret split by an image showed whole and unmasked. In a streamed pipe step, the stderr colors that the cast writes around reads, and sequences in the other stream's reads, cut a string that the masker read as one. Cutting at every read of the other stream was tried and missed secrets too: where nothing ends the string, the masker read the rest of it as text and joined it to the text after.
