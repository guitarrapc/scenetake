# Pre/Post Command Specification

Status: **Implemented**

## Motivation

scenario2cast records the visible command flow described by `steps`. Some scenarios also need setup and teardown work around that recorded flow, such as preparing a repository state, creating temporary files, stopping helper processes, or cleaning generated files.

Those operations are part of scenario execution, but they are not part of the cast content. This spec defines top-level `pre` and `post` commands so scenarios can prepare and restore state without polluting the generated asciinema cast.

## Scope

### In scope

- Top-level `pre` commands executed before `steps`.
- Top-level `post` commands executed after the cast file is written.
- `pre` and `post` as string arrays only.
- Shared `shell` and `cwd` behavior with `steps`.
- CLI-visible stdout/stderr for `pre` and `post` commands.
- `pre` and `post` output excluded from cast events.
- Fail-fast behavior for `pre` and `post`.
- `--verbose` CLI option for normal `pre`/`post` execution logs.

### Out of scope

- Step-level `pre` or `post`.
- Map/object form for `pre` or `post` entries.
- `name`, `highlight`, `run-highlight`, `stderr-color`, timing, or display metadata on `pre` or `post` commands.
- Recording `pre` or `post` command text or output into the cast file.
- Retrying failed `pre` or `post` commands.
- Running remaining `post` commands after one `post` command fails.

## YAML Contract

`pre` and `post` are top-level string arrays. Documentation and generated templates should present them in execution order: `pre` before `steps`, `post` after `steps`. YAML key order does not define behavior; this order is only for readability.

```yaml
settings:
  prompt: "$ "

pre:
  - dotnet build

steps:
  - run: dotnet test

post:
  - git clean -fd
```

Each array item is passed to the resolved shell as one command string, matching `steps[].run` behavior. Multi-line commands can be expressed with YAML block scalars and are still one array item.

```yaml
pre:
  - |
    set -euo pipefail
    dotnet build
```

Empty or whitespace-only `pre`/`post` entries are ignored, matching the existing empty-step behavior.

## Execution Order

The execution order is:

1. Resolve scenario settings, shell, cwd, deterministic seed, and deterministic timestamp from the YAML file.
2. Execute `pre` commands.
3. Execute and record `steps`.
4. Write the cast file.
5. Execute `post` commands.
6. Report final success or failure.

`pre` and `post` use the same resolved `shell` and `cwd` as `steps` because users expect scenario setup, recording, and teardown to run in the same execution environment.

`post` runs after the cast file is written. If `post` fails, the already written cast file remains in place because the recorded `steps` have already run and their result is still useful.

## Recording Behavior

`pre` and `post` are outside the recording flow.

Their stdout and stderr are printed to the CLI, preserving the original streams, but their command text and output are never written to the cast file.

Recorded cast events continue to come only from `steps` and their scenario display behavior.

## Failure Behavior

### `pre`

`pre` is fail-fast.

- Commands run in array order.
- Empty commands are skipped.
- If one `pre` command exits non-zero, execution stops immediately.
- `steps` are not executed.
- The cast file is not written.
- `post` is not executed.
- scenario2cast exits with the failed command's exit code.

### `steps`

`steps` remain recording targets.

A recorded step command may succeed or fail; either result is part of the intended recording. Step exit codes do not stop later steps and do not determine scenario2cast's exit code for this feature.

### `post`

`post` is fail-fast.

- `post` runs only after `pre` succeeds, `steps` have run, and the cast file has been written.
- Commands run in array order.
- Empty commands are skipped.
- If one `post` command exits non-zero, execution stops immediately.
- Remaining `post` commands are not executed.
- The already written cast file remains in place.
- scenario2cast exits with the failed command's exit code.

If no `pre` or `post` command fails, scenario2cast exits with code `0` regardless of individual `steps` command exit codes.

## CLI Logging

`pre` and `post` command stdout/stderr are emitted after each command exits, preserving stdout as stdout and stderr as stderr. Output does not need to be streamed live in v1.

Normal successful `pre`/`post` commands do not print command labels by default because they are outside the core cast content.

When a `pre` or `post` command fails, scenario2cast must print the failed phase, the full command text without truncation, and the exit code. Multi-line failed commands must not be abbreviated.

The `--verbose` CLI option enables normal execution labels and phase markers for `pre`/`post` commands. It does not change existing step logging.

- Existing `steps` `running:` logs remain always visible.
- Successful `pre`/`post` command labels are visible only with `--verbose`.
- `pre`/`post` failure details are always visible.
- Phase markers for `pre`, `steps`, and `post` are visible only with `--verbose`.

## CLI Option Contract

`--verbose` is accepted for scenario execution and may appear in any argument position.

Valid examples:

```text
scenario2cast --verbose samples/basic.yaml
scenario2cast samples/basic.yaml --verbose
scenario2cast samples/basic.yaml samples/basic.cast --verbose
```

Unknown `-` or `--` options are explicit errors so typos such as `--verbse` do not become confusing path-not-found errors.

`init` does not accept `--verbose`; it only keeps its existing help behavior.

## Init Template

The YAML generated by `scenario2cast init` should include commented `pre` and `post` templates so users discover the feature without enabling it by default.

Recommended placement:

```yaml
settings:
  prompt: "$ "

# pre:
#   - dotnet build

steps:
  - echo "Hello, World!"

# post:
#   - git clean -fd
```

## Documentation Requirements

README.md and README-ja.md must describe the same behavior with language as the only difference.

Both documents should explicitly state that `pre` and `post` commands run outside the recording flow: their stdout/stderr are visible in the CLI, but they are never written to the cast file.

## Determinism

The existing deterministic seed and timestamp behavior remains based on the whole YAML file. Adding or changing `pre` or `post` may therefore change deterministic cast metadata and typing jitter, even though `pre` and `post` output is not recorded.

This keeps the rule simple: the same full scenario file produces the same deterministic metadata.

## Lessons Learned

- Scenario setup and teardown are useful, but they should not be modeled as recorded steps because failed recorded commands are still legitimate demo content.
- `post` should run after cast writing so users keep the recording even when cleanup fails.
- `pre`/`post` failures should affect scenario2cast's exit code, while recorded step failures should not.
- Default logs should stay focused on cast generation; verbose logging is the right place for successful `pre`/`post` command labels.
