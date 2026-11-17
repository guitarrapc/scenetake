using System.Diagnostics;
using MiniPty;
using MiniPty.Console;

/// <summary>
/// Records host keyboard bytes forwarded by <see cref="PtyConsoleInput.Attach(PtySession, PtyConsoleAttachOptions)"/>
/// during an interactive PTY step.
/// </summary>
/// <remarks>
/// Keys typed while the child reads a password are not recorded. Such a child turns echo off and keeps canonical mode
/// on, so the keys never come back as output, and normalize-typing, which matches each recorded key to its echo, would
/// wait for them and drop every later echo in the step. The recording also never holds the password. The mode is read
/// from <see cref="PtySession.InputMode"/>, which is null on Windows, where every key is recorded. When such keys
/// were typed goes to <paramref name="hidden"/>, so --save-keys can say where a password belongs.
/// </remarks>
sealed class InteractivePtyInputRecorder(
    Stopwatch stopwatch,
    List<PtyInputChunk> chunks,
    Func<PtyInputMode?>? inputMode = null,
    List<TimeSpan>? hidden = null) : IPtyConsoleInputObserver
{
    public void OnForwardedInput(TimeSpan elapsed, ReadOnlySpan<byte> data)
    {
        _ = elapsed;
        if (data.IsEmpty)
            return;

        // MiniPty.Console calls this before it writes the bytes, so the mode is the one the child set while waiting for
        // them, the Enter that ends a password included.
        if (inputMode?.Invoke() is { Echo: false, Canonical: true })
        {
            hidden?.Add(stopwatch.Elapsed);
            return;
        }

        chunks.Add(new PtyInputChunk(stopwatch.Elapsed, data.ToArray().AsMemory()));
    }
}

/// <summary>
/// Timestamped host→PTY input bytes captured during an interactive step (same clock as <c>PtyCaptureChunk</c> output).
/// </summary>
readonly record struct PtyInputChunk(TimeSpan Time, ReadOnlyMemory<byte> Data);
