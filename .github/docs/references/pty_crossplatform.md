# Cross-Platform PTY Implementation Reference

This document describes **how** scenetake implements pseudo-terminal recording. User-facing behavior lives in [spec_pty.md](../spec_pty.md).

Implementation code: `PseudoTerminal.cs` (included from `scenetake.cs`).

## Architecture

scenetake uses a single OS-specific backend selected at runtime:

| OS | API | Entry point |
|---|---|---|
| Windows | ConPTY (`CreatePseudoConsole`) | `WindowsPseudoTerminal.Start` |
| Linux / macOS / FreeBSD | `openpty` + `fork` + `execvp` | `UnixPseudoTerminal.Start` |

Upper layers (`RunCommandCore`, cast generation) call `PseudoTerminal.Run` (or `Start` + `WaitForExitAsync` for library callers) with the scenario shell executable and arguments. They receive a `CommandOutput` with timestamped `CommandOutputChunk` entries.

PTY capture and terminal display are separate:

| Layer | Responsibility | Code |
|---|---|---|
| **PTY backend** | Spawn child, attach PTY, read/write bytes, wait, exit code | `PseudoTerminal.cs` |
| **Terminal display** | Parse ANSI/VT, screen buffer, SVG rendering | `Terminal.cs`, `Svg.cs` |

Do not parse escape sequences inside the PTY backend. Do not spawn processes from the SVG renderer.

## Session lifecycle and cancellation

Library callers should prefer `PseudoTerminal.Start` → `PseudoTerminalSession` over blocking `Run` when they need timeouts or cooperative shutdown.

