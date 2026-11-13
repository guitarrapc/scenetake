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

- Step-level stdin beyond what the shell command provides (non-interactive PTY)
- YAML keys beyond those defined in [spec_scenario.md](spec_scenario.md)
- MiniPty library API details — [MiniPty docs](https://github.com/guitarrapc/MiniPty/blob/main/.github/docs/spec_index.md)

## Recording behavior

When a map-form step has `pty: true` (default `false`):

- **Simulated typing** — scenetake emits the same prompt, per-character typed command, optional `run-highlight`, and synthetic Enter used by non-PTY steps before the PTY output begins. The command is still executed as a PTY shell command. Typing is a recording effect, not PTY stdin.
- **PTY geometry** — uses scenario `width` × `height` (same as cast header terminal size).
- **Shell interpretation** — the `run` string is passed to the scenario `shell` the same way as non-PTY execution (`pwsh -Command`, `cmd /c`, `bash -lc`, etc.). PTY does not bypass the shell.
- **Color environment** — the PTY child gets `COLORTERM=truecolor` like every scenario command, replacing any host value ([spec_scenario.md](spec_scenario.md) → Command Environment). The child sees scenetake's PTY, not the host terminal, so host hints such as `WT_SESSION` say nothing about what the recording can show.
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
- ConPTY cannot identify Windows Terminal directly. Inherited `WT_SESSION` caused `matrix` to select 16 colors when absent, despite renderer support for 24-bit color. Setting `COLORTERM=truecolor` removes this dependency ([spec_scenario.md](spec_scenario.md) → Lessons Learned).
- **ConPTY transforms soft-font sequences.** The Windows ConPTY renames DECDLD fonts, assigns them to G1, and surrounds characters with SO/SI. It answers device queries with soft-font support. Unix PTYs do not answer those queries. The renderer accepts both streams, but application detection controls whether a font is sent ([spec_svg.md](spec_svg.md) → Soft fonts).
