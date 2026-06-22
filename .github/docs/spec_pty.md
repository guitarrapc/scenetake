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
| **scenetake** | Map capture chunks to cast `o` events; shell launch; failure handling |

### Out of scope (v1)

- Long-lived interactive sessions (vim, REPLs)
- Step-level stdin beyond what the shell command provides
- YAML keys beyond those defined in [spec_scenario.md](spec_scenario.md)
- MiniPty library API details — [MiniPty docs](https://github.com/guitarrapc/MiniPty/blob/main/.github/docs/spec_index.md)

## Recording behavior

When a map-form step has `pty: true` (default `false`):

- **No simulated typing** — the cast does not emit prompt, per-character typing, or synthetic Enter for that step. Other steps without `pty` keep the usual typing recording.
- **PTY geometry** — uses scenario `width` × `height` (same as cast header terminal size).
- **Shell interpretation** — the `run` string is passed to the scenario `shell` the same way as non-PTY execution (`pwsh -Command`, `cmd /c`, `bash -lc`, etc.). PTY does not bypass the shell.
- **Merged output** — stdout and stderr are one byte stream. `stderr-color` and pipe-style `highlight` do not apply to PTY output for that step.
- **Timestamped chunks** — output is read while the child runs; cast `o` events use `command_start + chunk_time` on the scenario timeline.
- **Raw byte stream** — no newline normalization; ANSI sequences may span chunk boundaries.
- **No fallback** — if a PTY cannot be created, the run fails fatally. scenetake does not fall back to pipe redirect or simulated typing.

When `pty: false` (default):

- stdout and stderr are captured separately via pipe redirect.
- Typing animation and prompt are simulated in the cast.
- Coloring keys apply per [spec_highlight.md](spec_highlight.md).

PTY capture records bytes from the child session. Terminal rendering (ANSI parsing, SVG) is separate: [spec_svg.md](spec_svg.md). OS implementation notes: [MiniPty references/pty_crossplatform.md](https://github.com/guitarrapc/MiniPty/blob/main/.github/docs/references/pty_crossplatform.md).

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
| PTY spawn / ConPTY / `openpty` failure | Fatal error; scenario run aborts |
| Output drain timeout | Fatal error (`TimeoutException` from capture) |
| Cancel during interactive API use | Not used by scenetake scenario path in v1 |

## Verification

| Test | Location |
|---|---|
| MiniPty core + Capture | [MiniPty/tests](https://github.com/guitarrapc/MiniPty) |
| PTY layer | `scenetake/tests/pty_test.cs` |
| Fixture scenarios | `scenetake/tests/fixtures/pty-*.yaml` |

Integration tests require `SCENETAKE_BIN` pointing at a published scenetake binary.

## Cross-Document Notes

- [spec_scenario.md](spec_scenario.md) — `pty`, `width`, `height` keys and defaults
- [spec_cast.md](spec_cast.md) — cast event format
- [spec_cli.md](spec_cli.md) — stderr warnings for non-zero step exits
- [spec_pre_post.md](spec_pre_post.md) — contrast with `pre`/`post` fail-fast

## Lessons Learned

- Pipe redirect is not a PTY; ConPTY is required on Windows for TUI tools.
- Keeping `pty` opt-in preserves simpler pipe behavior for ordinary commands.
- Recorded step failures belong in the cast as demo content; only infrastructure failures (spawn, drain timeout) should abort the run. See [spec_scenario.md](spec_scenario.md) → Step exit codes.
