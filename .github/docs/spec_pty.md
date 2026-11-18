# PTY Recording Specification

Status: **Implemented**

## Motivation

Some tools disable color or animation, or refuse to run, when stdout is not a TTY. A scenario step can enable pseudo-terminal capture for commands that need a terminal session. Other steps retain pipe capture.

PTY mode stays opt-in so ordinary steps keep predictable pipe-based stdout/stderr capture and declarative highlighting. YAML keys (`pty`, `width`, `height`): [spec_scenario.md](spec_scenario.md).

## Scope

### In scope

| Layer | Responsibility |
|---|---|
| **MiniPty** / **MiniPty.Capture** | Spawn child in PTY, timestamped byte capture ([MiniPty spec](https://github.com/guitarrapc/MiniPty/blob/main/.github/docs/spec.md)) |
| **MiniPty.Console** | Host-terminal attach for interactive `pty: true` steps ([MiniPty Console spec](https://github.com/guitarrapc/MiniPty/blob/main/.github/docs/spec_console.md)) |
| **scenetake** | Map capture chunks to cast `o` events. Shell launch. Failure handling |

### Out of scope (v1)

- Step-level stdin beyond what the shell command provides and what `keys` writes (non-interactive PTY)
- Waiting for output before a key, as `expect` does. `keys` waits for time only.
- YAML keys beyond those defined in [spec_scenario.md](spec_scenario.md)
- MiniPty library API details — [MiniPty docs](https://github.com/guitarrapc/MiniPty/blob/main/.github/docs/spec_index.md)

## Recording behavior

When a map-form step has `pty: true` (default `false`):

- **Simulated typing** — scenetake emits the same prompt, per-character typed command, optional `run-highlight`, and synthetic Enter used by non-PTY steps before the PTY output begins. The command is still executed as a PTY shell command. Typing is a recording effect, not PTY stdin.
- **PTY geometry** — uses scenario `width` × `height` (same as cast header terminal size).
- **Shell interpretation** — the `run` string is passed to the scenario `shell` the same way as non-PTY execution (`pwsh -Command`, `cmd /c`, `bash -lc`, etc.). PTY does not bypass the shell.
- **Color environment** — the PTY child gets `COLORTERM=truecolor` like every scenario command, replacing any host value ([spec_scenario.md](spec_scenario.md) → Command Environment). The child sees scenetake's PTY, not the host terminal, so host hints such as `WT_SESSION` say nothing about what the recording can show. On Unix MiniPty sets `TERM=xterm-256color` for the child, also when scenetake's own environment has no `TERM`, as in a container: curses tools such as `sl` start there without one.
- **Merged output** — stdout and stderr are one byte stream. `stderr-color` and pipe-style `highlight` do not apply to PTY output for that step.
- **Timestamped chunks** — output is read while the child runs. Cast `o` events use `command_start + chunk_time` on the scenario timeline when stream emit is enabled (see [spec_scenario.md](spec_scenario.md) → Output timing). When stream emit is off, PTY bytes are still timestamped internally but merged according to the existing startup-offset rules before write.
- **Startup timing** — after `PtyLeadingInitFilter` produces the first cast `o` event, scenetake may shift all chunk times earlier so that output appears no later than non-PTY steps (`execution-duration` after `command_start`). When the first emitted chunk is already faster than that threshold, times are unchanged. Inter-chunk spacing (for example TUI frame intervals) is not scaled unless stream emit and `max-duration` are set.
- **Raw byte stream with startup cleanup** — no newline normalization. ANSI sequences may span chunk boundaries. Shell-startup cleanup and immediate alternate-screen exit cleanup are filtered before cast events are written so PTY steps compose with surrounding pipe-recorded steps.
- **No PTY fallback** — if a PTY cannot be created, the run fails fatally. Scenetake does not fall back to pipe redirect execution.

### Screen-state continuity

scenetake removes leading shell initialization controls from every `pty: true` capture before writing cast events. This prevents shell startup from clearing output recorded by earlier steps. It applies to ordinary PTY commands and alternate-screen TUIs, which should restore the previous main-screen history. It also applies to the first step, even without earlier output.

Before the first meaningful output, the filter removes these shell startup controls:

- Screen erase: `CSI 2 J`, `CSI J`, `CSI 0 J`, and `CSI 1 J`.
- Cursor home and position: `CSI H`, `CSI row;col H`, and `CSI row;col f`.
- Cursor position queries: `CSI 6 n`.
- Cursor visibility changes and SGR reset.
- OSC title updates.
- ConPTY, focus, and bracketed-paste private modes.

Cast writing also removes later non-color OSC sequences, including in interactive steps ([spec_cast.md](spec_cast.md) → Sequences left out). ConPTY can send the shell's full path as a title after the first output.

**ConPTY starts at the recording's cursor.** Non-interactive Windows PTY steps use `PSEUDOCONSOLE_INHERIT_CURSOR` (MiniPty `InheritCursor`). Before the child starts, scenetake sends a cursor position report (`ESC[row;colR`). The position comes from replaying existing events with the same terminal model used by SVG and mp4.

ConPTY then preserves preceding rows, redraws lines with `\r`, and scrolls with the recording. Without this position, ConPTY starts at row 1 and can overwrite earlier output. ConPTY consumes the report before the child reads input. Its `CSI 6 n` query is removed during startup filtering.

Unix PTY startup is unchanged because Unix PTYs have no screen model. Interactive startup is also unchanged because output uses the host terminal, whose cursor can differ from the recording's.

The leading phase ends at printable output, alternate-screen entry (`?1049`, `?1047`, or `?47`), or the 4096-byte safety limit. Alternate-screen entry and exit remain intact. Immediately after exit, the filter removes main-screen cleanup controls to preserve restored history. These include cursor home, line erase, blank-line advancement, cursor visibility, SGR reset, and private-mode changes. Other bytes remain unchanged.

When `pty: false` (default):

- stdout and stderr are captured separately via pipe redirect.
- Typing animation and prompt are simulated in the cast.
- Coloring keys apply per [spec_highlight.md](spec_highlight.md).

PTY capture records bytes from the child session. Terminal rendering (ANSI parsing, SVG) is separate: [spec_svg.md](spec_svg.md). OS implementation notes: [MiniPty references/pty_crossplatform.md](https://github.com/guitarrapc/MiniPty/blob/main/.github/docs/references/pty_crossplatform.md).

### Keys and timeout

A TUI often runs until a key ends it, and a demo often needs a menu choice or a typed answer. A non-interactive PTY step can script both, so the recording needs no person at the keyboard and comes out the same on every run.

```yaml
- run: btop
  pty: true
  timeout: 10          # safety net: stop it if the keys did not
  keys:
    - wait: "CPU"      # let it draw
      key: down
    - sleep: 1
      type: "q"
```

#### `keys` entries

| Entry key | Value | What scenetake does |
|---|---|---|
| `sleep` | Seconds, 0 or more | Waits, then goes on to the rest of the entry, or to the next entry |
| `wait` | Text (a number is text too) | Waits until the output shows the text ([`wait`](#wait) below), then goes on |
| `type` | Text (a number is text too) | Writes it one character at a time, each after a typing delay drawn from the step's `typing-speed` and `typing-jitter`, as for a typed command. A line break is Enter. |
| `key` | A key name (table below) | Writes that key |

- Entries run in order. Times count from the command's start, and after a `wait` from when its text appeared.
- An entry holds at least one of the four, and not both `type` and `key`. Within an entry, `sleep` comes first, then `wait`, then `type` or `key`.
- A key right after typed text waits one typing delay. So does a `wait` right after it.
- Writing stops when the command exits. Keys left over are dropped without a message.

#### `wait`

A program's start-up or a command it runs takes a different time on each machine, and a `sleep` long enough for the slowest wastes time on the others. A `wait` goes on as soon as the program shows what the next key answers, such as a prompt.

- **Where it looks.** In the output after the previous key was written, or after the text the previous `wait` matched, or since the command started. So a prompt drawn before a key does not count for a wait after it, and two waits for the same prompt find two prompts, as in expect. Keys typed into a terminal that echoes them come back as output too, so a wait should look for text the keys do not type.
- **How it compares.** The output is read as text without escape sequences. Line breaks, cursor moves, and runs of spaces all count as one space, in the output and in the text. Spaces at the ends of the text do not count, so `wait: "Name? "` also finds a prompt drawn without its trailing space. The comparison is exact otherwise, case included.
- **How long.** In a step with `timeout`, until the timeout stops the command. In a step without one, 10 seconds. Then stderr shows `Warning: step N keys entry M waited 10 s for "text" and goes on without it`, and the next entry runs. A text that never appears does not hold the recording up.
- A command that exits while a wait is open drops the keys left over, as for other entries.

#### Key names

Names ignore case. Each sends the bytes xterm sends in normal cursor mode. Programs in application cursor mode, such as vim, read those arrows too.

| Key name | Bytes sent |
|---|---|
| `enter` (`return`) | CR (`0x0D`) |
| `tab` | HT (`0x09`) |
| `space` | A space (`0x20`) |
| `backspace` | DEL (`0x7F`) |
| `escape` (`esc`) | ESC (`0x1B`) |
| `up`, `down`, `right`, `left` | `ESC [ A`, `ESC [ B`, `ESC [ C`, `ESC [ D` |
| `home`, `end` | `ESC [ H`, `ESC [ F` |
| `page-up` (`pageup`), `page-down` (`pagedown`) | `ESC [ 5 ~`, `ESC [ 6 ~` |
| `insert`, `delete` | `ESC [ 2 ~`, `ESC [ 3 ~` |
| `f1`–`f4` | `ESC O P`, `ESC O Q`, `ESC O R`, `ESC O S` |
| `f5`–`f12` | `ESC [ 15 ~`, `ESC [ 17 ~`, `ESC [ 18 ~`, `ESC [ 19 ~`, `ESC [ 20 ~`, `ESC [ 21 ~`, `ESC [ 23 ~`, `ESC [ 24 ~` |
| `ctrl+` and a letter, such as `ctrl+c` | The control character: `ctrl+a` is `0x01`, `ctrl+c` is `0x03`, `ctrl+z` is `0x1A` |
| `alt+` and one printable ASCII character, such as `alt+x` | ESC, then the character |

#### `timeout`

Seconds from the command's start, above 0. When the command is still running then:

| Step | At the timeout | If it is still running |
|---|---|---|
| PTY | Ctrl+C, as a person would press it, so a TUI can leave the alternate screen and restore the terminal. The recording shows that. | SIGTERM after 2 seconds. Ended 1 second after that. |
| Pipe | Its process tree ends. On Windows every process it started ends, through a job object, including one whose parent has exited. What it printed so far is its output. | — |

- stderr shows `stopped after N s (timeout)`.
- **Why a job object on Windows.** Git Bash runs a command in a child whose parent then exits, once the shell has an EXIT trap, which the shell state sets ([spec_scenario.md](spec_scenario.md) → Shell state across steps). Ending the process tree missed that child, which held the pipes open: a step stopped after 1 second waited 30 for its `sleep 30`.
- **A stop is no failure.** The author asked for it, so its exit code (130, or a signal's) is recorded as 0: no warning, and `--strict` goes on.

#### Errors

Each of these stops the run before `pre`, so a typo does not cost a recording. The message names the step and the entry.

| Scenario | Error |
|---|---|
| `keys` without `pty: true` | `Step N key 'keys' requires 'pty: true'` |
| `keys` or `timeout` with `interactive: true` | `Step N key 'keys' does not apply to 'interactive: true'` (or `'timeout'`) |
| `timeout` that is not a number above 0 | `Step N key 'timeout' must be a number of seconds above 0` |
| `keys` that is not a list | `Step N key 'keys' must be a list` |
| An entry that is not a mapping | `Step N keys entry M must be a mapping with 'sleep', 'wait', 'type', or 'key'` |
| An entry with none of `sleep`, `wait`, `type`, `key` | `Step N keys entry M needs 'sleep', 'wait', 'type', or 'key'` |
| An entry with both `type` and `key` | `Step N keys entry M has both 'type' and 'key'` |
| A negative or non-numeric `sleep` | `Step N keys entry M 'sleep' must be a number of seconds` |
| A `wait` with only spaces and line breaks, which match nothing, or a list or mapping under it | `Step N keys entry M 'wait' must be text the program prints` |
| An empty `type`, or a list or mapping under it | `Step N keys entry M 'type' must be text to type` |
| An unknown key name | `Step N keys entry M has an unknown key 'x'` |

An unknown key inside an entry, such as `slep`, is a warning like other unknown keys.

#### Recording, cast, and playground

- **Recording path.** A PTY step with `keys` or `timeout` reads the PTY itself while it writes the keys, timing each chunk on arrival, as interactive steps do: writing keys needs the session while output is read, which MiniPty.Capture does not expose. Other PTY steps keep MiniPty.Capture, which joins bursts differently, so their recordings stay as they were. Startup cleanup, the ConPTY cursor report, and output timing are the same for both.
- **Cast.** Keys are input, so the cast has no event for them. The recording shows what the program echoes or draws in answer.
- **Playground.** The outputs file holds the output the keys produced. The playground draws the same typing delays for `type`, so later steps type as on the CLI, but it writes nothing, waits for nothing, and stops nothing ([spec_playground.md](spec_playground.md) → Keys that have no effect). A `wait` draws no typing delay.

### Interactive PTY (`interactive: true`)

When a map-form step has `pty: true` and `interactive: true` (default `false`):

- **Host terminal required** — stdin and stdout on the scenetake process must not be redirected. Scenetake checks this before `pre` runs when any step is interactive, so `pre` and the steps before it do not run only to be thrown away. Scenetake attaches the host console to the PTY child via **MiniPty.Console** until the child exits.
- **Simulated typing first** — scenetake records the prompt, typed command, and synthetic Enter before the interactive session, as with other steps. Typing is a recording effect. Keystrokes during the session go to the PTY through the host terminal, not through the pre-recorded typing.
- **Live output on host** — PTY output is pumped to the host stdout while the session runs so the author can see the TUI.
- **Wall-clock cast times (default)** — buffered PTY chunks keep measured arrival times. Cast `o` events use `command_start + chunk_time`. **No** startup-offset compress, burst merge, idle collapse, or `stream-output` pipeline for interactive steps.
- **`normalize-typing: true` (opt-in)** — requires `interactive: true`. Each host keystroke opens a short **echo window** (~450ms). PTY output **outside** those windows (banner, streamed AI, redraws) stays wall-clock. **Inside** echo windows: chunks with ANSI/ESC pass through unchanged. Multi-byte or long chunks pass through (banner box-drawing). Short printable echo bytes are **retimed** at `typing-speed` while preserving raw PTY bytes (cursor layout intact). Input log filters out echo for characters later erased by Backspace before they reach the cast. After normalization, idle gaps use the original wall-clock times. Gaps exceeding `silence-threshold` (default `2.0`s) shrink to `max-idle` (default `0.2`s). Banner frame intervals below the threshold retain their timing. Think-time pauses shrink.
- **Parallel input log** — host keystrokes are recorded as `PtyInputChunks` via `IPtyConsoleInputObserver` (required for `normalize-typing`). The log only times and matches echo. The cast holds PTY output, never typed keys.
- **Password keys are not logged** — Unix excludes keys when terminal echo is off and canonical mode is on. `PtySession.InputMode` reports the mode when each key is forwarded. These keys open no echo window, so normalization does not wait for absent echoes. Masked prompts that use raw mode are not recognized. Windows reports no input mode and logs every key.
- **Startup cleanup only** — `PtyLeadingInitFilter` applies the same screen continuity rules as non-interactive PTY steps.
- **Fixed PTY geometry** — `SyncHostSize = false` keeps scenario `width` × `height`.
- **Step ends on child exit** — for example, `:wq` in vim or command completion.
- **Status on stderr** — attach progress and verbose output/input counts use stderr, separate from the Unix PTY stream.
- **`normalize-typing` scope** — supports insert-mode typing at line end and Backspace corrections. Normal-mode cursor motion (`hjkl`) adds no cast events. Destructive normal-mode edits (`x`, `dd`, `dw`) are not normalized.
- **No interactive duration limit** — `stream-output`, `max-duration`, and per-step compression do not apply. `max-duration` does not terminate an interactive session.

| Condition | Behavior |
|---|---|
| `interactive: true` without `pty: true` | Scenario error, reported before `pre` runs |
| `normalize-typing: true` without `interactive: true` | Scenario error, reported before `pre` runs |
| Host stdin or stdout redirected | Scenario error, reported before `pre` runs |
| `Pty.IsSupported` false | Fatal error (same as other PTY steps) |

Non-interactive `pty: true` behavior is unchanged. See cast timing contrast in [spec_scenario.md](spec_scenario.md) → Output timing.

### Saving the keys of an interactive step (`--save-keys`)

An interactive step needs a person at the keyboard on every recording. `scenetake --save-keys FILE scenario.yaml` writes what the person typed as `keys` entries, so the step can become one that replays the session with no one there.

```yaml
# Keys typed in the interactive steps of demo.yaml, saved by scenetake --save-keys.
# To replay a step without a person, replace 'interactive: true' in that step with its 'keys'
# below, and remove 'normalize-typing'. Each 'type' replays at the step's typing-speed.
# Where a program takes a varying time, a 'wait' for text it prints is steadier than a 'sleep'.

steps:
  # step 2
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

- **Where the keys go.** To a file of their own, a `steps` list with each interactive step's `run` and `keys`, so it is valid YAML and each block reads like its step. scenetake never writes the scenario: rewriting a hand-written file would lose its comments and layout, and a flow mapping such as `- {run: vim, pty: true, interactive: true}` has no line to put `keys` on. The author moves the `keys` into the step. They belong in the scenario, not in a file it names, because the playground reads only the scenario and the outputs file, and it needs the `type` entries to draw the same typing delays.
- **How keys become entries.**

  | Typed | Entry |
  |---|---|
  | Characters with less than 0.5 s between them | One `type`. The replay types it at the step's `typing-speed`, so uneven typing evens out. |
  | Enter right after them | A line break that ends the `type`. What the person typed next came after the program's answer. |
  | A key from the key table, and Enter after a pause | One `key` |
  | A pause | A `sleep` on the next entry, to 0.1 s. After typed text it is one typing delay shorter, because the replay waits that long by itself. |
  | Input without a key name: mouse reports, and keys with modifiers outside the key table, such as Ctrl+Up or Shift+Tab | A comment `not saved: ...` where it was |
  | Answers the host terminal sends to the program's queries (cursor position, device attributes, modes, colors), focus events, and bracketed paste markers | Nothing. The person did not type them. |

- **Input forms.** On Unix the host terminal sends xterm bytes. ConPTY asks the host terminal for win32-input-mode, in which each key press and release is a record with its virtual key and character, and a host may also send plain xterm bytes. Both read as the same key names.
- **Passwords.** On Unix, keys typed while the program reads a password (echo off, canonical mode on) are never recorded ([Interactive PTY](#interactive-pty-interactive-true)), so the file has a comment `not saved: keys typed while the program read a password` where they were. A replay stops at that prompt until a `type` with the password is added. It goes after a `wait` for the prompt: typed earlier, it reaches the terminal before the program turns echo off, and the terminal shows it. Windows reports no input mode, so it saves every key, and the file's header says that a password is in it too.
- **When it is written.** Right after the steps run, before the cast, and also when `--strict` or a failing step stops the recording: the person's session is the part that is hard to repeat. A failure to write it fails the run (exit 1) after the other outputs are written.

| Condition | Behavior |
|---|---|
| The scenario has no interactive step | Error before `pre`: `--save-keys saves the keys typed in 'interactive: true' steps, and this scenario has none.` |
| FILE is the scenario or another output of the run | Error before `pre` |
| `capture` | Not taken (`unknown option`): it has no interactive step |
| An interactive step with no keys typed | Its block has the comment `no keys were typed in this step` and no `keys` |
| The recording stops before an interactive step runs | No file. stderr shows `Warning: --save-keys wrote nothing: no interactive step ran.` |

## Platform support

| OS | Backend | Minimum |
|---|---|---|
| Windows | ConPTY (`CreatePseudoConsole`) | Windows 10 1809+, Windows 11 |
| Linux | `openpty` + `fork` + `execvp` | Common glibc/musl targets |
| macOS | `openpty` + `fork` + `execvp` | Supported runners |
| FreeBSD | `openpty` + `fork` + `execvp` | `libutil` + BSD `TIOCSCTTY` |

Pipe redirect without ConPTY is **not** a PTY. TUI tools (`matrix`, `vim`, etc.) require `pty: true` on Windows.

## Failure behavior

PTY steps follow the same **recorded step** exit-code rules as pipe steps. See [spec_scenario.md](spec_scenario.md) → Step exit codes.

| Condition | Behavior |
|---|---|
| Child non-zero exit | Warning on stderr. Recording continues. Scenetake exits `0` (same as pipe `steps`) |
| PTY spawn / ConPTY / `openpty` failure | Fatal error. The recording stops |
| Output drain timeout | Fatal error (`TimeoutException` from capture) |
| Interactive step on non-TTY host | Fatal error at step entry |
| Cancel during interactive attach | Host terminal restored on dispose. Scenario aborts if the run is interrupted |

Each fatal error stops the recording: no cast is written, `post` runs, and scenetake exits `1`, or with the failing `post` command's code ([spec_pre_post.md](spec_pre_post.md)).

## Verification

| Test | Location |
|---|---|
| MiniPty core + Capture | [MiniPty/tests](https://github.com/guitarrapc/MiniPty) |
| PTY layer | `scenetake/tests/pty_test.cs` |
| PTY startup cleanup filter | `scenetake/tests/pty_continue_test.cs` |
| Interactive PTY validation | `scenetake/tests/interactive_pty_test.cs` |
| `keys` and `timeout`: key names, errors, timing, `wait` timing, and the playground. With `SCENETAKE_BIN`: typed input, Ctrl+C, the grace kill, a pipe timeout, a `wait` that types after its prompt, a `wait` that gives up, and `--save-keys` without an interactive step | `scenetake/tests/step_input_test.cs`, fixtures `scenetake/tests/fixtures/step-input-*.yaml` |
| `--save-keys`: xterm and win32-input-mode input to key names, terminal answers left out, entries and pauses, password notes, and the file read back as keys. `wait` text matching | `scenetake/tests/key_recording_test.cs` |
| Interactive input recorder | `scenetake/tests/interactive_pty_input_test.cs` |
| Command environment (`COLORTERM`) for pipe and PTY steps | `scenetake/tests/pty_test.cs` (`IntegrationChildEnvironmentFixture`) |
| Fixture scenarios | `scenetake/tests/fixtures/pty-*.yaml`, `scenetake/tests/fixtures/child-env*.yaml` |

Integration tests require `SCENETAKE_BIN` pointing at a published scenetake binary.

## Cross-Document Notes

- [spec_scenario.md](spec_scenario.md) — `pty`, `interactive`, `width`, `height` keys and defaults
- [spec_cast.md](spec_cast.md) — cast event format
- [spec_cli.md](spec_cli.md) — stderr warnings for non-zero step exits
- [spec_pre_post.md](spec_pre_post.md) — contrast with `pre`/`post` fail-fast

## Lessons Learned

- Pipe redirect is not a PTY. ConPTY is required on Windows for TUI tools.
- Keeping `pty` opt-in preserves simpler pipe behavior for ordinary commands.
- PTY startup should preserve earlier output. `pty: true` requests a terminal, so shell initialization should not clear the recording.
- PTY command typing should be emitted by default for visual continuity with pipe steps, but it remains a recording effect. The command still runs through the configured shell in the PTY. Scenetake does not inject the typed characters into PTY stdin.
- Alternate-screen TUI tools may restore the main screen and then emit extra cleanup controls. Preserving the alternate-screen enter/exit while filtering that immediate main-screen cleanup keeps the TUI behavior intact and returns the viewer to the previous history. The cleanup phase must not end early on private-mode toggles such as `?12h` (cursor blink) between `?1049l` and the erase-to-end-of-line / cursor-home sequences. Copilot CLI exhibits this pattern.
- Interactive PTY steps need the output pump running **before** `PtyConsoleInput.Attach` on Windows ConPTY. Reversing that order can stall the session.
- Interactive steps record wall-clock chunk times so TUI motion (banner animations, spinner frames) replays at real speed in cast/SVG. Startup-offset compress applies only to non-interactive PTY.
- **Host resize sync breaks vim/svg replay**: `PtyConsoleInput.Attach` defaults to resizing the PTY to the host console. Vim then emits cursor addresses for the larger height (for example row 44 on a 44-row host) while the cast header stays at scenario `height` (24). SVG replay clamps out-of-range rows and corrupts the session. Scenetake uses `PtyConsoleAttachOptions.SyncHostSize = false` so the PTY keeps scenario geometry.
- On Unix, scenetake writes `\r\n` to host stdout after detach so the parent shell prompt realigns after raw-mode interactive output.
- **Password keys blocked later echo matching.** Normalization matches output to logged keys in order. Unix password input disables echo but keeps canonical mode, so those keys never appear in output. With password keys logged, matching discarded later echoes. A Linux PTY test on 2026-11-13 used `read -s`, then `read -p '> '` with `ls`. It recorded `> s` because `s` matched the password's first letter. Excluding password keys produced `> ls` in all three repeats. MiniPty 1.6.0 reports the input mode before forwarding each key, including the Enter that ends password entry. Echo alone cannot identify passwords: vim and shell editors disable echo and canonical mode, then draw characters themselves.
- **ConPTY redraws overwrote earlier output.** Windows progress bars using `\r` became cursor-home sequences because ConPTY started with an empty screen at row 1. This overwrote preceding steps and disrupted later output. The same Linux scenario retained `\r` and rendered correctly. Tests on Windows 11 26200 (2026-11-13) confirmed that `InheritCursor` plus an initial position report preserved `\r` and removed startup clears. The child received no extra input, and runtime did not increase. ConPTY already had the report when it needed it. Starting at row 29 of 30 scrolled correctly, and `matrix` rendering was unchanged. The ConPTY title also arrived before the first output, where startup filtering removed it.
- **Interactive terminal checks must precede `pre`.** Previously, redirected stdin or stdout failed only when the interactive step began. Setup and earlier steps ran before the recording was discarded. Redirection does not change during a run, so validation now checks it before `pre`.
- Recorded failures normally remain demo content. `--strict` stops unexpected non-zero exits. See [spec_scenario.md](spec_scenario.md) → Step exit codes.
- **Ctrl+C from a timeout reaches the program through both PTYs.** On 2026-11-16 a test whose trap leaves the alternate screen on SIGINT showed its message in the cast on Linux, and on Windows through ConPTY and Git Bash. A command that ignores SIGINT was ended by the later SIGTERM and kill within the timeout plus about 3 seconds. On Linux the terminal echoes `^C`, which the cast keeps. nyancat clears its screen when it gets Ctrl+C.
- **A container needs no `TERM`.** With `TERM` removed from scenetake's environment, a Linux PTY step still saw `TERM=xterm-256color`, which MiniPty sets.
- ConPTY cannot identify Windows Terminal directly. Inherited `WT_SESSION` caused `matrix` to select 16 colors when absent, despite renderer support for 24-bit color. Setting `COLORTERM=truecolor` removes this dependency ([spec_scenario.md](spec_scenario.md) → Lessons Learned).
- **ConPTY transforms soft-font sequences.** The Windows ConPTY renames DECDLD fonts, assigns them to G1, and surrounds characters with SO/SI. It answers device queries with soft-font support. Unix PTYs do not answer those queries. The renderer accepts both streams, but application detection controls whether a font is sent ([spec_svg.md](spec_svg.md) → Soft fonts).
- **A wait test passed without the wait.** On Windows, MSYS bash echoes keys typed ahead only when `read` reads them, so typing the answer at once and typing it after the prompt left the same cast on 2026-11-17. The fixture now reads once with a short timeout before the prompt. That read takes keys typed too early, so only a `wait` gets the answer to the prompt.
- **ConPTY sent a prompt's trailing space.** On Windows 11 26200, with the ConPTY that MiniPty ships, `printf 'Name? '` arrived as `Name? ` with its space. `wait` still ignores spaces at the ends of its text and counts cursor moves as spaces, because a terminal can draw spaces by moving the cursor, and a prompt's last space prints nothing visible.
- **`--save-keys` end to end on Windows and Linux.** On 2026-11-17, on Windows, keys written with `WriteConsoleInput` into the console that scenetake ran in came back as `type: "abc\n"`, `key: up`, `key: backspace`, and `type: "xy\n"`, with the pauses between them. A replay of those keys in place of `interactive: true` printed the same `done abc y` as the interactive run. In a Debian 13 container, keys fed through script(1) as a terminal sends them, with a password read by `read -s` between, came back the same way, with the password note in its place. With `wait: "password?"` and the password added there, the replay printed the same `done abc 6 y` as the interactive run, and neither cast held the password. A password typed without the wait would have arrived before `read -s` turned echo off, and the terminal would have shown it.
