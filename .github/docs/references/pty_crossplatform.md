# Cross-Platform PTY Implementation Reference

This document describes **how** scenetake implements pseudo-terminal recording. User-facing behavior lives in [spec_pty.md](../spec_pty.md).

Implementation code: `PseudoTerminal.cs` (included from `scenetake.cs`).

## Architecture

scenetake uses a single OS-specific backend selected at runtime:

| OS | API | Entry point |
|---|---|---|
| Windows | ConPTY (`CreatePseudoConsole`) | `WindowsPseudoTerminal.Run` |
| Linux / macOS / FreeBSD | `openpty` + `fork` + `execvp` | `UnixPseudoTerminal.Run` |

Upper layers (`RunCommandCore`, cast generation) call `PseudoTerminal.Run` with the scenario shell executable and arguments. They receive a `CommandOutput` with timestamped `CommandOutputChunk` entries.

PTY capture and terminal display are separate:

| Layer | Responsibility | Code |
|---|---|---|
| **PTY backend** | Spawn child, attach PTY, read/write bytes, wait, exit code | `PseudoTerminal.cs` |
| **Terminal display** | Parse ANSI/VT, screen buffer, SVG rendering | `Terminal.cs`, `Svg.cs` |

Do not parse escape sequences inside the PTY backend. Do not spawn processes from the SVG renderer.

## Windows: ConPTY

### Topology

Pipes are transport only. The pseudo-terminal is the `HPCON` from `CreatePseudoConsole`:

```text
scenetake (parent)
  ├─ write → input pipe  → ConPTY (HPCON) → child stdin/stdout/stderr
  ├─ read  ← output pipe ← ConPTY
  └─ CreateProcess with PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE
```

Redirecting `CreateProcess` stdin/stdout to anonymous pipes **without** ConPTY is not a PTY. Children report stdout as redirected; TUI tools such as `matrix` will not render.

### Setup sequence

1. Create input and output pipe pairs (`CreatePipe` with inheritable handles).
2. Mark the **parent** ends non-inheritable (`SetHandleInformation` on `inputWrite` and `outputRead`).
3. `CreatePseudoConsole(size, inputRead, outputWrite, …)` → `HPCON`.
4. **Close** `inputRead` and `outputWrite` in the parent immediately (ConPTY holds its own duplicates).
5. Build `STARTUPINFOEX` with `PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE`.
6. `CreateProcessW` with `EXTENDED_STARTUPINFO_PRESENT` and `bInheritHandles = false`.
7. Start a background read on `outputRead` before or as the child runs.
8. On shutdown: wait for child → close `inputWrite` → `ClosePseudoConsole` → drain read task.

### Parent console attachment

When scenetake runs attached to a real console (e.g. `dotnet run`), the OS may duplicate the parent's standard handles into a ConPTY child unless explicitly prevented. scenetake sets:

```text
STARTUPINFO.dwFlags |= STARTF_USESTDHANDLES
hStdInput  = INVALID_HANDLE_VALUE
hStdOutput = INVALID_HANDLE_VALUE
hStdError  = INVALID_HANDLE_VALUE
```

Without this, command output can appear on the parent's console instead of the capture pipe. `CREATE_NO_WINDOW` alone was insufficient in testing.

### Handle pitfalls

| Mistake | Symptom |
|---|---|
| Not closing ConPTY pipe ends after `CreatePseudoConsole` | Missing output, hung reads |
| Closing `inputWrite` before the child is ready | `STATUS_CONTROL_C_EXIT` (0xC000013A) |
| `CREATE_NEW_CONSOLE` with ConPTY | Wrong console attachment |
| Serial read while child blocks on full pipe buffer | Deadlock |

### Minimum Windows version

ConPTY requires Windows 10 1809+ / Windows 11. scenetake does not use winpty.

## Unix: openpty + fork

scenetake uses `openpty` + `fork` rather than `forkpty` to keep explicit control over `setsid`, `TIOCSCTTY`, `chdir`, and `execvp`. This matches P0/P1 needs without a wrapper API.

### Child setup

```text
close(master)
setsid()
ioctl(slave, TIOCSCTTY, 0)
dup2(slave, 0..2)
execvp via UTF-8 argv built with NativeMemory (byte**)
```

`execvp` uses `LibraryImport` with `byte* file` and `byte** argv` — not `string[]` marshalling — so NativeAOT does not depend on runtime array marshalling for the exec boundary. Each argument is a null-terminated UTF-8 C string; the pointer array ends with `NULL`.

`execve` (explicit environment block) is a future option if scenetake needs to override `TERM` or other variables independently of the parent process.

### Parent I/O

