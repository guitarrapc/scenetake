# Interactive PTY Recording Plan

Status: **Planned** (grill-me complete; implementation not started)

## Motivation

scenetake records terminal demos from YAML scenarios. Today `pty: true` steps use **MiniPty.Capture** for one-shot PTY runs (`matrix`, `copilot --banner` with a fixed command string, etc.). That works when the command runs unattended and exits on its own.

It does **not** support a human operating a TUI on the **host terminal** — for example editing in **vim**, or interacting with a rich TUI until the user finishes. **MiniPty.Console** (`PtyConsoleInput.Attach`) exists for this use case (MiniPty use case 3); scenetake should embed it so interactive steps stay inside the existing scenario workflow rather than requiring a separate recording tool.

A second motivation (deferred) is **typing normalization**: the author should not have to type carefully for the demo. Mistakes corrected with Backspace should not appear in the final cast (`abv` + Backspace + `cde` → `abcde` at `typing-speed`). That layer is **out of v1 scope**; v1 records real PTY output at real wall-clock times.

## Decisions (grill-me summary)

| Topic | Decision |
|---|---|
| Entry point | YAML step extension: `pty: true` + `interactive: true` (not a new subcommand) |
| Step termination | Child process exit (`:wq`, `exit`, command completion) |
| Command entry UX | Keep simulated typing (`$ vim …` + Enter) before PTY starts — same as non-interactive PTY steps |
| v1 recording | **Real PTY bytes + wall-clock chunk times** (no typing normalization) |
| Cast timing (v1) | **Option C**: raw measured times; **only** `PtyLeadingInitFilter` from existing PTY path. No startup offset compress, burst merge, idle collapse, or `stream-output` pipeline for interactive steps |
| TUI animations | Record at actual duration (e.g. `copilot --banner` banner motion) |
| Typing normalization | **Deferred** — optional future `normalize-typing: true` per step |
| Normalization mechanism (future) | Screen-state diff on `ScreenBuffer` for insert-mode corrections; input tap via MiniPty.Console callback only if needed later |
| Normalization v1 scope (future) | Insert-mode end-of-line typing + Backspace; Normal-mode `hjkl` silent; `x`/`dd`/`dw` → v2 |
| Package wiring | Local dev via **`nuget.config`** pointing at `../MiniPty/publish`; pack MiniPty with `scripts/pack-with-native.sh` |
| New dependency | `MiniPty.Console` (plus existing `MiniPty`, `MiniPty.Capture` for non-interactive PTY) |
| CI | Keep building against nuget.org versions; interactive smoke remains manual / TTY-only skip. Local feed is developer workflow only until Console is published |

## Non-goals (v1)

- `normalize-typing` and correction stripping
- Insert-mode / vim state detection
- In-editor terminal (xterm.js) integration
- Changes to MiniPty.Console public API (unless implementation discovers a hard blocker)
- `max-duration` forced kill of interactive sessions (optional safety valve — defer unless needed during dogfooding)

## YAML contract

### Step keys

| Key | Default | Description |
|---|---|---|
| `pty` | `false` | Must be `true` when `interactive` is used |
| `interactive` | `false` | Human operates the host terminal until the child exits |

Existing per-step keys (`typing-speed`, `pre-delay`, `post-delay`, `name`, `run-highlight`, etc.) continue to apply. Simulated typing of `run` runs **before** the interactive PTY session starts.

### Validation

| Condition | Behavior |
|---|---|
| `interactive: true` and `pty: false` | Fatal error with clear message |
| Host stdin or stdout not a TTY | Fatal error at step entry (`interactive` requires a real console) |
| `Pty.IsSupported` false | Fatal error (same as existing PTY steps) |

### Examples

```yaml
settings:
  typing-speed: 0.05
  pre-delay: 0.4

steps:
  - name: "Copilot banner"
    run: copilot --banner
    pty: true
    interactive: true

  - name: "Edit README"
    run: vim README.md
    pty: true
    interactive: true
```

## Runtime flow (v1)

Per interactive step, after simulated typing is written to the cast timeline:

```
1. Verify host TTY + PTY support
2. Pty.Start(shell + run command)     // same shell launch as non-interactive PTY
3. Start ReadOutputAsync pump:
     - write each chunk to host stdout (user sees the TUI)
     - buffer (wall-clock time, bytes) for cast emit after session ends
4. PtyConsoleInput.Attach(session)
5. Link CancellationToken to WaitForExitAsync; PumpInputUntil(token)
6. Await output pump + exit code
7. Dispose console attach (restore host terminal)
8. Apply PtyLeadingInitFilter to buffered chunks; emit cast o events at commandStart + chunk_time
9. Continue scenario (post-delay, next step)
```