| API | Behavior |
|---|---|
| `PseudoTerminal.Start(...)` | Spawns the child and starts the background PTY read. Does not wait. |
| `WriteInput(string)` | Writes UTF-8 bytes to the PTY stdin. Does not close stdin. |
| `CloseInput()` | **Windows:** closes the ConPTY input pipe write end (true EOF). **Unix:** writes EOT (`0x04`, Ctrl-D) to the PTY master — see [Unix stdin EOF](#unix-stdin-eof) below. |
| `WaitForExitAsync(CancellationToken)` | Polls the child (`WaitForSingleObject` / `waitpid(WNOHANG)`). On cancellation, calls `Kill()` then throws `OperationCanceledException`. |
| `Kill()` | `TerminateProcess` (Windows) or `kill(SIGKILL)` (Unix). Does not release handles; call `Dispose` or `Complete` afterward. |
| `Complete(verbose)` | After exit, drains the read task and returns `CommandOutput`. |
| `Dispose()` | If the child is still running, **kills** it, then closes ConPTY/pipes/process handles. Unlike `System.Diagnostics.Process.Dispose`, this is intentional for short-lived PTY sessions. |
| `PseudoTerminal.Run(..., input: string?)` | `input: null` — stdin stays open (TUI / no stdin). `input: ""` or text — write (if non-empty) then `CloseInput()` before wait. |
| `PtyCaptureOptions.OutputEncoding` | Byte-to-text decoding for captured chunks (default `Encoding.UTF8`). |

The scenetake CLI uses `Run` with `CancellationToken.None` (no scenario-level timeout in Phase 1).

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
3. `CreatePseudoConsole(size, inputRead, outputWrite, …)` → `SafePseudoConsoleHandle` (`HPCON`). Returns **HRESULT** (not `GetLastError`); treat `hr < 0` as failure and use `Marshal.ThrowExceptionForHR(hr)` — do not pass the value to `Win32Exception` as a Win32 error code.
4. **Close** `inputRead` and `outputWrite` in the parent immediately (ConPTY holds its own duplicates).
5. Build `STARTUPINFOEX` with `PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE`.
6. `CreateProcessW` with `EXTENDED_STARTUPINFO_PRESENT` and `bInheritHandles = false`.
7. Start a background read on `outputRead` before or as the child runs.
8. On shutdown: if one-shot stdin was used, `CloseInput()` before wait → wait for child → `ClosePseudoConsole` → drain read task.

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
| Letting `HPCON` leak as raw `IntPtr` | Double-close or missed `ClosePseudoConsole` on error paths |
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

**`fork()` safety:** Build executable path, `argv`, and optional `cwd` as unmanaged UTF-8 C strings in the **parent** before `fork()`. The child path (`ChildMainAfterFork`) calls only libc P/Invoke (`close`, `setsid`, `ioctl`, `dup2`, `chdir`, `execvp`, `_exit`) — no managed allocation, no `RuntimeInformation`, no string marshalling. The parent frees the payload after `fork()`; the child retains a copy-on-write mapping until `execvp` replaces the address space.

`execvp` uses `LibraryImport` with `byte* file` and `byte** argv` — not `string[]` marshalling — so NativeAOT does not depend on runtime array marshalling for the exec boundary. Each argument is a null-terminated UTF-8 C string; the pointer array ends with `NULL`.

`execve` (explicit environment block) is a future option if scenetake needs to override `TERM` or other variables independently of the parent process.

### Parent I/O

1. `close(slave)` after fork.
2. Start background read on `master`.
3. If one-shot stdin was provided, `CloseInput()` before waiting — see [Unix stdin EOF](#unix-stdin-eof).
4. If no stdin bytes, leave the master open for writes until the child exits (TUI programs).
5. `waitpid`, then `close(master)` after the read task drains.

### Unix stdin EOF

PTY master fds are **not** sockets. `shutdown(master, SHUT_WR)` is invalid (`ENOTSOCK`) and must not be used. Closing the master fd ends both read and write, so the parent cannot keep draining output.

| Approach | When | scenetake |
|---|---|---|
| **EOT (`0x04`, Ctrl-D)** | One-shot / line-discipline programs (`cat`, shells) | `CloseInput()` writes EOT to the master |
| **Leave master open** | TUI / no stdin (`matrix`, `cmatrix`) | `Run(..., input: null)` — no `CloseInput()` |
| **`Kill()` / `close(master)` on dispose** | Forced teardown | `Dispose()` kills if still running, then `close(master)` |

EOT is a **terminal convention**, not a kernel EOF like closing a pipe. Raw-mode readers that never interpret `VEOF` may ignore it; that is acceptable for P0/P1 (shell-wrapped commands) but is a known limitation for future interactive (P3) work.

**Windows vs Unix asymmetry:** `CloseInput()` closes the ConPTY input pipe on Windows (true EOF). On Unix it sends EOT only. Callers using `PseudoTerminalSession` should treat `CloseInput()` as “I am done sending input” rather than assuming identical kernel semantics.

### Platform differences

Shared session logic and per-OS constants / `openpty` imports all live in `UnixPseudoTerminal` inside `PseudoTerminal.cs`. Runtime dispatch selects the correct pair; do not reuse Linux-only values on BSD.

| OS | `TIOCSCTTY` | `openpty` library |
|---|---|---|
| Linux | `0x540E` | `libc` (`LinuxOpenPty`) |
| macOS | `0x20007461` (`_IO('t', 97)`) | `libutil` (`MacOSOpenPty`) |
| FreeBSD | `0x20007461` (`_IO('t', 97)`) | `libutil` (`FreeBSDOpenPty`) |

`fork`, `setsid`, `ioctl`, `waitpid`, and other syscalls remain on `libc` in the shared partial class. macOS/FreeBSD CI must pass before claiming support on those platforms; do not assume Linux-only constants work elsewhere.

## Shared rules

### Byte streams

PTY output is a **byte stream**, not lines or Unicode strings:

- Do not translate `\n` ↔ `\r\n`.
- Do not use line-based APIs (`ReadLine`) as the primary read path.
- Decode PTY bytes with `PtyCaptureOptions.OutputEncoding` (default **UTF-8**). Do not use `Console.OutputEncoding` — in NativeAOT, containers, and CI it may not match the child terminal.

`TerminalChunkReader` timestamps each read while a `Stopwatch` runs during child lifetime.

### Terminal size

Use scenario `width` × `height` (terminal cells, not pixels). Windows: `COORD` for `CreatePseudoConsole`. Unix: `winsize` in `openpty`.

Resize (`ResizePseudoConsole` / `TIOCSWINSZ`) is not implemented in Phase 1.

### Environment variables

Phase 1 inherits the parent environment. Unix tools often expect `TERM=xterm-256color`; Windows behavior varies. Do not set `TERM` on Windows unless a specific tool requires it.

### Shutdown ordering

PTY shutdown is timing-sensitive. General pattern:

```text
1. Start background read on PTY output
2. One-shot stdin: WriteInput → CloseInput (Windows: pipe close; Unix: EOT) before wait
3. Detect child exit (wait / WaitForSingleObject)
4. Close ConPTY (Windows) or master fd (Unix) after draining output
5. Release process handles
```

Exact ordering differs slightly by OS; avoid forcing one sequence if it causes hangs on one platform.

## Concurrency

At minimum, separate:

- **Output read** — background task reading the PTY while the child runs
- **Process wait** — foreground wait for exit
- **Input write** — optional; `CloseInput()` when done (platform-specific EOF semantics)

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
