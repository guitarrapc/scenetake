# Pre/Post Command Specification

Status: **Implemented**

## Motivation

scenetake records the visible command flow described by `steps`. Scenarios often need setup and teardown around that flow—preparing state, starting helpers, cleaning up—without polluting the cast. Top-level `pre` and `post` run those commands outside the recording.

Failed recorded steps can still be legitimate demo content. Setup and teardown failures should instead fail the scenario run.

## Scope

### In scope

- Top-level `pre` (before `steps`) and `post` (last: after the cast write and any render, or as soon as the run stops with an error after `pre` succeeded).
- Same resolved `shell` and `cwd` as `steps`.
- stdout/stderr visible in the CLI. Command text and output never written to the cast.
- Fail-fast for `pre` and `post`.

### Out of scope

- Step-level `pre` or `post`. Map/object entries.
- Coloring, timing, or display metadata on `pre`/`post` commands.
- Recording, retrying, or continuing past a failed `pre`/`post` command.

YAML structure for `pre`/`post`: [spec_scenario.md](spec_scenario.md). CLI logging: [spec_cli.md](spec_cli.md). What is recorded into the cast: [spec_cast.md](spec_cast.md) → Recording boundary.

## Failure Behavior

| Phase | On failure |
|---|---|
| Validation before `pre` | No commands run. Exit `1`. Checks cover input parsing, render settings, terminal size, step structure, conflicting keys, `cwd`, outputs, mp4 requirements, and shell resolution. See [spec_scenario.md](spec_scenario.md) → Execution Order. |
| `pre` | Stop immediately. Skip `steps`, cast writing, and `post`. Return the failed command's exit code. |
| Output validation after `pre` | Skip recording and cast writing. Run `post`. Exit `1`, or return the failed `post` command's code. See [spec_cli.md](spec_cli.md) → Output Paths. |
| Recorded step exit | Normally, warn on non-zero exits and continue. `continue-on-error: true` suppresses the warning. `--strict` stops unexpected failures. See [spec_scenario.md](spec_scenario.md) → Step exit codes. |
| Fatal recording error | Skip cast writing. Run `post`. Exit `1`, or return the failed `post` command's code. Examples include PTY startup failure or loss of a required terminal. |
| Cast write | Run `post`. Exit `1`, or return the failed `post` command's code. |
| `post` | Stop immediately. Skip remaining `post` commands and retain the cast. Return the failed command's exit code. |

Without `--strict`, scenetake exits `0` despite non-zero recorded step exits, unless another failure occurs.

## Lessons Learned

- Cleanup belongs after cast write so users keep the recording when teardown fails.
- Errors after successful setup previously skipped cleanup. Examples included invalid step keys, PTY startup failure, and an output folder removed by a step. PTY and cast-write failures also produced unhandled exceptions. Step keys now undergo validation before `pre`. Errors after successful setup still run `post`.