Embedder pattern follows [MiniPty ConsoleAttach sample](https://github.com/guitarrapc/MiniPty/blob/main/samples/ConsoleAttach.cs):

- Status/progress on **stderr** before attach
- Output pump **before** `Attach` (avoids ConPTY pipe stall on Windows)
- On Unix, optional `\r\n` on stdout after dispose to realign parent shell prompt

### Cast timing contrast

| Step kind | Timestamp source | Filters / pipeline |
|---|---|---|
| Non-interactive `pty: true` | Capture chunk times + startup offset + optional stream pipeline | `PtyLeadingInitFilter`, `PtyCastTiming`, `CastTimingPipeline` when `stream-output` / `max-duration` |
| Interactive `pty: true` | Capture chunk times **as measured** | **`PtyLeadingInitFilter` only** |
| Pipe steps | Unchanged | Unchanged |

## Local package workflow

### scenetake `nuget.config` (new file)

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="minipty-local" value="../MiniPty/publish" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

### Pack MiniPty (from MiniPty repo)

```bash
cd ../MiniPty
bash scripts/pack-with-native.sh 1.0.2-local ./publish
```

Use a `-local` (or similar) version suffix so local packages win over nuget.org `1.0.1`.

### scenetake.cs package lines (during development)

```csharp
#:package MiniPty@1.0.2-local
#:package MiniPty.Capture@1.0.2-local
#:package MiniPty.Console@1.0.2-local
```

Revert to published versions on nuget.org after MiniPty.Console release.

## Implementation plan

### Phase 1 — Interactive capture (this work)

1. **`nuget.config`** — local feed as above (committed; documents dev workflow).
2. **`scenetake.cs`**
   - Add `MiniPty.Console` package reference and `using`.
   - Parse `interactive` step key; validate `pty` + TTY preconditions.
   - Add `RunInteractivePtyAsync` (or extend `RunCommandCoreAsync`):
     - `Pty.Start` with existing `BuildPtyShellArguments`
     - Concurrent output pump → host stdout + in-memory chunk list with `Stopwatch` / `TimeSpan` timestamps
     - `PtyConsoleInput.Attach` + `PumpInputUntil` linked to child exit
     - Return `CommandExecution` with `PtyByteChunks` compatible with existing emit path
   - Add `EmitInteractivePtyStepOutput` (or flag on emit path):
     - `PtyLeadingInitFilter` only
     - No `PtyCastTiming.ComputeStartupOffset`
     - No `CastTimingSettings` stream pipeline
   - `GenerateAsync`: pass `interactive` flag; route emit to interactive path.
3. **Manual smoke**
   - `copilot --banner` interactive step → banner animates at real speed in SVG
   - `vim` interactive step → open file, edit, `:wq`; cast + SVG show session
4. **Tests**
   - Unit: validation (`interactive` without `pty` → error). TTY-gated attach tests skip when redirected (mirror MiniPty.Console test style).
   - Optional fixture YAML for integration when `SCENETAKE_BIN` + TTY available.

### Phase 2 — Docs (after Phase 1 dogfood)

- Update `.github/docs/spec_pty.md` — move interactive sessions from “out of scope” to implemented subset
- Update `.github/docs/spec_scenario.md` — `interactive` key
- Sample YAML under `samples/` (e.g. `interactive-vim.yaml`) if a portable smoke command exists
- Lessons learned appended below

### Phase 3 — Typing normalization (future)

- `normalize-typing: true` opt-in per step
- Default `interactive: true` remains real-time
- Screen diff layer for insert-mode correction stripping + `typing-speed` re-time
- See “Future: typing normalization” below

## Code touchpoints (scenetake)

| Area | Change |
|---|---|
| `GenerateAsync` | Read `interactive`; branch execution + emit |
| `RunCommandCoreAsync` | Delegate interactive to new path; keep `PtyCapture.RunAsync` for non-interactive |
| `EmitPtyStepOutput` | Split or parameterize: interactive omits startup offset and `CastTimingSettings` |
| `CommandExecution` | Unchanged shape if chunk list matches `PtyCaptureChunk` |
| `tests/pty_test.cs` | Validation tests; optional interactive integration behind TTY guard |

## Future: typing normalization

Not v1. Captured here so grill-me context is not lost.

### User goal

Authors type naturally; cast shows polished typing at `typing-speed`. Backspace corrections are invisible in the recording.

### Recommended design (when implemented)

| Layer | Responsibility |
|---|---|
| Layer 1 (`interactive: true`) | Raw PTY + wall-clock (required) |
| Layer 2 (`normalize-typing: true`) | Post-process or inline filter: screen diff on `ScreenBuffer`, prefix tracking on edit text, shrink → silent rollback, grow → emit chars at `typing-speed` |

### v1 normalization scope (agreed for future, not current work)

| Scope | Include |
|---|---|
| Insert mode, end-of-line typing + Backspace | Yes |
| Normal mode `hjkl` (cursor only) | Do not emit extra cast events |
| Normal mode `x` / `dd` / `dw` | v2 |

### Why not input tap first

MiniPty.Console owns host stdin. Clean input tap needs an optional callback on `Attach`. Screen diff reuses existing `Terminal.cs` / `ScreenBuffer` and aligns with “record what appeared on screen.” Input tap remains a v2 enhancement for echo-off edge cases.

## Test plan

- [ ] `interactive: true` without `pty: true` → parse/validation error
- [ ] Non-TTY host → clear error when step runs (or skip in CI with message)
- [ ] Non-interactive PTY fixtures still pass (`pty_test`, `pty_timing_test`, `pty_continue_test`)
- [ ] Manual: `copilot --banner` — motion timing looks real in cast/SVG
- [ ] Manual: `vim file` — edit, `:wq`, cast contains session; next scenario step composes if applicable
- [ ] Manual: Windows Terminal + Unix terminal keyboard smoke (per MiniPty.Console lessons)

## Risks and mitigations

| Risk | Mitigation |
|---|---|
| ConPTY stall if output pump starts late | Start `ReadOutputAsync` pump before `Attach` (Console spec) |
| Host terminal left raw after crash | `using` dispose on `PtyConsoleInputHandle`; document Ctrl+C behavior |
| stderr interleaving with raw stdout on Unix | Status only on stderr before attach |
| Local nuget feed missing in CI | CI uses nuget.org; `#:package` version without `-local` on main branch after release |
| vim / copilot not installed on dev machine | Samples document prerequisites; tests skip |

## Lessons learned

_(Empty until implementation.)_
