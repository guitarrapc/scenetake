#:sdk Microsoft.NET.Sdk
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ImplicitUsings=enable
#:package MiniPty.Console@1.2.0
#:include ../Terminal.cs
#:include ../CastTimingPipeline.cs

using System.Diagnostics;
using System.Text;

var failures = 0;
failures += Run("OnForwardedInput_RecordsElapsedAndBytes", OnForwardedInput_RecordsElapsedAndBytes);
failures += Run("OnForwardedInput_IgnoresEmptySpan", OnForwardedInput_IgnoresEmptySpan);
failures += Run("BuildInputWindows_MergesBurst", BuildInputWindows_MergesBurst);
failures += Run("NormalizeOutput_StripsBackspaceCorrections", NormalizeOutput_StripsBackspaceCorrections);
failures += Run("NormalizeOutput_PreservesWallClockOutsideInput", NormalizeOutput_PreservesWallClockOutsideInput);
failures += Run("NormalizeOutput_PassthroughAnsiInEchoWindow", NormalizeOutput_PassthroughAnsiInEchoWindow);
failures += Run("NormalizeOutput_PassthroughBannerUtf8InEchoWindow", NormalizeOutput_PassthroughBannerUtf8InEchoWindow);
failures += Run("NormalizeOutput_RetimesEchoPreservesBytes", NormalizeOutput_RetimesEchoPreservesBytes);
failures += Run("NormalizeOutput_CollapsesLongIdleGap", NormalizeOutput_CollapsesLongIdleGap);
failures += Run("NormalizeOutput_RetimesUtf8Echo", NormalizeOutput_RetimesUtf8Echo);
failures += Run("NormalizeOutput_CountsMultiCharEchoUnits", NormalizeOutput_CountsMultiCharEchoUnits);
failures += Run("NormalizeOutput_PassthroughClampsToMonotonicEmitTime", NormalizeOutput_PassthroughClampsToMonotonicEmitTime);
failures += Run("CollapseIdle_PreservesNormalizedEmitSpacing", CollapseIdle_PreservesNormalizedEmitSpacing);

return failures == 0 ? 0 : 1;

static int Run(string name, Func<bool> test)
{
    try
    {
        if (test())
        {
            Console.Error.WriteLine($"ok {name}");
            return 0;
        }

        Console.Error.WriteLine($"FAIL {name}");
        return 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
        return 1;
    }
}

static bool OnForwardedInput_RecordsElapsedAndBytes()
{
    var stopwatch = Stopwatch.StartNew();
    var chunks = new List<PtyInputChunk>();
    var recorder = new InteractivePtyInputRecorder(stopwatch, chunks);

    recorder.OnForwardedInput(TimeSpan.Zero, "hello"u8);
    Thread.Sleep(5);
    recorder.OnForwardedInput(TimeSpan.FromSeconds(1), "!"u8);

    if (chunks.Count != 2)
        return false;
    if (Encoding.UTF8.GetString(chunks[0].Data.Span) != "hello")
        return false;
    if (Encoding.UTF8.GetString(chunks[1].Data.Span) != "!")
        return false;
    return chunks[1].Time >= chunks[0].Time;
}

static bool OnForwardedInput_IgnoresEmptySpan()
{
    var stopwatch = Stopwatch.StartNew();
    var chunks = new List<PtyInputChunk>();
    var recorder = new InteractivePtyInputRecorder(stopwatch, chunks);

    recorder.OnForwardedInput(TimeSpan.Zero, ReadOnlySpan<byte>.Empty);

    return chunks.Count == 0;
}

static bool BuildInputWindows_MergesBurst()
{
    var windows = InteractivePtyCastEmit.BuildInputEchoWindows([1.0, 1.2, 1.4, 5.0, 5.1]);
    if (windows.Count != 2)
        return false;
    if (Math.Abs(windows[0].Start - 1.0) > 0.001)
        return false;
    if (windows[0].End < 1.59)
        return false;
    return Math.Abs(windows[1].Start - 5.0) < 0.001;
}

