# Pre/Post Command Specification

Status: **Implemented**

## Motivation

scenetake records the visible command flow described by `steps`. Scenarios often need setup and teardown around that flow—preparing state, starting helpers, cleaning up—without polluting the cast. Top-level `pre` and `post` run those commands outside the recording.

Failed recorded steps can still be legitimate demo content; setup and teardown failures should instead fail the scenario run.

## Scope

### In scope

- Top-level `pre` (before `steps`) and `post` (after cast write).
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
| `pre` | Fail-fast; `steps`, cast write, and `post` are skipped; exit with the command's code. |
| `steps` | Recording continues. Non-zero exits warn on stderr; scenetake exits `0` unless another fatal error occurs. See [spec_scenario.md](spec_scenario.md) → Step exit codes. |
| `post` | Fail-fast; remaining `post` commands skipped; cast file retained; exit with the command's code. |

If no `pre` or `post` command fails, scenetake exits `0` regardless of individual step exit codes.

## Lessons Learned

- Cleanup belongs after cast write so users keep the recording when teardown fails.
