# scenario2cast

Generate [asciinema v2 cast](https://docs.asciinema.org/manual/asciicast/v2/) files from YAML scenario files.

You do not need to install or launch asciinema to record. Write a YAML scenario with commands, and this tool executes those commands and emits a cast file with simulated typing plus real command output.

## How It Works

1. List commands in a YAML scenario.
2. The tool writes cast events that look like human typing (with random jitter).
3. The tool executes each command and writes real output to the cast.

## Requirements

- .NET 10 SDK (for C# file-based apps)

## Usage

```bash
dotnet run scenario2cast.cs <scenario.yaml> [output.cast]
```

If `output.cast` is omitted, the output is written next to the scenario file with the `.cast` extension.

```bash
# Basic
dotnet run scenario2cast.cs examples/basic.yaml

# Specify output path
dotnet run scenario2cast.cs examples/basic.yaml demo.cast

# Play with asciinema
asciinema play demo.cast

# convert to gif with agg
docker run --rm -v "$($PWD.Path):/data" kayvan/agg /data/examples/basic.cast /data/examples/basic.gif
```

## Scenario Format

```yaml
title: "Demo Title"    # Optional cast title
width: 120              # Terminal width (default: 120)
height: 24              # Terminal height (default: 24)
cwd: /path/to/dir       # Optional working directory for all commands

settings:
  prompt: "$ "
  typing_speed: 0.05       # Seconds per character (average)
  typing_jitter: 0.015     # Random jitter (+/- seconds)
  pre_command_delay: 0.8   # Pause before typing next command
  post_command_delay: 1.5  # Pause after output before next prompt

commands:
  # Simple string command
  - echo "Hello, World!"
  - ls -la

  # Mapping command with per-command overrides
  - cmd: git log --oneline -10
    post_delay: 3.0

  - cmd: git status
    typing_speed: 0.10
    pre_delay: 1.5
    post_delay: 2.0
```

### Command Keys

| Key | Description | Default |
|------|------|-----------|
| `cmd` | Command to execute | required |
| `typing_speed` | Seconds per typed character | `settings.typing_speed` |
| `typing_jitter` | Typing jitter range | `settings.typing_jitter` |
| `pre_delay` | Pause before command typing | `settings.pre_command_delay` |
| `post_delay` | Pause after command output | `settings.post_command_delay` |

## Notes

- Commands run in the system default shell (`$SHELL` on Linux/macOS, `COMSPEC` on Windows).
- Avoid interactive commands such as `vim` or `htop`.
- On Windows, `echo "text"` may output with quotes; use `echo text` or run via Git Bash/WSL if needed.
- Commands are executed for real, so be careful with side effects.

## Examples

```bash
dotnet run scenario2cast.cs examples/basic.yaml
dotnet run scenario2cast.cs examples/git-demo.yaml
```
