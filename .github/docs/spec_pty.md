# PTY specification

User-facing behavior for `pty: true` in scenetake scenarios and the underlying [MiniPty](https://github.com/guitarrapc/MiniPty) libraries.

## Scope

| Layer | Package | Responsibility |
|-------|---------|----------------|
| PTY session | **MiniPty** | Spawn, `Input`/`Output` streams, `SendEof`, `Resize`, `WaitForExitAsync`, `CompleteAsync` |
| Timestamped capture | **MiniPty.Capture** (0.3.x) | `PtyCapture.RunAsync` → `PtyCaptureResult` with `Chunks` |
| Cast generation | **scenetake** | `PtyCaptureChunk` → cast `o` events; YAML `pty: true` |

Recording semantics (timestamps, chunks) are **not** part of the core PTY API. scenetake depends on **MiniPty** + **MiniPty.Capture**.

## YAML

```yaml
pty: true   # optional per step; default false
width: 120  # scenario-level terminal columns
height: 24  # scenario-level terminal rows
```

When `pty: true`:

- The command runs in a pseudo-terminal (TTY semantics for the child).
- stdout and stderr are merged into one byte stream.
- Output is captured with timestamps via `MiniPty.Capture`.
- Cast events use chunk times relative to command start in the scenario timeline.

When `pty: false` (default):

- stdout and stderr are captured separately via pipe redirect.
- Typing animation and prompt are simulated in the cast.

## Platform support

| OS | Backend |
|----|---------|
| Windows 10 1809+ | ConPTY (`CreatePseudoConsole`) |
| Linux | `openpty` + `fork` + `execvp` |
| macOS | `openpty` + `fork` + `execvp` |
| FreeBSD | `openpty` + `fork` + `execvp` |

Pipe redirect without ConPTY is **not** a PTY. TUI tools (`matrix`, `vim`, etc.) require `pty: true` on Windows.

## MiniPty core API

### `Pty.Start(PtyStartInfo)` → `PtySession`

Spawns a child. Does not wait for exit.

| Member | Behavior |
|--------|----------|
| `Input` / `Output` | Read/write streams (bytes; no line translation) |
| `WriteInputAsync` | UTF-8 (default) or byte write helpers |
| `SendEof()` | End stdin (platform-specific; see reference doc) |
| `Resize(PtySize)` | Terminal resize |
| `WaitForExitAsync` | Wait for child exit only; **cancel = stop waiting, child keeps running** |
| `CompleteAsync` | Pump output, optional stdin, wait, drain, return `PtyResult` |
| `Kill()` | SIGKILL / `TerminateProcess` |
| `HasExited` / `ExitCode?` | Poll exit state; `ExitCode` is null until exited |
| `Dispose` | Kill if still running, release handles |

### `PtyCompleteOptions`

Used by `CompleteAsync` (and by Capture via composition):

| Option | Default | Purpose |
|--------|---------|---------|
| `OutputEncoding` | UTF-8 | Decode PTY bytes |
| `Input` | null | Stdin text; null = leave open (TUI) |
| `SendEofAfterInput` | true | Call `SendEof` after writing input |
| `ExitTimeout` | null | Max wait for child exit |
| `OutputDrainGrace` | 1s | Drain after exit before closing transport |
| `OutputReaderCloseTimeout` | 5s | Wait for reader after transport close |
| `KillOnCancellation` | true | **CompleteAsync only** — cancel kills child |

### Backpressure warning

A PTY has backpressure. If the child writes output and nobody reads `PtySession.Output`, the child may block when the terminal buffer is full. Use `CompleteAsync`, `PtyCapture.RunAsync`, or continuously read `Output`.

## MiniPty.Capture API

```csharp
PtyCaptureResult result = await PtyCapture.RunAsync(startInfo, options);
// result.Output   — merged text
// result.ExitCode
// result.Chunks   — PtyCaptureChunk(TimeSpan Time, string Data)
```

- `PtyCaptureOptions.Completion` wraps `PtyCompleteOptions` (drain, stdin, timeouts).
- Chunk `Time` is elapsed since **session start** (`SessionStart`).
- Future capture-only options (not implemented yet): `TimeProvider`, `ChunkTimestampMode`, `MaxChunkSize`, etc.

## scenetake integration

1. Resolve shell and scenario `width` × `height`.
2. `await PtyCapture.RunAsync(...)` for `pty: true` steps.
3. Map each `PtyCaptureChunk` to `CastEvent.Output(scenarioTime + chunk.Time, data)`.
4. Pipe-redirected steps unchanged (`CommandExecution.FromPipe`).

PTY commands do not simulate typing or prompt in the cast (the real TUI output is recorded).

## Failure behavior

| Condition | Behavior |
|-----------|----------|
| Child non-zero exit | scenetake fails the scenario run (same as pipe commands) |
| Output drain timeout | `TimeoutException` from `CompleteAsync` / Capture |
| Cancel during `WaitForExitAsync` | Wait ends; child may still run |
| Cancel during `CompleteAsync` | Child killed when `KillOnCancellation` is true (default) |
| Dispose while running | Child killed |

## Verification

| Test | Location |
|------|----------|
| MiniPty core + Capture | `MiniPty/tests/MiniPty.Tests` |
| PTY layer + integration | `scenetake/tests/pty_test.cs` |
| Fixture scenarios | `scenetake/tests/fixtures/pty-*.yaml` |

Integration tests require `SCENETAKE_BIN` pointing at a published scenetake binary.

## Related documents

- [spec_scenario.md](spec_scenario.md) — `pty` YAML key
- [MiniPty spec.md](https://github.com/guitarrapc/MiniPty/blob/main/.github/docs/spec.md) — library API contract
- [MiniPty references/pty_crossplatform.md](https://github.com/guitarrapc/MiniPty/blob/main/.github/docs/references/pty_crossplatform.md) — OS implementation reference
- [spec_cast.md](spec_cast.md) — cast event format
