namespace Scenetake.Core;

/// <summary>A piece of a pipe step's output: when it arrived after the command started, which stream, and its text.</summary>
internal readonly record struct PipeStreamChunk(TimeSpan Time, bool IsStderr, string Text);

/// <summary>
/// PTY bytes with their time since the command started: MiniPty's output and input chunks on the CLI, or a
/// recorded step's chunks in the playground, which has no MiniPty.
/// </summary>
internal readonly record struct TimedBytes(TimeSpan Time, ReadOnlyMemory<byte> Data);
