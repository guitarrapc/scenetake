# PTY Recording Specification

Status: **Implemented**

## Motivation

Some CLI tools and terminal UIs behave differently when stdout is not a TTY: they disable color, skip animations, or refuse to run. scenetake records demos as asciinema casts; for commands that need a real terminal session, a step can opt into pseudo-terminal capture without changing the rest of the scenario format.

PTY mode stays opt-in so ordinary steps keep predictable pipe-based stdout/stderr capture and declarative highlighting. YAML keys (`pty`, `width`, `height`): [spec_scenario.md](spec_scenario.md).

## Scope

### In scope

| Layer | Responsibility |
|---|---|
| **MiniPty** / **MiniPty.Capture** | Spawn child in PTY, timestamped byte capture ([MiniPty spec](https://github.com/guitarrapc/MiniPty/blob/main/.github/docs/spec.md)) |
| **MiniPty.Console** | Host-terminal attach for interactive `pty: true` steps ([MiniPty Console spec](https://github.com/guitarrapc/MiniPty/blob/main/.github/docs/spec_console.md)) |
| **scenetake** | Map capture chunks to cast `o` events; shell launch; failure handling |

### Out of scope (v1)

- Step-level stdin beyond what the shell command provides (non-interactive PTY)
- YAML keys beyond those defined in [spec_scenario.md](spec_scenario.md)
- MiniPty library API details — [MiniPty docs](https://github.com/guitarrapc/MiniPty/blob/main/.github/docs/spec_index.md)

## Recording behavior

When a map-form step has `pty: true` (default `false`):

- **Simulated typing** — scenetake emits the same prompt, per-character typed command, optional `run-highlight`, and synthetic Enter used by non-PTY steps before the PTY output begins. The command is still executed as a PTY shell command; typing is a recording effect, not PTY stdin.
- **PTY geometry** — uses scenario `width` × `height` (same as cast header terminal size).
- **Shell interpretation** — the `run` string is passed to the scenario `shell` the same way as non-PTY execution (`pwsh -Command`, `cmd /c`, `bash -lc`, etc.). PTY does not bypass the shell.
- **Color environment** — the PTY child gets `COLORTERM=truecolor` like every scenario command, replacing any host value ([spec_scenario.md](spec_scenario.md) → Command Environment). The child sees scenetake's PTY, not the host terminal, so host hints such as `WT_SESSION` say nothing about what the recording can show.
- **Merged output** — stdout and stderr are one byte stream. `stderr-color` and pipe-style `highlight` do not apply to PTY output for that step.
- **Timestamped chunks** — output is read while the child runs; cast `o` events use `command_start + chunk_time` on the scenario timeline when stream emit is enabled (see [spec_scenario.md](spec_scenario.md) → Output timing). When stream emit is off, PTY bytes are still timestamped internally but merged according to the existing startup-offset rules before write.
- **Startup timing** — after `PtyLeadingInitFilter` produces the first cast `o` event, scenetake may shift all chunk times earlier so that output appears no later than non-PTY steps (`execution-duration` after `command_start`). When the first emitted chunk is already faster than that threshold, times are unchanged. Inter-chunk spacing (for example TUI frame intervals) is not scaled unless stream emit and `max-duration` are set.
- **Raw byte stream with startup cleanup** — no newline normalization; ANSI sequences may span chunk boundaries. Shell-startup cleanup and immediate alternate-screen exit cleanup are filtered before cast events are written so PTY steps compose with surrounding pipe-recorded steps.
- **No PTY fallback** — if a PTY cannot be created, the run fails fatally. scenetake does not fall back to pipe redirect execution.

### Screen-state continuity

For every `pty: true` step, scenetake removes shell initialization sequences from the beginning of that PTY capture before writing cast `o` events. This supports mixed scenarios where an ordinary step is followed by a PTY command and the PTY shell startup would otherwise clear the accumulated terminal state. It applies both to lightweight commands such as `echo` and alternate-screen TUI commands such as `matrix`, where the TUI should run full-screen and then return to the previous main-screen history. It is also valid on the first step; in that case there may simply be no prior screen state to preserve.

The filter applies first to the leading initialization phase, before the first meaningful user output. It strips common shell startup controls such as full-screen erase (`CSI 2 J`), erase-to-screen-end (`CSI J`, `CSI 0 J`, `CSI 1 J`), cursor home / position (`CSI H`, `CSI row;col H`, `CSI row;col f`), cursor visibility toggles, SGR reset, OSC title updates, and platform mode toggles such as ConPTY/focus/bracketed-paste private modes.

The leading phase ends as soon as printable output appears, an alternate-screen mode (`?1049`, `?1047`, or `?47`) appears, or a 4096-byte safety limit is reached. Alternate-screen enter/exit sequences are not stripped, so TUI commands retain their own terminal behavior. When an alternate-screen exit is followed immediately by main-screen cleanup controls such as cursor home, line erase, blank-line advancement, cursor visibility toggles, SGR reset, or platform private-mode toggles, those cleanup controls are stripped so the restored main-screen history remains visible. Other bytes after the leading phase are recorded unchanged.

When `pty: false` (default):

- stdout and stderr are captured separately via pipe redirect.
- Typing animation and prompt are simulated in the cast.
- Coloring keys apply per [spec_highlight.md](spec_highlight.md).

PTY capture records bytes from the child session. Terminal rendering (ANSI parsing, SVG) is separate: [spec_svg.md](spec_svg.md). OS implementation notes: [MiniPty references/pty_crossplatform.md](https://github.com/guitarrapc/MiniPty/blob/main/.github/docs/references/pty_crossplatform.md).

### Interactive PTY (`interactive: true`)

When a map-form step has `pty: true` and `interactive: true` (default `false`):

- **Host terminal required** — stdin and stdout on the scenetake process must not be redirected. scenetake attaches the host console to the PTY child via **MiniPty.Console** until the child exits.
- **Simulated typing first** — the same prompt, per-character command typing, and synthetic Enter used by other steps are emitted on the cast timeline **before** the interactive session begins. Typing is a recording effect; keystrokes during the session go to the PTY through the host terminal, not through the pre-recorded typing.
- **Live output on host** — PTY output is pumped to the host stdout while the session runs so the author can see the TUI.
- **Wall-clock cast times (default)** — buffered PTY chunks keep measured arrival times. cast `o` events use `command_start + chunk_time`. **No** startup-offset compress, burst merge, idle collapse, or `stream-output` pipeline for interactive steps.
- **`normalize-typing: true` (opt-in)** — requires `interactive: true`. Each host keystroke opens a short **echo window** (~450ms). PTY output **outside** those windows (banner, streamed AI, redraws) stays wall-clock. **Inside** echo windows: chunks with ANSI/ESC pass through unchanged; multi-byte or long chunks pass through (banner box-drawing); short printable echo bytes are **retimed** at `typing-speed` while preserving raw PTY bytes (cursor layout intact). Input log filters out echo for characters later erased by Backspace before they reach the cast. After normalization, long **idle gaps** between output bursts (measured on original wall-clock times) collapse using scenario `silence-threshold` (default `2.0`s) and `max-idle` (default `0.2`s) — banner frame intervals under the threshold stay real-time; think-time pauses shrink.
- **Parallel input log** — host keystrokes are recorded as `PtyInputChunks` via `IPtyConsoleInputObserver` (required for `normalize-typing`).
- **Startup cleanup only** — `PtyLeadingInitFilter` runs on the buffered bytes (same screen-state continuity rules as non-interactive PTY).
- **Fixed PTY geometry** — `SyncHostSize = false` so the child uses scenario `width` × `height`.
- **Step ends on child exit** — for example `:wq` in vim or command completion.
- **Status on stderr** — attach progress and verbose chunk logs (output + input event counts) go to stderr so they do not mix with the PTY byte stream on Unix.
- **`normalize-typing` scope** — insert-mode end-of-line typing with Backspace correction stripping; normal-mode cursor motion (`hjkl`) does not add extra cast events. Normal-mode destructive edits (`x`, `dd`, `dw`) are not normalized yet.
- **No stream pipeline or forced kill** — `stream-output`, `max-duration`, and per-step timeline compression do not apply to interactive steps. There is no `max-duration` forced kill of a running interactive session.

| Condition | Behavior |
|---|---|
| `interactive: true` without `pty: true` | Scenario error, reported before `pre` runs |
| `normalize-typing: true` without `interactive: true` | Scenario error, reported before `pre` runs |
| Host stdin or stdout redirected | Fatal error at step entry |
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
| Child non-zero exit | Warning on stderr; recording continues; scenetake exits `0` (same as pipe `steps`) |
| PTY spawn / ConPTY / `openpty` failure | Fatal error; the recording stops |
| Output drain timeout | Fatal error (`TimeoutException` from capture) |
| Interactive step on non-TTY host | Fatal error at step entry |
| Cancel during interactive attach | Host terminal restored on dispose; scenario aborts if the run is interrupted |

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

- Pipe redirect is not a PTY; ConPTY is required on Windows for TUI tools.
- Keeping `pty` opt-in preserves simpler pipe behavior for ordinary commands.
- Treating screen-state continuity as the default PTY behavior matches user expectations: `pty: true` means a command needs a terminal, not that shell startup clears should become visible demo content.
- PTY command typing should be emitted by default for visual continuity with pipe steps, but it remains a recording effect. The command still runs through the configured shell in the PTY; scenetake does not inject the typed characters into PTY stdin.
- Alternate-screen TUI tools may restore the main screen and then emit extra cleanup controls. Preserving the alternate-screen enter/exit while filtering that immediate main-screen cleanup keeps the TUI behavior intact and returns the viewer to the previous history. The cleanup phase must not end early on private-mode toggles such as `?12h` (cursor blink) between `?1049l` and the erase-to-end-of-line / cursor-home sequences; Copilot CLI exhibits this pattern.
- Interactive PTY steps need the output pump running **before** `PtyConsoleInput.Attach` on Windows ConPTY; reversing that order can stall the session.
- Interactive steps record wall-clock chunk times so TUI motion (banner animations, spinner frames) replays at real speed in cast/SVG. Startup-offset compress applies only to non-interactive PTY.
- **Host resize sync breaks vim/svg replay**: `PtyConsoleInput.Attach` defaults to resizing the PTY to the host console. vim then emits cursor addresses for the larger height (for example row 44 on a 44-row host) while the cast header stays at scenario `height` (24). SVG replay clamps out-of-range rows and corrupts the session. scenetake uses `PtyConsoleAttachOptions.SyncHostSize = false` so the PTY keeps scenario geometry.
- On Unix, scenetake writes `\r\n` to host stdout after detach so the parent shell prompt realigns after raw-mode interactive output.
- Recorded step failures belong in the cast as demo content; only infrastructure failures (spawn, drain timeout) should stop the recording. See [spec_scenario.md](spec_scenario.md) → Step exit codes.
- A TUI behind ConPTY cannot detect Windows Terminal; it can only read `WT_SESSION` inherited from the shell that started scenetake. A pwsh without `WT_SESSION` made `matrix` fall back to 16 colors even though the recording renders 24-bit color. scenetake now sets `COLORTERM=truecolor` for PTY children instead of relying on host hints ([spec_scenario.md](spec_scenario.md) → Lessons Learned).
