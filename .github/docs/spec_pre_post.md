# Pre/Post Command Specification

Status: **Implemented**

## Motivation

scenetake records the visible command flow described by `steps`. Scenarios often need setup and teardown around that flow—preparing state, starting helpers, cleaning up—without polluting the cast. Top-level `pre` and `post` run those commands outside the recording.

Failed recorded steps can still be legitimate demo content; setup and teardown failures should instead fail the scenario run.

## Scope

### In scope

- Top-level `pre` (before `steps`) and `post` (last: after the cast write and any render, or as soon as the run stops with an error after `pre` succeeded).
- Same resolved `shell` and `cwd` as `steps`.
- stdout/stderr visible in the CLI; command text and output never written to the cast.
- Fail-fast for `pre` and `post`.

### Out of scope

- Step-level `pre` or `post`; map/object entries.
- Coloring, timing, or display metadata on `pre`/`post` commands.
- Recording, retrying, or continuing past a failed `pre`/`post` command.

YAML structure for `pre`/`post`: [spec_scenario.md](spec_scenario.md). CLI logging: [spec_cli.md](spec_cli.md). What is recorded into the cast: [spec_cast.md](spec_cast.md) → Recording boundary.

## Failure Behavior

| Phase | On failure |
|---|---|
| Scenario errors found before `pre` (a scenario file that cannot be read or parsed, render settings, terminal size, a step `run` given as a list or a mapping, step keys that contradict each other, outputs, `--format mp4` needs, a shell that cannot be found, on Unix one given by an absolute path with no `..`, and a shell with a NUL character) | Error; nothing runs; exit `1`. |
| `pre` | Fail-fast; `steps`, cast write, and `post` are skipped; exit with the command's code. |
| An output check fails after `pre` (folder missing, or `pre` put a directory or a link to the scenario where an output goes) | `steps` and cast write are skipped; `post` runs; exit `1`, or the failing `post` command's code ([spec_cli.md](spec_cli.md) → Output Paths). |
| `steps` | Recording continues. Non-zero exits warn on stderr; scenetake exits `0` unless another fatal error occurs. See [spec_scenario.md](spec_scenario.md) → Step exit codes. A step error that stops the recording, such as an interactive step without a terminal or a PTY that cannot start, skips the cast write; `post` runs; exit `1`, or the failing `post` command's code. |
| Cast write | Error; `post` runs; exit `1`, or the failing `post` command's code. |
| `post` | Fail-fast; remaining `post` commands skipped; cast file retained; exit with the command's code. |

If no `pre` or `post` command fails and nothing else does, scenetake exits `0` regardless of individual step exit codes.

## Lessons Learned

- Cleanup belongs after cast write so users keep the recording when teardown fails.
- An error after `pre` succeeded used to skip `post`, which left what `pre` set up in place: a step key error found only when its step was reached, a PTY that could not start, and a cast write that failed because a step removed the output folder. The last two ended scenetake with an unhandled exception. Step keys are now checked before `pre`, and every error that stops the run after `pre` runs `post`.