static bool NormalizeOutput_StripsBackspaceCorrections()
{
    var windows = InteractivePtyCastEmit.BuildInputEchoWindows([1.0, 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7]);
    var inputs = new List<(double Time, ReadOnlyMemory<byte> Data)>
    {
        (1.0, "a"u8.ToArray().AsMemory()),
        (1.1, "b"u8.ToArray().AsMemory()),
        (1.2, "v"u8.ToArray().AsMemory()),
        (1.3, "\x7f"u8.ToArray().AsMemory()),
        (1.4, "c"u8.ToArray().AsMemory()),
        (1.5, "d"u8.ToArray().AsMemory()),
        (1.6, "e"u8.ToArray().AsMemory()),
    };
    var output = new List<(double Time, ReadOnlyMemory<byte> Data)>
    {
        (0.5, "> "u8.ToArray().AsMemory()),
        (1.1, "a"u8.ToArray().AsMemory()),
        (1.2, "b"u8.ToArray().AsMemory()),
        (1.3, "v"u8.ToArray().AsMemory()),
        (1.4, "\b \b"u8.ToArray().AsMemory()),
        (1.5, "c"u8.ToArray().AsMemory()),
        (1.6, "d"u8.ToArray().AsMemory()),
        (1.7, "e"u8.ToArray().AsMemory()),
    };

    var normalized = InteractivePtyCastEmit.NormalizeOutputChunks(output, windows, 0.05, 40, 3, inputs);
    var text = string.Concat(normalized.Select(c => Encoding.UTF8.GetString(c.Data.Span)));

    if (text.Contains('v'))
        return false;
    return text.Contains("abcde", StringComparison.Ordinal);
}

static bool NormalizeOutput_PreservesWallClockOutsideInput()
{
    var windows = InteractivePtyCastEmit.BuildInputEchoWindows([2.0]);
    var banner = "BANNER"u8.ToArray().AsMemory();
    var output = new List<(double Time, ReadOnlyMemory<byte> Data)>
    {
        (0.5, banner),
        (3.0, "z"u8.ToArray().AsMemory()),
    };

    var normalized = InteractivePtyCastEmit.NormalizeOutputChunks(output, windows, 0.05, 40, 3);
    if (normalized.Count != 2)
        return false;
    if (normalized[0].Time != 0.5)
        return false;
    return Encoding.UTF8.GetString(normalized[0].Data.Span) == "BANNER";
}

static bool NormalizeOutput_CollapsesLongIdleGap()
{
    var banner = "BANNER"u8.ToArray().AsMemory();
    var output = new List<(double Time, ReadOnlyMemory<byte> Data)>
    {
        (0.0, banner),
        (0.1, banner),
        (12.0, banner),
        (12.1, banner),
    };

    var normalized = InteractivePtyCastEmit.NormalizeOutputChunks(output, [], 0.05, 40, 3);
    InteractivePtyCastEmit.CollapseIdleAfterNormalize(normalized, silenceThreshold: 2.0, maxIdle: 0.2);
    if (normalized.Count != 4)
        return false;
    if (Math.Abs(normalized[1].Time - 0.1) > 0.001)
        return false;
    // 12s wall gap → 0.2s collapsed; prior emit ends at 0.1
    return Math.Abs(normalized[2].Time - 0.3) < 0.001
        && Math.Abs(normalized[3].Time - 0.4) < 0.001;
}

static bool NormalizeOutput_PassthroughAnsiInEchoWindow()
{
    var windows = InteractivePtyCastEmit.BuildInputEchoWindows([1.0]);
    var ansi = "\u001b[1mX\u001b[0m"u8.ToArray().AsMemory();
    var output = new List<(double Time, ReadOnlyMemory<byte> Data)>
    {
        (1.1, ansi),
    };

    var normalized = InteractivePtyCastEmit.NormalizeOutputChunks(output, windows, 0.05, 80, 24);
    return normalized.Count == 1
        && normalized[0].Time == 1.1
        && normalized[0].Data.Span.SequenceEqual(ansi.Span);
}

static bool NormalizeOutput_PassthroughBannerUtf8InEchoWindow()
{
    var windows = InteractivePtyCastEmit.BuildInputEchoWindows([1.0]);
    var box = "└"u8.ToArray().AsMemory();
    var output = new List<(double Time, ReadOnlyMemory<byte> Data)>
    {
        (1.05, box),
    };

    var normalized = InteractivePtyCastEmit.NormalizeOutputChunks(output, windows, 0.05, 80, 24);
    return normalized.Count == 1
        && Math.Abs(normalized[0].Time - 1.05) < 0.001
        && normalized[0].Data.Span.SequenceEqual(box.Span);
}

