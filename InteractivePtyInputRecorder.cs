using System.Diagnostics;
using MiniPty.Console;

/// <summary>
/// Records host keyboard bytes forwarded by <see cref="PtyConsoleInput.Attach"/> during an interactive PTY step.
/// </summary>
internal sealed class InteractivePtyInputRecorder(Stopwatch stopwatch, List<PtyInputChunk> chunks) : IPtyConsoleInputObserver
{
    public void OnForwardedInput(TimeSpan elapsed, ReadOnlySpan<byte> data)
    {
        _ = elapsed;
        if (data.IsEmpty)
            return;

        chunks.Add(new PtyInputChunk(stopwatch.Elapsed, data.ToArray().AsMemory()));
    }
}

/// <summary>
/// Timestamped host→PTY input bytes captured during an interactive step (same clock as <see cref="PtyCaptureChunk"/> output).
/// </summary>
internal readonly record struct PtyInputChunk(TimeSpan Time, ReadOnlyMemory<byte> Data);