1. `close(slave)` after fork.
2. Start background read on `master`.
3. If no input bytes, `shutdown(master, SHUT_WR)` to send EOF without closing the read side.
4. `waitpid`, then `close(master)` after the read task drains.

### Platform differences

Linux and macOS are similar but not identical (`ioctl`, `termios`, `ptsname_r`, job control). Keep one `UnixPseudoTerminal` path but expect platform-specific fixes. Do not assume Linux-only `ioctl` values work on macOS without verification.

## Shared rules

### Byte streams

PTY output is a **byte stream**, not lines or Unicode strings:

- Do not translate `\n` ↔ `\r\n`.
- Do not use line-based APIs (`ReadLine`) as the primary read path.
- ANSI sequences may arrive split across reads; the cast layer stores chunks as received (decoded with the console output encoding for text chunks).

`TerminalChunkReader` timestamps each read while a `Stopwatch` runs during child lifetime.

### Terminal size

Use scenario `width` × `height` (terminal cells, not pixels). Windows: `COORD` for `CreatePseudoConsole`. Unix: `winsize` in `openpty`.

Resize (`ResizePseudoConsole` / `TIOCSWINSZ`) is not implemented in Phase 1.

### Environment variables

Phase 1 inherits the parent environment. Unix tools often expect `TERM=xterm-256color`; Windows behavior varies. Do not set `TERM` on Windows unless a specific tool requires it.

### Shutdown ordering

PTY shutdown is timing-sensitive. General pattern:

```text
1. Detect child exit (wait / WaitForSingleObject)
2. Close PTY input to the child (pipe write end / shutdown SHUT_WR)
3. Close ConPTY (Windows) or master fd (Unix) after draining output
4. Release process handles
```

Exact ordering differs slightly by OS; avoid forcing one sequence if it causes hangs on one platform.

## Concurrency

At minimum, separate:

- **Output read** — background task reading the PTY while the child runs
- **Process wait** — foreground wait for exit
- **Input write** — optional; close when done to signal EOF

PTY is full-duplex; serializing read and wait on one thread risks deadlock when buffers fill.

## Anti-patterns

| Anti-pattern | Why |
|---|---|
| Pipe redirect without ConPTY (Windows) | No TTY semantics |
| Direct `.exe` launch bypassing scenario shell | Breaks cmd builtins and PowerShell cmdlets |
| winpty / external PTY helpers | Extra dependency; conflicts with NativeAOT single-binary goal |
| Line-based PTY API | Breaks ANSI and binary-safe capture |
| Coupling PTY to VT parsing | Complicates testing and SVG pipeline |
| Golden `.cast` PTY tests | OS/timing variance; use property assertions |

## Testing (implementers)

See [spec_pty.md](../spec_pty.md) → Verification for CI layout. Additional notes:

| Check | Why |
|---|---|
| TTY detection (`redirected=False`, `isatty`) | Confirms P0 |
| Simple shell command (`echo`, `Write-Output`) | Confirms P1 |
| Short TUI (`matrix 3`) | Confirms chunked ANSI capture |
| Property asserts, not byte-identical casts | Reduces flaky CI |

PTY-layer tests call `PseudoTerminal.Run` directly. Integration tests require `SCENETAKE_BIN` pointing at a published binary (avoids `dotnet run` file locks).

High-volume output and interactive programs (vim, REPL) are useful stress tests but out of Phase 1 scope.

## Future phases

Not implemented yet; listed for planning:

| Phase | Features |
|---|---|
| **1 (current)** | spawn, read, wait, exit code, timestamped chunks, shell launch |
| **2** | resize, explicit `CloseInput`, Ctrl-C (`\x03` write), cancellation, `execve` env control |
| **3+** | Long-lived interactive sessions, disk spill for huge captures |

## NativeAOT interop

- Use `[LibraryImport]` (source-generated P/Invoke), not `[DllImport]`. `AllowUnsafeBlocks` is required in the project.
- Windows `CreateProcessW` takes a writable `char[]` command line; `InitializeProcThreadAttributeList` size query uses `ref nuint` (not `out`).
- Unix `execvp` is declared as `execvp(byte* file, byte** argv)`. The child builds a UTF-8 `argv` with `NativeMemory.Alloc` — do not marshal `string[]` across the exec boundary.
- `execve` remains an option when scenetake needs an explicit environment block instead of inheriting the parent env.

## References

- [Creating a pseudoconsole session (Microsoft Learn)](https://learn.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session)
- [Windows Terminal ConPTY samples](https://github.com/microsoft/terminal/tree/main/samples/ConPTY)
- [spec_pty.md](../spec_pty.md) — recording behavior and scope