static bool NormalizeOutput_RetimesEchoPreservesBytes()
{
    var windows = InteractivePtyCastEmit.BuildInputEchoWindows([1.0]);
    var inputs = new List<(double Time, ReadOnlyMemory<byte> Data)>
    {
        (1.0, "a"u8.ToArray().AsMemory()),
    };
    var output = new List<(double Time, ReadOnlyMemory<byte> Data)>
    {
        (1.01, "a"u8.ToArray().AsMemory()),
    };

    var normalized = InteractivePtyCastEmit.NormalizeOutputChunks(output, windows, 0.05, 80, 24, inputs);
    return normalized.Count == 1
        && normalized[0].Time >= 1.0
        && Encoding.UTF8.GetString(normalized[0].Data.Span) == "a";
}

static bool NormalizeOutput_RetimesUtf8Echo()
{
    var windows = InteractivePtyCastEmit.BuildInputEchoWindows([1.0]);
    var glyph = "あ"u8.ToArray().AsMemory();
    var inputs = new List<(double Time, ReadOnlyMemory<byte> Data)>
    {
        (1.0, glyph),
    };
    var output = new List<(double Time, ReadOnlyMemory<byte> Data)>
    {
        (1.05, glyph),
    };

    var normalized = InteractivePtyCastEmit.NormalizeOutputChunks(output, windows, 0.05, 80, 24, inputs);
    return normalized.Count == 1
        && Encoding.UTF8.GetString(normalized[0].Data.Span) == "あ";
}

static bool NormalizeOutput_CountsMultiCharEchoUnits()
{
    var windows = InteractivePtyCastEmit.BuildInputEchoWindows([1.0]);
    var output = new List<(double Time, ReadOnlyMemory<byte> Data)>
    {
        (1.0, "ab"u8.ToArray().AsMemory()),
        (1.1, "c"u8.ToArray().AsMemory()),
    };

    var normalized = InteractivePtyCastEmit.NormalizeOutputChunks(output, windows, 0.05, 80, 24);
    if (normalized.Count != 2)
        return false;

    // "ab" counts as 2 typing units → second chunk starts at 1.0 + 2 * 0.05
    return Math.Abs(normalized[1].Time - 1.1) < 0.001;
}

static bool NormalizeOutput_PassthroughClampsToMonotonicEmitTime()
{
    var windows = InteractivePtyCastEmit.BuildInputEchoWindows([1.0]);
    var ansi = "\u001b[1mX\u001b[0m"u8.ToArray().AsMemory();
    var output = new List<(double Time, ReadOnlyMemory<byte> Data)>
    {
        (1.0, "a"u8.ToArray().AsMemory()),
        (1.0, "b"u8.ToArray().AsMemory()),
        (1.0, "c"u8.ToArray().AsMemory()),
        (1.01, ansi),
    };

    var normalized = InteractivePtyCastEmit.NormalizeOutputChunks(output, windows, 0.05, 80, 24);
    if (normalized.Count != 4)
        return false;

    for (var i = 1; i < normalized.Count; i++)
    {
        if (normalized[i].Time < normalized[i - 1].Time)
            return false;
    }

    return normalized[3].Time >= normalized[2].Time;
}

static bool CollapseIdle_PreservesNormalizedEmitSpacing()
{
    var chunks = new List<(double Time, double OriginalTime, ReadOnlyMemory<byte> Data)>
    {
        (0.0, 0.0, "a"u8.ToArray().AsMemory()),
        (0.15, 0.1, "b"u8.ToArray().AsMemory()),
        (12.0, 12.0, "c"u8.ToArray().AsMemory()),
        (12.15, 12.1, "d"u8.ToArray().AsMemory()),
    };

    InteractivePtyCastEmit.CollapseIdleAfterNormalize(chunks, silenceThreshold: 2.0, maxIdle: 0.2);
    if (Math.Abs(chunks[1].Time - 0.15) > 0.001)
        return false;
    if (Math.Abs(chunks[2].Time - 0.35) > 0.001)
        return false;
    return Math.Abs(chunks[3].Time - 0.50) < 0.001;
}

// Mirrors scenetake.cs — isolated here so this test does not pull in the full CLI entry point.
sealed class InteractivePtyInputRecorder(Stopwatch stopwatch, List<PtyInputChunk> chunks) : MiniPty.Console.IPtyConsoleInputObserver
{
    public void OnForwardedInput(TimeSpan elapsed, ReadOnlySpan<byte> data)
    {
        _ = elapsed;
        if (data.IsEmpty)
            return;

        chunks.Add(new PtyInputChunk(stopwatch.Elapsed, data.ToArray().AsMemory()));
    }
}

readonly record struct PtyInputChunk(TimeSpan Time, ReadOnlyMemory<byte> Data);

internal readonly record struct ResolvedTheme(string Fg, string Bg, string Palette);
